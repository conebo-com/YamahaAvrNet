using System;
using System.Collections.Generic;
using YamahaAvrNet.Models;
using YamahaAvrNet.PublicApi;

namespace YamahaAvrNet.Discovery
{
    /// <summary>Maps reported device features without assuming read or write permission.</summary>
    internal static class YamahaExtendedCapabilityMapper
    {
        internal static YamahaDeviceCapabilities Build(YamahaExtendedDeviceInfo device,
            YamahaExtendedFeatures features, YamahaExtendedFunctionStatus status)
        {
            var values = new Dictionary<string, YamahaCapability>(StringComparer.OrdinalIgnoreCase);
            if (features == null)
                return new YamahaDeviceCapabilities(values.Values);

            foreach (string function in features.SystemFunctions)
                AddAdvertised(values, "system." + function, device);

            foreach (var zone in features.Zones)
            {
                foreach (string function in zone.Functions)
                    AddAdvertised(values, "zone." + zone.NativeId + "." + function, device);

                if (zone.Inputs.Count > 0)
                    AddReadVerified(values, "zone." + zone.NativeId + ".inputs", device,
                        features.ObservedUtc);
                if (zone.SoundPrograms.Count > 0)
                    AddReadVerified(values, "zone." + zone.NativeId + ".sound_programs", device,
                        features.ObservedUtc);
                if (zone.SceneCount.HasValue)
                    AddReadVerified(values, "zone." + zone.NativeId + ".scene_count", device,
                        features.ObservedUtc);
                YamahaExtendedNumericRange volumeRange;
                if (zone.Ranges.TryGetValue("actual_volume_db", out volumeRange) &&
                    volumeRange.Minimum.HasValue && volumeRange.Maximum.HasValue)
                    AddReadVerified(values, "zone." + zone.NativeId + ".volume_range_db", device,
                        features.ObservedUtc);
            }

            if (features.SpeakerPatternCount.HasValue)
                AddReadVerified(values, "speaker.pattern_count", device, features.ObservedUtc);
            if (features.TunerPresetCount.HasValue)
                AddReadVerified(values, "media.tuner.preset_count", device, features.ObservedUtc);
            if (features.NetUsbPresetCount.HasValue)
                AddReadVerified(values, "media.netusb.preset_count", device, features.ObservedUtc);

            foreach (string function in features.SystemFunctions)
            {
                if (string.Equals(function, "speaker_pattern", StringComparison.OrdinalIgnoreCase))
                {
                    AddAdvertised(values, "speaker.pattern", device);
                    break;
                }
            }

            if (status != null && status.ActiveSpeakerPattern.HasValue)
                AddReadVerified(values, "speaker.pattern", device, status.ObservedUtc,
                    YamahaEvidenceSource.ReadProbe);

            return new YamahaDeviceCapabilities(values.Values);
        }

        private static void AddAdvertised(Dictionary<string, YamahaCapability> values,
            string id, YamahaExtendedDeviceInfo device)
        {
            if (values.ContainsKey(id))
                return;
            values[id] = new YamahaCapability(id, YamahaCapabilityStatus.Advertised,
                YamahaEvidenceSource.DeviceReported, protocol: "ExtendedControl",
                model: device == null ? null : device.ModelName,
                firmware: device == null ? null : device.SystemVersion);
        }

        private static void AddReadVerified(Dictionary<string, YamahaCapability> values,
            string id, YamahaExtendedDeviceInfo device, DateTimeOffset observedUtc,
            YamahaEvidenceSource evidence = YamahaEvidenceSource.DeviceReported)
        {
            values[id] = new YamahaCapability(id, YamahaCapabilityStatus.ReadVerified,
                evidence, canRead: true,
                protocol: "ExtendedControl",
                model: device == null ? null : device.ModelName,
                firmware: device == null ? null : device.SystemVersion,
                lastVerifiedUtc: observedUtc);
        }
    }
}
