using System;

namespace YamahaAvrNet.Models
{
    /// <summary>
    /// One monitoring iteration. HasSuccessfulReads means one or more read paths worked;
    /// Error may still describe other unavailable protocols or zones.
    /// </summary>
    public sealed class YamahaMonitorPollEventArgs : EventArgs
    {
        public YamahaMonitorPollEventArgs(DateTimeOffset observedUtc, bool hasSuccessfulReads,
            int successfulReads, string error)
        {
            ObservedUtc = observedUtc;
            HasSuccessfulReads = hasSuccessfulReads;
            SuccessfulReads = successfulReads;
            Error = error;
        }

        public DateTimeOffset ObservedUtc { get; private set; }
        public bool HasSuccessfulReads { get; private set; }
        public int SuccessfulReads { get; private set; }
        public string Error { get; private set; }
    }
}
