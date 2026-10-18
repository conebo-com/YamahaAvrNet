using System;
using YamahaAvrNet.PublicApi;

namespace YamahaAvrNet.Models
{
    /// <summary>Immutable receiver snapshot published after a meaningful state observation.</summary>
    public sealed class YamahaReceiverStateChangedEventArgs : EventArgs
    {
        public YamahaReceiverStateChangedEventArgs(YamahaReceiverState state)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
        }

        public YamahaReceiverState State { get; private set; }
    }
}
