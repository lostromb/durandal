using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.MediaProtocol
{
    /// <summary>
    /// Represents a single piece of media that can be played, listened to, or viewed
    /// </summary>
    public class Media
    {
        public string Title { get; set; }
        public string ArtistPerformer { get; set; }
        public string Album { get; set; }
        public string Genre { get; set; }

        /// <summary>
        /// The name of the series (as for TV / video) e.g. "Top Gear"
        /// </summary>
        public string SeriesName { get; set; }

        /// <summary>
        /// The number of the series / season of the media, e.g. "Season 3"
        /// </summary>
        public int? SeriesNumber { get; set; }

        /// <summary>
        /// The number of the episode within a series. If series number is also present it is the episode within that series
        /// </summary>
        public int? EpisodeNumber { get; set; }
    }
}
