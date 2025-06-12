using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Extensions.Compression.Brotli
{
    /// <summary>
    /// Enumeration of Brotli encoder operations.
    /// </summary>
    public enum BrotliEncoderOperation : int
    {
        /// <summary>
        /// Process input.
        /// Encoder may postpone producing output, until it has processed enough input.
        /// </summary>
        Process = 0,

        /// <summary>
        /// Request output stream to flush. Performed when input stream is depleted
        /// and there is enough space in output stream.
        /// </summary>
        Flush = 1,

        /// <summary>
        /// Request output stream to finish. Performed when input stream is depleted
        /// and there is enough space in output stream.
        /// </summary>
        Finish = 2,

        /// <summary>
        /// Emits metadata block to stream. Stream is soft-flushed before metadata
        /// block is emitted. CAUTION: when operation is started, length of the input
        /// buffer is interpreted as length of a metadata block; changing operation,
        /// expanding or truncating input before metadata block is completely emitted
        /// will cause an error; metadata block must not be greater than 16MiB.
        /// </summary>
        EmitMetadata = 3
    }
}
