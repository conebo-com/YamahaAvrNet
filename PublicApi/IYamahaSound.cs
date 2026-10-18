using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YamahaAvrNet.Models;

namespace YamahaAvrNet.PublicApi
{
    /// <summary>
    /// Sound and scene operations using receiver-advertised Extended Control IDs.
    /// Writes are disabled unless EnableSoundSceneWrites is explicitly set.
    /// </summary>
    public interface IYamahaSound
    {
        Task<YamahaSoundStatus> GetStatusAsync(string zoneNativeId,
            CancellationToken cancellationToken = default(CancellationToken));
        IReadOnlyList<string> GetAdvertisedSoundPrograms(string zoneNativeId);
        int? GetAdvertisedSceneCount(string zoneNativeId);
        Task SetSoundProgramAsync(string zoneNativeId, string programNativeId,
            CancellationToken cancellationToken = default(CancellationToken));
        Task SetPureDirectAsync(bool enabled,
            CancellationToken cancellationToken = default(CancellationToken));
        Task SetStraightAsync(bool enabled,
            CancellationToken cancellationToken = default(CancellationToken));
        Task SetEnhancerAsync(bool enabled,
            CancellationToken cancellationToken = default(CancellationToken));
        /// <summary>
        /// Recalls one advertised scene. HTTP response_code=0 acknowledges the command,
        /// but does not establish that all scene settings were subsequently applied.
        /// </summary>
        Task RecallSceneAsync(string zoneNativeId, int sceneNumber,
            CancellationToken cancellationToken = default(CancellationToken));
    }
}
