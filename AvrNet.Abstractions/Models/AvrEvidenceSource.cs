namespace AvrNet.Abstractions.Models
{
    public enum AvrEvidenceSource
    {
        Unknown = 0,
        DeviceReported = 1,
        ReadProbe = 2,
        VerifiedHardwareWrite = 3,
        KnownProfile = 4,
        BackupInspection = 5,
        Inferred = 6
    }
}
