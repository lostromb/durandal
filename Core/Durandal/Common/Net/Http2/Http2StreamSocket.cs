using Durandal.Common.Collections;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net.Http2
{
    /// <summary>
    /// An implementation of <see cref="ISocket"/> which operates over a single stream within an HTTP2 connection.
    /// Used as the basis for websockets over h2, see RFC 8441.
    /// </summary>
    internal class Http2StreamSocket : ISocket
    {
        private static IReadOnlyDictionary<SocketFeature, object> EMPTY_FEATURE_DICTIONARY = new SmallDictionary<SocketFeature, object>(initialCapacity: 0);
        private readonly WeakPointer<Http2Session> _session;
        private readonly int _streamId;

        public Http2StreamSocket(WeakPointer<Http2Session> session, int streamId)
        {
            _session = session;
            _streamId = streamId;
        }   

        public int ReceiveTimeout
        {
            get
            {
                return 0;
            }
            set
            {
            }
        }

        public string RemoteEndpointString => "Websocket";

        public IReadOnlyDictionary<SocketFeature, object> Features => EMPTY_FEATURE_DICTIONARY;

        public Task Disconnect(CancellationToken cancelToken, IRealTimeProvider realTime, NetworkDuplex which = NetworkDuplex.ReadWrite, bool allowLinger = false)
        {
            // this time provider is only used for timestamping frames for debug, so not that important...
            _session.Value.SocketStream_Close(which, _streamId, realTime);
            return DurandalTaskExtensions.NoOpTask;
        }

        public void Dispose()
        {
            _session.Value.SocketStream_Close(NetworkDuplex.ReadWrite, _streamId, DefaultRealTimeProvider.Singleton);
        }

        public Task FlushAsync(CancellationToken cancelToken, IRealTimeProvider waitProvider)
        {
            return DurandalTaskExtensions.NoOpTask;
        }

        public Task<int> ReadAnyAsync(byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider waitProvider)
        {
            return _session.Value.SocketStream_ReadData(_streamId, data, offset, count, cancelToken, waitProvider);
        }

        public Task<int> ReadAsync(byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider waitProvider)
        {
            return SocketHelpers.ReliableRead(this, data, offset, count, cancelToken, waitProvider);
        }

        public void Unread(byte[] data, int offset, int count)
        {
            // FIXME implement unread buffer
            throw new NotImplementedException();
        }

        public async Task WriteAsync(byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider waitProvider)
        {
            bool success = await _session.Value.SocketStream_SendData(_streamId, data, offset, count, cancelToken, waitProvider).ConfigureAwait(false);
        }
    }
}
