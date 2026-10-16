using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YamahaAvrNet.Models;

namespace YamahaAvrNet.PublicApi
{
    /// <summary>
    /// Typed YNCA zone control. Readback confirms an observed value, not exclusive
    /// causality, since the receiver may send the same value spontaneously.
    /// Writes require explicit opt-in through YamahaAvrOptions.
    /// </summary>
    public interface IYamahaZones
    {
        event EventHandler<YamahaZoneStateChangedEventArgs> ZoneStateChanged;
        IReadOnlyDictionary<string, YamahaZoneState> GetStates();
        Task<YamahaZoneState> RefreshAsync(string nativeZoneId,
            CancellationToken cancellationToken = default(CancellationToken));
        Task SetPowerAsync(string nativeZoneId, bool on,
            CancellationToken cancellationToken = default(CancellationToken));
        Task SetMuteAsync(string nativeZoneId, bool mute,
            CancellationToken cancellationToken = default(CancellationToken));
        Task SetVolumeDbAsync(string nativeZoneId, decimal decibels,
            CancellationToken cancellationToken = default(CancellationToken));
        Task SetInputAsync(string nativeZoneId, string inputNativeId,
            CancellationToken cancellationToken = default(CancellationToken));
    }
}
