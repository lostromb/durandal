using Durandal.API;
using Durandal.Common.File;
using Durandal.Common.Logger;
using Durandal.Common.Utils.NativePlatform;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace Durandal.AndroidClient.Common
{
    public class AndroidNativeLibraryResolver : INativeLibraryResolver
    {
        private static readonly Lazy<OSAndArchitecture> CachedPlatformInfo = new Lazy<OSAndArchitecture>(
            GetCurrentPlatformInternal, LazyThreadSafetyMode.PublicationOnly);

        private readonly IDictionary<string, NativeLibraryStatus> _loadedLibraries = new Dictionary<string, NativeLibraryStatus>();
        private readonly object _libraryLoadMutex = new object();

        /// <inheritdoc />
        public OSAndArchitecture GetCurrentPlatform()
        {
            return CachedPlatformInfo.Value;
        }

        /// <inheritdoc />
        public NativeLibraryStatus PrepareNativeLibrary(string libraryName, ILogger logger)
        {
            logger.Log(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "Preparing native library \"{0}\"", libraryName);

            OSAndArchitecture platform = CachedPlatformInfo.Value;
            string normalizedLibraryName = NormalizeLibraryName(libraryName, platform);
            lock (_libraryLoadMutex)
            {
                NativeLibraryStatus prevStatus;
                if (_loadedLibraries.TryGetValue(normalizedLibraryName, out prevStatus))
                {
                    logger.Log(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "Native library \"{0}\" has already been prepared; nothing to do", libraryName);
                    return prevStatus;
                }

                // On Android, all we can do is probe for .so files included in our package APK.
                // So if probe fails, just return false.
                NativeLibraryStatus builtInLibStatus = ProbeLibrary(normalizedLibraryName);
                if (builtInLibStatus == NativeLibraryStatus.Available)
                {
                    logger.Log(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "Native library \"{0}\" was resolved successfully", libraryName);
                    _loadedLibraries[normalizedLibraryName] = builtInLibStatus;
                    return builtInLibStatus;
                }

                logger.Log(LogLevel.Wrn, DataPrivacyClassification.SystemMetadata,
                    "Failed to resolve native library \"{0}\".", libraryName);
                _loadedLibraries[normalizedLibraryName] = NativeLibraryStatus.Unavailable;
                return NativeLibraryStatus.Unavailable;
            }
        }

        /// <summary>
        /// Determines the current operating system and processor architecture that this program is running in.
        /// </summary>
        /// <returns></returns>
        private static OSAndArchitecture GetCurrentPlatformInternal()
        {
            PlatformOperatingSystem os = PlatformOperatingSystem.Android;

            // Figure out our architecture
            PlatformArchitecture arch = PlatformArchitecture.Unknown;
            if (RuntimeInformation.ProcessArchitecture == Architecture.X86)
            {
                arch = PlatformArchitecture.I386;
            }
            else if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
            {
                arch = PlatformArchitecture.X64;
            }
            else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm)
            {
                arch = PlatformArchitecture.ArmV7;
            }
            else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                arch = PlatformArchitecture.Arm64;
            }

            return new OSAndArchitecture(os, arch);
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
            if (nameWithoutExtension.StartsWith("lib"))
            {
                return nameWithoutExtension + ".so";
            }
            else
            {
                return "lib" + nameWithoutExtension + ".so";
            }
        }

        private NativeLibraryStatus ProbeLibrary(string libName)
        {
            IntPtr soHandle = IntPtr.Zero;
            try
            {
                soHandle = dlopen(libName, RTLD_NOW);
                return soHandle == IntPtr.Zero ? NativeLibraryStatus.Unavailable : NativeLibraryStatus.Available;
            }
            finally
            {
                if (soHandle != IntPtr.Zero)
                {
                    dlclose(soHandle);
                }
            }
        }

        private const int RTLD_NOW = 2;

        [DllImport("libdl.so")]
        private static extern IntPtr dlopen(string fileName, int flags);

        [DllImport("libdl.so")]
        private static extern int dlclose(IntPtr handle);

        [DllImport("libdl.so")]
        private static extern IntPtr dlerror();
    }
}
