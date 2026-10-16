using System.Threading;
using System.Threading.Tasks;

namespace AvrNet.Abstractions.Interfaces
{
    public interface IAvrPowerControl
    {
        /// <summary>Nullable because not observed and powered-off are distinct states.</summary>
        Task<bool?> GetPowerAsync(CancellationToken cancellationToken = default(CancellationToken));
        Task SetPowerAsync(bool poweredOn, CancellationToken cancellationToken = default(CancellationToken));
    }
}
