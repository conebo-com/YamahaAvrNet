using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using YamahaAvrNet.Models;

namespace YamahaAvrNet.PublicApi
{
    /// <summary>
    /// Immutable, read-only receiver overview captured at one API boundary.
    /// A capability or function route describes evidence, never authorization.
    /// Unknown and stale state remains distinguishable from unsupported features.
    /// </summary>
    public sealed class YamahaReceiverOverview
    {
        internal YamahaReceiverOverview(YamahaReceiverState state,
            IEnumerable<YamahaZoneInfo> discoveredZones,
            IDictionary<string, YamahaProtocolProbe> protocols,
            IEnumerable<YamahaCapability> capabilities,
            IEnumerable<YamahaFunctionRoute> functionRoutes,
            DateTimeOffset capturedUtc)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            if (discoveredZones == null) throw new ArgumentNullException(nameof(discoveredZones));
            if (protocols == null) throw new ArgumentNullException(nameof(protocols));
            if (capabilities == null) throw new ArgumentNullException(nameof(capabilities));
            if (functionRoutes == null) throw new ArgumentNullException(nameof(functionRoutes));
            if (capturedUtc.Offset != TimeSpan.Zero)
                throw new ArgumentException("Capture timestamp must be UTC.", nameof(capturedUtc));

            CapturedUtc = capturedUtc;
            DiscoveredZones = new ReadOnlyCollection<YamahaZoneInfo>(new List<YamahaZoneInfo>(discoveredZones));
            Protocols = new ReadOnlyDictionary<string, YamahaProtocolProbe>(
                new Dictionary<string, YamahaProtocolProbe>(protocols, StringComparer.OrdinalIgnoreCase));
            Capabilities = new ReadOnlyCollection<YamahaCapability>(new List<YamahaCapability>(capabilities));
            FunctionRoutes = new ReadOnlyCollection<YamahaFunctionRoute>(new List<YamahaFunctionRoute>(functionRoutes));
        }

        /// <summary>Local UTC capture time; not the time every receiver field was updated.</summary>
        public DateTimeOffset CapturedUtc { get; private set; }
        public YamahaReceiverState State { get; private set; }
        public IReadOnlyList<YamahaZoneInfo> DiscoveredZones { get; private set; }
        public IReadOnlyDictionary<string, YamahaProtocolProbe> Protocols { get; private set; }
        public IReadOnlyList<YamahaCapability> Capabilities { get; private set; }
        public IReadOnlyList<YamahaFunctionRoute> FunctionRoutes { get; private set; }
    }
}
