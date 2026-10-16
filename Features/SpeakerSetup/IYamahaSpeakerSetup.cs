using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YamahaAvrNet.Models;

namespace YamahaAvrNet.Features.SpeakerSetup
{
    /// <summary>Yamaha-native IDs are retained and never matched by list position.</summary>
    public interface IYamahaSpeakerSetup
    {
        Task<int?> GetActivePatternAsync(CancellationToken cancellationToken = default(CancellationToken));
        Task<IReadOnlyList<YamahaSpeakerPatternInfo>> GetPatternsAsync(
            CancellationToken cancellationToken = default(CancellationToken));
        Task<IReadOnlyDictionary<string, YamahaSpeakerValue>> GetLevelsAsync(
            int pattern, CancellationToken cancellationToken = default(CancellationToken));
        Task<IReadOnlyDictionary<string, YamahaSpeakerValue>> GetDistancesAsync(
            int pattern, CancellationToken cancellationToken = default(CancellationToken));
        Task SetLevelAsync(int pattern, string nativeId, decimal decibels,
            CancellationToken cancellationToken = default(CancellationToken));
        Task SetDistanceAsync(int pattern, string nativeId, decimal meters,
            CancellationToken cancellationToken = default(CancellationToken));
    }
}
