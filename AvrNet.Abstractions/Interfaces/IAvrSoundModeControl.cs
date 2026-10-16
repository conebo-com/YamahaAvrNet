using System.Threading;
using System.Threading.Tasks;

namespace AvrNet.Abstractions.Interfaces
{
    public interface IAvrSoundModeControl
    {
        Task<string> GetSoundModeIdAsync(CancellationToken cancellationToken = default(CancellationToken));
        Task SetSoundModeIdAsync(string soundModeId, CancellationToken cancellationToken = default(CancellationToken));
    }
}
