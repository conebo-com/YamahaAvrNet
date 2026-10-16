namespace YamahaAvrNet.Models
{
    /// <summary>Transport lifecycle, not an assertion about every protocol.</summary>
    public enum YamahaConnectionState
    {
        Disconnected = 0,
        Connecting = 1,
        Connected = 2,
        PartiallyConnected = 3,
        Reconnecting = 4,
        Faulted = 5,
        Disposed = 6
    }
}
