using Durandal.Common.Logger;
using Durandal.Common.Utils;
using Durandal.Common.Utils.NativePlatform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ZstdNative = Durandal.Extensions.Compression.ZStandard.NativeWrapper;
using ZstdManaged = ZstdSharp;

namespace Durandal.Extensions.Compression.ZStandard
{
    public static class ZStandardCodec
    {
        private static int _initialized = 0;
        private static bool _nativeLibraryAvailable = false;

        public static Stream CreateCompressorStream(Stream innerStream, ILogger logger, int compressionLevel = 10, int bufferSize = 32768, bool isolateInnerStream = false)
        {
            bufferSize = bufferSize.AssertNonNegative(nameof(bufferSize));
            compressionLevel = Math.Max(Math.Min(compressionLevel, ZstdManaged.Compressor.MaxCompressionLevel), ZstdManaged.Compressor.MinCompressionLevel);

            InitializeLibrary(logger);
            if (_nativeLibraryAvailable)
            {
                ZstdNative.CompressionOptions compressOpts = new ZstdNative.CompressionOptions(compressionLevel);
                return new ZstdNative.CompressionStream(innerStream, compressOpts, bufferSize, isolateInnerStream);
            }
            else
            {
                return new ZstdManaged.CompressionStream(innerStream, compressionLevel, bufferSize, leaveOpen: isolateInnerStream);
            }
        }

        public static Stream CreateDecompressorStream(Stream innerStream, ILogger logger, int bufferSize = 32768, bool isolateInnerStream = false)
        {
            bufferSize = bufferSize.AssertNonNegative(nameof(bufferSize));
            InitializeLibrary(logger);
            if (_nativeLibraryAvailable)
            {
                ZstdNative.DecompressionOptions decompressOpts = new ZstdNative.DecompressionOptions();
                return new ZstdNative.DecompressionStream(innerStream, decompressOpts, bufferSize, isolateInnerStream);
            }
            else
            {
                return new ZstdManaged.DecompressionStream(innerStream, bufferSize, leaveOpen: isolateInnerStream);
            }
        }

        internal static void InitializeLibrary(ILogger logger)
        {
            if (!AtomicOperations.ExecuteOnce(ref _initialized))
            {
                return;
            }

            try
            {
                NativeLibraryStatus libStatus = NativePlatformUtils.PrepareNativeLibrary(ZstdNative.ExternMethods.LIBRARY_NAME, logger);
                _nativeLibraryAvailable = libStatus == NativeLibraryStatus.Available;
                logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "Is native zstd library available? {0}", _nativeLibraryAvailable);
            }
            catch (Exception e)
            {
                logger.Log(e);
                logger.Log("Falling back to managed implementation of Zstandard compression");
                _nativeLibraryAvailable = false;
            }
        }
    }
}
