using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stromberg.Utils
{
    using System.IO;
    using System.Threading;

    /// <summary>
    /// Not an actual threaded stream reader
    /// </summary>
    public class NotThreadedStreamReader
    {
        private const int BLOCK_SIZE = 128;
        private BasicBuffer<byte> _buffer;
        private bool _closed;
        private Stream _inStream;
        private int _bufferSize;

        public NotThreadedStreamReader(Stream inputStream, int blockSize = 1024, int bufferSize = -1)
        {
            if (bufferSize < 0)
            {
                bufferSize = blockSize * 32;
            }
            if (bufferSize < blockSize * 4)
            {
                throw new ArgumentException("Buffer size must be at least 4x the block size");
            }
            _closed = false;
            _buffer = new BasicBuffer<byte>(bufferSize);
            _inStream = inputStream;
            _bufferSize = bufferSize;
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
            Advance();
            if (EndOfStream)
            {
                // Definitive end-of-stream
                return -1;
            }

            int available = Math.Min(_buffer.Available(), length);
            if (available > 0)
            {
                byte[] chunk = _buffer.Read(available);
                Array.Copy(chunk, 0, data, offset, available);
            }
            return available;
        }

        public int Available()
        {
            return _buffer.Available();
        }

        public bool EndOfStream
        {
            get
            {
                return _closed && _buffer.Available() == 0;
            }
        }

        /// <summary>
        /// Dumps all the data in the buffer to the specified output stream, but does not close either pipe.
        /// </summary>
        /// <param name="output"></param>
        public void FlushToStream(Stream output)
        {
            Advance();
            while (this.Available() > 0)
            {
                byte[] newBlock = new byte[Math.Max(BLOCK_SIZE, Available())];
                int bytesRead = Read(newBlock, 0, newBlock.Length);
                if (bytesRead > 0)
                {
                    output.Write(newBlock, 0, bytesRead);
                }
                Advance();
            }
        }

        private void Advance()
        {
            byte[] buf = new byte[BLOCK_SIZE];
            int check = 0;
            byte[] checkByte = new byte[1];

            // Don't overfill the target buffer; break instead
            if (_buffer.Available() > _bufferSize - (BLOCK_SIZE * 4))
            {
                return;
            }

            // BLOCKING CALL
            int bytesRead = _inStream.Read(buf, 0, BLOCK_SIZE);
            if (bytesRead == 0)
            {
                // Reached end-of-stream, mark the underlying stream as being closed
                _closed = true;
                return;
            }

            byte[] newChunk = new byte[bytesRead];
            Array.Copy(buf, 0, newChunk, 0, bytesRead);
            _buffer.Write(newChunk);

            // Use the ReadByte() method to determine if end-of-stream or not (-1 indicates end)
            check = _inStream.ReadByte();

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
