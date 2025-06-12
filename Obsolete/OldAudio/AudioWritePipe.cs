using Durandal.Common.IO;
using Durandal.Common.Tasks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio
{
    public class AudioWritePipe : IDisposable
    {
        private readonly PipeStream _basePipe;
        private PipeStream.PipeWriteStream _writeStream;
        private PipeStream.PipeReadStream _readStream;

        // Enforces byte alignment of the input data
        private readonly int _writeChunkSize;
        private readonly int _byteAlignment;
        private readonly byte[] _orphanBytes;
        private int _orphanCount = 0;
        private int _disposed = 0;

        public AudioWritePipe(int byteAlignment = 2)
        {
            _byteAlignment = byteAlignment;
            _orphanBytes = new byte[_byteAlignment - 1];
            _writeChunkSize = _byteAlignment * 512;
            _basePipe = new PipeStream();
            _readStream = _basePipe.GetReadStream();
            _writeStream = _basePipe.GetWriteStream();
        }

        ~AudioWritePipe()
        {
            Dispose(false);
        }

        public AudioReadPipe GetReadPipe()
        {
            if (_readStream == null)
            {
                throw new InvalidOperationException("You can only access the read pipe once, because ownership of the disposable stream is transferred");
            }

            AudioReadPipe returnVal = new AudioReadPipe(_readStream, GetCodec(), GetCodecParams());
            _readStream = null;
            return returnVal;
        }

        protected virtual string GetCodec()
        {
            return string.Empty;
        }

        protected virtual string GetCodecParams()
        {
            return string.Empty;
        }

        public Task WriteAsync(byte[] inputData, int offset, int count)
        {
            Write(inputData, offset, count);
            return DurandalTaskExtensions.NoOpTask;
        }

        public void Write(byte[] inputData, int offset, int count)
        {
            // Chunk the input and pass it to the transformer implementation (to prevent buffer overruns if the transformer is an audio codec or something)
            byte[] inputChunk = null;
            int input_ptr;
            for (input_ptr = 0; input_ptr < count;)
            {
                int thisChunkSize = Math.Min(_writeChunkSize, count - input_ptr + _orphanCount);
                if (thisChunkSize != _writeChunkSize)
                {
                    // this path only happens on the last iteration
                    int newOrphanCount = thisChunkSize % _byteAlignment;

                    int thisChunkSizeByteAligned = thisChunkSize - newOrphanCount;

                    // Is this piece enough to make a complete byte-aligned chunk?
                    // If not, we append to the current list of orphans
                    if (thisChunkSizeByteAligned < _orphanCount)
                    {
                        Array.Copy(inputData, offset + input_ptr, _orphanBytes, _orphanCount, newOrphanCount - _orphanCount);
                        input_ptr += newOrphanCount - _orphanCount;
                        _orphanCount = newOrphanCount;
                        continue;
                    }

                    // create new byte-aligned array
                    inputChunk = new byte[thisChunkSizeByteAligned];

                    if (_orphanCount > 0)
                    {
                        // Copy old orphan data from side buffer
                        Array.Copy(_orphanBytes, 0, inputChunk, 0, _orphanCount);
                    }

                    // Copy byte-aligned data
                    if (thisChunkSizeByteAligned > _orphanCount)
                    {
                        Array.Copy(inputData, offset + input_ptr, inputChunk, _orphanCount, thisChunkSizeByteAligned - _orphanCount);
                    }

                    // Copy new orphans to side buffer
                    if (newOrphanCount > 0)
                    {
                        Array.Copy(inputData, offset + input_ptr + thisChunkSizeByteAligned - _orphanCount, _orphanBytes, 0, newOrphanCount);
                    }

                    input_ptr += thisChunkSizeByteAligned + newOrphanCount;
                    _orphanCount = newOrphanCount;
                }
                else
                {
                    if (inputChunk == null)
                    {
                        inputChunk = new byte[_writeChunkSize];
                    }

                    if (_orphanCount > 0)
                    {
                        Array.Copy(_orphanBytes, 0, inputChunk, 0, _orphanCount);
                    }

                    Array.Copy(inputData, offset + input_ptr, inputChunk, _orphanCount, thisChunkSize - _orphanCount);
                    input_ptr += thisChunkSize - _orphanCount;
                    _orphanCount = 0; // always set to zero since the write buffer is byte-aligned
                }

                if (inputChunk.Length > 0)
                {
                    byte[] transformedData = TransformOutput(inputChunk);
                    if (transformedData != null && transformedData.Length > 0)
                    {
                        _writeStream.Write(transformedData, 0, transformedData.Length);
                    }
                }
            }
        }

        public void CloseWrite()
        {
            if (_writeStream == null)
            {
                throw new InvalidOperationException("Write pipe is already closed");
            }

            // Allow subclasses such as audio compressor streams to flush their output before we finally close the stream
            byte[] finalizedOutput = FinalizeOutput();
            if (finalizedOutput != null && finalizedOutput.Length > 0)
            {
                Write(finalizedOutput, 0, finalizedOutput.Length);
            }

            _writeStream.Dispose();
            _writeStream = null;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            {
                return;
            }

            if (!disposing) Durandal.Common.Utils.DebugMemoryLeaktracer.TraceDisposableItemFinalized(this.GetType());

            if (disposing)
            {
                _writeStream?.Dispose();
                _readStream?.Dispose();
                _basePipe?.Dispose();
            }
        }

        /// <summary>
        /// This method allows subclasses to transform raw data that is written to the buffer
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        protected virtual byte[] TransformOutput(byte[] input)
        {
            return input;
        }

        /// <summary>
        /// This method allows subclasses to flush internal buffers before closing the write stream.
        /// Invoking this method signals that no further data will be written to this stream.
        /// </summary>
        /// <returns></returns>
        protected virtual byte[] FinalizeOutput()
        {
            return null;
        }
    }
}
