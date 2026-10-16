using System;
using System.Text;

namespace YamahaAvrNet.Protocols.Ynca
{
    /// <summary>Pure YNCA wire-format operations. No Python runtime or generated definitions.</summary>
    internal static class YamahaYncaCodec
    {
        internal const int MaxLineBytes = 4096;

        internal static byte[] Get(string subunit, string function)
        {
            ValidateToken(subunit, "subunit");
            ValidateToken(function, "function");
            return Encoding.ASCII.GetBytes("@" + subunit.ToUpperInvariant() + ":" +
                function.ToUpperInvariant() + "=?\r\n");
        }

        internal static byte[] Put(string subunit, string function, string value)
        {
            ValidateToken(subunit, nameof(subunit));
            ValidateToken(function, nameof(function));
            if (string.IsNullOrEmpty(value) || value.Length > 64)
                throw new ArgumentException("YNCA parameter must be 1-64 characters.", nameof(value));
            foreach (char c in value)
                if (c < 32 || c > 126 || c == '=' || c == '?')
                    throw new ArgumentException("Unsafe YNCA parameter character.", nameof(value));
            return Encoding.ASCII.GetBytes("@" + subunit.ToUpperInvariant() + ":" +
                function.ToUpperInvariant() + "=" + value + "\r\n");
        }

        internal static void ValidateToken(string token, string paramName)
        {
            if (string.IsNullOrEmpty(token) || token.Length > 48)
                throw new ArgumentException("YNCA identifier must be 1-48 characters.", paramName);
            foreach (char c in token)
                if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                      (c >= '0' && c <= '9') || c == '_'))
                    throw new ArgumentException("Invalid YNCA identifier.", paramName);
        }

        internal static YamahaYncaMessage Parse(string line)
        {
            if (line == null) throw new ArgumentNullException(nameof(line));
            if (line == "@RESTRICTED")
                return new YamahaYncaMessage(line, null, null, null, YamahaYncaMessageKind.Restricted);
            if (line == "@UNDEFINED")
                return new YamahaYncaMessage(line, null, null, null, YamahaYncaMessageKind.Undefined);
            if (!line.StartsWith("@", StringComparison.Ordinal))
                return new YamahaYncaMessage(line, null, null, null, YamahaYncaMessageKind.Unknown);
            int colon = line.IndexOf(':', 1);
            int equal = colon < 0 ? -1 : line.IndexOf('=', colon + 1);
            if (colon < 2 || equal <= colon + 1)
                return new YamahaYncaMessage(line, null, null, null, YamahaYncaMessageKind.Unknown);
            string subunit = line.Substring(1, colon - 1);
            string function = line.Substring(colon + 1, equal - colon - 1);
            // Unknown receiver-defined identifiers should never break message reception.
            return new YamahaYncaMessage(line, subunit, function,
                line.Substring(equal + 1), YamahaYncaMessageKind.Value);
        }
    }
}
