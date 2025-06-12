using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Hardware
{
    /// <summary>
    /// Represents a hardware device that reads audio from a device (microphone) and pushes it to an audio graph.
    /// Since it is a hardware device handle (ostensibly) we need to be able to control its behavior
    /// </summary>
    public interface IAudioCaptureDevice : IAudioSampleSource
    {
        Task StartCapture(IRealTimeProvider realTime);

        Task StopCapture();
    }
}
