namespace Durandal.Common.Utils.NativePlatform
{
    /// <summary>
    /// Represents the status of loading a native library.
    /// </summary>
    public enum NativeLibraryStatus
    {
        /// <summary>
        /// The library may or may not be available.
        /// </summary>
        Unknown,

        /// <summary>
        /// The library is available and ready to invoke.
        /// </summary>
        Available,

        /// <summary>
        /// The library is not available on this system.
        /// </summary>
        Unavailable,
    }
}