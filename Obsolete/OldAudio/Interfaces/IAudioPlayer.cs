using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Durandal.Common.Audio.Interfaces
{
    /// <summary>
    /// Represents a device that can play audio samples + streams on a speaker of some sort
    /// </summary>
    public interface IAudioPlayer : IDisposable
    {
        /// <summary>
        /// Begins playing the given audio sample
        /// </summary>
        /// <param name="chunk">The sample to play</param>
        /// <param name="channelToken">A token that identifies the sample, if you want to be informed later on of when it stops playing</param>
        void PlaySound(AudioChunk chunk, object channelToken = null);

        /// <summary>
        /// Begins playing the given audio stream
        /// </summary>
        /// <param name="stream">The stream to play</param>
        /// <param name="channelToken">A token that identifies the stream, if you want to be informed later on of when it stops playing</param>
        void PlayStream(ChunkedAudioStream stream, object channelToken = null);

        /// <summary>
        /// Indicates whether any samples or streams are currently playing
        /// </summary>
        /// <returns></returns>
        bool IsPlaying();

        /// <summary>
        /// Stops all currently playing samples and streams
        /// </summary>
        void StopPlaying();

        /// <summary>
        /// Raised whenever an audio sample or stream has finished playing, whose identity is known by a channel token returned in the arguments
        /// </summary>
        AsyncEvent<ChannelFinishedEventArgs> ChannelFinishedEvent { get; }
    }
}
