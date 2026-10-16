using System;
using YamahaAvrNet.Models;

namespace YamahaAvrNet.PublicApi
{
    /// <summary>
    /// A point-in-time state snapshot. Unobserved properties remain null.
    /// Later phases will extend this with zone/audio/speaker snapshots.
    /// </summary>
    public sealed class YamahaReceiverState
    {
        public YamahaReceiverState(YamahaConnectionState connectionState,
            DateTimeOffset? lastUpdatedUtc, bool isStale, string model)
        {
            if (lastUpdatedUtc.HasValue && lastUpdatedUtc.Value.Offset != TimeSpan.Zero)
                throw new ArgumentException("Timestamp must be UTC.", nameof(lastUpdatedUtc));

            ConnectionState = connectionState;
            LastUpdatedUtc = lastUpdatedUtc;
            IsStale = isStale;
            Model = model;
        }

        public YamahaConnectionState ConnectionState { get; private set; }
        public DateTimeOffset? LastUpdatedUtc { get; private set; }
        public bool IsStale { get; private set; }
        public string Model { get; private set; }
    }
}
