using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.MediaProtocol
{
    /// <summary>
    /// Clears the currently enqueued playlist, and implicitly stops all playback
    /// </summary>
    public class ClearPlaylistMediaCommand : MediaCommand
    {
        public override string Action
        {
            get
            {
                return "ClearPlaylist";
            }
        }
    }
}
