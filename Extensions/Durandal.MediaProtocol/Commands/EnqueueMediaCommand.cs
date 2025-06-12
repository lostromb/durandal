using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.MediaProtocol
{
    /// <summary>
    /// Adds media to the end of the current playlist, being selected by a variety of factors.
    /// Queue can be done by search term, song title, genre playlist name, series name, episode number, etc.
    /// </summary>
    public class EnqueueMediaCommand : MediaCommand
    {
        public override string Action
        {
            get
            {
                return "EnqueueMedia";
            }
        }
        
        /// <summary>
        /// The title of the media to enqueue e.g. "Ballad of Oregon"
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// A catch-all search term to be used when it's not quite certain what field (title / artist / series) the user has in mind
        /// </summary>
        public string SearchTerm { get; set; }

        /// <summary>
        /// The name of the artist to enqueue e.g. "Ashleigh Ball" (typically for music)
        /// </summary>
        public string Artist { get; set; }

        /// <summary>
        /// The name of the album to enqueue e.g. "Discovery" (typically for music)
        /// </summary>
        public string Album { get; set; }

        /// <summary>
        /// The name of the genre to enqueue e.g. "Blues" (typically for music)
        /// </summary>
        public string Genre { get; set; }

        /// <summary>
        /// The name of the playlist to enqueue e.g. "Christmas party"
        /// </summary>
        public string PlaylistName { get; set; }

        /// <summary>
        /// The name of the series to enqueue (as for TV / video) e.g. "Top Gear"
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

        /// <summary>
        /// After processing the initial query, attempt to filter that result set based on entries which also match this subquery.
        /// If no entries match the subquery, the result set is unmodified. This is intended for specifying some kind of variant filter
        /// on the media to be enqueued, for example { Artist:"Smashing Pumpkins" Subquery:"Unplugged" }
        /// </summary>
        public string Subquery { get; set; }
    }
}
