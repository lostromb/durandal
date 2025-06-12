using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Audio.Hardware
{
    /// <summary>
    /// Base implementation of <see cref="IAudioCaptureDeviceId"/> which provides boilerplate equals / hashcode support.
    /// </summary>
    public abstract class AbstractAudioCaptureDeviceId : IAudioCaptureDeviceId
    {
        /// <inheritdoc />
        public abstract string DriverName { get; }

        /// <inheritdoc />
        public abstract string Id {  get; }

        /// <inheritdoc />
        public abstract string DeviceFriendlyName { get; set; }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is IAudioCaptureDeviceId))
            {
                return false;
            }

            return string.Equals(((IAudioCaptureDeviceId)obj).Id, this.Id, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public bool Equals(IAudioCaptureDeviceId other)
        {
            return string.Equals(other.Id, this.Id, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return this.Id.GetHashCode();
        }
    }
}
