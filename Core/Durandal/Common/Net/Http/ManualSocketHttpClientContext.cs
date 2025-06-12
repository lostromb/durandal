using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net.Http
{
    /// <summary>
    /// This is intended for very specific cases (so far, only WebSocket client connections) which want to have very granular
    /// control over the lifetime of a socket that is tied with an HttpResponse.
    /// </summary>
    public class ManualSocketHttpClientContext : IHttpClientContext
    {
        public ManualSocketHttpClientContext(HttpVersion protocolVersion)
        {
            ProtocolVersion = protocolVersion;
        }

        /// <inheritdoc/>
        public HttpVersion ProtocolVersion { get; private set; }

        /// <inheritdoc/>
        public Task FinishAsync(HttpResponse sourceResponse, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return DurandalTaskExtensions.NoOpTask;
        }
    }
}
