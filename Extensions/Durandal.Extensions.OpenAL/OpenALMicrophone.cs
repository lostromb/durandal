using System;
using System.Threading;
using Durandal.Common.Audio;
using System.Diagnostics;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Durandal.Common.Time;
using Durandal.Common.Tasks;
using Durandal.Common.IO;
using Durandal.API;
using Durandal.Common.Audio.Hardware;
using Durandal.Common.MathExt;
using OpenTK.Audio.OpenAL;
using Durandal.Common.ServiceMgmt;
using System.IO;

namespace Durandal.Extensions.OpenAL
{
    /// <summary>
    /// A simple microphone class which uses the OpenAL backend.
    /// </summary>
    internal class OpenALMicrophone : AbstractAudioSampleSource, IAudioCaptureDevice
    {
        private static readonly TimeSpan CAPTURE_BUFFER_SIZE = TimeSpan.FromMilliseconds(50);

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
        private readonly ALCaptureDevice _device;

        /// <summary>
        /// The AL sample format of the data
        /// </summary>
        private readonly ALFormat _inputFormat;

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
        private readonly AutoResetEventAsync _recordingStartedSignal = new AutoResetEventAsync();

        /// <summary>
        /// Whether or not recording is active
        /// </summary>
        private bool _recording = false;

        private int _disposed = 0;

        /// <summary>
        /// Constructs a new <see cref="OpenALMicrophone"/> for audio input.
        /// </summary>
        /// <param name="audioGraph">The graph to associated with this component.</param>
        /// <param name="hardwareFormat">The format to initialize the hardware device with.</param>
        /// <param name="nodeCustomName">The name of this node in the audio graph, can be null</param>
        /// <param name="logger">A logger.</param>
        /// <param name="desiredDeviceName">The name of the capture device to use, as defined by OpenAL, or null to use the default device.</param>
        public OpenALMicrophone(
            WeakPointer<IAudioGraph> audioGraph,
            AudioSampleFormat hardwareFormat,
            string nodeCustomName,
            ILogger logger,
            WeakPointer<IHighPrecisionWaitProvider> mediaThreadTimer,
            string desiredDeviceName = null)
            : base(audioGraph, nameof(OpenALMicrophone), nodeCustomName)
        {
            _logger = logger;
            OutputFormat = hardwareFormat.AssertNonNull(nameof(hardwareFormat));
            _highPrecisionWaitProvider = mediaThreadTimer.AssertNonNull(nameof(mediaThreadTimer));

            if (hardwareFormat.NumChannels > 2)
            {
                throw new ArgumentException("OpenAL input currently cannot process more than 2 channels at once");
            }
            if (hardwareFormat.ChannelMapping == MultiChannelMapping.Stereo_R_L)
            {
                throw new ArgumentException("Stereo audio in OpenAL must be L-R");
            }

            try
            {
                if (!ALC.IsExtensionPresent(ALDevice.Null, "ALC_EXT_CAPTURE"))
                {
                    throw new PlatformNotSupportedException("The current OpenAL driver does not support capture extension");
                }

                _logger.Log("Initializing OpenAL audio input...");

                // Do we support float32 extension?
                // BUGBUG device is null device here
                _supportsFloat = ALC.IsExtensionPresent(_device, AL.EXTFloat32.ExtensionName /*"AL_EXT_float32"*/);
                
                _inputFormat = GetALFormat(hardwareFormat, _supportsFloat);

                int captureBufferSizeTotalSamples =
                    (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(hardwareFormat.SampleRateHz, CAPTURE_BUFFER_SIZE)
                        * hardwareFormat.NumChannels;
                captureBufferSizeTotalSamples = FastMath.RoundUpToPowerOf2(captureBufferSizeTotalSamples);

                ThrowOpenALErrors();

                if (string.IsNullOrWhiteSpace(desiredDeviceName))
                {
                    desiredDeviceName = null;
                    _logger.Log("Opening default OpenAL input device");
                }
                else
                {
                    _logger.LogFormat(
                        LogLevel.Std,
                        DataPrivacyClassification.SystemMetadata,
                        "Opening OpenAL input device {0}", desiredDeviceName);
                }

                _device = ALC.CaptureOpenDevice(desiredDeviceName, hardwareFormat.SampleRateHz, _inputFormat, captureBufferSizeTotalSamples);

                ThrowOpenALErrors();
                if (_device == ALCaptureDevice.Null)
                {
                    throw new Exception("Failed to open a valid OpenAL input device");
                }

                _logger.Log("OpenAL audio input initialized");
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
                _logger.Log("FAILED to load OpenAL shared library. Required DLLs were not found.", LogLevel.Err);
                _logger.Log(e, LogLevel.Err);
            }
        }

        ~OpenALMicrophone()
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

            ALC.CaptureStart(_device);
            _recording = true;
            _recordingStartedSignal.Set();

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

            ALC.CaptureStop(_device);
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
                _shutdownSignal.Cancel();

                if (_device != ALCaptureDevice.Null)
                {
                    ALC.CaptureCloseDevice(_device);
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

        protected override ValueTask<int> ReadAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            throw new NotImplementedException("Cannot read from an OpenALMicrophone; it is a push component in the graph");
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

            try
            {
                while (!shutdownToken.IsCancellationRequested)
                {
                    if (_recording)
                    {
                        // Get number of samples available
                        int numSamplesPerChannel;
                        ALC.GetInteger(_device, AlcGetInteger.CaptureSamples, 1, out numSamplesPerChannel);
                        ThrowOpenALErrors();

                        if (numSamplesPerChannel > 0)
                        {
                            int totalSamplesAvailable = numSamplesPerChannel * OutputFormat.NumChannels;
                            using (PooledBuffer<float> floatScratch = BufferPool<float>.Rent(totalSamplesAvailable))
                            {
                                // Read them from device
                                if (_supportsFloat)
                                {
                                    // float path is untested - don't know if float32 extension on capture devices is supported by creative driver
                                    ALC.CaptureSamples(_device, floatScratch.Buffer, numSamplesPerChannel);
                                }
                                else
                                {
                                    // intermediate conversion from pcm16 as needed
                                    using (PooledBuffer<short> conversionScratch = BufferPool<short>.Rent(totalSamplesAvailable))
                                    {
                                        ALC.CaptureSamples(_device, conversionScratch.Buffer, numSamplesPerChannel);
                                        AudioMath.ConvertSamples_Int16ToFloat(conversionScratch.Buffer, 0, floatScratch.Buffer, 0, totalSamplesAvailable);
                                    }
                                }

                                ThrowOpenALErrors();

                                // Write to audio graph
                                OutputGraph.LockGraph();
                                OutputGraph.BeginInstrumentedScope(DefaultRealTimeProvider.Singleton, NodeFullName);
                                try
                                {
                                    if (Output != null)
                                    {
                                        Output.WriteAsync(floatScratch.Buffer, 0, numSamplesPerChannel, shutdownToken, DefaultRealTimeProvider.Singleton).Await();
                                    }
                                }
                                finally
                                {
                                    OutputGraph.EndInstrumentedScope(DefaultRealTimeProvider.Singleton, AudioMath.ConvertSamplesPerChannelToTimeSpan(OutputFormat.SampleRateHz, numSamplesPerChannel));
                                    OutputGraph.UnlockGraph();
                                }
                            }
                        }

                        // Sleep for 5ms
                        await _highPrecisionWaitProvider.Value.WaitAsync(UPDATE_INTERVAL_MS, shutdownToken).ConfigureAwait(false);
                    }
                    else
                    {
                        // Wait for recording to start again
                        await _recordingStartedSignal.WaitAsync(shutdownToken);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                _logger.Log("Fatal error in OpenAL input driver", LogLevel.Err);
                _logger.Log(e, LogLevel.Err);
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
    }
}