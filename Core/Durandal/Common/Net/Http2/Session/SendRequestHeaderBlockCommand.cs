using Durandal.Common.Net.Http;
using Durandal.Common.Tasks;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.Http2.Session
{
    internal class SendRequestHeaderBlockCommand : ISessionCommand
    {
        public SendRequestHeaderBlockCommand(
            IHttpHeaders headers,
            string requestPath,
            string requestMethod,
            int reservedStreamId,
            bool noBodyContent,
            Http2Stream parentStream)
        {
            Headers = headers;
            RequestPath = requestPath;
            RequestMethod = requestMethod;
            ReservedStreamId = reservedStreamId;
            NoBodyContent = noBodyContent;
            ParentStream = parentStream;
        }

        public IHttpHeaders Headers { get; private set; }
        public string RequestPath { get; set; }
        public string RequestMethod { get; set; }
        public int ReservedStreamId { get; private set; }
        public bool NoBodyContent { get; private set; }
        public Http2Stream ParentStream { get; private set; }
    }
}
