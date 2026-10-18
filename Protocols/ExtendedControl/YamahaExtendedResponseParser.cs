using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamahaAvrNet.Exceptions;
using YamahaAvrNet.Models;

namespace YamahaAvrNet.Protocols.ExtendedControl
{
    /// <summary>Strict response envelope, tolerant optional fields and native identifiers.</summary>
    internal static class YamahaExtendedResponseParser
    {
        internal static YamahaExtendedDeviceInfo ParseDeviceInfo(string json)
        {
            var root = ReadRoot(json, "getDeviceInfo");
            return new YamahaExtendedDeviceInfo(
                GetString(root, "model_name"),
                GetString(root, "system_version"),
                GetString(root, "api_version"),
                GetString(root, "destination"),
                GetString(root, "netmodule_version"),
                GetInteger(root, "netmodule_generation"),
                DateTimeOffset.UtcNow);
        }

        internal static YamahaExtendedFeatures ParseFeatures(string json)
        {
            var root = ReadRoot(json, "getFeatures");
            var system = root["system"] as JObject;
            var inputs = new List<YamahaExtendedInputInfo>();
            var systemInputArray = system == null ? null : system["input_list"] as JArray;

            if (systemInputArray != null)
            {
                foreach (JToken item in systemInputArray)
                {
                    var obj = item as JObject;
                    var id = obj == null ? ReadScalarString(item) : GetString(obj, "id");
                    if (string.IsNullOrWhiteSpace(id))
                        continue;
                    inputs.Add(new YamahaExtendedInputInfo(id,
                        obj == null ? null : GetString(obj, "play_info_type"),
                        obj == null ? null : GetBoolean(obj, "rename_enable"),
                        obj == null ? null : GetBoolean(obj, "distribution_enable")));
                }
            }

            var zones = new List<YamahaExtendedZoneFeatures>();
            var zoneArray = root["zone"] as JArray;
            if (zoneArray != null)
            {
                var seenZones = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (JToken item in zoneArray)
                {
                    var zone = item as JObject;
                    if (zone == null)
                        continue;
                    string nativeId = GetString(zone, "id");
                    if (string.IsNullOrWhiteSpace(nativeId) || !seenZones.Add(nativeId))
                        continue;

                    zones.Add(new YamahaExtendedZoneFeatures(
                        nativeId,
                        ReadStringList(zone["func_list"]),
                        ReadStringList(zone["input_list"]),
                        ReadStringList(zone["sound_program_list"]),
                        GetNonNegativeInteger(zone, "scene_num"),
                        ReadRanges(zone["range_step"]),
                        ReadOptionLists(zone)));
                }
            }

            return new YamahaExtendedFeatures(
                ReadStringList(system == null ? null : system["func_list"]),
                new ReadOnlyCollection<YamahaExtendedInputInfo>(inputs),
                new ReadOnlyCollection<YamahaExtendedZoneFeatures>(zones),
                GetNonNegativeInteger(system, "zone_num"),
                GetPositiveInteger(system, "speaker_pattern_num"),
                ReadPresetCount(root["tuner"]),
                ReadPresetCount(root["netusb"]),
                DateTimeOffset.UtcNow);
        }

        internal static YamahaExtendedFunctionStatus ParseFuncStatus(string json)
        {
            var root = ReadRoot(json, "getFuncStatus");
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in root.Properties())
            {
                if (string.Equals(pair.Name, "response_code", StringComparison.OrdinalIgnoreCase))
                    continue;
                values[pair.Name] = pair.Value.ToString(Formatting.None);
            }

            return new YamahaExtendedFunctionStatus(
                GetPositiveInteger(root, "speaker_pattern"),
                GetBoolean(root, "party_mode"),
                new ReadOnlyDictionary<string, string>(values),
                DateTimeOffset.UtcNow);
        }

        internal static YamahaSoundStatus ParseZoneSoundStatus(string json, string zoneNativeId)
        {
            JObject root = ReadRoot(json, "getStatus");
            return new YamahaSoundStatus(zoneNativeId,
                GetString(root, "sound_program"),
                GetBoolean(root, "pure_direct"),
                GetBoolean(root, "enhancer"),
                GetBoolean(root, "direct"),
                GetString(root, "surr_decoder_type"), DateTimeOffset.UtcNow);
        }

        internal static void ValidateCommandResponse(string json, string endpoint)
        {
            ReadRoot(json, endpoint);
        }

        private static JObject ReadRoot(string json, string endpoint)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new YamahaExtendedControlException(endpoint + " returned an empty response.");

            try
            {
                using (var input = new StringReader(json))
                using (var reader = new JsonTextReader(input))
                {
                    reader.MaxDepth = 64;
                    reader.DateParseHandling = DateParseHandling.None;
                    reader.FloatParseHandling = FloatParseHandling.Decimal;
                    var root = JObject.Load(reader);
                    if (reader.Read())
                        throw new YamahaExtendedControlException(endpoint + " contained trailing JSON data.");
                    int? responseCode = GetInteger(root, "response_code");
                    if (!responseCode.HasValue)
                        throw new YamahaExtendedControlException(endpoint + " did not return a valid response_code.");
                    if (responseCode.Value != 0)
                        throw new YamahaExtendedControlException(endpoint + " returned Yamaha response_code=" +
                            responseCode.Value.ToString(CultureInfo.InvariantCulture) + ".");
                    return root;
                }
            }
            catch (JsonException ex)
            {
                throw new YamahaExtendedControlException(endpoint + " returned invalid JSON.", ex);
            }
        }

        private static IReadOnlyList<string> ReadStringList(JToken token)
        {
            var result = new List<string>();
            var array = token as JArray;
            if (array != null)
            {
                foreach (JToken item in array)
                {
                    var value = item is JObject ? GetString((JObject)item, "id") : ReadScalarString(item);
                    if (!string.IsNullOrWhiteSpace(value))
                        result.Add(value);
                }
            }
            return new ReadOnlyCollection<string>(result);
        }

        private static IReadOnlyDictionary<string, YamahaExtendedNumericRange> ReadRanges(JToken token)
        {
            var result = new Dictionary<string, YamahaExtendedNumericRange>(StringComparer.OrdinalIgnoreCase);
            var array = token as JArray;
            if (array != null)
            {
                foreach (JToken item in array)
                {
                    var range = item as JObject;
                    if (range == null)
                        continue;
                    string id = GetString(range, "id");
                    if (string.IsNullOrWhiteSpace(id))
                        continue;
                    result[id] = new YamahaExtendedNumericRange(id,
                        GetDecimal(range, "min"), GetDecimal(range, "max"), GetDecimal(range, "step"));
                }
            }
            return new ReadOnlyDictionary<string, YamahaExtendedNumericRange>(result);
        }

        private static IReadOnlyDictionary<string, IReadOnlyList<string>> ReadOptionLists(JObject zone)
        {
            var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (JProperty property in zone.Properties())
            {
                if (!property.Name.EndsWith("_list", StringComparison.OrdinalIgnoreCase) ||
                    !(property.Value is JArray))
                    continue;
                result[property.Name] = ReadStringList(property.Value);
            }
            return new ReadOnlyDictionary<string, IReadOnlyList<string>>(result);
        }

        private static int? ReadPresetCount(JToken section)
        {
            var obj = section as JObject;
            var preset = obj == null ? null : obj["preset"] as JObject;
            return GetNonNegativeInteger(preset, "num");
        }

        private static string GetString(JObject obj, string field)
        {
            return obj == null ? null : ReadScalarString(obj[field]);
        }

        private static string ReadScalarString(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
                return null;
            if (token.Type != JTokenType.String && token.Type != JTokenType.Integer &&
                token.Type != JTokenType.Float)
                return null;
            return token.Type == JTokenType.String ? (string)token : token.ToString(Formatting.None);
        }

        private static int? GetPositiveInteger(JObject obj, string field)
        {
            int? result = GetInteger(obj, field);
            return result.HasValue && result.Value > 0 ? result : null;
        }

        private static int? GetNonNegativeInteger(JObject obj, string field)
        {
            int? result = GetInteger(obj, field);
            return result.HasValue && result.Value >= 0 ? result : null;
        }

        private static int? GetInteger(JObject obj, string field)
        {
            JToken token = obj == null ? null : obj[field];
            if (token == null || token.Type != JTokenType.Integer)
                return null;
            int value;
            return int.TryParse(token.ToString(Formatting.None), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out value) ? (int?)value : null;
        }

        private static decimal? GetDecimal(JObject obj, string field)
        {
            JToken token = obj == null ? null : obj[field];
            if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float))
                return null;
            decimal value;
            return decimal.TryParse(token.ToString(Formatting.None), NumberStyles.Float,
                CultureInfo.InvariantCulture, out value) ? (decimal?)value : null;
        }

        private static bool? GetBoolean(JObject obj, string field)
        {
            JToken token = obj == null ? null : obj[field];
            if (token == null || token.Type != JTokenType.Boolean)
                return null;
            return token.Value<bool>();
        }
    }
}
