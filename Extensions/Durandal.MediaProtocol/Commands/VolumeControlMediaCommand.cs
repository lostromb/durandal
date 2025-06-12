using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.MediaProtocol
{
    /// <summary>
    /// Controls volume on the media server, either with a relative or absolute value
    /// </summary>
    public class VolumeControlMediaCommand : MediaCommand
    {
        public override string Action
        {
            get
            {
                return "VolumeControl";
            }
        }

        /// <summary>
        /// Whether to set the volume directly (absolute) or alter it by a percentage amount (relative)
        /// </summary>
        public ValueChangeType ChangeType { get; set; }

        /// <summary>
        /// If ChangeType is Absolute, this is the new volume to set, in the range of 0.0 - 1.0.
        /// If ChangeType is RelativeAdd, this is the percentage of volume to add / subtract, ranging from -1.0 to 1.0
        /// If ChangeType is RelativeMultiply, this is the number to multiply the current volume by, e.g. 0.7 to reduce volume by 30%
        /// </summary>
        public float Value { get; set; }
    }
}
