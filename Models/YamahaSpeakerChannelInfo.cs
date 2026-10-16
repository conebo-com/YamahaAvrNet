using System;

namespace YamahaAvrNet.Models
{
    /// <summary>
    /// Native ID, semantic role and physical placement are different things.
    /// Addressability does not establish that a speaker is configured or active.
    /// </summary>
    public sealed class YamahaSpeakerChannelInfo
    {
        public YamahaSpeakerChannelInfo(
            string nativeId,
            YamahaSpeakerRole? role = null,
            YamahaSpeakerPlacement? placement = null,
            bool? isAddressable = null,
            bool? isConfigured = null,
            bool? isCurrentlyActive = null,
            bool? canReadLevel = null,
            bool? canWriteLevel = null,
            bool? canReadDistance = null,
            bool? canWriteDistance = null)
        {
            if (string.IsNullOrWhiteSpace(nativeId))
                throw new ArgumentException("A channel needs a native ID.", nameof(nativeId));

            NativeId = nativeId;
            Role = role;
            Placement = placement;
            IsAddressable = isAddressable;
            IsConfigured = isConfigured;
            IsCurrentlyActive = isCurrentlyActive;
            CanReadLevel = canReadLevel;
            CanWriteLevel = canWriteLevel;
            CanReadDistance = canReadDistance;
            CanWriteDistance = canWriteDistance;
        }

        public string NativeId { get; private set; }
        public YamahaSpeakerRole? Role { get; private set; }
        public YamahaSpeakerPlacement? Placement { get; private set; }
        public bool? IsAddressable { get; private set; }
        public bool? IsConfigured { get; private set; }
        public bool? IsCurrentlyActive { get; private set; }
        public bool? CanReadLevel { get; private set; }
        public bool? CanWriteLevel { get; private set; }
        public bool? CanReadDistance { get; private set; }
        public bool? CanWriteDistance { get; private set; }
    }
}
