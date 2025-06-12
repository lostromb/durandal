using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Durandal.Extensions.Compression.Brotli
{
    /// <summary>
    /// Abstract interface for a Brotli library, independent of platform or architecture.
    /// Usually backed by a P/Invoke layer for the C x64 library, but it could theoretically
    /// be a managed port if we wanted.
    /// </summary>
    public interface IBrolib
    {
        /// <summary>
        /// Gets the current native library's encoder version.
        /// </summary>
        uint EncoderVersion { get; }

        /// <summary>
        /// Creates an instance of ::BrotliEncoderState and initializes it.
        /// </summary>
        /// <returns>0 if instance can not be allocated or initialized,
        /// or a pointer to initialized ::BrotliEncoderState otherwise</returns>
        SafeHandle CreateEncoder();

        /// <summary>
        /// Sets the specified parameter to the given encoder instance.
        /// </summary>
        /// <param name="encoder">The encoder instance.</param>
        /// <param name="parameter">The parameter to set.</param>
        /// <param name="value">The new parameter value.</param>
        void EncoderSetParameter(SafeHandle encoder, BrotliEncoderParameter parameter, uint value);

        /// <summary>
        /// Compresses data to Brotli as a stream.
        /// https://github.com/google/brotli/blob/ed1995b6bda19244070ab5d331111f16f67c8054/c/include/brotli/encode.h#L426
        /// </summary>
        /// <param name="encoder">The encoder handle.</param>
        /// <param name="op">The operation code - for us this indicates whether this is the last block or not.</param>
        /// <param name="availableIn">Input buffer length.</param>
        /// <param name="nextIn">A pointer to input data.</param>
        /// <param name="availableOut">Output buffer length.</param>
        /// <param name="nextOut">A pointer to output buffer.</param>
        /// <param name="totalOut">Total output bytes (see encode.h documentation).</param>
        /// <returns>True if the operation succeeded.</returns>
        bool EncoderCompressStream(
            SafeHandle encoder,
            BrotliEncoderOperation op,
            ref ulong availableIn,
            ref IntPtr nextIn,
            ref ulong availableOut,
            ref IntPtr nextOut,
            out ulong totalOut);

        /// <summary>
        /// Checks if encoder instance reached the final state.
        /// </summary>
        /// <param name="encoder">The encoder handle</param>
        /// <returns>True if encoder is in a state where it reached the end of the input and produced all of the output.</returns>
        bool EncoderIsFinished(SafeHandle encoder);

        /// <summary>
        /// Creates a Brotli decoder object.
        /// </summary>
        /// <returns>A safe handle to the decoder struct.</returns>
        SafeHandle CreateDecoder();

        /// <summary>
        /// Decompresses Brotli data as a stream.
        /// </summary>
        /// <param name="decoder">A decoder handle.</param>
        /// <param name="availableIn">Size of the input buffer</param>
        /// <param name="nextIn">Pointer to input data.</param>
        /// <param name="availableOut">Size of the output buffer.</param>
        /// <param name="nextOut">Pointer to output buffer.</param>
        /// <param name="totalOut">Number of bytes produced in this operation.</param>
        /// <returns>A decoder result enum.</returns>
        BrotliDecoderResult DecoderDecompressStream(
            SafeHandle decoder,
            ref ulong availableIn,
            ref IntPtr nextIn,
            ref ulong availableOut,
            ref IntPtr nextOut,
            out ulong totalOut);

        /// <summary>
        /// Checks if decoder instance reached the final state.
        /// </summary>
        /// <param name="decoder">The decoder handle.</param>
        /// <returns>True if decoder is in a state where it reached the end of the input and produced all of the output.</returns>
        bool DecoderIsFinished(SafeHandle decoder);

        /// <summary>
        /// Gets the result of the latest decoder <see cref="DecoderDecompressStream"/> operation as a string.
        /// </summary>
        /// <param name="decoder">The decoder handle.</param>
        /// <returns>An error string, potentially empty but never null.</returns>
        string DecoderGetErrorString(SafeHandle decoder);
    }
}
