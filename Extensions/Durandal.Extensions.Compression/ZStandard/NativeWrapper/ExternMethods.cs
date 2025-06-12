﻿using Durandal.Common.Logger;
using Durandal.Common.Utils;
using Durandal.Common.Utils.NativePlatform;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using size_t = System.UIntPtr;

namespace Durandal.Extensions.Compression.ZStandard.NativeWrapper
{
    internal static class ExternMethods
    {
        internal const string LIBRARY_NAME = "zstd";

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZDICT_trainFromBuffer(byte[] dictBuffer, size_t dictBufferCapacity, byte[] samplesBuffer, size_t[] samplesSizes, uint nbSamples);
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint ZDICT_isError(size_t code);
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ZDICT_getErrorName(size_t code);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ZSTD_createCCtx();
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_freeCCtx(IntPtr cctx);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ZSTD_createDCtx();
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_freeDCtx(IntPtr cctx);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_compressCCtx(IntPtr ctx, IntPtr dst, size_t dstCapacity, IntPtr src, size_t srcSize, int compressionLevel);
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_compressCCtx(IntPtr ctx, ref byte dst, size_t dstCapacity, ref byte src, size_t srcSize, int compressionLevel);
        public static size_t ZSTD_compressCCtx(IntPtr ctx, Span<byte> dst, size_t dstCapacity, ReadOnlySpan<byte> src, size_t srcSize, int compressionLevel)
            => ZSTD_compressCCtx(ctx, ref MemoryMarshal.GetReference(dst), dstCapacity, ref MemoryMarshal.GetReference(src), srcSize, compressionLevel);
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_decompressDCtx(IntPtr ctx, IntPtr dst, size_t dstCapacity, IntPtr src, size_t srcSize);
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_decompressDCtx(IntPtr ctx, ref byte dst, size_t dstCapacity, ref byte src, size_t srcSize);
        public static size_t ZSTD_decompressDCtx(IntPtr ctx, Span<byte> dst, size_t dstCapacity, ReadOnlySpan<byte> src, size_t srcSize)
            => ZSTD_decompressDCtx(ctx, ref MemoryMarshal.GetReference(dst), dstCapacity, ref MemoryMarshal.GetReference(src), srcSize);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_compress2(IntPtr ctx, ref byte dst, size_t dstCapacity, ref byte src, size_t srcSize);
        public static size_t ZSTD_compress2(IntPtr ctx, Span<byte> dst, size_t dstCapacity, ReadOnlySpan<byte> src, size_t srcSize)
            => ZSTD_compress2(ctx, ref MemoryMarshal.GetReference(dst), dstCapacity, ref MemoryMarshal.GetReference(src), srcSize);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ZSTD_createCDict(byte[] dict, size_t dictSize, int compressionLevel);
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_freeCDict(IntPtr cdict);
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_compress_usingCDict(IntPtr cctx, IntPtr dst, size_t dstCapacity, IntPtr src, size_t srcSize, IntPtr cdict);
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_compress_usingCDict(IntPtr cctx, ref byte dst, size_t dstCapacity, ref byte src, size_t srcSize, IntPtr cdict);
        public static size_t ZSTD_compress_usingCDict(IntPtr cctx, Span<byte> dst, size_t dstCapacity, ReadOnlySpan<byte> src, size_t srcSize, IntPtr cdict)
            => ZSTD_compress_usingCDict(cctx, ref MemoryMarshal.GetReference(dst), dstCapacity, ref MemoryMarshal.GetReference(src), srcSize, cdict);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ZSTD_createDDict(byte[] dict, size_t dictSize);
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_freeDDict(IntPtr ddict);
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_decompress_usingDDict(IntPtr dctx, IntPtr dst, size_t dstCapacity, IntPtr src, size_t srcSize, IntPtr ddict);
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_decompress_usingDDict(IntPtr dctx, ref byte dst, size_t dstCapacity, ref byte src, size_t srcSize, IntPtr ddict);
        public static size_t ZSTD_decompress_usingDDict(IntPtr dctx, Span<byte> dst, size_t dstCapacity, ReadOnlySpan<byte> src, size_t srcSize, IntPtr ddict)
            => ZSTD_decompress_usingDDict(dctx, ref MemoryMarshal.GetReference(dst), dstCapacity, ref MemoryMarshal.GetReference(src), srcSize, ddict);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong ZSTD_getDecompressedSize(IntPtr src, size_t srcSize);
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong ZSTD_getFrameContentSize(IntPtr src, size_t srcSize);
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong ZSTD_getFrameContentSize(ref byte src, size_t srcSize);
        public static ulong ZSTD_getFrameContentSize(ReadOnlySpan<byte> src, size_t srcSize)
            => ZSTD_getFrameContentSize(ref MemoryMarshal.GetReference(src), srcSize);

        public const ulong ZSTD_CONTENTSIZE_UNKNOWN = unchecked(0UL - 1);
        public const ulong ZSTD_CONTENTSIZE_ERROR = unchecked(0UL - 2);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ZSTD_maxCLevel();
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ZSTD_minCLevel();
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_compressBound(size_t srcSize);
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint ZSTD_isError(size_t code);
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ZSTD_getErrorName(size_t code);

        #region Advanced APIs

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_CCtx_reset(IntPtr cctx, ZSTD_ResetDirective reset);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern ZSTD_bounds ZSTD_cParam_getBounds(ZSTD_cParameter cParam);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_CCtx_setParameter(IntPtr cctx, ZSTD_cParameter param, int value);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_DCtx_reset(IntPtr dctx, ZSTD_ResetDirective reset);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern ZSTD_bounds ZSTD_dParam_getBounds(ZSTD_dParameter dParam);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_DCtx_setParameter(IntPtr dctx, ZSTD_dParameter param, int value);


        [StructLayout(LayoutKind.Sequential)]
        internal struct ZSTD_bounds
        {
            public size_t error;
            public int lowerBound;
            public int upperBound;
        }

        public enum ZSTD_ResetDirective
        {
            ZSTD_reset_session_only = 1,
            ZSTD_reset_parameters = 2,
            ZSTD_reset_session_and_parameters = 3
        }

        #endregion

        #region Streaming APIs

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ZSTD_createCStream();
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_freeCStream(IntPtr zcs);
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_initCStream(IntPtr zcs, int compressionLevel);
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_compressStream(IntPtr zcs, ref ZSTD_Buffer output, ref ZSTD_Buffer input);
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_flushStream(IntPtr zcs, ref ZSTD_Buffer output);
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_endStream(IntPtr zcs, ref ZSTD_Buffer output);
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_CStreamInSize();
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_CStreamOutSize();
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ZSTD_createDStream();
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_freeDStream(IntPtr zds);
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_initDStream(IntPtr zds);
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_decompressStream(IntPtr zds, ref ZSTD_Buffer output, ref ZSTD_Buffer input);
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_DStreamInSize();
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_DStreamOutSize();

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_compressStream2(IntPtr zcs, ref ZSTD_Buffer output, ref ZSTD_Buffer input, ZSTD_EndDirective endOp);

        public enum ZSTD_EndDirective
        {
            ZSTD_e_continue = 0,
            ZSTD_e_flush = 1,
            ZSTD_e_end = 2
        }

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_initDStream_usingDDict(IntPtr zds, IntPtr dict);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_initCStream_usingCDict(IntPtr zds, IntPtr dict);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_CCtx_refCDict(IntPtr cctx, IntPtr cdict);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_DCtx_refDDict(IntPtr cctx, IntPtr cdict);

        [StructLayout(LayoutKind.Sequential)]
        internal struct ZSTD_Buffer
        {
            public ZSTD_Buffer(size_t pos, size_t size)
            {
                this.buffer = IntPtr.Zero;
                this.size = size;
                this.pos = pos;
            }

            public IntPtr buffer;
            public size_t size;
            public size_t pos;

            public bool IsFullyConsumed => (ulong)size <= (ulong)pos;
        }

        #endregion
    }

    public enum ZSTD_cParameter
    {
        // compression parameters
        ZSTD_c_compressionLevel = 100,
        ZSTD_c_windowLog = 101,
        ZSTD_c_hashLog = 102,
        ZSTD_c_chainLog = 103,
        ZSTD_c_searchLog = 104,
        ZSTD_c_minMatch = 105,
        ZSTD_c_targetLength = 106,
        ZSTD_c_strategy = 107,

        // long distance matching mode parameters
        ZSTD_c_enableLongDistanceMatching = 160,
        ZSTD_c_ldmHashLog = 161,
        ZSTD_c_ldmMinMatch = 162,
        ZSTD_c_ldmBucketSizeLog = 163,
        ZSTD_c_ldmHashRateLog = 164,

        // frame parameters
        ZSTD_c_contentSizeFlag = 200,
        ZSTD_c_checksumFlag = 201,
        ZSTD_c_dictIDFlag = 202,

        // multi-threading parameters
        ZSTD_c_nbWorkers = 400,
        ZSTD_c_jobSize = 401,
        ZSTD_c_overlapLog = 402
    }

    public enum ZSTD_dParameter
    {
        ZSTD_d_windowLogMax = 100
    }

    public enum ZSTD_ErrorCode
    {
        ZSTD_error_no_error = 0,
        ZSTD_error_GENERIC = 1,
        ZSTD_error_prefix_unknown = 10,
        ZSTD_error_version_unsupported = 12,
        ZSTD_error_frameParameter_unsupported = 14,
        ZSTD_error_frameParameter_windowTooLarge = 16,
        ZSTD_error_corruption_detected = 20,
        ZSTD_error_checksum_wrong = 22,
        ZSTD_error_dictionary_corrupted = 30,
        ZSTD_error_dictionary_wrong = 32,
        ZSTD_error_dictionaryCreation_failed = 34,
        ZSTD_error_parameter_unsupported = 40,
        ZSTD_error_parameter_outOfBound = 42,
        ZSTD_error_tableLog_tooLarge = 44,
        ZSTD_error_maxSymbolValue_tooLarge = 46,
        ZSTD_error_maxSymbolValue_tooSmall = 48,
        ZSTD_error_stage_wrong = 60,
        ZSTD_error_init_missing = 62,
        ZSTD_error_memory_allocation = 64,
        ZSTD_error_workSpace_tooSmall = 66,
        ZSTD_error_dstSize_tooSmall = 70,
        ZSTD_error_srcSize_wrong = 72,
        ZSTD_error_dstBuffer_null = 74
    }
}