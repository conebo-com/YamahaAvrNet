using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace YamahaAvrNet.Models
{
    /// <summary>
    /// Read-only signal information returned by Extended Control getSignalInfo.
    /// Empty, missing and "---" fields are unknown; they do not mean silence or no support.
    /// This is a snapshot, not evidence that audio is currently playing.
    /// </summary>
    public sealed class YamahaAudioSignalInfo
    {
        internal YamahaAudioSignalInfo(string zone, int? error, string format,
            string sampleRate, IDictionary<string, string> audioFields,
            IDictionary<string, string> otherFields, DateTimeOffset observedUtc)
        {
            ZoneNativeId = zone;
            AudioErrorCode = error;
            AudioFormat = format;
            SampleRate = sampleRate;
            AudioFields = new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(audioFields, StringComparer.OrdinalIgnoreCase));
            OtherFields = new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(otherFields, StringComparer.OrdinalIgnoreCase));
            ObservedUtc = observedUtc;
        }

        public string ZoneNativeId { get; private set; }
        public int? AudioErrorCode { get; private set; }
        /// <summary>Yamaha-native format text, not a normalized Dolby/DTS codec enum.</summary>
        public string AudioFormat { get; private set; }
        /// <summary>Yamaha-native Fs text, e.g. "48 kHz"; no units are inferred.</summary>
        public string SampleRate { get; private set; }
        /// <summary>All scalar values returned inside audio, including unknown future fields.</summary>
        public IReadOnlyDictionary<string, string> AudioFields { get; private set; }
        /// <summary>Other top-level object/scalar fields as compact JSON for diagnostics.</summary>
        public IReadOnlyDictionary<string, string> OtherFields { get; private set; }
        public DateTimeOffset ObservedUtc { get; private set; }
    }
}
