using Durandal.Common.Utils;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Instrumentation;
using System.Diagnostics;
using Durandal.Common.Instrumentation.Profiling;
using System.Runtime.Versioning;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Net
{
    /// <summary>
    /// Implements a lock-free producer-consumer buffer backed by a non-persistent memory-mapped file, which can be used as an IPC data pipe.
    /// This is a single-duplex connection that only works on 1:1 producer-consumer situations.
    /// </summary>
    public class MemoryMappedFileStream : IDisposable
    {
        // This implementation is based on a ring buffer with 2 volatile indexes for read + write cursors. These are stored as extra header data
        // preceding the actual buffer data inside the stream.
        // Since we can't guarantee atomic read/write to 32-bit values in a random access stream, we have to add another layer of indirection
        // where a single byte specifies the "slot" of the cursor we are updating, and then an array of 4-byte slots contains the actual values
        // for the cursors. That way we can do a 4-byte write to the slot, and then update the single-byte slot index atomically to guarantee
        // that nobody can read an invalid value. Astute observers will note that there is still technically a race condition in this code.
        // If the read process reads the slot ID but not the value from the slot, and then the write process performs lots of consecutive write operations and then
        // updates the write index at the exact same time the read process is reading it, the read process can possibly receive an invalid value.
        // However, it is so astronomically improbable to happen during actual execution that it's safe not to worry about.

        private const int BUFFER_SIZE_INDEX = 0;
        private const int READ_INDEX_SLOT_ID = 4;
        private const int WRITE_INDEX_SLOT_ID = 5;
        private const int READ_INDEX_BASE = 6;
        private const int NUM_ATOMIC_SAFE_SLOTS = 256; // must be 256 or less. In practice this could probably be between 2 and 4, but we are insanely paranoid
        private const int WRITE_INDEX_BASE = READ_INDEX_BASE + (NUM_ATOMIC_SAFE_SLOTS * 4);
        private const int HEADER_SIZE = WRITE_INDEX_BASE + (NUM_ATOMIC_SAFE_SLOTS * 4);

        // name of kernel memory mapped file
        private static readonly string MMIO_FILE_PREFIX = ".mmio-file-";
        // name of kernel handle for the "data ready" EventWaitHandle
        private static readonly string MMIO_SYNC_PREFIX = ".mmio-sync-";

        private int _streamHash = 0;

        private const int SPINWAIT_INCREMENT_MS = 50;
        private static readonly TimeSpan SPINWAIT_INCREMENT = TimeSpan.FromMilliseconds(SPINWAIT_INCREMENT_MS);

        // Memory layout:
        // 1. Buffer size (int32)
        // 2. Read index slot ID (byte) - used for atomically updating the 32-bit read index value
        // 3. Write index slot ID (byte) - used for atomically updating the 32-bit write index value
        // 4. Read index slots (int32 * NUM_ATOMIC_SAFE_SLOTS) - stores actual values for read index
        // 5. Write index slots (int32 * NUM_ATOMIC_SAFE_SLOTS) - stores actual values for write index
        // 6. Remaining bytes: the buffer itself

        private readonly MemoryMappedFile _mmf; // Handle to the non-persistent memory mapped file
        private readonly MemoryMappedViewAccessor _buf; // Random access to file

        /// <summary>
        /// Signal that there is data available in this file. This is implemented as a global (kernel-level) event wait handle
        /// with a name equal to ".mmio-sync-{FILENAME}"
        /// </summary>
        private readonly EventWaitHandle _kernelWriteSignal;
        private readonly RegisteredWaitHandle _kernelThreadCallbackHandle; // This is a threadpool callback registration which triggers an event each time kernelWriteSignal is set
        private readonly SemaphoreSlim _localWriteSignal; // When that callback happens, the changes to kernelWriteSignal are translated to this local write signal
        private readonly WeakPointer<IMetricCollector> _metrics;
        private readonly DimensionSet _dimensions;
        private readonly ILogger _logger;
        private readonly int _bufferSize;
        private int _disposed = 0;

        /// <summary>
        /// Creates a new lock-free buffer using a newly created memory-mapped file. This object will then own the handle for that file.
        /// </summary>
        /// <param name="newStreamName">The name of the stream (usually a few random characters)</param>
        /// <param name="bufferSize">The size of the file buffer to use, in bytes</param>
        /// <param name="logger">A logger for the stream</param>
        /// <param name="metrics">A metric collector</param>
        /// <param name="dimensions">Dimensions for reporting metrics</param>
        /// <param name="isReadEnd">Indicates whether this is the read end of the stream. Since streams are one-way, you can only either read or write.</param>
        public MemoryMappedFileStream(
            string newStreamName,
            int bufferSize,
            ILogger logger,
            WeakPointer<IMetricCollector> metrics,
            DimensionSet dimensions,
            bool isReadEnd)
        {
            ReadTimeout = -1;
            _bufferSize = bufferSize + 1; // add 1 because we can't actually fill the buffer to desired capacity because the write cursor can never pass the read cursor
            _metrics = metrics;
            _dimensions = dimensions;
            _logger = logger.AssertNonNull(nameof(logger));
            _logger.Log("Creating new memory-mapped stream with name " + newStreamName, LogLevel.Vrb);
            _localWriteSignal = new SemaphoreSlim(0, 1000); // ostrich problem - we pretend there's not a concurrency issue here by giving a lot of headroom for overflow
            _kernelWriteSignal = new EventWaitHandle(false, EventResetMode.AutoReset, MMIO_SYNC_PREFIX + newStreamName);
#if NETCOREAPP
            _mmf = MemoryMappedFile.CreateNew(
                MMIO_FILE_PREFIX + newStreamName,
                HEADER_SIZE + _bufferSize,
                MemoryMappedFileAccess.ReadWrite,
                MemoryMappedFileOptions.None,
                HandleInheritability.None);
#else
            _mmf = MemoryMappedFile.CreateNew(
                MMIO_FILE_PREFIX + newStreamName,
                HEADER_SIZE + _bufferSize,
                MemoryMappedFileAccess.ReadWrite,
                MemoryMappedFileOptions.None,
                null,
                HandleInheritability.None);
#endif

            _buf = _mmf.CreateViewAccessor();
            _streamHash = newStreamName.GetHashCode();

            // Write the buffer size to the file
            _buf.Write(BUFFER_SIZE_INDEX, _bufferSize);
            _buf.Flush();

            // And register the thread callback which converts signals on the kernel write signal to the local write signal
            // We only consume this signal on the read end of the pipe - if we did it on the write end we would just signal ourselves
            if (isReadEnd)
            {
                _kernelThreadCallbackHandle = ThreadPool.RegisterWaitForSingleObject(_kernelWriteSignal, KernelWriteSignalCallback, _localWriteSignal, -1, false);
            }

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        /// <summary>
        /// Creates a new lock-free buffer using an existing memory-mapped file.
        /// </summary>
        /// <param name="existingStreamName">The unique stream name to open (usually a few random characters)</param>
        /// <param name="logger">A logger for the stream</param>
        /// <param name="metrics">A metric collector</param>
        /// <param name="dimensions">Dimensions to use for the metrics</param>
        /// <param name="isReadEnd">Indicates whether this is the read end of the stream. Since streams are one-way, you can only either read or write.</param>
#if NETCOREAPP
        [SupportedOSPlatform("windows")]
#endif
        public MemoryMappedFileStream(string existingStreamName, ILogger logger, IMetricCollector metrics, DimensionSet dimensions, bool isReadEnd)
        {
            ReadTimeout = -1;
            logger.Log("Opening existing memory-mapped stream with name " + existingStreamName, LogLevel.Vrb);

            _metrics = new WeakPointer<IMetricCollector>(metrics ?? NullMetricCollector.Singleton);
            _dimensions = dimensions ?? DimensionSet.Empty;
            _localWriteSignal = new SemaphoreSlim(0, 1000); // ostrich problem - we pretend there's not a concurrency issue here by giving a lot of headroom for overflow
            _kernelWriteSignal = new EventWaitHandle(false, EventResetMode.AutoReset, MMIO_SYNC_PREFIX + existingStreamName);
            _mmf = MemoryMappedFile.OpenExisting(
                MMIO_FILE_PREFIX + existingStreamName,
                MemoryMappedFileRights.ReadWrite,
                HandleInheritability.None);
            _buf = _mmf.CreateViewAccessor();
            _streamHash = existingStreamName.GetHashCode();
            _bufferSize = _buf.ReadInt32(BUFFER_SIZE_INDEX);
            logger.Log("Reported buffer size is " + _bufferSize, LogLevel.Vrb);

            // And register the thread callback which converts signals on the kernel write signal to the local write signal
            // We only consume this signal on the read end of the pipe - if we did it on the write end we would just signal ourselves
            if (isReadEnd)
            {
                _kernelThreadCallbackHandle = ThreadPool.RegisterWaitForSingleObject(_kernelWriteSignal, KernelWriteSignalCallback, _localWriteSignal, -1, false);
            }

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~MemoryMappedFileStream()
        {
            Dispose(false);
        }
#endif

        public int ReadTimeout { get; set; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Gets the approximate percentage of the buffer space that is used as a decimal between 0 and 100
        /// </summary>
        public double PercentBufferUsed
        {
            get
            {
                byte readCursorIdx = _buf.ReadByte(READ_INDEX_SLOT_ID);
                int readCursor = _buf.ReadInt32(READ_INDEX_BASE + (4 * readCursorIdx));
                byte writeCursorIdx = _buf.ReadByte(WRITE_INDEX_SLOT_ID);
                int writeCursor = _buf.ReadInt32(WRITE_INDEX_BASE + (4 * writeCursorIdx));

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

                return 100d * (double)amountCanSafelyRead / (double)_bufferSize;
            }
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
                _kernelThreadCallbackHandle?.Unregister(_kernelWriteSignal);
                _buf?.Dispose();
                _mmf?.Dispose();
                _kernelWriteSignal?.Dispose();
                _localWriteSignal?.Dispose();
            }
        }

        public async Task<int> ReadAnyAsync(byte[] buf, int offset, int count, CancellationToken cancelToken, IRealTimeProvider waitProvider)
        {
            cancelToken.ThrowIfCancellationRequested();
            uint operationId = MicroProfiler.GenerateOperationId();
            MicroProfiler.Send(MicroProfilingEventType.MMIO_Read_EnterReadAnyMethod, operationId, _streamHash);
            ValueStopwatch readTimer = ValueStopwatch.StartNew();
            ValueStopwatch paranoidSpinwaitTimer = new ValueStopwatch();

            byte readCursorIdx = _buf.ReadByte(READ_INDEX_SLOT_ID);
            int readCursor = _buf.ReadInt32(READ_INDEX_BASE + (4 * readCursorIdx));
            int msWaited = 0;
            while (!cancelToken.IsCancellationRequested)
            {
                if (_disposed != 0)
                {
                    // Gracefully disconnect readers
                    return 0;
                }

                byte writeCursorIdx = _buf.ReadByte(WRITE_INDEX_SLOT_ID);
                int writeCursor = _buf.ReadInt32(WRITE_INDEX_BASE + (4 * writeCursorIdx));

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
                MicroProfiler.Send(MicroProfilingEventType.MMIO_Read_PreRead, operationId, amountCanSafelyRead);
                if (amountCanSafelyRead == 0)
                {
                    // No data. Start waiting.
                    paranoidSpinwaitTimer.Restart();
                    _metrics.Value.ReportPercentile(CommonInstrumentation.Key_Counter_MMIO_BufferUsage, _dimensions, 0);

                    MicroProfiler.Send(MicroProfilingEventType.MMIO_Read_Spinwait_Start, operationId);
                    if (waitProvider.IsForDebug)
                    {
                        // Debug scenario.
                        // Wait on the write signal for up to 50 ms before just attempting to read again.
                        // If this wait happens too much (and a read timeout is configured) then we return a timeout response
                        if (!(await _localWriteSignal.WaitAsync(SPINWAIT_INCREMENT, cancelToken)))
                        {
                            msWaited += SPINWAIT_INCREMENT_MS;
                        }

                        // Make sure we consume virtual time too
                        await waitProvider.WaitAsync(SPINWAIT_INCREMENT, cancelToken).ConfigureAwait(false);

                        if (ReadTimeout > 0 && msWaited >= ReadTimeout)
                        {
                            throw new TimeoutException("MMIO read timed out after " + ReadTimeout + " ms");
                        }
                    }
                    else
                    {
                        bool gotSignal = await _localWriteSignal.WaitAsync(ReadTimeout, cancelToken).ConfigureAwait(false);
                        if (!gotSignal)
                        {
                            throw new TimeoutException("MMIO read timed out after " + ReadTimeout + " ms");
                        }
                    }

                    paranoidSpinwaitTimer.Stop();
#if PARANOID_MMIO_METRICS
                    MicroProfiler.Send(MicroProfilingEventType.MMIO_Read_Spinwait_End, operationId, (int)paranoidSpinwaitTimer.ElapsedMillisecondsPrecise());
                    _metrics.Value.ReportPercentile(CommonInstrumentation.Key_Counter_MMIO_ReadSingleSpinwaitTime, paranoidSpinwaitTimer.ElapsedMillisecondsPrecise(), _dimensions);
#endif
                }
                else
                {
                    // Do the read in potentially two parts
                    readTimer.Stop();
#if PARANOID_MMIO_METRICS
                    _metrics.Value.ReportPercentile(CommonInstrumentation.Key_Counter_MMIO_ReadSpinwaitTime, readTimer.ElapsedMillisecondsPrecise(), _dimensions);
#endif
                    readTimer.Restart();
                    int amountToActuallyRead = FastMath.Min(amountCanSafelyRead, count);
                    MicroProfiler.Send(MicroProfilingEventType.MMIO_Read_ReadStart, operationId, amountToActuallyRead);
                    int read1 = FastMath.Min(amountToActuallyRead, _bufferSize - readCursor);
                    int read2 = amountToActuallyRead - read1;
                    _buf.ReadArray(readCursor + HEADER_SIZE, buf, offset, read1);
                    if (read2 > 0)
                    {
                        _buf.ReadArray(HEADER_SIZE, buf, offset + read1, read2);
                    }

                    MicroProfiler.Send(MicroProfilingEventType.MMIO_Read_ReadDataFinish, operationId);

                    // Increment the read cursor index
                    readCursorIdx = (byte)((readCursorIdx + 1) % NUM_ATOMIC_SAFE_SLOTS);
                    readCursor = (readCursor + amountToActuallyRead) % _bufferSize;
                    // Write the new read index to the other slot
                    _buf.Write(READ_INDEX_BASE + (4 * readCursorIdx), ref readCursor);
                    // And then swap the slot atomically
                    _buf.Write(READ_INDEX_SLOT_ID, readCursorIdx);
                    //Console.WriteLine("Updated WC: " + readCursor + " (index " + readCursorIdx + ")");


                    MicroProfiler.Send(MicroProfilingEventType.MMIO_Read_ReadFinish, operationId);
                    readTimer.Stop();

                    // Report metrics
                    double percentageOfBufferUsed = 100d * amountCanSafelyRead / (double)_bufferSize;
                    _metrics.Value.ReportPercentile(CommonInstrumentation.Key_Counter_MMIO_BufferUsage, _dimensions, percentageOfBufferUsed);
#if PARANOID_MMIO_METRICS
                    _metrics.Value.ReportPercentile(CommonInstrumentation.Key_Counter_MMIO_ReadTime, readTimer.ElapsedMillisecondsPrecise(), _dimensions);
#endif
                    //Console.WriteLine("Read " + amountToActuallyRead + " bytes");
                    return amountToActuallyRead;
                }
            }

            cancelToken.ThrowIfCancellationRequested();
            return 0;
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

                int thisReadSize = await ReadAnyAsync(data, offset + received, nextPacketSize, cancelToken, waitProvider).ConfigureAwait(false);
                received += thisReadSize;

                // If the stream has closed, throw an exception because we can't possibly return the amount of data requested
                if (thisReadSize == 0)
                {
                    throw new EndOfStreamException("MMIO stream closed before reliable read could be completed");
                }
            }

            cancelToken.ThrowIfCancellationRequested();

            return received;
        }

        public async Task WriteAsync(byte[] buf, int offset, int count, CancellationToken cancelToken)
        {
            ValueStopwatch overallWriteTimer = ValueStopwatch.StartNew();
            ValueStopwatch writeTimer = ValueStopwatch.StartNew();
            uint operationId = MicroProfiler.GenerateOperationId();
            MicroProfiler.Send(MicroProfilingEventType.MMIO_Write_EnterWriteMethod, operationId, _streamHash);
            int amountWritten = 0;
            bool writeStallOccurred = false;

            byte writeCursorIdx = _buf.ReadByte(WRITE_INDEX_SLOT_ID);
            int writeCursor = _buf.ReadInt32(WRITE_INDEX_BASE + (4 * writeCursorIdx));

            while (amountWritten < count)
            {
                cancelToken.ThrowIfCancellationRequested();

                byte readCursorIdx = _buf.ReadByte(READ_INDEX_SLOT_ID);
                int readCursor = _buf.ReadInt32(READ_INDEX_BASE + (4 * readCursorIdx));

                int amountCanSafelyWrite;
                // do a -1 on all of these values because we can't allow the write pointer to become equal to the read pointer
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
                MicroProfiler.Send(MicroProfilingEventType.MMIO_Write_PreWrite, operationId, amountCanSafelyWrite);
                if (amountCanSafelyWrite == 0)
                {
                    // Buffer is full. Spinwait
                    //Console.WriteLine("WRITE: WAITING");
                    MicroProfiler.Send(MicroProfilingEventType.MMIO_Write_StallBufferFull, operationId);
                    writeStallOccurred = true;
                }
                else
                {
                    int amountToActuallyWrite = FastMath.Min(count - amountWritten, amountCanSafelyWrite);
                    MicroProfiler.Send(MicroProfilingEventType.MMIO_Write_WriteStart, operationId, amountToActuallyWrite);
                    int write1 = FastMath.Min(_bufferSize - writeCursor, amountToActuallyWrite);
                    int write2 = amountToActuallyWrite - write1;
                    _buf.WriteArray(writeCursor + HEADER_SIZE, buf, offset + amountWritten, write1);
                    if (write2 > 0)
                    {
                        _buf.WriteArray(HEADER_SIZE, buf, offset + amountWritten + write1, write2);
                    }

                    MicroProfiler.Send(MicroProfilingEventType.MMIO_Write_WriteDataFinish, operationId);
                    // Invert the write cursor index
                    writeCursorIdx = (byte)((writeCursorIdx + 1) % NUM_ATOMIC_SAFE_SLOTS);
                    // Write the new write index to the other slot
                    writeCursor = (writeCursor + amountToActuallyWrite) % _bufferSize;
                    _buf.Write(WRITE_INDEX_BASE + (4 * writeCursorIdx), writeCursor);
                    // And then swap the slot atomically
                    _buf.Write(WRITE_INDEX_SLOT_ID, writeCursorIdx);
                    //Console.WriteLine("Updated WC: " + writeCursor + " (index " + writeCursorIdx + ")");

                    _kernelWriteSignal.Set();
                    writeTimer.Stop();
                    MicroProfiler.Send(MicroProfilingEventType.MMIO_Write_WriteFinish, operationId);

                    // Report metrics
                    double percentageOfBufferUsed = 100d * (_bufferSize - amountCanSafelyWrite) / (double)_bufferSize;
                    _metrics.Value.ReportPercentile(CommonInstrumentation.Key_Counter_MMIO_BufferUsage, _dimensions, percentageOfBufferUsed);
#if PARANOID_MMIO_METRICS
                    _metrics.Value.ReportPercentile(CommonInstrumentation.Key_Counter_MMIO_WriteTime, writeTimer.ElapsedMillisecondsPrecise(), _dimensions);
#endif
                    amountWritten += amountToActuallyWrite;
                    //Console.WriteLine("Wrote " + amountToActuallyWrite + " bytes");

                    writeTimer.Restart();
                }
            }

            // If the buffer ever stalled us during this write operation, report it to metrics
            if (writeStallOccurred)
            {
                _metrics.Value.ReportPercentile(CommonInstrumentation.Key_Counter_MMIO_BufferUsage, _dimensions, 100);
                _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_MMIO_WriteStalls, _dimensions);
                await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            }

            overallWriteTimer.Stop();
#if PARANOID_MMIO_METRICS
            _metrics.Value.ReportPercentile(CommonInstrumentation.Key_Counter_MMIO_WriteTotalTime, overallWriteTimer.ElapsedMillisecondsPrecise(), _dimensions);
#endif
        }

        private void KernelWriteSignalCallback(object state, bool timedOut)
        {
            SemaphoreSlim copyOfLocalWriteSignal = state as SemaphoreSlim;
            if (copyOfLocalWriteSignal == null)
            {
                return;
            }

            try
            {
                if (copyOfLocalWriteSignal.CurrentCount < 1)
                {
                    copyOfLocalWriteSignal.Release();
                }
            }
            catch (Exception e)
            {
                // Don't let any exceptions propagate to the native layer
                _logger?.Log(e);
            }
        }
    }
}
