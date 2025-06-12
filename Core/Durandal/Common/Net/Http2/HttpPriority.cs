using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.Http2
{
    internal enum HttpPriority
    {
        /// <summary>
        /// Highest priority, usually for GOAWAY or SETTINGS
        /// </summary>
        SessionControl = 0,

        /// <summary>
        /// Stateful transmission units such as HEADERS, CONTINUATION, PUSH_PROMISE
        /// </summary>
        Headers = 1,

        /// <summary>
        /// Responding to pings
        /// </summary>
        Ping = 2,

        /// <summary>
        /// Things like RST_STREAM
        /// </summary>
        StreamControl = 3,

        /// <summary>
        /// Data frames
        /// </summary>
        Data = 4,

        /// <summary>
        /// Everything else
        /// </summary>
        Idle = 5,
    }
}
