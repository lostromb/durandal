using Durandal.Common.Audio;
using Durandal.Common.Events;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech
{
    public interface IUtteranceRecorder : IAudioSampleTarget
    {
        /// <summary>
        /// Resets the state of this utterance recorder and prepares it to process a new utterance.
        /// </summary>
        void Reset();

        AsyncEvent<RecorderStateEventArgs> UtteranceFinishedEvent { get; }
    }
}
