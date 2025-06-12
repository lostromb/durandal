using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net.Http
{
    public interface IPushPromiseStream
    {
        string PromisedRequestUrl { get; }
        string PromisedRequestMethod { get; }

        Task WritePromiseResponse(
            IHttpHeaders responseHeaders,
            int responseCode,
            NonRealTimeStream contentStream,
            ILogger traceLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime);
    }
}
