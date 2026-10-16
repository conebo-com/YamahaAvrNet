namespace YamahaAvrNet.Models
{
    /// <summary>Physical placement; independent of role and native channel ID.</summary>
    public enum YamahaSpeakerPlacement
    {
        Unknown = 0,
        Floor,
        FrontHeight,
        RearHeight,
        Overhead,
        TopFront,
        TopMiddle,
        TopRear,
        FrontWide,
        Custom
    }
}
