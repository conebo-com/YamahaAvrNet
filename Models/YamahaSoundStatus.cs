using System;

namespace YamahaAvrNet.Models
{
    /// <summary>Latest read-only Extended Control sound status for a single zone.</summary>
    public sealed class YamahaSoundStatus
    {
        internal YamahaSoundStatus(string zone, string program, bool? pureDirect,
            bool? enhancer, bool? direct, string surroundDecoder, DateTimeOffset observedUtc)
        {
            ZoneNativeId = zone;
            SoundProgramNativeId = program;
            PureDirectEnabled = pureDirect;
            EnhancerEnabled = enhancer;
            DirectEnabled = direct;
            SurroundDecoderNativeId = surroundDecoder;
            ObservedUtc = observedUtc;
        }

        public string ZoneNativeId { get; private set; }
        public string SoundProgramNativeId { get; private set; }
        public bool? PureDirectEnabled { get; private set; }
        public bool? EnhancerEnabled { get; private set; }
        public bool? DirectEnabled { get; private set; }
        public string SurroundDecoderNativeId { get; private set; }
        public DateTimeOffset ObservedUtc { get; private set; }
    }
}
