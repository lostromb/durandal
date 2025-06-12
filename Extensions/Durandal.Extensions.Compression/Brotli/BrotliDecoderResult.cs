using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Extensions.Compression.Brotli
{
    /// <summary>
    /// Enumeration of Brotli decoder results.
    /// </summary>
    public enum BrotliDecoderResult : int
    {
        /// <summary>
        /// Decoding error, e.g. corrupt input or memory allocation problem
        /// </summary>
        Error = 0,

        /// <summary>
        /// Decoding successfully completed
        /// </summary>
        Success = 1,

        /// <summary>
        /// Partially done; should be called again with more input
        /// </summary>
        NeedsMoreInput = 2,

        /// <summary>
        /// Partially done; should be called again with more output
        /// </summary>
        NeedsMoreOutput = 3
    }
}
