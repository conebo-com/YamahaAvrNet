using System.Collections.Generic;
using AvrNet.Abstractions.Models;

namespace AvrNet.Abstractions.Interfaces
{
    public interface IAvrCapabilityProvider
    {
        /// <summary>Current known capabilities. An empty set is not proof of non-support.</summary>
        IReadOnlyList<AvrCapability> Capabilities { get; }
        AvrCapability GetCapability(string featureId);
    }
}
