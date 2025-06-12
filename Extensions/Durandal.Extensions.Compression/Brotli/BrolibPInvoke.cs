namespace Durandal.Extensions.Compression.Brotli
{
    using System;
    using System.Runtime.ConstrainedExecution;
    using System.Runtime.InteropServices;
    using Microsoft.Win32.SafeHandles;

    /// <summary>
    /// P/Invoke Brotli bindings - platform and bitness independent.
    /// </summary>
    internal class BrolibPInvoke : IBrolib
    {
        private const string LibraryName = "brotli";

        /// <inheritdoc />
        public SafeHandle CreateEncoder()
        {
            BrotliEncoderHandle returnVal = BrotliEncoderCreateInstance(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            return returnVal;
        }

        /// <inheritdoc />
        public uint EncoderVersion => BrotliEncoderVersion();

        /// <inheritdoc />
        public void EncoderSetParameter(SafeHandle encoder, BrotliEncoderParameter parameter, uint value)
        {
            if (!(encoder is BrotliEncoderHandle encoderHandle))
            {
                throw new Exception("Brotli encoder handle is invalid");
            }

            BrotliEncoderSetParameter(encoderHandle, parameter, value);
        }

        /// <inheritdoc />
        public bool EncoderCompressStream(
            SafeHandle encoder,
            BrotliEncoderOperation op,
            ref ulong availableIn,
            ref IntPtr nextIn,
            ref ulong availableOut,
            ref IntPtr nextOut,
            out ulong totalOut)
        {
            if (!(encoder is BrotliEncoderHandle encoderHandle))
            {
                throw new Exception("Brotli encoder handle is invalid");
            }

            // The width of parameters varies based on architecture because the C code uses size_t.
            // Ideally we would just use nuint to get the right size automatically, but this is old C# so we have to use IntPtr
            UIntPtr inNint = new UIntPtr(availableIn);
            UIntPtr outNint = new UIntPtr(availableOut);
            UIntPtr totalNint;
            bool returnVal = BrotliEncoderCompressStream(encoderHandle, op, ref inNint, ref nextIn, ref outNint, ref nextOut, out totalNint);
            availableIn = inNint.ToUInt64();
            availableOut = outNint.ToUInt64();
            totalOut = totalNint.ToUInt64();
            return returnVal;
        }

        /// <inheritdoc />
        public bool EncoderIsFinished(SafeHandle encoder)
        {
            if (!(encoder is BrotliEncoderHandle encoderHandle))
            {
                throw new Exception("Brotli encoder handle is invalid");
            }

            return BrotliEncoderIsFinished(encoderHandle);
        }

        /// <inheritdoc />
        public SafeHandle CreateDecoder()
        {
            BrotliDecoderHandle returnVal = BrotliDecoderCreateInstance(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            return returnVal;
        }

        /// <inheritdoc />
        public uint DecoderVersion => BrotliDecoderVersion();

        /// <inheritdoc />
        public BrotliDecoderResult DecoderDecompressStream(
            SafeHandle decoder,
            ref ulong availableIn,
            ref IntPtr nextIn,
            ref ulong availableOut,
            ref IntPtr nextOut,
            out ulong totalOut)
        {
            if (!(decoder is BrotliDecoderHandle decoderHandle))
            {
                throw new Exception("Brotli decoder handle is invalid");
            }

            // The width of parameters varies based on architecture because the C code uses size_t.
            // Ideally we would just use nuint to get the right size automatically, but this is old C# so we have to use IntPtr
            UIntPtr inNint = new UIntPtr(availableIn);
            UIntPtr outNint = new UIntPtr(availableOut);
            UIntPtr totalNint;
            BrotliDecoderResult returnVal = BrotliDecoderDecompressStream(decoderHandle, ref inNint, ref nextIn, ref outNint, ref nextOut, out totalNint);
            availableIn = inNint.ToUInt64();
            availableOut = outNint.ToUInt64();
            totalOut = totalNint.ToUInt64();
            return returnVal;
        }

        /// <inheritdoc />
        public bool DecoderIsFinished(SafeHandle decoder)
        {
            if (!(decoder is BrotliDecoderHandle decoderHandle))
            {
                throw new Exception("Brotli decoder handle is invalid");
            }

            return BrotliDecoderIsFinished(decoderHandle);
        }

        /// <inheritdoc />
        public string DecoderGetErrorString(SafeHandle decoder)
        {
            if (!(decoder is BrotliDecoderHandle decoderHandle))
            {
                throw new Exception("Brotli decoder handle is invalid");
            }

            int code = BrotliDecoderGetErrorCode(decoderHandle);
            if (code == 0)
            {
                return string.Empty;
            }

            IntPtr cstring = BrotliDecoderErrorString(code);
            if (cstring == IntPtr.Zero)
            {
                return string.Empty;
            }

            return Marshal.PtrToStringAnsi(cstring);
        }

        /// <summary>
        /// SafeHandle implementation for a Brotli encoder struct.
        /// </summary>
        internal class BrotliEncoderHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            /// <summary>
            /// Constructs a new SafeHandle - this constructor isn't invoked directly
            /// except by P/Invoke internals, so don't worry about setting IntPtr handles or anything.
            /// </summary>
            public BrotliEncoderHandle()
                : base(ownsHandle: true)
            {
            }

            /// <inheritdoc />
            //[ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
            protected override bool ReleaseHandle()
            {
                BrotliEncoderDestroyInstance(handle);
                return true;
            }
        }

        /// <summary>
        /// SafeHandle implementation for a Brotli decoder struct.
        /// </summary>
        internal class BrotliDecoderHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            /// <summary>
            /// Constructs a new SafeHandle - this constructor isn't invoked directly
            /// except by P/Invoke internals, so don't worry about setting IntPtr handles or anything.
            /// </summary>
            public BrotliDecoderHandle()
                : base(ownsHandle: true)
            {
            }

            /// <inheritdoc />
            //[ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
            protected override bool ReleaseHandle()
            {
                BrotliDecoderDestroyInstance(handle);
                return true;
            }
        }

        /// <summary>
        /// P/Invoke wrapper for BrotliEncoderCreateInstance.
        /// https://github.com/google/brotli/blob/ed1995b6bda19244070ab5d331111f16f67c8054/c/include/brotli/encode.h#L263
        /// </summary>
        /// <param name="allocFunc">Don't use.</param>
        /// <param name="freeFunc">Don't use this.</param>
        /// <param name="opaque">Don't use this either.</param>
        /// <returns>A SafeHandle for the encoder.</returns>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern BrotliEncoderHandle BrotliEncoderCreateInstance(IntPtr allocFunc, IntPtr freeFunc, IntPtr opaque);

        /// <summary>
        /// P/Invoke wrapper for BrotliEncoderSetParameter.
        /// https://github.com/google/brotli/blob/ed1995b6bda19244070ab5d331111f16f67c8054/c/include/brotli/encode.h#L246
        /// </summary>
        /// <param name="encoder">The encoder handle.</param>
        /// <param name="parameter">The parameter to set.</param>
        /// <param name="value">The value to pass for the parameter.</param>
        /// <returns>True if the parameter change was valid.</returns>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool BrotliEncoderSetParameter(BrotliEncoderHandle encoder, BrotliEncoderParameter parameter, uint value);

        /// <summary>
        /// P/Invoke wrapper for BrotliEncoderCompressStream.
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
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool BrotliEncoderCompressStream(
            BrotliEncoderHandle encoder,
            BrotliEncoderOperation op,
            ref UIntPtr availableIn,
            ref IntPtr nextIn,
            ref UIntPtr availableOut,
            ref IntPtr nextOut,
            out UIntPtr totalOut);

        /// <summary>
        /// P/Invoke wrapper for BrotliEncoderIsFinished.
        /// https://github.com/google/brotli/blob/ed1995b6bda19244070ab5d331111f16f67c8054/c/include/brotli/encode.h#L439
        /// </summary>
        /// <param name="encoder">The encode rhandle.</param>
        /// <returns>TRue if the encoder has reached its final state.</returns>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool BrotliEncoderIsFinished(BrotliEncoderHandle encoder);

        /// <summary>
        /// P/Invoke wrapper for BrotliEncoderDestroyInstance.
        /// </summary>
        /// <param name="encoder">The encoder handle.</param>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void BrotliEncoderDestroyInstance(IntPtr encoder);

        /// <summary>
        /// P/Invoke wrapper for BrotliEncoderVersion.
        /// </summary>
        /// <returns>The current library encoder version.</returns>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint BrotliEncoderVersion();

        /// <summary>
        /// P/Invoke wrapper for BrotliDecoderCreateInstance.
        /// </summary>
        /// <param name="allocFunc">Don't use.</param>
        /// <param name="freeFunc">Don't use this.</param>
        /// <param name="opaque">Don't use this either.</param>
        /// <returns>A new decoder handle.</returns>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern BrotliDecoderHandle BrotliDecoderCreateInstance(IntPtr allocFunc, IntPtr freeFunc, IntPtr opaque);

        /// <summary>
        /// P/Invoke wrapper for BrotliDecoderDecompressStream.
        /// </summary>
        /// <param name="decoder">The decoder handle.</param>
        /// <param name="availableIn">Bytes of input available</param>
        /// <param name="nextIn">Pointer to input data</param>
        /// <param name="availableOut">Bytes of output available.</param>
        /// <param name="nextOut">Pointer to output buffer.</param>
        /// <param name="totalOut">Total bytes decompressed.</param>
        /// <returns>A decoder result enum.</returns>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern BrotliDecoderResult BrotliDecoderDecompressStream(
            BrotliDecoderHandle decoder,
            ref UIntPtr availableIn,
            ref IntPtr nextIn,
            ref UIntPtr availableOut,
            ref IntPtr nextOut,
            out UIntPtr totalOut);

        /// <summary>
        /// P/Invoke wrapper for BrotliDecoderDestroyInstance.
        /// </summary>
        /// <param name="decoder">The decoder handle.</param>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void BrotliDecoderDestroyInstance(IntPtr decoder);

        /// <summary>
        /// P/Invoke wrapper for BrotliDecoderVersion.
        /// </summary>
        /// <returns>The library's decoder version.</returns>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint BrotliDecoderVersion();

        /// <summary>
        /// P/Invoke wrapper for BrotliDecoderIsFinished.
        /// </summary>
        /// <param name="decoder">The decoder handle.</param>
        /// <returns>True if the decoder has finished all input and produced all output.</returns>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool BrotliDecoderIsFinished(BrotliDecoderHandle decoder);

        /// <summary>
        /// P/Invoke wrapper for BrotliDecoderGetErrorCode.
        /// </summary>
        /// <param name="decoder">The decoder handle.</param>
        /// <returns>The most recent error code.</returns>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int BrotliDecoderGetErrorCode(BrotliDecoderHandle decoder);

        /// <summary>
        /// P/Invoke wrapper for BrotliDecoderErrorString.
        /// </summary>
        /// <param name="code">The error code to look up</param>
        /// <returns>A pointer to a static error code CString.</returns>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr BrotliDecoderErrorString(int code);
    }
}
