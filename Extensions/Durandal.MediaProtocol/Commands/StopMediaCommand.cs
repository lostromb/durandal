using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.MediaProtocol
{
    /// <summary>
    /// Stops the currently playing media and resets playback state.
    /// In the case of video, the video output should be closed.
    /// </summary>
    public class StopMediaCommand : MediaCommand
    {
        public override string Action
        {
            get
            {
                return "StopMedia";
            }
        }
    }
}
