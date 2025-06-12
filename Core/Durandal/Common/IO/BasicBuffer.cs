using Durandal.Common.Collections;
using Durandal.Common.MathExt;
using System;

namespace Durandal.Common.IO
{
    /// <summary>
    /// A very simple, fixed-size read-write buffer.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class BasicBuffer<T>
    {
        private T[] data;
        private int writeIndex = 0;
        private int readIndex = 0;
        private int available = 0;
        private int capacity = 0;

        public BasicBuffer(int capacity)
        {
            this.capacity = capacity;
            data = new T[capacity];
        }

        /// <summary>
        /// Writes an entire array to the buffer
        /// </summary>
        /// <param name="toWrite"></param>
        public void Write(T[] toWrite)
        {
            Write(toWrite, 0, toWrite.Length);
        }

        /// <summary>
        /// Writes a portion of an array to the buffer
        /// </summary>
        /// <param name="toWrite"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public void Write(T[] toWrite, int offset, int count)
        {
            if (toWrite.Length < offset + count)
            {
                throw new IndexOutOfRangeException();
            }

            // Write the data in chunks
            int itemsCopied = 0;
            while (itemsCopied < count)
            {
                int thisCopyCount = FastMath.Min(count - itemsCopied, capacity - writeIndex);
                ArrayExtensions.MemCopy(toWrite, itemsCopied + offset, data, writeIndex, thisCopyCount);
                writeIndex = (writeIndex + thisCopyCount) % capacity;
                itemsCopied += thisCopyCount;
            }

            available += count;
            if (available > capacity)
            {
                // Did we overflow? In this case, move the readIndex to just after the writeIndex
                readIndex = (writeIndex + 1) % capacity;
                available = capacity;
                throw new IndexOutOfRangeException("Buffer has exceeded capacity");
            }
        }

        /// <summary>
        /// Writes a single value to the buffer
        /// </summary>
        /// <param name="toWrite"></param>
        public void Write(T toWrite)
        {
            data[writeIndex] = toWrite;
            writeIndex = (writeIndex + 1) % capacity;
            available += 1;
            if (available > capacity)
            {
                readIndex = (writeIndex + 1) % capacity;
                available = capacity;
                throw new IndexOutOfRangeException("Buffer has exceeded capacity");
            }
        }

        public void Clear()
        {
            writeIndex = 0;
            readIndex = 0;
            available = 0;
        }

        /// <summary>
        /// Read up to the specified amount to the buffer
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public T[] Read(int count)
        {
            T[] returnVal = new T[count];
            // Read the data in chunks
            int sourceIndex = 0;
            while (sourceIndex < count)
            {
                int readCount = FastMath.Min(count - sourceIndex, capacity - readIndex);
                ArrayExtensions.MemCopy(data, readIndex, returnVal, sourceIndex, readCount);
                readIndex = (readIndex + readCount) % capacity;
                sourceIndex += readCount;
            }
            available -= count;
            if (available < 0)
            {
                // Did we underflow? In this case, move the writeIndex to where the next data will be read
                writeIndex = (readIndex + 1) % capacity;
                available = 0;
            }

            return returnVal;
        }

        /// <summary>
        /// Read up to the specified amount to the buffer
        /// </summary>
        /// <param name="target"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public int Read(T[] target, int offset, int count)
        {
            // Read the data in chunks
            int sourceIndex = 0;
            while (sourceIndex < count)
            {
                int readCount = FastMath.Min(count - sourceIndex, capacity - readIndex);
                ArrayExtensions.MemCopy(data, readIndex, target, sourceIndex + offset, readCount);
                readIndex = (readIndex + readCount) % capacity;
                sourceIndex += readCount;
            }
            available -= count;

            if (available < 0)
            {
                // Did we underflow? In this case, move the writeIndex to where the next data will be read
                writeIndex = (readIndex + 1) % capacity;
                available = 0;
            }

            return count;
        }

        /// <summary>
        /// Reads a single value
        /// </summary>
        /// <returns></returns>
        public T Read()
        {
            T returnVal = data[readIndex];
            readIndex = (readIndex + 1) % capacity;
            available -= 1;
            if (available < 0)
            {
                writeIndex = (readIndex + 1) % capacity;
                available = 0;
            }

            return returnVal;
        }

        /// <summary>
        /// Reads from the buffer without actually consuming the data
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public T[] Peek(int count)
        {
            int toRead = FastMath.Min(available, count);
            T[] returnVal = new T[toRead];
            // Read the data in chunks
            int sourceIndex = 0;
            int localReadIndex = readIndex;
            while (sourceIndex < toRead)
            {
                int readCount = FastMath.Min(toRead - sourceIndex, capacity - localReadIndex);
                ArrayExtensions.MemCopy(data, localReadIndex, returnVal, sourceIndex, readCount);
                localReadIndex = (localReadIndex + readCount) % capacity;
                sourceIndex += readCount;
            }

            return returnVal;
        }

        public int Available => available;
        public int Capacity => capacity;
    }

    /// <summary>
    /// A drop-in implementation of BasicBuffer strongly typed to the int16 data.
    /// This has better performance in the CLR because it avoids the boxed arrays used in the generic buffer class
    /// </summary>
    public class BasicBufferShort
    {
        private short[] data;
        private int writeIndex = 0;
        private int readIndex = 0;
        private int available = 0;
        private int capacity = 0;

        public BasicBufferShort(int capacity)
        {
            this.capacity = capacity;
            data = new short[capacity];
        }

        /// <summary>
        /// Writes an entire array to the buffer
        /// </summary>
        /// <param name="toWrite"></param>
        public void Write(short[] toWrite)
        {
            Write(toWrite, 0, toWrite.Length);
        }

        /// <summary>
        /// Writes a portion of an array to the buffer
        /// </summary>
        /// <param name="toWrite"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public void Write(short[] toWrite, int offset, int count)
        {
            if (toWrite.Length < offset + count)
            {
                throw new IndexOutOfRangeException();
            }

            // Write the data in chunks
            int itemsCopied = 0;
            while (itemsCopied < count)
            {
                int thisCopyCount = FastMath.Min(count - itemsCopied, capacity - writeIndex);
                ArrayExtensions.MemCopy(toWrite, (itemsCopied + offset), data, writeIndex, thisCopyCount);
                writeIndex = (writeIndex + thisCopyCount) % capacity;
                itemsCopied += thisCopyCount;
            }

            available += count;
            if (available > capacity)
            {
                // Did we overflow? In this case, move the readIndex to just after the writeIndex
                readIndex = (writeIndex + 1) % capacity;
                available = capacity;
                throw new IndexOutOfRangeException("Buffer has exceeded capacity");
            }
        }

        /// <summary>
        /// Writes a single value
        /// </summary>
        /// <param name="toWrite"></param>
        public void Write(short toWrite)
        {
            data[writeIndex] = toWrite;
            writeIndex = (writeIndex + 1) % capacity;
            available += 1;
            if (available > capacity)
            {
                readIndex = (writeIndex + 1) % capacity;
                available = capacity;
                throw new IndexOutOfRangeException("Buffer has exceeded capacity");
            }
        }

        public void Clear()
        {
            writeIndex = 0;
            readIndex = 0;
            available = 0;
        }

        public short[] Read(int count)
        {
            short[] returnVal = new short[count];
            // Read the data in chunks
            int sourceIndex = 0;
            while (sourceIndex < count)
            {
                int readCount = FastMath.Min(count - sourceIndex, capacity - readIndex);
                ArrayExtensions.MemCopy(data, readIndex, returnVal, sourceIndex, readCount);
                readIndex = (readIndex + readCount) % capacity;
                sourceIndex += readCount;
            }
            available -= count;
            if (available < 0)
            {
                // Did we underflow? In this case, move the writeIndex to where the next data will be read
                writeIndex = (readIndex + 1) % capacity;
                available = 0;
            }

            return returnVal;
        }

        /// <summary>
        /// Reads a single value
        /// </summary>
        /// <returns></returns>
        public short Read()
        {
            short returnVal = data[readIndex];
            readIndex = (readIndex + 1) % capacity;
            available -= 1;
            if (available < 0)
            {
                writeIndex = (readIndex + 1) % capacity;
                available = 0;
            }
            return returnVal;
        }

        /// <summary>
        /// Reads from the buffer without actually consuming the data
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public short[] Peek(int count)
        {
            int toRead = FastMath.Min(available, count);
            short[] returnVal = new short[toRead];
            // Read the data in chunks
            int sourceIndex = 0;
            int localReadIndex = readIndex;
            while (sourceIndex < toRead)
            {
                int readCount = FastMath.Min(toRead - sourceIndex, capacity - localReadIndex);
                ArrayExtensions.MemCopy(data, localReadIndex, returnVal, sourceIndex, readCount);
                localReadIndex = (localReadIndex + readCount) % capacity;
                sourceIndex += readCount;
            }

            return returnVal;
        }

        public int Available => available;
        public int Capacity => capacity;
    }

    /// <summary>
    /// A drop-in implementation of BasicBuffer strongly typed to the uint8 data.
    /// This has better performance in the CLR because it avoids the boxed arrays used in the generic buffer class
    /// </summary>
    public class BasicBufferByte
    {
        private byte[] data;
        private int writeIndex = 0;
        private int readIndex = 0;
        private int available = 0;
        private int capacity = 0;

        public BasicBufferByte(int capacity)
        {
            this.capacity = capacity;
            data = new byte[capacity];
        }

        /// <summary>
        /// Writes an entire array to the buffer
        /// </summary>
        /// <param name="toWrite"></param>
        public void Write(byte[] toWrite)
        {
            Write(toWrite, 0, toWrite.Length);
        }

        /// <summary>
        /// Writes a portion of an array to the buffer
        /// </summary>
        /// <param name="toWrite"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public void Write(byte[] toWrite, int offset, int count)
        {
            if (toWrite.Length < offset + count)
            {
                throw new IndexOutOfRangeException();
            }

            // Write the data in chunks
            int itemsCopied = 0;
            while (itemsCopied < count)
            {
                int thisCopyCount = FastMath.Min(count - itemsCopied, capacity - writeIndex);
                ArrayExtensions.MemCopy(toWrite, itemsCopied + offset, data, writeIndex, thisCopyCount);
                writeIndex = (writeIndex + thisCopyCount) % capacity;
                itemsCopied += thisCopyCount;
            }

            available += count;
            if (available > capacity)
            {
                // Did we overflow? In this case, move the readIndex to just after the writeIndex
                readIndex = (writeIndex + 1) % capacity;
                available = capacity;
                throw new IndexOutOfRangeException("Buffer has exceeded capacity");
            }
        }

        /// <summary>
        /// Writes a single value
        /// </summary>
        /// <param name="toWrite"></param>
        public void Write(byte toWrite)
        {
            data[writeIndex] = toWrite;
            writeIndex = (writeIndex + 1) % capacity;
            available += 1;
            if (available > capacity)
            {
                readIndex = (writeIndex + 1) % capacity;
                available = capacity;
                throw new IndexOutOfRangeException("Buffer has exceeded capacity");
            }
        }

        public void Clear()
        {
            writeIndex = 0;
            readIndex = 0;
            available = 0;
        }

        [Obsolete("Use the alternate version which writes to an existing buffer")]
        public byte[] Read(int count)
        {
            byte[] returnVal = new byte[count];
            Read(returnVal, 0, count);
            return returnVal;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            // Read the data in chunks
            int sourceIndex = 0;
            while (sourceIndex < count)
            {
                int readCount = FastMath.Min(count - sourceIndex, capacity - readIndex);
                ArrayExtensions.MemCopy(data, readIndex, buffer, offset + sourceIndex, readCount);
                readIndex = (readIndex + readCount) % capacity;
                sourceIndex += readCount;
            }
            available -= count;
            if (available < 0)
            {
                // Did we underflow? In this case, move the writeIndex to where the next data will be read
                writeIndex = (readIndex + 1) % capacity;
                available = 0;
            }

            return count;
        }

        /// <summary>
        /// Reads a single value
        /// </summary>
        /// <returns></returns>
        public byte Read()
        {
            byte returnVal = data[readIndex];
            readIndex = (readIndex + 1) % capacity;
            available -= 1;
            if (available < 0)
            {
                writeIndex = (readIndex + 1) % capacity;
                available = 0;
            }

            return returnVal;
        }

        /// <summary>
        /// Reads from the buffer without actually consuming the data
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public byte[] Peek(int count)
        {
            int toRead = FastMath.Min(available, count);
            byte[] returnVal = new byte[toRead];
            // Read the data in chunks
            int sourceIndex = 0;
            int localReadIndex = readIndex;
            while (sourceIndex < toRead)
            {
                int readCount = FastMath.Min(toRead - sourceIndex, capacity - localReadIndex);
                ArrayExtensions.MemCopy(data, localReadIndex, returnVal, sourceIndex, readCount);
                localReadIndex = (localReadIndex + readCount) % capacity;
                sourceIndex += readCount;
            }

            return returnVal;
        }

        public int Available => available;
        public int Capacity => capacity;
    }
}
