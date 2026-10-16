using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AvrNet.Abstractions.Models;

namespace AvrNet.Abstractions.Interfaces
{
    /// <summary>Small vendor-neutral lifecycle contract; no dispatcher dependency.</summary>
    public interface IAvrReceiver : IAvrCapabilityProvider, IDisposable
    {
        string Manufacturer { get; }
        string Model { get; }
        AvrConnectionState ConnectionState { get; }
        IReadOnlyList<AvrZoneInfo> Zones { get; }

        Task ConnectAsync(CancellationToken cancellationToken = default(CancellationToken));
        Task InitializeAsync(CancellationToken cancellationToken = default(CancellationToken));
        Task RefreshAsync(CancellationToken cancellationToken = default(CancellationToken));
        Task DisconnectAsync(CancellationToken cancellationToken = default(CancellationToken));
    }
}
