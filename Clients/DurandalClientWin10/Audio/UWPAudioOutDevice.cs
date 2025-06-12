using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.Audio;
using Durandal.Common.Audio;
using Windows.Media.Render;
using Windows.Media.MediaProperties;
using System.Runtime.InteropServices;
using Windows.Media;
using Windows.Foundation;
using Durandal.Common.Audio.Codecs.Opus.Common;
using System.Threading;
using Windows.Storage;
using System.Diagnostics;
using Durandal.Common.Time;
using Durandal.Common.Tasks;
using Durandal.Common.IO;

using WindowsMedia = Windows.Media.Audio;

namespace DurandalClientWin10.Audio
{
    public class UWPAudioOutDevice : AbstractAudioSampleTarget, IAudioRenderDevice
    {
        // defines the buffer size for the output audio node
        private const int MINIMUM_BUFFERED_SAMPLES = 500;
        private readonly ILogger _logger;

        private WindowsMedia.AudioGraph _uwpGraph;
        private AudioDeviceOutputNode _outNode;
        private AudioFrameInputNode _readNode;
        private AudioEncodingProperties _hardwareProperties;
        private int _disposed = 0;

        public static async Task<UWPAudioOutDevice> Create(ILogger logger)
        {
            UWPAudioOutDevice returnVal = new UWPAudioOutDevice(logger);
            await returnVal.Initialize();
            return returnVal;
        }

        private UWPAudioOutDevice(ILogger logger) : base(null, nameof(UWPAudioOutDevice), null)
        {
            _logger = logger;
        }

        ~UWPAudioOutDevice()
        {
            Dispose(false);
        }

        private async Task Initialize()
        {
            try
            {
                _logger.Log("Initializing UWP audio output graph");
                var graphCreateResult = await WindowsMedia.AudioGraph.CreateAsync(new AudioGraphSettings(AudioRenderCategory.Speech));
                _uwpGraph = graphCreateResult.Graph;
                _hardwareProperties = _uwpGraph.EncodingProperties;
                if (_hardwareProperties.ChannelCount > 2)
                {
                    throw new NotImplementedException("Multichannel audio not supported");
                }

                InputFormat = new AudioSampleFormat((int)_hardwareProperties.SampleRate, (int)_hardwareProperties.ChannelCount, _hardwareProperties.ChannelCount == 1 ? MultiChannelMapping.Monaural : MultiChannelMapping.Stereo_L_R);
                var speakerNode = await _uwpGraph.CreateDeviceOutputNodeAsync();
                _outNode = speakerNode.DeviceOutputNode;
                _readNode = _uwpGraph.CreateFrameInputNode();
                _readNode.AddOutgoingConnection(_outNode);
                _readNode.QuantumStarted += ReadInputData;

                //StorageFolder folder = KnownFolders.MusicLibrary;
                //IStorageFile debugOutFile = folder.CreateFileAsync("audioout.wav").AsTask().Await();
                //var fileOutNode = _uwpGraph.CreateFileOutputNodeAsync(debugOutFile, MediaEncodingProfile.CreateWav(AudioEncodingQuality.High)).AsTask().Await();
                //_readNode.AddOutgoingConnection(fileOutNode.FileOutputNode);

                _uwpGraph.Start();
                _logger.Log("Speakers initialized successfully");
            }
            catch (Exception e)
            {
                _logger.Log(e, LogLevel.Err);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            {
                return;
            }

            Durandal.Common.Utils.DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            try
            {
                if (disposing)
                {
                    _uwpGraph?.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        private void ReadInputData(AudioFrameInputNode sender, FrameInputNodeQuantumStartedEventArgs args)
        {
            if (args.RequiredSamples > 0)
            {
                uint numSamplesNeeded = (uint)(args.RequiredSamples + MINIMUM_BUFFERED_SAMPLES);
                AudioFrame audioData = MixAudioData(numSamplesNeeded, (int)_hardwareProperties.ChannelCount);
                _readNode.AddFrame(audioData);
            }
        }

        private unsafe AudioFrame MixAudioData(uint samplesPerChannel, int outputChannelCount)
        {
            // Buffer size is (number of samples) * (size of each sample) * (num of channels)
            uint bufferSize = samplesPerChannel * (_hardwareProperties.BitsPerSample / 8) * (uint)outputChannelCount;
            AudioFrame frame = new AudioFrame(bufferSize);

            // Read data from the internal mixer
            using (PooledBuffer<float> scratch = BufferPool<float>.Rent((int)samplesPerChannel * outputChannelCount))
            {
                // FIXME this doesn't gracefully handle the case where the sample provider didn't return enough samples
                int samplesPerChannelActuallyRead = Input.ReadAsync(scratch.Buffer, 0, (int)samplesPerChannel, DefaultRealTimeProvider.Singleton, CancellationToken.None).Await();

                // Check for discontinuities
                //float maxDisc = 0;
                //int discIndex = 0;
                //for (int c = 1; c < samples - 1; c++)
                //{
                //    float disc = Math.Abs(mixerOutputMono[c] - ((mixerOutputMono[c + 1] + mixerOutputMono[c - 1]) / 2f));
                //    if (disc > maxDisc)
                //    {
                //        maxDisc = disc;
                //        discIndex = c;
                //    }
                //}

                //if (maxDisc > 0.04f)
                //{
                //    Debugger.Break();
                //}

                // Then finally write it to the low-level buffer
                using (AudioBuffer buffer = frame.LockBuffer(AudioBufferAccessMode.Write))
                using (IMemoryBufferReference reference = buffer.CreateReference())
                {
                    byte* dataInBytes;
                    uint capacityInBytes;
                    float* dataInFloat;

                    // Get the buffer from the AudioFrame
                    ((IMemoryBufferByteAccess)reference).GetBuffer(out dataInBytes, out capacityInBytes);

                    // Cast to float pointer since the data we are generating is float
                    dataInFloat = (float*)dataInBytes;

                    for (int c = 0; c < samplesPerChannelActuallyRead * outputChannelCount; c++)
                    {
                        dataInFloat[c] = scratch.Buffer[c];
                    }
                }

                return frame;
            }
        }

        protected override Task WriteAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            throw new NotImplementedException("Cannot write to an active graph node");
        }

        public Task StartPlayback(IRealTimeProvider timeProvider)
        {
            _outNode.Start();
            return DurandalTaskExtensions.NoOpTask;
        }

        public Task StopPlayback()
        {
            _outNode.Stop();
            return DurandalTaskExtensions.NoOpTask;
        }

        [ComImport]
        [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private unsafe interface IMemoryBufferByteAccess
        {
            void GetBuffer(out byte* buffer, out uint capacity);
        }
    }
}
