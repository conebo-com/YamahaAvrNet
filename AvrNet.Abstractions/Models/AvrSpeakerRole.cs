namespace AvrNet.Abstractions.Models
{
    /// <summary>Semantic speaker function. Never infer it from an array index.</summary>
    public enum AvrSpeakerRole
    {
        Unknown = 0,
        FrontLeft,
        FrontRight,
        Center,
        SurroundLeft,
        SurroundRight,
        SurroundBackLeft,
        SurroundBackRight,
        FrontPresenceLeft,
        FrontPresenceRight,
        RearPresenceLeft,
        RearPresenceRight,
        Subwoofer,
        Custom
    }
}
