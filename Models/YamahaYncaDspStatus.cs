using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace YamahaAvrNet.Models
{
    /// <summary>
    /// Observed, native YNCA MAIN DSP fields. SOUNDPRG is the stored DSP name;
    /// it is not necessarily the effective playback mode when STRAIGHT is On.
    /// </summary>
    public sealed class YamahaYncaDspStatus
    {
        internal YamahaYncaDspStatus(IDictionary<string, string> observed,
            IDictionary<string, string> unavailable, DateTimeOffset observedUtc)
        {
            Values = new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(observed, StringComparer.OrdinalIgnoreCase));
            Unavailable = new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(unavailable, StringComparer.OrdinalIgnoreCase));
            ObservedUtc = observedUtc;
        }

        /// <summary>Native YNCA function name to its reported value.</summary>
        public IReadOnlyDictionary<string, string> Values { get; private set; }
        /// <summary>Commands returning UNDEFINED or RESTRICTED, not inferred permanent absence.</summary>
        public IReadOnlyDictionary<string, string> Unavailable { get; private set; }
        public DateTimeOffset ObservedUtc { get; private set; }
    }
}
