namespace Durandal.Answers.VLCAnswer
{
    using Durandal.Common.NLP;
    using Durandal.Common.Config;
    using Durandal.Common.Logger;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.RegularExpressions;
    using Durandal.Common.Statistics;
    using Durandal.Common.Utils;

    public class MovieLibrary
    {
        private IList<MovieFileEntry> knownMovies;
        private readonly IConfiguration globalConfiguration;
        private readonly ILogger _logger;
        
        public MovieLibrary(DirectoryInfo rootDirectory, IConfiguration configuration, ILogger logger, NLPTools.EditDistanceComparer editDist)
        {
            this.globalConfiguration = configuration;
            this._logger = logger;
            this.knownMovies = new List<MovieFileEntry>();
            if (!rootDirectory.Exists)
                this._logger.Log("Could not find movie library at path " + rootDirectory.FullName, LogLevel.Wrn);
            this.DiscoverAllMovies(rootDirectory, ref this.knownMovies, string.Empty, 0, editDist);
        }

        public IList<MovieFileEntry> FindMatchingMovies(string spokenMovieTitle, string seasonNum, string episodeNum, NLPTools.EditDistanceComparer editDist)
        {
            List<Hypothesis<MovieFileEntry>> resultsList = new List<Hypothesis<MovieFileEntry>>();
            string movieTitleLowercase = spokenMovieTitle.ToLowerInvariant();
            this._logger.Log("Finding movies to match query " + movieTitleLowercase);

            int seasonNumber = 0;
            int episodeNumber = 0;
            bool filterBySeasonAndEpisode = int.TryParse(seasonNum, out seasonNumber) &&
                int.TryParse(episodeNum, out episodeNumber);
            bool matchedSeasonAndEpisode = false;

            // Filter the list of all movies and find the one that is closest in edit distance
            foreach (MovieFileEntry potentialMovie in this.knownMovies)
            {
                foreach (string commonName in potentialMovie.ShortNames)
                {
                    float dist = editDist(commonName, movieTitleLowercase);
                    if (dist < this.globalConfiguration.GetFloat32("videoMatchThreshold"))
                    {
                        if (filterBySeasonAndEpisode)
                            matchedSeasonAndEpisode = matchedSeasonAndEpisode || 
                                (potentialMovie.SeasonNum == seasonNumber &&
                                 potentialMovie.EpisodeNum == episodeNumber);
                        resultsList.Add(new Hypothesis<MovieFileEntry>(potentialMovie, dist));
                        break;
                    }
                }
            }

            // If the user specified a season/episode, and we found an exact match, filter the list to only that result
            if (filterBySeasonAndEpisode && matchedSeasonAndEpisode)
            {
                List<Hypothesis<MovieFileEntry>> filteredResults = new List<Hypothesis<MovieFileEntry>>();
                foreach (var tuple in resultsList)
                {
                    if (tuple.Value.EpisodeNum == episodeNumber &&
                        tuple.Value.SeasonNum == seasonNumber)
                        filteredResults.Add(tuple);
                }
                
                resultsList = filteredResults;
            }

            // Sort movies based on their edit distance
            resultsList.Sort(new Hypothesis<MovieFileEntry>.DescendingComparator());

            IList<MovieFileEntry> returnVal = new List<MovieFileEntry>();
            foreach(var tuple in resultsList)
            {
                this._logger.Log("Matched movie " + tuple.Value.FilePath + " with confidence " + (1 - tuple.Conf));
                returnVal.Add(tuple.Value);
            }

            return returnVal;
        }
        
        private static bool IsAMovieFile(string fileName)
        {
            int lastDot = fileName.LastIndexOf('.');
            if (lastDot < 0)
                return false;
            string extension = fileName.Substring(lastDot + 1).ToLowerInvariant();
            return (extension.Equals("mp4") ||
                extension.Equals("mkv") ||
                extension.Equals("avi") ||
                extension.Equals("ogm") ||
                extension.Equals("m4v") ||
                extension.Equals("m2v") ||
                extension.Equals("mov"));
        }

        private void DiscoverAllMovies(DirectoryInfo directory,
            ref IList<MovieFileEntry> returnVal,
            string hypothesizedShowName,
            int hypothesizedSeasonNumber,
            NLPTools.EditDistanceComparer editDist)
        {
            if (!directory.Exists)
                return;

            // Inspect the directory for info about the current show title and season info (for series)
            string newShowNameHypothesis = directory.Name;
            Regex seasonExtractor = new Regex("[Ss](?:eason |eries )?([0-9]+)");
            string seasonNum = StringUtils.RegexRip(seasonExtractor, newShowNameHypothesis, 1);
            int newSeasonNumHypothesis = 0;
            if (int.TryParse(seasonNum, out newSeasonNumHypothesis))
            {
                hypothesizedSeasonNumber = newSeasonNumHypothesis;
                newShowNameHypothesis = StringUtils.RegexRemove(seasonExtractor, newShowNameHypothesis);
            }
            
            if (!string.IsNullOrWhiteSpace(newShowNameHypothesis))
                hypothesizedShowName = newShowNameHypothesis;

            foreach (FileInfo file in directory.GetFiles())
            {
                // Check if it's a movie file
                if (IsAMovieFile(file.Name))
                {
                    // Create entries
                    this._logger.Log("Discovered movie file " + file.Name, LogLevel.Vrb);
                    returnVal.Add(
                        MovieFileEntry.CreateFileEntriesFromPath(
                            file, 
                            hypothesizedShowName,
                            hypothesizedSeasonNumber,
                            this.globalConfiguration,
                            editDist));
                }
            }
            // Recurse into subdirectories
            foreach (DirectoryInfo subDir in directory.GetDirectories("*", SearchOption.TopDirectoryOnly))
            {
                this.DiscoverAllMovies(subDir, ref returnVal, hypothesizedShowName, hypothesizedSeasonNumber, editDist);
            }
        }
    }
}
