﻿// ---------------------------------------------------------------------
// Copyright (c) 2015 Microsoft
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
// ---------------------------------------------------------------------

namespace Durandal.Common.IO
{
    using System;
    using System.IO;
    using System.Collections.Generic;
    using Durandal.Common.MathExt;
    using Durandal.Common.Collections;
    using Durandal.Common.ServiceMgmt;
    using System.Buffers;
    using Durandal.Common.Utils;

    /// <summary>
    /// MemoryStream implementation that deals with pooling and managing memory streams which use potentially large
    /// buffers.
    /// </summary>
    /// <remarks>
    /// This class works in tandem with the RecylableMemoryStreamManager to supply MemoryStream
    /// objects to callers, while avoiding these specific problems:
    /// 1. LOH allocations - since all large buffers are pooled, they will never incur a Gen2 GC
    /// 2. Memory waste - A standard memory stream doubles its size when it runs out of room. This
    /// leads to continual memory growth as each stream approaches the maximum allowed size.
    /// 3. Memory copying - Each time a MemoryStream grows, all the bytes are copied into new buffers.
    /// This implementation only copies the bytes when GetBuffer is called.
    /// 4. Memory fragmentation - By using homogeneous buffer sizes, it ensures that blocks of memory
    /// can be easily reused.
    /// 
    /// The stream is implemented on top of a series of uniformly-sized blocks. As the stream's length grows,
    /// additional blocks are retrieved from the memory manager. It is these blocks that are pooled, not the stream
    /// object itself.
    /// 
    /// The biggest wrinkle in this implementation is when GetBuffer() is called. This requires a single 
    /// contiguous buffer. If only a single block is in use, then that block is returned. If multiple blocks 
    /// are in use, we retrieve a larger buffer from the memory manager. These large buffers are also pooled, 
    /// split by size--they are multiples of a chunk size (1 MB by default).
    /// 
    /// Once a large buffer is assigned to the stream the blocks are NEVER again used for this stream. All operations take place on the 
    /// large buffer. The large buffer can be replaced by a larger buffer from the pool as needed. All blocks and large buffers 
    /// are maintained in the stream until the stream is disposed (unless AggressiveBufferReturn is enabled in the stream manager).
    /// 
    /// </remarks>
    public sealed class RecyclableMemoryStream : MemoryStream
    {
        private const long MaxStreamLength = Int32.MaxValue;

        /// <summary>
        /// All of these blocks must be the same size
        /// </summary>
        private readonly List<PooledBuffer<byte>> blocks = new List<PooledBuffer<byte>>(1);

        /// <summary>
        /// This is only set by GetBuffer() if the necessary buffer is larger than a single block size, or on
        /// construction if the caller immediately requests a single large buffer.
        /// </summary>
        /// <remarks>If this field is non-null, it contains the concatenation of the bytes found in the individual
        /// blocks. Once it is created, this (or a larger) largeBuffer will be used for the life of the stream.
        /// </remarks>
        private byte[] largeBuffer;

        /// <summary>
        /// This list is used to store buffers once they're replaced by something larger.
        /// This is for the cases where you have users of this class that may hold onto the buffers longer
        /// than they should and you want to prevent race conditions which could corrupt the data.
        /// </summary>
        private List<byte[]> dirtyBuffers;

        private readonly Guid id;
        /// <summary>
        /// Unique identifier for this stream across it's entire lifetime
        /// </summary>
        /// <exception cref="ObjectDisposedException">Object has been disposed</exception>
        internal Guid Id { get { this.CheckDisposed(); return this.id; } }

        private readonly string tag;
        /// <summary>
        /// A temporary identifier for the current usage of this stream.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Object has been disposed</exception>
        internal string Tag { get { this.CheckDisposed(); return this.tag; } }

        private readonly RecyclableMemoryStreamManager memoryManager;

        /// <summary>
        /// Gets the memory manager being used by this stream.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Object has been disposed</exception>
        internal RecyclableMemoryStreamManager MemoryManager
        {
            get
            {
                this.CheckDisposed();
                return this.memoryManager;
            }
        }

        private bool disposed;

        private readonly string allocationStack;
        private string disposeStack;

        /// <summary>
        /// Callstack of the constructor. It is only set if MemoryManager.GenerateCallStacks is true,
        /// which should only be in debugging situations.
        /// </summary>
        internal string AllocationStack { get { return this.allocationStack; } }

        /// <summary>
        /// Callstack of the Dispose call. It is only set if MemoryManager.GenerateCallStacks is true,
        /// which should only be in debugging situations.
        /// </summary>
        internal string DisposeStack { get { return this.disposeStack; } }

        /// <summary>
        /// This buffer exists so that WriteByte can forward all of its calls to Write
        /// without creating a new byte[] buffer on every call.
        /// </summary>
        private readonly byte[] byteBuffer = new byte[1];

        #region Constructors
        /// <summary>
        /// Allocate a new RecyclableMemoryStream object.
        /// </summary>
        /// <param name="memoryManager">The memory manager</param>
        public RecyclableMemoryStream(RecyclableMemoryStreamManager memoryManager)
            : this(memoryManager, null, 0, null)
        {
        }

        /// <summary>
        /// Allocate a new RecyclableMemoryStream object
        /// </summary>
        /// <param name="memoryManager">The memory manager</param>
        /// <param name="tag">A string identifying this stream for logging and debugging purposes</param>
        public RecyclableMemoryStream(RecyclableMemoryStreamManager memoryManager, string tag)
            : this(memoryManager, tag, 0, null)
        {
        }

        /// <summary>
        /// Allocate a new RecyclableMemoryStream object
        /// </summary>
        /// <param name="memoryManager">The memory manager</param>
        /// <param name="tag">A string identifying this stream for logging and debugging purposes</param>
        /// <param name="requestedSize">The initial requested size to prevent future allocations</param>
        public RecyclableMemoryStream(RecyclableMemoryStreamManager memoryManager, string tag, int requestedSize)
            : this(memoryManager, tag, requestedSize, null)
        {
        }

        /// <summary>
        /// Allocate a new RecyclableMemoryStream object
        /// </summary>
        /// <param name="memoryManager">The memory manager</param>
        /// <param name="tag">A string identifying this stream for logging and debugging purposes</param>
        /// <param name="requestedSize">The initial requested size to prevent future allocations</param>
        /// <param name="initialLargeBuffer">An initial buffer to use. This buffer will be owned by the stream and returned to the memory manager upon Dispose.</param>
        internal RecyclableMemoryStream(RecyclableMemoryStreamManager memoryManager, string tag, int requestedSize,
                                      byte[] initialLargeBuffer)
        {
            this.memoryManager = memoryManager;
            this.id = Guid.NewGuid();
            this.tag = tag;

            if (requestedSize < memoryManager.BlockSize)
            {
                requestedSize = memoryManager.BlockSize;
            }

            if (initialLargeBuffer == null)
            {
                this.EnsureCapacity(requestedSize);
            }
            else
            {
                this.largeBuffer = initialLargeBuffer;
            }

            this.disposed = false;

            if (this.memoryManager.GenerateCallStacks)
            {
                this.allocationStack = "null";
            }
            
            this.memoryManager.ReportStreamCreated();
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }
        #endregion

#region Dispose and Finalize
        ~RecyclableMemoryStream()
        {
            Dispose(false);
        }

        /// <summary>
        /// Returns the memory used by this stream back to the pool.
        /// </summary>
        /// <param name="disposing">Whether we're disposing (true), or being called by the finalizer (false)</param>
        /// <remarks>This method is not thread safe and it may not be called more than once.</remarks>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1816:CallGCSuppressFinalizeCorrectly", Justification = "We have different disposal semantics, so SuppressFinalize is in a different spot.")]
        protected override void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (this.memoryManager.GenerateCallStacks)
            {
                this.disposeStack = "null";
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                // Once this flag is set, we can't access any properties -- use fields directly
                this.disposed = true;

                this.memoryManager.ReportStreamDisposed();

                GC.SuppressFinalize(this);
            }
            else
            {
                // We're being finalized.

                //if (AppDomain.CurrentDomain.IsFinalizingForUnload())
                {
                    // If we're being finalized because of a shutdown, don't go any further.
                    // We have no idea what's already been cleaned up. Triggering events may cause
                    // a crash.
                    base.Dispose(disposing);
                    return;
                }

                //this.memoryManager.ReportStreamFinalized();
            }

            this.memoryManager.ReportStreamLength(this.length);

            if (this.largeBuffer != null)
            {
                this.memoryManager.ReturnLargeBuffer(this.largeBuffer, this.tag);
            }

            if (this.dirtyBuffers != null)
            {
                foreach (var buffer in this.dirtyBuffers)
                {
                    this.memoryManager.ReturnLargeBuffer(buffer, this.tag);
                }
            }

            this.memoryManager.ReturnBlocks(this.blocks, this.tag);

            base.Dispose(disposing);
        }

#endregion

#region MemoryStream overrides
        /// <summary>
        /// Gets or sets the capacity
        /// </summary>
        /// <remarks>Capacity is always in multiples of the memory manager's block size, unless
        /// the large buffer is in use.  Capacity never decreases during a stream's lifetime. 
        /// Explicitly setting the capacity to a lower value than the current value will have no effect. 
        /// This is because the buffers are all pooled by chunks and there's little reason to 
        /// allow stream truncation.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">Object has been disposed</exception>
        public override int Capacity
        {
            get
            {
                this.CheckDisposed();
                if (this.largeBuffer != null)
                {
                    return this.largeBuffer.Length;
                }

                if (this.blocks.Count > 0)
                {
                    return this.blocks.Count * this.memoryManager.BlockSize;
                }
                return 0;
            }
            set
            {
                this.CheckDisposed();
                this.EnsureCapacity(value);
            }
        }

        private int length;

        /// <summary>
        /// Gets the number of bytes written to this stream.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Object has been disposed</exception>
        public override long Length
        {
            get
            {
                this.CheckDisposed();
                return this.length;
            }
        }

        private int position;

        /// <summary>
        /// Gets the current position in the stream
        /// </summary>
        /// <exception cref="ObjectDisposedException">Object has been disposed</exception>
        public override long Position
        {
            get
            {
                this.CheckDisposed();
                return this.position;
            }
            set
            {
                this.CheckDisposed();
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException("value", "value must be non-negative");
                }

                if (value > MaxStreamLength)
                {
                    throw new ArgumentOutOfRangeException("value", "value cannot be more than " + MaxStreamLength);
                }

                this.position = (int)value;
            }
        }

        /// <summary>
        /// Whether the stream can currently read
        /// </summary>
        public override bool CanRead
        {
            get { return !this.disposed; }
        }

        /// <summary>
        /// Whether the stream can currently seek
        /// </summary>
        public override bool CanSeek
        {
            get { return !this.disposed; }
        }

        /// <summary>
        /// Always false
        /// </summary>
        public override bool CanTimeout
        {
            get { return false; }
        }

        /// <summary>
        /// Whether the stream can currently write
        /// </summary>
        public override bool CanWrite
        {
            get { return !this.disposed; }
        }

        /// <summary>
        /// Returns a single buffer containing the contents of the stream.
        /// The buffer may be longer than the stream length.
        /// </summary>
        /// <returns>A byte[] buffer</returns>
        /// <remarks>IMPORTANT: Doing a Write() after calling GetBuffer() invalidates the buffer. The old buffer is held onto
        /// until Dispose is called, but the next time GetBuffer() is called, a new buffer from the pool will be required.
        /// Also, keep in mind the returned buffer may be part of a pool and could become invalid after disposal!</remarks>
        /// <exception cref="ObjectDisposedException">Object has been disposed</exception>
#pragma warning disable 0114
        public byte[] GetBuffer()
        {
#pragma warning restore 0114
            this.CheckDisposed();

            if (this.largeBuffer != null)
            {
                return this.largeBuffer;
            }

            if (this.blocks.Count == 1)
            {
                return this.blocks[0].Buffer;
            }

            // Buffer needs to reflect the capacity, not the length, because
            // it's possible that people will manipulate the buffer directly
            // and set the length afterward. Capacity sets the expectation
            // for the size of the buffer.
            var newBuffer = this.memoryManager.GetLargeBuffer(this.Capacity, this.tag);

            // InternalRead will check for existence of largeBuffer, so make sure we
            // don't set it until after we've copied the data.
            this.InternalRead(newBuffer, 0, this.length, 0);
            this.largeBuffer = newBuffer;

            if (this.blocks.Count > 0 && this.memoryManager.AggressiveBufferReturn)
            {
                this.memoryManager.ReturnBlocks(this.blocks, this.tag);
                this.blocks.Clear();
            }

            return this.largeBuffer;
        }

        /// <summary>
        /// Returns a new array with a copy of the buffer's contents. You should almost certainly be using GetBuffer combined with the Length to 
        /// access the bytes in this stream. Calling ToArray will destroy the benefits of pooled buffers, but it is included
        /// for the sake of completeness.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Object has been disposed</exception>
        // [Obsolete("This method has degraded performance vs. GetBuffer and should be avoided.")]
        public override byte[] ToArray()
        {
            this.CheckDisposed();
            var newBuffer = new byte[this.Length];

            this.InternalRead(newBuffer, 0, this.length, 0);
            this.memoryManager.ReportStreamToArray();

            return newBuffer;
        }

        public ReadOnlySequence<byte> ToReadOnlySequenceAvoidLOH()
        {
            const int MAX_SEGMENT_SIZE = 65536;

            this.CheckDisposed();

            if (this.Length == 0)
            {
                return new ReadOnlySequence<byte>(BinaryHelpers.EMPTY_BYTE_ARRAY);
            }

            int sizeProcessed = 0;
            MemorySegment<byte> firstSegment = null;
            MemorySegment<byte> lastSegment = null;
            while (sizeProcessed < this.Length)
            {
                int thisSegmentSize = (int)Math.Min(MAX_SEGMENT_SIZE, this.Length - sizeProcessed);
                byte[] segmentData = new byte[thisSegmentSize];
                int thisSegmentRead = 0;
                while (thisSegmentRead < thisSegmentSize)
                {
                    int amountRead = this.InternalRead(segmentData, 0, thisSegmentSize - thisSegmentRead, sizeProcessed + thisSegmentRead);
                    if (amountRead == 0)
                    {
                        throw new Exception("Weird stuff happened while copy recyclable memory stream to readonly sequence");
                    }

                    thisSegmentRead += amountRead;
                }
                
                if (firstSegment == null)
                {
                    firstSegment = new MemorySegment<byte>(segmentData);
                    lastSegment = firstSegment;
                }
                else
                {
                    lastSegment = lastSegment.Append(segmentData);
                }

                sizeProcessed += thisSegmentSize;
            }

            return new ReadOnlySequence<byte>(firstSegment, 0, lastSegment, lastSegment.Memory.Length);
        }

        /// <summary>
        /// Copies this stream into a contiguous pooled byte buffer. This is a little better then using ToArray(), but should still be avoided if possible.
        /// </summary>
        /// <returns></returns>
        public PooledBuffer<byte> ToPooledBuffer()
        {
            this.CheckDisposed();
            PooledBuffer<byte> newBuffer = BufferPool<byte>.Rent((int)this.Length);
            this.InternalRead(newBuffer.Buffer, 0, this.length, 0);
            //this.memoryManager.ReportStreamToArray();
            return newBuffer;
        }

        /// <summary>
        /// Reads from the current position into the provided buffer
        /// </summary>
        /// <param name="buffer">Destination buffer</param>
        /// <param name="offset">Offset into buffer at which to start placing the read bytes.</param>
        /// <param name="count">Number of bytes to read.</param>
        /// <returns>The number of bytes read</returns>
        /// <exception cref="ArgumentNullException">buffer is null</exception>
        /// <exception cref="ArgumentOutOfRangeException">offset or count is less than 0</exception>
        /// <exception cref="ArgumentException">offset subtracted from the buffer length is less than count</exception>
        /// <exception cref="ObjectDisposedException">Object has been disposed</exception>
        public override int Read(byte[] buffer, int offset, int count)
        {
            this.CheckDisposed();
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }

            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException("offset", "offset cannot be negative");
            }

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("count", "count cannot be negative");
            }

            if (offset + count > buffer.Length)
            {
                throw new ArgumentException("buffer length must be at least offset + count");
            }

            int amountRead = this.InternalRead(buffer, offset, count, this.position);
            this.position += amountRead;
            return amountRead;
        }

        /// <summary>
        /// Writes the buffer to the stream
        /// </summary>
        /// <param name="buffer">Source buffer</param>
        /// <param name="offset">Start position</param>
        /// <param name="count">Number of bytes to write</param>
        /// <exception cref="ArgumentNullException">buffer is null</exception>
        /// <exception cref="ArgumentOutOfRangeException">offset or count is negative</exception>
        /// <exception cref="ArgumentException">buffer.Length - offset is not less than count</exception>
        /// <exception cref="ObjectDisposedException">Object has been disposed</exception>
        public override void Write(byte[] buffer, int offset, int count)
        {
            this.CheckDisposed();
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }

            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException("offset", offset, "Offset must be in the range of 0 - buffer.Length-1");
            }

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("count", count, "count must be non-negative");
            }

            if (count + offset > buffer.Length)
            {
                throw new ArgumentException("count must be greater than buffer.Length - offset");
            }

            Write(buffer.AsSpan(offset, count));
        }

        /// <summary>
        /// Writes a span to the stream
        /// </summary>
        /// <param name="data">The span to write</param>
        /// <exception cref="ArgumentNullException">buffer is null</exception>
        /// <exception cref="ArgumentOutOfRangeException">offset or count is negative</exception>
        /// <exception cref="ArgumentException">buffer.Length - offset is not less than count</exception>
        /// <exception cref="ObjectDisposedException">Object has been disposed</exception>
        public void Write(ReadOnlySpan<byte> data)
        {
            this.CheckDisposed();
            int blockSize = this.memoryManager.BlockSize;
            long end = (long)this.position + data.Length;
            // Check for overflow
            if (end > MaxStreamLength)
            {
                throw new IOException("Maximum capacity exceeded");
            }

            long requiredBuffers = (end + blockSize - 1) / blockSize;

            if (requiredBuffers * blockSize > MaxStreamLength)
            {
                throw new IOException("Maximum capacity exceeded");
            }

            this.EnsureCapacity((int)end);

            if (this.largeBuffer == null)
            {
                int bytesRemaining = data.Length;
                int bytesWritten = 0;
                var blockAndOffset = this.GetBlockAndRelativeOffset(this.position);

                while (bytesRemaining > 0)
                {
                    byte[] currentBlock = this.blocks[blockAndOffset.Block].Buffer;
                    int remainingInBlock = blockSize - blockAndOffset.Offset;
                    int amountToWriteInBlock = FastMath.Min(remainingInBlock, bytesRemaining);

                    data.Slice(bytesWritten, amountToWriteInBlock).CopyTo(currentBlock.AsSpan(blockAndOffset.Offset, amountToWriteInBlock));

                    bytesRemaining -= amountToWriteInBlock;
                    bytesWritten += amountToWriteInBlock;

                    ++blockAndOffset.Block;
                    blockAndOffset.Offset = 0;
                }
            }
            else
            {
                data.CopyTo(this.largeBuffer.AsSpan(this.position, data.Length));
            }

            this.position = (int)end;
            this.length = FastMath.Max(this.position, this.length);
        }

        /// <summary>
        /// Returns a useful string for debugging. This should not normally be called in actual production code.
        /// </summary>
        public override string ToString()
        {
            return string.Format("Id = {0}, Tag = {1}, Length = {2:N0} bytes", this.Id, this.Tag, this.Length);
        }

        /// <summary>
        /// Writes a single byte to the current position in the stream.
        /// </summary>
        /// <param name="value">byte value to write</param>
        /// <exception cref="ObjectDisposedException">Object has been disposed</exception>
        public override void WriteByte(byte value)
        {
            this.CheckDisposed();
            this.byteBuffer[0] = value;
            this.Write(this.byteBuffer, 0, 1);
        }

        /// <summary>
        /// Reads a single byte from the current position in the stream.
        /// </summary>
        /// <returns>The byte at the current position, or -1 if the position is at the end of the stream.</returns>
        /// <exception cref="ObjectDisposedException">Object has been disposed</exception>
        public override int ReadByte()
        {
            this.CheckDisposed();
            if (this.position == this.length)
            {
                return -1;
            }
            byte value = 0;
            if (this.largeBuffer == null)
            {
                var blockAndOffset = this.GetBlockAndRelativeOffset(this.position);
                value = this.blocks[blockAndOffset.Block].Buffer[blockAndOffset.Offset];
            }
            else
            {
                value = this.largeBuffer[position];
            }
            this.position++;
            return value;
        }

        /// <summary>
        /// Sets the length of the stream
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">value is negative or larger than MaxStreamLength</exception>
        /// <exception cref="ObjectDisposedException">Object has been disposed</exception>
        public override void SetLength(long value)
        {
            this.CheckDisposed();
            if (value < 0 || value > MaxStreamLength)
            {
                throw new ArgumentOutOfRangeException("value", "value must be non-negative and at most " + MaxStreamLength);
            }

            this.EnsureCapacity((int)value);

            this.length = (int)value;
            if (this.position > value)
            {
                this.position = (int)value;
            }
        }

        /// <summary>
        /// Sets the position to the offset from the seek location
        /// </summary>
        /// <param name="offset">How many bytes to move</param>
        /// <param name="loc">From where</param>
        /// <returns>The new position</returns>
        /// <exception cref="ObjectDisposedException">Object has been disposed</exception>
        /// <exception cref="ArgumentOutOfRangeException">offset is larger than MaxStreamLength</exception>
        /// <exception cref="ArgumentException">Invalid seek origin</exception>
        /// <exception cref="IOException">Attempt to set negative position</exception>
        public override long Seek(long offset, SeekOrigin loc)
        {
            this.CheckDisposed();
            if (offset > MaxStreamLength)
            {
                throw new ArgumentOutOfRangeException("offset", "offset cannot be larger than " + MaxStreamLength);
            }

            int newPosition;
            switch (loc)
            {
                case SeekOrigin.Begin:
                    newPosition = (int)offset;
                    break;
                case SeekOrigin.Current:
                    newPosition = (int)offset + this.position;
                    break;
                case SeekOrigin.End:
                    newPosition = (int)offset + this.length;
                    break;
                default:
                    throw new ArgumentException("Invalid seek origin", "loc");
            }
            if (newPosition < 0)
            {
                throw new IOException("Seek before beginning");
            }
            this.position = newPosition;
            return this.position;
        }

        /// <summary>
        /// Synchronously writes this stream's bytes to the parameter stream.
        /// </summary>
        /// <param name="stream">Destination stream</param>
        /// <remarks>Important: This does a synchronous write, which may not be desired in some situations</remarks>
        public override void WriteTo(Stream stream)
        {
            this.CheckDisposed();
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }

            if (this.largeBuffer == null)
            {
                int currentBlock = 0;
                int bytesRemaining = this.length;

                while (bytesRemaining > 0)
                {
                    int amountToCopy = FastMath.Min(this.blocks[currentBlock].Length, bytesRemaining);
                    stream.Write(this.blocks[currentBlock].Buffer, 0, amountToCopy);

                    bytesRemaining -= amountToCopy;

                    ++currentBlock;
                }
            }
            else
            {
                stream.Write(this.largeBuffer, 0, this.length);
            }
        }
#endregion

#region Helper Methods
        private void CheckDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(nameof(RecyclableMemoryStream), string.Format("The stream with Id {0} and Tag {1} is disposed.", this.id, this.tag));
            }
        }

        private int InternalRead(byte[] buffer, int offset, int count, int fromPosition)
        {
            if (this.length - fromPosition <= 0)
            {
                return 0;
            }
            if (this.largeBuffer == null)
            {
                var blockAndOffset = this.GetBlockAndRelativeOffset(fromPosition);
                int bytesWritten = 0;
                int bytesRemaining = FastMath.Min(count, this.length - fromPosition);

                while (bytesRemaining > 0)
                {
                    int amountToCopy = FastMath.Min(this.blocks[blockAndOffset.Block].Length - blockAndOffset.Offset, bytesRemaining);
                    ArrayExtensions.MemCopy(this.blocks[blockAndOffset.Block].Buffer, blockAndOffset.Offset, buffer, bytesWritten + offset, amountToCopy);

                    bytesWritten += amountToCopy;
                    bytesRemaining -= amountToCopy;

                    ++blockAndOffset.Block;
                    blockAndOffset.Offset = 0;
                }
                return bytesWritten;
            }
            else
            {
                int amountToCopy = FastMath.Min(count, this.length - fromPosition);
                ArrayExtensions.MemCopy(this.largeBuffer, fromPosition, buffer, offset, amountToCopy);
                return amountToCopy;
            }
        }

        private struct BlockAndOffset
        {
            public int Block;
            public int Offset;

            public BlockAndOffset(int block, int offset)
            {
                this.Block = block;
                this.Offset = offset;
            }
        }

        private BlockAndOffset GetBlockAndRelativeOffset(int offset)
        {
            var blockSize = this.memoryManager.BlockSize;
            return new BlockAndOffset(offset / blockSize, offset % blockSize);
        }

        private void EnsureCapacity(int newCapacity)
        {
            if (newCapacity > this.memoryManager.MaximumStreamCapacity && this.memoryManager.MaximumStreamCapacity > 0)
            {
                throw new InvalidOperationException("Requested capacity is too large: " + newCapacity + ". Limit is " + this.memoryManager.MaximumStreamCapacity);
            }

            if (this.largeBuffer != null)
            {
                if (newCapacity > this.largeBuffer.Length)
                {
                    var newBuffer = this.memoryManager.GetLargeBuffer(newCapacity, this.tag);
                    this.InternalRead(newBuffer, 0, this.length, 0);
                    this.ReleaseLargeBuffer();
                    this.largeBuffer = newBuffer;
                }
            }
            else
            {
                while (this.Capacity < newCapacity)
                {
                    blocks.Add((this.memoryManager.GetBlock()));
                }
            }
        }

        /// <summary>
        /// Release the large buffer (either stores it for eventual release or returns it immediately).
        /// </summary>
        private void ReleaseLargeBuffer()
        {
            if (this.memoryManager.AggressiveBufferReturn)
            {
                this.memoryManager.ReturnLargeBuffer(this.largeBuffer, this.tag);
            }
            else
            {
                if (this.dirtyBuffers == null)
                {
                    // We most likely will only ever need space for one
                    this.dirtyBuffers = new List<byte[]>(1);
                }
                this.dirtyBuffers.Add(this.largeBuffer);
            }

            this.largeBuffer = null;
        }
#endregion
    }
}