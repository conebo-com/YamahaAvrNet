using System;

namespace YamahaAvrNet.Models
{
    /// <summary>
    /// Physical value (for instance dB or m) together with the native Val/Exp/Unit.
    /// Phase 3 will use the protocol-reported exponent and unit for every value.
    /// </summary>
    public sealed class YamahaSpeakerValue
    {
        public YamahaSpeakerValue(decimal value, string unit, decimal rawValue,
            int exponent, DateTimeOffset? observedUtc = null)
        {
            if (string.IsNullOrWhiteSpace(unit))
                throw new ArgumentException("Unit must be specified.", nameof(unit));
            if (observedUtc.HasValue && observedUtc.Value.Offset != TimeSpan.Zero)
                throw new ArgumentException("Observation timestamp must be UTC.", nameof(observedUtc));

            Value = value;
            Unit = unit;
            RawValue = rawValue;
            Exponent = exponent;
            ObservedUtc = observedUtc;
        }

        public decimal Value { get; private set; }
        public string Unit { get; private set; }
        public decimal RawValue { get; private set; }
        public int Exponent { get; private set; }
        public DateTimeOffset? ObservedUtc { get; private set; }
    }
}
