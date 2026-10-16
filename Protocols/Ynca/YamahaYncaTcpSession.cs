using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YamahaAvrNet.Exceptions;

namespace YamahaAvrNet.Protocols.Ynca
{
    /// <summary>
    /// Single TCP session with one reader and serialized GETs. The protocol has no request IDs;
    /// matching a received value proves observation, not exclusive request/response causality.
    /// Writes are opt-in at the controller level, serialized and followed by a GET.
    /// </summary>
    internal sealed class YamahaYncaTcpSession : IDisposable
    {
        private sealed class PendingRead
        {
            internal string Subunit;
            internal string Function;
            internal TaskCompletionSource<YamahaYncaMessage> Completion =
                new TaskCompletionSource<YamahaYncaMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private readonly string _host;
        private readonly int _port;
        private readonly TimeSpan _timeout;
        private readonly SemaphoreSlim _connectGate = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _commandGate = new SemaphoreSlim(1, 1);
        private readonly object _sync = new object();
        private readonly Dictionary<string, YamahaYncaMessage> _last =
            new Dictionary<string, YamahaYncaMessage>(StringComparer.OrdinalIgnoreCase);
        private TcpClient _tcp;
        private CancellationTokenSource _sessionCts;
        private PendingRead _pending;
        private bool _disposed;

        internal YamahaYncaTcpSession(string host, int port, TimeSpan timeout)
        {
            _host = host;
            _port = port;
            _timeout = timeout;
        }

        internal event EventHandler<YamahaYncaMessageEventArgs> MessageReceived;
        internal event EventHandler<YamahaYncaConnectionEventArgs> ConnectionChanged;

        internal bool IsConnected
        {
            get { lock (_sync) return _tcp != null && _sessionCts != null && !_sessionCts.IsCancellationRequested; }
        }

        internal IReadOnlyDictionary<string, YamahaYncaMessage> GetLastMessages()
        {
            lock (_sync)
                return new System.Collections.ObjectModel.ReadOnlyDictionary<string, YamahaYncaMessage>(
                    new Dictionary<string, YamahaYncaMessage>(_last, StringComparer.OrdinalIgnoreCase));
        }

        internal async Task ConnectAsync(CancellationToken ct)
        {
            ThrowIfDisposed();
            await _connectGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                ThrowIfDisposed();
                if (IsConnected) return;
                var tcp = new TcpClient();
                try
                {
                    Task opening = tcp.ConnectAsync(_host, _port);
                    using (var limit = CancellationTokenSource.CreateLinkedTokenSource(ct))
                    {
                        Task timer = Task.Delay(_timeout, limit.Token);
                        if (await Task.WhenAny(opening, timer).ConfigureAwait(false) != opening)
                        {
                            ct.ThrowIfCancellationRequested();
                            throw new YamahaTimeoutException("YNCA TCP connection timed out.");
                        }
                        limit.Cancel();
                        await opening.ConfigureAwait(false);
                    }
                    var session = new CancellationTokenSource();
                    lock (_sync)
                    {
                        if (_disposed) throw new ObjectDisposedException(nameof(YamahaYncaTcpSession));
                        _tcp = tcp;
                        _sessionCts = session;
                        _last.Clear();
                    }
                    // Exactly one read loop is associated with each physical TCP connection.
                    Task.Run(() => ReadLoopAsync(tcp, session));
                    RaiseConnectionChanged(true, "YNCA TCP connection established.");
                }
                catch (Exception ex)
                {
                    tcp.Close();
                    if (ex is SocketException || ex is IOException)
                        throw new YamahaConnectionException("Cannot connect to YNCA TCP endpoint.", ex);
                    throw;
                }
            }
            finally { _connectGate.Release(); }
        }

        internal async Task<YamahaYncaMessage> GetAsync(
            string subunit, string function, CancellationToken ct)
        {
            byte[] bytes = YamahaYncaCodec.Get(subunit, function);
            ThrowIfDisposed();
            await _commandGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                // After disconnect or a timed-out request, a fresh session prevents late replies
                // being mistaken for those of a subsequent GET.
                await ConnectAsync(ct).ConfigureAwait(false);
                PendingRead pending = new PendingRead
                {
                    Subunit = subunit,
                    Function = function
                };
                TcpClient tcp;
                CancellationTokenSource session;
                lock (_sync)
                {
                    tcp = _tcp;
                    session = _sessionCts;
                    if (tcp == null || session == null || session.IsCancellationRequested)
                        throw new YamahaConnectionException("YNCA connection was closed.");
                    _pending = pending;
                }
                try
                {
                    using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct, session.Token))
                    {
                        Task writer = tcp.GetStream().WriteAsync(bytes, 0, bytes.Length, deadline.Token);
                        Task timer = Task.Delay(_timeout, deadline.Token);
                        if (await Task.WhenAny(writer, timer).ConfigureAwait(false) != writer)
                        {
                            ct.ThrowIfCancellationRequested();
                            if (session.IsCancellationRequested)
                                throw new YamahaConnectionException("YNCA session closed while sending a GET.");
                            throw new YamahaTimeoutException("YNCA GET send timed out.");
                        }
                        await writer.ConfigureAwait(false);
                        timer = Task.Delay(_timeout, deadline.Token);
                        if (await Task.WhenAny(pending.Completion.Task, timer).ConfigureAwait(false) != pending.Completion.Task)
                        {
                            ct.ThrowIfCancellationRequested();
                            if (session.IsCancellationRequested)
                                throw new YamahaConnectionException("YNCA session closed while waiting for a GET.");
                            throw new YamahaTimeoutException("YNCA GET did not receive a matching value in time.");
                        }
                        YamahaYncaMessage message = await pending.Completion.Task.ConfigureAwait(false);
                        if (message.Kind == YamahaYncaMessageKind.Restricted)
                            throw new YamahaFeatureTemporarilyUnavailableException("YNCA request was restricted by the receiver.");
                        if (message.Kind == YamahaYncaMessageKind.Undefined)
                            throw new YamahaFeatureNotSupportedException("YNCA request was undefined on this receiver.");
                        return message;
                    }
                }
                catch
                {
                    // A timeout, cancellation or transport fault leaves response ordering uncertain.
                    // Closing the session is safer than retrying on the same stream.
                    CloseSession("YNCA request aborted; session reset to avoid stale responses.", tcp, session);
                    throw;
                }
                finally
                {
                    lock (_sync)
                        if (ReferenceEquals(_pending, pending)) _pending = null;
                }
            }
            finally { _commandGate.Release(); }
        }

        /// <summary>
        /// Sends one PUT and observes a subsequent GET without releasing the command gate.
        /// PUT is never retried. If the connection fails after the send, the physical
        /// receiver state is uncertain and must be checked before any further change.
        /// </summary>
        internal async Task<YamahaYncaMessage> PutAndReadBackAsync(string subunit,
            string function, string value, CancellationToken ct)
        {
            byte[] put = YamahaYncaCodec.Put(subunit, function, value);
            ThrowIfDisposed();
            await _commandGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await ConnectAsync(ct).ConfigureAwait(false);
                TcpClient tcp;
                CancellationTokenSource session;
                lock (_sync)
                {
                    tcp = _tcp;
                    session = _sessionCts;
                    if (tcp == null || session == null || session.IsCancellationRequested)
                        throw new YamahaConnectionException("YNCA connection closed before PUT.");
                }
                try
                {
                    using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct, session.Token))
                    {
                        Task writer = tcp.GetStream().WriteAsync(put, 0, put.Length, deadline.Token);
                        Task timer = Task.Delay(_timeout, deadline.Token);
                        if (await Task.WhenAny(writer, timer).ConfigureAwait(false) != writer)
                        {
                            ct.ThrowIfCancellationRequested();
                            if (session.IsCancellationRequested)
                                throw new YamahaConnectionException("YNCA connection closed during PUT.");
                            throw new YamahaTimeoutException("YNCA PUT send timed out; receiver state is uncertain.");
                        }
                        await writer.ConfigureAwait(false);
                        // Do not treat a PUT as acknowledged: perform a separate GET.
                        // A queued unsolicited notification may still match that GET.
                        var pending = new PendingRead { Subunit = subunit, Function = function };
                        lock (_sync)
                        {
                            if (!ReferenceEquals(tcp, _tcp) || session.IsCancellationRequested)
                                throw new YamahaConnectionException("YNCA connection closed after PUT.");
                            _pending = pending;
                        }
                        try
                        {
                            byte[] get = YamahaYncaCodec.Get(subunit, function);
                            Task readWriter = tcp.GetStream().WriteAsync(get, 0, get.Length, deadline.Token);
                            timer = Task.Delay(_timeout, deadline.Token);
                            if (await Task.WhenAny(readWriter, timer).ConfigureAwait(false) != readWriter)
                            {
                                ct.ThrowIfCancellationRequested();
                                if (session.IsCancellationRequested)
                                    throw new YamahaConnectionException("YNCA connection closed during readback GET.");
                                throw new YamahaTimeoutException("YNCA readback GET send timed out.");
                            }
                            await readWriter.ConfigureAwait(false);
                            timer = Task.Delay(_timeout, deadline.Token);
                            if (await Task.WhenAny(pending.Completion.Task, timer).ConfigureAwait(false) != pending.Completion.Task)
                            {
                                ct.ThrowIfCancellationRequested();
                                if (session.IsCancellationRequested)
                                    throw new YamahaConnectionException("YNCA connection closed during readback.");
                                throw new YamahaTimeoutException("YNCA readback timed out; receiver state is uncertain.");
                            }
                            YamahaYncaMessage response = await pending.Completion.Task.ConfigureAwait(false);
                            if (response.Kind == YamahaYncaMessageKind.Restricted)
                                throw new YamahaFeatureTemporarilyUnavailableException("YNCA readback was restricted.");
                            if (response.Kind == YamahaYncaMessageKind.Undefined)
                                throw new YamahaFeatureNotSupportedException("YNCA readback was undefined.");
                            return response;
                        }
                        finally
                        {
                            lock (_sync)
                                if (ReferenceEquals(_pending, pending)) _pending = null;
                        }
                    }
                }
                catch
                {
                    CloseSession("YNCA write/readback failed; no automatic retry.", tcp, session);
                    throw;
                }
            }
            finally { _commandGate.Release(); }
        }

        private async Task ReadLoopAsync(TcpClient tcp, CancellationTokenSource session)
        {
            var buffer = new byte[2048];
            var line = new List<byte>();
            string error = "YNCA connection closed by receiver.";
            try
            {
                NetworkStream stream = tcp.GetStream();
                while (!session.IsCancellationRequested)
                {
                    int count = await stream.ReadAsync(buffer, 0, buffer.Length, session.Token)
                        .ConfigureAwait(false);
                    if (count == 0) break;
                    for (int i = 0; i < count; i++)
                    {
                        byte value = buffer[i];
                        if (value == 10)
                        {
                            if (line.Count > 0 && line[line.Count - 1] == 13)
                                line.RemoveAt(line.Count - 1);
                            if (line.Count > 0)
                                Receive(YamahaYncaCodec.Parse(Encoding.ASCII.GetString(line.ToArray())), tcp, session);
                            line.Clear();
                        }
                        else
                        {
                            if (line.Count >= YamahaYncaCodec.MaxLineBytes)
                                throw new YamahaYncaException("YNCA response exceeds the maximum line length.");
                            line.Add(value);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { error = "YNCA read stream failed: " + ex.Message; }
            finally { CloseSessionIfCurrent(tcp, session, error); }
        }

        private void Receive(YamahaYncaMessage message, TcpClient tcp,
            CancellationTokenSource session)
        {
            PendingRead pending;
            lock (_sync)
            {
                if (!ReferenceEquals(_tcp, tcp) || !ReferenceEquals(_sessionCts, session))
                    return; // Ignore any trailing bytes received by an obsolete reader.
                if (message.Kind == YamahaYncaMessageKind.Value)
                    _last[message.Subunit + ":" + message.Function] = message;
                pending = _pending;
                if (pending != null &&
                    !((message.Kind == YamahaYncaMessageKind.Value &&
                       string.Equals(message.Subunit, pending.Subunit, StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(message.Function, pending.Function, StringComparison.OrdinalIgnoreCase)) ||
                      message.Kind == YamahaYncaMessageKind.Restricted ||
                      message.Kind == YamahaYncaMessageKind.Undefined))
                    pending = null;
            }
            // Publish the new status to zone observers before completing a GET.
            // Otherwise RefreshAsync might return the previous cache snapshot.
            var handler = MessageReceived;
            if (handler != null)
                foreach (EventHandler<YamahaYncaMessageEventArgs> subscriber in handler.GetInvocationList())
                    try { subscriber(this, new YamahaYncaMessageEventArgs(message)); }
                    catch { /* Subscribers must never terminate the TCP reader. */ }
            if (pending != null)
                pending.Completion.TrySetResult(message);
        }

        private void CloseSessionIfCurrent(TcpClient tcp, CancellationTokenSource session, string reason)
        {
            CloseSession(reason, tcp, session);
        }

        internal void Disconnect() { CloseSession("YNCA session disconnected."); }

        private void CloseSession(string reason, TcpClient expected = null,
            CancellationTokenSource expectedSession = null)
        {
            TcpClient old;
            CancellationTokenSource token;
            PendingRead pending;
            lock (_sync)
            {
                if (expected != null &&
                    (!ReferenceEquals(_tcp, expected) || !ReferenceEquals(_sessionCts, expectedSession)))
                    return; // An old reader must never close a newer connection.
                old = _tcp;
                token = _sessionCts;
                pending = _pending;
                _tcp = null;
                _sessionCts = null;
                _pending = null;
                _last.Clear();
            }
            if (token != null)
            {
                try { token.Cancel(); }
                catch (ObjectDisposedException) { }
            }
            if (old != null) old.Close();
            if (pending != null)
                pending.Completion.TrySetException(new YamahaConnectionException(reason));
            if (old != null) RaiseConnectionChanged(false, reason);
            // Do not dispose token here: the background ReadAsync may still use it.
        }

        private void RaiseConnectionChanged(bool connected, string reason)
        {
            var handler = ConnectionChanged;
            if (handler != null)
                foreach (EventHandler<YamahaYncaConnectionEventArgs> subscriber in handler.GetInvocationList())
                    try { subscriber(this, new YamahaYncaConnectionEventArgs(connected, reason)); }
                    catch { }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(YamahaYncaTcpSession));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            CloseSession("YNCA session disposed.");
        }
    }
}
