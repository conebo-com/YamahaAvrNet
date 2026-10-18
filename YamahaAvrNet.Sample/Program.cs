using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using YamahaAvrNet.Models;
using YamahaAvrNet.PublicApi;

namespace YamahaAvrNet.Sample
{
    internal static class Program
    {
        private static int Main()
        {
            int result;
            try
            {
                result = RunAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Receiver operation failed: " + ex.Message);
                result = 1;
            }

            if (!Console.IsInputRedirected)
            {
                Console.WriteLine();
                Console.Write("Press Enter to exit...");
                Console.ReadLine();
            }
            return result;
        }

        private static async Task<int> RunAsync()
        {
            Console.WriteLine("YamahaAvrNet - receiver discovery, speaker settings and monitoring");
            Console.WriteLine("Read-only: no receiver settings will be changed.");
            Console.WriteLine();

            string host = ReadHost();
            if (host == null)
                return 0;
            int? port = ReadHttpPort();
            if (!port.HasValue)
                return 0;

            Console.WriteLine();
            Console.WriteLine("Connecting to " + host + ":" + port.Value + "...");
            using (var receiver = new YamahaAvrClient(new YamahaAvrOptions
            {
                Host = host,
                HttpPort = port.Value,
                EnableYnca = true,
                EnableExtendedControl = true,
                EnableLegacyXml = true
            }))
            {
                await receiver.InitializeAsync();
                var info = receiver.DeviceInfo;
                var features = receiver.DeviceFeatures;
                var status = receiver.FunctionStatus;

                Console.WriteLine("Model: " + Show(receiver.Model));
                Console.WriteLine("Firmware: " + Show(info == null ? null : info.SystemVersion));
                Console.WriteLine("Extended Control API: " + Show(info == null ? null : info.ApiVersion));
                Console.WriteLine("Protocol discovery (read-only):");
                foreach (var protocol in receiver.Protocols.Values.OrderBy(x => x.Protocol))
                    Console.WriteLine("  " + protocol.Protocol + ": " + protocol.Availability +
                        (string.IsNullOrEmpty(protocol.Detail) ? "" : " - " + protocol.Detail));
                // The same Yamaha-only read contract can later be consumed by an
                // AvrDispatcher adapter, without coupling either library to it.
                IYamahaReceiverInfo receiverInfo = receiver;
                YamahaReceiverOverview overview = receiverInfo.GetOverview();
                Console.WriteLine("Receiver overview: " + overview.State.ConnectionState +
                    ", stale=" + overview.State.IsStale +
                    ", discovered zones=" + overview.DiscoveredZones.Count);
                Console.WriteLine("Function routes: " + overview.FunctionRoutes.Count +
                    " (advertised or observed; not proof of write support)");
                var description = receiver.LegacyDescriptor;
                Console.WriteLine("Legacy XML description: " +
                    (description == null ? "unavailable / unverified" :
                        description.ResourcePath + " (" + description.CommandElementCount +
                        " command elements; incomplete metadata)"));
                var descriptorDiagnostic = receiver.LegacyDescriptorDiagnostic;
                if (descriptorDiagnostic != null)
                    foreach (var attempt in descriptorDiagnostic.Attempts)
                        Console.WriteLine("  " + attempt.Path + ": " + attempt.Status +
                            (attempt.HttpStatusCode.HasValue ? " (HTTP " + attempt.HttpStatusCode + ")" : "") +
                            (string.IsNullOrEmpty(attempt.Detail) ? "" : " - " + attempt.Detail));
                if (Confirm("Show discovered function routes? [y/N]: "))
                {
                    foreach (var route in receiver.FunctionRoutes.OrderBy(r => r.FunctionId)
                        .ThenBy(r => r.Protocol))
                        Console.WriteLine("  " + route.FunctionId + " | " + route.Protocol +
                            " | " + route.NativeIdentifier + " | " + route.Status +
                            " | implementation R/W/E=" +
                            (route.ImplementedRead ? "Y" : "-") + "/" +
                            (route.ImplementedWrite ? "Y" : "-") + "/" +
                            (route.ImplementedExecute ? "Y" : "-") +
                            " | verified R/W/E=" +
                            (route.CanRead == true ? "Y" : "?") + "/" +
                            (route.CanWrite == true ? "Y" : "?") + "/" +
                            (route.CanExecute == true ? "Y" : "?"));
                    Console.WriteLine("Routes are discovery metadata, not executable commands.");
                }
                Console.WriteLine("Reported zones: " +
                    Show(features == null ? (int?)null : features.ReportedZoneCount));
                Console.WriteLine("Observed YNCA/Extended zones: " +
                    string.Join(", ", receiver.Zones.Select(z => z.NativeId)));
                Console.WriteLine("Reported speaker patterns: " +
                    Show(features == null ? (int?)null : features.SpeakerPatternCount));
                Console.WriteLine("Active speaker pattern (Extended): " +
                    Show(status == null ? (int?)null : status.ActiveSpeakerPattern));

                if (features != null)
                {
                    foreach (var zone in features.Zones)
                        Console.WriteLine("Zone " + zone.NativeId + ": inputs=" + zone.Inputs.Count +
                            ", sound programs=" + zone.SoundPrograms.Count +
                            ", scenes=" + Show(zone.SceneCount));
                    if (receiver.Sound != null &&
                        Confirm("Read sound/DSP status and advertised scenes? [y/N]: "))
                    {
                        foreach (var zone in features.Zones)
                        {
                            try
                            {
                                YamahaSoundStatus sound = await receiver.Sound.GetStatusAsync(zone.NativeId);
                                Console.WriteLine("Zone " + zone.NativeId + ": program=" + Show(sound.SoundProgramNativeId) +
                                    ", pure direct=" + ShowBool(sound.PureDirectEnabled) +
                                    ", enhancer=" + ShowBool(sound.EnhancerEnabled) +
                                    ", decoder=" + Show(sound.SurroundDecoderNativeId));
                                Console.WriteLine("  Available programs: " + string.Join(", ",
                                    receiver.Sound.GetAdvertisedSoundPrograms(zone.NativeId)));
                                Console.WriteLine("  Advertised scenes: " +
                                    Show(receiver.Sound.GetAdvertisedSceneCount(zone.NativeId)));
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("  Zone " + zone.NativeId + " sound read unavailable: " + ex.Message);
                            }
                        }
                    }
                }
                else Console.WriteLine("Extended Control feature lists unavailable; other protocols remain usable.");

                if (receiver.Sound != null && features != null &&
                    Confirm("Read audio signal info (no playback required)? [y/N]: "))
                {
                    foreach (var zone in features.Zones.Where(z => z.Functions.Any(f =>
                        string.Equals(f, "signal_info", StringComparison.OrdinalIgnoreCase))))
                    {
                        try
                        {
                            var signal = await receiver.Sound.GetSignalInfoAsync(zone.NativeId);
                            Console.WriteLine("Zone " + zone.NativeId + " signal: format=" +
                                Show(signal.AudioFormat) + ", Fs=" + Show(signal.SampleRate) +
                                ", error=" + Show(signal.AudioErrorCode));
                            foreach (var field in signal.AudioFields)
                                Console.WriteLine("  audio." + field.Key + "=" + field.Value);
                            foreach (var field in signal.OtherFields)
                                Console.WriteLine("  " + field.Key + "=" + field.Value);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Zone " + zone.NativeId + " signal read unavailable: " + ex.Message);
                        }
                    }
                    Console.WriteLine("Unknown signal fields do not prove absence of an audio feature.");
                }
                if (receiver.Sound != null && receiver.Ynca != null &&
                    Confirm("Read YNCA MAIN advanced DSP settings? [y/N]: "))
                {
                    try
                    {
                        var dsp = await receiver.Sound.GetMainYncaDspStatusAsync();
                        foreach (var value in dsp.Values)
                            Console.WriteLine("  MAIN:" + value.Key + "=" + value.Value);
                        foreach (var missing in dsp.Unavailable)
                            Console.WriteLine("  MAIN:" + missing.Key + "=" + missing.Value);
                        Console.WriteLine("YNCA SOUNDPRG is stored DSP; Straight may override playback.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("YNCA DSP read failed: " + ex.Message);
                    }
                }

                if (!Confirm("Read legacy XML speaker settings? [y/N]: "))
                {
                    await ReadMonitoringAsync(receiver);
                    await ReadYncaAsync(receiver);
                    return 0;
                }

                try
                {
                    int? active = await receiver.Speakers.GetActivePatternAsync();
                    Console.WriteLine("Active speaker pattern (Legacy XML): " + Show(active));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Active pattern read failed: " + ex.Message);
                }

                int? count = features == null ? (int?)null : features.SpeakerPatternCount;
                if (!count.HasValue || count.Value < 1 || count.Value > 32)
                    Console.WriteLine("Pattern count unknown; explicit reads do not imply pattern support.");
                int upper = count.HasValue && count.Value >= 1 && count.Value <= 32 ? count.Value : 32;
                while (true)
                {
                    Console.Write("Pattern to read [1-" + upper + "] (Enter to finish): ");
                    string selection = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(selection))
                        break;
                    int pattern;
                    if (!int.TryParse(selection.Trim(), NumberStyles.None,
                        CultureInfo.InvariantCulture, out pattern) || pattern < 1 || pattern > upper)
                    {
                        Console.WriteLine("Enter a valid pattern number.");
                        continue;
                    }
                    await DisplayPatternAsync(receiver, pattern);
                }
                await ReadMonitoringAsync(receiver);
                await ReadYncaAsync(receiver);
            }
            return 0;
        }

        private static async Task ReadMonitoringAsync(YamahaAvrClient receiver)
        {
            Console.WriteLine();
            if (!Confirm("Monitor read-only state (30 s polls; Enter to stop)? [y/N]: ")) return;
            EventHandler<YamahaMonitorPollEventArgs> poll = (sender, e) =>
            {
                Console.WriteLine("Monitor at " + e.ObservedUtc.ToString("HH:mm:ss", CultureInfo.InvariantCulture) +
                    " UTC: successful read paths=" + e.SuccessfulReads +
                    (string.IsNullOrEmpty(e.Error) ? "" : " | partial failures=" + e.Error));
            };
            EventHandler<YamahaZoneStateChangedEventArgs> zoneChanged = (sender, e) =>
            {
                var z = e.State;
                Console.WriteLine("  " + z.NativeId + ": power=" + ShowBool(z.IsPoweredOn) +
                    " volume=" + Show(z.VolumeDb) + " dB" +
                    " mute=" + ShowBool(z.IsMuted) + " input=" + Show(z.InputNativeId) +
                    (z.IsStale ? " [stale]" : ""));
            };
            receiver.Monitor.PollCompleted += poll;
            if (receiver.ZoneControl != null) receiver.ZoneControl.ZoneStateChanged += zoneChanged;
            try
            {
                receiver.Monitor.Start(TimeSpan.FromSeconds(30));
                Console.WriteLine("Read-only monitor started. Press Enter to stop.");
                Console.ReadLine();
            }
            finally
            {
                await receiver.Monitor.StopAsync();
                receiver.Monitor.PollCompleted -= poll;
                if (receiver.ZoneControl != null) receiver.ZoneControl.ZoneStateChanged -= zoneChanged;
                Console.WriteLine("Monitoring stopped.");
            }
        }

        private static async Task ReadYncaAsync(YamahaAvrClient receiver)
        {
            Console.WriteLine();
            if (receiver.Ynca == null) return;
            if (!Confirm("Read YNCA status over TCP port 50000? [y/N]: ")) return;
            Console.WriteLine("YNCA only permits one connection on many receivers. Close other YNCA tools first.");
            try
            {
                await receiver.Ynca.ConnectAsync();
                Console.WriteLine("YNCA connected. Reading Main Zone and model (GET only):");
                foreach (var request in new[]
                {
                    "SYS:MODELNAME", "MAIN:PWR", "MAIN:VOL", "MAIN:MUTE", "MAIN:INP"
                })
                {
                    string[] parts = request.Split(':');
                    await ShowYncaAsync(receiver, parts[0], parts[1]);
                }
                Console.WriteLine();
                if (Confirm("Read the detected zones via YNCA? [y/N]: "))
                {
                    foreach (var feature in receiver.Zones)
                    {
                        try
                        {
                            YamahaZoneState zone = await receiver.ZoneControl.RefreshAsync(feature.NativeId);
                            Console.WriteLine("Zone " + zone.NativeId +
                                ": power=" + (zone.IsPoweredOn.HasValue ? (zone.IsPoweredOn.Value ? "On" : "Standby") : "unknown") +
                                ", volume=" + (zone.VolumeDb.HasValue
                                    ? zone.VolumeDb.Value.ToString(CultureInfo.InvariantCulture) + " dB" : "unknown") +
                                ", mute=" + (zone.IsMuted.HasValue ? zone.IsMuted.Value.ToString() : "unknown") +
                                ", input=" + Show(zone.InputNativeId) +
                                (zone.IsStale ? " [stale]" : ""));
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Zone " + feature.NativeId + " unavailable: " + ex.Message);
                        }
                    }
                }
                Console.WriteLine();
                if (Confirm("Read Main Zone YNCA sound/DSP values? [y/N]: "))
                {
                    foreach (string field in new[] { "SOUNDPRG", "STRAIGHT", "PUREDIRMODE", "ENHANCER" })
                        await ShowYncaAsync(receiver, "MAIN", field);
                }
                Console.WriteLine();
                Console.WriteLine("Optional YNCA GET: enter SUBUNIT:FUNCTION (e.g. MAIN:VOL).");
                Console.WriteLine("Press Enter to close the YNCA connection.");
                while (true)
                {
                    Console.Write("YNCA GET> ");
                    string input = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(input)) break;
                    string[] parts = input.Trim().Split(':');
                    if (parts.Length != 2)
                    {
                        Console.WriteLine("Use SUBUNIT:FUNCTION. Writes are not supported here.");
                        continue;
                    }
                    await ShowYncaAsync(receiver, parts[0], parts[1]);
                }
            }
            catch (Exception ex) { Console.WriteLine("YNCA connection failed: " + ex.Message); }
            finally { receiver.Ynca.Disconnect(); }
        }

        private static async Task ShowYncaAsync(YamahaAvrClient receiver, string subunit, string function)
        {
            try
            {
                var observed = await receiver.Ynca.GetAsync(subunit, function);
                Console.WriteLine(observed.Raw + "  (observed; could also be a push update)");
            }
            catch (Exception ex)
            {
                Console.WriteLine(subunit + ":" + function + " unavailable: " + ex.Message);
            }
        }

        private static async Task DisplayPatternAsync(YamahaAvrClient receiver, int pattern)
        {
            try
            {
                var levels = await receiver.Speakers.GetLevelsAsync(pattern);
                var distances = await receiver.Speakers.GetDistancesAsync(pattern);
                var channelIds = new SortedSet<string>(levels.Keys, StringComparer.Ordinal);
                channelIds.UnionWith(distances.Keys);
                Console.WriteLine();
                Console.WriteLine("Pattern " + pattern + " - native speaker channels");
                Console.WriteLine("{0,-25} {1,14} {2,14}", "Channel", "Level", "Distance");
                foreach (string id in channelIds)
                {
                    YamahaSpeakerValue level;
                    YamahaSpeakerValue distance;
                    string levelText = levels.TryGetValue(id, out level)
                        ? FormatValue(level) : "n/a";
                    string distanceText = distances.TryGetValue(id, out distance)
                        ? FormatValue(distance) : "n/a";
                    Console.WriteLine("{0,-25} {1,14} {2,14}", id, levelText, distanceText);
                }
                Console.WriteLine("Channel presence does not imply that a speaker is configured.");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Pattern " + pattern + " could not be read: " + ex.Message);
            }
        }

        private static string FormatValue(YamahaSpeakerValue value)
        {
            return value.Value.ToString(CultureInfo.InvariantCulture) + " " + value.Unit;
        }

        private static bool Confirm(string prompt)
        {
            Console.Write(prompt);
            string input = Console.ReadLine();
            return string.Equals(input == null ? null : input.Trim(), "y",
                StringComparison.OrdinalIgnoreCase);
        }

        private static string ReadHost()
        {
            while (true)
            {
                Console.Write("Receiver IP address or hostname (Enter to quit): ");
                string input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input))
                    return null;
                string host = input.Trim();
                if (Uri.CheckHostName(host) != UriHostNameType.Unknown)
                    return host;
                Console.WriteLine("Enter an IP address or hostname, not a URL.");
            }
        }

        private static int? ReadHttpPort()
        {
            while (true)
            {
                Console.Write("HTTP port [80]: ");
                string input = Console.ReadLine();
                if (input == null)
                    return null;
                if (string.IsNullOrWhiteSpace(input))
                    return 80;
                int port;
                if (int.TryParse(input.Trim(), NumberStyles.None,
                    CultureInfo.InvariantCulture, out port) && port >= 1 && port <= 65535)
                    return port;
                Console.WriteLine("Enter an HTTP port from 1 to 65535.");
            }
        }

        private static string ShowBool(bool? value)
        {
            return value.HasValue ? (value.Value ? "On" : "Off") : "unknown";
        }

        private static string Show(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "unknown" : value;
        }

        private static string Show(decimal? value)
        {
            return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "unknown";
        }

        private static string Show(int? value)
        {
            return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "unknown";
        }
    }
}
