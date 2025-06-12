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
using DurandalNet = Durandal.Common.Net;
using JavaNet = Java.Net;

namespace Durandal.AndroidClient.Common
{
    public class AndroidSocket : DurandalNet.ISocket
    {
        private JavaNet.Socket _socket;
        private Stream _inputStream;
        private Stream _outputStream;
        private StackBuffer _unreadBuffer;

        public AndroidSocket(JavaNet.Socket socket)
        {
            _socket = socket;
            _inputStream = _socket.InputStream;
            _outputStream = _socket.OutputStream;
            _unreadBuffer = new StackBuffer();
            Dictionary<SocketFeature, object> features = new Dictionary<SocketFeature, object>();
            Features = features;
        }

        public int ReceiveTimeout
        {
            get
            {
                return _socket.SoTimeout;
            }

            set
            {
                _socket.SoTimeout = value;
            }
        }

        public string RemoteEndpointString
        {
            get
            {
                return _socket.RemoteSocketAddress.ToString();
            }
        }

        public IReadOnlyDictionary<SocketFeature, object> Features { get; private set; }

        public void Dispose()
        {
            _socket?.Dispose();
            _unreadBuffer?.Dispose();
        }
        public Task Disconnect(
            CancellationToken cancelToken,
            IRealTimeProvider waitProvider,
            NetworkDuplex which = NetworkDuplex.ReadWrite,
            bool allowLinger = false)
        {
            switch (which)
            {
                case NetworkDuplex.ReadWrite:
                    _socket.Close();
                    break;
                case NetworkDuplex.Write:
                    if (_socket.IsInputShutdown)
                    {
                        _socket.Close();
                    }
                    else
                    {
                        _socket.ShutdownOutput();
                    }
                    break; ;
                case NetworkDuplex.Read:
                    if (_socket.IsOutputShutdown)
                    {
                        _socket.Close();
                    }
                    else
                    {
                        _socket.ShutdownInput();
                    }
                    break;
            }

            return DurandalTaskExtensions.NoOpTask;
        }

        /// <inheritdoc />
        public Task FlushAsync(CancellationToken cancelToken, IRealTimeProvider waitProvider)
        {
            return _outputStream.FlushAsync(cancelToken);
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

            return await _inputStream.ReadAsync(data, offset, count, cancelToken);
        }

        /// <inheritdoc />
        public Task WriteAsync(byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider waitProvider)
        {
            return _outputStream.WriteAsync(data, offset, count, cancelToken);
        }
        public Stream CreateMultiplexedStream()
        {
            return new MultiplexedStream(_inputStream, _outputStream);
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

        private class MultiplexedStream : Stream
        {
            private Stream _input;
            private Stream _output;

            public MultiplexedStream(Stream input, Stream output)
            {
                _input = input;
                _output = output;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => _input.Length;

            public override long Position
            {
                get
                {
                    return _input.Position;
                }

                set
                {
                    throw new NotImplementedException();
                }
            }

            public override void Flush()
            {
                _output.Flush();
            }
            
            public override Task FlushAsync(CancellationToken token)
            {
                return _output.FlushAsync(token);
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return _input.Read(buffer, offset, count);
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return _input.ReadAsync(buffer, offset, count, cancellationToken);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                _output.Write(buffer, offset, count);
            }

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return _output.WriteAsync(buffer, offset, count, cancellationToken);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotImplementedException();
            }

            public override void SetLength(long value)
            {
                throw new NotImplementedException();
            }
        }
    }
}