using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;

namespace Durandal.Common.Net
{
    /// <summary>
    /// A class which wraps a Stream over the interface of a socket.
    /// This is really only useful if you want to capture socket traffic into a MemoryStream or something
    /// for certain serialiation scenarios.
    /// </summary>
    public class StreamSocket : ISocket
    {
        private static readonly SmallDictionary<SocketFeature, object> DEFAULT_FEATURES =
            new SmallDictionary<SocketFeature, object>(new Dictionary<SocketFeature, object>()
                { { SocketFeature.MemorySocket, null } });

        private readonly Stream _innerStream;
        private readonly StackBuffer _unreadBuffer = new StackBuffer();
        private int _disposed = 0;

        public StreamSocket(Stream innerStream)
        {
            _innerStream = innerStream.AssertNonNull(nameof(innerStream));
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~StreamSocket()
        {
            Dispose(false);
        }
#endif

        /// <inheritdoc />
        public int ReceiveTimeout
        {
            get
            {
                return 0;
            }
            set { }
        }

        /// <inheritdoc />
        public string RemoteEndpointString => "stream://";

        /// <inheritdoc />
        public IReadOnlyDictionary<SocketFeature, object> Features => DEFAULT_FEATURES;

        /// <inheritdoc />
        public Task Disconnect(CancellationToken cancelToken, IRealTimeProvider realTime, NetworkDuplex which = NetworkDuplex.ReadWrite, bool allowLinger = false)
        {
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
                _innerStream.Dispose();
                _unreadBuffer.Dispose();
            }
        }

        /// <inheritdoc />
        public Task FlushAsync(CancellationToken cancelToken, IRealTimeProvider waitProvider)
        {
            return _innerStream.FlushAsync(cancelToken);
        }

        /// <inheritdoc />
        public Task<int> ReadAnyAsync(byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider waitProvider)
        {
            // First, try to read from the unread buffer
            int unreadBytes = _unreadBuffer.Read(data, offset, count);
            if (unreadBytes > 0)
            {
                // If we have just some data from the unread buffer, return it as a partial read because a read from the actual socket may block.
                return Task.FromResult<int>(unreadBytes);
            }

            return _innerStream.ReadAsync(data, offset, count, cancelToken);
        }

        /// <inheritdoc />
        public Task<int> ReadAsync(byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider waitProvider)
        {
            int unreadBytes = _unreadBuffer.Read(data, offset, count);
            if (unreadBytes == count)
            {
                return Task.FromResult<int>(count);
            }

            return SocketHelpers.ReliableRead(this, data, offset + unreadBytes, count - unreadBytes, cancelToken, waitProvider);
        }

        /// <inheritdoc />
        public Task WriteAsync(byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider waitProvider)
        {
            return _innerStream.WriteAsync(data, offset, count, cancelToken);
        }

        /// <inheritdoc />
        public void Unread(byte[] buffer, int offset, int count)
        {
            _unreadBuffer.Write(buffer, offset, count);
        }
    }
}
