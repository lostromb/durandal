using Durandal.Common.Net;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Durandal.Common.IO;
using Durandal.Common.Collections;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;

namespace Durandal.Common.Net
{
    public class NamedPipeClientSocket : ISocket
    {
        private static readonly SmallDictionary<SocketFeature, object> DEFAULT_FEATURES =
            new SmallDictionary<SocketFeature, object>(new Dictionary<SocketFeature, object>()
                { { SocketFeature.MemorySocket, null } });

        private readonly string _pipeName;
        private readonly NamedPipeClientStream _pipe;
        private readonly StackBuffer _unreadBuffer = new StackBuffer();
        private int _disposed = 0;

        public NamedPipeClientSocket(string pipeName, int initialConnectTimeout = 10000)
        {
            _pipeName = pipeName;
            _pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            _pipe.Connect(initialConnectTimeout);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~NamedPipeClientSocket()
        {
            Dispose(false);
        }
#endif

        public int ReceiveTimeout
        {
            get
            {
                return _pipe.ReadTimeout;
            }

            set
            {
                //_pipe.ReadTimeout = value;
            }
        }

        public string RemoteEndpointString
        {
            get
            {
                return "named-pipe://" + _pipeName;
            }
        }

        public IReadOnlyDictionary<SocketFeature, object> Features => DEFAULT_FEATURES;

        public Task Disconnect(CancellationToken cancelToken, IRealTimeProvider waitProvider, NetworkDuplex which = NetworkDuplex.ReadWrite, bool allowLinger = false)
        {
            Dispose(true);
            return DurandalTaskExtensions.NoOpTask;
        }

        public Task FlushAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _pipe.FlushAsync(cancelToken);
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

            return _pipe.ReadAsync(data, offset, count, cancelToken);
        }

        public async Task WriteAsync(byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            // Since pipe buffers are small, break up the write into 1Kb pieces
            int sent = 0;
            while (sent < count)
            {
                int thisPacketSize = Math.Min(1024, count - sent);
                await _pipe.WriteAsync(data, sent + offset, thisPacketSize, cancelToken).ConfigureAwait(false);
                sent += thisPacketSize;
            }
        }

        public void Unread(byte[] buffer, int offset, int count)
        {
            _unreadBuffer.Write(buffer, offset, count);
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
                _pipe.Dispose();
                _unreadBuffer.Dispose();
            }
        }
    }
}
