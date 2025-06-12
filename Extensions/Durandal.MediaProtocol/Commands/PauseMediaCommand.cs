using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.MediaProtocol
{
    /// <summary>
    /// Sets the current playback state to "paused".
    /// In the case of video, the screen should stay on a freeze frame.
    /// </summary>
    public class PauseMediaCommand : MediaCommand
    {
        public override string Action
        {
            get
            {
                return "PauseMedia";
            }
        }
    }
}
