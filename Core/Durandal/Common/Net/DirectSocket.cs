using Durandal.Common.Time;
using Durandal.Common.Tasks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.IO;
using Durandal.Common.Utils;
using Durandal.Common.Collections;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
using System.Diagnostics;

namespace Durandal.Common.Net
{
    /// <summary>
    /// A fake socket object that uses a BufferedChannel for in-memory channeling, rather than using the network
    /// </summary>
    public class DirectSocket : ISocket
    {
        private static readonly IReadOnlyDictionary<SocketFeature, object> DEFAULT_FEATURES =
            new SmallDictionary<SocketFeature, object>(new Dictionary<SocketFeature, object>()
                { { SocketFeature.MemorySocket, null } });

        private readonly WeakPointer<BufferedChannel<Packet>> _read;
        private readonly BufferedChannel<Packet> _write;
        private readonly StackBuffer _unreadBuffer = new StackBuffer();

        // The number of milliseconds of accumulated wait before we trigger an actual Wait operation
        private const double THROTTLING_WAIT_THRESHOLD_MS = 10;
        // the amount of time that it costs to write a single byte with current bandwidth constraints
        private double _waitAccumulationRateMsPerByte = 0;

        // the current level of accumulated wait that recent reads/writes have accrued. Trigger a wait
        private double _throttlingWaitDeficitMsWriteEnd = 0;
        private double _throttlingWaitDeficitMsReadEnd = 0;

        // The current simulated bandwidth cap for each independent socket direction
        private double? _currentBandwidthCapBytesPerSecond;

        private NetworkDuplex _closedEnds = NetworkDuplex.Unknown;
        private Packet _curPacket = null;
        private int _curPacketIdx = 0;
        private int _disposed = 0;

        // atomic flags used to fail fast on concurrency errors which might come up in tests from other classes that use sockets
        private int _threadWriting = 0;
        private int _threadReading = 0;

        private DirectSocket(
            WeakPointer<BufferedChannel<Packet>> readBuffer,
            BufferedChannel<Packet> writeBuffer,
            TimeSpan? simulatedLatency,
            double? simulatedBandwidth)
        {
            _read = readBuffer.AssertNonNull(nameof(readBuffer));
            _write = writeBuffer.AssertNonNull(nameof(writeBuffer));
            SimulatedLatency = simulatedLatency;
            BandwidthCapBytesPerSecond = simulatedBandwidth;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        /// <summary>
        /// Gets or sets a simulated latency to apply between the two endpoints of this socket. Null is the same as TimeSpan.Zero;
        /// </summary>
        public TimeSpan? SimulatedLatency { get; set; }

        /// <summary>
        /// Gets or sets the maximum transmission speed in any single direction of this socket. The cap is enforced by rough waits
        /// of about 10 milliseconds each as data is read or written.
        /// </summary>
        public double? BandwidthCapBytesPerSecond
        {
            get
            {
                return _currentBandwidthCapBytesPerSecond;
            }
            set
            {
                _currentBandwidthCapBytesPerSecond = value;
                if (value.HasValue)
                {
                    _waitAccumulationRateMsPerByte = 1000.0 / _currentBandwidthCapBytesPerSecond.Value;
                }
                else
                {
                    _waitAccumulationRateMsPerByte = 0;
                }
            }
        }

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
        public string RemoteEndpointString
        {
            get
            {
                return "mem://";
            }
        }

        /// <inheritdoc />
        public IReadOnlyDictionary<SocketFeature, object> Features => DEFAULT_FEATURES;

        /// <inheritdoc />
        public Task Disconnect(CancellationToken cancelToken, IRealTimeProvider realTime, NetworkDuplex which = NetworkDuplex.ReadWrite, bool allowLinger = false)
        {
            bool closingWrite = !_closedEnds.HasFlag(NetworkDuplex.Write) &&
                which.HasFlag(NetworkDuplex.Write);

            bool closingRead = !_closedEnds.HasFlag(NetworkDuplex.Read) &&
                which.HasFlag(NetworkDuplex.Read);

            if (closingWrite)
            {
                _write.Send(new Packet(null, true, 0)); // send end of stream to remote endpoint
            }

            _closedEnds = _closedEnds | which;
            if (_closedEnds == NetworkDuplex.ReadWrite)
            {
                Dispose();
            }

            return DurandalTaskExtensions.NoOpTask;
        }

        /// <inheritdoc />
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
                // Each half of the socket pair will dispose of its Write channel.
                // Over the two sockets in the pair this will end up disposing of both channels properly
                _write.Dispose();
                _unreadBuffer.Dispose();
                _curPacket?.Data?.Dispose();
            }
        }

        /// <inheritdoc />
        public Task FlushAsync(CancellationToken cancelToken, IRealTimeProvider waitProvider)
        {
            if (Interlocked.CompareExchange(ref _threadWriting, 1, 0) != 0)
            {
                throw new InvalidOperationException("Concurrent writes detected on the same socket. This is a fast-fail exception as code should never be doing this");
            }
            try
            {
                if (_closedEnds.HasFlag(NetworkDuplex.Write))
                {
                    throw new InvalidOperationException("Cannot flush after closing write end of socket");
                }
            }
            finally
            {
                _threadWriting = 0;
            }

            return DurandalTaskExtensions.NoOpTask;
        }

        /// <inheritdoc />
        public async Task<int> ReadAsync(byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider waitProvider)
        {
            int unreadBytes = _unreadBuffer.Read(data, offset, count);
            if (unreadBytes == count)
            {
                return count;
            }

            return await SocketHelpers.ReliableRead(this, data, offset + unreadBytes, count - unreadBytes, cancelToken, waitProvider);
        }

        /// <inheritdoc />
        public async Task<int> ReadAnyAsync(byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider waitProvider)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(DirectSocket), "Cannot read from a disposed socket");
            }

            if (_closedEnds.HasFlag(NetworkDuplex.Read))
            {
                throw new InvalidOperationException("Cannot read after closing read end of socket");
            }

            if (Interlocked.CompareExchange(ref _threadReading, 1, 0) != 0)
            {
                throw new InvalidOperationException("Concurrent reads detected on the same socket. This is a fast-fail exception as code should never be doing this");
            }
            try
            {
                // First, try to read from the unread buffer
                int unreadBytes = _unreadBuffer.Read(data, offset, count);
                if (unreadBytes > 0)
                {
                    // If we have just some data from the unread buffer, return it as a partial read because a read from the actual socket may block.
                    return unreadBytes;
                }

                cancelToken.ThrowIfCancellationRequested();

                // Queue up a new chunk of data if needed
                if (_curPacket == null)
                {
                    // could be first packet or could be a new packet after an empty interval, so make sure we reset the index
                    _curPacketIdx = 0;
                    _curPacket = await _read.Value.ReceiveAsync(cancelToken, waitProvider).ConfigureAwait(false);
                }
                else if (_curPacketIdx >= _curPacket.Data.Length)
                {
                    // Recycle old buffer
                    _curPacket.Data.Dispose();
                    _curPacket = null; // make curpacket null just in case the next line gets canceled, so we don't retain a handle to the stale pooled buffer
                    _curPacketIdx = 0;
                    _curPacket = await _read.Value.ReceiveAsync(cancelToken, waitProvider).ConfigureAwait(false);
                }

                // this case happens if the buffered stream is disposed
                if (_curPacket == null)
                {
                    return 0;
                }

                // Is the current packet an end of stream signal?
                if (_curPacket.IsEndOfStream)
                {
                    await Disconnect(cancelToken, waitProvider, NetworkDuplex.Read).ConfigureAwait(false);
                    return 0;
                }

                if (SimulatedLatency.HasValue && SimulatedLatency.Value > TimeSpan.Zero)
                {
                    // Apply simulated latency if needed
                    TimeSpan timeUntilPacketArrives = new TimeSpan(_curPacket.WriteTime + SimulatedLatency.Value.Ticks - waitProvider.TimestampTicks);
                    if (timeUntilPacketArrives > TimeSpan.Zero)
                    {
                        await waitProvider.WaitAsync(timeUntilPacketArrives, cancelToken).ConfigureAwait(false);
                    }
                }

                // Read whatever is available in this chunk, could be all of it, could be just a segment
                int toRead = FastMath.Min(_curPacket.Data.Length - _curPacketIdx, count);
                ArrayExtensions.MemCopy(_curPacket.Data.Buffer, _curPacketIdx, data, offset, toRead);
                _curPacketIdx += toRead;

                // Finally, we may have to do another wait to simulate throttled bandwidth
                _throttlingWaitDeficitMsReadEnd = await ThrottleBandwidthIfNeeded(
                    toRead,
                    _throttlingWaitDeficitMsReadEnd,
                    _waitAccumulationRateMsPerByte,
                    waitProvider,
                    cancelToken).ConfigureAwait(false);

                return toRead;
            }
            finally
            {
                _threadReading = 0;
            }
        }

        /// <inheritdoc />
        public async Task WriteAsync(byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider waitProvider)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(DirectSocket), "Cannot write to a disposed socket");
            }

            if (_closedEnds.HasFlag(NetworkDuplex.Write))
            {
                throw new InvalidOperationException("Cannot write after closing write end of socket");
            }

            if (Interlocked.CompareExchange(ref _threadWriting, 1, 0) != 0)
            {
                throw new InvalidOperationException("Concurrent writes detected on the same socket. This is a fast-fail exception as code should never be doing this");
            }
            try
            {
                const int MAX_PACKET_SIZE = 32768;
                int bytesWritten = 0;
                while (bytesWritten < count)
                {
                    int thisBlockSize = Math.Min(MAX_PACKET_SIZE, (count - bytesWritten));
                    PooledBuffer<byte> block = BufferPool<byte>.Rent(thisBlockSize);
                    ArrayExtensions.MemCopy(data, offset + bytesWritten, block.Buffer, 0, thisBlockSize);
                    await _write.SendAsync(new Packet(block, false, waitProvider.TimestampTicks)).ConfigureAwait(false);
                    bytesWritten += thisBlockSize;

                    // Potentially throttle bandwidth in between sending each packet.
                    // This also more accurately simulates the latency between each individual
                    // packet when the bandwidth is slow, as opposed to just sending a giant
                    // packet and then doing a long wait aftarwards
                    _throttlingWaitDeficitMsWriteEnd = await ThrottleBandwidthIfNeeded(
                        thisBlockSize,
                        _throttlingWaitDeficitMsWriteEnd,
                        _waitAccumulationRateMsPerByte,
                        waitProvider,
                        cancelToken).ConfigureAwait(false);
                }
            }
            finally
            {
                _threadWriting = 0;
            }
        }

        /// <inheritdoc />
        public void Unread(byte[] buffer, int offset, int count)
        {
            _unreadBuffer.Write(buffer, offset, count);
        }

        /// <summary>
        /// Creates a virtual socket connection represented as a pair of sockets, the first one for client, the second one a server
        /// </summary>
        /// <param name="simulatedLatency">The simulated latency that should be applied between the two virtual socket endpoints.</param>
        /// <returns>A pair of sockets, one for client and one for server</returns>
        public static DirectSocketPair CreateSocketPair(
            TimeSpan? simulatedLatency = null,
            double? simulatedBandwidthBytesPerSecond = null)
        {
            BufferedChannel<Packet> duplexA = new BufferedChannel<Packet>();
            BufferedChannel<Packet> duplexB = new BufferedChannel<Packet>();
            DirectSocket clientSocket = new DirectSocket(new WeakPointer<BufferedChannel<Packet>>(duplexB), duplexA, simulatedLatency, simulatedBandwidthBytesPerSecond);
            DirectSocket serverSocket = new DirectSocket(new WeakPointer<BufferedChannel<Packet>>(duplexA), duplexB, simulatedLatency, simulatedBandwidthBytesPerSecond);
            return new DirectSocketPair(clientSocket, serverSocket);
        }

        private static async Task<double> ThrottleBandwidthIfNeeded(
            int bytesTransferred,
            double currentTimeDeficit,
            double currentAccumulationRate,
            IRealTimeProvider realTime,
            CancellationToken cancelToken)
        {
            if (currentAccumulationRate == 0 ||
                bytesTransferred <= 0)
            {
                return currentTimeDeficit;
            }

            currentTimeDeficit += (bytesTransferred * currentAccumulationRate);
            if (currentTimeDeficit > THROTTLING_WAIT_THRESHOLD_MS)
            {
                long waitStartTime = realTime.TimestampTicks;
                await realTime.WaitAsync(TimeSpanExtensions.TimeSpanFromMillisecondsPrecise(currentTimeDeficit), cancelToken).ConfigureAwait(false);
                currentTimeDeficit -= (realTime.TimestampTicks - waitStartTime) / TimeSpan.TicksPerMillisecond;
            }

            return currentTimeDeficit;
        }

        /// <summary>
        /// Internal class used to track what data was sent and when, so we can simulate actual network transmit latency
        /// </summary>
        private class Packet
        {
            public Packet(PooledBuffer<byte> data, bool isEndOfStream, long writeTimeTicks)
            {
                Data = data;
                IsEndOfStream = isEndOfStream;
                WriteTime = writeTimeTicks;
            }

            public readonly PooledBuffer<byte> Data;
            public readonly long WriteTime;
            public bool IsEndOfStream;
        }
    }
}
