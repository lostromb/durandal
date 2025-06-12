using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Collections;
using Durandal.Common.File;
using Durandal.Common.IO;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;

namespace Durandal.Common.Tasks
{
    /// <summary>
    /// This class wraps a Stream and offloads actual read operations to a thread,
    /// which has the effect of making all read calls non-blocking, even if they previously blocked
    /// </summary>
    public class ThreadedStreamReader : IDisposable
    {
        private const int BLOCK_SIZE = 128;
        private readonly CancellationTokenSource _threadCancelizer;
        private readonly BasicBufferByte _buffer;
        private readonly Task _asyncThread;
        private bool _closed;
        private int _disposed = 0;

        public ThreadedStreamReader(Stream inputStream, IRealTimeProvider realTime, int blockSize = 1024, int bufferSize = -1)
        {
            if (bufferSize < 0)
            {
                bufferSize = blockSize * 32;
            }
            if (bufferSize < blockSize * 4)
            {
                throw new ArgumentException("Buffer size must be at least 4x the block size");
            }

            _threadCancelizer = new CancellationTokenSource();
            _closed = false;
            _buffer = new BasicBufferByte(bufferSize);
            CancellationToken cancelToken = _threadCancelizer.Token;
            IRealTimeProvider threadTime = realTime.Fork("ThreadedStreamReader");
            _asyncThread = DurandalTaskExtensions.LongRunningTaskFactory.StartNew(async () =>
                {
                    try
                    {
                        byte[] buf = new byte[BLOCK_SIZE];
                        int check = 0;
                        byte[] checkByte = new byte[1];
                        while (!_closed && !cancelToken.IsCancellationRequested)
                        {
                            // Don't overfill the target buffer; spinwait instead
                            while (_buffer.Available > bufferSize - (BLOCK_SIZE * 4))
                            {
                                await threadTime.WaitAsync(TimeSpan.FromMilliseconds(1), cancelToken).ConfigureAwait(false);
                            }

                            // BLOCKING CALL
                            int bytesRead = await inputStream.ReadAsync(buf, 0, BLOCK_SIZE, cancelToken).ConfigureAwait(false);
                            lock (this)
                            {
                                if (bytesRead == 0)
                                {
                                    // Reached end-of-stream, mark the underlying stream as being closed
                                    _closed = true;
                                    continue;
                                }

                                byte[] newChunk = new byte[bytesRead];
                                ArrayExtensions.MemCopy(buf, 0, newChunk, 0, bytesRead);
                                _buffer.Write(newChunk);
                            }

                            // Use the ReadByte() method to determine if end-of-stream or not (-1 indicates end)
                            check = inputStream.ReadByte();

                            lock (this)
                            {
                                if (check >= 0)
                                {
                                    // If we're not at the end, we have to send this byte down the pipe
                                    checkByte[0] = (byte)check;
                                    _buffer.Write(checkByte);
                                }
                                else
                                {
                                    _closed = true;
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException) { }
                    finally
                    {
                        _closed = true;
                        threadTime.Merge();
                    }
                });
            //_asyncThread.IsBackground = true;
            //_asyncThread.Name = "Threaded Stream Reader";
            //_asyncThread.Start();

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        ~ThreadedStreamReader()
        {
            Dispose(false);
        }

        /// <summary>
        /// Non-blocking read method. This works the same as Stream.Read, except for the interpretation
        /// of the return value:
        /// -1 = End of stream. The stream will never produce more data
        /// 0 = No data available. The stream has no data right now, but could produce more later
        /// >0 = Standard data read of __ bytes
        /// </summary>
        /// <param name="data"></param>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public int Read(byte[] data, int offset, int length)
        {
            lock (this)
            {
                if (EndOfStream)
                {
                    // Definitive end-of-stream
                    return -1;
                }

                int available = System.Math.Min(_buffer.Available, length);
                if (available > 0)
                {
                    using (PooledBuffer<byte> buf = BufferPool<byte>.Rent(available))
                    {
                        _buffer.Read(buf.Buffer, 0, available);
                        ArrayExtensions.MemCopy(buf.Buffer, 0, data, offset, available);
                    }
                }

                return available;
            }
        }

        public int Available()
        {
            lock (this)
            {
                return _buffer.Available;
            }
        }

        public bool EndOfStream
        {
            get
            {
                lock (this)
                {
                    return _closed && _buffer.Available == 0;
                }
            }
        }

        /// <summary>
        /// Dumps all the data in the buffer to the specified output stream, but does not close either pipe.
        /// </summary>
        /// <param name="output"></param>
        public void FlushToStream(Stream output)
        {
            lock (this)
            {
                while (this.Available() > 0)
                {
                    byte[] newBlock = new byte[System.Math.Max(BLOCK_SIZE, Available())];
                    int bytesRead = Read(newBlock, 0, newBlock.Length);
                    if (bytesRead > 0)
                    {
                        output.Write(newBlock, 0, bytesRead);
                    }
                }
            }
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

            _threadCancelizer.Cancel();

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                _threadCancelizer.Dispose();
            }
        }
    }
}
