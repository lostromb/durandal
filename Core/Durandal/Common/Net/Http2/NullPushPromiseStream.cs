using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.Net.Http;
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
    /// <summary>
    /// Push promise stream to use when the remote peer doesn't support push or something.
    /// </summary>
    internal class NullPushPromiseStream : IPushPromiseStream
    {
        public string PromisedRequestUrl => null;
        public string PromisedRequestMethod => null;

        public Task WritePromiseResponse(
             IHttpHeaders responseHeaders,
             int responseCode,
             NonRealTimeStream contentStream,
             ILogger traceLogger,
             CancellationToken cancelToken,
             IRealTimeProvider realTime)
        {
            contentStream?.Dispose();
            return DurandalTaskExtensions.NoOpTask;
        }
    }
}
