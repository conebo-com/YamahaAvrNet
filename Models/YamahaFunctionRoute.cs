namespace YamahaAvrNet.Models
{
    /// <summary>
    /// Read-only discovery metadata for a native function. A route is not a
    /// callable command and does not confer write/execute permission.
    /// </summary>
    public sealed class YamahaFunctionRoute
    {
        internal YamahaFunctionRoute(string functionId, string protocol,
            string nativeIdentifier, string reportedBy, YamahaCapabilityStatus status)
        {
            FunctionId = functionId;
            Protocol = protocol;
            NativeIdentifier = nativeIdentifier;
            ReportedBy = reportedBy;
            Status = status;
        }

        public string FunctionId { get; private set; }
        public string Protocol { get; private set; }
        public string NativeIdentifier { get; private set; }
        public string ReportedBy { get; private set; }
        public YamahaCapabilityStatus Status { get; private set; }
    }
}
