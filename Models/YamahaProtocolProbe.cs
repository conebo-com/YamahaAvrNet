using System;

namespace YamahaAvrNet.Models
{
    /// <summary>
    /// A protocol probe reports endpoint reachability, not support for every command.
    /// Unavailable is not the same as permanently unsupported.
    /// </summary>
    public enum YamahaProtocolAvailability
    {
        NotChecked = 0,
        Disabled = 1,
        Available = 2,
        Unavailable = 3
    }

    public sealed class YamahaProtocolProbe
    {
        public YamahaProtocolProbe(string protocol, YamahaProtocolAvailability availability,
            string detail, DateTimeOffset? observedUtc)
        {
            if (string.IsNullOrWhiteSpace(protocol))
                throw new ArgumentException("A protocol name is required.", nameof(protocol));
            Protocol = protocol;
            Availability = availability;
            Detail = detail;
            ObservedUtc = observedUtc;
        }

        public string Protocol { get; private set; }
        public YamahaProtocolAvailability Availability { get; private set; }
        public string Detail { get; private set; }
        public DateTimeOffset? ObservedUtc { get; private set; }
    }
}
