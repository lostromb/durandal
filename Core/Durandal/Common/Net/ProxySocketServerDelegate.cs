using Durandal.Common.Logger;
using Durandal.Common.IO;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net
{
    public class ProxySocketServerDelegate : ISocketServerDelegate
    {
        private readonly ISocketFactory _socketFactory;
        private readonly TcpConnectionConfiguration _connectionConfig;
        private readonly ILogger _logger;
        private readonly Stream _uploadStream;
        private readonly Stream _downloadStream;

        public ProxySocketServerDelegate(
            ISocketFactory socketFactory,
            ILogger logger,
            Stream uploadStream,
            Stream downloadStream,
            TcpConnectionConfiguration connectionConfig)
        {
            _socketFactory = socketFactory;
            _logger = logger;
            _uploadStream = uploadStream;
            _downloadStream = downloadStream;
            _connectionConfig = connectionConfig;
        }

        public async Task HandleSocketConnection(ISocket clientSocket, ServerBindingInfo socketBinding, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            ISocket proxiedSocket = await _socketFactory.Connect(_connectionConfig, _logger, cancelToken, realTime).ConfigureAwait(false);
            try
            {
                // BUGBUG - thread local realtime is probably not correctly applied here
                Task outgoingPipe = Pipe(clientSocket, proxiedSocket, _logger.Clone("UPLOAD"), cancelToken, realTime, _uploadStream);
                Task incomingPipe = Pipe(proxiedSocket, clientSocket, _logger.Clone("DOWNLOAD"), cancelToken, realTime, _downloadStream);
                await outgoingPipe.ConfigureAwait(false);
                await incomingPipe.ConfigureAwait(false);
            }
            finally
            {
                await proxiedSocket.Disconnect(cancelToken, realTime, NetworkDuplex.ReadWrite, allowLinger: false).ConfigureAwait(false);
            }
        }

        private static async Task Pipe(ISocket input, ISocket output, ILogger logger, CancellationToken cancelToken, IRealTimeProvider realTime, Stream mirrorStream = null)
        {
            IRealTimeProvider forkTime = realTime.Fork("ProxySocketServerPipe");
            try
            {
                using (PooledBuffer<byte> pooledBuffer = BufferPool<byte>.Rent(65536))
                {
                    byte[] scratch = pooledBuffer.Buffer;
                    bool done = false;
                    while (!done)
                    {
                        int readSize = await input.ReadAnyAsync(scratch, 0, 65536, cancelToken, forkTime).ConfigureAwait(false);
                        if (readSize == 0)
                        {
                            done = true;
                            if (mirrorStream != null)
                            {
                                await mirrorStream.FlushAsync(cancelToken).ConfigureAwait(false);
                            }

                            logger.Log("Pipe closed");
                        }
                        else
                        {
                            if (mirrorStream != null)
                            {
                                await mirrorStream.WriteAsync(scratch, 0, readSize).ConfigureAwait(false);
                            }

                            await output.WriteAsync(scratch, 0, readSize, cancelToken, realTime).ConfigureAwait(false);
                            await output.FlushAsync(cancelToken, realTime).ConfigureAwait(false);
                        }
                    }
                }
            }
            finally
            {
                forkTime.Merge();
            }
        }
    }
}
