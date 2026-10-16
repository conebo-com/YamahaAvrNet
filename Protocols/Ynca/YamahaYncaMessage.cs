using System;

namespace YamahaAvrNet.Protocols.Ynca
{
    public enum YamahaYncaMessageKind
    {
        Value,
        Restricted,
        Undefined,
        Unknown
    }

    /// <summary>One received YNCA line. A matching line may also be an unsolicited update.</summary>
    public sealed class YamahaYncaMessage
    {
        internal YamahaYncaMessage(string raw, string subunit, string function,
            string value, YamahaYncaMessageKind kind)
        {
            Raw = raw;
            Subunit = subunit;
            Function = function;
            Value = value;
            Kind = kind;
            ReceivedUtc = DateTimeOffset.UtcNow;
        }

        public string Raw { get; private set; }
        public string Subunit { get; private set; }
        public string Function { get; private set; }
        public string Value { get; private set; }
        public YamahaYncaMessageKind Kind { get; private set; }
        public DateTimeOffset ReceivedUtc { get; private set; }
    }

    public sealed class YamahaYncaMessageEventArgs : EventArgs
    {
        public YamahaYncaMessageEventArgs(YamahaYncaMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            Message = message;
        }

        public YamahaYncaMessage Message { get; private set; }
    }

    public sealed class YamahaYncaConnectionEventArgs : EventArgs
    {
        public YamahaYncaConnectionEventArgs(bool connected, string reason)
        {
            IsConnected = connected;
            Reason = reason;
        }

        public bool IsConnected { get; private set; }
        public string Reason { get; private set; }
    }
}
