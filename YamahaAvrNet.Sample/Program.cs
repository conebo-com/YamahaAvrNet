using System;
using YamahaAvrNet;
using YamahaAvrNet.PublicApi;

namespace YamahaAvrNet.Sample
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            Console.WriteLine(YamahaAvrNetInfo.LibraryName + " / " +
                YamahaAvrNetInfo.TargetFrameworkMoniker);

            using (var receiver = new YamahaAvrClient(new YamahaAvrOptions
            {
                Host = "192.0.2.1" // RFC 5737 documentation address; never contacted.
            }))
            {
                Console.WriteLine("Hersteller: " + receiver.Manufacturer);
                Console.WriteLine("Verbindungsstatus: " + receiver.ConnectionState);
                Console.WriteLine("Speaker capability: " +
                    receiver.GetCapability("speaker.level").Status);
                Console.WriteLine("Phase 1: kein Netzwerkzugriff, kein Schreibbefehl.");
            }

            return 0;
        }
    }
}
