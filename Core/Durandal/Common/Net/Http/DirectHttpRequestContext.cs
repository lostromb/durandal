using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.Net.WebSocket;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net.Http
{
    public class DirectHttpRequestContext : IHttpServerContext
    {
        public DirectHttpRequestContext(HttpRequest request)
        {
            HttpRequest = request;
            PrimaryResponseStarted = false;
            PrimaryResponseFinished = false;

            // Conditionally enable trailers on the server-side based on the presence of the "TE: trailers" header
            SupportsTrailers = HttpRequest.RequestHeaders.ContainsValue(
                HttpConstants.HEADER_KEY_TE,
                HttpConstants.HEADER_VALUE_TRAILERS,
                StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc/>
        public HttpVersion CurrentProtocolVersion => HttpVersion.HTTP_1_1;

        /// <inheritdoc/>
        public HttpResponse ClientResponse { get; private set; }

        /// <inheritdoc/>
        public bool SupportsWebSocket => false;

        /// <inheritdoc/>
        public bool SupportsServerPush => false;

        /// <inheritdoc/>
        public bool SupportsTrailers { get; private set; }

        /// <inheritdoc/>
        public HttpRequest HttpRequest { get; private set; }

        /// <inheritdoc/>
        public bool PrimaryResponseStarted { get; private set; }

        /// <inheritdoc/>
        public bool PrimaryResponseFinished { get; private set; }

        /// <inheritdoc/>
        public Task<IWebSocket> AcceptWebsocketUpgrade(CancellationToken cancelToken, IRealTimeProvider realTime, string subProtocol = null)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public Task WritePrimaryResponse(HttpResponse response, ILogger traceLogger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            ClientResponse = response;
            PrimaryResponseStarted = true;
            PrimaryResponseFinished = true;
            if (response.ResponseHeaders.ContainsKey(HttpConstants.HEADER_KEY_TRAILER))
            {
                throw new ArgumentException("You may not set the \"Trailer\" HTTP header manually");
            }

            return DurandalTaskExtensions.NoOpTask;
        }

        /// <inheritdoc/>
        public Task WritePrimaryResponse(
            HttpResponse response,
            ILogger traceLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            IReadOnlyCollection<string> trailerNames,
            Func<string, Task<string>> trailerDelegate)
        {
            if (ReferenceEquals(response.GetOutgoingContentStream(), HttpRequest.GetIncomingContentStream()))
            {
                throw new InvalidOperationException("You can't pipe an HTTP input directly back to HTTP output");
            }

            response.WrapOutgoingContentStream(
                (originalStream) =>
                    new TrailerAppendingStreamWrapper(
                        originalStream,
                        trailerNames,
                        trailerDelegate));
            return WritePrimaryResponse(response, traceLogger, cancelToken, realTime);
        }

        /// <inheritdoc/>
        public void PushPromise(
            string expectedRequestMethod,
            string expectedPath,
            HttpHeaders expectedRequestHeaders,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            throw new NotSupportedException("Server push is not implemented for direct HTTP connections");
        }

        private sealed class TrailerAppendingStreamWrapper : HttpContentStream
        {
            private readonly NonRealTimeStream _innerStream;
            private readonly IReadOnlyCollection<string> _trailerNames;
            private readonly Func<string, Task<string>> _trailerDelegate;
            private HttpHeaders _trailers;
            private long _contentBytesTransferred;

            public TrailerAppendingStreamWrapper(
                NonRealTimeStream innerStream,
                IReadOnlyCollection<string> trailerNames,
                Func<string, Task<string>> trailerDelegate)
            {
                _innerStream = innerStream.AssertNonNull(nameof(innerStream));
                _trailerNames = trailerNames;
                _trailerDelegate = trailerDelegate;
            }

            public override long ContentBytesTransferred => _contentBytesTransferred;

            public override HttpHeaders Trailers => _trailers;

            public override bool CanRead => _innerStream.CanRead;

            public override bool CanSeek => _innerStream.CanSeek;

            public override bool CanWrite => _innerStream.CanWrite;

            public override long Length => _innerStream.Length;

            public override long Position { get => _innerStream.Position; set => _innerStream.Position = value; }

            public override void Flush()
            {
                _innerStream.Flush();
            }

            public override int Read(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                int returnVal = _innerStream.Read(targetBuffer, offset, count, cancelToken, realTime);
                if (returnVal == 0)
                {
                    GenerateTrailersIfApplicable().Await();
                }
                else
                {
                    _contentBytesTransferred += returnVal;
                }

                return returnVal;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int returnVal = _innerStream.Read(buffer, offset, count);
                if (returnVal == 0)
                {
                    GenerateTrailersIfApplicable().Await();
                }
                else
                {
                    _contentBytesTransferred += returnVal;
                }

                return returnVal;
            }

            public override async Task<int> ReadAsync(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                int returnVal = await _innerStream.ReadAsync(targetBuffer, offset, count, cancelToken, realTime).ConfigureAwait(false);
                if (returnVal == 0)
                {
                    await GenerateTrailersIfApplicable().ConfigureAwait(false);
                }
                else
                {
                    _contentBytesTransferred += returnVal;
                }

                return returnVal;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return _innerStream.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                _innerStream.SetLength(value);
            }

            public override void Write(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                _innerStream.Write(sourceBuffer, offset, count, cancelToken, realTime);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                _innerStream.Write(buffer, offset, count);
            }

            public override Task WriteAsync(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                return _innerStream.WriteAsync(sourceBuffer, offset, count, cancelToken, realTime);
            }

            private async ValueTask GenerateTrailersIfApplicable()
            {
                if (_trailerNames == null || _trailerDelegate == null || _trailerNames.Count == 0)
                {
                    return;
                }

                _trailers = new HttpHeaders(_trailerNames.Count);
                foreach (string trailerName in _trailerNames)
                {
                    string trailerValue = await _trailerDelegate(trailerName).ConfigureAwait(false);
                    _trailers.Add(trailerName, trailerValue);
                }
            }
        }
    }
}
