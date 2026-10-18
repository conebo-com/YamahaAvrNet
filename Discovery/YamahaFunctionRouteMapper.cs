using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using YamahaAvrNet.Models;

namespace YamahaAvrNet.Discovery
{
    /// <summary>
    /// Combines advertised device functions, implemented commands and instance reads.
    /// An implemented command with no device evidence remains Unknown. Neither the
    /// catalog nor getFeatures alone grants writable or executable permissions.
    /// </summary>
    internal static class YamahaFunctionRouteMapper
    {
        internal static IReadOnlyList<YamahaFunctionRoute> Build(
            YamahaExtendedDeviceInfo device, YamahaExtendedFeatures features,
            YamahaExtendedFunctionStatus status, IReadOnlyList<YamahaZoneInfo> zones, bool yncaAvailable, bool legacyAvailable,
            int? legacyActivePattern, IDictionary<string, DateTimeOffset> yncaReads,
            IDictionary<string, DateTimeOffset> speakerReads,
            IDictionary<string, DateTimeOffset> extendedReads,
            IDictionary<string, DateTimeOffset> verifiedWrites,
            IDictionary<string, DateTimeOffset> acceptedActions)
        {
            var routes = new Dictionary<string, RouteBuilder>(StringComparer.OrdinalIgnoreCase);
            if (device != null)
            {
                if (!string.IsNullOrWhiteSpace(device.ModelName))
                    VerifyRead(Add(routes, "system.model", "ExtendedControl", "getDeviceInfo.model_name",
                        "getDeviceInfo", YamahaCapabilityStatus.Advertised), device.ObservedUtc);
                if (!string.IsNullOrWhiteSpace(device.SystemVersion))
                    VerifyRead(Add(routes, "system.firmware", "ExtendedControl", "getDeviceInfo.system_version",
                        "getDeviceInfo", YamahaCapabilityStatus.Advertised), device.ObservedUtc);
                if (!string.IsNullOrWhiteSpace(device.ApiVersion))
                    VerifyRead(Add(routes, "system.api_version", "ExtendedControl", "getDeviceInfo.api_version",
                        "getDeviceInfo", YamahaCapabilityStatus.Advertised), device.ObservedUtc);
            }
            if (features != null)
            {
                foreach (string function in features.SystemFunctions)
                    Add(routes, "system." + function, "ExtendedControl", function,
                        "system.func_list", YamahaCapabilityStatus.Advertised);
                foreach (var zone in features.Zones)
                {
                    string prefix = "zone." + zone.NativeId + ".";
                    foreach (string function in zone.Functions)
                        Add(routes, prefix + function, "ExtendedControl", function,
                            "zone.func_list", YamahaCapabilityStatus.Advertised);
                    if (zone.Inputs.Count > 0)
                        Add(routes, prefix + "input", "ExtendedControl", "input_list",
                            "zone.input_list", YamahaCapabilityStatus.Advertised);
                    if (zone.SoundPrograms.Count > 0)
                        Add(routes, prefix + "sound_program", "ExtendedControl", "sound_program_list",
                            "zone.sound_program_list", YamahaCapabilityStatus.Advertised);
                    if (zone.SceneCount.GetValueOrDefault() > 0)
                        Add(routes, prefix + "scene", "ExtendedControl", "scene_num",
                            "zone.scene_num", YamahaCapabilityStatus.Advertised);

                    // Only the implemented Extended Control controllers are marked implemented.
                    bool controllerMapped = IsMappedSoundZone(zone.NativeId);
                    if (controllerMapped)
                        foreach (string field in new[] { "pure_direct", "enhancer", "surr_decoder_type" })
                        {
                            RouteBuilder soundField;
                            if (routes.TryGetValue("ExtendedControl:" + prefix + field, out soundField))
                                soundField.ImplementedRead = true;
                        }
                    if (controllerMapped && zone.SoundPrograms.Count > 0)
                    {
                        var sound = Add(routes, prefix + "sound_program", "ExtendedControl",
                            "setSoundProgram", "implemented sound controller", YamahaCapabilityStatus.Advertised);
                        sound.ImplementedRead = true;
                        sound.ImplementedWrite = true;
                    }
                    if (controllerMapped && zone.SceneCount.GetValueOrDefault() > 0 &&
                        Contains(zone.Functions, "scene"))
                        Add(routes, prefix + "scene", "ExtendedControl", "recallScene",
                            "implemented scene controller", YamahaCapabilityStatus.Advertised)
                            .ImplementedExecute = true;
                }
                if (features.SpeakerPatternCount.HasValue)
                    VerifyRead(Add(routes, "speaker.pattern_count", "ExtendedControl",
                        "speaker_pattern_num", "getFeatures", YamahaCapabilityStatus.Advertised),
                        features.ObservedUtc);
                foreach (var zone in features.Zones)
                {
                    string prefix = "zone." + zone.NativeId + ".";
                    if (zone.Inputs.Count > 0)
                        VerifyRead(Add(routes, prefix + "inputs", "ExtendedControl", "input_list",
                            "getFeatures", YamahaCapabilityStatus.Advertised), features.ObservedUtc);
                    if (zone.SoundPrograms.Count > 0)
                        VerifyRead(Add(routes, prefix + "sound_programs", "ExtendedControl", "sound_program_list",
                            "getFeatures", YamahaCapabilityStatus.Advertised), features.ObservedUtc);
                    foreach (var range in zone.Ranges)
                        VerifyRead(Add(routes, prefix + "range." + range.Key, "ExtendedControl",
                            range.Key, "getFeatures", YamahaCapabilityStatus.Advertised), features.ObservedUtc);
                    foreach (var options in zone.OptionLists)
                        VerifyRead(Add(routes, prefix + "options." + options.Key, "ExtendedControl",
                            options.Key, "getFeatures", YamahaCapabilityStatus.Advertised), features.ObservedUtc);
                    string id = "zone." + zone.NativeId + ".scene_count";
                    if (zone.SceneCount.HasValue)
                        VerifyRead(Add(routes, id, "ExtendedControl", "scene_num",
                            "getFeatures", YamahaCapabilityStatus.Advertised), features.ObservedUtc);
                }
            }
            if (status != null && status.ActiveSpeakerPattern.HasValue)
                VerifyRead(Add(routes, "speaker.pattern", "ExtendedControl",
                    "speaker_pattern", "getFuncStatus", YamahaCapabilityStatus.Advertised), status.ObservedUtc);
            if (legacyAvailable)
            {
                int count = features == null ? 0 : features.SpeakerPatternCount.GetValueOrDefault();
                if (count > 0 && count <= 32)
                    for (int number = 1; number <= count; number++)
                    {
                        foreach (var item in new[] { "levels", "distances" })
                        {
                            string id = "speaker.pattern." + number + "." + item;
                            var entry = Add(routes, id, "LegacyXml", LegacyPath(id),
                                "implemented aggregate XML GET (not yet probed)",
                                YamahaCapabilityStatus.Unknown);
                            entry.ImplementedRead = true;
                        }
                    }
                var pattern = Add(routes, "speaker.pattern", "LegacyXml",
                    "System/Speaker_Preout/Pattern_Sel", "implemented legacy XML GET",
                    YamahaCapabilityStatus.Unknown);
                pattern.ImplementedRead = true;
                DateTimeOffset observed;
                if (speakerReads != null && speakerReads.TryGetValue("speaker.pattern", out observed))
                    VerifyRead(pattern, observed);
                else if (legacyActivePattern.HasValue)
                    VerifyRead(pattern, DateTimeOffset.UtcNow);
            }
            if (speakerReads != null)
                foreach (var read in speakerReads)
                {
                    // The aggregate GET is verified, never an individual channel leaf GET.
                    string path = LegacyPath(read.Key);
                    if (path == null) continue;
                    var route = Add(routes, read.Key, "LegacyXml", path,
                        "successful aggregate GET", YamahaCapabilityStatus.Unknown);
                    route.ImplementedRead = true;
                    VerifyRead(route, read.Value);
                }
            if (yncaAvailable)
            {
                // Only documented/implemented YNCA names for discovered zones; no
                // assumption that their mere presence in code proves device support.
                AddKnownYnca(routes, "SYS", "MODELNAME", true, false);
                foreach (var zone in zones)
                {
                    string native = zone.NativeId.ToUpperInvariant();
                    if (native != "MAIN" && native != "ZONE2" && native != "ZONE3" && native != "ZONE4")
                        continue;
                    AddKnownYnca(routes, native, "PWR", true, true);
                    AddKnownYnca(routes, native, "VOL", true, true);
                    AddKnownYnca(routes, native, "MUTE", true, true);
                    AddKnownYnca(routes, native, "INP", true, true);
                    if (native == "MAIN")
                    {
                        AddKnownYnca(routes, native, "SOUNDPRG", true, false);
                        AddKnownYnca(routes, native, "STRAIGHT", true, true);
                        AddKnownYnca(routes, native, "PUREDIRMODE", true, true);
                        AddKnownYnca(routes, native, "ENHANCER", true, true);
                    }
                }
            }
            if (yncaReads != null)
                foreach (var read in yncaReads)
                {
                    string subunit, field;
                    if (!SplitYncaKey(read.Key, out subunit, out field)) continue;
                    string id = YncaFunctionId(subunit, field);
                    if (id == null) continue;
                    var route = Add(routes, id, "YNCA", subunit.ToUpperInvariant() + ":" + field.ToUpperInvariant(),
                        "successful YNCA GET", YamahaCapabilityStatus.Unknown);
                    route.ImplementedRead = true;
                    VerifyRead(route, read.Value);
                }
            // The legacy writer is constrained to one explicitly validated channel.
            if (legacyAvailable && features != null && device != null &&
                string.Equals(device.ModelName, "RX-A3060", StringComparison.Ordinal) &&
                string.Equals(device.SystemVersion, "2.86", StringComparison.Ordinal) &&
                features.SpeakerPatternCount == 2)
            {
                foreach (string suffix in new[] { "level", "distance" })
                {
                    var route = Add(routes, "speaker.pattern.1.channel.front_l." + suffix,
                        "LegacyXml", "System/Speaker_Preout/Pattern_1/" +
                        (suffix == "level" ? "Lvl/Front_L" : "Distance/Meter/Front_L"),
                        "restricted RX-A3060 writer (device support unverified)",
                        YamahaCapabilityStatus.Unknown);
                    // A safe model/firmware check still happens in the controller.
                    // Only an instance-specific write readback may set CanWrite=true.
                    route.ImplementedWrite = true;
                }
            }
            if (extendedReads != null)
                foreach (var read in extendedReads)
                {
                    RouteBuilder route;
                    if (routes.TryGetValue(read.Key, out route))
                        VerifyRead(route, read.Value);
                }
            ApplyPositiveObservations(routes, verifiedWrites, true);
            ApplyPositiveObservations(routes, acceptedActions, false);

            var list = new List<YamahaFunctionRoute>();
            foreach (var item in routes.Values)
                list.Add(new YamahaFunctionRoute(item.FunctionId, item.Protocol,
                    item.NativeId, item.Source, item.Status, item.ImplementedRead,
                    item.ImplementedWrite, item.ImplementedExecute,
                    item.CanRead, item.CanWrite, item.CanExecute, item.Timestamp));
            list.Sort((a, b) =>
            {
                int cmp = StringComparer.OrdinalIgnoreCase.Compare(a.FunctionId, b.FunctionId);
                return cmp != 0 ? cmp : StringComparer.OrdinalIgnoreCase.Compare(a.Protocol, b.Protocol);
            });
            return new ReadOnlyCollection<YamahaFunctionRoute>(list);
        }

        internal static string YncaFunctionId(string subunit, string field)
        {
            if (string.Equals(subunit, "SYS", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(field, "MODELNAME", StringComparison.OrdinalIgnoreCase))
                return "system.model";
            string zone = subunit.ToLowerInvariant();
            if (zone != "main" && zone != "zone2" && zone != "zone3" && zone != "zone4")
                return null;
            string suffix;
            switch (field.ToUpperInvariant())
            {
                case "PWR": suffix = "power"; break;
                case "VOL": suffix = "volume"; break;
                case "MUTE": suffix = "mute"; break;
                case "INP": suffix = "input"; break;
                case "SOUNDPRG": suffix = zone == "main" ? "stored_sound_program" : null; break;
                case "STRAIGHT": suffix = zone == "main" ? "straight" : null; break;
                case "PUREDIRMODE": suffix = zone == "main" ? "pure_direct" : null; break;
                case "ENHANCER": suffix = zone == "main" ? "enhancer" : null; break;
                default: suffix = null; break;
            }
            return suffix == null ? null : "zone." + zone + "." + suffix;
        }

        private static string LegacyPath(string id)
        {
            var parts = id.Split('.');
            if (parts.Length != 4 || parts[0] != "speaker" || parts[1] != "pattern") return null;
            int number;
            if (!int.TryParse(parts[2], out number) || number < 1 || number > 32) return null;
            string leaf = parts[3] == "levels" ? "Lvl" : parts[3] == "distances" ? "Distance" : null;
            return leaf == null ? null : "System/Speaker_Preout/Pattern_" + number + "/" + leaf;
        }

        private static bool SplitYncaKey(string key, out string subunit, out string field)
        {
            subunit = null; field = null;
            if (!key.StartsWith("ynca.", StringComparison.OrdinalIgnoreCase)) return false;
            string[] parts = key.Split('.');
            if (parts.Length != 3) return false;
            subunit = parts[1]; field = parts[2];
            return true;
        }

        private static void AddKnownYnca(Dictionary<string, RouteBuilder> routes,
            string subunit, string field, bool read, bool write)
        {
            string id = YncaFunctionId(subunit, field);
            if (id == null) return;
            var route = Add(routes, id, "YNCA", subunit + ":" + field,
                "implemented command (not probed)", YamahaCapabilityStatus.Unknown);
            route.ImplementedRead |= read;
            route.ImplementedWrite |= write;
        }

        private static bool IsMappedSoundZone(string nativeId)
        {
            return string.Equals(nativeId, "main", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(nativeId, "zone2", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(nativeId, "zone3", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(nativeId, "zone4", StringComparison.OrdinalIgnoreCase);
        }

        private static bool Contains(IReadOnlyList<string> values, string item)
        {
            foreach (var value in values)
                if (string.Equals(value, item, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static void ApplyPositiveObservations(Dictionary<string, RouteBuilder> routes,
            IDictionary<string, DateTimeOffset> observations, bool write)
        {
            if (observations == null) return;
            foreach (var observation in observations)
            {
                RouteBuilder route;
                if (!routes.TryGetValue(observation.Key, out route)) continue;
                if (write && !route.ImplementedWrite) continue;
                if (!write && !route.ImplementedExecute) continue;
                if (write)
                {
                    route.CanWrite = true;
                    route.Status = YamahaCapabilityStatus.WriteVerified;
                }
                else
                {
                    route.CanExecute = true;
                    // HTTP accepted an action; it did not verify the full resulting state.
                    route.Source = "receiver accepted action";
                    route.Status = YamahaCapabilityStatus.ActionAccepted;
                }
                if (!route.Timestamp.HasValue || observation.Value > route.Timestamp.Value)
                    route.Timestamp = observation.Value;
            }
        }

        private static void VerifyRead(RouteBuilder route, DateTimeOffset when)
        {
            route.ImplementedRead = true;
            route.CanRead = true;
            if (route.Status != YamahaCapabilityStatus.WriteVerified)
                route.Status = YamahaCapabilityStatus.ReadVerified;
            route.Source = "successful read";
            route.Timestamp = when;
        }

        private static RouteBuilder Add(Dictionary<string, RouteBuilder> routes, string id,
            string protocol, string nativeId, string source, YamahaCapabilityStatus status)
        {
            string key = protocol + ":" + id;
            RouteBuilder route;
            if (!routes.TryGetValue(key, out route))
            {
                route = new RouteBuilder { FunctionId = id, Protocol = protocol,
                    NativeId = nativeId, Source = source, Status = status };
                routes.Add(key, route);
            }
            else if (route.Status == YamahaCapabilityStatus.Unknown &&
                     status == YamahaCapabilityStatus.Advertised)
                route.Status = status;
            return route;
        }

        private sealed class RouteBuilder
        {
            internal string FunctionId, Protocol, NativeId, Source;
            internal YamahaCapabilityStatus Status;
            internal bool ImplementedRead, ImplementedWrite, ImplementedExecute;
            internal bool? CanRead, CanWrite, CanExecute;
            internal DateTimeOffset? Timestamp;
        }
    }
}
