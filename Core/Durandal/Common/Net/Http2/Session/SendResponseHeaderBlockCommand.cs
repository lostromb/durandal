using Durandal.Common.Net.Http;
using Durandal.Common.Tasks;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.Http2.Session
{
    internal class SendResponseHeaderBlockCommand : ISessionCommand
    {
        public SendResponseHeaderBlockCommand(
            IHttpHeaders headers,
            int responseCode,
            bool endOfStream,
            int streamId)
        {
            Headers = headers;
            ResponseCode = responseCode;
            EndOfStream = endOfStream;
            StreamId = streamId;
        }

        public IHttpHeaders Headers { get; private set; }
        public int ResponseCode { get; set; }
        public bool EndOfStream { get; private set; }
        public int StreamId { get; private set; }
    }
}
