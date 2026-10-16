using System;

namespace YamahaAvrNet.Models
{
    /// <summary>
    /// Unknown permission is null, not false. A successful read must not grant write access.
    /// </summary>
    public sealed class YamahaCapability
    {
        public YamahaCapability(
            string featureId,
            YamahaCapabilityStatus status,
            YamahaEvidenceSource evidence,
            bool? canRead = null,
            bool? canWrite = null,
            bool? canExecute = null,
            bool? canObserve = null,
            string protocol = null,
            string model = null,
            string firmware = null,
            DateTimeOffset? lastVerifiedUtc = null)
        {
            if (string.IsNullOrWhiteSpace(featureId))
                throw new ArgumentException("A feature needs an ID.", nameof(featureId));
            if (lastVerifiedUtc.HasValue && lastVerifiedUtc.Value.Offset != TimeSpan.Zero)
                throw new ArgumentException("Evidence timestamps must be UTC.", nameof(lastVerifiedUtc));

            FeatureId = featureId;
            Status = status;
            Evidence = evidence;
            CanRead = canRead;
            CanWrite = canWrite;
            CanExecute = canExecute;
            CanObserve = canObserve;
            Protocol = protocol;
            Model = model;
            Firmware = firmware;
            LastVerifiedUtc = lastVerifiedUtc;
        }

        public string FeatureId { get; private set; }
        public YamahaCapabilityStatus Status { get; private set; }
        public YamahaEvidenceSource Evidence { get; private set; }
        public bool? CanRead { get; private set; }
        public bool? CanWrite { get; private set; }
        public bool? CanExecute { get; private set; }
        public bool? CanObserve { get; private set; }
        public string Protocol { get; private set; }
        public string Model { get; private set; }
        public string Firmware { get; private set; }
        public DateTimeOffset? LastVerifiedUtc { get; private set; }
    }
}
