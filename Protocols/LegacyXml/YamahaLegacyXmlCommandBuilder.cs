using System;
using System.Xml.Linq;

namespace YamahaAvrNet.Protocols.LegacyXml
{
    /// <summary>Builds only documented, read-only aggregate GET requests.</summary>
    internal static class YamahaLegacyXmlCommandBuilder
    {
        internal static XDocument ActivePattern()
        {
            return Envelope(new XElement("Pattern_Sel", "GetParam"), false, 0);
        }

        internal static XDocument Levels(int pattern)
        {
            return Envelope(new XElement("Lvl", "GetParam"), true, pattern);
        }

        internal static XDocument Distances(int pattern)
        {
            return Envelope(new XElement("Distance", "GetParam"), true, pattern);
        }

        private static XDocument Envelope(XElement target, bool patternSpecific, int pattern)
        {
            if (patternSpecific && (pattern < 1 || pattern > 99))
                throw new ArgumentOutOfRangeException(nameof(pattern),
                    "A speaker pattern must be between 1 and 99.");

            var speaker = patternSpecific
                ? new XElement("Speaker_Preout", new XElement("Pattern_" + pattern, target))
                : new XElement("Speaker_Preout", target);
            return new XDocument(new XElement("YAMAHA_AV",
                new XAttribute("cmd", "GET"), new XElement("System", speaker)));
        }
    }
}
