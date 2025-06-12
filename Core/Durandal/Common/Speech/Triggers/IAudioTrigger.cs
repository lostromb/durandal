using Durandal.Common.Audio;
using Durandal.Common.Events;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers
{
    public interface IAudioTrigger : IAudioSampleTarget
    {
        void Configure(KeywordSpottingConfiguration config);

        void Reset();

        AsyncEvent<AudioTriggerEventArgs> TriggeredEvent { get; }
    }
}
