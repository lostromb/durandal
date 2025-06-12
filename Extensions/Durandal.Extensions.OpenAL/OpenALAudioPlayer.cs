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
using OpenTK.Audio.OpenAL;
using Durandal.API;
using Durandal.Common.Audio.Hardware;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.MathExt;
using Durandal.Common.Cache;
using Durandal.Common.Collections;

namespace Durandal.Extensions.OpenAL
{
    /// <summary>
    /// Audio output device backed by OpenAL (via OpenTK).
    /// On Windows there are usually better options, but maybe this has some value somewhere.
    /// </summary>
    internal class OpenALAudioPlayer : AbstractAudioSampleTarget, IAudioRenderDevice
    {
        // Queue fixed-size buffers until there is at least this much data queued on each update
        // Because openAL is dumb, we can't take this number lower than about 40
        private static readonly double DEFAULT_BUFFER_FULLNESS_MS = 50;

        // Keep all buffers the same size to avoid device memory fragmentation
        private static readonly double INTERNAL_BUFFER_LENGTH_MS = 5;

        // The interval that the updater loop runs at. Each update can queue or release multiple buffers as needed
        private static readonly double UPDATE_INTERVAL_MS = 5;

        /// <summary>
        /// Multimedia timer to control the updater thread. Ideally should be able to handle waits of less than 10 ms (winMM or equivalent)
        /// </summary>
        private readonly WeakPointer<IHighPrecisionWaitProvider> _highPrecisionWaitProvider;

        /// <summary>
        /// The logger
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// Handle to native OpenAL device
        /// </summary>
        private readonly ALDevice _device;

        /// <summary>
        /// Handle to native OpenAL context
        /// </summary>
        private readonly ALContext _context;

        /// <summary>
        /// The AL sample format of the data
        /// </summary>
        private readonly ALFormat _outputFormat;

        /// <summary>
        /// A driver handle for the single source that we define
        /// </summary>
        private readonly int _sourceId;

        /// <summary>
        /// Whether or not device init completed successfully
        /// </summary>
        private readonly bool _initialized;

        /// <summary>
        /// Whether the current device supports float32 extension
        /// </summary>
        private readonly bool _supportsFloat;

        /// <summary>
        /// Cancellation token source whose main job is to stop the updater thread
        /// </summary>
        private readonly CancellationTokenSource _shutdownSignal = new CancellationTokenSource();

        /// <summary>
        /// Signal for when playback starts so the updater thread can wake up immediately
        /// </summary>
        private readonly AutoResetEventAsync _playbackStartedSignal = new AutoResetEventAsync();

        private readonly double _desiredBufferLatencyMs;

        /// <summary>
        /// Whether or not playback is active
        /// </summary>
        private bool _playing = false;

        private int _disposed = 0;

        /// <summary>
        /// Constructs a new <see cref="OpenALAudioPlayer"/>.
        /// </summary>
        /// <param name="graph">The audio graph to associate with.</param>
        /// <param name="hardwareFormat">The hardware audio format to use.</param>
        /// <param name="nodeCustomName">The custom name of this audio component (may be null)</param>
        /// <param name="logger">A logger</param>
        /// <param name="mediaThreadTimer">A timer capable of high precision, to use for the updater thread</param>
        public OpenALAudioPlayer(
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat hardwareFormat,
            string nodeCustomName,
            ILogger logger,
            WeakPointer<IHighPrecisionWaitProvider> mediaThreadTimer,
            string desiredDeviceName = null,
            TimeSpan? desiredLatency = null)
            : base(graph, nameof(OpenALAudioPlayer), nodeCustomName)
        {
            _logger = logger.AssertNonNull(nameof(logger));
            _highPrecisionWaitProvider = mediaThreadTimer.AssertNonNull(nameof(mediaThreadTimer));
            _desiredBufferLatencyMs = DEFAULT_BUFFER_FULLNESS_MS;
            if (desiredLatency.HasValue)
            {
                _desiredBufferLatencyMs = desiredLatency.Value.TotalMilliseconds.AssertPositive(nameof(desiredLatency));
            }

            hardwareFormat.AssertNonNull(nameof(hardwareFormat));

            if (hardwareFormat.NumChannels > 2)
            {
                throw new ArgumentException("OpenAL output currently cannot process more than 2 channels at once");
            }
            if (hardwareFormat.ChannelMapping == MultiChannelMapping.Stereo_R_L)
            {
                throw new ArgumentException("Stereo audio in OpenAL must be L-R");
            }

            InputFormat = hardwareFormat.AssertNonNull(nameof(hardwareFormat));

            try
            {
                // ALC_EXT_CAPTURE means it supports capture, I assume
                // attributes 4096 and 4097 with a flag of 1 - indicate enumeration support? EAX?
                _logger.Log("Initializing OpenAL audio output...");

                if (string.IsNullOrWhiteSpace(desiredDeviceName))
                {
                    desiredDeviceName = null;
                    _logger.Log("Opening default OpenAL output device");
                }
                else
                {
                    _logger.LogFormat(
                        LogLevel.Std,
                        DataPrivacyClassification.SystemMetadata,
                        "Opening OpenAL output device {0}", desiredDeviceName);
                }

                ThrowOpenALErrors();

                _device = ALC.OpenDevice(desiredDeviceName);
                if (_device == ALDevice.Null)
                {
                    throw new Exception("Failed to open a valid OpenAL output device");
                }

                ThrowOpenALErrors();
                ALContextAttributes contextAttributes = new ALContextAttributes()
                {
                    Frequency = hardwareFormat.SampleRateHz,
                    Refresh = (int)(1000 / UPDATE_INTERVAL_MS), // Windows will only ever give us 25Hz at most, but we try and request more anyway
                };

                _logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "Supported OpenAL Extensions: {0}", ALC.GetString(_device, AlcGetString.Extensions));
                _context = ALC.CreateContext(_device, contextAttributes);

                ThrowOpenALErrors();
                ALC.MakeContextCurrent(_context);
                ThrowOpenALErrors();
                _sourceId = AL.GenSource();
                if (_sourceId == 0)
                {
                    _logger.Log("Failed to allocate a valid source for OpenAL streaming.", LogLevel.Err);
                    return;
                }

                ThrowOpenALErrors();

                // Do we support float32 extension?
                _supportsFloat = ALC.IsExtensionPresent(_device, AL.EXTFloat32.ExtensionName /*"AL_EXT_float32"*/);

                //AL.GetError();
                //int probeBuffer = AL.GenBuffer();
                //AL.BufferData(
                //    probeBuffer,
                //    ALFormat.MonoFloat32Ext,
                //    new short[1],
                //    InputFormat.SampleRateHz);
                //_supportsFloat = AL.GetError() == ALError.NoError;

                _outputFormat = GetALFormat(hardwareFormat, _supportsFloat);

                _logger.Log("OpenAL audio output initialized");
                Task.Run(UpdaterThread);
                _initialized = true;
            }
            catch (BadImageFormatException e)
            {
                _logger.Log("FAILED to load OpenAL shared library. The given binary does not match the current runtime architecture " + RuntimeInformation.ProcessArchitecture, LogLevel.Err);
                _logger.Log(e, LogLevel.Err);
            }
            catch (DllNotFoundException e)
            {
                _logger.Log("FAILED to load OpenAL shared library.", LogLevel.Err);
                _logger.Log(e, LogLevel.Err);
            }
        }

        ~OpenALAudioPlayer()
        {
            Dispose(false);
        }

        public Task StopPlayback()
        {
            if (!_initialized)
            {
                return DurandalTaskExtensions.NoOpTask;
            }

            if (_playing)
            {
                _playing = false;
            }

            return DurandalTaskExtensions.NoOpTask;
        }

        public Task StartPlayback(IRealTimeProvider realTime)
        {
            if (!_initialized)
            {
                return DurandalTaskExtensions.NoOpTask;
            }

            if (_playing)
            {
                // playback already started
                return DurandalTaskExtensions.NoOpTask;
            }

            _playing = true;
            _playbackStartedSignal.Set();
            return DurandalTaskExtensions.NoOpTask;
        }

        protected override ValueTask WriteAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            throw new InvalidOperationException("Cannot push data to an active OpenAL audio stream");
        }

        protected override void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            try
            {
                _shutdownSignal.Cancel();

                if (_device != ALDevice.Null)
                {
                    AL.DeleteSource(_sourceId);
                    ALC.DestroyContext(_context);
                    ALC.CloseDevice(_device);
                }

                if (disposing)
                {
                    _shutdownSignal.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        private static ALFormat GetALFormat(AudioSampleFormat desiredFormat, bool supportsFloat)
        {
            switch (desiredFormat.ChannelMapping)
            {
                case MultiChannelMapping.Monaural:
                    return supportsFloat ? ALFormat.MonoFloat32Ext : ALFormat.Mono16;
                case MultiChannelMapping.Stereo_L_R:
                    return supportsFloat ? ALFormat.StereoFloat32Ext : ALFormat.Stereo16;
                default:
                    throw new NotImplementedException($"Unsupported channel config {desiredFormat.ChannelMapping}");
            }
        }

        /// <summary>
        /// The main update thread loop. Reads from the audio graph and pushes to hardware for as long as
        /// playback is active.
        /// </summary>
        /// <returns></returns>
        private async Task UpdaterThread()
        {
            CancellationToken shutdownToken = _shutdownSignal.Token;
            int[] releasedBufferIds = new int[16];
            Queue<OpenALDeviceBuffer> idleBuffers = new Queue<OpenALDeviceBuffer>();
            SmallDictionary<int, OpenALDeviceBuffer> queuedBuffers = new SmallDictionary<int, OpenALDeviceBuffer>();
            long samplesPerChannelQueued = 0;
            int samplesPerChannelToFillBuffer = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(
                InputFormat.SampleRateHz,
                TimeSpan.FromMilliseconds(_desiredBufferLatencyMs));
            int samplesPerChannelPerBuffer = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(
                InputFormat.SampleRateHz,
                TimeSpan.FromMilliseconds(INTERNAL_BUFFER_LENGTH_MS));

            try
            {
                while (!shutdownToken.IsCancellationRequested)
                {
                    if (_playing)
                    {
                        // Queue new buffers if needed
                        while (samplesPerChannelQueued < samplesPerChannelToFillBuffer)
                        {
                            using (PooledBuffer<float> floatScratch = BufferPool<float>.Rent(samplesPerChannelPerBuffer * InputFormat.NumChannels))
                            {
                                InputGraph.LockGraph();
                                InputGraph.BeginInstrumentedScope(DefaultRealTimeProvider.Singleton, NodeFullName);
                                try
                                {
                                    // Read input from the rest of the graph
                                    int samplesPerChannelReadFromInput = await Input.ReadAsync(
                                        floatScratch.Buffer,
                                        0,
                                        samplesPerChannelPerBuffer,
                                        shutdownToken,
                                        DefaultRealTimeProvider.Singleton);

                                    // IMPORTANT! Because OpenAL operates with thread affinity, we have to 
                                    // make the context current on the local thread exactly here before
                                    // we do buffer operations.
                                    // Any await statements in between could change the thread identity
                                    ALC.MakeContextCurrent(_context);
                                    int samplesReadFromInput = samplesPerChannelReadFromInput * InputFormat.NumChannels;
                                    if (samplesPerChannelReadFromInput > 0)
                                    {
                                        // Try and reuse allocated buffers as much as possible
                                        OpenALDeviceBuffer deviceBuffer;
                                        if (idleBuffers.Count > 0)
                                        {
                                            deviceBuffer = idleBuffers.Dequeue();
                                        }
                                        else
                                        {
                                            deviceBuffer = new OpenALDeviceBuffer()
                                            {
                                                BufferId = AL.GenBuffer(),
                                            };
                                        }

                                        deviceBuffer.SamplesPerChannel = samplesPerChannelReadFromInput;
                                        if (_supportsFloat)
                                        {
                                            AL.BufferData(
                                                deviceBuffer.BufferId,
                                                _outputFormat,
                                                (ReadOnlySpan<float>)floatScratch.Buffer.AsSpan(0, samplesReadFromInput),
                                                InputFormat.SampleRateHz);
                                            AL.SourceQueueBuffer(_sourceId, deviceBuffer.BufferId);
                                        }
                                        else
                                        {
                                            using (PooledBuffer<short> int16Scratch = BufferPool<short>.Rent(samplesReadFromInput))
                                            {
                                                // Convert the input data to pcm16 and copy a new AL buffer
                                                AudioMath.ConvertSamples_FloatToInt16(floatScratch.Buffer, 0, int16Scratch.Buffer, 0, samplesReadFromInput);

                                                AL.BufferData(
                                                    deviceBuffer.BufferId,
                                                    _outputFormat,
                                                    (ReadOnlySpan<short>)int16Scratch.Buffer.AsSpan(0, samplesReadFromInput),
                                                    InputFormat.SampleRateHz);
                                                AL.SourceQueueBuffer(_sourceId, deviceBuffer.BufferId);
                                            }
                                        }

                                        queuedBuffers.Add(deviceBuffer.BufferId, deviceBuffer);
                                        samplesPerChannelQueued += deviceBuffer.SamplesPerChannel;
                                    }
                                    else
                                    {
                                        break; // if graph produced no data, break the loop of trying to fill buffer for now
                                    }
                                }
                                catch (Exception e)
                                {
                                    _logger.Log(e, LogLevel.Err);
                                }
                                finally
                                {
                                    InputGraph.EndInstrumentedScope(DefaultRealTimeProvider.Singleton, AudioMath.ConvertSamplesPerChannelToTimeSpan(InputFormat.SampleRateHz, samplesPerChannelPerBuffer));
                                    InputGraph.UnlockGraph();
                                }
                            }
                        }

                        // Start the source if it has stopped playing (if there was stutter)
                        int sourceState;
                        AL.GetSource(_sourceId, ALGetSourcei.SourceState, out sourceState);
                        ALSourceState State = (ALSourceState)sourceState;

                        if (State != ALSourceState.Playing)
                        {
                            AL.SourcePlay(_sourceId);
                        }

                        // Free old buffers and reuse them
                        int releasedBufferCount;
                        AL.GetSource(_sourceId, ALGetSourcei.BuffersProcessed, out releasedBufferCount);

                        if (releasedBufferCount > 0)
                        {
                            releasedBufferCount = FastMath.Min(releasedBufferCount, releasedBufferIds.Length);
                            AL.SourceUnqueueBuffers(_sourceId, releasedBufferCount, releasedBufferIds);
                            for (int buf = 0; buf < releasedBufferCount; buf++)
                            {
                                OpenALDeviceBuffer actualBuffer;
                                if (queuedBuffers.Remove(releasedBufferIds[buf], out actualBuffer))
                                {
                                    samplesPerChannelQueued -= actualBuffer.SamplesPerChannel;
                                    idleBuffers.Enqueue(actualBuffer);
                                }
                                else
                                {
                                    _logger.Log("Unknown audio buffer ID " + releasedBufferIds[buf], LogLevel.Wrn);
                                    AL.DeleteBuffer(releasedBufferIds[buf]);
                                }
                            }
                        }

                        ThrowOpenALErrors();

                        // Sleep for 5ms
                        await _highPrecisionWaitProvider.Value.WaitAsync(UPDATE_INTERVAL_MS, shutdownToken).ConfigureAwait(false);
                    }
                    else
                    {
                        // Wait for playback to start again
                        await _playbackStartedSignal.WaitAsync(shutdownToken);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                _logger.Log("Fatal error in OpenAL output driver", LogLevel.Err);
                _logger.Log(e, LogLevel.Err);
            }
            finally
            {
                // Try and free all buffers that we allocated
                try
                {
                    while (idleBuffers.Count > 0)
                    {
                        AL.DeleteBuffer(idleBuffers.Dequeue().BufferId);
                    }
                }
                catch (Exception) { }
            }
        }

        private void ThrowOpenALErrors()
        {
            ALError error = AL.GetError();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // hack - for whatever reason, the Linux driver sometimes raises illegal command
                // errors, even if the operations succeed otherwise. A bug in openTK?
                if (error != ALError.NoError && error != ALError.IllegalCommand)
                {
                    throw new Exception(string.Format("OpenAL error code: {0}", error));
                }
            }
            else
            {
                if (error != ALError.NoError)
                {
                    throw new Exception(string.Format("OpenAL error code: {0}", error));
                }
            }
        }

        /// <summary>
        /// Represents a handle to a device buffer which we reuse over and over
        /// to prevent the possibility (?) that openAL runs out of buffer IDs or something.
        /// </summary>
        private class OpenALDeviceBuffer
        {
            public int BufferId;
            public long SamplesPerChannel;
        }
    }
}