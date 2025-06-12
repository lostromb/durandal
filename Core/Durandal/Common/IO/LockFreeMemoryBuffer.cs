using Durandal.Common.Collections;
using Durandal.Common.MathExt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.IO
{
    /// <summary>
    /// Implements a fixed-size, lock-free, producer-consumer byte buffer backed by a circular memory array.
    /// </summary>
    public class LockFreeMemoryBuffer
    {
        private readonly byte[] _buf;
        private readonly int _bufferSize;
        private volatile int _volatileWriteIndex;
        private volatile int _volatileReadIndex;

        /// <summary>
        /// Creates a new lock-free buffer
        /// </summary>
        /// <param name="bufferSize"></param>
        public LockFreeMemoryBuffer(int bufferSize)
        {
            _bufferSize = bufferSize + 1;
            _buf = new byte[_bufferSize];
            _volatileWriteIndex = 0;
            _volatileReadIndex = 0;
        }

        public async Task<int> ReadAnyAsync(byte[] buf, int offset, int count, CancellationToken cancelToken, IRealTimeProvider waitProvider)
        {
            cancelToken.ThrowIfCancellationRequested();

            int readCursor = _volatileWriteIndex;
            while (true)
            {
                int writeCursor = _volatileReadIndex;

                int amountCanSafelyRead;
                if (readCursor < writeCursor)
                {
                    amountCanSafelyRead = writeCursor - readCursor;
                }
                else if (readCursor > writeCursor)
                {
                    amountCanSafelyRead = (_bufferSize - readCursor) + writeCursor;
                }
                else
                {
                    amountCanSafelyRead = 0;
                }

                //Console.WriteLine("READ: RC: " + readCursor + " WC: " + writeCursor + " Amount can safely read: " + amountCanSafelyRead);

                if (amountCanSafelyRead == 0)
                {
                    // No data. Start waiting.
                    await waitProvider.WaitAsync(TimeSpan.FromMilliseconds(1), cancelToken).ConfigureAwait(false);
                    //Console.WriteLine("READ: WAITING");
                }
                else
                {
                    // Do the read in potentially two parts
                    int amountToActuallyRead = FastMath.Min(amountCanSafelyRead, count);
                    int read1 = FastMath.Min(amountToActuallyRead, _bufferSize - readCursor);
                    int read2 = amountToActuallyRead - read1;
                    ArrayExtensions.MemCopy(_buf, readCursor, buf, offset, read1);
                    if (read2 > 0)
                    {
                        ArrayExtensions.MemCopy(_buf, 0, buf, offset + read1, read2);
                    }
                    
                    readCursor = (readCursor + amountToActuallyRead) % _bufferSize;
                    _volatileWriteIndex = readCursor;
                    //Console.WriteLine("Updated WC: " + readCursor + " (index " + readCursorIdx + ")");

                    //Console.WriteLine("Read " + amountToActuallyRead + " bytes");
                    return amountToActuallyRead;
                }
            }
        }

        public async Task<int> ReadAsync(byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider waitProvider)
        {
            int readChunkSize = _bufferSize - 1;
            int received = 0;
            while (received < count && !cancelToken.IsCancellationRequested)
            {
                int nextPacketSize = count - received;
                if (nextPacketSize > readChunkSize)
                {
                    nextPacketSize = readChunkSize;
                }

                // BUGBUG If the stream closes unexpectedly, this will just loop forever
                received += await ReadAnyAsync(data, offset + received, nextPacketSize, cancelToken, waitProvider).ConfigureAwait(false);
            }

            cancelToken.ThrowIfCancellationRequested();

            return received;
        }

        public async Task WriteAsync(byte[] buf, int offset, int count, CancellationToken cancelToken)
        {
            int amountWritten = 0;
            int writeCursor = _volatileReadIndex;

            while (amountWritten < count)
            {
                cancelToken.ThrowIfCancellationRequested();

                int readCursor = _volatileWriteIndex;

                int amountCanSafelyWrite;
                if (writeCursor > readCursor)
                {
                    amountCanSafelyWrite = (_bufferSize - writeCursor) + readCursor - 1;
                }
                else if (writeCursor < readCursor)
                {
                    amountCanSafelyWrite = readCursor - writeCursor - 1;
                }
                else
                {
                    // If cursors are the same, the buffer is empty
                    amountCanSafelyWrite = _bufferSize - 1;
                }

                //Console.WriteLine("WRITE: RC: " + readCursor + " WC: "+ writeCursor + " Amount can safely write: "  + amountCanSafelyWrite);
                if (amountCanSafelyWrite == 0)
                {
                    // Buffer is full. Spinwait
                    //Console.WriteLine("WRITE: WAITING");
                    await Task.Yield();
                }
                else
                {
                    int amountToActuallyWrite = FastMath.Min(count - amountWritten, amountCanSafelyWrite);
                    int write1 = FastMath.Min(_bufferSize - writeCursor, amountToActuallyWrite);
                    int write2 = amountToActuallyWrite - write1;
                    ArrayExtensions.MemCopy(buf, offset + amountWritten, _buf, writeCursor, write1);
                    if (write2 > 0)
                    {
                        ArrayExtensions.MemCopy(buf, offset + amountWritten + write1, _buf, 0, write2);
                    }
                    
                    writeCursor = (writeCursor + amountToActuallyWrite) % _bufferSize;
                    _volatileReadIndex = writeCursor;
                    //Console.WriteLine("Updated WC: " + writeCursor + " (index " + writeCursorIdx + ")");

                    amountWritten += amountToActuallyWrite;
                    //Console.WriteLine("Wrote " + amountToActuallyWrite + " bytes");
                }
            }
        }
    }
}
