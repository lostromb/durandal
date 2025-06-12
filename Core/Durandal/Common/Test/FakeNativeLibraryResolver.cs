using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.Utils.NativePlatform;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;

namespace Durandal.Common.Test
{
    public class FakeNativeLibraryResolver : INativeLibraryResolver
    {
        private OSAndArchitecture _arch;
        private IDictionary<string, NativeLibraryStatus> _libraryStatuses;

        public FakeNativeLibraryResolver()
        {
            _arch = new OSAndArchitecture(PlatformOperatingSystem.Unknown, PlatformArchitecture.Unknown);
            _libraryStatuses = new Dictionary<string, NativeLibraryStatus>();
        }

        public void SetFakeMachinePlatformForTest(OSAndArchitecture arch)
        {
            _arch = arch;
        }

        public void SetFakeNativeLibStatusForTest(string libraryName, NativeLibraryStatus status)
        {
            _libraryStatuses[libraryName] = status;
        }

        /// <inheritdoc />
        public OSAndArchitecture GetCurrentPlatform(ILogger logger)
        {
            return _arch;
        }

        /// <inheritdoc />
        public NativeLibraryStatus PrepareNativeLibrary(string libraryName, ILogger logger)
        {
            NativeLibraryStatus returnVal;
            if (!_libraryStatuses.TryGetValue(libraryName, out returnVal))
            {
                throw new ArgumentException($"Test setup for native library {libraryName} was not done correctly");
            }

            return returnVal;
        }
    }
}
