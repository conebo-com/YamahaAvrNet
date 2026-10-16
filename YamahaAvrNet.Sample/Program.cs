using System;
using System.Globalization;
using System.Threading.Tasks;
using YamahaAvrNet.PublicApi;

namespace YamahaAvrNet.Sample
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                return RunAsync(args).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Receiver discovery failed: " + ex.Message);
                return 1;
            }
        }

        private static async Task<int> RunAsync(string[] args)
        {
            if (args.Length < 1 || args.Length > 2)
            {
                Console.WriteLine("YamahaAvrNet - read-only Extended Control discovery");
                Console.WriteLine("Usage: YamahaAvrNet.Sample.exe <host> [http-port]");
                Console.WriteLine("No request is sent unless a host is supplied.");
                return 0;
            }

            int port = 80;
            if (args.Length == 2 && (!int.TryParse(args[1], NumberStyles.None,
                CultureInfo.InvariantCulture, out port) || port < 1 || port > 65535))
                throw new ArgumentException("The HTTP port must be between 1 and 65535.");

            using (var receiver = new YamahaAvrClient(new YamahaAvrOptions
            {
                Host = args[0],
                HttpPort = port,
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

        private static string Format(int? value)
        {
            return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "unknown";
        }
    }
}
