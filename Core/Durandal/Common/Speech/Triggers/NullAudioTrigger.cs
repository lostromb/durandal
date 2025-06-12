using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Audio;
using Durandal.Common.Audio.Components;
using Durandal.Common.Events;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;

namespace Durandal.Common.Speech.Triggers
{
    public class NullAudioTrigger : NullAudioSampleTarget, IAudioTrigger
    {
        public NullAudioTrigger(WeakPointer<IAudioGraph> graph, AudioSampleFormat format) : base(graph, format, nameof(NullAudioTrigger))
        {
            TriggeredEvent = new AsyncEvent<AudioTriggerEventArgs>();
        }

        public void Configure(KeywordSpottingConfiguration config) { }

        public void Reset() { }

        public AsyncEvent<AudioTriggerEventArgs> TriggeredEvent { get; private set; }
    }
}
