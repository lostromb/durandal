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
    public class PortableHttpClientContext : IHttpClientContext
    {
        private readonly HttpResponseMessage _innerResponseMessage;

        public PortableHttpClientContext(HttpResponseMessage innerResponseMessage)
        {
            _innerResponseMessage = innerResponseMessage.AssertNonNull(nameof(innerResponseMessage));
        }

        /// <inheritdoc/>
        public HttpVersion ProtocolVersion => HttpVersion.FromVersion(_innerResponseMessage.Version);

        /// <inheritdoc/>
        public Task FinishAsync(HttpResponse sourceResponse, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            _innerResponseMessage.Dispose();
            return DurandalTaskExtensions.NoOpTask;
        }
    }
}
