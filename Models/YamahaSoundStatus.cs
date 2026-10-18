using System;

namespace YamahaAvrNet.Models
{
    /// <summary>Latest read-only Extended Control sound status for a single zone.</summary>
    public sealed class YamahaSoundStatus
    {
        internal YamahaSoundStatus(string zone, string program, bool? pureDirect,
            bool? enhancer, bool? direct, string surroundDecoder, bool? surround3d,
            DateTimeOffset observedUtc)
        {
            ZoneNativeId = zone;
            SoundProgramNativeId = program;
            PureDirectEnabled = pureDirect;
            EnhancerEnabled = enhancer;
            DirectEnabled = direct;
            SurroundDecoderNativeId = surroundDecoder;
            Surround3dEnabled = surround3d;
            ObservedUtc = observedUtc;
        }

        public string ZoneNativeId { get; private set; }
        public string SoundProgramNativeId { get; private set; }
        public bool? PureDirectEnabled { get; private set; }
        public bool? EnhancerEnabled { get; private set; }
        public bool? DirectEnabled { get; private set; }
        public string SurroundDecoderNativeId { get; private set; }
        /// <summary>Extended Control surround_3d, not necessarily the YNCA 3DCINEMA field.</summary>
        public bool? Surround3dEnabled { get; private set; }
        public DateTimeOffset ObservedUtc { get; private set; }
    }
}
