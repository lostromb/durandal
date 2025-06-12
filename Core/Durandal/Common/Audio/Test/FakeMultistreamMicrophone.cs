namespace Durandal.Common.Audio.Test
{
    using Durandal.Common.Audio;
    using Durandal.Common.Utils;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using System.Collections.Generic;
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Durandal.Common.Audio.Components;
    using Durandal.Common.Logger;
    using Durandal.Common.Audio.Hardware;
    using Durandal.Common.ServiceMgmt;

    public class FakeMultistreamMicrophone : LinearMixer, IAudioCaptureDevice
    {
        private readonly SilenceAudioSampleSource _driver;
        private readonly List<FixedAudioSampleSource> _sources;
        private readonly ILogger _logger;
        private int _disposed = 0;

        public FakeMultistreamMicrophone(ILogger logger, WeakPointer<IAudioGraph> audioGraph, AudioSampleFormat outputFormat)
            : base(audioGraph, outputFormat, nodeCustomName: null, readForever: true, logger: logger)
        {
            _logger = logger;
            _driver = new SilenceAudioSampleSource(audioGraph, outputFormat, nodeCustomName: null);
            _sources = new List<FixedAudioSampleSource>();
            AddInput(_driver);
        }

        public Task StartCapture(IRealTimeProvider realTime)
        {
            _driver.BeginActivelyWriting(_logger, realTime, true);
            return DurandalTaskExtensions.NoOpTask;
        }

        public Task StopCapture()
        {
            _driver.StopActivelyWriting();
            return DurandalTaskExtensions.NoOpTask;
        }

        public void AddInput(AudioSample sample)
        {
            FixedAudioSampleSource newInput = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(OutputGraph), sample, nodeCustomName: null);
            _sources.Add(newInput);
            AddInput(newInput, null, true);
        }

        protected override void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            try
            {
                if (disposing)
                {
                    foreach (var input in _sources)
                    {
                        input.Dispose();
                    }

                    _driver?.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}
