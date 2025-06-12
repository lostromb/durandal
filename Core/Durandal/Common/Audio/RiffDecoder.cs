using Durandal.Common.IO;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio
{
    /// <summary>
    /// Generic decoder class for RIFF files (commonly used for .wav audio as well as .webp images)
    /// </summary>
    public class RiffDecoder : IDisposable
    {
        private readonly byte[] SCRATCH_SPACE = new byte[12];
        private readonly NonRealTimeStream _innerStream;
        private readonly bool _ownsStream;
        private readonly Encoding _stringEncoding;
        private int _disposed = 0;
        private bool _initialized = false;
        private int _totalReportedFileSize; // bytes 3-7 in the file. We don't really care about it though.
        private int _thisBlockCursor; // # of bytes we have read from the current block, excluding the 8 bytes type-size header

        public RiffDecoder(NonRealTimeStream innerStream, bool ownsStream)
        {
            _innerStream = innerStream.AssertNonNull(nameof(innerStream));
            _ownsStream = ownsStream;

            try
            {
                _stringEncoding = Encoding.GetEncoding("ASCII");
            }
            catch (Exception)
            {
                _stringEncoding = Encoding.UTF8;
            }

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~RiffDecoder()
        {
            Dispose(false);
        }
#endif

        /// <summary>
        /// The type of RIFF file this is, e.g. "WAVE"
        /// </summary>
        public string FileType { get; private set; }

        /// <summary>
        /// The common name of the current block, e.g. "data" or "fmt "
        /// </summary>
        public string CurrentBlockName { get; private set; }

        /// <summary>
        /// Indicates the end of the stream
        /// </summary>
        public bool EndOfStream { get; private set; }

        /// <summary>
        /// Total size of the current block of riff data.
        /// Can be zero to indicate an indefinite size.
        /// </summary>
        public int CurrentBlockLength { get; private set; }

        /// <summary>
        /// Initializes this decoder by reading the file header and the header of the first block of data.
        /// </summary>
        /// <param name="cancelToken"></param>
        /// <param name="realTime"></param>
        /// <returns>True if the headers were read successfully, false if this is not a RIFF file or if end of stream was reached.</returns>
        public bool Initialize(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int headerBytesRead = 0;
            while (headerBytesRead < 12)
            {
                int thisReadSize = _innerStream.Read(SCRATCH_SPACE, headerBytesRead, 12 - headerBytesRead, cancelToken, realTime);
                if (thisReadSize < 0)
                {
                    return false;
                }

                headerBytesRead += thisReadSize;
            }

            if (SCRATCH_SPACE[0] != 0x52 ||
                SCRATCH_SPACE[1] != 0x49 ||
                SCRATCH_SPACE[2] != 0x46 ||
                SCRATCH_SPACE[3] != 0x46)
            {
                // Header is missing, this is not a RIFF file
                return false;
            }

            _totalReportedFileSize = BinaryHelpers.ByteArrayToInt32LittleEndian(SCRATCH_SPACE, 4);
            FileType = _stringEncoding.GetString(SCRATCH_SPACE, 8, 4);
            _initialized = QueueNextBlock(cancelToken, realTime);
            return _initialized;
        }

        /// <summary>
        /// Initializes this decoder by reading the file header and the header of the first block of data.
        /// </summary>
        /// <param name="cancelToken"></param>
        /// <param name="realTime"></param>
        /// <returns>True if the headers were read successfully, false if this is not a RIFF file or if end of stream was reached.</returns>
        public async Task<bool> InitializeAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int headerBytesRead = 0;
            while (headerBytesRead < 12)
            {
                int thisReadSize = await _innerStream.ReadAsync(SCRATCH_SPACE, headerBytesRead, 12 - headerBytesRead, cancelToken, realTime).ConfigureAwait(false);
                if (thisReadSize < 0)
                {
                    return false;
                }

                headerBytesRead += thisReadSize;
            }

            if (SCRATCH_SPACE[0] != 0x52 || // "R I F F"
                SCRATCH_SPACE[1] != 0x49 ||
                SCRATCH_SPACE[2] != 0x46 ||
                SCRATCH_SPACE[3] != 0x46)
            {
                // Header is missing, this is not a RIFF file
                return false;
            }

            _totalReportedFileSize = BinaryHelpers.ByteArrayToInt32LittleEndian(SCRATCH_SPACE, 4);
            FileType = _stringEncoding.GetString(SCRATCH_SPACE, 8, 4);
            _initialized = QueueNextBlock(cancelToken, realTime);
            return _initialized;
        }

        /// <summary>
        /// Attempts to read a certain number of bytes from the current RIFF block of data. A single read is guaranteed to never
        /// span two blocks. The amount of data that can be read is dictated by the current block size.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="cancelToken"></param>
        /// <param name="realTime"></param>
        /// <returns>The number of bytes read, or 0 if end of stream</returns>
        public int ReadFromCurrentBlock(byte[] target, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("Riff decoder has not been initialized");
            }

            if (EndOfStream)
            {
                return -1;
            }

            int amountCanReadFromCurrentBlock = count;
            if (CurrentBlockLength > 0)
            {
                amountCanReadFromCurrentBlock = FastMath.Min(count, CurrentBlockLength - _thisBlockCursor);
            }

            int readSize = _innerStream.Read(target, offset, amountCanReadFromCurrentBlock, cancelToken, realTime);
            if (readSize <= 0)
            {
                StreamEnded();
            }
            else if (readSize > 0)
            {
                _thisBlockCursor += readSize;
                if (CurrentBlockLength > 0 && _thisBlockCursor >= CurrentBlockLength)
                {
                    // If reached end of current block, try and queue the next one
                    if (!QueueNextBlock(cancelToken, realTime))
                    {
                        StreamEnded();
                    }
                }
            }

            return readSize;
        }

        /// <summary>
        /// Attempts to read a certain number of bytes from the current RIFF block of data. A single read is guaranteed to never
        /// span two blocks. The amount of data that can be read is dictated by the current block size.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="cancelToken"></param>
        /// <param name="realTime"></param>
        /// <returns>The number of bytes read, or 0 if end of stream</returns>
        public async Task<int> ReadFromCurrentBlockAsync(byte[] target, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("Riff decoder has not been initialized");
            }

            if (EndOfStream)
            {
                return 0;
            }

            int amountCanReadFromCurrentBlock = count;
            if (CurrentBlockLength > 0)
            {
                amountCanReadFromCurrentBlock = FastMath.Min(count, CurrentBlockLength - _thisBlockCursor);
            }

            int readSize = await _innerStream.ReadAsync(target, offset, amountCanReadFromCurrentBlock, cancelToken, realTime).ConfigureAwait(false);
            if (readSize <= 0)
            {
                StreamEnded();
            }
            else if (readSize > 0)
            {
                _thisBlockCursor += readSize;
                if (CurrentBlockLength > 0 && _thisBlockCursor >= CurrentBlockLength)
                {
                    // If reached end of current block, try and queue the next one
                    if (!(await QueueNextBlockAsync(cancelToken, realTime).ConfigureAwait(false)))
                    {
                        StreamEnded();
                    }
                }
            }

            return readSize;
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
                if (_ownsStream)
                {
                    _innerStream?.Dispose();
                }
            }
        }

        private void StreamEnded()
        {
            EndOfStream = true;
            _thisBlockCursor = 0;
            CurrentBlockName = null;
            CurrentBlockLength = 0;
        }

        /// <summary>
        /// Attempts to read 8 bytes from the stream and parse a block header from it.
        /// Returns false if the stream ends
        /// </summary>
        /// <param name="cancelToken"></param>
        /// <param name="realTime"></param>
        /// <returns></returns>
        private bool QueueNextBlock(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int headerBytesRead = 0;
            while (headerBytesRead < 8)
            {
                int thisReadSize = _innerStream.Read(SCRATCH_SPACE, headerBytesRead, 8 - headerBytesRead, cancelToken, realTime);
                if (thisReadSize <= 0)
                {
                    return false;
                }

                headerBytesRead += thisReadSize;
            }

            _thisBlockCursor = 0;
            CurrentBlockName = _stringEncoding.GetString(SCRATCH_SPACE, 0, 4);
            CurrentBlockLength = BinaryHelpers.ByteArrayToInt32LittleEndian(SCRATCH_SPACE, 4);
            return true;
        }

        private async Task<bool> QueueNextBlockAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int headerBytesRead = 0;
            while (headerBytesRead < 8)
            {
                int thisReadSize = await _innerStream.ReadAsync(SCRATCH_SPACE, headerBytesRead, 8 - headerBytesRead, cancelToken, realTime).ConfigureAwait(false);
                if (thisReadSize <= 0)
                {
                    return false;
                }

                headerBytesRead += thisReadSize;
            }

            _thisBlockCursor = 0;
            CurrentBlockName = _stringEncoding.GetString(SCRATCH_SPACE, 0, 4);
            CurrentBlockLength = BinaryHelpers.ByteArrayToInt32LittleEndian(SCRATCH_SPACE, 4);
            return true;
        }
    }
}
