using Durandal.Common.Audio;
using Durandal.Common.Audio.Hardware;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Test
{
    public class FakeAudioOutDevice : AbstractAudioSampleTarget, IAudioRenderDevice
    {
        private readonly ILogger _logger;

        public FakeAudioOutDevice(ILogger logger, WeakPointer<IAudioGraph> graph, AudioSampleFormat inputFormat) : base(graph, nameof(FakeAudioOutDevice), nodeCustomName: null)
        {
            _logger = logger ?? NullLogger.Singleton;
            InputFormat = inputFormat;
        }

        public Task StartPlayback(IRealTimeProvider realTime)
        {
            BeginActivelyReading(_logger, realTime, true);
            return DurandalTaskExtensions.NoOpTask;
        }

        public Task StopPlayback()
        {
            StopActivelyReading();
            return DurandalTaskExtensions.NoOpTask;
        }

        protected override ValueTask WriteAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return new ValueTask();
        }
    }
}
