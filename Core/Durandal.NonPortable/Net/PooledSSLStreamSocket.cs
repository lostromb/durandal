using Durandal.Common.Cache;
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
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net
{
    public class PooledSslStreamSocket : IPooledSocket
    {
        private readonly TcpClient _baseClient;
        private readonly SslStream _stream;
        private readonly StackBuffer _unreadBuffer = new StackBuffer();
        private int _disconnected = 0;
        private int _disposed = 0;
        private readonly Action<IPooledSocket, NetworkDuplex, bool> _disconnectAction;
        private readonly SmallDictionary<SocketFeature, object> _features;
        private volatile bool _lastOperationSucceeded = true;

        public PooledSslStreamSocket(TcpClient baseClient, SslStream stream, Action<IPooledSocket, NetworkDuplex, bool> disconnectAction)
        {
            _stream = stream;
            _baseClient = baseClient;
            _disconnectAction = disconnectAction;

            _features = new SmallDictionary<SocketFeature, object>(4);
            _features.Add(SocketFeature.SecureConnection, null);
            _features.Add(SocketFeature.PooledConnection, null);

#pragma warning disable SYSLIB0039 // Warning against using outdated TLS. We're not actually using it here, but handling if it does actually get specified.
            if (stream.SslProtocol == System.Security.Authentication.SslProtocols.Tls)
            {
                _features.Add(SocketFeature.TlsProtocolVersion, new Version(1, 0));
            }
            else if (stream.SslProtocol == System.Security.Authentication.SslProtocols.Tls11)
            {
                _features.Add(SocketFeature.TlsProtocolVersion, new Version(1, 1));
            }
#pragma warning restore SYSLIB0039
            else if (stream.SslProtocol == System.Security.Authentication.SslProtocols.Tls12)
            {
                _features.Add(SocketFeature.TlsProtocolVersion, new Version(1, 2));
            }
#if NETCOREAPP
            else if (stream.SslProtocol == System.Security.Authentication.SslProtocols.Tls13)
            {
                _features.Add(SocketFeature.TlsProtocolVersion, new Version(1, 3));
            }
            if (stream.NegotiatedApplicationProtocol == SslApplicationProtocol.Http2)
            {
                _features.Add(SocketFeature.NegotiatedHttp2Support, null);
            }
#endif

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~PooledSslStreamSocket()
        {
            Dispose(false);
        }
#endif

        public int ReceiveTimeout
        {
            get
            {
                return _stream.ReadTimeout;
            }

            set
            {
                //_stream.ReadTimeout = value;
            }
        }

        public string RemoteEndpointString
        {
            get
            {
                return _baseClient.Client.RemoteEndPoint.ToString();
            }
        }

        public bool Healthy
        {
            get
            {
                return _lastOperationSucceeded && _baseClient.Connected;
            }
        }

        public IReadOnlyDictionary<SocketFeature, object> Features => _features;

        public async Task WriteAsync(byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(PooledSslStreamSocket));
            }

            _lastOperationSucceeded = false;
            await _stream.WriteAsync(data, offset, count, cancelToken).ConfigureAwait(false);
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
                throw new ObjectDisposedException(nameof(PooledSslStreamSocket));
            }

            // First, try to read from the unread buffer
            int unreadBytes = _unreadBuffer.Read(data, offset, count);
            if (unreadBytes > 0)
            {
                // If we have just some data from the unread buffer, return it as a partial read because a read from the actual socket may block.
                return unreadBytes;
            }

            _lastOperationSucceeded = false;
            int returnVal = await _stream.ReadAsync(data, offset, count, cancelToken).ConfigureAwait(false);
            _lastOperationSucceeded = true;
            return returnVal;
        }

        public async Task FlushAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(PooledSslStreamSocket));
            }

            _lastOperationSucceeded = false;
            await _stream.FlushAsync(cancelToken).ConfigureAwait(false);
            _lastOperationSucceeded = true;
        }

        public void Unread(byte[] buffer, int offset, int count)
        {
            _unreadBuffer.Write(buffer, offset, count);
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

        public void MakeReadyForReuse()
        {
            _disconnected = 0;
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
                // BUGBUG Many times we can dispose the socket while it's still in the middle of a read operation, which throws
                // and ObjectDisposedException. Is there anything I can do about that?
                try
                {
                    _stream.Dispose();
                }
                catch (Exception) { }

                _unreadBuffer.Dispose();
            }
        }
    }
}
