using System;

namespace YamahaAvrNet.Models
{
    public sealed class YamahaZoneInfo
    {
        public YamahaZoneInfo(string nativeId, string displayName)
        {
            if (string.IsNullOrWhiteSpace(nativeId))
                throw new ArgumentException("A zone needs a native ID.", nameof(nativeId));

            NativeId = nativeId;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? nativeId : displayName;
        }

        public string NativeId { get; private set; }
        public string DisplayName { get; private set; }
    }
}
