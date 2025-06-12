using Durandal.Common.IO;
using Durandal.Common.Net.Http;
using Durandal.Common.Tasks;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.Http2
{
    internal class Http2Stream : IDisposable
    {
        public int StreamId { get; set; }
        public StreamState State { get; set; }
        public HttpHeaders Headers { get; set; }
        public HttpHeaders Trailers { get; set; }
        public PipeStream.PipeReadStream ReadStream { get; private set; }
        public PipeStream.PipeWriteStream WriteStream { get; private set; }
        public DataTransferWindow IncomingFlowControlWindow { get; set; }
        public DataTransferWindow OutgoingFlowControlWindow { get; set; }
        public AutoResetEventAsync ReceievedHeadersSignal { get; set; }
        public AutoResetEventAsync OutgoingFlowControlAvailableSignal { get; set; }

        // Responses and push promises only
        public int? ResponseStatusCode { get; set; }

        // Requests only
        public string RequestPath { get; set; }
        public string RequestMethod { get; set; }
        public string RequestAuthority { get; set; }
        public string RequestScheme { get; set; }

        public Http2Stream()
        {
            using (PipeStream pipe = new PipeStream())
            {
                ReadStream = pipe.GetReadStream();
                WriteStream = pipe.GetWriteStream();
            }
        }

        public void Dispose()
        {

        }
    }
}
