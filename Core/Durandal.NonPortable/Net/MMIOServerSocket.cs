using Durandal.Common.Net;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using Durandal.Common.Time;
using Durandal.Common.Logger;
using Durandal.Common.Tasks;
using Durandal.Common.Instrumentation;
using Durandal.Common.Utils;
using Durandal.Common.IO;
using Durandal.Common.Collections;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Net
{
    /// <summary>
    /// Single-use socket implementation for IPC on a local system using a pair of memory-mapped files (each one can only be half-duplex)
    /// </summary>
    public class MMIOServerSocket : ISocket
    {
        private static readonly SmallDictionary<SocketFeature, object> DEFAULT_FEATURES =
               new SmallDictionary<SocketFeature, object>(new Dictionary<SocketFeature, object>()
                   { { SocketFeature.MemorySocket, null } });

        private readonly MemoryMappedFileStream _inStream;
        private readonly MemoryMappedFileStream _outStream;
        private readonly string _endpointString;
        private readonly StackBuffer _unreadBuffer = new StackBuffer();
        private int _disposed = 0;

        /// <summary>
        /// Creates a new server socket pair
        /// </summary>
        /// <param name="logger">A logger</param>
        /// <param name="metrics">A metric collector</param>
        /// <param name="dimensions">Dimensions for reporting metrics</param>
        /// <param name="bufferSize">The buffer size in bytes to use for the new pipes</param>
        public MMIOServerSocket(ILogger logger, WeakPointer<IMetricCollector> metrics, DimensionSet dimensions, int bufferSize = 65536)
        {
            string inFileName = Guid.NewGuid().ToString("N").Substring(0, 8);
            string outFileName = Guid.NewGuid().ToString("N").Substring(0, 8);
            _inStream = new MemoryMappedFileStream(inFileName, bufferSize, logger, metrics, dimensions, true);
            _outStream = new MemoryMappedFileStream(outFileName, bufferSize, logger, metrics, dimensions, false);
            _endpointString = "mmio:///?in=" + inFileName + "&out=" + outFileName;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~MMIOServerSocket()
        {
            Dispose(false);
        }
#endif

        public int ReceiveTimeout
        {
            get
            {
                return _inStream.ReadTimeout;
            }

            set
            {
                _inStream.ReadTimeout = value;
            }
        }

        public string RemoteEndpointString
        {
            get
            {
                return _endpointString;
            }
        }

        public IReadOnlyDictionary<SocketFeature, object> Features => DEFAULT_FEATURES;

        public Task Disconnect(CancellationToken cancelToken, IRealTimeProvider waitProvider, NetworkDuplex which = NetworkDuplex.ReadWrite, bool allowLinger = false)
        {
            return DurandalTaskExtensions.NoOpTask;
        }

        public Task FlushAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return DurandalTaskExtensions.NoOpTask;
        }

        public Task<int> ReadAsync(byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _inStream.ReadAsync(data, offset, count, cancelToken, realTime);
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

            return _inStream.ReadAnyAsync(data, offset, count, cancelToken, realTime);
        }

        public Task WriteAsync(byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _outStream.WriteAsync(data, offset, count, cancelToken);
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
                _inStream.Dispose();
                _outStream.Dispose();
                _unreadBuffer.Dispose();
            }
        }
    }
}
