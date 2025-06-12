using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net.Http
{
    public class SocketHttpClientContext : IHttpClientContext
    {
        private readonly WeakPointer<ISocket> _socket;
        private readonly bool _allowSocketLinger;
        private readonly ILogger _logger;

        public SocketHttpClientContext(WeakPointer<ISocket> socket, bool allowLingeringConnection, ILogger logger, HttpVersion protocolVersion)
        {
            _socket = socket.AssertNonNull(nameof(socket));
            _allowSocketLinger = allowLingeringConnection;
            _logger = logger.AssertNonNull(nameof(logger));
            ProtocolVersion = protocolVersion;
        }

        /// <inheritdoc/>
        public HttpVersion ProtocolVersion { get; private set; }

        /// <inheritdoc/>
        public async Task FinishAsync(HttpResponse sourceResponse, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            // Did we read all of the data from the response socket?
            using (PooledBuffer<byte> scratch = BufferPool<byte>.Rent(16))
            {
                //_logger.Log("Finishing HTTP socket " + _socket.Value.RemoteEndpointString, LogLevel.Vrb);
                try
                {
                    int bytesRead = await sourceResponse.ReadContentAsStream().ReadAsync(scratch.Buffer, 0, 16, cancelToken, realTime).ConfigureAwait(false);
                    if (bytesRead == 0)
                    {
                        // All data is read. Allow socket linger
                        //_logger.Log("Disconnect socket " + _socket.Value.RemoteEndpointString + " with linger = " + _allowSocketLinger, LogLevel.Vrb);
                        await _socket.Value.Disconnect(cancelToken, realTime, NetworkDuplex.ReadWrite, _allowSocketLinger).ConfigureAwait(false);
                    }
                    else
                    {
                        // There's still data on the wire. This could be either because the client failed to read it (an exception happened),
                        // the client is misconfigured, or the caller intentionally doesn't care about the response contents.
                        // Rather than make assumptions and try and read the full payload here, we will just disconnect the socket
                        _logger.Log("Socket HTTP response was closed before the full response was read. This may be intentional, however the socket connection will be closed", LogLevel.Wrn);
                        await _socket.Value.Disconnect(cancelToken, realTime, NetworkDuplex.ReadWrite, allowLinger: false).ConfigureAwait(false);
                    }
                }
                // Swallow exception that would indicate that the socket is already closed; otherwise rethrow
                catch (ObjectDisposedException) { } // Socket is unhealthy (presumably was closed on write)
                catch (Exception e)
                {
                    if (!SocketHelpers.DoesExceptionIndicateSocketClosed(e))
                    {
                        throw;
                    }
                }
            }

            //_logger.Log("HTTP request context finished", LogLevel.Vrb);
        }
    }
}
