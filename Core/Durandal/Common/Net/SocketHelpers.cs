using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net
{
    public static class SocketHelpers
    {
        /// <summary>
        /// Reliably read a certain number of bytes from the socket. A "reliable" read is defined as follows:
        /// Attempt to read exactly the requested number of bytes from the socket. Any other number from 0 to (size - 1) indicates that the stream is closed and there are no more bytes available.
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="data"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="cancelToken"></param>
        /// <param name="waitProvider">real time definition</param>
        /// <param name="readChunkSize">The maximum size of each read</param>
        /// <returns></returns>
        public static async Task<int> ReliableRead(
            ISocket socket,
            byte[] data,
            int offset,
            int count,
            CancellationToken cancelToken,
            IRealTimeProvider waitProvider,
            int readChunkSize = 65536)
        {
            if (readChunkSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(readChunkSize));
            }

            int received = 0;
            while (received < count && !cancelToken.IsCancellationRequested)
            {
                int nextPacketSize = count - received;
                if (nextPacketSize > readChunkSize)
                {
                    nextPacketSize = readChunkSize;
                }

                int returnVal = await socket.ReadAnyAsync(data, offset + received, nextPacketSize, cancelToken, waitProvider).ConfigureAwait(false);
                if (returnVal <= 0)
                {
                    throw new EndOfStreamException();
                }

                received += returnVal;
            }

            cancelToken.ThrowIfCancellationRequested();

            return received;
        }
        
        /// <summary>
        /// Writes a large buffer to the socket in small pieces, to ensure compatability with potentially small write buffers
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="sourceBuffer"></param>
        /// <param name="offset"></param>
        /// <param name="size"></param>
        /// <param name="cancelToken"></param>
        /// <param name="realTime"></param>
        /// <param name="writeChunkSize"></param>
        /// <returns></returns>
        public static async Task PiecewiseWrite(ISocket socket, byte[] sourceBuffer, int offset, int size, CancellationToken cancelToken, IRealTimeProvider realTime, int writeChunkSize = 256)
        {
            if (writeChunkSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(writeChunkSize));
            }

            int sent = 0;
            while (sent < size)
            {
                int thisPacketSize = Math.Min(writeChunkSize, size - sent);
                await socket.WriteAsync(sourceBuffer, sent + offset, thisPacketSize, cancelToken, realTime).ConfigureAwait(false);
                sent += thisPacketSize;
            }
        }

        /// <summary>
        /// Tests whether the given exception is a SocketException or IOException that indicates a
        /// network connection was forcibly closed.
        /// </summary>
        /// <param name="e">The exception to test.</param>
        /// <returns>True if it is a socket close exception.</returns>
        public static bool DoesExceptionIndicateSocketClosed(Exception e)
        {
            string exceptionType = e.GetType().Name;
            return (string.Equals("SocketException", exceptionType, StringComparison.Ordinal) || string.Equals("IOException", exceptionType, StringComparison.Ordinal)) &&
                (e.Message.Contains("forcibly closed") || e.Message.Contains("established connection was aborted"));
        }

        /// <summary>
        /// Tests whether the given exception is a SocketException or IOException that indicates a
        /// network connection attempt was refused by the remote host.
        /// </summary>
        /// <param name="e">The exception to test.</param>
        /// <returns>True if it is a connection refused exception.</returns>
        public static bool DoesExceptionIndicateConnectionRefused(Exception e)
        {
            string exceptionType = e.GetType().Name;
            return (string.Equals("SocketException", exceptionType, StringComparison.Ordinal) || string.Equals("IOException", exceptionType, StringComparison.Ordinal)) &&
                (e.Message.Contains("target machine actively refused it"));
        }
    }
}
