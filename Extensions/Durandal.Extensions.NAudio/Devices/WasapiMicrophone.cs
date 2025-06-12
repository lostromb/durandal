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
    using global::NAudio.CoreAudioApi;
    using global::NAudio.Wave;
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Durandal.Common.ServiceMgmt;

    /// <summary>
    /// A simple microphone class which uses the NAudio backend
    /// </summary>
    internal class WasapiMicrophone : AbstractAudioSampleSource, IAudioCaptureDevice
    {
        private static readonly TimeSpan DEFAULT_BUFFER_LENGTH = TimeSpan.FromMilliseconds(50);

        private readonly byte[] _scratchBufferInput;
        private readonly float[] _scratchBufferOutput;
        private readonly int _byteAlignment;
        private readonly ILogger _logger;
        private readonly TimeSpan _bufferLength;
        private int _scratchBytes = 0;
        private readonly WasapiCapture _recorder;
        private bool _recording = false;
        private int _disposed = 0;

        /// <summary>
        /// Constructs a new <see cref="WasapiMicrophone"/> for audio input.
        /// </summary>
        /// <param name="audioGraph">The graph to associated with this component.</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        /// <param name="logger">A logger.</param>
        /// <param name="deviceToUse">The capture device to use, or NULL to use the default device.</param>
        /// <param name="desiredLatency">The desired microphone buffer latency, default is 50</param>
        /// <param name="isLoopbackDevice">Set this to true if the MMDevice passed in is for loopback capture</param>
        public WasapiMicrophone(
            WeakPointer<IAudioGraph> audioGraph,
            string nodeCustomName,
            ILogger logger,
            MMDevice deviceToUse = null,
            TimeSpan? desiredLatency = null,
            bool isLoopbackDevice = false)
            : base(audioGraph, nameof(WasapiMicrophone), nodeCustomName)
        {
            _logger = logger.AssertNonNull(nameof(logger));
            _bufferLength = desiredLatency.GetValueOrDefault(DEFAULT_BUFFER_LENGTH);
            if (_bufferLength <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(desiredLatency));
            }

            if (deviceToUse == null)
            {
                deviceToUse = WasapiCapture.GetDefaultCaptureDevice();
                _logger.Log($"Using default WASAPI capture device {deviceToUse.DeviceFriendlyName}");
            }

            MultiChannelMapping actualOutputMapping;
            switch (deviceToUse.AudioClient.MixFormat.Channels)
            {
                case 1:
                    actualOutputMapping = MultiChannelMapping.Monaural;
                    break;
                case 2:
                    actualOutputMapping = MultiChannelMapping.Stereo_L_R;
                    break;
                case 4:
                    actualOutputMapping = MultiChannelMapping.Packed_4Ch;
                    break;
                default:
                    throw new Exception("WASAPI microphone returned an invalid channel layout");
            }

            // Derive this node's output format from hardware - we can't guarantee that we can change it
            OutputFormat = new AudioSampleFormat(
                deviceToUse.AudioClient.MixFormat.SampleRate,
                deviceToUse.AudioClient.MixFormat.Channels,
                actualOutputMapping);

            if (isLoopbackDevice)
            {
                // In the case of loopback, the MMDevice is actually the output device we're reading the loopback from
                _recorder = new WasapiLoopbackCapture(deviceToUse);
            }
            else
            {
                _recorder = new WasapiCapture(
                    deviceToUse,
                    useEventSync: true,
                    audioBufferMillisecondsLength: (int)_bufferLength.TotalMilliseconds);
            }

            _byteAlignment = _recorder.WaveFormat.BlockAlign;
            _scratchBufferOutput = new float[AudioMath.ConvertTimeSpanToSamplesPerChannel(OutputFormat.SampleRateHz, _bufferLength) * OutputFormat.NumChannels];
                
            if (_recorder.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat &&
                _recorder.WaveFormat.BitsPerSample == 32)
            {
                _recorder.DataAvailable += ProcessAudioInputFloat32;
                _scratchBufferInput = new byte[_scratchBufferOutput.Length * sizeof(float)];
            }
            else if (_recorder.WaveFormat.Encoding == WaveFormatEncoding.Pcm &&
                _recorder.WaveFormat.BitsPerSample == 16)
            {
                _recorder.DataAvailable += ProcessAudioInputInt16;
                _scratchBufferInput = new byte[_scratchBufferOutput.Length * sizeof(short)];
            }
            else
            {
                throw new PlatformNotSupportedException("Audio input device does not support float32 or pcm16 encodings; no fallback available");
            }
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
        /// Handle audio input events from the NAudio module.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void ProcessAudioInputFloat32(object sender, WaveInEventArgs args)
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
                        int numSamplesCanConvert = (_scratchBytes - (_scratchBytes % _byteAlignment)) / sizeof(float);
                        int numSamplesPerChannelCanConvert = numSamplesCanConvert / OutputFormat.NumChannels;
                        AudioMath.ConvertSamples_4BytesFloatLittleEndianToFloat(_scratchBufferInput, 0, _scratchBufferOutput, 0, numSamplesCanConvert);
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
                OutputGraph.EndInstrumentedScope(DefaultRealTimeProvider.Singleton, AudioMath.ConvertSamplesPerChannelToTimeSpan(OutputFormat.SampleRateHz, args.BytesRecorded / OutputFormat.NumChannels / sizeof(float)));
                OutputGraph.UnlockGraph();
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
            throw new NotImplementedException("Cannot read from a WasapiMicrophone; it is a push component in the graph");
        }
    }
}