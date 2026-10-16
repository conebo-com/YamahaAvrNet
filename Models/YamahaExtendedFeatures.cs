using System;
using System.Collections.Generic;

namespace YamahaAvrNet.Models
{
    /// <summary>Feature lists reported by one receiver, not a model-wide compatibility guarantee.</summary>
    public sealed class YamahaExtendedFeatures
    {
        internal YamahaExtendedFeatures(IReadOnlyList<string> systemFunctions,
            IReadOnlyList<YamahaExtendedInputInfo> systemInputs,
            IReadOnlyList<YamahaExtendedZoneFeatures> zones,
            int? reportedZoneCount, int? speakerPatternCount,
            int? tunerPresetCount, int? netUsbPresetCount,
            DateTimeOffset observedUtc)
        {
            SystemFunctions = systemFunctions;
            SystemInputs = systemInputs;
            Zones = zones;
            ReportedZoneCount = reportedZoneCount;
            SpeakerPatternCount = speakerPatternCount;
            TunerPresetCount = tunerPresetCount;
            NetUsbPresetCount = netUsbPresetCount;
            ObservedUtc = observedUtc;
        }

        public IReadOnlyList<string> SystemFunctions { get; private set; }
        public IReadOnlyList<YamahaExtendedInputInfo> SystemInputs { get; private set; }
        public IReadOnlyList<YamahaExtendedZoneFeatures> Zones { get; private set; }
        /// <summary>A count reported by the device; it is not inferred from the array length.</summary>
        public int? ReportedZoneCount { get; private set; }
        public int? SpeakerPatternCount { get; private set; }
        public int? TunerPresetCount { get; private set; }
        public int? NetUsbPresetCount { get; private set; }
        public DateTimeOffset ObservedUtc { get; private set; }
    }

    /// <summary>System input definition, preserving the Yamaha-native input ID.</summary>
    public sealed class YamahaExtendedInputInfo
    {
        internal YamahaExtendedInputInfo(string nativeId, string playInfoType,
            bool? canRename, bool? canDistribute)
        {
            NativeId = nativeId;
            PlayInfoType = playInfoType;
            CanRename = canRename;
            CanDistribute = canDistribute;
        }

        public string NativeId { get; private set; }
        public string PlayInfoType { get; private set; }
        public bool? CanRename { get; private set; }
        public bool? CanDistribute { get; private set; }
    }

    /// <summary>Feature availability is determined separately for every native zone.</summary>
    public sealed class YamahaExtendedZoneFeatures
    {
        internal YamahaExtendedZoneFeatures(string nativeId,
            IReadOnlyList<string> functions, IReadOnlyList<string> inputs,
            IReadOnlyList<string> soundPrograms, int? sceneCount,
            IReadOnlyDictionary<string, YamahaExtendedNumericRange> ranges,
            IReadOnlyDictionary<string, IReadOnlyList<string>> optionLists)
        {
            NativeId = nativeId;
            Functions = functions;
            Inputs = inputs;
            SoundPrograms = soundPrograms;
            SceneCount = sceneCount;
            Ranges = ranges;
            OptionLists = optionLists;
        }

        public string NativeId { get; private set; }
        public IReadOnlyList<string> Functions { get; private set; }
        public IReadOnlyList<string> Inputs { get; private set; }
        public IReadOnlyList<string> SoundPrograms { get; private set; }
        public int? SceneCount { get; private set; }
        /// <summary>Ranges are keyed by native ID, for example volume or actual_volume_db.</summary>
        public IReadOnlyDictionary<string, YamahaExtendedNumericRange> Ranges { get; private set; }
        /// <summary>Additional *_list properties, such as surr_decoder_type_list.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> OptionLists { get; private set; }
    }

    /// <summary>Numbers are retained in their native units; volume and actual_volume_db differ.</summary>
    public sealed class YamahaExtendedNumericRange
    {
        internal YamahaExtendedNumericRange(string nativeId, decimal? minimum,
            decimal? maximum, decimal? step)
        {
            NativeId = nativeId;
            Minimum = minimum;
            Maximum = maximum;
            Step = step;
        }

        public string NativeId { get; private set; }
        public decimal? Minimum { get; private set; }
        public decimal? Maximum { get; private set; }
        public decimal? Step { get; private set; }
    }
}
