using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.MediaProtocol
{
    /// <summary>
    /// A media command to advance or reverse playlist position. Think "next/prev track" function.
    /// The playback state of the media should not be affected - if playing is active, play will continue starting at the new track.
    /// </summary>
    public class AdvancePlaylistMediaCommand : MediaCommand
    {
        public override string Action
        {
            get
            {
                return "AdvancePlaylist";
            }
        }

        /// <summary>
        /// The number of tracks to skip. Can be negative. Default 1
        /// 1 = go to next track, -1 = go to previous track, etc.
        /// </summary>
        public int? TracksToSkip { get; set; }
    }
}
