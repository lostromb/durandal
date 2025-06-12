using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net
{
    public class PooledTcpClientSocket : IPooledSocket
    {
        private static readonly SmallDictionary<SocketFeature, object> DEFAULT_FEATURES =
            new SmallDictionary<SocketFeature, object>(new Dictionary<SocketFeature, object>()
                { { SocketFeature.PooledConnection, null } });

        private readonly TcpClient _baseSocket;
        private readonly NetworkStream _baseStream;
        private readonly StackBuffer _unreadBuffer = new StackBuffer();
        private int _disconnected = 0;
        private int _disposed = 0;
        private readonly Action<IPooledSocket, NetworkDuplex, bool> _disconnectAction;
        private volatile bool _lastOperationSucceeded = true;

        public PooledTcpClientSocket(TcpClient baseSocket, Action<IPooledSocket, NetworkDuplex, bool> disconnectAction)
        {
            _baseSocket = baseSocket;
            _baseStream = _baseSocket.GetStream();
            _disconnectAction = disconnectAction;
            ReceiveTimeout = 30000;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~PooledTcpClientSocket()
        {
            Dispose(false);
        }
#endif

        public int ReceiveTimeout
        {
            get
            {
                return _baseSocket.ReceiveTimeout;
            }
            set
            {
                _baseSocket.ReceiveTimeout = value;
            }
        }

        public string RemoteEndpointString
        {
            get
            {
                return _baseSocket.Client.RemoteEndPoint.ToString();
            }
        }

        public bool Healthy
        {
            get
            {
                return _lastOperationSucceeded &&
                    _baseSocket.Connected;
            }
        }

        public IReadOnlyDictionary<SocketFeature, object> Features => DEFAULT_FEATURES;

        public async Task FlushAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(PooledTcpClientSocket));
            }
            
            _lastOperationSucceeded = false;
            await _baseStream.FlushAsync(cancelToken).ConfigureAwait(false);
            _lastOperationSucceeded = true;
        }

        public Task<int> ReadAsync(byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int unreadBytes = _unreadBuffer.Read(data, offset, count);
            if (unreadBytes == count)
            {
                return Task.FromResult<int>(count);
            }

            return SocketHelpers.ReliableRead(this, data, offset + unreadBytes, count - unreadBytes, cancelToken, realTime);
        }

        public async Task<int> ReadAnyAsync(byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(PooledTcpClientSocket));
            }

            // First, try to read from the unread buffer
            int unreadBytes = _unreadBuffer.Read(data, offset, count);
            if (unreadBytes > 0)
            {
                // If we have just some data from the unread buffer, return it as a partial read because a read from the actual socket may block.
                return unreadBytes;
            }

            _lastOperationSucceeded = false;
            int returnVal = await _baseStream.ReadAsync(data, offset, count, cancelToken).ConfigureAwait(false);
            _lastOperationSucceeded = true;
            return returnVal;
        }

        public async Task WriteAsync(byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(PooledTcpClientSocket));
            }
            
            _lastOperationSucceeded = false;
            await _baseStream.WriteAsync(data, offset, count, cancelToken).ConfigureAwait(false);
            _lastOperationSucceeded = true;
        }

        public void Unread(byte[] buffer, int offset, int count)
        {
            _unreadBuffer.Write(buffer, offset, count);
        }

        public void MakeReadyForReuse()
        {
            _disconnected = 0;
        }

        public Task Disconnect(CancellationToken cancelToken, IRealTimeProvider waitProvider, NetworkDuplex which = NetworkDuplex.ReadWrite, bool allowLinger = false)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disconnected))
            {
                return DurandalTaskExtensions.NoOpTask;
            }

            _disconnectAction(this, which, allowLinger);
            return DurandalTaskExtensions.NoOpTask;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Actual disconnection happens here
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                _baseStream?.Dispose();
                _baseSocket?.Close();
                _unreadBuffer.Dispose();
            }
        }
    }
}
