using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.Http2
{
    public static class Http2Constants
    {
        /// <summary>
        /// The byte sequence for the HTTP/2 connection preface: PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n
        /// </summary>
        public static readonly byte[] HTTP2_CONNECTION_PREFACE = BinaryHelpers.FromHexString("505249202a20485454502f322e300d0a0d0a534d0d0a0d0a");

        public const int DEFAULT_HEADER_TABLE_SIZE = 4096;
        public const int DEFAULT_MAX_CONCURRENT_STREAMS = int.MaxValue;
        public const int DEFAULT_INITIAL_WINDOW_SIZE = 65535;
        public const int MAX_INITIAL_WINDOW_SIZE = int.MaxValue;
        public const int DEFAULT_MAX_FRAME_SIZE = 16384;
        public const int MAX_MAX_FRAME_SIZE = 16777215;
        public const int DEFAULT_MAX_HEADER_LIST_SIZE = int.MaxValue;

        public static readonly string PSEUDOHEADER_METHOD = ":method";
        public static readonly string PSEUDOHEADER_SCHEME = ":scheme";
        public static readonly string PSEUDOHEADER_AUTHORITY = ":authority";
        public static readonly string PSEUDOHEADER_PATH = ":path";
        public static readonly string PSEUDOHEADER_PROTOCOL = ":protocol"; // defined in websocket extension RFC 8441
        public static readonly string PSEUDOHEADER_STATUS_CODE = ":status";

        /// <summary>
        /// Default H2 settings encoded to a base64url string, intended for use with the HTTP2-Settings upgrade header.
        /// </summary>
        public static readonly string DEFAULT_CLIENT_SETTINGS_BASE64_STRING = "AAEAABAAAAIAAAABAAN_____AAQAAP__AAUAAEAAAAZ_____";

        /// <summary>
        /// State machine for HTTP2 stream state transitions
        /// </summary>
        public static readonly StateMachine<StreamState> STREAM_STATE_MACHINE = new StateMachine<StreamState>(
            new Dictionary<StreamState, StreamState[]>()
            {
                { StreamState.Idle, new StreamState[] { StreamState.ReservedLocal, StreamState.ReservedRemote, StreamState.Open } },
                { StreamState.ReservedLocal, new StreamState[] { StreamState.HalfClosedRemote, StreamState.Closed } },
                { StreamState.ReservedRemote, new StreamState[] { StreamState.HalfClosedLocal, StreamState.Closed } },
                { StreamState.Open, new StreamState[] { StreamState.HalfClosedRemote, StreamState.HalfClosedLocal, StreamState.Closed } },
                { StreamState.HalfClosedRemote, new StreamState[] { StreamState.Closed } },
                { StreamState.HalfClosedLocal, new StreamState[] { StreamState.Closed } },
            });
    }
}
