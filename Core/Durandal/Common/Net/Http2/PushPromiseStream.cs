using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.Net.Http;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net.Http2
{
    internal class PushPromiseStream : IPushPromiseStream
    {
        private readonly WeakPointer<Http2Session> _session;
        private readonly Http2Stream _promiseStream;

        public PushPromiseStream(WeakPointer<Http2Session> session, Http2Stream promiseStream, string promisedRequestUrl, string promisedRequestMethod)
        {
            _session = session.AssertNonNull(nameof(session));
            _promiseStream = promiseStream.AssertNonNull(nameof(promiseStream));
            PromisedRequestUrl = promisedRequestUrl.AssertNonNullOrEmpty(nameof(promisedRequestUrl));
            PromisedRequestMethod = promisedRequestMethod.AssertNonNullOrEmpty(nameof(promisedRequestMethod));
        }

        public string PromisedRequestUrl { get; private set; }
        public string PromisedRequestMethod { get; private set; }

        public Task WritePromiseResponse(
             IHttpHeaders responseHeaders,
             int responseCode,
             NonRealTimeStream contentStream,
             ILogger traceLogger,
             CancellationToken cancelToken,
             IRealTimeProvider realTime)
        {
            return _session.Value.WriteOutgoingPushPromise(
                responseHeaders,
                responseCode,
                _promiseStream, 
                contentStream,
                traceLogger,
                cancelToken,
                realTime);
        }
    }
}
