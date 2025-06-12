using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net
{
    /// <summary>
    /// A <see cref="NonRealTimeStream"/> implementation which wraps around an <see cref="ISocket"/>.
    /// </summary>
    public class SocketStream : NonRealTimeStream
    {
        private readonly WeakPointer<ISocket> _socket;
        private readonly ILogger _logger;
        private readonly bool _ownsSocket;
        private int _disposed = 0;

        /// <summary>
        /// Constructs a new <see cref="SocketStream"/>.
        /// </summary>
        /// <param name="wrappedSocket">The socket to wrap this stream around</param>
        /// <param name="logger">A logger</param>
        /// <param name="ownsSocket">Whether this stream should take ownership of the socket.</param>
        public SocketStream(WeakPointer<ISocket> wrappedSocket, ILogger logger, bool ownsSocket)
        {
            _socket = wrappedSocket.AssertNonNull(nameof(wrappedSocket));
            _logger = logger.AssertNonNull(nameof(logger));
            _ownsSocket = ownsSocket;
        }

        /// <inheritdoc />
        public override bool CanRead => true;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override bool CanWrite => true;

        /// <inheritdoc />
        public override long Length => throw new NotImplementedException();

        /// <inheritdoc />
        public override long Position
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        /// <inheritdoc />
        public override void Flush()
        {
            _logger.Log("Synchronous flush detected on SocketStream, please use async", LogLevel.Wrn);
            _socket.Value.FlushAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).Await();
        }

        /// <inheritdoc />
        public override int Read(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            _logger.Log("Synchronous read detected on SocketStream, please use async", LogLevel.Wrn);
            return _socket.Value.ReadAnyAsync(targetBuffer, offset, count, cancelToken, realTime).Await();
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            _logger.Log("Synchronous read detected on SocketStream, please use async", LogLevel.Wrn);
            return _socket.Value.ReadAnyAsync(buffer, offset, count, CancellationToken.None, DefaultRealTimeProvider.Singleton).Await();
        }

        /// <inheritdoc />
        public override Task<int> ReadAsync(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _socket.Value.ReadAnyAsync(targetBuffer, offset, count, cancelToken, realTime);
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override void Write(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            _logger.Log("Synchronous write detected on SocketStream, please use async", LogLevel.Wrn);
            _socket.Value.WriteAsync(sourceBuffer, offset, count, cancelToken, realTime).Await();
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            _logger.Log("Synchronous write detected on SocketStream, please use async", LogLevel.Wrn);
            _socket.Value.WriteAsync(buffer, offset, count, CancellationToken.None, DefaultRealTimeProvider.Singleton).Await();
        }

        /// <inheritdoc />
        public override Task WriteAsync(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _socket.Value.WriteAsync(sourceBuffer, offset, count, cancelToken, realTime);
        }

        protected override void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            try
            {
                if (disposing && _ownsSocket)
                {
                    _socket.Value?.Disconnect(CancellationToken.None, DefaultRealTimeProvider.Singleton, NetworkDuplex.ReadWrite, allowLinger: false).Await();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}
