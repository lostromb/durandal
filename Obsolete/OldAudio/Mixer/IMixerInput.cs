using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;

namespace Durandal.Common.Audio.Mixer
{
    public interface IMixerInput : IDisposable
    {
        int Read(short[] target, int count, int offset, IRealTimeProvider realTime);

        bool Finished { get; }

        AsyncEvent<ChannelFinishedEventArgs> PlaybackFinishedEvent { get; }
    }
}

