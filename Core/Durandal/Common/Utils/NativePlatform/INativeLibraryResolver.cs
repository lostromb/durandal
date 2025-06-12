using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Utils.NativePlatform
{
    /// <summary>
    /// Interface for a service which can determine the current runtime properties and preload
    /// native libraries in such a way that future P/Invoke calls on that library should succeed
    /// and invoke the correct library for the current runtime architecture.
    /// </summary>
    public interface INativeLibraryResolver
    {
        /// <summary>
        /// Given a native developer-provided library name, such as "mynativelib",
        /// search the current runtime directoty + /runtimes/{runtime ID}/native for files like "mynativelib.dll" / "mynativelib.so",
        /// matching the given library name and current runtime OS and architecture, and then prepare that library file
        /// in such a way that future P/Invoke calls to that library should succeed and should invoke the correct
        /// architecture-specific code.
        /// </summary>
        /// <param name="libraryName">The library name to prepare (without platform-specific extensions such as ".dll")</param>
        /// <param name="logger">A logger</param>
        /// <returns>The availability status of the requested library.</returns>
        NativeLibraryStatus PrepareNativeLibrary(string libraryName, ILogger logger);

        /// <summary>
        /// Gets information about the current runtime OS and processor, in parity with .Net's Runtime Identifier (RID) system.
        /// </summary>
        /// <returns>The current OS and architecture.</returns>
        OSAndArchitecture GetCurrentPlatform(ILogger logger);
    }
}
