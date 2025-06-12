using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Durandal.Common.Utils.NativePlatform
{
    internal class BasicNativeLibraryResolver : INativeLibraryResolver
    {
        public OSAndArchitecture GetCurrentPlatform(ILogger logger)
        {
            PlatformOperatingSystem os = PlatformOperatingSystem.Unknown;
            PlatformArchitecture arch = PlatformArchitecture.Unknown;
#if NET6_0_OR_GREATER
            OSAndArchitecture fromRid = NativePlatformUtils.ParseRuntimeId(RuntimeInformation.RuntimeIdentifier);
            logger.Log($"Parsed runtime ID \"{RuntimeInformation.RuntimeIdentifier}\" as {fromRid}", LogLevel.Vrb);
            os = fromRid.OS;
            arch = fromRid.Architecture;
#endif

            // We can sometimes fail to parse new runtime IDs (like if they add "debian" as a runtime ID in the future), so fall back if needed
#if NETCOREAPP || NETSTANDARD1_1_OR_GREATER
            if (os == PlatformOperatingSystem.Unknown)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    os = PlatformOperatingSystem.Windows;
                }
#if !NETSTANDARD1_1
                else if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANDROID_STORAGE")))
                {
                    os = PlatformOperatingSystem.Android;
                }
#endif // !NETSTANDARD1_1
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    os = PlatformOperatingSystem.MacOS;
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    os = PlatformOperatingSystem.Linux;
                }
#if NET6_0_OR_GREATER
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
                {
                    os = PlatformOperatingSystem.FreeBSD;
                }
#endif // NET6_0_OR_GREATER
            }
#endif // NETCOREAPP || NETSTANDARD1_1_OR_GREATER

#if PCL
            // On PCL we have only very rudimentary checks. P/invoke is basically our only option here.
            // Use p/invoke to common system functions and use exceptions to determine what is available.
            // Even this seems risky with the possibility of causing segfaults.
            if (os == PlatformOperatingSystem.Unknown) try { KernelInteropWindows.GetLastError(); os = PlatformOperatingSystem.Windows; } catch (Exception) { }
            if (os == PlatformOperatingSystem.Unknown) try { KernelInteropLinux.dlerror(); os = PlatformOperatingSystem.Linux; } catch (Exception) { }
            if (os == PlatformOperatingSystem.Unknown) try { KernelInteropLinux2.dlerror(); os = PlatformOperatingSystem.Linux; } catch (Exception) { }
            if (os == PlatformOperatingSystem.Unknown) try { KernelInteropMacOS.dlerror(); os = PlatformOperatingSystem.MacOS; } catch (Exception) { }
#endif

            // Then just fall back to the .net runtime values
            if (arch == PlatformArchitecture.Unknown)
            {
#if NET452_OR_GREATER
                arch = Environment.Is64BitProcess ? PlatformArchitecture.X64 : PlatformArchitecture.I386;
#else
                switch (RuntimeInformation.ProcessArchitecture)
                {
                    case Architecture.X86:
                        arch = PlatformArchitecture.I386;
                        break;
                    case Architecture.X64:
                        arch = PlatformArchitecture.X64;
                        break;
                    case Architecture.Arm:
                        arch = PlatformArchitecture.ArmV7;
                        break;
                    case Architecture.Arm64:
                        arch = PlatformArchitecture.Arm64;
                        break;
#if NET6_0_OR_GREATER
                    case Architecture.LoongArch64:
                        arch = PlatformArchitecture.Loongarch64;
                        break;
                    case Architecture.Ppc64le:
                        arch = PlatformArchitecture.PowerPC64;
                        break;
                    case Architecture.Armv6:
                        arch = PlatformArchitecture.ArmV6;
                        break;
                    case Architecture.S390x:
                        arch = PlatformArchitecture.S390x;
                        break;
#endif // NET6_0_OR_GREATER
                }
#endif // !NET452_OR_GREATER
            }

            return new OSAndArchitecture(os, arch);
        }

        public NativeLibraryStatus PrepareNativeLibrary(string libraryName, ILogger logger)
        {
            logger.Log("Native libraries may not be reliably loaded now because the Durandal native runtime is in portable mode. " +
                $"You can fix this by setting a more platform-appropriate resolver near your application's entry point using {nameof(NativePlatformUtils)}.{nameof(NativePlatformUtils.SetGlobalResolver)}()",
                LogLevel.Wrn);

            return NativePlatformUtils.ProbeLibrary(libraryName, GetCurrentPlatform(logger), logger);
        }

        private static class KernelInteropWindows
        {
            [DllImport("kernel32.dll", SetLastError = false)]
            internal static extern uint GetLastError();
        }

        private static class KernelInteropLinux
        {
            [DllImport("libdl.so")]
            internal static extern IntPtr dlerror();
        }

        private static class KernelInteropLinux2
        {
            [DllImport("libdl.so.2")]
            internal static extern IntPtr dlerror();
        }

        private static class KernelInteropMacOS
        {
            [DllImport("libSystem.dylib")]
            internal static extern IntPtr dlerror();
        }
    }
}
