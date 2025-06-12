using Durandal.Common.Audio;
using ManagedBass;
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
using Durandal.Common.Utils.NativePlatform;
using Durandal.Common.Audio.Hardware;
using System.IO;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Extensions.BassAudio
{
    internal class BassAudioPlayer : AbstractAudioSampleTarget, IAudioRenderDevice
    {
        private readonly ILogger _logger;

        private readonly int _bassDeviceId;
        private bool _useFloatingPoint = false;
        private int _hStream;
        private bool _bassInitialized = false;
        private int _disposed = 0;
        private TimeSpan? _latency;

        public BassAudioPlayer(
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat hardwareFormat,
            string nodeCustomName,
            ILogger logger,
            int deviceId,
            TimeSpan? desiredLatency)
            : base(graph, nameof(BassAudioPlayer), nodeCustomName)
        {
            _logger = logger;
            _latency = desiredLatency;

            if (hardwareFormat.NumChannels > 2)
            {
                throw new ArgumentException("Bass output cannot process more than 2 channels at once");
            }

            if (hardwareFormat.ChannelMapping == MultiChannelMapping.Stereo_R_L)
            {
                throw new ArgumentException("Stereo audio in Bass must be L-R");
            }

            if (desiredLatency.HasValue && desiredLatency.Value <= TimeSpan.Zero)
            {
                throw new ArgumentException(nameof(desiredLatency));
            }

            InputFormat = hardwareFormat.AssertNonNull(nameof(hardwareFormat));
            _bassDeviceId = deviceId;

            // Check for floating-point support
            _useFloatingPoint = false;
#if !PCL
            if (Bass.GetConfigBool(Configuration.Float))
            {
                int hFloatStream = Bass.CreateStream(44100, 1, BassFlags.Float, null, IntPtr.Zero);
                if (hFloatStream != 0)
                {
                    Bass.StreamFree(hFloatStream);
                    _useFloatingPoint = true;
                }
            }
#endif
        }

        ~BassAudioPlayer()
        {
            Dispose(false);
        }

        /// <inheritdoc />
        public override bool IsActiveNode => true;

        /// <inheritdoc />
        public Task StopPlayback()
        {
            if (_bassInitialized)
            {
                Bass.Stop();
                Bass.StreamFree(_hStream);
                Bass.Free();
                _hStream = -1;
                _bassInitialized = false;
            }

            return DurandalTaskExtensions.NoOpTask;
        }

        /// <inheritdoc />
        public Task StartPlayback(IRealTimeProvider realTime)
        {
            if (_bassInitialized)
            {
                // playback already started
                return DurandalTaskExtensions.NoOpTask;
            }

            InitializeOutputDevice();

            if (_bassInitialized)
            {
                BassFlags flags = BassFlags.StreamDownloadBlocks;
                if (_useFloatingPoint)
                {
                    flags |= BassFlags.Float;
                }

                _hStream = Bass.CreateStream(InputFormat.SampleRateHz, InputFormat.NumChannels, flags, UserCallbackStreamWrite);
                if (_hStream == 0)
                {
                    _logger.Log("Failed to create output stream. Error: " + Bass.LastError, LogLevel.Err);
                }

                if (!Bass.ChannelPlay(_hStream))
                {
                    _logger.Log("Failed to start playback on output channel. Error: " + Bass.LastError, LogLevel.Err);
                }
            }

            return DurandalTaskExtensions.NoOpTask;
        }

        //[UnmanagedCallersOnly]
        private unsafe int UserCallbackStreamWrite(int handle, IntPtr buffer, int requestedAudioBytes, IntPtr user)
        {
            try
            {
                int elementSize = _useFloatingPoint ? sizeof(float) : sizeof(short);
                int samplesPerChannelRequested = requestedAudioBytes / elementSize / InputFormat.NumChannels;
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
                                if (_useFloatingPoint)
                                {
                                    floatScratch.Buffer.AsSpan(0, samplesPerChannelReadFromInput * InputFormat.NumChannels)
                                        .CopyTo(
                                            new Span<float>(buffer.ToPointer(), requestedAudioBytes / elementSize)
                                                .Slice(samplesPerChannelFulfilled * InputFormat.NumChannels));
                                }
                                else
                                {
                                    // Fallback to pcm16 conversion here
                                    AudioMath.ConvertSamples_FloatToInt16(
                                        floatScratch.Buffer.AsSpan(0, samplesPerChannelReadFromInput * InputFormat.NumChannels),
                                        new Span<short>(buffer.ToPointer(), requestedAudioBytes / elementSize)
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
                        new Span<byte>(buffer.ToPointer(), requestedAudioBytes).Slice(samplesPerChannelFulfilled * InputFormat.NumChannels * elementSize).Fill(0);
                    }

                    return requestedAudioBytes;
                }
            }
            catch (Exception e)
            {
                _logger.Log(e); // definitely do not want to raise exceptions back to the native layer
                return 0;
            }
        }

        /// <inheritdoc />
        protected override ValueTask WriteAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            throw new InvalidOperationException("Cannot push data to an active Bass audio stream");
        }

        private void InitializeOutputDevice()
        {
            if (_bassInitialized)
            {
                return;
            }

            try
            {
                DeviceInitFlags flags = DeviceInitFlags.Default;
                if (InputFormat.NumChannels == 1)
                {
                    flags |= DeviceInitFlags.Mono;
                }
#if !PCL
                else
                {
                    // TODO theoretically we can support multichannel if we use the right speaker assignment calls, but for now stereo is enough
                    flags |= DeviceInitFlags.Stereo;
                }

                if (!_useFloatingPoint)
                {
                    flags |= DeviceInitFlags.Bits16;
                }

                if (_latency.HasValue)
                {
                    Bass.DeviceBufferLength = Math.Max(1, (int)Math.Round(_latency.Value.TotalMilliseconds));
                }
#endif

                if (!Bass.Init(_bassDeviceId, InputFormat.SampleRateHz, flags))
                {
                    _logger.Log("Bass failed to initialize: " + Bass.LastError, LogLevel.Err);
                    return;
                }

                //if (!Bass.Configure(Configuration.DevNonStop, 1))
                //{
                //    _logger.Log("Bass failed to set DevNonStop flag: " + Bass.LastError, LogLevel.Wrn);
                //    return;
                //}

                //if (!Bass.Configure(Configuration.PlaybackBufferLength, 500))
                //{
                //    _logger.Log("Bass failed to set playback buffer size: " + Bass.LastError, LogLevel.Wrn);
                //    return;
                //}

                //if (!Bass.Configure(Configuration.UpdatePeriod, 5))
                //{
                //    _logger.Log("Bass failed to set playback update interval: " + Bass.LastError, LogLevel.Wrn);
                //    return;
                //}

                _bassInitialized = true;
            }
            catch (Exception e)
            {
                _logger.Log(e, LogLevel.Err);
            }
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
                }

                Bass.Stop();
                Bass.Free();
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}