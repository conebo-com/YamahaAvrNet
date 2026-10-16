using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace YamahaAvrNet.Models
{
    public sealed class YamahaSpeakerPatternInfo
    {
        public YamahaSpeakerPatternInfo(int number, bool? isActive,
            IEnumerable<YamahaSpeakerChannelInfo> channels)
        {
            if (number < 1)
                throw new ArgumentOutOfRangeException(nameof(number));
            if (channels == null)
                throw new ArgumentNullException(nameof(channels));

            var list = new List<YamahaSpeakerChannelInfo>();
            foreach (var channel in channels)
            {
                if (channel == null)
                    throw new ArgumentException("Channel list contains null.", nameof(channels));
                list.Add(channel);
            }

            Number = number;
            IsActive = isActive;
            Channels = new ReadOnlyCollection<YamahaSpeakerChannelInfo>(list);
        }

        public int Number { get; private set; }
        public bool? IsActive { get; private set; }
        public IReadOnlyList<YamahaSpeakerChannelInfo> Channels { get; private set; }
    }
}
