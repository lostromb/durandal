using Durandal.Common.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using System.Threading;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;
using Durandal.Common.Logger;
using Windows.Networking;
using Durandal.Common.Tasks;
using System.Diagnostics;
using System.IO;
using Durandal.Common.Time;

namespace DurandalWinRT
{
    /// <summary>
    /// ISocket implementation backed by native StreamSocket
    /// </summary>
    public class WinRTSocket : ISocket
    {
        private Windows.Networking.Sockets.StreamSocket _socket;
        private ILogger _logger;
        private Stream _outputStream;
        private Stream _inputStream;
        private bool _syncReadHack = false;
        private int _disposed = 0;

        public WinRTSocket(Windows.Networking.Sockets.StreamSocket baseSocket, ILogger logger, bool syncReadHack = false)
        {
            _logger = logger;
            _socket = baseSocket;
            _syncReadHack = syncReadHack;

            ReceiveTimeout = 10000;

            _inputStream = baseSocket.InputStream.AsStreamForRead();
            _outputStream = baseSocket.OutputStream.AsStreamForWrite();
        }

        ~WinRTSocket()
        {
            Dispose(false);
        }

        public int ReceiveTimeout
        {
            get; set;
        }

        public string RemoteEndpointString
        {
            get
            {
                return _socket.Information.RemoteAddress + ":" + _socket.Information.RemotePort;
            }
        }

        public void Disconnect(bool allowLinger)
        {
            Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            {
                return;
            }

            Durandal.Common.Utils.DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                lock (this)
                {
                    if (_socket != null)
                    {
                        _inputStream.Dispose();
                        _outputStream.Dispose();
                        _socket.Dispose();
                        _socket = null;
                    }
                }
            }
        }

        public async Task WriteAsync(byte[] data, int offset, int count, CancellationToken cancelToken)
        {
            //_logger.Log("WRITE " + count);
            await _outputStream.WriteAsync(data, offset, count, cancelToken);
        }

        public Task<int> ReadAsync(byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return SocketHelpers.ReliableRead(this, data, offset, count, cancelToken, realTime);
        }

        public async Task<int> ReadAnyAsync(byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            //_logger.Log("READING " + count);
            //byte[] actualData = new byte[count];
            //IBuffer buf = WindowsRuntimeBufferExtensions.AsBuffer(actualData);
            //IBuffer responseBuf = await _socket.InputStream.ReadAsync(buf, (uint)count, InputStreamOptions.Partial);
            //Array.Copy(actualData, 0, data, offset, (int)responseBuf.Length);
            //_logger.Log("READ " + responseBuf.Length);
            //return (int)responseBuf.Length;

            if (!_syncReadHack)
            {
                Task<int> readTask = _inputStream.ReadAsync(data, offset, count, cancelToken);
                /*TimeoutResult<int> readResult = await readTask.Timeout(ReceiveTimeout);
                if (readResult.TimedOut)
                {
                    throw new TimeoutException("The socket read operation timed out");
                }

                return readResult.Result;*/
                return await readTask;
            }
            else
            {
                // THIS IS SO DUMB
                // For some reason when I use ReadAsync here on WP8.1, it blocks forever on read even though I know data is available.
                // So, I have to use synchronous read, but to enforce timeout i have to wrap it in async anyways.
                // Obviously this is less than ideal but it is literally the only thing that works
                Task<int> readTask = Task.Run(() =>
                {
                    //_logger.Log("READING " + count);
                    int read = _inputStream.Read(data, offset, count);
                    //_logger.Log("READ " + read);
                    return read;
                });

                /*TimeoutResult<int> readResult = await readTask.Timeout(ReceiveTimeout);
                if (readResult.TimedOut)
                {
                    throw new TimeoutException("The socket read operation timed out");
                }

                return readResult.Result;*/
                return await readTask;
            }
        }

        public async Task FlushAsync(CancellationToken cancelToken)
        {
            //_logger.Log("FLUSH");
            //await _socket.OutputStream.FlushAsync();
            await _outputStream.FlushAsync(cancelToken);
        }
    }
}
