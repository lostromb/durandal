using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Hardware
{
    /// <summary>
    /// Represents a hardware device that reads audio from a graph to output it to speakers.
    /// Since it is a hardware device handle (ostensibly) we need to be able to control its behavior
    /// </summary>
    public interface IAudioRenderDevice : IAudioSampleTarget
    {
        Task StartPlayback(IRealTimeProvider realTime);

        Task StopPlayback();
    }
}
