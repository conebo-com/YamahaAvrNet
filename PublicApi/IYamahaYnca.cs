using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YamahaAvrNet.Protocols.Ynca;

namespace YamahaAvrNet.PublicApi
{
    /// <summary>Read-only YNCA session shared by commands and unsolicited status events.</summary>
    public interface IYamahaYnca
    {
        bool IsConnected { get; }
        event EventHandler<YamahaYncaMessageEventArgs> MessageReceived;
        event EventHandler<YamahaYncaConnectionEventArgs> ConnectionChanged;
        Task ConnectAsync(CancellationToken cancellationToken = default(CancellationToken));
        Task<YamahaYncaMessage> GetAsync(string subunit, string function,
            CancellationToken cancellationToken = default(CancellationToken));
        IReadOnlyDictionary<string, YamahaYncaMessage> GetLastMessages();
        void Disconnect();
    }
}
