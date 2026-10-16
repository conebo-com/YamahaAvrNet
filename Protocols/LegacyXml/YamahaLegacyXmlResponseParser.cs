using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using YamahaAvrNet.Exceptions;
using YamahaAvrNet.Models;

namespace YamahaAvrNet.Protocols.LegacyXml
{
    /// <summary>Checks Yamaha RC independently of HTTP status; parses native speaker IDs.</summary>
    internal static class YamahaLegacyXmlResponseParser
    {
        private static readonly Regex PatternNumber = new Regex(@"^Pattern\s*([0-9]+)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        internal static XElement ParseResponse(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml))
                throw new YamahaLegacyXmlException("Legacy XML response is empty.");

            try
            {
                var settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = 2 * 1024 * 1024,
                    MaxCharactersFromEntities = 0,
                    IgnoreComments = true
                };
                XDocument document;
                using (var input = new StringReader(xml))
                using (var reader = XmlReader.Create(input, settings))
                    document = XDocument.Load(reader, LoadOptions.None);

                XElement root = document.Root;
                if (root == null || root.Name != "YAMAHA_AV")
                    throw new YamahaLegacyXmlException("Unexpected legacy XML response root.");
                string responseType = (string)root.Attribute("rsp");
                if (!string.IsNullOrEmpty(responseType) &&
                    !string.Equals(responseType, "GET", StringComparison.OrdinalIgnoreCase))
                    throw new YamahaLegacyXmlException("Legacy XML response is not a GET reply.");
                var rc = (string)root.Attribute("RC");
                if (string.IsNullOrWhiteSpace(rc))
                    throw new YamahaLegacyXmlException("Legacy XML response has no Yamaha RC attribute.");
                if (rc != "0")
                    throw new YamahaLegacyXmlException(
                        "Yamaha rejected the legacy XML GET request (RC=" + rc + ").");
                return root;
            }
            catch (XmlException ex)
            {
                throw new YamahaLegacyXmlException("Malformed or unsafe Yamaha legacy XML response.", ex);
            }
        }

        internal static int? ParseActivePattern(string xml)
        {
            XElement speaker = SpeakerNode(ParseResponse(xml));
            XElement selection = speaker.Element("Pattern_Sel");
            if (selection == null || string.IsNullOrWhiteSpace(selection.Value))
                throw new YamahaLegacyXmlException("Pattern_Sel is missing from the response.");

            string value = selection.Value.Trim();
            Match match = PatternNumber.Match(value);
            if (match.Success)
                value = match.Groups[1].Value;
            int number;
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number)
                || number < 1)
                throw new YamahaLegacyXmlException("Unrecognized speaker pattern selection: " + value);
            return number;
        }

        internal static IReadOnlyDictionary<string, YamahaSpeakerValue> ParseLevels(
            string xml, int pattern)
        {
            XElement container = PatternNode(ParseResponse(xml), pattern).Element("Lvl");
            if (container == null)
                throw new YamahaLegacyXmlException("Lvl block is missing from the response.");
            return ParseChannelValues(container, "dB");
        }

        internal static IReadOnlyDictionary<string, YamahaSpeakerValue> ParseDistances(
            string xml, int pattern)
        {
            XElement distance = PatternNode(ParseResponse(xml), pattern).Element("Distance");
            if (distance == null)
                throw new YamahaLegacyXmlException("Distance block is missing from the response.");
            // RX-A3060 returned Unit_of_Distance, Meter and Feet. Select the metre
            // aggregate explicitly; no assumptions about the receiver's display unit.
            XElement metre = distance.Element("Meter");
            if (metre == null)
                throw new YamahaLegacyXmlException("Distance/Meter block is not available.");
            return ParseChannelValues(metre, "m");
        }

        private static XElement SpeakerNode(XElement root)
        {
            XElement speaker = root.Element("System") == null ? null :
                root.Element("System").Element("Speaker_Preout");
            if (speaker == null)
                throw new YamahaLegacyXmlException("System/Speaker_Preout is missing.");
            return speaker;
        }

        private static XElement PatternNode(XElement root, int pattern)
        {
            if (pattern < 1 || pattern > 99)
                throw new ArgumentOutOfRangeException(nameof(pattern));
            XElement node = SpeakerNode(root).Element("Pattern_" + pattern);
            if (node == null)
                throw new YamahaLegacyXmlException("Pattern_" + pattern + " is missing from the response.");
            return node;
        }

        private static IReadOnlyDictionary<string, YamahaSpeakerValue> ParseChannelValues(
            XElement container, string expectedUnit)
        {
            var values = new Dictionary<string, YamahaSpeakerValue>(StringComparer.Ordinal);
            var observedUtc = DateTimeOffset.UtcNow;
            foreach (var channel in container.Elements())
            {
                // A native channel is defined by direct Val/Exp/Unit children.
                XElement val = channel.Element("Val");
                XElement exp = channel.Element("Exp");
                XElement unit = channel.Element("Unit");
                if (val == null && exp == null && unit == null)
                    continue;
                string nativeId = channel.Name.LocalName;
                if (val == null || exp == null || unit == null)
                    throw new YamahaLegacyXmlException("Incomplete Val/Exp/Unit for " + nativeId + ".");

                decimal raw;
                int exponent;
                if (!decimal.TryParse(val.Value.Trim(), NumberStyles.AllowLeadingSign |
                    NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out raw) ||
                    !int.TryParse(exp.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out exponent) || exponent < -9 || exponent > 9)
                    throw new YamahaLegacyXmlException("Invalid Val or Exp for " + nativeId + ".");

                string nativeUnit = unit.Value.Trim();
                if (!string.Equals(nativeUnit, expectedUnit, StringComparison.OrdinalIgnoreCase))
                    throw new YamahaLegacyXmlException("Unexpected unit for " + nativeId +
                        ": " + nativeUnit + " (expected " + expectedUnit + ").");

                decimal divisor = 1m;
                for (int i = 0; i < Math.Abs(exponent); i++)
                    divisor *= 10m;
                decimal physical;
                try { physical = exponent >= 0 ? raw / divisor : checked(raw * divisor); }
                catch (OverflowException ex)
                {
                    throw new YamahaLegacyXmlException("Speaker value overflow for " + nativeId + ".", ex);
                }
                if (values.ContainsKey(nativeId))
                    throw new YamahaLegacyXmlException("Duplicate native channel: " + nativeId + ".");
                values.Add(nativeId, new YamahaSpeakerValue(
                    physical, nativeUnit, raw, exponent, observedUtc));
            }
            if (values.Count == 0)
                throw new YamahaLegacyXmlException("No speaker channels were found in the aggregate response.");
            return new ReadOnlyDictionary<string, YamahaSpeakerValue>(values);
        }
    }
}
