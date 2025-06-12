using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.Http2
{
    public enum Http2FrameType
    {
        Data = 0x0,
        Headers = 0x1,
        Priority = 0x2,
        RstStream = 0x3,
        Settings = 0x4,
        PushPromise = 0x5,
        Ping = 0x6,
        GoAway = 0x7,
        WindowUpdate = 0x8,
        Continuation = 0x9,
    }
}
