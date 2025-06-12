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
using System.Net;
using Durandal.Common.Logger;
using Durandal.Common.Events;
using Durandal.Common.Collections;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Speech
{
    /// <summary>
    /// Old implementation of the "dynamic" utterance recorder, which attempts to detect the time
    /// that a speaker finishes their utterance without hesitation. It behaves oddly in some setups
    /// and relies on some bad math so it should really get replaced sometime soon.
    /// </summary>
    public sealed class DynamicUtteranceRecorder : AbstractAudioSampleTarget, IUtteranceRecorder
    {
        private static readonly TimeSpan INCREMENT_SIZE = TimeSpan.FromMilliseconds(50);
        private static readonly TimeSpan MAX_INITIAL_WAIT_TIME = TimeSpan.FromSeconds(4);
        private static readonly TimeSpan MAX_RECORD_LENGTH = TimeSpan.FromSeconds(8);

        // Optimum parameters here were calculated by a genetic algorithm
        private const double INITIAL_VOLUME = 1088;
        private const int MAX_MS_OF_SILENCE = 992;
        private const int SLOWER_AVERAGE_WINDOW = 22;
        private const int SLOW_AVERAGE_WINDOW = 10;
        private const int FAST_AVERAGE_WINDOW = 4;
        private const double UTTERANCE_START_VELOCITY = 0.279839516992572;
        private const double UTTERANCE_END_THRESHOLD = 0.316104528934743;
        private const double SILENCE_THRESHOLD = 0.0926201062323151;
        private const double MIN_VOLUME = 230.067475249317;

        private readonly MovingAverage fastAverage = new MovingAverage(FAST_AVERAGE_WINDOW, INITIAL_VOLUME);
        private readonly MovingAverage slowAverage = new MovingAverage(SLOW_AVERAGE_WINDOW, INITIAL_VOLUME);
        private readonly MovingAverage slowerAverage = new MovingAverage(SLOWER_AVERAGE_WINDOW, INITIAL_VOLUME);
        private readonly StaticAverage overallVolume = new StaticAverage();
        private readonly ILogger _traceLogger;

        private bool _started = false;
        private bool _ended = false;
        private TimeSpan _pos = TimeSpan.Zero;
        //private int startTime = 0;
        //private int endTime = 0;
        private int _msOfSilence = 0;

        private readonly float[] _inBuf;
        private readonly int _inBufLengthSamplerPerChannel;
        private int _samplesPerChannelInBuf;

        public AsyncEvent<RecorderStateEventArgs> UtteranceFinishedEvent { get; private set; }

        public DynamicUtteranceRecorder(WeakPointer<IAudioGraph> graph, AudioSampleFormat inputFormat, string nodeCustomName, ILogger traceLogger)
            : base(graph, nameof(DynamicUtteranceRecorder), nodeCustomName)
        {
            InputFormat = inputFormat;
            _traceLogger = traceLogger ?? NullLogger.Singleton;
            UtteranceFinishedEvent = new AsyncEvent<RecorderStateEventArgs>();
            _inBufLengthSamplerPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(inputFormat.SampleRateHz, INCREMENT_SIZE);
            _inBuf = new float[_inBufLengthSamplerPerChannel * inputFormat.NumChannels];
            Reset();
        }

        public void Reset()
        {
            fastAverage.Average = INITIAL_VOLUME;
            slowAverage.Average = INITIAL_VOLUME;
            slowerAverage.Average = INITIAL_VOLUME;
            overallVolume.Reset();
            _started = false;
            _ended = false;
            _pos = TimeSpan.Zero;
            _msOfSilence = 0;
            _samplesPerChannelInBuf = 0;
        }

        protected override ValueTask WriteAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_ended)
            {
                return new ValueTask();
            }

            int samplesPerChannelReadFromInput = 0;
            while (samplesPerChannelReadFromInput < numSamplesPerChannel)
            {
                int samplesPerChannelCanReadFromInput = Math.Min(_inBufLengthSamplerPerChannel - _samplesPerChannelInBuf, numSamplesPerChannel - samplesPerChannelReadFromInput);
                if (samplesPerChannelCanReadFromInput > 0)
                {
                    ArrayExtensions.MemCopy(
                        buffer,
                        (bufferOffset + (samplesPerChannelReadFromInput * InputFormat.NumChannels)),
                        _inBuf,
                        _samplesPerChannelInBuf * InputFormat.NumChannels,
                        samplesPerChannelCanReadFromInput * InputFormat.NumChannels);
                    _samplesPerChannelInBuf += samplesPerChannelCanReadFromInput;
                    samplesPerChannelReadFromInput += samplesPerChannelCanReadFromInput;
                }

                if (_samplesPerChannelInBuf == _inBufLengthSamplerPerChannel)
                {
                    ProcessFullBuffer(realTime);
                    _samplesPerChannelInBuf = 0;
                }
            }

            return new ValueTask();
        }

        private void ProcessFullBuffer(IRealTimeProvider realTime)
        {
            double curVolume = CalculateBufferVolume();
            overallVolume.Add(curVolume);
            double volumeFactor = Math.Max(MIN_VOLUME, overallVolume.Average);
            fastAverage.Add(curVolume);
            slowAverage.Add(curVolume);
            slowerAverage.Add(curVolume);
            double velocity = fastAverage.Average - slowAverage.Average;
            _pos += INCREMENT_SIZE;

            // Is there silence?
            if (Math.Abs(velocity) < volumeFactor * SILENCE_THRESHOLD)
            {
                _msOfSilence += (int)INCREMENT_SIZE.TotalMilliseconds;
            }
            else
            {
                _msOfSilence = 0;
            }

            // Detect silence or extended hesitation
            if (!_started && _pos > MAX_INITIAL_WAIT_TIME)
            {
                _ended = true;
                UtteranceFinishedEvent.FireInBackground(this, new RecorderStateEventArgs(RecorderState.FinishedNothingRecorded), _traceLogger, realTime);
            }

            // Detect the start
            if (!_started && velocity > volumeFactor * UTTERANCE_START_VELOCITY)
            {
                _started = true;
                //startTime = pos;
            }
            // Detect the end, either by rapid waveform decay
            else if (_started &&
                slowerAverage.Average > fastAverage.Average &&
                slowerAverage.Average > slowAverage.Average &&
                slowerAverage.Average < volumeFactor * UTTERANCE_END_THRESHOLD)
            {
                _ended = true;
                //endTime = pos;
                UtteranceFinishedEvent.FireInBackground(this, new RecorderStateEventArgs(RecorderState.Finished), _traceLogger, realTime);
            }
            // or by prolonged silence
            else if (_pos > MAX_INITIAL_WAIT_TIME && overallVolume.Average < MIN_VOLUME)
            {
                _ended = true;
                UtteranceFinishedEvent.FireInBackground(this, new RecorderStateEventArgs(RecorderState.Finished), _traceLogger, realTime);
            }
            else if (_started && (_msOfSilence > MAX_MS_OF_SILENCE || _pos > MAX_RECORD_LENGTH))
            {
                _ended = true;
                //endTime = pos;
                UtteranceFinishedEvent.FireInBackground(this, new RecorderStateEventArgs(RecorderState.Finished), _traceLogger, realTime);
            }
        }

        // This algorithm is janky but so is the rest of this class
        private double CalculateBufferVolume()
        {
            double curVolume = 0;
            for (int c = 0; c < _inBuf.Length; c++)
            {
                curVolume += Math.Abs(_inBuf[c]);
            }

            curVolume = curVolume / (double)_inBuf.Length * (double)short.MaxValue;
            return curVolume;
        }
    }
}
