namespace AvrNet.Abstractions.Interfaces
{
    /// <summary>
    /// Native zone IDs are retained because not all receivers offer the same zones.
    /// Functional zone controls will be added when the Denon contract is reconciled.
    /// </summary>
    public interface IAvrZone
    {
        string NativeId { get; }
        string DisplayName { get; }
    }
}
