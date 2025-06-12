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
using Durandal.Common.Utils;
using Durandal.Common.IO;
using Durandal.Common.Collections;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;

namespace Durandal.Common.Net
{
    /// <summary>
    /// Single-use socket implementation for IPC on a local system using a pair of anonymous pipes (each one can only be half-duplex)
    /// </summary>
    public class AnonymousPipeServerSocket : ISocket
    {
        private static readonly SmallDictionary<SocketFeature, object> DEFAULT_FEATURES =
            new SmallDictionary<SocketFeature, object>(new Dictionary<SocketFeature, object>()
                { { SocketFeature.MemorySocket, null } });

        private readonly AnonymousPipeServerStream _inPipe;
        private readonly AnonymousPipeServerStream _outPipe;
        private readonly string _endpointString;
        private readonly StackBuffer _unreadBuffer = new StackBuffer();
        private int _disposed = 0;

        /// <summary>
        /// Creates a new server socket pair
        /// </summary>
        /// <param name="bufferSize">The buffer size to use for the new pipes</param>
        public AnonymousPipeServerSocket(int bufferSize = 4096)
        {
            _inPipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable, bufferSize);
            _outPipe = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable, bufferSize);
            _endpointString = "pipe:///?in=" + _inPipe.GetClientHandleAsString() + "&out=" + _outPipe.GetClientHandleAsString();
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~AnonymousPipeServerSocket()
        {
            Dispose(false);
        }
#endif

        public int ReceiveTimeout
        {
            get
            {
                return _inPipe.ReadTimeout;
            }

            set
            {
                //_inPipe.ReadTimeout = value;
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

        /// <summary>
        /// When two processes both share an anonymous pipe, both of them will try and exclusively own that handle which can
        /// cause problems with double disposal (often manifested as obscure SEHExceptions). Invoking this method will cause
        /// the server to relinquish ownership over the handles and allow the pipe client to maintain control
        /// </summary>
        public void DisposeLocalCopyOfClientHandle()
        {
            _inPipe.DisposeLocalCopyOfClientHandle();
            _outPipe.DisposeLocalCopyOfClientHandle();
        }

        public Task Disconnect(CancellationToken cancelToken, IRealTimeProvider waitProvider, NetworkDuplex which = NetworkDuplex.ReadWrite, bool allowLinger = false)
        {
            return DurandalTaskExtensions.NoOpTask;
        }

        public Task FlushAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            // Flush doesn't actually do anything for anonymous pipes...
            return _outPipe.FlushAsync(cancelToken);
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

            return _inPipe.ReadAsync(data, offset, count, cancelToken);
        }

        public Task WriteAsync(byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _outPipe.WriteAsync(data, offset, count, cancelToken);
            // Since pipe buffers are small, break up the write into tiny pieces
            //int sent = 0;
            //while (sent < count)
            //{
            //    int thisPacketSize = Math.Min(1024, count - sent);
            //    await _outPipe.WriteAsync(data, sent + offset, thisPacketSize, cancelToken);
            //    sent += thisPacketSize;
            //}
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
                _inPipe.Dispose();
                _outPipe.Dispose();
                _unreadBuffer.Dispose();
            }
        }
    }
}
