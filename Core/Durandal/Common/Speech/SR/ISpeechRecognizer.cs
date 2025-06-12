using Durandal.API;
using Durandal.Common.Audio;
using Durandal.Common.Client;
using Durandal.Common.Events;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.SR
{
    public interface ISpeechRecognizer : IAudioSampleTarget
    {
        /// <summary>
        /// Event that is fired when an intermediate speech result is available.
        /// </summary>
        AsyncEvent<TextEventArgs> IntermediateResultEvent { get; }

        /// <summary>
        /// Finalizes an asynchronous speech reco request and returns the final hypotheses.
        /// This method will block until recognition fully completes.
        /// </summary>
        /// <returns>The set of all final speech hypotheses</returns>
        Task<SpeechRecognitionResult> FinishUnderstandSpeech(CancellationToken cancelToken, IRealTimeProvider realTime);
    }
}
