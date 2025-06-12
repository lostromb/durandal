using Durandal.API;
using Durandal.Common.Collections;
using Durandal.Common.Logger;
using Durandal.Common.Time;
using Durandal.Common.Utils.NativePlatform;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Durandal.Common.Utils.NativePlatform
{
    /// <summary>
    /// Global helpers for handling platform and OS-specific tasks regarding native P/Invoke libraries
    /// (mostly to make sure that the right one for the current platform actually gets invoked).
    /// The default resolver behavior will look for native libraries in the /runtimes/{PLATFORM}/native/ directory of the current program directory,
    /// though this could be overridden to look in linux shared system libraries, android .APKs, embedded resources, etc.
    /// The typical use pattern should be:
    /// <list type="number">
    /// <item>Near the entry point of your program, call <see cref="SetGlobalResolver"/> to install a more appropriate resolver for your platform.</item>
    /// <item>Before calling into a native library, call <see cref="PrepareNativeLibrary(string, ILogger)"/> once to ensure that library is present and available to load</item>
    /// <item>Begin using your P/Invoke methods as normal</item>
    /// </list>
    /// </summary>
    public static class NativePlatformUtils
    {
        // folder layout is based on https://learn.microsoft.com/en-us/nuget/create-packages/supporting-multiple-target-frameworks#architecture-specific-folders
        // runtimes
        //  |- linux-x64
        //     |- native
        //        |- libopus.so (amd64)
        //  |- osx-x64
        //     |- native
        //        |- libopus.dylib (amd64)
        //  |- win-x86
        //     |- native
        //        |- opus.dll (i386)
        //  |- win-x64
        //     |- native
        //        |- opus.dll (amd64)
        //  |- win10-arm
        //     |- native
        //        |- opus.dll (arm)

        private static INativeLibraryResolver _globalResolver = new BasicNativeLibraryResolver();
        private static Lazy<int> _cachedPerformanceScore = new Lazy<int>(CalculateSingleThreadPerformanceScore, System.Threading.LazyThreadSafetyMode.PublicationOnly);

        /// <summary>
        /// Sets a global handler for resolving runtime info and preloading native library files
        /// for the current architecture. Yes, this is very similar to NativeLibrary.SetDllImportResolver, but it has to be more portable than that
        /// to comply with .NetStandard 1.1. That, and this class has more flexibility to load libs from, say, an embedded resource or distributable
        /// package which you include in your application (such as an android .apk)
        /// Setting this value to null will cause the runtime to fallback to the default portable resolver which is limited
        /// to detecting the current platform and loading already-available libraries in situ.
        /// </summary>
        /// <param name="newResolver">The new global resolver to use.</param>
        public static void SetGlobalResolver(INativeLibraryResolver newResolver)
        {
            _globalResolver = newResolver ?? new BasicNativeLibraryResolver();
        }

        /// <summary>
        /// Gets information about the current runtime OS and processor, in parity with .Net's Runtime Identifier (RID) system.
        /// </summary>
        /// <returns>The current OS and architecture.</returns>
        public static OSAndArchitecture GetCurrentPlatform(ILogger logger)
        {
            return _globalResolver.GetCurrentPlatform(logger);
        }

        /// <summary>
        /// Given a native developer-provided library name, such as "mynativelib",
        /// search the current runtime directory + /runtimes/{runtime ID}/native for files like "mynativelib.dll" / "mynativelib.so",
        /// matching the given library name and current runtime OS and architecture, and then prepare that library file
        /// in such a way that future P/Invoke calls to that library should succeed and should invoke the correct
        /// architecture-specific code. The exact behavior of the library resolver may vary based on previous calls to
        /// <see cref="SetGlobalResolver"/>, if you desire to implement some other platform-specific logic.
        /// </summary>
        /// <param name="libraryName">The library name to prepare (without platform-specific extensions such as ".dll")</param>
        /// <param name="logger">A logger to log the result of the operation</param>
        /// <returns>Whether the runtime believes the given library is now available for loading or not.</returns>
        public static NativeLibraryStatus PrepareNativeLibrary(string libraryName, ILogger logger)
        {
            logger.AssertNonNull(nameof(logger));
            return _globalResolver.PrepareNativeLibrary(libraryName, logger);
        }

        /// <summary>
        /// Gets the approximate performance class of this machine's processor,
        /// based on the results of a microbenchmark. The first invocation of this
        /// function will actually run the benchmark, which will incur about 100ms of latency.
        /// </summary>
        /// <returns>The calculate performance class of this computer.</returns>
        public static PerformanceClass GetMachinePerformanceClass()
        {
            int perfScore = _cachedPerformanceScore.Value;

            // I know this is semi-arbitrary, but it's better than nothing. Future e-cores may mess up the benchmark too
            // - Ryzen 3700X = 1000
            // - i7 2800M = 597
            // - Core 2 duo = 128
            // - LG G6 (2018 android phone) = 104
            // - Raspberry Pi 3 = 34
            if (perfScore < 300)
            {
                return PerformanceClass.Low;
            }
            else if (perfScore < 600)
            {
                return PerformanceClass.Medium;
            }
            else
            {
                return PerformanceClass.High;
            }
        }

        private static int CalculateSingleThreadPerformanceScore()
        {
            RunMicroBenchmark(TimeSpan.FromMilliseconds(10)); // warmup / JIT
            int perfScore = RunMicroBenchmark(TimeSpan.FromMilliseconds(100));
            return perfScore;
        }

        private static int RunMicroBenchmark(TimeSpan timeToRun)
        {
            int iterationCount = 0;
            const int matrixSize = 40;
            ValueStopwatch stopwatch = ValueStopwatch.StartNew();
            do
            {
                int n = matrixSize & 0x7FFFFFFE;
                var a = GenerateMatrix(n, 1.0f);
                var b = GenerateMatrix(n, 2.0f);
                var x = MultiplyMatrix(ref a, ref b, n);
                float result = x[n / 2, n / 2];
                result.GetHashCode();
                iterationCount++;
            } while (stopwatch.Elapsed < timeToRun);

            return iterationCount;
        }

        /// <summary>
        /// Gets the runtime ID string for a given architecture, e.g. "arm64"
        /// </summary>
        /// <param name="architecture"></param>
        /// <returns></returns>
        /// <exception cref="PlatformNotSupportedException"></exception>
        internal static string GetRuntimeIdString(this PlatformArchitecture architecture)
        {
            switch (architecture)
            {
                case PlatformArchitecture.Unknown:
                    return "unknown";
                case PlatformArchitecture.Any:
                    return "any";
                case PlatformArchitecture.I386:
                    return "x86";
                case PlatformArchitecture.X64:
                    return "x64";
                case PlatformArchitecture.ArmV7:
                    return "arm";
                case PlatformArchitecture.Arm64:
                    return "arm64";
                case PlatformArchitecture.Armel:
                    return "armel";
                case PlatformArchitecture.ArmV6:
                    return "armv6";
                case PlatformArchitecture.Mips64:
                    return "mips64";
                case PlatformArchitecture.PowerPC64:
                    return "ppc64le";
                case PlatformArchitecture.RiscFive:
                    return "riscv64";
                case PlatformArchitecture.S390x:
                    return "s390x";
                case PlatformArchitecture.Loongarch64:
                    return "loongarch64";
                case PlatformArchitecture.Itanium64:
                    return "ia64";
                default:
                    throw new PlatformNotSupportedException("No runtime ID defined for " + architecture.ToString());
            }
        }

        /// <summary>
        /// Gets the runtime ID string for a given operating system, e.g. "osx"
        /// </summary>
        /// <param name="os"></param>
        /// <returns></returns>
        /// <exception cref="PlatformNotSupportedException"></exception>
        internal static string GetRuntimeIdString(this PlatformOperatingSystem os)
        {
            switch (os)
            {
                case PlatformOperatingSystem.Unknown:
                    return "unknown";
                case PlatformOperatingSystem.Any:
                    return "any";
                case PlatformOperatingSystem.Windows:
                    return "win";
                case PlatformOperatingSystem.Linux:
                    return "linux";
                case PlatformOperatingSystem.MacOS:
                    return "osx";
                case PlatformOperatingSystem.iOS:
                    return "ios";
                case PlatformOperatingSystem.iOS_Simulator:
                    return "iossimulator";
                case PlatformOperatingSystem.Android:
                    return "android";
                case PlatformOperatingSystem.FreeBSD:
                    return "freebsd";
                case PlatformOperatingSystem.Illumos:
                    return "illumos";
                case PlatformOperatingSystem.Linux_Bionic:
                    return "linux-bionic";
                case PlatformOperatingSystem.Linux_Musl:
                    return "linux-musl";
                case PlatformOperatingSystem.MacCatalyst:
                    return "maccatalyst";
                case PlatformOperatingSystem.Solaris:
                    return "solaris";
                case PlatformOperatingSystem.TvOS:
                    return "tvos";
                case PlatformOperatingSystem.TvOS_Simulator:
                    return "tvossimulator";
                case PlatformOperatingSystem.Unix:
                    return "unix";
                case PlatformOperatingSystem.Browser:
                    return "browser";
                case PlatformOperatingSystem.Wasi:
                    return "wasi";
                default:
                    throw new PlatformNotSupportedException("No runtime ID defined for " + os.ToString());
            }
        }

        internal static string[] GetInheritedRuntimeIds(string runtimeId)
        {
            string[] returnVal;
            if (RidInheritanceMappings.TryGetValue(runtimeId, out returnVal))
            {
                return returnVal;
            }

            return ArrayExtensions.EMPTY_STRING_ARRAY;
        }

        /// <summary>
        /// Parses the output from NetCore's RuntimeInformation.RuntimeIdentifier into an <see cref="OSAndArchitecture"/> struct.
        /// </summary>
        /// <param name="runtimeId">The runtime identifier.</param>
        /// <returns>A parsed identifier struct.</returns>
        internal static OSAndArchitecture ParseRuntimeId(ReadOnlySpan<char> runtimeId)
        {
            int splitIdx = runtimeId.IndexOf('-');

            if (splitIdx < 0)
            {
                // No processor architecture. Hmmmm
                return new OSAndArchitecture(TryParseOperatingSystemString(runtimeId), PlatformArchitecture.Unknown);
            }
            else
            {
                return new OSAndArchitecture(
                    TryParseOperatingSystemString(runtimeId.Slice(0, splitIdx)),
                    TryParseArchitectureString(runtimeId.Slice(splitIdx + 1)));
            }
        }

        /// <summary>
        /// Attempts to parse a runtime OS identifier string (e.g. "win10", "osx") into a structured
        /// operating system enum. Returns <see cref="PlatformOperatingSystem.Unknown"/> if parsing failed.
        /// </summary>
        /// <param name="os">The OS identifier string (should be lowercase but not strictly necessary)</param>
        /// <returns>A parsed OS enumeration</returns>
        internal static PlatformOperatingSystem TryParseOperatingSystemString(string os)
        {
            return TryParseOperatingSystemString(os.AsSpan());
        }

        /// <summary>
        /// Attempts to parse a runtime OS identifier string (e.g. "win10", "osx") into a structured
        /// operating system enum. Returns <see cref="PlatformOperatingSystem.Unknown"/> if parsing failed.
        /// </summary>
        /// <param name="os">The OS identifier string (should be lowercase but not strictly necessary)</param>
        /// <returns>A parsed OS enumeration</returns>
        internal static PlatformOperatingSystem TryParseOperatingSystemString(ReadOnlySpan<char> os)
        {
            if (os.Length >= "win".Length && os.Slice(0, "win".Length).Equals("win".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return PlatformOperatingSystem.Windows;
            }
            else if (os.Length >= "linux".Length && os.Slice(0, "linux".Length).Equals("linux".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return PlatformOperatingSystem.Linux;
            }
            else if (os.Length >= "ubuntu".Length && os.Slice(0, "ubuntu".Length).Equals("ubuntu".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return PlatformOperatingSystem.Linux;
            }
            else if (os.Length >= "debian".Length && os.Slice(0, "debian".Length).Equals("debian".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return PlatformOperatingSystem.Linux;
            }
            else if (os.Length >= "osx".Length && os.Slice(0, "osx".Length).Equals("osx".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return PlatformOperatingSystem.MacOS;
            }
            else if (os.Length >= "ios".Length && os.Slice(0, "ios".Length).Equals("ios".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return PlatformOperatingSystem.iOS;
            }
            else if (os.Length >= "iossimulator".Length && os.Slice(0, "iossimulator".Length).Equals("iossimulator".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return PlatformOperatingSystem.iOS_Simulator;
            }
            else if (os.Length >= "android".Length && os.Slice(0, "android".Length).Equals("android".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return PlatformOperatingSystem.Android;
            }
            else if (os.Length >= "freebsd".Length && os.Slice(0, "freebsd".Length).Equals("freebsd".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return PlatformOperatingSystem.FreeBSD;
            }
            else if (os.Length >= "illumos".Length && os.Slice(0, "illumos".Length).Equals("illumos".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return PlatformOperatingSystem.Illumos;
            }
            else if (os.Length >= "linux-bionic".Length && os.Slice(0, "linux-bionic".Length).Equals("linux-bionic".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return PlatformOperatingSystem.Linux_Bionic;
            }
            else if (os.Length >= "linux-musl".Length && os.Slice(0, "linux-musl".Length).Equals("linux-musl".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return PlatformOperatingSystem.Linux_Musl;
            }
            else if (os.Length >= "maccatalyst".Length && os.Slice(0, "maccatalyst".Length).Equals("maccatalyst".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return PlatformOperatingSystem.MacCatalyst;
            }
            else if (os.Length >= "solaris".Length && os.Slice(0, "solaris".Length).Equals("solaris".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return PlatformOperatingSystem.Solaris;
            }
            else if (os.Length >= "tvos".Length && os.Slice(0, "tvos".Length).Equals("tvos".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return PlatformOperatingSystem.TvOS;
            }
            else if (os.Length >= "tvossimulator".Length && os.Slice(0, "tvossimulator".Length).Equals("tvossimulator".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return PlatformOperatingSystem.TvOS_Simulator;
            }
            else if (os.Length >= "unix".Length && os.Slice(0, "unix".Length).Equals("unix".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return PlatformOperatingSystem.Unix;
            }
            else if (os.Length >= "browser".Length && os.Slice(0, "browser".Length).Equals("browser".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return PlatformOperatingSystem.Browser;
            }
            else if (os.Length >= "wasi".Length && os.Slice(0, "wasi".Length).Equals("wasi".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return PlatformOperatingSystem.Wasi;
            }
            else
            {
                return PlatformOperatingSystem.Unknown;
            }
        }

        /// <summary>
        /// Attempts to parse a runtime platform architecture string (e.g. "x64", "arm") into a structured
        /// architecture enum. Returns <see cref="PlatformArchitecture.Unknown"/> if parsing failed.
        /// </summary>
        /// <param name="arch">The architecture identifier string (should be lowercase but not strictly necessary)</param>
        /// <returns>A parsed architecture enumeration</returns>
        internal static PlatformArchitecture TryParseArchitectureString(string arch)
        {
            return TryParseArchitectureString(arch.AsSpan());
        }

        /// <summary>
        /// Attempts to parse a runtime platform architecture string (e.g. "x64", "arm") into a structured
        /// architecture enum. Returns <see cref="PlatformArchitecture.Unknown"/> if parsing failed.
        /// </summary>
        /// <param name="arch">The architecture identifier string (should be lowercase but not strictly necessary)</param>
        /// <returns>A parsed architecture enumeration</returns>
        internal static PlatformArchitecture TryParseArchitectureString(ReadOnlySpan<char> arch)
        {
            if (arch.Equals("any".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return PlatformArchitecture.Any;
            }
            else if (arch.Equals("x86".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return PlatformArchitecture.I386;
            }
            else if (arch.Equals("x64".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return PlatformArchitecture.X64;
            }
            else if (arch.Equals("arm".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return PlatformArchitecture.ArmV7;
            }
            else if (arch.Equals("arm64".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return PlatformArchitecture.Arm64;
            }
            else if (arch.Equals("armel".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return PlatformArchitecture.Armel;
            }
            else if (arch.Equals("armv6".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return PlatformArchitecture.ArmV6;
            }
            else if (arch.Equals("mips64".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return PlatformArchitecture.Mips64;
            }
            else if (arch.Equals("ppc64le".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return PlatformArchitecture.PowerPC64;
            }
            else if (arch.Equals("riscv64".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return PlatformArchitecture.RiscFive;
            }
            else if (arch.Equals("s390x".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return PlatformArchitecture.S390x;
            }
            else if (arch.Equals("loongarch64".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return PlatformArchitecture.Loongarch64;
            }
            else if (arch.Equals("ia64".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return PlatformArchitecture.Itanium64;
            }
            else
            {
                return PlatformArchitecture.Unknown;
            }
        }

        internal static PlatformArchitecture TryGetNativeArchitecture(PlatformOperatingSystem os, ILogger logger)
        {
            if (os == PlatformOperatingSystem.Windows)
            {
                return KernelInteropWindows.TryGetArchForWindows(logger);
            }
            else if (os == PlatformOperatingSystem.Linux ||
                    os == PlatformOperatingSystem.Unix ||
                    os == PlatformOperatingSystem.Linux_Musl ||
                    os == PlatformOperatingSystem.Linux_Bionic ||
                    os == PlatformOperatingSystem.Android)
            {
                return KernelInteropLinux.TryGetArchForUnix(logger);
            }

            return PlatformArchitecture.Unknown;
        }

        /// <summary>
        /// Attempts to load the given library using kernel hooks for the current runtime operating system.
        /// </summary>
        /// <param name="libName">The name of the library to open, e.g. "libc"</param>
        /// <param name="platformInfo">The currently running platform</param>
        /// <param name="logger">A logger</param>
        /// <returns>The availability of the given library after the probe attempt (it may load a locally provided or system-installed version of the requested library).</returns>
        internal static NativeLibraryStatus ProbeLibrary(
            string libName,
            OSAndArchitecture platformInfo,
            ILogger logger)
        {
            libName.AssertNonNullOrEmpty(nameof(libName));
            logger.AssertNonNull(nameof(logger));

            try
            {
                if (platformInfo.OS == PlatformOperatingSystem.Windows)
                {
                    IntPtr dllHandle = IntPtr.Zero;
                    try
                    {
                        logger.Log($"Attempting to load {libName} as a windows .dll", LogLevel.Vrb);
                        KernelInteropWindows.GetLastError(); // clear any previous error
                        dllHandle = KernelInteropWindows.LoadLibraryExW(libName, hFile: IntPtr.Zero, dwFlags: KernelInteropWindows.LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
                        if (dllHandle == IntPtr.Zero)
                        {
                            uint lastError = KernelInteropWindows.GetLastError();
                            if (lastError != 0)
                            {
                                uint hresult = KernelInteropWindows.HRESULT_MASK_KERNEL | lastError;
                                if (hresult == KernelInteropWindows.HRESULT_KERNEL_FILE_NOT_FOUND)
                                {
                                    logger.Log(string.Format("Win32 error 0x{0:X8}: File {1} not found", hresult, libName), LogLevel.Vrb);
                                }
                                else if (hresult == KernelInteropWindows.HRESULT_KERNEL_INVALID_BINARY_FORMAT)
                                {
                                    logger.Log(string.Format("Win32 error 0x{0:X8}: Invalid binary format while loading library {1}", hresult, libName), LogLevel.Wrn);
                                }
                                else
                                {
                                    logger.Log(string.Format("Win32 error 0x{0:X8} while loading library {1}", hresult, libName), LogLevel.Wrn);
                                }
                            }
                            else
                            {
                                logger.Log($"{libName} not found.", LogLevel.Vrb);
                            }

                            return NativeLibraryStatus.Unavailable;
                        }
                        else
                        {
                            logger.Log($"{libName} found", LogLevel.Vrb);
                            return NativeLibraryStatus.Available;
                        }
                    }
                    finally
                    {
                        if (dllHandle != IntPtr.Zero)
                        {
                            KernelInteropWindows.FreeLibrary(dllHandle);
                        }
                    }
                }
                else if (platformInfo.OS == PlatformOperatingSystem.Linux ||
                    platformInfo.OS == PlatformOperatingSystem.Android ||
                    platformInfo.OS == PlatformOperatingSystem.Linux_Bionic ||
                    platformInfo.OS == PlatformOperatingSystem.Linux_Musl ||
                    platformInfo.OS == PlatformOperatingSystem.Unix)
                {
                    IntPtr soHandle = IntPtr.Zero;
                    try
                    {
                        logger.Log($"Attempting to load {libName} as a linux .so", LogLevel.Vrb);
                        soHandle = KernelInteropLinux.dlopen(libName, KernelInteropLinux.RTLD_NOW);
                        if (soHandle == IntPtr.Zero)
                        {
                            IntPtr lastError = KernelInteropLinux.dlerror();
                            if (lastError != IntPtr.Zero)
                            {
                                string dlErrorMsg = Marshal.PtrToStringAnsi(lastError);
                                if (!string.IsNullOrEmpty(dlErrorMsg))
                                {
                                    logger.Log(string.Format("Error while loading library {0}: {1}", libName, dlErrorMsg), LogLevel.Wrn);
                                }
                                else
                                {
                                    logger.Log($"{libName} could not be loaded.", LogLevel.Vrb);
                                }
                            }
                            else
                            {
                                logger.Log($"{libName} could not be loaded.", LogLevel.Vrb);
                            }

                            return NativeLibraryStatus.Unavailable;
                        }
                        else
                        {
                            logger.Log($"{libName} found!", LogLevel.Vrb);
                            return NativeLibraryStatus.Available;
                        }
                    }
                    finally
                    {
                        if (soHandle != IntPtr.Zero)
                        {
                            KernelInteropLinux.dlclose(soHandle);
                        }
                    }
                }
                else if (platformInfo.OS == PlatformOperatingSystem.MacOS ||
                    platformInfo.OS == PlatformOperatingSystem.MacCatalyst ||
                    platformInfo.OS == PlatformOperatingSystem.iOS ||
                    platformInfo.OS == PlatformOperatingSystem.iOS_Simulator)
                {
                    IntPtr dylibHandle = IntPtr.Zero;
                    try
                    {
                        logger.Log($"Attempting to load {libName} as a macOS .dylib", LogLevel.Vrb);
                        dylibHandle = KernelInteropMacOS.dlopen(libName, KernelInteropMacOS.RTLD_NOW);
                        if (dylibHandle == IntPtr.Zero)
                        {
                            IntPtr lastError = KernelInteropMacOS.dlerror();
                            if (lastError != IntPtr.Zero)
                            {
                                string dlErrorMsg = Marshal.PtrToStringAnsi(lastError);
                                if (!string.IsNullOrEmpty(dlErrorMsg))
                                {
                                    logger.Log(string.Format("Error while loading library {0}: {1}", libName, dlErrorMsg), LogLevel.Wrn);
                                }
                                else
                                {
                                    logger.Log($"{libName} could not be loaded.", LogLevel.Vrb);
                                }
                            }
                            else
                            {
                                logger.Log($"{libName} could not be loaded.", LogLevel.Vrb);
                            }

                            return NativeLibraryStatus.Unavailable;
                        }
                        else
                        {
                            logger.Log($"{libName} found!", LogLevel.Vrb);
                            return NativeLibraryStatus.Available;
                        }
                    }
                    finally
                    {
                        if (dylibHandle != IntPtr.Zero)
                        {
                            KernelInteropMacOS.dlclose(dylibHandle);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                logger.Log(e);
            }

            return NativeLibraryStatus.Unknown;
        }

        #region Big tables
        // Used for full inheritance lookups
        // Based on the runtime ID inheritance document which is now deprecated anyways.
        private static readonly IReadOnlyDictionary<string, string[]> RidInheritanceMappings =
            new Dictionary<string, string[]>()
            {
                { "android", new string[] { "linux-bionic", "linux", "unix", "any" } },
                { "android-arm", new string[] { "android", "linux-bionic-arm", "linux-bionic", "linux-arm", "linux", "unix-arm", "unix", "any" } },
                { "android-arm64", new string[] { "android", "linux-bionic-arm64", "linux-bionic", "linux-arm64", "linux", "unix-arm64", "unix", "any" } },
                { "android-x64", new string[] { "android", "linux-bionic-x64", "linux-bionic", "linux-x64", "linux", "unix-x64", "unix", "any" } },
                { "android-x86", new string[] { "android", "linux-bionic-x86", "linux-bionic", "linux-x86", "linux", "unix-x86", "unix", "any" } },
                { "any", new string[] { } },
                { "base", new string[] { } },
                { "browser", new string[] { "any" } },
                { "browser-wasm", new string[] { "browser", "any" } },
                { "freebsd", new string[] { "unix", "any" } },
                { "freebsd-arm64", new string[] { "freebsd", "unix-arm64", "unix", "any" } },
                { "freebsd-x64", new string[] { "freebsd", "unix-x64", "unix", "any" } },
                { "illumos", new string[] { "unix", "any" } },
                { "illumos-x64", new string[] { "illumos", "unix-x64", "unix", "any" } },
                { "ios", new string[] { "unix", "any" } },
                { "ios-arm", new string[] { "ios", "unix-arm", "unix", "any" } },
                { "ios-arm64", new string[] { "ios", "unix-arm64", "unix", "any" } },
                { "iossimulator", new string[] { "ios", "unix", "any" } },
                { "iossimulator-arm64", new string[] { "iossimulator", "ios-arm64", "ios", "unix-arm64", "unix", "any" } },
                { "iossimulator-x64", new string[] { "iossimulator", "ios-x64", "ios", "unix-x64", "unix", "any" } },
                { "iossimulator-x86", new string[] { "iossimulator", "ios-x86", "ios", "unix-x86", "unix", "any" } },
                { "ios-x64", new string[] { "ios", "unix-x64", "unix", "any" } },
                { "ios-x86", new string[] { "ios", "unix-x86", "unix", "any" } },
                { "linux", new string[] { "unix", "any" } },
                { "linux-arm", new string[] { "linux", "unix-arm", "unix", "any" } },
                { "linux-arm64", new string[] { "linux", "unix-arm64", "unix", "any" } },
                { "linux-armel", new string[] { "linux", "unix-armel", "unix", "any" } },
                { "linux-armv6", new string[] { "linux", "unix-armv6", "unix", "any" } },
                { "linux-bionic", new string[] { "linux", "unix", "any" } },
                { "linux-bionic-arm", new string[] { "linux-bionic", "linux-arm", "linux", "unix-arm", "unix", "any" } },
                { "linux-bionic-arm64", new string[] { "linux-bionic", "linux-arm64", "linux", "unix-arm64", "unix", "any" } },
                { "linux-bionic-x64", new string[] { "linux-bionic", "linux-x64", "linux", "unix-x64", "unix", "any" } },
                { "linux-bionic-x86", new string[] { "linux-bionic", "linux-x86", "linux", "unix-x86", "unix", "any" } },
                { "linux-loongarch64", new string[] { "linux", "unix-loongarch64", "unix", "any" } },
                { "linux-mips64", new string[] { "linux", "unix-mips64", "unix", "any" } },
                { "linux-musl", new string[] { "linux", "unix", "any" } },
                { "linux-musl-arm", new string[] { "linux-musl", "linux-arm", "linux", "unix-arm", "unix", "any" } },
                { "linux-musl-arm64", new string[] { "linux-musl", "linux-arm64", "linux", "unix-arm64", "unix", "any" } },
                { "linux-musl-armel", new string[] { "linux-musl", "linux-armel", "linux", "unix-armel", "unix", "any" } },
                { "linux-musl-armv6", new string[] { "linux-musl", "linux-armv6", "linux", "unix-armv6", "unix", "any" } },
                { "linux-musl-ppc64le", new string[] { "linux-musl", "linux-ppc64le", "linux", "unix-ppc64le", "unix", "any" } },
                { "linux-musl-riscv64", new string[] { "linux-musl", "linux-riscv64", "linux", "unix-riscv64", "unix", "any" } },
                { "linux-musl-s390x", new string[] { "linux-musl", "linux-s390x", "linux", "unix-s390x", "unix", "any" } },
                { "linux-musl-x64", new string[] { "linux-musl", "linux-x64", "linux", "unix-x64", "unix", "any" } },
                { "linux-musl-x86", new string[] { "linux-musl", "linux-x86", "linux", "unix-x86", "unix", "any" } },
                { "linux-ppc64le", new string[] { "linux", "unix-ppc64le", "unix", "any" } },
                { "linux-riscv64", new string[] { "linux", "unix-riscv64", "unix", "any" } },
                { "linux-s390x", new string[] { "linux", "unix-s390x", "unix", "any" } },
                { "linux-x64", new string[] { "linux", "unix-x64", "unix", "any" } },
                { "linux-x86", new string[] { "linux", "unix-x86", "unix", "any" } },
                { "maccatalyst", new string[] { "ios", "unix", "any" } },
                { "maccatalyst-arm64", new string[] { "maccatalyst", "ios-arm64", "ios", "unix-arm64", "unix", "any" } },
                { "maccatalyst-x64", new string[] { "maccatalyst", "ios-x64", "ios", "unix-x64", "unix", "any" } },
                { "osx", new string[] { "unix", "any" } },
                { "osx-arm64", new string[] { "osx", "unix-arm64", "unix", "any" } },
                { "osx-x64", new string[] { "osx", "unix-x64", "unix", "any" } },
                { "solaris", new string[] { "unix", "any" } },
                { "solaris-x64", new string[] { "solaris", "unix-x64", "unix", "any" } },
                { "tvos", new string[] { "unix", "any" } },
                { "tvos-arm64", new string[] { "tvos", "unix-arm64", "unix", "any" } },
                { "tvossimulator", new string[] { "tvos", "unix", "any" } },
                { "tvossimulator-arm64", new string[] { "tvossimulator", "tvos-arm64", "tvos", "unix-arm64", "unix", "any" } },
                { "tvossimulator-x64", new string[] { "tvossimulator", "tvos-x64", "tvos", "unix-x64", "unix", "any" } },
                { "tvos-x64", new string[] { "tvos", "unix-x64", "unix", "any" } },
                { "unix", new string[] { "any" } },
                { "unix-arm", new string[] { "unix", "any" } },
                { "unix-arm64", new string[] { "unix", "any" } },
                { "unix-armel", new string[] { "unix", "any" } },
                { "unix-armv6", new string[] { "unix", "any" } },
                { "unix-loongarch64", new string[] { "unix", "any" } },
                { "unix-mips64", new string[] { "unix", "any" } },
                { "unix-ppc64le", new string[] { "unix", "any" } },
                { "unix-riscv64", new string[] { "unix", "any" } },
                { "unix-s390x", new string[] { "unix", "any" } },
                { "unix-x64", new string[] { "unix", "any" } },
                { "unix-x86", new string[] { "unix", "any" } },
                { "wasi", new string[] { "any" } },
                { "wasi-wasm", new string[] { "wasi", "any" } },
                { "win", new string[] { "any" } },
                { "win-arm", new string[] { "win", "any" } },
                { "win-arm64", new string[] { "win", "any" } },
                { "win-x64", new string[] { "win", "any" } },
                { "win-x86", new string[] { "win", "any" } },
            };
        #endregion

        private static float[,] GenerateMatrix(int n, float seed)
        {
            var tmp = seed / n / n;
            var a = new float[n, n];
            for (var i = 0; i < n; ++i)
                for (var j = 0; j < n; ++j)
                    a[i, j] = tmp * (i - j) * (i + j);
            return a;
        }

        private static float[,] MultiplyMatrix(ref float[,] a, ref float[,] b, int n)
        {
            var x = new float[n, n];
            var c = new float[n, n];

            for (var i = 0; i < n; ++i)
                for (var j = 0; j < n; ++j)
                    c[j, i] = b[i, j];

            for (var i = 0; i < n; ++i)
                for (var j = 0; j < n; ++j)
                {
                    var s = 0.0f;
                    for (var k = 0; k < n; ++k)
                        s += a[i, k] * c[j, k];
                    x[i, j] = s;
                }

            return x;
        }
    }
}
