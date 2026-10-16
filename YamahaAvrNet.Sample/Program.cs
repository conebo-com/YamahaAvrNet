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
            Console.WriteLine("YamahaAvrNet - receiver information and speaker settings");
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

                Console.WriteLine("Model: " + Show(info.ModelName));
                Console.WriteLine("Firmware: " + Show(info.SystemVersion));
                Console.WriteLine("Extended Control API: " + Show(info.ApiVersion));
                Console.WriteLine("Reported zones: " + Show(features.ReportedZoneCount));
                Console.WriteLine("Reported speaker patterns: " + Show(features.SpeakerPatternCount));
                Console.WriteLine("Active speaker pattern (Extended): " + Show(status.ActiveSpeakerPattern));

                foreach (var zone in features.Zones)
                    Console.WriteLine("Zone " + zone.NativeId + ": inputs=" + zone.Inputs.Count +
                        ", sound programs=" + zone.SoundPrograms.Count +
                        ", scenes=" + Show(zone.SceneCount));

                if (!Confirm("Read legacy XML speaker settings? [y/N]: "))
                {
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

                int? count = features.SpeakerPatternCount;
                if (!count.HasValue || count.Value < 1 || count.Value > 32)
                {
                    Console.WriteLine("Pattern count not available. No automatic pattern enumeration.");
                    await ReadYncaAsync(receiver);
                    return 0;
                }

                while (true)
                {
                    Console.Write("Pattern to read [1-" + count.Value + "] (Enter to finish): ");
                    string selection = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(selection))
                        break;
                    int pattern;
                    if (!int.TryParse(selection.Trim(), NumberStyles.None,
                        CultureInfo.InvariantCulture, out pattern) || pattern < 1 || pattern > count.Value)
                    {
                        Console.WriteLine("Enter a valid pattern number.");
                        continue;
                    }
                    await DisplayPatternAsync(receiver, pattern);
                }
                await ReadYncaAsync(receiver);
            }
            return 0;
        }

        private static async Task ReadYncaAsync(YamahaAvrClient receiver)
        {
            Console.WriteLine();
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

        private static string Show(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "unknown" : value;
        }

        private static string Show(int? value)
        {
            return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "unknown";
        }
    }
}
