using System;
using System.Globalization;
using System.Threading.Tasks;
using YamahaAvrNet.PublicApi;

namespace YamahaAvrNet.Sample
{
    internal static class Program
    {
        private static int Main()
        {
            int exitCode;
            try
            {
                exitCode = RunAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("Receiver discovery failed: " + ex.Message);
                exitCode = 1;
            }

            if (!Console.IsInputRedirected)
            {
                Console.WriteLine();
                Console.Write("Press Enter to exit...");
                Console.ReadLine();
            }

            return exitCode;
        }

        private static async Task<int> RunAsync()
        {
            Console.WriteLine("YamahaAvrNet - read-only Extended Control discovery");
            Console.WriteLine("No receiver settings will be changed.");
            Console.WriteLine();

            string host = ReadHost();
            if (host == null)
                return 0;

            int? port = ReadHttpPort();
            if (!port.HasValue)
                return 0;

            Console.WriteLine();
            Console.WriteLine("Connecting to " + host + ":" + port.Value + "...");
            Console.WriteLine();

            using (var receiver = new YamahaAvrClient(new YamahaAvrOptions
            {
                Host = host,
                HttpPort = port.Value,
                EnableYnca = false,
                EnableLegacyXml = false,
                EnableExtendedControl = true
            }))
            {
                await receiver.InitializeAsync();

                var info = receiver.DeviceInfo;
                var features = receiver.DeviceFeatures;
                var status = receiver.FunctionStatus;

                Console.WriteLine("Model: " + (info.ModelName ?? "unknown"));
                Console.WriteLine("Firmware: " + (info.SystemVersion ?? "unknown"));
                Console.WriteLine("Extended Control API: " + (info.ApiVersion ?? "unknown"));
                Console.WriteLine("Reported zones: " + Format(features.ReportedZoneCount));
                Console.WriteLine("Reported speaker patterns: " + Format(features.SpeakerPatternCount));
                Console.WriteLine("Active speaker pattern: " + Format(status.ActiveSpeakerPattern));

                foreach (var zone in features.Zones)
                {
                    Console.WriteLine("Zone " + zone.NativeId + ": inputs=" + zone.Inputs.Count +
                        ", sound programs=" + zone.SoundPrograms.Count +
                        ", scenes=" + Format(zone.SceneCount));
                }
            }

            return 0;
        }

        private static string ReadHost()
        {
            while (true)
            {
                Console.Write("Receiver IP address or hostname (Enter to quit): ");
                string input = Console.ReadLine();
                if (input == null || string.IsNullOrWhiteSpace(input))
                    return null;

                string host = input.Trim();
                if (Uri.CheckHostName(host) != UriHostNameType.Unknown)
                    return host;

                Console.WriteLine("Invalid IP address or hostname. Enter a host, not a URL.");
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

                Console.WriteLine("Invalid HTTP port. Enter a number from 1 to 65535.");
            }
        }

        private static string Format(int? value)
        {
            return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "unknown";
        }
    }
}
