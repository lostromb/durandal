﻿using System;
using System.Runtime.InteropServices;

namespace ManagedBass
{
    /// <summary>
    /// Wraps bass.dll.
    ///
    /// <para>
    /// Supports: .mp3, .ogg, .wav, .mp2, .mp1, .aiff, .m2a, .mpa, .m1a, .mpg, .mpeg, .aif, .mp3pro, .bwf, .mus,
    /// .mod, .mo3, .s3m, .xm, .it, .mtm, .umx, .mdz, .s3z, .itz, .xmz
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// BASS is a multiplatform audio library.
    /// It's purpose is to provide the most powerful and efficient (yet easy to use),
    /// sample, stream, MOD music, and recording functions.
    /// All in a tiny DLL, under 100KB in size.
    /// </para>
    /// </remarks>
    public static partial class Bass
    {
#if __IOS__
        const string DllName = "__Internal";
#else
        const string DllName = "bass";
#endif

#if !WINDOWS_PHONE_APP
        static IntPtr hLib;

        /// <summary>
        /// Load this library into Memory.
        /// </summary>
        /// <param name="Folder">Directory to Load from... <see langword="null"/> (default) = Load from Current Directory.</param>
        /// <returns><see langword="true" />, if the library loaded successfully, else <see langword="false" />.</returns>
        /// <remarks>
        /// <para>
        /// An external library is loaded into memory when any of its methods are called for the first time.
        /// This results in the first method call being slower than all subsequent calls.
        /// </para>
        /// <para>
        /// Some BASS libraries and add-ons may introduce new options to the main BASS lib like new parameters.
        /// But, before using these new options the respective library must be already loaded.
        /// This method can be used to make sure, that this library has been loaded.
        /// </para>
        /// </remarks>
        public static bool Load(string Folder = null) => (hLib = DynamicLibrary.Load(DllName, Folder)) != IntPtr.Zero;
#endif

        /// <summary>
        /// Unloads this library from Memory.
        /// </summary>
        /// <returns><see langword="true" />, if the library unloaded successfully, else <see langword="false" />.</returns>
        //public static bool Unload() => DynamicLibrary.Unload(hLib);

        /// <summary>
        /// Gets the Formats supported by Bass.
        /// </summary>
        public static PluginFormat[] SupportedFormats
        {
            get
            {
                return PluginGetInfo(0).Formats;
            }
        }

        /// <summary>
        /// Updates the HSTREAM and HMUSIC channel playback buffers.
        /// </summary>
        /// <param name="Length">The amount of data to render, in milliseconds.</param>
        /// <returns>If successful, then <see langword="true" /> is returned, else <see langword="false" /> is returned. Use <see cref="LastError" /> to get the error code.</returns>
        /// <exception cref="Errors.NotAvailable">Updating is already in progress.</exception>
        /// <remarks>
        /// When automatic updating is disabled, this function (or <see cref="ChannelUpdate" />) needs to be called to keep the playback buffers updated.
        /// The <paramref name="Length"/> parameter should include some safety margin, in case the next update cycle gets delayed.
        /// For example, if calling this function every 100ms, 200 would be a reasonable <paramref name="Length"/> parameter.
        /// </remarks>
        /// <seealso cref="ChannelUpdate"/>
        /// <seealso cref="PlaybackBufferLength"/>
        /// <seealso cref="UpdateThreads"/>
        [DllImport(DllName, EntryPoint = "BASS_Update")]
        public static extern bool Update(int Length);

#region CPUUsage
        [DllImport(DllName)]
        static extern float BASS_GetCPU();

        /// <summary>
        /// Retrieves the current CPU usage of BASS as a percentage of total CPU time.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This function includes the time taken to render stream (HSTREAM) and MOD music (HMUSIC) channels during playback, and any DSP functions set on those channels.
        /// It slso includes any FX that are not using the "with FX flag" DX8 effect implementation.
        /// </para>
        /// <para>
        /// The rendering of some add-on stream formats may not be entirely included, if they use additional decoding threads.
        /// See the add-on documentation for details.
        /// </para>
        /// <para>
        /// This function does not strictly tell the CPU usage, but rather how timely the processing is.
        /// For example, if it takes 10ms to render 100ms of data, that would be 10%.
        /// If the reported usage gets to 100%, that means the channel data is being played faster than it can be rendered, and Buffer underruns are likely to occur.
        /// </para>
        /// <para>
        /// If automatic updating is disabled, then the value returned by this function is only updated after each call to <see cref="Update" />.
        /// <see cref="ChannelUpdate" /> usage is not included.
        /// The CPU usage of an individual channel is available via the <see cref="ChannelAttribute.CPUUsage"/> attribute.
        /// </para>
        /// <para><b>Platform-specific</b></para>
        /// <para>
        /// On Windows, the CPU usage does not include sample channels (HCHANNEL), which are mixed by the output device/drivers (hardware mixing) or Windows (software mixing).
        /// On other platforms, the CPU usage does include sample playback as well as the generation of the final output mix.
        /// </para>
        /// </remarks>
        public static double CPUUsage
        {
            get
            {
                return BASS_GetCPU();
            }
        }
#endregion

#region Version
        [DllImport(DllName)]
        static extern int BASS_GetVersion();

        /// <summary>
        /// Retrieves the version of BASS that is loaded
        /// </summary>
        public static Version Version
        {
            get
            {
                return Extensions.GetVersion(BASS_GetVersion());
            }
        }
#endregion

#region Error Code
        [DllImport(DllName)]
        static extern Errors BASS_ErrorGetCode();

        /// <summary>
        /// Gets the Error that occured on the Last Bass function that was called.
        /// </summary>
        public static Errors LastError
        {
            get
            {
                return BASS_ErrorGetCode();
            }
        }
#endregion
    }
}
