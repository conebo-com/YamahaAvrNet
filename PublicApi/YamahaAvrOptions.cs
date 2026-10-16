using System;

namespace YamahaAvrNet.PublicApi
{
    /// <summary>Connection preferences. No connection is opened by constructing options.</summary>
    public sealed class YamahaAvrOptions
    {
        public YamahaAvrOptions()
        {
            YncaPort = 50000;
            HttpPort = 80;
            EnableYnca = true;
            EnableExtendedControl = true;
            EnableLegacyXml = true;
            RequestTimeout = TimeSpan.FromSeconds(5);
        }

        public string Host { get; set; }
        public int YncaPort { get; set; }
        public int HttpPort { get; set; }
        public bool EnableYnca { get; set; }
        public bool EnableExtendedControl { get; set; }
        public bool EnableLegacyXml { get; set; }
        public TimeSpan RequestTimeout { get; set; }

        /// <summary>
        /// Explicit opt-in for the narrowly verified RX-A3060 speaker writes.
        /// Defaults to false and never enables writes during discovery.
        /// </summary>
        public bool EnableVerifiedSpeakerWrites { get; set; }

        /// <summary>
        /// Explicit opt-in for manual YNCA zone power/volume/mute/input writes.
        /// This does not authorize any writes during discovery or refresh.
        /// </summary>
        public bool EnableYncaZoneWrites { get; set; }

        internal YamahaAvrOptions CopyValidated()
        {
            if (string.IsNullOrWhiteSpace(Host))
                throw new ArgumentException("Host must be provided.", nameof(Host));

            string host = Host.Trim();
            if (Uri.CheckHostName(host) == UriHostNameType.Unknown)
                throw new ArgumentException("Host must be a DNS name, IPv4 or IPv6 address, not a URL.", nameof(Host));
            if (YncaPort < 1 || YncaPort > 65535)
                throw new ArgumentOutOfRangeException(nameof(YncaPort));
            if (HttpPort < 1 || HttpPort > 65535)
                throw new ArgumentOutOfRangeException(nameof(HttpPort));
            if (!EnableYnca && !EnableExtendedControl && !EnableLegacyXml)
                throw new ArgumentException("At least one protocol must be enabled.");
            if (RequestTimeout <= TimeSpan.Zero ||
                RequestTimeout.TotalMilliseconds > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(RequestTimeout));

            return new YamahaAvrOptions
            {
                Host = host,
                YncaPort = YncaPort,
                HttpPort = HttpPort,
                EnableYnca = EnableYnca,
                EnableExtendedControl = EnableExtendedControl,
                EnableLegacyXml = EnableLegacyXml,
                RequestTimeout = RequestTimeout,
                EnableVerifiedSpeakerWrites = EnableVerifiedSpeakerWrites,
                EnableYncaZoneWrites = EnableYncaZoneWrites
            };
        }
    }
}
