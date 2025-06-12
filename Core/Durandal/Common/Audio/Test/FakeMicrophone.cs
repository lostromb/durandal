using System;

namespace Durandal.Common.Audio.Test
{
    using Durandal.Common.Audio;
    using System.Threading.Tasks;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using System.Threading;
    using Durandal.Common.Audio.Components;
    using Durandal.Common.Logger;
    using Durandal.Common.Audio.Hardware;
    using Durandal.Common.ServiceMgmt;

    /// <summary>
    /// Test microphone class that uses a static audio sample as its source
    /// </summary>
    public class FakeMicrophone : FixedAudioSampleSource, IAudioCaptureDevice
    {
        private readonly ILogger _logger;

        public FakeMicrophone(ILogger logger, WeakPointer<IAudioGraph> graph, AudioSample input) : base(graph, input, nameof(FakeMicrophone))
        {
            _logger = logger;
        }

        public Task StartCapture(IRealTimeProvider realTime)
        {
            BeginActivelyWriting(_logger, realTime, true);
            return DurandalTaskExtensions.NoOpTask;
        }

        public Task StopCapture()
        {
            StopActivelyWriting();
            return DurandalTaskExtensions.NoOpTask;
        }
    }
}
