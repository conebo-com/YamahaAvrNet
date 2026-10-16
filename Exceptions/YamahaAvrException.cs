using System;

namespace YamahaAvrNet.Exceptions
{
    public class YamahaAvrException : Exception
    {
        public YamahaAvrException(string message) : base(message) { }
        public YamahaAvrException(string message, Exception innerException)
            : base(message, innerException) { }
    }

    public class YamahaConnectionException : YamahaAvrException
    {
        public YamahaConnectionException(string message) : base(message) { }
        public YamahaConnectionException(string message, Exception innerException)
            : base(message, innerException) { }
    }

    public class YamahaTimeoutException : YamahaConnectionException
    {
        public YamahaTimeoutException(string message) : base(message) { }
        public YamahaTimeoutException(string message, Exception innerException)
            : base(message, innerException) { }
    }

    public class YamahaProtocolException : YamahaAvrException
    {
        public YamahaProtocolException(string message) : base(message) { }
        public YamahaProtocolException(string message, Exception innerException)
            : base(message, innerException) { }
    }

    public sealed class YamahaYncaException : YamahaProtocolException
    {
        public YamahaYncaException(string message) : base(message) { }
        public YamahaYncaException(string message, Exception innerException)
            : base(message, innerException) { }
    }

    public sealed class YamahaLegacyXmlException : YamahaProtocolException
    {
        public YamahaLegacyXmlException(string message) : base(message) { }
        public YamahaLegacyXmlException(string message, Exception innerException)
            : base(message, innerException) { }
    }

    public sealed class YamahaExtendedControlException : YamahaProtocolException
    {
        public YamahaExtendedControlException(string message) : base(message) { }
        public YamahaExtendedControlException(string message, Exception innerException)
            : base(message, innerException) { }
    }

    public sealed class YamahaFeatureNotSupportedException : YamahaAvrException
    {
        public YamahaFeatureNotSupportedException(string message) : base(message) { }
    }

    public sealed class YamahaFeatureTemporarilyUnavailableException : YamahaAvrException
    {
        public YamahaFeatureTemporarilyUnavailableException(string message) : base(message) { }
    }

    public sealed class YamahaValidationException : YamahaAvrException
    {
        public YamahaValidationException(string message) : base(message) { }
    }

    public sealed class YamahaStateVerificationException : YamahaAvrException
    {
        public YamahaStateVerificationException(string message) : base(message) { }
    }
}
