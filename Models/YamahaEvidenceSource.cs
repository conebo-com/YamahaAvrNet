namespace YamahaAvrNet.Models
{
    public enum YamahaEvidenceSource
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
