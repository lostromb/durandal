using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net
{
    /// <summary>
    /// The most common client implementation of <see cref="ISocket"/>, using TCP sockets from the System.Net.Sockets namespace.
    /// </summary>
    public class RawTcpSocket : ISocket
    {
        private static readonly SmallDictionary<SocketFeature, object> DEFAULT_FEATURES = new SmallDictionary<SocketFeature, object>(0);
        public static readonly string PROTOCOL_SCHEME = "tcp";

        private readonly System.Net.Sockets.Socket _baseSocket;
        private readonly NetworkStream _baseStream;
        private readonly bool _ownsSocket;
#pragma warning disable CA2213 // Disposable fields should be disposed - The analyzer is wrong
        private readonly StackBuffer _unreadBuffer = new StackBuffer();
#pragma warning restore CA2213 // Disposable fields should be disposed
        private int _disposed = 0;

        public RawTcpSocket(System.Net.Sockets.Socket baseSocket, bool ownsSocket)
        {
            _ownsSocket = ownsSocket;
            _baseSocket = baseSocket;
            _baseStream = new NetworkStream(baseSocket, ownsSocket);
            ReceiveTimeout = 30000;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        public RawTcpSocket(string remoteEndPoint)
        {
            _baseSocket = new System.Net.Sockets.Socket(SocketType.Stream, ProtocolType.Tcp);
            Uri targetUri;
            if (!Uri.TryCreate(remoteEndPoint, UriKind.Absolute, out targetUri))
            {
                throw new ArgumentException("Endpoint cannot be parsed: " + remoteEndPoint);
            }

            string targetHost = targetUri.Host;
            if (string.Equals("0.0.0.0", targetHost))
            {
                targetHost = "localhost";
            }

            _baseSocket.Connect(targetHost, targetUri.Port);
            _ownsSocket = true;
            _baseStream = new NetworkStream(_baseSocket, _ownsSocket);
            ReceiveTimeout = 30000;
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~RawTcpSocket()
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
                return _baseSocket.RemoteEndPoint.ToString();
            }
        }

        public IReadOnlyDictionary<SocketFeature, object> Features => DEFAULT_FEATURES;

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

            _baseSocket.Shutdown(how);
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
                if (_ownsSocket)
                {
                    _baseSocket?.Dispose();
                }

                _unreadBuffer?.Dispose();
            }
        }

        public Task FlushAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            //Console.WriteLine("SOCKET: FLUSH");
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

        public async Task<int> ReadAnyAsync(byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            // First, try to read from the unread buffer
            int unreadBytes = _unreadBuffer.Read(data, offset, count);
            if (unreadBytes > 0)
            {
                // If we have just some data from the unread buffer, return it as a partial read because a read from the actual socket may block.
                //Console.WriteLine("SOCKET REREAD : " + Encoding.ASCII.GetString(data, offset, unreadBytes).Replace("\r", "\\r").Replace("\n", "\\n"));
                return unreadBytes;
            }

            int returnVal = await _baseStream.ReadAsync(data, offset, count, cancelToken).ConfigureAwait(false);

            //if (returnVal > 0)
            //{
            //    Console.WriteLine("SOCKET READ : " + Encoding.ASCII.GetString(data, offset, returnVal).Replace("\r", "\\r").Replace("\n", "\\n"));
            //}
            //else
            //{
            //    Console.WriteLine("SOCKET READ : EOF");
            //}

            return returnVal;
        }

        public Task WriteAsync(byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            //Console.WriteLine("SOCKET WRITE: " + Encoding.ASCII.GetString(data, offset, count).Replace("\r", "\\r").Replace("\n", "\\n"));
            return _baseStream.WriteAsync(data, offset, count, cancelToken);
        }

        public void Unread(byte[] buffer, int offset, int count)
        {
            //Console.WriteLine("SOCKET UNREAD : " + Encoding.ASCII.GetString(buffer, offset, count).Replace("\r", "\\r").Replace("\n", "\\n"));
            _unreadBuffer.Write(buffer, offset, count);
        }
    }
}
