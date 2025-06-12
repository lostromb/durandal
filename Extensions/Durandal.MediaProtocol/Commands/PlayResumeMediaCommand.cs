using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.MediaProtocol
{
    /// <summary>
    /// Sets current playback state to "playing", either resuming from a paused
    /// position, or starting at the beginning of the currently queued media
    /// </summary>
    public class PlayResumeMediaCommand : MediaCommand
    {
        public override string Action
        {
            get
            {
                return "PlayResumeMedia";
            }
        }
    }
}
