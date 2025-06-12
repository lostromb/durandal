using Durandal.Common.Audio;
using Durandal.Common.Speech.Triggers;
using Durandal.Common.Utils;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Audio.Components;
using Durandal.Common.Events;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Test
{
    public class FakeAudioTrigger : NullAudioSampleTarget, IAudioTrigger
    {
        public FakeAudioTrigger(WeakPointer<IAudioGraph> graph, AudioSampleFormat format) : base(graph, format, nameof(FakeAudioTrigger))
        {
            TriggeredEvent = new AsyncEvent<AudioTriggerEventArgs>();
        }

        public void Configure(KeywordSpottingConfiguration config) { }

        public void Reset() { }

        public AsyncEvent<AudioTriggerEventArgs> TriggeredEvent { get; private set; }

        /// <summary>
        /// Manually triggers this object.
        /// </summary>
        /// <param name="triggerResult"></param>
        /// <param name="realTime"></param>
        /// <returns></returns>
        public Task Trigger(AudioTriggerResult triggerResult, IRealTimeProvider realTime)
        {
            return TriggeredEvent.Fire(this, new AudioTriggerEventArgs(triggerResult, realTime), realTime);
        }
    }
}
