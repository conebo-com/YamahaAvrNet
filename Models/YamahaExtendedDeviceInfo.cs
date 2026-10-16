using System;

namespace YamahaAvrNet.Models
{
    /// <summary>Device identity obtained from Extended Control getDeviceInfo.</summary>
    public sealed class YamahaExtendedDeviceInfo
    {
        internal YamahaExtendedDeviceInfo(string modelName, string systemVersion,
            string apiVersion, string destination, string networkModuleVersion,
            int? networkModuleGeneration, DateTimeOffset observedUtc)
        {
            ModelName = modelName;
            SystemVersion = systemVersion;
            ApiVersion = apiVersion;
            Destination = destination;
            NetworkModuleVersion = networkModuleVersion;
            NetworkModuleGeneration = networkModuleGeneration;
            ObservedUtc = observedUtc;
        }

        public string ModelName { get; private set; }
        public string SystemVersion { get; private set; }
        public string ApiVersion { get; private set; }
        public string Destination { get; private set; }
        public string NetworkModuleVersion { get; private set; }
        public int? NetworkModuleGeneration { get; private set; }
        public DateTimeOffset ObservedUtc { get; private set; }
    }
}
