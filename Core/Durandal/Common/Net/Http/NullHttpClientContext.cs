using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net.Http
{
    public class NullHttpClientContext : IHttpClientContext
    {
        private NullHttpClientContext() { }

        public static readonly NullHttpClientContext Singleton = new NullHttpClientContext();

        /// <inheritdoc/>
        public HttpVersion ProtocolVersion => HttpVersion.HTTP_1_1;

        /// <inheritdoc/>
        public Task FinishAsync(HttpResponse sourceResponse, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return DurandalTaskExtensions.NoOpTask;
        }
    }
}
