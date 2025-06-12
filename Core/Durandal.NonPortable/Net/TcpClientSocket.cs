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
    public class TcpClientSocket : ISocket
    {
        private static readonly SmallDictionary<SocketFeature, object> DEFAULT_FEATURES = new SmallDictionary<SocketFeature, object>(0);

        private readonly TcpClient _baseSocket;
        private readonly NetworkStream _baseStream;
        private readonly StackBuffer _unreadBuffer = new StackBuffer();
        private int _disposed = 0;

        public TcpClientSocket(TcpClient baseSocket)
        {
            _baseSocket = baseSocket;
            _baseStream = _baseSocket.GetStream();
            ReceiveTimeout = 30000;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~TcpClientSocket()
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

        public IReadOnlyDictionary<SocketFeature, object> Features
        {
            get
            {
                return DEFAULT_FEATURES;
            }
        }

        public Task FlushAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _baseStream.FlushAsync(cancelToken);
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

        public Task<int> ReadAnyAsync(byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            // First, try to read from the unread buffer
            int unreadBytes = _unreadBuffer.Read(data, offset, count);
            if (unreadBytes > 0)
            {
                // If we have just some data from the unread buffer, return it as a partial read because a read from the actual socket may block.
                return Task.FromResult<int>(unreadBytes);
            }

            return _baseStream.ReadAsync(data, offset, count, cancelToken);
        }

        public Task WriteAsync(byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _baseStream.WriteAsync(data, offset, count, cancelToken);
        }

        public void Unread(byte[] buffer, int offset, int count)
        {
            _unreadBuffer.Write(buffer, offset, count);
        }

        public Task Disconnect(CancellationToken cancelToken, IRealTimeProvider waitProvider, NetworkDuplex which = NetworkDuplex.ReadWrite, bool allowLinger = false)
        {
            SocketShutdown how;
            switch (which)
            {
                case NetworkDuplex.Read:
                    how = SocketShutdown.Receive; break;
                case NetworkDuplex.Write:
                    how = SocketShutdown.Send; break;
                default:
                    how = SocketShutdown.Both; break;
            }

            _baseSocket.Client.Shutdown(how);
            return DurandalTaskExtensions.NoOpTask;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

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

                if (_baseSocket != null && _baseSocket.Connected)
                {
                    _baseSocket.Close();
                }

                _unreadBuffer.Dispose();
            }
        }
    }
}
