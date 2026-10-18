using System;
using System.Collections.Generic;

namespace YamahaAvrNet.Models
{
    /// <summary>
    /// Optional, incomplete receiver-provided legacy XML command description.
    /// These entries do not prove that a command accepts GET or PUT.
    /// </summary>
    public sealed class YamahaLegacyDescriptor
    {
        internal YamahaLegacyDescriptor(string resourcePath, string rootElement,
            IReadOnlyList<string> commandIdentifiers, int commandElementCount,
            DateTimeOffset observedUtc)
        {
            ResourcePath = resourcePath;
            RootElement = rootElement;
            CommandIdentifiers = commandIdentifiers;
            CommandElementCount = commandElementCount;
            ObservedUtc = observedUtc;
        }

        public string ResourcePath { get; private set; }
        public string RootElement { get; private set; }
        public IReadOnlyList<string> CommandIdentifiers { get; private set; }
        public int CommandElementCount { get; private set; }
        public DateTimeOffset ObservedUtc { get; private set; }
    }
}
