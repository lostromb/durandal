using Durandal.Common.Net.Http;
using Durandal.Common.Tasks;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.Http2.Session
{
    internal class SendPushPromiseFrameCommand : ISessionCommand
    {
        public SendPushPromiseFrameCommand(
            HttpHeaders requestPatternHeaders,
            string requestPath,
            string requestMethod,
            int primaryStreamId,
            int reservedStreamId)
        {
            RequestPatternHeaders = requestPatternHeaders;
            RequestPath = requestPath;
            RequestMethod = requestMethod;
            PrimaryStreamId = primaryStreamId;
            ReservedStreamId = reservedStreamId;
        }

        public HttpHeaders RequestPatternHeaders { get; private set; }
        public string RequestPath { get; set; }
        public string RequestMethod { get; set; }
        public int PrimaryStreamId { get; private set; }
        public int ReservedStreamId { get; private set; }
    }
}
