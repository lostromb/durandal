using Durandal.API;
using Durandal.Common.Audio;
using Durandal.Common.Audio.Components;
using Durandal.Common.Client;
using Durandal.Common.Events;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.SR
{
    public class NullSpeechReco : NullAudioSampleTarget, ISpeechRecognizer
    {
        public NullSpeechReco(WeakPointer<IAudioGraph> graph, AudioSampleFormat inputFormat) : base(graph, inputFormat, nameof(NullSpeechReco))
        {
            IntermediateResultEvent = new AsyncEvent<TextEventArgs>();
        }

        public AsyncEvent<TextEventArgs> IntermediateResultEvent
        {
            get;
            private set;
        }

        public Task<SpeechRecognitionResult> FinishUnderstandSpeech(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return Task.FromResult(new SpeechRecognitionResult());
        }
    }
}
