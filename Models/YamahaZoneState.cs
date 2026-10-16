using System;

namespace YamahaAvrNet.Models
{
    /// <summary>Immutable snapshot of the latest observed YNCA zone values.</summary>
    public sealed class YamahaZoneState
    {
        internal YamahaZoneState(string nativeId, bool? power, decimal? volumeDb,
            bool? mute, string input, DateTimeOffset? observedUtc, bool isStale)
        {
            NativeId = nativeId;
            IsPoweredOn = power;
            VolumeDb = volumeDb;
            IsMuted = mute;
            InputNativeId = input;
            ObservedUtc = observedUtc;
            IsStale = isStale;
        }

        public string NativeId { get; private set; }
        public bool? IsPoweredOn { get; private set; }
        public decimal? VolumeDb { get; private set; }
        public bool? IsMuted { get; private set; }
        public string InputNativeId { get; private set; }
        public DateTimeOffset? ObservedUtc { get; private set; }
        /// <summary>True when the TCP session was disconnected after the last observation.</summary>
        public bool IsStale { get; private set; }
    }

    public sealed class YamahaZoneStateChangedEventArgs : EventArgs
    {
        public YamahaZoneStateChangedEventArgs(YamahaZoneState state)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
        }

        public YamahaZoneState State { get; private set; }
    }
}
