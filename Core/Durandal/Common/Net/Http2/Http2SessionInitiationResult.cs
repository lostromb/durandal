using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.Http2
{
    public class Http2SessionInitiationResult
    {
        public Http2SessionInitiationResult(ISocket socket)
        {
            Socket = socket;
            Session = null;
        }

        public Http2SessionInitiationResult(Http2Session session)
        {
            Socket = null;
            Session = session;
        }

        public Http2Session Session { get; private set; }
        public ISocket Socket { get; private set; }
    }
}
