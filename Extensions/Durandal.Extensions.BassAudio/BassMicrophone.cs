using System;
using System.Threading;
using ManagedBass;
using Durandal.Common.Audio;
using System.Diagnostics;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Durandal.Common.Audio.Codecs.Opus.Common;
using Durandal.Common.Time;
using Durandal.Common.Tasks;
using Durandal.Common.IO;
using Durandal.API;
using Durandal.Common.Utils.NativePlatform;
using Durandal.Common.Audio.Hardware;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Extensions.BassAudio
{
    /// <summary>
    /// A simple microphone class which uses the BASS backend.
    /// Fixme: Using polling instead of callbacks + buffers; there should be less latency
    /// </summary>
    internal class BassMicrophone : AbstractAudioSampleSource, IAudioCaptureDevice
    {
        private const int NULL_DEVICE = 0;
        private readonly ILogger _logger;
        private readonly int _recordingDeviceIndex;
        private bool _useFloatingPoint;
        private int _hRecord = NULL_DEVICE;
        private bool _bassInitialized = false;
        private int _disposed = 0;

        /// <summary>
        /// Constructs a new <see cref="BassMicrophone"/> for audio input.
        /// </summary>
        /// <param name="audioGraph">The graph to associated with this component.</param>
        /// <param name="desiredHardwareFormat">The format to initialize the hardware device with.
        /// The actual result may be different based on what is actually supported.</param>
        /// <param name="nodeCustomName">The name of this node in the audio graph, can be null</param>
        /// <param name="logger">A logger.</param>
        /// <param name="deviceId">The index of the capture device to use, as an integer from 0 to N on the list of capture devices, or -1 to use the default device.</param>
        public BassMicrophone(
            WeakPointer<IAudioGraph> audioGraph,
            AudioSampleFormat desiredHardwareFormat,
            string nodeCustomName,
            ILogger logger,
            int deviceId)
            : base(audioGraph, nameof(BassMicrophone), nodeCustomName)
        {
            _logger = logger.AssertNonNull(nameof(logger));
            _recordingDeviceIndex = deviceId;
            OutputFormat = desiredHardwareFormat.AssertNonNull(nameof(desiredHardwareFormat));
            if (desiredHardwareFormat.NumChannels > 2)
            {
                throw new ArgumentException("Bass input currently cannot process more than 2 channels at once");
            }
            if (desiredHardwareFormat.ChannelMapping == MultiChannelMapping.Stereo_R_L)
            {
                throw new ArgumentException("Stereo audio in Bass must be L-R");
            }

            // Check for floating-point support, and also load the library at this point as well
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

            if (!Bass.RecordInit(_recordingDeviceIndex))
            {
                _logger.Log("FAILED to initialize recording device: Error " + Bass.LastError, LogLevel.Err);
            }

            //Bass.RecordingBufferLength = 3000;

            // See what is the actual hardware rate of the capture device, and override the output format if it differs
            RecordInfo recordInfo;
            if (Bass.RecordGetInfo(out recordInfo) && recordInfo.Frequency != 0)
            {
                OutputFormat = new AudioSampleFormat(recordInfo.Frequency, OutputFormat.NumChannels, OutputFormat.ChannelMapping);
            }

            _bassInitialized = true;
        }

        ~BassMicrophone()
        {
            Dispose(false);
        }

        public override bool IsActiveNode => true;

        public override bool PlaybackFinished => false;

        private unsafe bool AudioDataAvailableCallback(int handle, IntPtr buffer, int length, IntPtr user)
        {
            IAudioSampleTarget target = Output;
            if (target != null)
            {
                try
                {
                    if (_useFloatingPoint)
                    {
                        int samplesPerChannelReadFromInput = 0;
                        int samplesPerChannelAvailable = length / OutputFormat.NumChannels / sizeof(float);
                        if ((length / sizeof(float) % OutputFormat.NumChannels) != 0)
                        {
                            _logger.Log("Microphone data is not byte-aligned. This is a problem", LogLevel.Wrn);
                        }

                        using (PooledBuffer<float> scratchBuf = BufferPool<float>.Rent())
                        {
                            float* sourceBuf = (float*)buffer.ToPointer();
                            int maxReadLengthSamplesPerChannel = scratchBuf.Length / OutputFormat.NumChannels;
                            while (samplesPerChannelReadFromInput < samplesPerChannelAvailable)
                            {
                                int thisReadLengthSamplesPerChannel = Math.Min(maxReadLengthSamplesPerChannel, samplesPerChannelAvailable - samplesPerChannelReadFromInput);
                                int thisReadLengthSamples = OutputFormat.NumChannels * thisReadLengthSamplesPerChannel;
                                for (int c = 0; c < thisReadLengthSamples; c++)
                                {
                                    scratchBuf.Buffer[c] = sourceBuf[c];
                                }

                                OutputGraph.LockGraph();
                                try
                                {
                                    OutputGraph.BeginInstrumentedScope(DefaultRealTimeProvider.Singleton, NodeFullName);
                                    target.WriteAsync(
                                        scratchBuf.Buffer,
                                        0,
                                        thisReadLengthSamplesPerChannel,
                                        CancellationToken.None,
                                        DefaultRealTimeProvider.Singleton).Await();
                                }
                                catch (Exception e)
                                {
                                    _logger.Log(e);
                                }
                                finally
                                {
                                    OutputGraph.EndInstrumentedScope(DefaultRealTimeProvider.Singleton, AudioMath.ConvertSamplesPerChannelToTimeSpan(OutputFormat.SampleRateHz, thisReadLengthSamplesPerChannel));
                                    OutputGraph.UnlockGraph();
                                }

                                sourceBuf += thisReadLengthSamples;
                                samplesPerChannelReadFromInput += thisReadLengthSamplesPerChannel;
                            }
                        }
                    }
                    else
                    {
                        int samplesPerChannelReadFromInput = 0;
                        int samplesPerChannelAvailable = length / OutputFormat.NumChannels / sizeof(short);
                        if ((length / sizeof(short) % OutputFormat.NumChannels) != 0)
                        {
                            _logger.Log("Microphone data is not byte-aligned. This is a problem", LogLevel.Wrn);
                        }

                        using (PooledBuffer<short> int16Buf = BufferPool<short>.Rent())
                        using (PooledBuffer<float> floatBuf = BufferPool<float>.Rent())
                        {
                            short* sourceBuf = (short*)buffer.ToPointer();
                            int maxReadLengthSamplesPerChannel = int16Buf.Length / OutputFormat.NumChannels;
                            while (samplesPerChannelReadFromInput < samplesPerChannelAvailable)
                            {
                                int thisReadLengthSamplesPerChannel = Math.Min(maxReadLengthSamplesPerChannel, samplesPerChannelAvailable - samplesPerChannelReadFromInput);
                                int thisReadLengthSamples = OutputFormat.NumChannels * thisReadLengthSamplesPerChannel;
                                for (int c = 0; c < thisReadLengthSamples; c++)
                                {
                                    int16Buf.Buffer[c] = sourceBuf[c];
                                }

                                AudioMath.ConvertSamples_Int16ToFloat(int16Buf.Buffer, 0, floatBuf.Buffer, 0, thisReadLengthSamples);

                                OutputGraph.LockGraph();
                                try
                                {
                                    target.WriteAsync(
                                        floatBuf.Buffer,
                                        0,
                                        thisReadLengthSamplesPerChannel,
                                        CancellationToken.None,
                                        DefaultRealTimeProvider.Singleton).Await();
                                }
                                catch (Exception e)
                                {
                                    _logger.Log(e, LogLevel.Err);
                                }
                                finally
                                {
                                    OutputGraph.UnlockGraph();
                                }

                                sourceBuf += thisReadLengthSamples;
                                samplesPerChannelReadFromInput += thisReadLengthSamplesPerChannel;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.Log(e, LogLevel.Err);
                }
            }

            return true;
        }

        /// <summary>
        /// Turns the microphone on
        /// </summary>
        public Task StartCapture(IRealTimeProvider realTime)
        {
            if (_hRecord != NULL_DEVICE || !_bassInitialized)
            {
                return DurandalTaskExtensions.NoOpTask;
            }

            BassFlags flags = BassFlags.Default;
            if (_useFloatingPoint)
            {
                flags |= BassFlags.Float;
            }

            _hRecord = Bass.RecordStart(OutputFormat.SampleRateHz, OutputFormat.NumChannels, flags, AudioDataAvailableCallback);
            
            // Clear the Bass recording buffer
            // int bytesAvailable = Bass.ChannelGetData(_hRecord, IntPtr.Zero, (int)DataFlags.Available);
            // Bass.ChannelGetData(_hRecord, IntPtr.Zero, bytesAvailable);

            if (_hRecord == NULL_DEVICE)
            {
                _logger.Log("Failed to start recording; error = " + Bass.LastError.ToString(), LogLevel.Err);
            }

            return DurandalTaskExtensions.NoOpTask;
        }

        /// <summary>
        /// Turns the microphone off
        /// </summary>
        public Task StopCapture()
        {
            if (_hRecord != NULL_DEVICE)
            {
                Bass.ChannelStop(_hRecord);
                _hRecord = NULL_DEVICE;
            }

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
                if (disposing)
                {
                }

                if (_bassInitialized)
                {
                    if (_hRecord != NULL_DEVICE)
                    {
                        Bass.ChannelStop(_hRecord);
                        _hRecord = NULL_DEVICE;
                    }

                    Bass.RecordFree();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        protected override ValueTask<int> ReadAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            throw new NotImplementedException("Cannot read from an BassMicrophone; it is a push component in the graph");
        }
    }
}