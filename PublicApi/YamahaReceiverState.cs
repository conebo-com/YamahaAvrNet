using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using YamahaAvrNet.Models;

namespace YamahaAvrNet.PublicApi
{
    /// <summary>
    /// A point-in-time state snapshot. Unobserved properties remain null.
    /// YNCA zone observations are included when available; unknown values remain null.
    /// </summary>
    public sealed class YamahaReceiverState
    {
        public YamahaReceiverState(YamahaConnectionState connectionState,
            DateTimeOffset? lastUpdatedUtc, bool isStale, string model, IReadOnlyDictionary<string, YamahaZoneState> zones = null)
        {
            if (lastUpdatedUtc.HasValue && lastUpdatedUtc.Value.Offset != TimeSpan.Zero)
                throw new ArgumentException("Timestamp must be UTC.", nameof(lastUpdatedUtc));

            ConnectionState = connectionState;
            LastUpdatedUtc = lastUpdatedUtc;
            IsStale = isStale;
            Model = model;
            var copy = new Dictionary<string, YamahaZoneState>(StringComparer.OrdinalIgnoreCase);
            if (zones != null)
                foreach (var pair in zones)
                    copy[pair.Key] = pair.Value;
            Zones = new ReadOnlyDictionary<string, YamahaZoneState>(copy);
        }

        public YamahaConnectionState ConnectionState { get; private set; }
        public DateTimeOffset? LastUpdatedUtc { get; private set; }
        public bool IsStale { get; private set; }
        public string Model { get; private set; }
        public IReadOnlyDictionary<string, YamahaZoneState> Zones { get; private set; }
    }
}
