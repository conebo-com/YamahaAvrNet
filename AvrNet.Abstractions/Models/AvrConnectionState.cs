namespace AvrNet.Abstractions.Models
{
    /// <summary>Transport lifecycle, not an assertion about every protocol.</summary>
    public enum AvrConnectionState
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
