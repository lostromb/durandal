using Durandal.Common.Time;
using System;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Interfaces
{
    /// <summary>
    /// Represents a device that can read audio data from the world and provide it as a stream
    /// </summary>
    public interface IAudioInputDevice : IAudioDevice, IAudioSampleProvider
    {
        /// <summary>
        /// Turns the microphone on
        /// </summary>
        Task StartRecording();

        /// <summary>
        /// Turns the microphone off
        /// </summary>
        Task StopRecording();

        /// <summary>
        /// The on/off state of the microphone
        /// </summary>
        /// <returns></returns>
        bool IsRecording { get; }

        /// <summary>
        /// Clears all intermediate buffers inside the microphone, to get rid
        /// of old samples that occurred 0-3 seconds in the past (depending on the buffer size)
        /// </summary>
        void ClearBuffers();

        /// <summary>
        /// Returns the sample rate, in hz, of this microphone (the software sample rate, not necessarily the underlying hardware rate)
        /// </summary>
        /// <returns>The sample rate</returns>
        int SampleRate { get; }

        /// <summary>
        /// Requests an audio chunk with a specific length from the microphone. The number
        /// of samples that are actually returned will depend on the microphone's sample rate.
        /// If the requested number of samples are not available, this method will block until they are.
        /// </summary>
        /// <param name="desiredLength">The length of the audio to read</param>
        /// <returns>The microphone audio data</returns>
        Task<AudioChunk> ReadMicrophone(TimeSpan desiredLength, IRealTimeProvider realTime);
    }
}
