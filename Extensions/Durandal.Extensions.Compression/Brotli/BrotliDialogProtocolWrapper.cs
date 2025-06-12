namespace Durandal.Extensions.Compression.Brotli
{
    using Durandal.API;
    using Durandal.Common.Dialog.Web;
    using Durandal.Common.IO;
    using Durandal.Common.Logger;
    using System;
    using System.IO;

    /// <summary>
    /// Wraps a Brotli compression layer around an existing dialog transport protocol, prefixing the protocol name with "br"
    /// </summary>
    public abstract class BrotliDialogProtocolWrapper : IDialogTransportProtocol
    {
        private readonly IDialogTransportProtocol _innerProtocol;
        private readonly uint _compressLevel;
        private readonly uint _windowBits;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="innerProtocol">The dialog protocol to wrap</param>
        /// <param name="compressLevel">The Brotli compression level, from 0 to 11</param>
        /// <param name="compressWindowBits">The Brotli sliding window size, in bits, from 10 to 24</param>
        public BrotliDialogProtocolWrapper(
            IDialogTransportProtocol innerProtocol,
            uint compressLevel = 6,
            uint compressWindowBits = 16)
        {
            _innerProtocol = innerProtocol;
            _compressLevel = compressLevel;
            _windowBits = compressWindowBits;
        }

        /// <inheritdoc />
        public string ContentEncoding => "br";

        /// <inheritdoc />
        public string MimeType => _innerProtocol.MimeType;

        /// <inheritdoc />
        public string ProtocolName => "br" + _innerProtocol.ProtocolName;

        /// <inheritdoc />
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Uncompressed buffer should be disposed by inner protocol")]
        public DialogRequest ParseClientRequest(PooledBuffer<byte> compressed, ILogger logger)
        {
            PooledBuffer<byte> uncompressed = Decompress(compressed.AsArraySegment);
            compressed.Dispose();
            return _innerProtocol.ParseClientRequest(uncompressed, logger);
        }

        /// <inheritdoc />
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Uncompressed buffer should be disposed by inner protocol")]
        public DialogRequest ParseClientRequest(ArraySegment<byte> compressed, ILogger logger)
        {
            PooledBuffer<byte> uncompressed = Decompress(compressed);
            return _innerProtocol.ParseClientRequest(uncompressed, logger);
        }

        /// <inheritdoc />
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Uncompressed buffer should be disposed by inner protocol")]
        public DialogResponse ParseClientResponse(PooledBuffer<byte> compressed, ILogger logger)
        {
            PooledBuffer<byte> uncompressed = Decompress(compressed.AsArraySegment);
            compressed.Dispose();
            return _innerProtocol.ParseClientResponse(uncompressed, logger);
        }

        /// <inheritdoc />
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Uncompressed buffer should be disposed by inner protocol")]
        public DialogResponse ParseClientResponse(ArraySegment<byte> compressed, ILogger logger)
        {
            PooledBuffer<byte> uncompressed = Decompress(compressed);
            return _innerProtocol.ParseClientResponse(uncompressed, logger);
        }

        /// <inheritdoc />
        public PooledBuffer<byte> WriteClientRequest(DialogRequest input, ILogger logger)
        {
            using (PooledBuffer<byte> uncompressed = _innerProtocol.WriteClientRequest(input, logger))
            {
                return Compress(uncompressed);
            }
        }

        /// <inheritdoc />
        public PooledBuffer<byte> WriteClientResponse(DialogResponse input, ILogger logger)
        {
            using (PooledBuffer<byte> uncompressed = _innerProtocol.WriteClientResponse(input, logger))
            {
                return Compress(uncompressed);
            }
        }

        private PooledBuffer<byte> Compress(PooledBuffer<byte> uncompressed)
        {
            using (RecyclableMemoryStream outStream = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
            {
                //Span<byte> lengthHeaderBytes = stackalloc byte[4];
                //BinaryHelpers.Int32ToByteSpanLittleEndian(uncompressed.Length, ref lengthHeaderBytes);
                //outStream.Write(lengthHeaderBytes);

                using (BrotliCompressorStream compressor = new BrotliCompressorStream(outStream, leaveOpen: true, quality: _compressLevel, window: _windowBits))
                using (MemoryStream inStream = new MemoryStream(uncompressed.Buffer, 0, uncompressed.Length))
                {
                    inStream.CopyToPooled(compressor);
                }

                return outStream.ToPooledBuffer();
            }
        }

        private PooledBuffer<byte> Decompress(ArraySegment<byte> compressed)
        {
            //int uncompressedLength = BinaryHelpers.ByteArrayToInt32LittleEndian(compressed.Array, compressed.Offset);

            using (RecyclableMemoryStream outStream = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
            {
                using (MemoryStream inStream = new MemoryStream(compressed.Array, compressed.Offset, compressed.Count))
                using (BrotliDecompressorStream decompressor = new BrotliDecompressorStream(inStream))
                {
                    decompressor.CopyToPooled(outStream);
                }

                return outStream.ToPooledBuffer();
            }
        }
    }
}
