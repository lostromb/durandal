using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.MediaProtocol
{
    public class MediaQueuedResult : MediaResult
    {
        public override string Result
        {
            get
            {
                return "MediaQueued";
            }
        }

        /// <summary>
        /// The number of items that were enqueued for playback as a result of this command
        /// </summary>
        public int QueuedItemCount
        {
            get; set;
        }

        /// <summary>
        /// The media file that was first to be enqueued, if any. This can be analagous to 
        /// what's playing next in most cases
        /// </summary>
        public Media FirstQueuedMedia
        {
            get; set;
        }
    }
}
