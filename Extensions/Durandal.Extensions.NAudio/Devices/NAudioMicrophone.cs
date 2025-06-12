
namespace Durandal.Extensions.NAudio.Devices
{
    using Durandal.Common.Audio;
    using Durandal.Common.IO;
    using Durandal.Common.Logger;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using Durandal.Common.Utils;
    using global::NAudio;
    using global::NAudio.Wave;
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A simple microphone class which uses the NAudio backend
    /// </summary>
    public class NAudioMicrophone : AbstractAudioSampleSource, IAudioCaptureDevice
    {
        private readonly byte[] _scratchBufferInput;
        private readonly float[] _scratchBufferOutput;
        private readonly int _byteAlignment;
        private readonly ILogger _logger;
        private int _scratchBytes = 0;
        private readonly WaveIn _recorder;
        private bool _recording = false;
        private int _disposed = 0;

        /// <summary>
        /// Constructs a new <see cref="NAudioMicrophone"/> for audio input.
        /// </summary>
        /// <param name="audioGraph">The graph to associated with this component.</param>
        /// <param name="hardwareFormat">The format to initialize the hardware device with.</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        /// <param name="logger">A logger.</param>
        /// <param name="deviceId">The index of the capture device to use, as an integer from 0 to N on the list of capture devices, or null to use the default device.</param>
        public NAudioMicrophone(IAudioGraph audioGraph, AudioSampleFormat hardwareFormat, string nodeCustomName, ILogger logger, string deviceId = null)
            : base(audioGraph, nameof(NAudioMicrophone), nodeCustomName)
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
            _recorder = new WaveIn(WaveCallbackInfo.FunctionCallback());
            _recorder.WaveFormat = new WaveFormat(hardwareFormat.SampleRateHz, 16, hardwareFormat.NumChannels);
            _recorder.DataAvailable += ProcessAudioInput;
            _scratchBufferOutput = new float[AudioMath.ConvertTimeSpanToSamplesPerChannel(hardwareFormat.SampleRateHz, TimeSpan.FromMilliseconds(_recorder.BufferMilliseconds)) * hardwareFormat.NumChannels];
            _scratchBufferInput = new byte[_scratchBufferOutput.Length * sizeof(short)];
            _byteAlignment = _recorder.WaveFormat.BlockAlign;

            int deviceCount = WaveIn.DeviceCount;
            _logger.Log("Detected " + deviceCount + " WaveIn devices on this system");

            if (deviceCount == 0)
            {
                throw new PlatformNotSupportedException("Cannot initialize NAudio microphone as there are no audio input devices available");
            }

            for (int c = 0; c < deviceCount; c++)
            {
                WaveInCapabilities dev = WaveIn.GetCapabilities(c);
                _logger.Log(Durandal.Common.Utils.StringBuilderPool.Format("    {0}: \"{1}\" ({2} channels){3}{4}{5}{6} Khz",
                    c,
                    dev.ProductName,
                    dev.Channels,
                    dev.SupportsWaveFormat(SupportedWaveFormat.WAVE_FORMAT_1M16) ? " 11" : string.Empty,
                    dev.SupportsWaveFormat(SupportedWaveFormat.WAVE_FORMAT_2M16) ? " 22" : string.Empty,
                    dev.SupportsWaveFormat(SupportedWaveFormat.WAVE_FORMAT_44M16) ? " 44" : string.Empty,
                    dev.SupportsWaveFormat(SupportedWaveFormat.WAVE_FORMAT_48M16) ? " 48" : string.Empty));
            }

            int deviceNum;
            if (string.IsNullOrEmpty(deviceId))
            {
                deviceId = "-1";
            }

            if (int.TryParse(deviceId, out deviceNum))
            {
                if (deviceNum < 0)
                {
                    _logger.Log("Using default audio input device");
                }
                else if (deviceNum >= deviceCount)
                {
                    _logger.Log("Cannot select input audio device " + deviceNum + " as there are not that many devices attached to this system; will revert to default", LogLevel.Wrn);
                }
                else
                {
                    _recorder.DeviceNumber = deviceNum;
                    WaveInCapabilities actualDeviceInfo = WaveIn.GetCapabilities(deviceNum);
                    _logger.Log("Using audio input device \"" + actualDeviceInfo.ProductName + "\"");
                }
            }
            else
            {
                throw new ArgumentException("Cannot parse audio in device argument \"" + deviceId + "\". Expected an integer device index.");
            }
        }

        public override bool IsActiveNode => true;

        public override bool PlaybackFinished => false;

        /// <summary>
        /// Handle audio input events from the NAudio module.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void ProcessAudioInput(object sender, WaveInEventArgs args)
        {
            IAudioSampleTarget target = Output;
            if (target != null)
            {
                int bytesReadFromInput = 0;
                while (bytesReadFromInput < args.BytesRecorded)
                {
                    int bytesCanReadFromInput = Math.Min(_scratchBufferInput.Length - _scratchBytes, args.BytesRecorded - bytesReadFromInput);
                    Buffer.BlockCopy(args.Buffer, bytesReadFromInput, _scratchBufferInput, _scratchBytes, bytesCanReadFromInput);
                    _scratchBytes += bytesCanReadFromInput;
                    bytesReadFromInput += bytesCanReadFromInput;
                    int numSamplesCanConvert = (_scratchBytes - (_scratchBytes % _byteAlignment)) / sizeof(short);
                    int numSamplesPerChannelCanConvert = numSamplesCanConvert / OutputFormat.NumChannels;
                    AudioMath.ConvertSamples_BytePairsLittleEndianToFloat(_scratchBufferInput, 0, _scratchBufferOutput, 0, numSamplesCanConvert);
                    _graph.LockGraph();
                    try
                    {
                        target.WriteAsync(_scratchBufferOutput, 0, numSamplesPerChannelCanConvert, DefaultRealTimeProvider.Singleton, CancellationToken.None).AsTask().Await();
                    }
                    catch (Exception e)
                    {
                        _logger.Log(e, LogLevel.Err);
                    }
                    finally
                    {
                        _graph.UnlockGraph();
                    }

                    _scratchBytes = _scratchBytes % _byteAlignment;
                }
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

        protected override ValueTask<int> ReadAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            throw new NotImplementedException("Cannot read from an NAudioMicrophone; it is a push component in the graph");
        }
    }
}
