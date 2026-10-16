using System.Threading;
using System.Threading.Tasks;
using YamahaAvrNet.Models;

namespace YamahaAvrNet.PublicApi
{
    /// <summary>
    /// Explicit, read-only Yamaha Extended Control operations. These requests do not
    /// change receiver settings. Reading through this interface does not modify the
    /// receiver client's cached state; call InitializeAsync or RefreshAsync for that.
    /// </summary>
    public interface IYamahaExtendedControl
    {
        Task<YamahaExtendedDeviceInfo> GetDeviceInfoAsync(
            CancellationToken cancellationToken = default(CancellationToken));

        Task<YamahaExtendedFeatures> GetFeaturesAsync(
            CancellationToken cancellationToken = default(CancellationToken));

        Task<YamahaExtendedFunctionStatus> GetFuncStatusAsync(
            CancellationToken cancellationToken = default(CancellationToken));
    }
}
