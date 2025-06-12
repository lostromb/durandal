using Durandal.Common.Audio;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Durandal.Common.Logger;
using System.Runtime.InteropServices;
using Durandal.Common.Utils;
using Durandal.Common.Time;
using Durandal.Common.Tasks;
using Durandal.Common.IO;
using Durandal.API;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.MathExt;
using Durandal.Common.Collections;
using Durandal.Common.Audio.Hardware;
using static SDL2.SDL;

namespace Durandal.Extensions.SDL
{
    /// <summary>
    /// Audio output device backed by SDL2.
    /// Generally intended for use on Linux, but it works fine on Windows too.
    /// </summary>
    internal class SDL2AudioPlayer : AbstractAudioSampleTarget, IAudioRenderDevice
    {
        private static readonly TimeSpan DEFAULT_UPDATE_LATENCY = TimeSpan.FromMilliseconds(5);

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
        private readonly uint _outputStream;

        /// <summary>
        /// We need an explicit pinned handle to this callback function so it doesn't get garbage collected
        /// </summary>
        private readonly SDL_AudioCallback _audioCallback;

        /// <summary>
        /// Whether the negotiated output format allows for float32
        /// </summary>
        private bool _supportsFloat;

        /// <summary>
        /// Whether playback is active
        /// </summary>
        private bool _playing = false;

        private int _disposed = 0;

        /// <summary>
        /// Constructs a new <see cref="SDL2AudioPlayer"/>.
        /// </summary>
        /// <param name="graph">The audio graph to associate with.</param>
        /// <param name="hardwareFormat">The hardware audio format to use.</param>
        /// <param name="nodeCustomName">The custom name of this audio component (may be null)</param>
        /// <param name="logger">A logger</param>
        /// <param name="desiredDeviceName">The desired device name to use, as listed by SDL, or null to use the default.</param>
        public SDL2AudioPlayer(
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat hardwareFormat,
            string nodeCustomName,
            ILogger logger,
            string desiredDeviceName = null,
            TimeSpan? desiredLatency = null)
            : base(graph, nameof(SDL2AudioPlayer), nodeCustomName)
        {
            _logger = logger.AssertNonNull(nameof(logger));
            hardwareFormat.AssertNonNull(nameof(hardwareFormat));

            if ((hardwareFormat.ChannelMapping != MultiChannelMapping.Monaural) &&
                (hardwareFormat.ChannelMapping != MultiChannelMapping.Stereo_L_R) &&
                (hardwareFormat.ChannelMapping != MultiChannelMapping.Quadraphonic) &&
                (hardwareFormat.ChannelMapping != MultiChannelMapping.Surround_5_1ch))
            {
                throw new ArgumentException($"Unsupported SDL2 audio channel mapping: {hardwareFormat.ChannelMapping}. SDL2 supports mono, stereo, quad, and 5.1.");
            }

            TimeSpan actualLatency = desiredLatency.GetValueOrDefault(DEFAULT_UPDATE_LATENCY);
            _audioCallback = UpdateCallback;

            try
            {
                _logger.Log("Initializing SDL2 audio output...");

                int idealSampleBufferSizePerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(hardwareFormat.SampleRateHz, actualLatency);
                if (idealSampleBufferSizePerChannel > 32768)
                {
                    idealSampleBufferSizePerChannel = 32768;
                }
                else
                {
                    idealSampleBufferSizePerChannel = FastMath.RoundUpToPowerOf2(idealSampleBufferSizePerChannel);
                }

                // https://wiki.libsdl.org/SDL2/SDL_AudioSpec
                SDL_AudioSpec actualSettings;
                SDL_AudioSpec desiredSettings = new SDL_AudioSpec
                {
                    channels = (byte)hardwareFormat.NumChannels,
                    format = AUDIO_F32SYS,
                    freq = hardwareFormat.SampleRateHz,
                    samples = (ushort)idealSampleBufferSizePerChannel, // this value must be a power of two
                    callback = _audioCallback,
                };

                if (string.IsNullOrWhiteSpace(desiredDeviceName))
                {
                    desiredDeviceName = null;
                    _logger.Log("Opening default SDL2 output device");
                }
                else
                {
                    _logger.LogFormat(
                        LogLevel.Std,
                        DataPrivacyClassification.SystemMetadata,
                        "Opening SDL2 output device {0}", desiredDeviceName);
                }
                
                _outputStream = SDL_OpenAudioDevice(
                    desiredDeviceName,
                    0,
                    ref desiredSettings,
                    out actualSettings,
                    allowed_changes: (int)(SDL_AUDIO_ALLOW_CHANNELS_CHANGE | SDL_AUDIO_ALLOW_FORMAT_CHANGE | SDL_AUDIO_ALLOW_SAMPLES_CHANGE));

                if (_outputStream == 0)
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
                    SDL_CloseAudioDevice(_outputStream);
                    throw new Exception("SDL2 open audio device initialization failed because it does not support requested sample formats");
                }

                _supportsFloat = actualSettings.format == AUDIO_F32SYS || actualSettings.format == AUDIO_F32;
                _logger.LogFormat(
                    LogLevel.Std,
                    DataPrivacyClassification.SystemMetadata,
                    "SDL2 Audio output initialized. Device driver = {0}, format = {1}/{2}, using float32 = {3}",
                    SDL_GetCurrentAudioDriver(),
                    actualSettings.freq,
                    actualSettings.channels,
                    _supportsFloat);

                MultiChannelMapping actualMapping;
                switch (actualSettings.channels)
                {
                    case 1:
                        actualMapping = MultiChannelMapping.Monaural;
                        break;
                    case 2:
                        actualMapping = MultiChannelMapping.Stereo_L_R;
                        break;
                    case 4:
                        actualMapping = MultiChannelMapping.Quadraphonic;
                        break;
                    case 6:
                        actualMapping = MultiChannelMapping.Surround_5_1ch;
                        break;
                    default:
                        throw new Exception($"Actual channel count obtained from SDL2 audio was not expected (requested {hardwareFormat.NumChannels} got {actualSettings.channels})");
                }

                InputFormat = new AudioSampleFormat(actualSettings.freq, actualSettings.channels, actualMapping);
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
                if (_outputStream != 0)
                {
                    SDL_CloseAudioDevice(_outputStream);
                }

                throw;
            }
        }

        ~SDL2AudioPlayer()
        {
            Dispose(false);
        }

        public override bool IsActiveNode => true;

        public Task StartPlayback(IRealTimeProvider realTime)
        {
            if (!_initialized || _playing)
            {
                return DurandalTaskExtensions.NoOpTask;
            }

            if (_outputStream != 0)
            {
                SDL_PauseAudioDevice(_outputStream, pause_on: 0);
            }

            _playing = true;

            return DurandalTaskExtensions.NoOpTask;
        }

        public Task StopPlayback()
        {
            if (!_initialized || !_playing)
            {
                return DurandalTaskExtensions.NoOpTask;
            }

            if (_outputStream != 0)
            {
                SDL_PauseAudioDevice(_outputStream, pause_on: 1);
            }

            _playing = false;

            return DurandalTaskExtensions.NoOpTask;
        }

        protected override ValueTask WriteAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            throw new InvalidOperationException("Cannot push data to an active SDL2 audio stream");
        }

        protected override void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            try
            {
                StopPlayback();

                if (disposing)
                {
                    if (_outputStream != 0)
                    {
                        SDL_CloseAudioDevice(_outputStream);
                    }
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Callback function run by SDL internal thread when it needs to fill the audio buffer.
        /// </summary>
        /// <param name="userdata"> User data. Null in this case</param>
        /// <param name="stream">The pointer to the raw audio buffer in the format that we requested; usually float32</param>
        /// <param name="streamLengthBytes">The length in bytes of the target audio buffer</param>
        private unsafe void UpdateCallback(IntPtr userdata, IntPtr stream, int streamLengthBytes)
        {
            try
            {
                int elementSize = _supportsFloat ? sizeof(float) : sizeof(short);
                int samplesPerChannelRequested = streamLengthBytes / elementSize / InputFormat.NumChannels;
                int samplesPerChannelFulfilled = 0;
                using (PooledBuffer<float> floatScratch = BufferPool<float>.Rent(samplesPerChannelRequested * InputFormat.NumChannels))
                {
                    int attempts = 3;
                    while (attempts-- > 0 && samplesPerChannelFulfilled < samplesPerChannelRequested)
                    {
                        InputGraph.LockGraph();
                        InputGraph.BeginInstrumentedScope(DefaultRealTimeProvider.Singleton, NodeFullName);
                        try
                        {
                            // Read input from the rest of the graph
                            int samplesPerChannelReadFromInput = 0;
                            if (Input != null)
                            {
                                samplesPerChannelReadFromInput = Input.ReadAsync(
                                    floatScratch.Buffer,
                                    0,
                                    samplesPerChannelRequested - samplesPerChannelFulfilled,
                                    CancellationToken.None,
                                    DefaultRealTimeProvider.Singleton).Await();
                            }

                            if (samplesPerChannelReadFromInput > 0)
                            {
                                if (_supportsFloat)
                                {
                                    floatScratch.Buffer.AsSpan(0, samplesPerChannelReadFromInput * InputFormat.NumChannels)
                                        .CopyTo(
                                            new Span<float>(stream.ToPointer(), streamLengthBytes / elementSize)
                                                .Slice(samplesPerChannelFulfilled * InputFormat.NumChannels));
                                }
                                else
                                {
                                    // Fallback to pcm16 conversion here
                                    AudioMath.ConvertSamples_FloatToInt16(
                                        floatScratch.Buffer.AsSpan(0, samplesPerChannelReadFromInput * InputFormat.NumChannels),
                                        new Span<short>(stream.ToPointer(), streamLengthBytes / elementSize)
                                            .Slice(samplesPerChannelFulfilled * InputFormat.NumChannels),
                                        samplesPerChannelReadFromInput * InputFormat.NumChannels,
                                        clamp: true);
                                }

                                samplesPerChannelFulfilled += samplesPerChannelReadFromInput;
                            }
                        }
                        finally
                        {
                            InputGraph.EndInstrumentedScope(DefaultRealTimeProvider.Singleton, AudioMath.ConvertSamplesPerChannelToTimeSpan(InputFormat.SampleRateHz, samplesPerChannelRequested));
                            InputGraph.UnlockGraph();
                        }
                    }

                    // Pad remainder with zeroes if we couldn't fulfill the entire amount
                    if (samplesPerChannelFulfilled < samplesPerChannelRequested)
                    {
                        _logger.LogFormat(LogLevel.Wrn, DataPrivacyClassification.SystemMetadata, "Audio buffer underrun of {0} samples", samplesPerChannelRequested - samplesPerChannelFulfilled);
                        new Span<byte>(stream.ToPointer(), streamLengthBytes).Slice(samplesPerChannelFulfilled * InputFormat.NumChannels * elementSize).Fill(0);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.Log(e); // definitely do not want to raise exceptions back to the native layer
            }
        }
    }
}