using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.Http2
{
    /// <summary>
    /// Collection of client settings for a particular HTTP/2 peer in general.
    /// Not to be confused with <see cref="Http2Settings"/>, which is negotiated with the server,
    /// these are more for preferences like timeouts and local session limits.
    /// </summary>
    public class Http2SessionPreferences
    {
        /// <summary>
        /// The desired interval to send keepalive pings to the remote host
        /// </summary>
        public TimeSpan OutgoingPingInterval { get; set; }

        /// <summary>
        /// The maximum amount of time to wait without any active HEADERS / DATA frames before sending a GOAWAY
        /// to gracefully close the connection
        /// </summary>
        public TimeSpan MaxIdleTime { get; set; }

        /// <summary>
        /// Max amount of time to wait for a remote peer to send initial settings before closing the connection.
        /// </summary>
        public TimeSpan SettingsTimeout { get; set; }

        /// <summary>
        /// Max amount of time to hold promised stream data without a request fetching it before removing it from memory.
        /// </summary>
        public TimeSpan PromisedStreamTimeout { get; set; }

        /// <summary>
        /// The maximum number of recently promised streams to store in memory at any time
        /// </summary>
        public int MaxPromisedStreamsToStore { get; set; }

        /// <summary>
        /// The desired number of credits to reserve in the global flow control window
        /// </summary>
        public int DesiredGlobalConnectionFlowWindow { get; set; }

        /// <summary>
        /// The highest stream ID that this peer should be allowed to initiate before gracefully closing the connection
        /// so it can be reset. By default this is int32.MaxValue, but you can set it lower if you want to recycle
        /// connections more often.
        /// </summary>
        public int? MaxStreamId { get; set; }

        /// <summary>
        /// Constructs the default <see cref="Http2SessionPreferences"/>.
        /// </summary>
        public Http2SessionPreferences()
        {
            OutgoingPingInterval = TimeSpan.FromSeconds(30);
            MaxIdleTime = TimeSpan.FromMinutes(5);
            SettingsTimeout = TimeSpan.FromSeconds(5);
            MaxStreamId = null;
            DesiredGlobalConnectionFlowWindow = 12_582_912;
        }
    }
}
