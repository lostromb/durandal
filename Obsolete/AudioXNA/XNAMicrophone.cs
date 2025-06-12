using System;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

namespace Durandal.Common.Audio
{
    /// <summary>
    /// A simple microphone class which uses the XNA backend
    /// </summary>
    public class XnaMicrophone : IMicrophone
    {
        private const int BUFFER_LENGTH_SECONDS = 4;
        
        private readonly int _inputSampleRate;
        private readonly int _outputSampleRate;
        private readonly Microphone _recorder;
        private readonly ConcurrentBuffer _bufferData;
        private readonly float _amplification;
        private readonly EventWaitHandle _bufferDataFlag;
        private Thread _xnaDispatchThread;
        private readonly byte[] _scratchBuffer;

        public XnaMicrophone(int desiredSampleRate = 16000, float amplification = 1.0f)
        {
            _outputSampleRate = desiredSampleRate;
            _amplification = amplification;
            _scratchBuffer = new byte[24000];

            // Since XNA apparently doesn't let us specify an input capture rate, we simply have to resample the output upon retrieval.
            // In this case, use the default microphone
            _recorder =  Microphone.Default;
            _recorder.BufferReady += ProcessAudioInput;

            _inputSampleRate = _recorder.SampleRate;
            _bufferData = new ConcurrentBuffer(_inputSampleRate * BUFFER_LENGTH_SECONDS);

            _bufferDataFlag = new EventWaitHandle(false, EventResetMode.AutoReset);
        }

        /// <summary>
        /// Returns the sample rate, in hz, of the current capture device
        /// </summary>
        /// <returns>The sample rate</returns>
        public int GetSampleRate()
        {
            return _outputSampleRate;
        }

        /// <summary>
        /// Requests a specific number of samples to be read from the microphone.
        /// If the requested number of samples are not available, this method will block until they are.
        /// </summary>
        /// <param name="samplesRequested">The number of samples to retrieve</param>
        /// <returns>The microphone audio data</returns>
        public AudioChunk ReadMicrophone(int samplesRequested)
        {
            if (!IsRecording())
                return null;
            
            // Wait for new audio to be written
            while (_bufferData.Available() < samplesRequested)
            {
                _bufferDataFlag.WaitOne();
            }
            AudioChunk returnedChunk = new AudioChunk(_bufferData.Read(samplesRequested), _inputSampleRate)
                .ResampleTo(_outputSampleRate)
                .Amplify(_amplification);
            return returnedChunk;
        }

        /// <summary>
        /// Requests an audio chunk with a specific length from the microphone. The number
        /// of samples that are actually returned will depend on the microphone's sample rate.
        /// If the requested number of samples are not available, this method will block until they are.
        /// </summary>
        /// <param name="desiredLength">The length of the audio to read</param>
        /// <returns>The microphone audio data</returns>
        public AudioChunk ReadMicrophone(TimeSpan desiredLength)
        {
            int desiredSize = (int)(desiredLength.TotalMilliseconds * _outputSampleRate / 1000);
            return ReadMicrophone(desiredSize);
        }

        /// <summary>
        /// Handle audio input events from the XNA module.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void ProcessAudioInput(object sender, EventArgs args)
        {
            int bytesRead = _recorder.GetData(_scratchBuffer);
            while (bytesRead > 0)
            {
                if (bytesRead < _scratchBuffer.Length)
                {
                    byte[] truncatedTemp = new byte[bytesRead];
                    Array.Copy(_scratchBuffer, truncatedTemp, bytesRead);
                    _bufferData.Write(AudioMath.BytesToShorts(truncatedTemp));
                }
                else
                {
                    _bufferData.Write(AudioMath.BytesToShorts(_scratchBuffer));
                }
                _bufferDataFlag.Set();
                // Do another pass, if possible
                bytesRead = _recorder.GetData(_scratchBuffer);
            }
        }

        /// <summary>
        /// Turns the microphone on
        /// </summary>
        public void StartRecording()
        {
            if (IsRecording())
                return;

            if (_xnaDispatchThread != null)
            {
                _xnaDispatchThread.Abort();
            }
            _xnaDispatchThread = new Thread(() =>
                {
                    try
                    {
                        while (true)
                        {
                            // XNA requires that this method be called frequently, so do it in a dummy thread here
                            FrameworkDispatcher.Update();
                            Thread.Sleep(1);
                        }
                    }
                    catch (ThreadAbortException) {}
                });
            _xnaDispatchThread.Start();
            _recorder.Start();
        }

        /// <summary>
        /// Turns the microphone off
        /// </summary>
        public void StopRecording()
        {
            if (!IsRecording())
                return;

            if (_xnaDispatchThread != null)
            {
                _xnaDispatchThread.Abort();
            }

            _recorder.Stop();
        }

        public bool IsRecording()
        {
            return _recorder.State == MicrophoneState.Started;
        }

        /// <summary>
        /// Clears all intermediate buffers inside the microphone, to get rid
        /// of old samples that occurred 0-3 seconds in the past (depending on the buffer size)
        /// </summary>
        public void ClearBuffers()
        {
            _bufferData.Clear();
        }

        /// <summary>
        /// Implements the IDisposable interface
        /// </summary>
        public void Dispose()
        {
            if (_recorder != null && _recorder.State == MicrophoneState.Started)
                _recorder.Stop();
        }
    }
}
