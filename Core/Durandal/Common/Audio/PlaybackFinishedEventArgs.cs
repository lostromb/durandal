using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Audio
{
    /// <summary>
    /// Event arguments used when an audio mixer input has finished playback.
    /// </summary>
    public class PlaybackFinishedEventArgs : EventArgs
    {
        /// <summary>
        /// The channel token, which is a user-defined token that identifies the actual stream which finished playback.
        /// </summary>
        public object ChannelToken { get; set; }

        /// <summary>
        /// The local time according to the thread on which the playback finished
        /// </summary>
        public IRealTimeProvider ThreadLocalTime { get; set; }

        /// <summary>
        /// Constructs a new instance of <see cref="PlaybackFinishedEventArgs"/> .
        /// </summary>
        /// <param name="channelToken">The channel token associated with the playback which just finished</param>
        /// <param name="realTime">The thread-local time of the mixer which marked this playback as finished</param>
        public PlaybackFinishedEventArgs(object channelToken, IRealTimeProvider realTime)
        {
            ChannelToken = channelToken;
            ThreadLocalTime = realTime;
        }
    }
}
