using System;

namespace Durandal.Common.Net.Http2
{
    /// <summary>
    /// Structure which holds HTTP2 connection settings, meaning the actual settings which are sent over the wire and negotiated as part of the protocol.
    /// Additional internal configuration values which fall outside of these settings may be found in <see cref="Http2SessionPreferences"/>.
    /// </summary>
    public class Http2Settings : IEquatable<Http2Settings>
    {
        /// <summary>
        /// The default settings.
        /// </summary>
        /// <returns>A new Http2Settings object</returns>
        public static Http2Settings Default()
        {
            return new Http2Settings(
                headerTableSize: Http2Constants.DEFAULT_HEADER_TABLE_SIZE,
                enablePush: true,
                maxConcurrentStreams: Http2Constants.DEFAULT_MAX_CONCURRENT_STREAMS,
                initialWindowSize: Http2Constants.DEFAULT_INITIAL_WINDOW_SIZE,
                maxFrameSize: Http2Constants.DEFAULT_MAX_FRAME_SIZE,
                maxHeaderListSize: Http2Constants.DEFAULT_MAX_HEADER_LIST_SIZE,
                enableConnectProtocol: false);
        }

        /// <summary>
        /// The default settings except ENABLE_PUSH is 0, which is what servers must specify.
        /// </summary>
        /// <returns>A new Http2Settings object</returns>
        public static Http2Settings ServerDefault()
        {
            return new Http2Settings(
                headerTableSize: Http2Constants.DEFAULT_HEADER_TABLE_SIZE,
                enablePush: false,
                maxConcurrentStreams: Http2Constants.DEFAULT_MAX_CONCURRENT_STREAMS,
                initialWindowSize: Http2Constants.DEFAULT_INITIAL_WINDOW_SIZE,
                maxFrameSize: Http2Constants.DEFAULT_MAX_FRAME_SIZE,
                maxHeaderListSize: Http2Constants.DEFAULT_MAX_HEADER_LIST_SIZE,
                enableConnectProtocol: false);
        }

        /// <summary>
        /// Constructs a new <see cref="Http2Settings"/> object with the default settings.
        /// </summary>
        public Http2Settings(
            int headerTableSize,
            bool enablePush,
            int maxConcurrentStreams,
            int initialWindowSize,
            int maxFrameSize,
            int maxHeaderListSize,
            bool enableConnectProtocol)
        {
            HeaderTableSize = headerTableSize;
            EnablePush = enablePush;
            MaxConcurrentStreams = maxConcurrentStreams;
            InitialWindowSize = initialWindowSize;
            MaxFrameSize = maxFrameSize;
            MaxHeaderListSize = maxHeaderListSize;
            EnableConnectProtocol = enableConnectProtocol;
        }

        /// <summary>
        /// The maximum size of the header compression
        /// table used to decode header blocks, in octets. The encoder can select any size equal to or
        /// less than this value by using signaling specific to the header compression format inside
        /// a header block. The initial value is 4,096 octets.
        /// This value is set by the DECODER on the SENDER and acknowledged by the ENCODER on the RECEIVER.
        /// </summary>
        public int HeaderTableSize { get; set; }

        /// <summary>
        /// This setting can be used to enable or disable server push. This capability is determined by clients
        /// only. That is to say, a server must always specify "false" for this value, and clients can report
        /// either true or false depending on whether they want to allow incoming pushes.
        /// An endpoint MUST NOT send a PUSH_PROMISE  frame if this value is false. An endpoint that has both set this parameter to false and had it
        /// acknowledged MUST treat the receipt of a PUSH_PROMISE frame as a connection error of type PROTOCOL_ERROR.
        /// </summary>
        public bool EnablePush { get; set; }

        /// <summary>
        /// Indicates the maximum number of concurrent streams that the sender will allow. This limit is
        /// directional: it applies to the number of streams that the sender permits the receiver to
        /// create. Initially, there is no limit to this value. It is recommended that this value be no
        /// smaller than 100, so as to not unnecessarily limit parallelism.
        /// 
        /// A value of 0 for SETTINGS_MAX_CONCURRENT_STREAMS SHOULD NOT be treated as special by endpoints.
        /// A zero value does prevent the creation of new streams; however, this can also happen for any
        /// limit that is exhausted with active streams. Servers SHOULD only set a zero value for short
        /// durations; if a server does not wish to accept requests, closing the connection is more appropriate.
        /// </summary>
        public int MaxConcurrentStreams { get; set; }

        /// <summary>
        /// Indicates the sender's initial window size (in octets) for stream-level flow control.
        /// The window size is a measure of how much data the peer is willing to receive on a given stream.
        /// The initial value is 2^16 - 1 (65,535) octets.
        /// This setting affects the window size of all streams.
        /// Values above the maximum flow-control window size of 2^31 - 1 MUST be treated as a
        /// connection error of type FLOW_CONTROL_ERROR.
        /// 
        /// Since the receiver of a data stream is the controller of the flow control window,
        /// this frame size should be applied for any new stream which is being sent to an endpoint
        /// which advertises these settings. "Sender" in the RFC spec refers to the sender of the settings frame,
        /// not the sender of the data stream.
        /// </summary>
        public int InitialWindowSize { get; set; }

        /// <summary>
        /// Indicates the size of the largest frame payload that the sender is willing to receive, in octets.
        /// The initial value is 2^14 (16,384) octets. The value advertised by an endpoint MUST be between
        /// this initial value and the maximum allowed frame size (2^24 - 1 or 16,777,215 octets), inclusive.
        /// Values outside this range MUST be treated as a connection error of type PROTOCOL_ERROR.
        /// </summary>
        public int MaxFrameSize { get; set; }

        /// <summary>
        /// This advisory setting informs a peer of the maximum size of header list that the sender is prepared
        /// to accept, in octets. The value is based on the uncompressed size of header fields, including the
        /// length of the name and value in octets plus an overhead of 32 octets for each header field.
        /// For any given request, a lower limit than what is advertised MAY be enforced.
        /// The initial value of this setting is unlimited.
        /// </summary>
        public int MaxHeaderListSize { get; set; }

        /// <summary>
        /// (Extension) Upon receipt of SETTINGS_ENABLE_CONNECT_PROTOCOL with a value of 1, a
        /// client MAY use the Extended CONNECT as defined in RFC 8441 when
        /// creating new streams. Receipt of this parameter by a server does not
        /// have any impact.
        /// </summary>
        public bool EnableConnectProtocol { get; set; }

        /// <summary>
        /// Returns true if all settings are within the acceptable range.
        /// </summary>
        public bool Valid
        {
            get
            {
                return MaxFrameSize >= Http2Constants.DEFAULT_MAX_FRAME_SIZE &&
                    MaxFrameSize <= Http2Constants.MAX_MAX_FRAME_SIZE &&
                    InitialWindowSize > 0;
            }
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            return Equals((Http2Settings)obj);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return
                EnablePush.GetHashCode() ^
                (1954 * HeaderTableSize.GetHashCode()) ^
                (1044 * InitialWindowSize.GetHashCode()) ^
                (7693 * MaxConcurrentStreams.GetHashCode()) ^
                (8301 * MaxFrameSize.GetHashCode()) ^
                (2755 * MaxHeaderListSize.GetHashCode()) ^
                (8456 * EnableConnectProtocol.GetHashCode());
        }

        /// <inheritdoc />
        public bool Equals(Http2Settings other)
        {
            return other != null &&
                EnablePush == other.EnablePush &&
                HeaderTableSize == other.HeaderTableSize &&
                InitialWindowSize == other.InitialWindowSize &&
                MaxConcurrentStreams == other.MaxConcurrentStreams &&
                MaxFrameSize == other.MaxFrameSize &&
                MaxHeaderListSize == other.MaxHeaderListSize &&
                EnableConnectProtocol == other.EnableConnectProtocol;
        }
    }
}
