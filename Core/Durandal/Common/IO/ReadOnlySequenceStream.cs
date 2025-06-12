using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.IO
{
    /// <summary>
    /// Implements a <see cref="NonRealTimeStream"/> which allows read-only random access to a <see cref="ReadOnlySequence{T}"/> of bytes.
    /// </summary>
    public class ReadOnlySequenceStream : NonRealTimeStream
    {
        private readonly ReadOnlySequence<byte> _sequence;
        private ReadOnlySequence<byte>.Enumerator _enumerator;
        private ReadOnlyMemory<byte> _currentSegment;
        private long _overallPosition = 0;
        private int _positionInCurrentSegment = 0;
        private int _disposed = 0;

        public ReadOnlySequenceStream(ReadOnlySequence<byte> sequence)
        {
            _sequence = sequence.AssertNonNull(nameof(sequence));
            _enumerator = _sequence.GetEnumerator();
            MoveToNextSegmentInternal();
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => _sequence.Length;

        public override long Position
        {
            get => _overallPosition;
            set => Seek(value, SeekOrigin.Begin);
        }

        public override void Flush()
        {
            throw new NotSupportedException("Cannot flush a read-only stream");
        }

        public override int Read(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int bytesRead = 0;
            while (bytesRead < count)
            {
                if (_overallPosition == _sequence.Length)
                {
                    return bytesRead;
                }

                // Iterate segments if needed
                if (_positionInCurrentSegment == _currentSegment.Length)
                {
                    _positionInCurrentSegment = 0;
                    MoveToNextSegmentInternal();
                    if (_currentSegment.Length == 0)
                    {
                        return bytesRead;
                    }
                }

                // Copy from current segment
                int canReadFromThisSegment = FastMath.Min(count - bytesRead, _currentSegment.Length - _positionInCurrentSegment);
                _currentSegment.Slice(_positionInCurrentSegment, canReadFromThisSegment).CopyTo(targetBuffer.AsMemory(offset + bytesRead, canReadFromThisSegment));
                _overallPosition += canReadFromThisSegment;
                _positionInCurrentSegment += canReadFromThisSegment;
                bytesRead += canReadFromThisSegment;
            }

            return bytesRead;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return Read(buffer, offset, count, CancellationToken.None, DefaultRealTimeProvider.Singleton);
        }

        public override Task<int> ReadAsync(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return Task.FromResult(Read(targetBuffer, offset, count, cancelToken, realTime));
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long destination;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    destination = offset;
                    break;
                case SeekOrigin.Current:
                    destination = _overallPosition + offset;
                    break;
                case SeekOrigin.End:
                    destination = _sequence.Length + offset;
                    break;
                default:
                    throw new ArgumentException("Unknown SeekOrigin");
            }

            if (destination > _sequence.Length)
            {
                throw new ArgumentOutOfRangeException("Seek destination is beyond bounds of stream data");
            }

            _enumerator = _sequence.GetEnumerator();
            _overallPosition = 0;
            _positionInCurrentSegment = 0;
            MoveToNextSegmentInternal();

            while (_overallPosition < destination)
            {
                int amountCanIterate = (int)Math.Min(_currentSegment.Length, destination - _overallPosition);
                _overallPosition += amountCanIterate;
                _positionInCurrentSegment += amountCanIterate;

                if (_positionInCurrentSegment == _currentSegment.Length)
                {
                    _positionInCurrentSegment = 0;
                    MoveToNextSegmentInternal();
                }
            }

            return destination;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException("Cannot set length on a read-only stream");
        }

        public override void Write(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            throw new NotSupportedException("Cannot write to a read-only stream");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("Cannot write to a read-only stream");
        }

        public override Task WriteAsync(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            throw new NotSupportedException("Cannot write to a read-only stream");
        }

        protected override void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            try
            {
                DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

                if (disposing)
                {
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        private void MoveToNextSegmentInternal()
        {
            if (_enumerator.MoveNext())
            {
                _currentSegment = _enumerator.Current;
            }
            else
            {
                _currentSegment = BinaryHelpers.EMPTY_BYTE_ARRAY;
            }
        }
    }
}
