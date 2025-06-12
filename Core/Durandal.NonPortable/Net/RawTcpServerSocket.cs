using Durandal.Common.Collections;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net
{
    /// <summary>
    /// The most common server implementation of <see cref="ISocket"/>, using TCP sockets from the System.Net.Sockets namespace.
    /// </summary>
    public class RawTcpServerSocket : ISocket
    {
        private static readonly SmallDictionary<SocketFeature, object> DEFAULT_FEATURES = new SmallDictionary<SocketFeature, object>(0);
        private System.Net.Sockets.Socket _listenSocket = null;
        private System.Net.Sockets.Socket _acceptSocket = null;
        private RawTcpSocket _baseSocket = null;
        private bool _connected = false;
        private bool _closed = false;
        private int _disposed = 0;
        private int _receiveTimeout = 30000;

        public RawTcpServerSocket(ILogger logger)
        {
            _baseSocket = null;
            _connected = false;
            _listenSocket = new System.Net.Sockets.Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _listenSocket.Bind(new IPEndPoint(IPAddress.Any, 0));
            _listenSocket.Listen(1);
            logger.Log("Establishing single-use listen socket at " + _listenSocket.LocalEndPoint.ToString(), LogLevel.Std);

            Task.Run((Action)(() =>
            {
                try
                {
                    _acceptSocket = _listenSocket.Accept();
                    System.Net.Sockets.Socket listenSocket = _listenSocket;
                    _listenSocket = null;
                    _baseSocket = new RawTcpSocket(_acceptSocket, false);
                    _baseSocket.ReceiveTimeout = _receiveTimeout;
                    _connected = true;

                    listenSocket.Close();
                    listenSocket.Dispose();
                }
                catch (Exception e)
                {
                    _closed = true;
                    logger.Log(e, LogLevel.Err);
                }
            }));

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~RawTcpServerSocket()
        {
            Dispose(false);
        }
#endif

        public int ReceiveTimeout
        {
            get
            {
                return _receiveTimeout;
            }
            set
            {
                _receiveTimeout = value;
                if (_connected)
                {
                    _baseSocket.ReceiveTimeout = _receiveTimeout;
                }
            }
        }

        public string RemoteEndpointString
        {
            get
            {
                return "tcp://" + _listenSocket.LocalEndPoint.ToString();
            }
        }

        public IReadOnlyDictionary<SocketFeature, object> Features => DEFAULT_FEATURES;

        public async Task Disconnect(CancellationToken cancelToken, IRealTimeProvider waitProvider, NetworkDuplex which = NetworkDuplex.ReadWrite, bool allowLinger = false)
        {
            await _baseSocket.Disconnect(cancelToken, waitProvider, which, allowLinger).ConfigureAwait(false);
        }

        public async Task FlushAsync(CancellationToken cancelToken, IRealTimeProvider waitProvider)
        {
            if (await WaitUntilConnected(cancelToken, waitProvider).ConfigureAwait(false))
            {
                if (_closed)
                {
                    throw new ObjectDisposedException(nameof(RawTcpServerSocket), "Socket has been closed");
                }

                await _baseSocket.FlushAsync(cancelToken, waitProvider).ConfigureAwait(false);
            }
        }

        public async Task<int> ReadAnyAsync(byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider waitProvider)
        {
            if (await WaitUntilConnected(cancelToken, waitProvider, _receiveTimeout).ConfigureAwait(false))
            {
                if (_closed)
                {
                    throw new ObjectDisposedException(nameof(RawTcpServerSocket), "Socket has been closed");
                }

                return await _baseSocket.ReadAnyAsync(data, offset, count, cancelToken, waitProvider).ConfigureAwait(false);
            }
            else
            {
                return 0;
            }
        }

        public async Task<int> ReadAsync(byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider waitProvider)
        {
            if (await WaitUntilConnected(cancelToken, waitProvider, _receiveTimeout).ConfigureAwait(false))
            {
                if (_closed)
                {
                    throw new ObjectDisposedException(nameof(RawTcpServerSocket), "Socket has been closed");
                }

                return await _baseSocket.ReadAsync(data, offset, count, cancelToken, waitProvider).ConfigureAwait(false);
            }
            else
            {
                return 0;
            }
        }

        public async Task WriteAsync(byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (await WaitUntilConnected(cancelToken, realTime).ConfigureAwait(false))
            {
                if (_closed)
                {
                    throw new ObjectDisposedException(nameof(RawTcpServerSocket), "Socket has been closed");
                }

                await _baseSocket.WriteAsync(data, offset, count, cancelToken, realTime).ConfigureAwait(false);
            }
        }

        public void Unread(byte[] buffer, int offset, int count)
        {
            _baseSocket.Unread(buffer, offset, count);
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
                _baseSocket?.Dispose();
                try { _acceptSocket?.Close(); } catch (Exception) { }
                _acceptSocket?.Dispose();
                try { _listenSocket?.Close(); } catch (Exception) { }
                _listenSocket?.Dispose();
            }
        }

        private async Task<bool> WaitUntilConnected(CancellationToken cancelToken, IRealTimeProvider realTime, int? maxTimeout = null)
        {
            if (_connected || _closed)
            {
                return true;
            }

            if (maxTimeout.HasValue)
            {
                int timeRemaining = maxTimeout.Value;
                while ((!_connected && !_closed) && timeRemaining > 0)
                {
                    await realTime.WaitAsync(TimeSpan.FromMilliseconds(1), cancelToken).ConfigureAwait(false);
                    timeRemaining -= 1;
                }
            }
            else
            {
                while (!_connected && !_closed)
                {
                    await realTime.WaitAsync(TimeSpan.FromMilliseconds(1), cancelToken).ConfigureAwait(false);
                }
            }

            return _connected || _closed;
        }
    }
}
