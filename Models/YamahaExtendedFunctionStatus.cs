using System;
using System.Collections.Generic;

namespace YamahaAvrNet.Models
{
    /// <summary>
    /// Actual system status returned by getFuncStatus. Additional native keys are
    /// preserved as JSON literals in Values without assuming their semantics.
    /// </summary>
    public sealed class YamahaExtendedFunctionStatus
    {
        internal YamahaExtendedFunctionStatus(int? activeSpeakerPattern,
            bool? partyMode, IReadOnlyDictionary<string, string> values,
            DateTimeOffset observedUtc)
        {
            ActiveSpeakerPattern = activeSpeakerPattern;
            PartyMode = partyMode;
            Values = values;
            ObservedUtc = observedUtc;
        }

        public int? ActiveSpeakerPattern { get; private set; }
        public bool? PartyMode { get; private set; }
        public IReadOnlyDictionary<string, string> Values { get; private set; }
        public DateTimeOffset ObservedUtc { get; private set; }
    }
}
