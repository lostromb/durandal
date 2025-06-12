using Durandal.Common.Net.Http2.Frames;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.Http2.Session
{
    internal class ResetStreamCommand : ISessionCommand
    {
        public ResetStreamCommand(int streamId, Http2ErrorCode reason = Http2ErrorCode.NoError)
        {
            StreamId = streamId;
            Reason = reason;
        }

        public int StreamId { get; private set; }
        public Http2ErrorCode Reason { get; private set; }
    }
}
