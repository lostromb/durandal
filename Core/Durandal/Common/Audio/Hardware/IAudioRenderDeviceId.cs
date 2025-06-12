using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Audio.Hardware
{
    /// <summary>
    /// Defines an audio render device that can be handled by a specific <see cref="IAudioDriver"/>.
    /// Different implementations are provided by different drivers and cannot be interchanged.
    /// </summary>
    public interface IAudioRenderDeviceId : IEquatable<IAudioRenderDeviceId>
    {
        /// <summary>
        /// Gets the string representation of this ID, which should uniquely identify
        /// this device in the system (in a format that's not necessarily human-readable, such as a GUID).
        /// By convention, the ID should be preceded by the driver name and ':'. For example, "Wasapi:01" or
        /// "ALSA:{guid}". This more clearly distinguishes IDs provided by different drivers.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Gets the driver name that provides this audio device.
        /// </summary>
        string DriverName { get; }

        /// <summary>
        /// Gets the human-readable name of the device.
        /// </summary>
        string DeviceFriendlyName { get; }
    }
}
