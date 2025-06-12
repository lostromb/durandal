using Durandal.Common.Net.Http;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net.Http2
{
    internal class Http2ClientContext : IHttpClientContext
    {
        private readonly WeakPointer<Http2Stream> _stream;
        private readonly WeakPointer<Http2Session> _session;

        public Http2ClientContext(WeakPointer<Http2Stream> innerStream, WeakPointer<Http2Session> session)
        {
            _stream = innerStream.AssertNonNull(nameof(innerStream));
            _session = session.AssertNonNull(nameof(session));
        }

        /// <inheritdoc/>
        public HttpVersion ProtocolVersion => HttpVersion.HTTP_2_0;

        /// <inheritdoc/>
        public Task FinishAsync(HttpResponse sourceResponse, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            // If the stream has not completed reading, tell the session to cancel the incoming read stream.
            _session.Value.FinishIncomingResponseStream(_stream.Value.StreamId, !_stream.Value.ReadStream.EndOfStream);
            return DurandalTaskExtensions.NoOpTask;
        }
    }
}
