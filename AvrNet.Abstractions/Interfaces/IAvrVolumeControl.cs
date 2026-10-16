using System.Threading;
using System.Threading.Tasks;

namespace AvrNet.Abstractions.Interfaces
{
    /// <summary>Volume in dB, never in receiver-dependent display units.</summary>
    public interface IAvrVolumeControl
    {
        Task<decimal?> GetVolumeDbAsync(CancellationToken cancellationToken = default(CancellationToken));
        Task SetVolumeDbAsync(decimal decibels, CancellationToken cancellationToken = default(CancellationToken));
        Task<bool?> GetMuteAsync(CancellationToken cancellationToken = default(CancellationToken));
        Task SetMuteAsync(bool muted, CancellationToken cancellationToken = default(CancellationToken));
    }
}
