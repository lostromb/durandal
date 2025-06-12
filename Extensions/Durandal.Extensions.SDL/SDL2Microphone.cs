using System;
using System.Threading;
using Durandal.Common.Audio;
using System.Diagnostics;
using Durandal.Common.Logger;
using Durandal.Common.Audio.Hardware;
using Durandal.Common.Utils;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Durandal.Common.Time;
using Durandal.Common.Tasks;
using Durandal.Common.IO;
using Durandal.API;
using Durandal.Common.MathExt;
using static SDL2.SDL;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Extensions.SDL
{
    /// <summary>
    /// A simple microphone class which uses the SDL2 backend.
    /// </summary>
    internal class SDL2Microphone : AbstractAudioSampleSource, IAudioCaptureDevice
    {
        private static readonly TimeSpan DESIRED_UPDATE_LATENCY = TimeSpan.FromMilliseconds(5);

        /// <summary>
        /// The logger
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// Whether or not device init completed successfully
        /// </summary>
        private readonly bool _initialized;

        /// <summary>
        /// The handle the native SDL stream object
        /// </summary>
        private readonly uint _inputStream;

        /// <summary>
        /// We need an explicit pinned handle to this callback function so it doesn't get garbage collected
        /// </summary>
        private readonly SDL_AudioCallback _audioCallback;

        /// <summary>
        /// Whether the negotiated output format allows for float32
        /// </summary>
        private bool _supportsFloat;

        /// <summary>
        /// Whether recording is active
        /// </summary>
        private bool _recording = false;

        private int _disposed = 0;

        /// <summary>
        /// Constructs a new <see cref="SDL2Microphone"/> for audio input.
        /// </summary>
        /// <param name="audioGraph">The graph to associated with this component.</param>
        /// <param name="desiredHardwareFormat">The format to initialize the hardware device with.
        /// The actual result may be different based on what is actually supported.</param>
        /// <param name="nodeCustomName">The name of this node in the audio graph, can be null</param>
        /// <param name="logger">A logger.</param>
        /// <param name="desiredDeviceName">The name of the capture device to use, as defined by SDL, or null to use the default device.</param>
        public SDL2Microphone(
            WeakPointer<IAudioGraph> audioGraph,
            AudioSampleFormat desiredHardwareFormat,
            string nodeCustomName,
            ILogger logger,
            string desiredDeviceName = null)
            : base(audioGraph, nameof(SDL2Microphone), nodeCustomName)
        {
            _logger = logger;
            desiredHardwareFormat.AssertNonNull(nameof(desiredHardwareFormat));
            _audioCallback = UpdateCallback;

            //if (desiredHardwareFormat.NumChannels > 2)
            //{
            //    throw new ArgumentException("SDL2 input currently cannot process more than 2 channels at once");
            //}
            if (desiredHardwareFormat.ChannelMapping == MultiChannelMapping.Stereo_R_L)
            {
                throw new ArgumentException("Stereo audio in SDL2 must be L-R");
            }

            try
            {
                _logger.Log("Initializing SDL2 audio input...");

                int idealSampleBufferSizePerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(desiredHardwareFormat.SampleRateHz, DESIRED_UPDATE_LATENCY);
                if (idealSampleBufferSizePerChannel > 32768)
                {
                    idealSampleBufferSizePerChannel = 32768;
                }
                else
                {
                    idealSampleBufferSizePerChannel = FastMath.RoundUpToPowerOf2(idealSampleBufferSizePerChannel);
                }

                SDL_AudioSpec desiredSettings = new SDL_AudioSpec
                {
                    channels = (byte)desiredHardwareFormat.NumChannels,
                    format = AUDIO_F32SYS,
                    freq = desiredHardwareFormat.SampleRateHz,
                    samples = (ushort)idealSampleBufferSizePerChannel,
                };
                SDL_AudioSpec actualSettings;

                desiredSettings.callback = _audioCallback;

                if (string.IsNullOrWhiteSpace(desiredDeviceName))
                {
                    desiredDeviceName = null;
                    _logger.Log("Opening default SDL2 input device");
                }
                else
                {
                    _logger.LogFormat(
                        LogLevel.Std,
                        DataPrivacyClassification.SystemMetadata,
                        "Opening SDL2 input device {0}", desiredDeviceName);
                }

                _inputStream = SDL_OpenAudioDevice(
                    desiredDeviceName,
                    1,
                    ref desiredSettings,
                    out actualSettings,
                    allowed_changes: (int)(SDL_AUDIO_ALLOW_ANY_CHANGE));

                if (_inputStream == 0)
                {
                    if (string.Equals("No such device.", SDL_GetError(), StringComparison.Ordinal))
                    {
                        throw new ArgumentException($"Could not open SDL device \"{desiredDeviceName}\"; no such device.");
                    }
                    else
                    {
                        throw new Exception($"SDL2 open audio device initialization failed with error \"{SDL_GetError()}\"");
                    }
                }

                if (actualSettings.format != AUDIO_F32SYS &&
                    actualSettings.format != AUDIO_F32 &&
                    actualSettings.format != AUDIO_S16SYS &&
                    actualSettings.format != AUDIO_S16)
                {
                    SDL_CloseAudioDevice(_inputStream);
                    throw new Exception("SDL2 open audio device initialization failed because it does not support requested sample formats");
                }

                MultiChannelMapping actualOutputMapping;
                switch (actualSettings.channels)
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
                        throw new Exception("SDL microphone returned an invalid channel layout");
                }

                // Derive the actual hardware output format based on what the device initialized
                OutputFormat = new AudioSampleFormat(
                    actualSettings.freq,
                    actualSettings.channels,
                    actualOutputMapping);

                _supportsFloat = actualSettings.format == AUDIO_F32SYS || actualSettings.format == AUDIO_F32;
                _logger.LogFormat(
                    LogLevel.Std,
                    DataPrivacyClassification.SystemMetadata,
                    "SDL2 Audio input initialized. Device driver = {0}, format = {1}/{2}, using float32 = {3}",
                    SDL_GetCurrentAudioDriver(),
                    actualSettings.freq,
                    actualSettings.channels,
                    _supportsFloat);

                _initialized = true;
            }
            catch (BadImageFormatException e)
            {
                _logger.Log("FAILED to load SDL2 shared library. The given binary does not match the current runtime architecture " + RuntimeInformation.ProcessArchitecture, LogLevel.Err);
                _logger.Log(e, LogLevel.Err);
                throw;
            }
            catch (DllNotFoundException e)
            {
                _logger.Log("FAILED to load SDL2 shared library. Required DLLs were not found.", LogLevel.Err);
                _logger.Log(e, LogLevel.Err);
                throw;
            }
            catch (Exception e)
            {
                _logger.Log("Unexpected error while initializing SDL2 audio device.", LogLevel.Err);
                _logger.Log(e, LogLevel.Err);
                if (_inputStream != 0)
                {
                    SDL_CloseAudioDevice(_inputStream);
                }

                throw;
            }
        }

        ~SDL2Microphone()
        {
            Dispose(false);
        }

        public override bool IsActiveNode => true;

        public override bool PlaybackFinished => false;

        /// <summary>
        /// Turns the microphone on
        /// </summary>
        public Task StartCapture(IRealTimeProvider realTime)
        {
            if (!_initialized || _recording)
            {
                return DurandalTaskExtensions.NoOpTask;
            }

            if (_inputStream != 0)
            {
                SDL_PauseAudioDevice(_inputStream, pause_on: 0);
            }

            _recording = true;

            return DurandalTaskExtensions.NoOpTask;
        }

        /// <summary>
        /// Turns the microphone off
        /// </summary>
        public Task StopCapture()
        {
            if (!_initialized || !_recording)
            {
                return DurandalTaskExtensions.NoOpTask;
            }

            if (_inputStream != 0)
            {
                SDL_PauseAudioDevice(_inputStream, pause_on: 1);
            }

            _recording = false;

            return DurandalTaskExtensions.NoOpTask;
        }

        protected override void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            try
            {
                StopCapture();

                if (disposing)
                {
                    if (_inputStream != 0)
                    {
                        SDL_CloseAudioDevice(_inputStream);
                    }
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        protected override ValueTask<int> ReadAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            throw new NotImplementedException("Cannot read from an SDL2Microphone; it is a push component in the graph");
        }

        /// <summary>
        /// Callback function run by SDL internal thread when recorded audio data is available to the application.
        /// </summary>
        /// <param name="userdata">User data. Null in this case</param>
        /// <param name="stream">The pointer to the raw audio buffer in the format that we requested; usually float32</param>
        /// <param name="streamLength">The length in bytes of the source audio buffer</param>
        private unsafe void UpdateCallback(IntPtr userdata, IntPtr stream, int streamLength)
        {
            try
            {
                int elementSize = _supportsFloat ? sizeof(float) : sizeof(short);
                IAudioSampleTarget target = Output;
                if (target != null)
                {
                    int numSamplesTotal = streamLength / elementSize;
                    int numSamplesPerChannel = numSamplesTotal / OutputFormat.NumChannels;
                    using (PooledBuffer<float> floatScratch = BufferPool<float>.Rent(numSamplesTotal))
                    {
                        if (_supportsFloat)
                        {
                            // If input is already float, just copy it natively
                            Span<float> sourceSpan = new Span<float>((void*)stream, numSamplesTotal);
                            sourceSpan.CopyTo(floatScratch.AsSpan);
                        }
                        else
                        {
                            // Otherwise we need to do intermediate conversion from pcm16
                            using (PooledBuffer<short> conversionScratch = BufferPool<short>.Rent(numSamplesTotal))
                            {
                                Span<short> sourceSpan = new Span<short>((void*)stream, numSamplesTotal);
                                sourceSpan.CopyTo(conversionScratch.AsSpan);
                                AudioMath.ConvertSamples_Int16ToFloat(conversionScratch.Buffer, 0, floatScratch.Buffer, 0, numSamplesTotal);
                            }
                        }

                        // Write to audio graph
                        OutputGraph.LockGraph();
                        OutputGraph.BeginInstrumentedScope(DefaultRealTimeProvider.Singleton, NodeFullName);
                        try
                        {
                            if (Output != null)
                            {
                                Output.WriteAsync(floatScratch.Buffer, 0, numSamplesPerChannel, CancellationToken.None, DefaultRealTimeProvider.Singleton).Await();
                            }
                        }
                        finally
                        {
                            OutputGraph.EndInstrumentedScope(DefaultRealTimeProvider.Singleton, AudioMath.ConvertSamplesPerChannelToTimeSpan(OutputFormat.SampleRateHz, numSamplesPerChannel));
                            OutputGraph.UnlockGraph();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _logger.Log(e, LogLevel.Err); // !!do not!! expose exceptions to the native layer!
            }
        }
    }
}