using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using YamahaAvrNet.Models;

namespace YamahaAvrNet.Discovery
{
    /// <summary>
    /// Maps evidence onto function identifiers without inventing RPC endpoints
    /// or granting writes. Protocol strings retain their Yamaha-specific form.
    /// </summary>
    internal static class YamahaFunctionRouteMapper
    {
        internal static IReadOnlyList<YamahaFunctionRoute> Build(
            YamahaExtendedFeatures features,
            IDictionary<string, DateTimeOffset> yncaReads)
        {
            var items = new List<YamahaFunctionRoute>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (features != null)
            {
                foreach (var function in features.SystemFunctions)
                    Add(items, seen, "system." + function, "ExtendedControl", function,
                        "system.func_list", YamahaCapabilityStatus.Advertised);
                foreach (var zone in features.Zones)
                {
                    var prefix = "zone." + zone.NativeId + ".";
                    foreach (var function in zone.Functions)
                        Add(items, seen, prefix + function, "ExtendedControl", function,
                            "zone.func_list", YamahaCapabilityStatus.Advertised);
                    if (zone.Inputs.Count > 0)
                        Add(items, seen, prefix + "input", "ExtendedControl", "input_list",
                            "zone.input_list", YamahaCapabilityStatus.Advertised);
                    if (zone.SoundPrograms.Count > 0)
                        Add(items, seen, prefix + "sound_program", "ExtendedControl",
                            "sound_program_list", "zone.sound_program_list", YamahaCapabilityStatus.Advertised);
                    if (zone.SceneCount.GetValueOrDefault() > 0)
                        Add(items, seen, prefix + "scene", "ExtendedControl", "scene_num",
                            "zone.scene_num", YamahaCapabilityStatus.Advertised);
                }
            }

            if (yncaReads != null)
            {
                foreach (var pair in yncaReads)
                {
                    if (!pair.Key.StartsWith("ynca.", StringComparison.OrdinalIgnoreCase))
                        continue;
                    string[] parts = pair.Key.Split('.');
                    if (parts.Length != 3)
                        continue;
                    string field = parts[2].ToLowerInvariant();
                    string feature = field == "pwr" ? "power" :
                        field == "vol" ? "volume" :
                        field == "inp" ? "input" :
                        field == "mute" ? "mute" : null;
                    if (feature == null)
                        continue;
                    Add(items, seen, "zone." + parts[1] + "." + feature, "YNCA",
                        parts[1].ToUpperInvariant() + ":" + field.ToUpperInvariant(),
                        "observed YNCA GET", YamahaCapabilityStatus.ReadVerified);
                }
            }
            return new ReadOnlyCollection<YamahaFunctionRoute>(items);
        }

        private static void Add(List<YamahaFunctionRoute> items, HashSet<string> seen,
            string functionId, string protocol, string nativeId, string reportedBy,
            YamahaCapabilityStatus status)
        {
            if (seen.Add(protocol + ":" + functionId))
                items.Add(new YamahaFunctionRoute(functionId, protocol, nativeId, reportedBy, status));
        }
    }
}
