using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Durandal.Common.Utils.Tasks;
using Durandal.Common.Tasks;
using Durandal.Common.Net;
using Durandal.Common.Time;
using Durandal.Common.IO;
using System.Net.Security;

namespace Durandal.AndroidClient.Common
{
    public class AndroidSslSocket : ISocket
    {
        private AndroidSocket _socket;
        private SslStream _stream;
        private StackBuffer _unreadBuffer;

        public AndroidSslSocket(AndroidSocket socket, SslStream sslStream)
        {
            _socket = socket;
            _stream = sslStream;
            _unreadBuffer = new StackBuffer();
            Dictionary<SocketFeature, object> features = new Dictionary<SocketFeature, object>();
            features[SocketFeature.SecureConnection] = null;
            switch (sslStream.SslProtocol)
            {
                case System.Security.Authentication.SslProtocols.Tls11:
                    features[SocketFeature.TlsProtocolVersion] = new Version(1, 1);
                    break;
                case System.Security.Authentication.SslProtocols.Tls12:
                    features[SocketFeature.TlsProtocolVersion] = new Version(1, 2);
                    break;
                case System.Security.Authentication.SslProtocols.Tls13:
                    features[SocketFeature.TlsProtocolVersion] = new Version(1, 3);
                    break;
            }

            if (sslStream.NegotiatedApplicationProtocol == SslApplicationProtocol.Http2)
            {
                features[SocketFeature.NegotiatedHttp2Support] = null;
            }

            Features = features;
        }

        public int ReceiveTimeout
        {
            get
            {
                return _socket.ReceiveTimeout;
            }

            set
            {
                _socket.ReceiveTimeout = value;
            }
        }

        public string RemoteEndpointString
        {
            get
            {
                return _socket.RemoteEndpointString;
            }
        }

        public IReadOnlyDictionary<SocketFeature, object> Features { get; private set; }

        public void Dispose()
        {
            _socket?.Dispose();
            _unreadBuffer?.Dispose();
        }

        /// <inheritdoc />
        public Task FlushAsync(CancellationToken cancelToken, IRealTimeProvider waitProvider)
        {
            return _stream.FlushAsync(cancelToken);
        }

        /// <inheritdoc />
        public async Task<int> ReadAnyAsync(byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider waitProvider)
        {
            // Check unread buffer first
            int amountFromUnreadBuffer = _unreadBuffer.Read(data, offset, count);
            if (amountFromUnreadBuffer > 0)
            {
                return amountFromUnreadBuffer;
            }

            return await _stream.ReadAsync(data, offset, count, cancelToken);
        }

        /// <inheritdoc />
        public Task WriteAsync(byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider waitProvider)
        {
            return _stream.WriteAsync(data, offset, count, cancelToken);
        }

        /// <inheritdoc />
        public Task<int> ReadAsync(byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider waitProvider)
        {
            return SocketHelpers.ReliableRead(this, data, offset, count, cancelToken, waitProvider);
        }

        /// <inheritdoc />
        public void Unread(byte[] data, int offset, int count)
        {
            _unreadBuffer.Write(data, offset, count);
        }

        public async Task Disconnect(CancellationToken cancelToken, IRealTimeProvider waitProvider, NetworkDuplex which = NetworkDuplex.ReadWrite, bool allowLinger = false)
        {
            await _socket.Disconnect(cancelToken, waitProvider, which, allowLinger);
        }
    }
}
