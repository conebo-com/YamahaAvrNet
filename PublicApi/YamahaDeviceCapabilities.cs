using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using YamahaAvrNet.Models;

namespace YamahaAvrNet.PublicApi
{
    /// <summary>
    /// Immutable capability snapshot. Missing IDs return Unknown; they are not unsupported.
    /// </summary>
    public sealed class YamahaDeviceCapabilities
    {
        private readonly IReadOnlyList<YamahaCapability> _all;
        private readonly Dictionary<string, YamahaCapability> _byId;

        public YamahaDeviceCapabilities(IEnumerable<YamahaCapability> capabilities)
        {
            if (capabilities == null)
                throw new ArgumentNullException(nameof(capabilities));

            var values = new List<YamahaCapability>();
            _byId = new Dictionary<string, YamahaCapability>(StringComparer.OrdinalIgnoreCase);

            foreach (var capability in capabilities)
            {
                if (capability == null)
                    throw new ArgumentException("Capability collection contains null.", nameof(capabilities));
                if (_byId.ContainsKey(capability.FeatureId))
                    throw new ArgumentException("Duplicate capability ID: " + capability.FeatureId, nameof(capabilities));

                _byId.Add(capability.FeatureId, capability);
                values.Add(capability);
            }

            _all = new ReadOnlyCollection<YamahaCapability>(values);
        }

        public IReadOnlyList<YamahaCapability> All { get { return _all; } }

        public YamahaCapability Get(string featureId)
        {
            if (string.IsNullOrWhiteSpace(featureId))
                throw new ArgumentException("Feature ID must not be empty.", nameof(featureId));

            YamahaCapability capability;
            if (_byId.TryGetValue(featureId, out capability))
                return capability;

            return new YamahaCapability(featureId, YamahaCapabilityStatus.Unknown,
                YamahaEvidenceSource.Unknown);
        }
    }
}
