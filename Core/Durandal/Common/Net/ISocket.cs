using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net
{
    /// <summary>
    /// Represents an abstract network socket, comparable to a Win32 socket in semantics
    /// </summary>
    public interface ISocket : IDisposable
    {
        /// <summary>
        /// The receive timeout in milliseconds
        /// </summary>
        int ReceiveTimeout { get; set; }

        /// <summary>
        /// A string representing the remote endpoint of this socket, for informational purposes
        /// </summary>
        string RemoteEndpointString { get; }

        /// <summary>
        /// Writes data from the specified buffer to the socket asynchronously. Will potentially block until all data is written.
        /// </summary>
        /// <param name="data">A buffer containing data to be written</param>
        /// <param name="offset">The read offset in the buffer</param>
        /// <param name="count">The number of bytes to write</param>
        /// <param name="cancelToken">A token to cancel this operation</param>
        /// <param name="waitProvider">A definition of real time - only relevant for non-realtime unit tests.</param>
        Task WriteAsync(byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider waitProvider);

        /// <summary>
        /// Reads data from the socket into the specified buffer. This is a "reliable" read as it will usually never return partial results.
        /// This means that your protocol has to ensure that the desired # of bytes are actually coming, otherwise you will block forever.
        /// Other than that the read follows the semantics of normal C# stream reads, specifically:
        /// - Return zero if end of stream. If stream is still open, block until non-zero number of bytes are available
        /// - Return non-zero to indicate the number of bytes that were read
        /// - If the return value is less than the requested bytes, it means the stream ended prematurely and this is all the data remaining.
        /// </summary>
        /// <param name="data">A target buffer to put the read data into</param>
        /// <param name="offset">The write offset in the buffer</param>
        /// <param name="count">The total number of bytes to read. The read will block until exactly this many are received.</param>
        /// <param name="cancelToken">A token to cancel this operation</param>
        /// <param name="waitProvider">A definition of real time - only relevant for non-realtime unit tests.</param>
        /// <returns>The number of bytes that were read, or 0 to indicate end-of-stream</returns>
        Task<int> ReadAsync(byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider waitProvider);

        /// <summary>
        /// Reads the first available data from the socket into the specified buffer. This should be considered a "non-reliable" read
        /// because the amount read may be less than the amount requested depending on what is available.
        /// The read follows the semantics of normal C# stream reads, specifically:
        /// - Return zero IFF end of stream. If stream is still open, block until non-zero number of bytes are available
        /// - Return non-zero to indicate the number of bytes that were read
        /// - The actual number of bytes read may be less than what is requested
        /// </summary>
        /// <param name="data">A target buffer to put the read data into</param>
        /// <param name="offset">The write offset in the buffer</param>
        /// <param name="count">The maximum number of bytes to read</param>
        /// <param name="cancelToken">A token to cancel this operation</param>
        /// <param name="waitProvider">A definition of real time - only relevant for non-realtime unit tests.</param>
        /// <returns>The number of bytes that were read, or 0 to indicate end-of-stream</returns>
        Task<int> ReadAnyAsync(byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider waitProvider);

        /// <summary>
        /// Rewinds the socket by returning data that was previously read, so it will be read again by the next caller of Read().
        /// All implementations of ISocket must support this feature in someway, usually with a variable-length buffer to hold the
        /// returned data.
        /// </summary>
        /// <param name="data">The data to unread</param>
        /// <param name="offset">Offset into data array</param>
        /// <param name="count">Number of bytes to unread</param>
        void Unread(byte[] data, int offset, int count);

        /// <summary>
        /// Commits pending writes in the buffer to the underlying stream
        /// </summary>
        /// <param name="cancelToken">A token to cancel this operation</param>
        /// <param name="waitProvider">A definition of real time - only relevant for non-realtime unit tests.</param>
        Task FlushAsync(CancellationToken cancelToken, IRealTimeProvider waitProvider);

        /// <summary>
        /// Closes this socket connection. Depending on the implementation, this may be a "soft-disconnect", i.e. the connection
        /// remains lingering in a connection pool. This should be used in place of Dispose() in most user code. Disposing of a socket
        /// will always completely disconnect it.
        /// </summary>
        /// <param name="cancelToken">A token to cancel this operation</param>
        /// <param name="waitProvider">A definition of real time - only relevant for non-realtime unit tests.</param>
        /// <param name="which">The socket directions to disconnect. The requested operation will no longer be allowed
        /// on this endpoint after closing the corresponding direction.</param>
        /// <param name="allowLinger">If true, allow connection pools to reclaim this socket after this method finishes</param>
        Task Disconnect(CancellationToken cancelToken, IRealTimeProvider waitProvider, NetworkDuplex which = NetworkDuplex.ReadWrite, bool allowLinger = false);

        /// <summary>
        /// Returns a dictionary of features that this socket has been configured with or which were established
        /// during the connection. A typical example would be whether the connection is encrypted with TLS, or
        /// whether HTTP/2 support was negotiated during the handshake.
        /// The value type is dependent on the feature itself, and may be null.
        /// </summary>
        IReadOnlyDictionary<SocketFeature, object> Features { get; }
    }
}
