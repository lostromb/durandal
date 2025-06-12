using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Durandal.Common.Utils.NativePlatform
{
    internal partial class KernelInteropWindows
    {
        // if we use this flag then we can improperly load libraries that don't match the current architecture, so avoid it
        internal const uint LOAD_LIBRARY_AS_DATAFILE = 0x00000002;

        internal const uint LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000;

        private const ushort PROCESSOR_ARCHITECTURE_INTEL = 0x0000;
        private const ushort PROCESSOR_ARCHITECTURE_AMD64 = 0x0009;
        private const ushort PROCESSOR_ARCHITECTURE_ARM = 0x0005;
        private const ushort PROCESSOR_ARCHITECTURE_ARM64 = 0x0012;
        private const ushort PROCESSOR_ARCHITECTURE_IA64 = 0x0006;
        private const ushort PROCESSOR_ARCHITECTURE_UNKNOWN = 0xFFFF;

        internal const uint HRESULT_MASK_KERNEL = 0x80070000;
        internal const uint HRESULT_KERNEL_FILE_NOT_FOUND = 0x8007007EU;
        internal const uint HRESULT_KERNEL_INVALID_BINARY_FORMAT = 0x800700C1U;

#if NET8_0_OR_GREATER
        [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        internal static partial IntPtr LoadLibraryExW(string lpFileName, IntPtr hFile, uint dwFlags);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool FreeLibrary(IntPtr hModule);

        [LibraryImport("kernel32.dll", SetLastError = false)]
        internal static partial uint GetLastError();
#else
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr LoadLibraryExW(string lpFileName, IntPtr hFile, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", SetLastError = false)]
        internal static extern uint GetLastError();
#endif

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_INFO
        {
            internal ushort wProcessorArchitecture;
            internal ushort wReserved;
            internal uint dwPageSize;
            internal IntPtr lpMinimumApplicationAddress;
            internal IntPtr lpMaximumApplicationAddress;
            internal IntPtr dwActiveProcessorMask;
            internal uint dwNumberOfProcessors;
            internal uint dwProcessorType;
            internal uint dwAllocationGranularity;
            internal ushort wProcessorLevel;
            internal ushort wProcessorRevision;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern void GetSystemInfo(ref SYSTEM_INFO Info);

        internal static PlatformArchitecture TryGetArchForWindows(ILogger logger)
        {
            logger.AssertNonNull(nameof(logger));

            try
            {
                SYSTEM_INFO info = default;
                GetSystemInfo(ref info);
                logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "Windows kernel processor code is 0x{0:X4}", info.wProcessorArchitecture);
                switch (info.wProcessorArchitecture)
                {
                    case PROCESSOR_ARCHITECTURE_INTEL:
                    case PROCESSOR_ARCHITECTURE_AMD64:
                        return BinaryHelpers.SizeOfIntPtr == 4 ? PlatformArchitecture.I386 : PlatformArchitecture.X64;
                    case PROCESSOR_ARCHITECTURE_ARM:
                        return PlatformArchitecture.ArmV7;
                    case PROCESSOR_ARCHITECTURE_ARM64:
                        return PlatformArchitecture.Arm64;
                    case PROCESSOR_ARCHITECTURE_IA64:
                        return PlatformArchitecture.Itanium64;
                    default:
                        return PlatformArchitecture.Unknown;
                }
            }
            catch (Exception e)
            {
                logger.Log(e);
                return PlatformArchitecture.Unknown;
            }
        }
    }
}
