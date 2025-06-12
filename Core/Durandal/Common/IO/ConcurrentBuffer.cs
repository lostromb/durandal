namespace Durandal.Common.IO
{
    /// <summary>
    /// A BasicBuffer with thread-safe access
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ConcurrentBuffer<T>
    {
        private BasicBuffer<T> _buffer;

        public ConcurrentBuffer(int capacity)
        {
            _buffer = new BasicBuffer<T>(capacity);
        }

        public void Write(T[] toWrite)
        {
            lock (_buffer)
            {
                _buffer.Write(toWrite);
            }
        }

        public void Write(T[] toWrite, int offset, int count)
        {
            lock (_buffer)
            {
                _buffer.Write(toWrite, offset, count);
            }
        }

        public void Clear()
        {
            lock (_buffer)
            {
                _buffer.Clear();
            }
        }

        public T[] Read(int count)
        {
            lock (_buffer)
            {
                return _buffer.Read(count);
            }
        }

        /// <summary>
        /// Reads from the buffer without actually consuming the data
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public T[] Peek(int count)
        {
            lock (_buffer)
            {
                return _buffer.Peek(count);
            }
        }

        public int Available()
        {
            lock (_buffer)
            {
                return _buffer.Available;
            }
        }

        public int Capacity()
        {
            return _buffer.Capacity;
        }
    }
}
