using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.File;
using Durandal.Common.MathExt;
using Durandal.Common.IO;
using Durandal.Common.Audio;
using Durandal.Common.Time;
using System.Threading;
using Durandal.Common.Tasks;
using Durandal.Common.Logger;
using Durandal.Common.Events;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Speech
{
    public class StaticUtteranceRecorder : AbstractAudioSampleTarget, IUtteranceRecorder
    {
        private const int CHECK_INCREMENT_SAMPLES = 100;
        private const float MIN_VOLUME_FOR_UTTERANCE = 0.02f;
        private readonly int _amountToRecordSamplesPerChannel;
        private readonly MovingAverageRmsVolume _avgVolume;
        private readonly ILogger _logger;

        private int _amountRecordedSamplesPerChannel;
        private bool _hasUtteranceStarted = false;
        private bool _utteranceDetected = false;
        private int _currentIncrement = 0;

        public StaticUtteranceRecorder(WeakPointer<IAudioGraph> graph, AudioSampleFormat inputFormat, string nodeCustomName, TimeSpan amountToRecord, ILogger logger)
            : base(graph, nameof(StaticUtteranceRecorder), nodeCustomName)
        {
            inputFormat = inputFormat.AssertNonNull(nameof(inputFormat));
            if (amountToRecord <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException("Cannot record a negative amount of audio");
            }

            _logger = logger ?? NullLogger.Singleton;
            _amountToRecordSamplesPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(inputFormat.SampleRateHz, amountToRecord);
            InputFormat = inputFormat;
            _avgVolume = new MovingAverageRmsVolume(CHECK_INCREMENT_SAMPLES * inputFormat.NumChannels, 0.0f);
            UtteranceFinishedEvent = new AsyncEvent<RecorderStateEventArgs>();
            Reset();
        }

        public AsyncEvent<RecorderStateEventArgs> UtteranceFinishedEvent
        {
            get;
            private set;
        }

        public void Reset()
        {
            _hasUtteranceStarted = false;
            _utteranceDetected = false;
            _avgVolume.RmsVolume = 0.0f;
            _amountRecordedSamplesPerChannel = 0;
            _currentIncrement = 0;
        }

        protected override ValueTask WriteAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_utteranceDetected)
            {
                // Don't fire more than once until the recorder is reset
                return new ValueTask();
            }

            int start = bufferOffset;
            int end = bufferOffset + (numSamplesPerChannel * InputFormat.NumChannels);
            for (int c = start; c < end; c++)
            {
                _avgVolume.Add(buffer[c]);
                if (++_currentIncrement >= CHECK_INCREMENT_SAMPLES)
                {
                    if (_avgVolume.RmsVolume > MIN_VOLUME_FOR_UTTERANCE)
                    {
                        _hasUtteranceStarted = true;
                    }

                    _currentIncrement = 0;
                }
            }

            _amountRecordedSamplesPerChannel += numSamplesPerChannel;
            if (_amountRecordedSamplesPerChannel >= _amountToRecordSamplesPerChannel)
            {
                _utteranceDetected = true;
                if (_hasUtteranceStarted)
                {
                    UtteranceFinishedEvent.FireInBackground(this, new RecorderStateEventArgs(RecorderState.Finished), _logger, realTime);
                }
                else
                {
                    UtteranceFinishedEvent.FireInBackground(this, new RecorderStateEventArgs(RecorderState.FinishedNothingRecorded), _logger, realTime);
                }
            }

            return new ValueTask();
        }
    }
}
