using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.MediaProtocol
{
    /// <summary>
    /// Configures the Shuffle property of the media server which controls how to advance in the playlist
    /// </summary>
    public class SetShuffleMediaCommand : MediaCommand
    {
        public override string Action
        {
            get
            {
                return "SetShuffle";
            }
        }

        public bool EnableShuffle { get; set; }
    }
}
