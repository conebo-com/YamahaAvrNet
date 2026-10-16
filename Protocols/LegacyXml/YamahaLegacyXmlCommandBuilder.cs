using System;
using System.Xml.Linq;

namespace YamahaAvrNet.Protocols.LegacyXml
{
    /// <summary>Builds documented aggregate GETs and narrowly confirmed single-channel PUTs.</summary>
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

        internal static XDocument VerifiedFrontLeftLevel(int pattern, decimal rawValue, int exponent)
        {
            return VerifiedFrontLeftWrite(pattern, "Lvl", null, rawValue, exponent, "dB");
        }

        internal static XDocument VerifiedFrontLeftDistance(int pattern, decimal rawValue, int exponent)
        {
            return VerifiedFrontLeftWrite(pattern, "Distance", "Meter", rawValue, exponent, "m");
        }

        private static XDocument VerifiedFrontLeftWrite(int pattern, string kind, string subKind,
            decimal rawValue, int exponent, string unit)
        {
            if (pattern != 1)
                throw new ArgumentOutOfRangeException(nameof(pattern),
                    "Only the previously verified Pattern 1 write path is available.");
            XElement channel = new XElement("Front_L",
                new XElement("Val", rawValue.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new XElement("Exp", exponent), new XElement("Unit", unit));
            XElement child = subKind == null ? channel : new XElement(subKind, channel);
            return new XDocument(new XElement("YAMAHA_AV", new XAttribute("cmd", "PUT"),
                new XElement("System", new XElement("Speaker_Preout",
                    new XElement("Pattern_1", new XElement(kind, child))))));
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
