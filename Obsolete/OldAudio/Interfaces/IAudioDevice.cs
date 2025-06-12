using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Interfaces
{
    public interface IAudioDevice : IDisposable
    {
        /// <summary>
        /// Temporary release the hardware handle for this device, on the assumption that the app
        /// is going into the background or is suspending (as for a UWP lifecycle event)
        /// </summary>
        Task Suspend();

        /// <summary>
        /// Resumes the audio device after a call to Suspend()
        /// </summary>
        Task Resume();
    }
}
