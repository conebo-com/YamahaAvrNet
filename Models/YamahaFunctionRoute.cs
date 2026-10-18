using System;

namespace YamahaAvrNet.Models
{
    /// <summary>
    /// Describes one protocol-specific function. Implemented operations are library
    /// knowledge; nullable permissions are instance-specific evidence, not promises.
    /// A route is not itself a command and cannot bypass write safeguards.
    /// </summary>
    public sealed class YamahaFunctionRoute
    {
        internal YamahaFunctionRoute(string functionId, string protocol, string nativeIdentifier,
            string reportedBy, YamahaCapabilityStatus status,
            bool implementedRead, bool implementedWrite, bool implementedExecute,
            bool? canRead, bool? canWrite, bool? canExecute, DateTimeOffset? observedUtc)
        {
            FunctionId = functionId;
            Protocol = protocol;
            NativeIdentifier = nativeIdentifier;
            ReportedBy = reportedBy;
            Status = status;
            ImplementedRead = implementedRead;
            ImplementedWrite = implementedWrite;
            ImplementedExecute = implementedExecute;
            CanRead = canRead;
            CanWrite = canWrite;
            CanExecute = canExecute;
            LastObservedUtc = observedUtc;
        }

        public string FunctionId { get; private set; }
        public string Protocol { get; private set; }
        public string NativeIdentifier { get; private set; }
        public string ReportedBy { get; private set; }
        public YamahaCapabilityStatus Status { get; private set; }
        public bool ImplementedRead { get; private set; }
        public bool ImplementedWrite { get; private set; }
        public bool ImplementedExecute { get; private set; }
        public bool? CanRead { get; private set; }
        public bool? CanWrite { get; private set; }
        public bool? CanExecute { get; private set; }
        public DateTimeOffset? LastObservedUtc { get; private set; }

        public bool IsVerifiedFor(YamahaFunctionOperation operation)
        {
            switch (operation)
            {
                case YamahaFunctionOperation.Read: return ImplementedRead && CanRead == true;
                case YamahaFunctionOperation.Write: return ImplementedWrite && CanWrite == true;
                case YamahaFunctionOperation.Execute: return ImplementedExecute && CanExecute == true;
                default: return false;
            }
        }
    }
}
