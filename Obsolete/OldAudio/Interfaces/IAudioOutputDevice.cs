using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Interfaces
{
    /// <summary>
    /// Defines an actual audio hardware output device.
    /// This interface doesn't implement a Write() method, because by the pull-pipeline
    /// nature of the audio graph, this device requires an <see cref="IAudioSampleProvider" /> (usually a mixer)
    /// to pull its audio from.
    /// </summary>
    public interface IAudioOutputDevice : IAudioDevice
    {
        IAudioSampleProvider SampleProvider { get; set; }
    }
}
