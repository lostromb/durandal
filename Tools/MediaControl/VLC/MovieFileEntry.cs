namespace Durandal.Answers.VLCAnswer
{
    using Durandal.Common.Config;
    using Durandal.Common.NLP;
    using Durandal.Common.Utils;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;

    public class MovieFileEntry
    {
        public IList<string> ShortNames = new List<string>();
        public string FilePath = string.Empty;
        public int SeasonNum = 0;
        public int EpisodeNum = 0;

        private static Regex seasonParser = new Regex("(?<!\\w)[Ss]?\\.?([0-9]{1,2})[Xx\\. -][Ee]?\\.?([0-9]{1,2})(?!\\w)");
        private static Regex episodeParser = new Regex("(?<!\\w)(?:[Ee]pisode |E\\.?|[Ee][Pp]\\.?[ -]?)([0-9]{1,2})(?!\\w)");
        
        private static List<string> BreakIntoPhrases(string fileName)
        {
            fileName = fileName.Replace('(', '-');
            fileName = fileName.Replace(')', '-');
            fileName = fileName.Replace('[', '-');
            fileName = fileName.Replace(']', '-');
            fileName = fileName.Replace(',', ' ');
            fileName = fileName.Replace('.', ' ');
            fileName = fileName.Replace("  ", " ");
            fileName = fileName.Replace("  ", " ");
            Regex extractor = new Regex("(?<=^|-)[^-]+(?=$|-)");
            MatchCollection matches = extractor.Matches(fileName);
            List<string> returnVal = new List<string>();
            foreach (Match m in matches)
            {
                string newPhrase = m.Value.Trim();
                if (!string.IsNullOrWhiteSpace(newPhrase))
                    returnVal.Add(newPhrase);
            }
            return returnVal;
        }

        private static void AllCombinations(string[] components, ref IList<string> returnVal)
        {
            for (int outputLength = 1; outputLength <= components.Length; outputLength++)
            {
                int[] indices = new int[outputLength];
                bool[] taken = new bool[components.Length];
                for (int index = 0; index < outputLength; index++)
                    indices[index] = 0;
                while (indices[0] < components.Length)
                {
                    // Validate the index
                    for (int index = outputLength - 1; index > 0; index--)
                    {
                        if (indices[index] >= components.Length)
                        {
                            indices[index] = 0;
                            indices[index - 1]++;
                        }
                    }

                    if (indices[0] >= components.Length)
                        break;

                    // Is it a valid combination?
                    bool valid = true;
                    for (int index = 0; index < components.Length; index++)
                        taken[index] = false;
                    for (int index = 0; index < outputLength; index++)
                    {
                        if (taken[indices[index]])
                            valid = false;
                        taken[indices[index]] = true;
                    }

                    if (valid)
                    {
                        // Create a combination
                        string thisCombination = string.Empty;
                        for (int index = 0; index < outputLength; index++)
                        {
                            thisCombination += " " + components[indices[index]];
                        }
                        thisCombination = thisCombination.Trim();
                        returnVal.Add(thisCombination);
                    }

                    // Bump the index
                    indices[outputLength - 1]++;
                }
            }
        }

        // Sorts strings in descending order of length
        private class StringLengthComparator : IComparer<string>
        {
            public int Compare(string x, string y)
            {
                return y.Length - x.Length;
            }
        }

        public static MovieFileEntry CreateFileEntriesFromPath(
            FileInfo filePath,
            string hypothesizedShowTitle,
            int hypothesizedSeasonNum,
            IConfiguration configuration,
            NLPTools.EditDistanceComparer editDist)
        {
            MovieFileEntry returnVal = new MovieFileEntry();
            returnVal.FilePath = filePath.FullName;

            // Get the normalized file name without extension
            string simpleFileName = filePath.Name.ToLowerInvariant();
            if (simpleFileName.Contains('.')) // Trim the extension
            {
                simpleFileName = simpleFileName.Substring(0, simpleFileName.LastIndexOf('.'));
            }

            // Try and extract season/episode information
            if (seasonParser.IsMatch(simpleFileName))
            {
                string seasonNum = StringUtils.RegexRip(seasonParser, simpleFileName, 1);
                string episodeNum = StringUtils.RegexRip(seasonParser, simpleFileName, 2);
                int.TryParse(seasonNum, out returnVal.SeasonNum);
                int.TryParse(episodeNum, out returnVal.EpisodeNum);
                simpleFileName = StringUtils.RegexReplace(seasonParser, simpleFileName, " - ");
            }

            // Look for episode info if the season is absent. In this case, use the fallback season info
            // that came from the parent directory (if any)
            // But keep in mind cases like "Star Wars Episode 1" - in this case, we match an "episode" but
            // no season, so don't process it as a TV series
            // TODO: This currently does not support video series that have no "season" or "series" information, like webcasts
            if (hypothesizedSeasonNum != 0)
            {
                if (episodeParser.IsMatch(simpleFileName))
                {
                    string episodeNum = StringUtils.RegexRip(episodeParser, simpleFileName, 1);
                    int.TryParse(episodeNum, out returnVal.EpisodeNum);
                    simpleFileName = StringUtils.RegexReplace(episodeParser, simpleFileName, " - ");
                }
            }

            // Split it into phrases
            List<string> phrases = BreakIntoPhrases(simpleFileName);

            // If this is a episodic show, and the name of the show is not attached to each file name,
            // add it to the phrase list
            if (returnVal.SeasonNum != 0 && returnVal.EpisodeNum != 0)
            {
                bool showNamePresent = false;
                string showNameWithoutArticles = hypothesizedShowTitle;
                // Allow matching of things like "Mentalist" to "the mentalist"
                showNameWithoutArticles = StringUtils.RegexRemove(new Regex("[Tt]he"), showNameWithoutArticles);
                foreach (string subphrase in phrases)
                {
                    showNamePresent = showNamePresent ||
                        editDist(subphrase, hypothesizedShowTitle) < 0.15 ||
                        editDist(subphrase, showNameWithoutArticles) < 0.15;
                }

                if (!showNamePresent)
                    phrases.Add(hypothesizedShowTitle);
            }

            // No show title was found. Use the backup show title extracted from the directory name
            if (phrases.Count == 0)
            {
                returnVal.ShortNames.Add(hypothesizedShowTitle);
            }
            else
            {
                phrases.Sort(new StringLengthComparator());
                // Are there more than 3 subphrases?
                if (phrases.Count > configuration.GetInt32("maxVideoFileSubphrases"))
                {
                    // If so, keep only the longest ones
                    List<string> newPhrases = new List<string>();
                    for (int index = 0; index < phrases.Count; index++ )
                    {
                        // If there is more than one title, and the rest are really short
                        // (less than 6 or so chars), omit them
                        if (index == 0 || phrases[index].Length >= configuration.GetInt32("minVideoFilePhraseLength"))
                            newPhrases.Add(phrases[index]);
                    }
                    phrases = newPhrases;
                }

                // And add each combination of subphrases to the common name list
                AllCombinations(phrases.ToArray(), ref returnVal.ShortNames);
            }

            return returnVal;
        }
    }
}
