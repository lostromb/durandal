using Durandal.Common.Audio.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Audio;
using Windows.Media.Audio;
using Windows.Media.MediaProperties;
using Durandal.Common.Utils.Tasks;
using Windows.Media;
using Windows.Foundation;
using Windows.Media.Capture;
using Windows.Media.Render;
using Windows.Devices.Enumeration;
using Durandal.Common.Logger;
using System.IO;
using Durandal.Common.Utils.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Durandal.Common.Utils.Time;

namespace DurandalClientWin10.Audio
{
    public class UWPMicrophone : IMicrophone
    {
        private readonly ILogger _logger;
        private readonly float _preamp;
        private readonly int _softwareSampleRate;
        private readonly int _hardwareSampleRate;
        private readonly BasicBufferShort _rawMicrophoneBuffer;
        
        private AudioGraph _graph;
        private AudioDeviceInputNode _micNode;
        private AudioFrameOutputNode _readNode;
        private bool _recording;
        private int _disposed;

        public UWPMicrophone(ILogger logger, int softwareSampleRate = AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE, float preamp = 1.0f)
        {
            _logger = logger;
            _preamp = preamp;
            _softwareSampleRate = softwareSampleRate;
            _hardwareSampleRate = softwareSampleRate;
            _rawMicrophoneBuffer = new BasicBufferShort(_softwareSampleRate * 2); // Buffer up to 2 seconds of audio
        }

        ~UWPMicrophone()
        {
            Dispose(false);
        }

        public bool IsRecording
        {
            get
            {
                return _recording;
            }
        }

        public int SampleRate
        {
            get
            {
                return _softwareSampleRate;
            }
        }

        public void ClearBuffers()
        {
            _graph.ResetAllNodes();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            {
                return;
            }

            if (!disposing) Durandal.Common.Utils.DebugMemoryLeaktracer.TraceDisposableItemFinalized(this.GetType());

            if (disposing)
            {
                if (_graph != null)
                {
                    _graph.Dispose();
                }
            }
        }

        public async Task<AudioChunk> ReadMicrophone(TimeSpan desiredLength, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                return null;
            }

            int requestedSamples = (int)(desiredLength.TotalMilliseconds * _softwareSampleRate / 1000);

            // Wait until the buffer has enough bytes available
            while (_rawMicrophoneBuffer.Available < requestedSamples)
            {
                await realTime.WaitAsync(TimeSpan.FromMilliseconds(1), CancellationToken.None);
            }

            short[] microphoneData = _rawMicrophoneBuffer.Read(requestedSamples);
            return new AudioChunk(microphoneData, _softwareSampleRate);
        }

        public async Task Resume()
        {
            await StartRecording();
        }

        public async Task StartRecording()
        {
            try
            {
                // Dispose of old graph if needed
                if (_graph != null)
                {
                    _graph.Dispose();
                }

                AudioEncodingProperties encodingProperties = AudioEncodingProperties.CreatePcm((uint)_hardwareSampleRate, 1, 16);

                _logger.Log("Initializing UWP audio input graph");
                var graphCreateResult = await AudioGraph.CreateAsync(new AudioGraphSettings(AudioRenderCategory.Speech));
                _graph = graphCreateResult.Graph;
                var micNode = await _graph.CreateDeviceInputNodeAsync(MediaCategory.Media);
                _micNode = micNode.DeviceInputNode;
                _readNode = _graph.CreateFrameOutputNode(encodingProperties);
                _micNode.AddOutgoingConnection(_readNode, (double)_preamp);
                _graph.QuantumStarted += AudioReceived;
                _graph.Start();
                _recording = true;
                _logger.Log("Microphone initialized successfully");
            }
            catch (Exception e)
            {
                _logger.Log(e, LogLevel.Err);
            }
        }

        public Task StopRecording()
        {
            _recording = false;
            _graph.Stop();
            return DurandalTaskExtensions.NoOpTask;
        }

        public Task Suspend()
        {
            return StopRecording();
        }

        private void AudioReceived(AudioGraph sender, object result)
        {
            AudioFrame frame = _readNode.GetFrame();
            ReadAudioFromDeviceBufferUnsafe(frame);
        }

        //private void ReadAudioFromDeviceBuffer(AudioFrame frame)
        //{
        //    // FIXME This is really wasteful but I don't know a better way to avoid all these copies!
        //    using (AudioBuffer buf = frame.LockBuffer(AudioBufferAccessMode.Read))
        //    {
        //        uint bytesAvailable = buf.Length;
        //        if (bytesAvailable > 0)
        //        {
        //            byte[] bytes = new byte[bytesAvailable];
        //            Windows.Storage.Streams.Buffer copiedData = Windows.Storage.Streams.Buffer.CreateCopyFromMemoryBuffer(buf);
        //            byte[] array = System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.ToArray(copiedData);
        //            short[] samples = AudioMath.BytesToShorts(array);
        //            _rawMicrophoneBuffer.Write(samples);
        //        }

        //        buf.Dispose();
        //    }
        //}

        private unsafe void ReadAudioFromDeviceBufferUnsafe(AudioFrame frame)
        {
            using (AudioBuffer buffer = frame.LockBuffer(AudioBufferAccessMode.Read))
            {
                using (IMemoryBufferReference reference = buffer.CreateReference())
                {
                    byte* dataInBytes;
                    uint capacityInBytes;
                    float* dataInFloat;

                    // Get the buffer from the AudioFrame
                    ((IMemoryBufferByteAccess)reference).GetBuffer(out dataInBytes, out capacityInBytes);

                    if (capacityInBytes > 0)
                    {
                        dataInFloat = (float*)dataInBytes;
                        int numSamples = (int)(capacityInBytes / sizeof(float));
                        short[] samples = new short[numSamples];
                        for (int c = 0; c < numSamples; c++)
                        {
                            float sample = dataInFloat[c] * short.MaxValue;
                            // opt: clamping here is probably unnecessary
                            if (sample > short.MaxValue)
                                sample = short.MaxValue;
                            if (sample < short.MinValue)
                                sample = short.MinValue;
                            samples[c] = (short)(sample);
                        }

                        _rawMicrophoneBuffer.Write(samples);
                    }
                }
            }
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
