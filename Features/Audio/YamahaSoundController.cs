using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YamahaAvrNet.Exceptions;
using YamahaAvrNet.Models;
using YamahaAvrNet.Protocols.ExtendedControl;
using YamahaAvrNet.Protocols.Ynca;
using YamahaAvrNet.PublicApi;

namespace YamahaAvrNet.Features.Audio
{
    /// <summary>
    /// Sound and scene commands use device-advertised Extended Control identifiers.
    /// YNCA-only MAIN sound switches reuse the existing shared TCP session.
    /// </summary>
    internal sealed class YamahaSoundController : IYamahaSound
    {
        private readonly YamahaExtendedHttpClient _http;
        private readonly YamahaYncaTcpSession _ynca;
        private readonly Func<YamahaExtendedFeatures> _getFeatures;
        private readonly Func<bool> _isDisposed;
        private readonly bool _allowWrites;

        internal YamahaSoundController(YamahaExtendedHttpClient http,
            YamahaYncaTcpSession ynca, Func<YamahaExtendedFeatures> getFeatures,
            Func<bool> isDisposed, bool allowWrites)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _ynca = ynca;
            _getFeatures = getFeatures ?? throw new ArgumentNullException(nameof(getFeatures));
            _isDisposed = isDisposed ?? throw new ArgumentNullException(nameof(isDisposed));
            _allowWrites = allowWrites;
        }

        public Task<YamahaSoundStatus> GetStatusAsync(string zoneNativeId,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var zone = RequireZone(zoneNativeId);
            return _http.GetZoneSoundStatusAsync(zone.NativeId, cancellationToken);
        }

        public IReadOnlyList<string> GetAdvertisedSoundPrograms(string zoneNativeId)
        {
            return RequireZone(zoneNativeId).SoundPrograms;
        }

        public int? GetAdvertisedSceneCount(string zoneNativeId)
        {
            return RequireZone(zoneNativeId).SceneCount;
        }

        public async Task SetSoundProgramAsync(string zoneNativeId, string programNativeId,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            RequireWrites();
            var zone = RequireZone(zoneNativeId);
            if (string.IsNullOrWhiteSpace(programNativeId))
                throw new YamahaValidationException("A native sound program ID is required.");
            string selected = zone.SoundPrograms.FirstOrDefault(x =>
                string.Equals(x, programNativeId.Trim(), StringComparison.OrdinalIgnoreCase));
            if (selected == null)
                throw new YamahaValidationException("The sound program is not advertised for this zone.");
            // Use the Extended Control ID as-is. YNCA's SOUNDPRG often uses display names instead.
            await _http.SetSoundProgramAsync(zone.NativeId, selected, cancellationToken).ConfigureAwait(false);
            // An HTTP success code is not proof of an applied setting: verify independently.
            YamahaSoundStatus actual = await _http.GetZoneSoundStatusAsync(zone.NativeId, cancellationToken)
                .ConfigureAwait(false);
            if (!string.Equals(actual.SoundProgramNativeId, selected, StringComparison.OrdinalIgnoreCase))
                throw new YamahaStateVerificationException("Sound program readback does not match the requested ID. " +
                    "Receiver state may have changed; do not retry blindly.");
        }

        public Task SetPureDirectAsync(bool enabled,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return SetYncaMainSwitchAsync("PUREDIRMODE", enabled, cancellationToken);
        }

        public Task SetStraightAsync(bool enabled,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return SetYncaMainSwitchAsync("STRAIGHT", enabled, cancellationToken);
        }

        public Task SetEnhancerAsync(bool enabled,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return SetYncaMainSwitchAsync("ENHANCER", enabled, cancellationToken);
        }

        private async Task SetYncaMainSwitchAsync(string function,
            bool enabled, CancellationToken ct)
        {
            RequireWrites();
            RequireZone("main");
            if (_ynca == null)
                throw new YamahaFeatureNotSupportedException("YNCA is disabled for this receiver.");
            // Extended features may omit legacy YNCA functions. A manually opted-in
            // action may still return RESTRICTED or UNDEFINED from the device.
            string target = enabled ? "On" : "Off";
            YamahaYncaMessage response = await _ynca.PutAndReadBackAsync("MAIN", function,
                target, ct).ConfigureAwait(false);
            if (!string.Equals(response.Value, target, StringComparison.OrdinalIgnoreCase))
                throw new YamahaStateVerificationException("YNCA " + function +
                    " readback differs from the requested value; do not retry blindly.");
        }

        public async Task RecallSceneAsync(string zoneNativeId, int sceneNumber,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            RequireWrites();
            var zone = RequireZone(zoneNativeId);
            if (!zone.SceneCount.HasValue || sceneNumber < 1 || sceneNumber > zone.SceneCount.Value)
                throw new YamahaValidationException("Scene number is outside this zone's advertised range.");
            if (!zone.Functions.Any(x => string.Equals(x, "scene", StringComparison.OrdinalIgnoreCase)))
                throw new YamahaFeatureNotSupportedException("Scene recall was not advertised by this zone.");
            // recallScene is an action, not a persistent field with an authoritative readback.
            // A response_code=0 means the receiver accepted the request, not that all
            // scene-dependent input/DSP settings have been proven to take effect.
            await _http.RecallSceneAsync(zone.NativeId, sceneNumber, cancellationToken)
                .ConfigureAwait(false);
        }

        private YamahaExtendedZoneFeatures RequireZone(string nativeZoneId)
        {
            if (_isDisposed()) throw new ObjectDisposedException(nameof(YamahaSoundController));
            if (string.IsNullOrWhiteSpace(nativeZoneId))
                throw new YamahaValidationException("Zone ID is required.");
            // Do not allow arbitrary paths to reach the HTTP transport.
            var features = _getFeatures();
            if (features == null)
                throw new YamahaValidationException("Discover receiver features before using Sound.");
            var zone = features.Zones.FirstOrDefault(x =>
                string.Equals(x.NativeId, nativeZoneId.Trim(), StringComparison.OrdinalIgnoreCase));
            if (zone == null || !(new[] { "main", "zone2", "zone3", "zone4" }).Any(x =>
                string.Equals(x, zone.NativeId, StringComparison.OrdinalIgnoreCase)))
                throw new YamahaFeatureNotSupportedException("Zone is not advertised or mapped: " + nativeZoneId);
            return zone;
        }

        private void RequireWrites()
        {
            if (_isDisposed()) throw new ObjectDisposedException(nameof(YamahaSoundController));
            if (!_allowWrites)
                throw new YamahaValidationException("Sound and scene writes are disabled. " +
                    "Set EnableSoundSceneWrites explicitly before connecting.");
        }
    }
}
