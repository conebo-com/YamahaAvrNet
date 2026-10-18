namespace YamahaAvrNet.Models
{
    /// <summary>Evidence strength for one device feature, not a universal model claim.</summary>
    public enum YamahaCapabilityStatus
    {
        Unknown = 0,
        Advertised = 1,
        ReadVerified = 2,
        WriteVerified = 3,
        KnownUnsupported = 4,
        TemporarilyUnavailable = 5,
        ActionAccepted = 6
    }
}
