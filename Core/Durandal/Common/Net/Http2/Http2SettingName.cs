﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.Http2
{
    /// <summary>
    /// Listing of supported HTTP setting codes.
    /// </summary>
    public enum Http2SettingName
    {
        Unknown = 0x0,

        /// <summary>
        /// Allows the sender to inform the remote endpoint of the maximum size of the header compression
        /// table used to decode header blocks, in octets. The encoder can select any size equal to or
        /// less than this value by using signaling specific to the header compression format inside
        /// a header block. The initial value is 4,096 octets.
        /// </summary>
        HeaderTableSize = 0x1,

        /// <summary>
        /// This setting can be used to disable server push. An endpoint MUST NOT send a PUSH_PROMISE
        /// frame if it receives this parameter set to a value of 0. An endpoint that has both set this
        /// parameter to 0 and had it acknowledged MUST treat the receipt of a PUSH_PROMISE frame as a
        /// connection error of type PROTOCOL_ERROR.
        /// </summary>
        EnablePush = 0x2,

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
        MaxConcurrentStreams = 0x3,

        /// <summary>
        /// Indicates the sender's initial window size (in octets) for stream-level flow control.
        /// The initial value is 2^16 - 1 (65,535) octets.
        /// This setting affects the window size of all streams.
        /// Values above the maximum flow-control window size of 2^31 - 1 MUST be treated as a
        /// connection error of type FLOW_CONTROL_ERROR.
        /// </summary>
        InitialWindowSize = 0x4,

        /// <summary>
        /// Indicates the size of the largest frame payload that the sender is willing to receive, in octets.
        /// The initial value is 2^14 (16,384) octets. The value advertised by an endpoint MUST be between
        /// this initial value and the maximum allowed frame size (2^24 - 1 or 16,777,215 octets), inclusive.
        /// Values outside this range MUST be treated as a connection error of type PROTOCOL_ERROR.
        /// </summary>
        MaxFrameSize = 0x5,

        /// <summary>
        /// This advisory setting informs a peer of the maximum size of header list that the sender is prepared
        /// to accept, in octets. The value is based on the uncompressed size of header fields, including the
        /// length of the name and value in octets plus an overhead of 32 octets for each header field.
        /// For any given request, a lower limit than what is advertised MAY be enforced.
        /// The initial value of this setting is unlimited.
        /// </summary>
        MaxHeaderListSize = 0x6,

        /// <summary>
        /// (Extension) Upon receipt of SETTINGS_ENABLE_CONNECT_PROTOCOL with a value of 1, a
        /// client MAY use the Extended CONNECT as defined in RFC 8441 when
        /// creating new streams. Receipt of this parameter by a server does not
        /// have any impact.
        /// </summary>
        EnableConnectProtocol = 0x8,
    }
}
