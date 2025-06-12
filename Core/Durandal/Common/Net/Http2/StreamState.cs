using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.Http2
{
    /// <summary>
    /// Enumeration of stream states.
    /// </summary>
    public enum StreamState
    {
        /// <summary>
        /// Stream state is unknown.
        /// </summary>
        Unknown = 0x0,

        /// <summary>
        /// Default state of a stream before any activity occurs on it
        /// </summary>
        Idle = 0x1,

        /// <summary>
        /// A stream in the "reserved (local)" state is one that has been promised by sending a PUSH_PROMISE frame.
        /// A PUSH_PROMISE frame reserves an idle stream by associating the stream with an
        /// open stream that was initiated by the remote peer.
        /// </summary>
        ReservedLocal = 0x2,

        /// <summary>
        /// A stream in the "reserved (remote)" state has been reserved by a remote peer.
        /// </summary>
        ReservedRemote = 0x3,

        /// <summary>
        /// A stream in the "open" state may be used by both peers to send frames of any type.
        /// In this state, sending peers observe advertised stream-level flow-control limits.
        /// </summary>
        Open = 0x4,

        /// <summary>
        /// A stream that is in the "half-closed (local)" state cannot be used for sending
        /// frames other than WINDOW_UPDATE, PRIORITY, and RST_STREAM.
        /// </summary>
        HalfClosedLocal = 0x5,

        /// <summary>
        /// A stream that is "half-closed (remote)" is no longer being used by the peer to send frames.
        /// In this state, an endpoint is no longer obligated to maintain a receiver flow-control window.
        /// </summary>
        HalfClosedRemote = 0x6,

        /// <summary>
        /// The "closed" state is the terminal state.
        /// An endpoint MUST NOT send frames other than PRIORITY on a closed stream.
        /// </summary>
        Closed = 0x7,
    }
}
