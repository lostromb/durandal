using Durandal.API;
using Durandal.Common.Collections;
using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;

namespace Durandal.Common.Utils.NativePlatform
{
    /// <summary>
    /// Default implementation of INativeLibraryResolver which works for most platforms.
    /// Looks for library files for the current platform in
    /// /{currentDir}/runtimes/{runtime}/native, and (usually) copies the matching library into
    /// {currentDir} so it will be picked up by the platform's library resolver.
    /// </summary>
    public class NativeLibraryResolverImpl : INativeLibraryResolver
    {
        private static bool _cachedPlatformInfoExists = false;
        private static OSAndArchitecture _cachedPlatformInfo = new OSAndArchitecture(PlatformOperatingSystem.Unknown, PlatformArchitecture.Unknown);

        private readonly IDictionary<string, NativeLibraryStatus> _loadedLibraries = new Dictionary<string, NativeLibraryStatus>();
        private readonly object _mutex = new object();
        private readonly DirectoryInfo _workingDir;

        /// <summary>
        /// Constructs a new <see cref="NativeLibraryResolverImpl"/> with a specified working directory.
        /// </summary>
        /// <param name="workingDirectory">The working directory of the program. If null, <see cref="Environment.CurrentDirectory"/> will be used.</param>
        public NativeLibraryResolverImpl(DirectoryInfo workingDirectory = null)
        {
            _workingDir = workingDirectory ?? new DirectoryInfo(Environment.CurrentDirectory);
        }

        /// <inheritdoc />
        public OSAndArchitecture GetCurrentPlatform(ILogger logger)
        {
            // can't use Lazy<T> for this because the producer function uses parameters
            lock (_mutex)
            {
                if (!_cachedPlatformInfoExists)
                {
                    _cachedPlatformInfo = GetCurrentPlatformInternal(logger);
                }

                return _cachedPlatformInfo;
            }
        }

        /// <inheritdoc />
        public NativeLibraryStatus PrepareNativeLibrary(string libraryName, ILogger logger)
        {
            logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "Preparing native library \"{0}\"", libraryName);

            OSAndArchitecture platform = GetCurrentPlatform(logger);
            string normalizedLibraryName = NormalizeLibraryName(libraryName, platform);
            lock (_mutex)
            {
                NativeLibraryStatus prevStatus;
                if (_loadedLibraries.TryGetValue(normalizedLibraryName, out prevStatus))
                {
                    logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "Native library \"{0}\" has already been prepared; nothing to do", libraryName);
                    return prevStatus;
                }

                if (platform.OS == PlatformOperatingSystem.Android)
                {
                    // On android we're not allowed to dlopen shared system binaries directly.
                    // So we have to probe and see if there's a native .so provided to us by this application's .apk
                    logger.Log($"Probing for {normalizedLibraryName} within local Android .apk", LogLevel.Vrb);
                    NativeLibraryStatus androidApkLibStatus = NativePlatformUtils.ProbeLibrary(normalizedLibraryName, platform, logger);
                    if (androidApkLibStatus != NativeLibraryStatus.Available)
                    {
                        logger.LogFormat(LogLevel.Wrn, DataPrivacyClassification.SystemMetadata, "Native library \"{0}\" was not found in the local .apk", libraryName);
                        return NativeLibraryStatus.Unavailable;
                    }

                    return NativeLibraryStatus.Available;
                }

                // See if the library is actually provided by the system already
                logger.Log($"Probing for an already existing {normalizedLibraryName}", LogLevel.Vrb);
                NativeLibraryStatus builtInLibStatus = NativePlatformUtils.ProbeLibrary(normalizedLibraryName, platform, logger);
                if (builtInLibStatus == NativeLibraryStatus.Available)
                {
                    logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "Native library \"{0}\" resolved to an already-existing library. Loading current file as-is.", libraryName);
                    _loadedLibraries[normalizedLibraryName] = builtInLibStatus;
                    return builtInLibStatus;
                }

                // If the dll was not found or couldn't be loaded because it was the wrong format or something, start trying to pull
                // the matching library from our current /runtimes/{PLATFORM} directory tree

                // Clean up any loose local binaries first
                DeleteLocalLibraryIfPresent(normalizedLibraryName, logger);

                // Search the most applicable /runtimes source directory for a matching library file
                string baseDirectory = Path.Combine(_workingDir.FullName, "runtimes");
                List<string> possibleLibraryNames = PermuteLibraryNames(libraryName, platform);
                List<string> possibleDirectoryNames = PermuteArchitectureSpecificDirectoryNames(platform);
                foreach (string possibleDirectory in possibleDirectoryNames)
                {
                    DirectoryInfo probeDir = new DirectoryInfo(Path.Combine(baseDirectory, possibleDirectory, "native"));
                    if (!probeDir.Exists)
                    {
                        continue;
                    }

                    foreach (string possibleSourceLibrary in possibleLibraryNames)
                    {
                        FileInfo sourceLibraryFile = new FileInfo(Path.Combine(probeDir.FullName, possibleSourceLibrary));
                        if (!sourceLibraryFile.Exists)
                        {
                            continue;
                        }

                        // Do platform-specific work to make this library discoverable by the platform's default library lookup
                        // Apparently in legacy .NetFx (and Mono), Linux .so libraries would not be picked up from the current
                        // executable directory. This seems to have changed in .Net core so that .so files are discovered
                        // the same way as .dlls. "lib" is also prepended to Linux lib search paths automatically.
                        if (platform.OS == PlatformOperatingSystem.Windows ||
                            platform.OS == PlatformOperatingSystem.Linux ||
                            platform.OS == PlatformOperatingSystem.MacOS)
                        {
                            FileInfo desiredBinplacePath = new FileInfo(Path.Combine(_workingDir.FullName, normalizedLibraryName));
                            try
                            {
                                logger.Log($"Resolved native library \"{libraryName}\" to {sourceLibraryFile.FullName}");
                                sourceLibraryFile.CopyTo(desiredBinplacePath.FullName);
                                _loadedLibraries[normalizedLibraryName] = NativeLibraryStatus.Available;
                                return NativeLibraryStatus.Available;
                            }
                            catch (Exception e)
                            {
                                logger.Log(e, LogLevel.Err);
                                logger.Log($"Could not prepare native library \"{libraryName}\" (is the existing library file locked or in use?)", LogLevel.Err);
                                _loadedLibraries[normalizedLibraryName] = NativeLibraryStatus.Unknown;
                                return NativeLibraryStatus.Unknown;
                            }
                        }
                        else
                        {
                            throw new PlatformNotSupportedException($"Don't know yet how to load libraries for {platform.OS}");
                        }
                    }
                }

                logger.LogFormat(LogLevel.Wrn, DataPrivacyClassification.SystemMetadata,
                    "Failed to resolve native library \"{0}\".", libraryName);
                _loadedLibraries[normalizedLibraryName] = NativeLibraryStatus.Unavailable;
                return NativeLibraryStatus.Unavailable;
            }
        }

        private void DeleteLocalLibraryIfPresent(string normalizedLibraryName, ILogger logger)
        {
            FileInfo existingLocalLibPath = new FileInfo(Path.Combine(_workingDir.FullName, normalizedLibraryName));

            if (existingLocalLibPath.Exists)
            {
                try
                {
                    logger.Log($"Clobbering existing file {existingLocalLibPath.FullName}", LogLevel.Wrn);
                    existingLocalLibPath.Delete();
                }
                catch (Exception)
                {
                    logger.Log($"Failed to clean up \"{existingLocalLibPath.FullName}\" (is it locked or in use?)", LogLevel.Wrn);
                }
            }
        }

        /// <summary>
        /// Determines the current operating system and processor architecture that this program is running in.
        /// </summary>
        /// <returns></returns>
        private static OSAndArchitecture GetCurrentPlatformInternal(ILogger logger)
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
            if (os == PlatformOperatingSystem.Unknown)
            {
                // Figure out our OS
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    os = PlatformOperatingSystem.Windows;
                }
                else if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANDROID_STORAGE")))
                {
                    os = PlatformOperatingSystem.Android;
                }
#if NET6_0_OR_GREATER
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
                {
                    os = PlatformOperatingSystem.FreeBSD;
                }
#endif
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    os = PlatformOperatingSystem.Linux;
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    os = PlatformOperatingSystem.MacOS;
                }
            }

            // Figure out our architecture
            if (arch == PlatformArchitecture.Unknown)
            {
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
#endif
                }

                // If we're on *nix, try to get more detailed info using uname -m
                if (os == PlatformOperatingSystem.Linux ||
                    os == PlatformOperatingSystem.Unix ||
                    os == PlatformOperatingSystem.Linux_Musl ||
                    os == PlatformOperatingSystem.Linux_Bionic ||
                    os == PlatformOperatingSystem.Android)
                {
                    PlatformArchitecture? possibleArch = KernelInteropLinux.TryGetArchForUnix(logger);
                    if (possibleArch.HasValue)
                    {
                        arch = possibleArch.Value;
                    }
                }
            }

            return new OSAndArchitecture(os, arch);
        }

        private static List<string> PermuteArchitectureSpecificDirectoryNames(OSAndArchitecture platformInfo)
        {
            string mostSpecificRid = $"{platformInfo.OS.GetRuntimeIdString()}-{platformInfo.Architecture.GetRuntimeIdString()}";

            IReadOnlyList<string> inheritedRids = NativePlatformUtils.GetInheritedRuntimeIds(mostSpecificRid);
            List<string> returnVal = new List<string>(inheritedRids.Count + 1);
            returnVal.Add(mostSpecificRid);

            // handle legacy windows IDs that might come up somewhere
            // this is a hack because we don't have proper handling of OS versioning in runtime IDs
            if (platformInfo.OS == PlatformOperatingSystem.Windows)
            {
                returnVal.Add($"win10-{platformInfo.Architecture.GetRuntimeIdString()}");
                returnVal.Add($"win81-{platformInfo.Architecture.GetRuntimeIdString()}");
                returnVal.Add($"win8-{platformInfo.Architecture.GetRuntimeIdString()}");
                returnVal.Add($"win7-{platformInfo.Architecture.GetRuntimeIdString()}");
                returnVal.Add($"win10");
                returnVal.Add($"win81");
                returnVal.Add($"win8");
                returnVal.Add($"win7");
            }

            returnVal.FastAddRangeReadOnlyCollection(inheritedRids);
            return returnVal;
        }

        private static string LibraryNameWithoutExtension(string libraryName)
        {
            if (!libraryName.Contains('.'))
            {
                return libraryName;
            }

            string libNameLowercase = libraryName.ToLowerInvariant();
            if (libNameLowercase.EndsWith(".dll") ||
                libNameLowercase.EndsWith(".so") ||
                libNameLowercase.EndsWith(".dylib"))
            {
                return libraryName.Substring(0, libraryName.LastIndexOf('.'));
            }

            return libraryName;
        }

        private static string NormalizeLibraryName(string requestedName, OSAndArchitecture platformInfo)
        {
            string nameWithoutExtension = LibraryNameWithoutExtension(requestedName);

            if (platformInfo.OS == PlatformOperatingSystem.Windows)
            {
                return nameWithoutExtension + ".dll";
            }
            else if (platformInfo.OS == PlatformOperatingSystem.Linux ||
                platformInfo.OS == PlatformOperatingSystem.Android ||
                platformInfo.OS == PlatformOperatingSystem.Linux_Bionic ||
                platformInfo.OS == PlatformOperatingSystem.Linux_Musl ||
                platformInfo.OS == PlatformOperatingSystem.Unix)
            {
                if (!nameWithoutExtension.StartsWith("lib", StringComparison.Ordinal))
                {
                    return $"lib{nameWithoutExtension}.so";
                }
                else
                {
                    return nameWithoutExtension + ".so";
                }
            }
            else if (platformInfo.OS == PlatformOperatingSystem.iOS ||
                platformInfo.OS == PlatformOperatingSystem.iOS_Simulator ||
                platformInfo.OS == PlatformOperatingSystem.MacOS ||
                platformInfo.OS == PlatformOperatingSystem.MacCatalyst)
            {
                if (!nameWithoutExtension.StartsWith("lib", StringComparison.Ordinal))
                {
                    return $"lib{nameWithoutExtension}.dylib";
                }
                else
                {
                    return nameWithoutExtension + ".dylib";
                }
            }
            else
            {
                return requestedName;
            }
        }

        private static List<string> PermuteLibraryNames(string requestedName, OSAndArchitecture platformInfo)
        {
            List<string> returnVal = new List<string>(16);
            string nameWithoutExtension = LibraryNameWithoutExtension(requestedName);

            if (platformInfo.OS == PlatformOperatingSystem.Windows)
            {
                returnVal.Add($"{nameWithoutExtension}.dll");
                returnVal.Add($"lib{nameWithoutExtension}.dll");
                if (platformInfo.Architecture == PlatformArchitecture.I386)
                {
                    returnVal.Add($"{nameWithoutExtension}_x86.dll");
                    returnVal.Add($"lib{nameWithoutExtension}_x86.dll");
                    returnVal.Add($"{nameWithoutExtension}x86.dll");
                    returnVal.Add($"lib{nameWithoutExtension}x86.dll");
                    returnVal.Add($"{nameWithoutExtension}32.dll");
                    returnVal.Add($"lib{nameWithoutExtension}32.dll");
                    returnVal.Add($"{nameWithoutExtension}_32.dll");
                    returnVal.Add($"lib{nameWithoutExtension}_32.dll");
                    returnVal.Add($"{nameWithoutExtension}-32.dll");
                    returnVal.Add($"lib{nameWithoutExtension}-32.dll");
                }

                if (platformInfo.Architecture == PlatformArchitecture.X64)
                {
                    returnVal.Add($"{nameWithoutExtension}_x64.dll");
                    returnVal.Add($"lib{nameWithoutExtension}_x64.dll");
                    returnVal.Add($"{nameWithoutExtension}x64.dll");
                    returnVal.Add($"lib{nameWithoutExtension}x64.dll");
                    returnVal.Add($"{nameWithoutExtension}64.dll");
                    returnVal.Add($"lib{nameWithoutExtension}64.dll");
                    returnVal.Add($"{nameWithoutExtension}_64.dll");
                    returnVal.Add($"lib{nameWithoutExtension}_64.dll");
                    returnVal.Add($"{nameWithoutExtension}-64.dll");
                    returnVal.Add($"lib{nameWithoutExtension}-64.dll");
                }
            }
            else if (platformInfo.OS == PlatformOperatingSystem.Linux ||
                platformInfo.OS == PlatformOperatingSystem.Android ||
                platformInfo.OS == PlatformOperatingSystem.Linux_Bionic ||
                platformInfo.OS == PlatformOperatingSystem.Linux_Musl ||
                platformInfo.OS == PlatformOperatingSystem.Unix)
            {
                returnVal.Add($"{nameWithoutExtension}.so");
                returnVal.Add($"lib{nameWithoutExtension}.so");
            }
            else if (platformInfo.OS == PlatformOperatingSystem.MacOS ||
                platformInfo.OS == PlatformOperatingSystem.MacCatalyst ||
                platformInfo.OS == PlatformOperatingSystem.iOS ||
                platformInfo.OS == PlatformOperatingSystem.iOS_Simulator)
            {
                returnVal.Add($"{nameWithoutExtension}.dylib");
                returnVal.Add($"lib{nameWithoutExtension}.dylib");
            }
            else
            {
                returnVal.Add(requestedName);
            }

            return returnVal;
        }
    }
}
