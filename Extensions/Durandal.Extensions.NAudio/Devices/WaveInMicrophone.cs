
namespace Durandal.Extensions.NAudio.Devices
{
    using Durandal.Common.Audio;
    using Durandal.Common.Collections;
    using Durandal.Common.IO;
    using Durandal.Common.Logger;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using Durandal.Common.Utils;
    using Durandal.Common.Audio.Hardware;
    using global::NAudio;
    using global::NAudio.Wave;
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Durandal.Common.ServiceMgmt;

    /// <summary>
    /// A simple microphone class which uses the NAudio backend
    /// </summary>
    internal class WaveInMicrophone : AbstractAudioSampleSource, IAudioCaptureDevice
    {
        private readonly byte[] _scratchBufferInput;
        private readonly float[] _scratchBufferOutput;
        private readonly int _byteAlignment;
        private readonly ILogger _logger;
        private int _scratchBytes = 0;
        private readonly WaveInEvent _recorder;
        private bool _recording = false;
        private int _disposed = 0;

        /// <summary>
        /// Constructs a new <see cref="WaveInMicrophone"/> for audio input.
        /// </summary>
        /// <param name="audioGraph">The graph to associated with this component.</param>
        /// <param name="hardwareFormat">The format to initialize the hardware device with.</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        /// <param name="logger">A logger.</param>
        public WaveInMicrophone(WeakPointer<IAudioGraph> audioGraph, AudioSampleFormat hardwareFormat, string nodeCustomName, ILogger logger)
            : base(audioGraph, nameof(WaveInMicrophone), nodeCustomName)
        {
            OutputFormat = hardwareFormat.AssertNonNull(nameof(hardwareFormat));
            if (hardwareFormat.NumChannels > 2)
            {
                throw new ArgumentException("NAudio input cannot process more than 2 channels at once");
            }
            if (hardwareFormat.ChannelMapping == MultiChannelMapping.Stereo_R_L)
            {
                throw new ArgumentException("Stereo audio in NAudio must be L-R");
            }

            _logger = logger.AssertNonNull(nameof(logger));
            _recorder = new WaveInEvent();
            _recorder.WaveFormat = new WaveFormat(hardwareFormat.SampleRateHz, 16, hardwareFormat.NumChannels); // TODO can this support native float?
            _recorder.DataAvailable += ProcessAudioInputInt16;
            _scratchBufferOutput = new float[AudioMath.ConvertTimeSpanToSamplesPerChannel(hardwareFormat.SampleRateHz, TimeSpan.FromMilliseconds(_recorder.BufferMilliseconds)) * hardwareFormat.NumChannels];
            _scratchBufferInput = new byte[_scratchBufferOutput.Length * sizeof(short)];
            _byteAlignment = _recorder.WaveFormat.BlockAlign;
        }

        /// <inheritdoc />
        public override bool IsActiveNode => true;

        /// <inheritdoc />
        public override bool PlaybackFinished => false;

        /// <summary>
        /// Handle audio input events from the NAudio module.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void ProcessAudioInputInt16(object sender, WaveInEventArgs args)
        {
            OutputGraph.LockGraph();
            OutputGraph.BeginInstrumentedScope(DefaultRealTimeProvider.Singleton, NodeFullName);
            try
            {
                if (Output != null)
                {
                    int bytesReadFromInput = 0;
                    while (bytesReadFromInput < args.BytesRecorded)
                    {
                        int bytesCanReadFromInput = Math.Min(_scratchBufferInput.Length - _scratchBytes, args.BytesRecorded - bytesReadFromInput);
                        ArrayExtensions.MemCopy(args.Buffer, bytesReadFromInput, _scratchBufferInput, _scratchBytes, bytesCanReadFromInput);
                        _scratchBytes += bytesCanReadFromInput;
                        bytesReadFromInput += bytesCanReadFromInput;
                        int numSamplesCanConvert = (_scratchBytes - (_scratchBytes % _byteAlignment)) / sizeof(short);
                        int numSamplesPerChannelCanConvert = numSamplesCanConvert / OutputFormat.NumChannels;
                        AudioMath.ConvertSamples_2BytesIntLittleEndianToFloat(_scratchBufferInput, 0, _scratchBufferOutput, 0, numSamplesCanConvert);
                        Output.WriteAsync(_scratchBufferOutput, 0, numSamplesPerChannelCanConvert, CancellationToken.None, DefaultRealTimeProvider.Singleton).Await();
                        _scratchBytes = _scratchBytes % _byteAlignment;
                    }
                }
            }
            catch (Exception e)
            {
                _logger.Log(e, LogLevel.Err);
            }
            finally
            {
                OutputGraph.EndInstrumentedScope(DefaultRealTimeProvider.Singleton, AudioMath.ConvertSamplesPerChannelToTimeSpan(OutputFormat.SampleRateHz, args.BytesRecorded / OutputFormat.NumChannels / sizeof(short)));
            }
        }

        /// <summary>
        /// Turns the microphone on
        /// </summary>
        public async Task StartCapture(IRealTimeProvider realTime)
        {
            if (_recording)
            {
                await DurandalTaskExtensions.NoOpTask;
                return;
            }

            //try
            //{
                _recorder.StartRecording();
                _recording = true;
            //}
            //catch (MmException)
            //{
            //    // No microphone on this system
            //}
        }

        /// <summary>
        /// Turns the microphone off
        /// </summary>
        public async Task StopCapture()
        {
            if (!_recording)
            {
                await DurandalTaskExtensions.NoOpTask;
                return;
            }

            _recording = false;
            _recorder.StopRecording();
        }

        /// <inheritdoc />
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
                    _recorder?.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        /// <inheritdoc />
        protected override ValueTask<int> ReadAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            throw new InvalidOperationException("Cannot read from a WaveInMicrophone; it is a push component in the graph");
        }
    }
}