using Durandal.Common.Net.Http;
using Durandal.Common.Tasks;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.Http2.Session
{
    internal class SendResponseTrailerBlockCommand : ISessionCommand
    {
        public SendResponseTrailerBlockCommand(
            HttpHeaders trailers,
            int streamId)
        {
            Trailers = trailers;
            StreamId = streamId;
        }

        public HttpHeaders Trailers { get; private set; }
        public int StreamId { get; private set; }
    }
}
