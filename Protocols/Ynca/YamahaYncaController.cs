using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YamahaAvrNet.PublicApi;

namespace YamahaAvrNet.Protocols.Ynca
{
    internal sealed class YamahaYncaController : IYamahaYnca
    {
        private readonly YamahaYncaTcpSession _session;
        internal YamahaYncaController(YamahaYncaTcpSession session)
        {
            _session = session;
        }

        public bool IsConnected { get { return _session.IsConnected; } }

        public event EventHandler<YamahaYncaMessageEventArgs> MessageReceived
        {
            add { _session.MessageReceived += value; }
            remove { _session.MessageReceived -= value; }
        }

        public event EventHandler<YamahaYncaConnectionEventArgs> ConnectionChanged
        {
            add { _session.ConnectionChanged += value; }
            remove { _session.ConnectionChanged -= value; }
        }

        public Task ConnectAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return _session.ConnectAsync(cancellationToken);
        }

        public Task<YamahaYncaMessage> GetAsync(string subunit, string function,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return _session.GetAsync(subunit, function, cancellationToken);
        }

        public IReadOnlyDictionary<string, YamahaYncaMessage> GetLastMessages()
        {
            return _session.GetLastMessages();
        }

        public void Disconnect() { _session.Disconnect(); }
    }
}
