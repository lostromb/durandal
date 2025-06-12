using Durandal.Common.Logger;
using Durandal.Common.Utils.NativePlatform;
using System;
using System.Threading;

namespace Durandal.Extensions.Compression.Brotli
{
    /// <summary>
    /// Static accessor for the current native Brotli library binding 
    /// (or potentially a C# implementation if there's ever a managed port).
    /// </summary>
    internal static class BrotliImplFactory
    {
        /// <summary>
        /// A static delegate interface for invoking Brotli native methods. The actual nature of the delegate
        /// will autoconfigure based on your platform and architecture.
        /// </summary>
        public static IBrolib Singleton => STATIC_LIBRARY.Value;

        private static readonly Lazy<IBrolib> STATIC_LIBRARY = new Lazy<IBrolib>(
            () =>
            {
                OSAndArchitecture currentRuntime = NativePlatformUtils.GetCurrentPlatform(DebugLogger.Default);
                NativeLibraryStatus libStatus = NativePlatformUtils.PrepareNativeLibrary("brotli", DebugLogger.Default);
                if (libStatus == NativeLibraryStatus.Unavailable)
                {
                    throw new PlatformNotSupportedException($"Brotli native library not yet supported for this platform {currentRuntime}");
                }

                return new BrolibPInvoke();
            },
            LazyThreadSafetyMode.PublicationOnly);
    }
}
