using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Extensions.Compression.Brotli
{
    /// <summary>
    /// Enumeration of Brotli decoder operations.
    /// </summary>
    public enum BrotliDecoderParameter : int
    {
        /// <summary>
        /// Disable "canny" ring buffer allocation strategy.
        /// Ring buffer is allocated according to window size, despite the real size of
        /// the content.
        /// Not relevant to C# code since we don't use an allocator.
        /// </summary>
        DisableRingBufferReallocation = 0,

        /// <summary>
        /// Flag that determines if "Large Window Brotli" is used.
        /// </summary>
        LargeWindow = 1,
    }
}
