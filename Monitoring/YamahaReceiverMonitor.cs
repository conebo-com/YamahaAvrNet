using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YamahaAvrNet.Models;
using YamahaAvrNet.PublicApi;

namespace YamahaAvrNet.Monitoring
{
    /// <summary>
    /// Opt-in, read-only monitoring. Polls independent available transports with no
    /// second YNCA connection, and relies on the existing YNCA event reader for pushes.
    /// Poll errors do not imply a feature is permanently unsupported.
    /// </summary>
    public sealed class YamahaReceiverMonitor : IDisposable
    {
        private readonly YamahaAvrClient _receiver;
        private readonly object _sync = new object();
        private CancellationTokenSource _cts;
        private Task _worker;
        private bool _disposed;
        private string _lastError;
        private DateTimeOffset? _lastPollUtc;
        private DateTimeOffset? _lastSuccessfulPollUtc;

        internal YamahaReceiverMonitor(YamahaAvrClient receiver)
        {
            _receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
        }

        public event EventHandler<YamahaMonitorPollEventArgs> PollCompleted;

        public bool IsRunning { get { lock (_sync) return _cts != null; } }
        public string LastError { get { lock (_sync) return _lastError; } }
        public DateTimeOffset? LastPollUtc { get { lock (_sync) return _lastPollUtc; } }
        public DateTimeOffset? LastSuccessfulPollUtc
        {
            get { lock (_sync) return _lastSuccessfulPollUtc; }
        }

        /// <summary>
        /// Begin polling immediately and subsequently at the specified cadence.
        /// No changes are ever sent to the receiver. Call StopAsync before another
        /// exclusive maintenance operation, or DisconnectAsync on the parent.
        /// </summary>
        public void Start(TimeSpan interval)
        {
            if (interval < TimeSpan.FromSeconds(5) || interval > TimeSpan.FromHours(1))
                throw new ArgumentOutOfRangeException(nameof(interval),
                    "Monitoring interval must be between 5 seconds and 1 hour.");
            if (_receiver.ConnectionState == YamahaConnectionState.Disconnected ||
                _receiver.ConnectionState == YamahaConnectionState.Connecting ||
                _receiver.ConnectionState == YamahaConnectionState.Disposed)
                throw new InvalidOperationException("Initialize the receiver before monitoring.");
            lock (_sync)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(YamahaReceiverMonitor));
                if (_cts != null) throw new InvalidOperationException("Monitoring is already active.");
                var cts = new CancellationTokenSource();
                _cts = cts;
                _lastError = null;
                _worker = Task.Run(() => LoopAsync(interval, cts.Token));
            }
        }

        public async Task StopAsync()
        {
            CancellationTokenSource cts;
            Task worker;
            lock (_sync)
            {
                cts = _cts;
                worker = _worker;
                _cts = null;
                _worker = null;
            }
            if (cts == null) return;
            cts.Cancel();
            try
            {
                if (worker != null) await worker.ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            finally { cts.Dispose(); }
        }

        private async Task LoopAsync(TimeSpan interval, CancellationToken ct)
        {
            int failures = 0;
            while (!ct.IsCancellationRequested)
            {
                int successes = 0;
                var errors = new List<string>();
                try
                {
                    // HTTP status and native YNCA are independent: failure of one
                    // path does not prevent a best-effort read of the others.
                    if (_receiver.ExtendedControl != null && _receiver.DeviceInfo != null &&
                        _receiver.DeviceFeatures != null)
                    {
                        try
                        {
                            await _receiver.RefreshAsync(ct).ConfigureAwait(false);
                            successes++;
                        }
                        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                        catch (Exception ex) { errors.Add("ExtendedControl: " + ex.Message); }
                    }
                    if (_receiver.Ynca != null && _receiver.ZoneControl != null)
                    {
                        foreach (var zone in _receiver.Zones)
                        {
                            ct.ThrowIfCancellationRequested();
                            try
                            {
                                var requestStartedUtc = DateTimeOffset.UtcNow;
                                var observed = await _receiver.ZoneControl.RefreshAsync(zone.NativeId, ct)
                                    .ConfigureAwait(false);
                                // A controller may return a cached snapshot when all
                                // individual functions were rejected by the receiver.
                                if (observed.ObservedUtc.HasValue &&
                                    observed.ObservedUtc.Value >= requestStartedUtc &&
                                    !observed.IsStale)
                                    successes++;
                                else
                                    errors.Add("YNCA " + zone.NativeId + ": no fresh value observed.");
                            }
                            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                            catch (Exception ex)
                            {
                                errors.Add("YNCA " + zone.NativeId + ": " + ex.Message);
                                // Do not attempt four fresh TCP connects to the same
                                // offline receiver in a single polling iteration.
                                if (!_receiver.Ynca.IsConnected) break;
                            }
                        }
                    }
                    if (successes == 0)
                    {
                        _receiver.MarkMonitoringFailure();
                        if (errors.Count == 0)
                            errors.Add("No discovered zone or status path can currently be polled.");
                    }
                    else
                    {
                        if (errors.Count > 0) _receiver.MarkMonitoringPartial();
                        else _receiver.MarkMonitoringSuccess();
                    }
                    string detail = errors.Count == 0 ? null : string.Join(" | ", errors);
                    var observed = DateTimeOffset.UtcNow;
                    lock (_sync)
                    {
                        _lastPollUtc = observed;
                        _lastError = detail;
                        if (successes > 0) _lastSuccessfulPollUtc = observed;
                    }
                    RaisePollCompleted(new YamahaMonitorPollEventArgs(observed,
                        successes > 0, successes, detail));
                    failures = successes > 0 ? 0 : Math.Min(failures + 1, 4);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                catch (ObjectDisposedException) when (ct.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    // An unexpected per-poll fault is reported, not left as an
                    // unobserved Task exception that silently stops monitoring.
                    failures = Math.Min(failures + 1, 4);
                    lock (_sync)
                    {
                        _lastPollUtc = DateTimeOffset.UtcNow;
                        _lastError = ex.Message;
                    }
                    _receiver.MarkMonitoringFailure();
                    RaisePollCompleted(new YamahaMonitorPollEventArgs(
                        DateTimeOffset.UtcNow, false, 0, ex.Message));
                }
                // Capped backoff for wholly unreachable devices. Partial success
                // retains the requested interval, even when one protocol fails.
                double seconds = Math.Min(180.0, interval.TotalSeconds * (failures + 1));
                try { await Task.Delay(TimeSpan.FromSeconds(seconds), ct).ConfigureAwait(false); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            }
        }

        private void RaisePollCompleted(YamahaMonitorPollEventArgs result)
        {
            var handler = PollCompleted;
            if (handler == null) return;
            foreach (EventHandler<YamahaMonitorPollEventArgs> listener in handler.GetInvocationList())
                try { listener(this, result); }
                catch { /* A user callback must not terminate monitoring. */ }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;
            }
            // Dispose is synchronous by contract; polling uses ConfigureAwait(false).
            StopAsync().GetAwaiter().GetResult();
        }
    }
}
