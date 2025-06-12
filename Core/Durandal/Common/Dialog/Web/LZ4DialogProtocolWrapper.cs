using System;
using System.Collections.Generic;
using System.Text;
using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.IO;
using Durandal.Common.Compression.LZ4;
using System.IO;
using Durandal.Common.Utils;

namespace Durandal.Common.Dialog.Web
{
    /// <summary>
    /// Wraps an LZ4 compression layer around an existing dialog transport protocol, prefixing the protocol name with "lz4"
    /// </summary>
    public abstract class LZ4DialogProtocolWrapper : IDialogTransportProtocol
    {
        private readonly IDialogTransportProtocol _innerProtocol;

        public LZ4DialogProtocolWrapper(IDialogTransportProtocol innerProtocol)
        {
            _innerProtocol = innerProtocol;
        }

        public string ContentEncoding => "lz4";

        public string MimeType => _innerProtocol.MimeType;

        public string ProtocolName => "lz4" + _innerProtocol.ProtocolName;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Uncompressed buffer should be disposed by inner protocol")]
        public DialogRequest ParseClientRequest(PooledBuffer<byte> compressed, ILogger logger)
        {
            PooledBuffer<byte> uncompressed = Decompress(compressed.AsArraySegment);
            compressed.Dispose();
            return _innerProtocol.ParseClientRequest(uncompressed, logger);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Uncompressed buffer should be disposed by inner protocol")]
        public DialogRequest ParseClientRequest(ArraySegment<byte> compressed, ILogger logger)
        {
            PooledBuffer<byte> uncompressed = Decompress(compressed);
            return _innerProtocol.ParseClientRequest(uncompressed, logger);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Uncompressed buffer should be disposed by inner protocol")]
        public DialogResponse ParseClientResponse(PooledBuffer<byte> compressed, ILogger logger)
        {
            PooledBuffer<byte> uncompressed = Decompress(compressed.AsArraySegment);
            compressed.Dispose();
            return _innerProtocol.ParseClientResponse(uncompressed, logger);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Uncompressed buffer should be disposed by inner protocol")]
        public DialogResponse ParseClientResponse(ArraySegment<byte> compressed, ILogger logger)
        {
            PooledBuffer<byte> uncompressed = Decompress(compressed);
            return _innerProtocol.ParseClientResponse(uncompressed, logger);
        }

        public PooledBuffer<byte> WriteClientRequest(DialogRequest input, ILogger logger)
        {
            using (PooledBuffer<byte> uncompressed = _innerProtocol.WriteClientRequest(input, logger))
            {
                return Compress(uncompressed);
            }
        }

        public PooledBuffer<byte> WriteClientResponse(DialogResponse input, ILogger logger)
        {
            using (PooledBuffer<byte> uncompressed = _innerProtocol.WriteClientResponse(input, logger))
            {
                return Compress(uncompressed);
            }
        }

        private static PooledBuffer<byte> Compress(PooledBuffer<byte> uncompressed)
        {
            using (RecyclableMemoryStream outStream = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
            {
                using (LZ4Stream compressor = new LZ4Stream(outStream, LZ4StreamMode.Compress, LZ4StreamFlags.IsolateInnerStream))
                {
                    Span<byte> lengthHeaderBytes = stackalloc byte[4];
                    BinaryHelpers.Int32ToByteSpanLittleEndian(uncompressed.Length, ref lengthHeaderBytes);
                    compressor.Write(lengthHeaderBytes);

                    using (MemoryStream inStream = new MemoryStream(uncompressed.Buffer, 0, uncompressed.Length))
                    {
                        inStream.CopyToPooled(compressor);
                        //inStream.Flush();
                    }
                }

                return outStream.ToPooledBuffer();
            }
        }

        private static PooledBuffer<byte> Decompress(ArraySegment<byte> compressed)
        {
            using (RecyclableMemoryStream outStream = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
            {
                using (MemoryStream inStream = new MemoryStream(compressed.Array, compressed.Offset, compressed.Count))
                {
                    using (LZ4Stream decompressor = new LZ4Stream(inStream, LZ4StreamMode.Decompress))
                    {
                        Span<byte> lengthHeaderBytes = stackalloc byte[4];
                        decompressor.Read(lengthHeaderBytes);
                        int uncompressedLength = BinaryHelpers.ByteSpanToInt32LittleEndian(ref lengthHeaderBytes);
                        decompressor.CopyToPooled(outStream);
                        //decompressor.Flush();
                    }
                }

                return outStream.ToPooledBuffer();
            }
        }
    }
}
