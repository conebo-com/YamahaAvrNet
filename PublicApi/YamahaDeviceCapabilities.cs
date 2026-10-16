using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AvrNet.Abstractions.Models;

namespace YamahaAvrNet.PublicApi
{
    /// <summary>
    /// Immutable capability snapshot. Missing IDs return Unknown; they are not unsupported.
    /// </summary>
    public sealed class YamahaDeviceCapabilities
    {
        private readonly IReadOnlyList<AvrCapability> _all;
        private readonly Dictionary<string, AvrCapability> _byId;

        public YamahaDeviceCapabilities(IEnumerable<AvrCapability> capabilities)
        {
            if (capabilities == null)
                throw new ArgumentNullException(nameof(capabilities));

            var values = new List<AvrCapability>();
            _byId = new Dictionary<string, AvrCapability>(StringComparer.OrdinalIgnoreCase);

            foreach (var capability in capabilities)
            {
                if (capability == null)
                    throw new ArgumentException("Capability collection contains null.", nameof(capabilities));
                if (_byId.ContainsKey(capability.FeatureId))
                    throw new ArgumentException("Duplicate capability ID: " + capability.FeatureId, nameof(capabilities));

                _byId.Add(capability.FeatureId, capability);
                values.Add(capability);
            }

            _all = new ReadOnlyCollection<AvrCapability>(values);
        }

        public IReadOnlyList<AvrCapability> All { get { return _all; } }

        public AvrCapability Get(string featureId)
        {
            if (string.IsNullOrWhiteSpace(featureId))
                throw new ArgumentException("Feature ID must not be empty.", nameof(featureId));

            AvrCapability capability;
            if (_byId.TryGetValue(featureId, out capability))
                return capability;

            return new AvrCapability(featureId, AvrCapabilityStatus.Unknown,
                AvrEvidenceSource.Unknown);
        }
    }
}
