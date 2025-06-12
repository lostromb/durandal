using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Durandal.Common.Statistics.Classification
{
    using System.Text.RegularExpressions;

    using Durandal.API;
    using Durandal.Common.NLP.Feature;
    using Durandal.Common.NLP.Train;

    using Durandal.Common.Logger;
    using Durandal.Common.File;
    using Durandal.Common.Dialog;

    /// <summary>
    /// This class processes regex-based whitelists and blacklists, using them
    /// to generate classifier hypotheses as though they had come from a statistical model
    /// </summary>
    public class RegexClassifier
    {
        /// <summary>
        /// Defines the global flags to be used with each regex
        /// </summary>
        private const RegexOptions REGEX_OPTIONS =
            RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;
        
        private DomainIntent _domainIntent;
        private IFileSystem _fileSystem;
        private ILogger _logger;
        private IList<Regex> _whitelistRegexes = new List<Regex>();
        private IList<Regex> _blacklistRegexes = new List<Regex>(); 

        public RegexClassifier(DomainIntent domainIntent, IFileSystem fileSystem, VirtualPath whitelistFile, VirtualPath blacklistFile, ILogger logger)
        {
            _domainIntent = domainIntent;
            _fileSystem = fileSystem;
            _logger = logger;

            // Try and load the files
            _whitelistRegexes = ReadFile(_fileSystem, whitelistFile);
            _blacklistRegexes = ReadFile(_fileSystem, blacklistFile);
        }

        private IList<Regex> ReadFile(IFileSystem fileSystem, VirtualPath file)
        {
            IList<Regex> returnVal = new List<Regex>();
            
            if (!fileSystem.Exists(file))
            {
                return returnVal;
            }

            foreach (string line in fileSystem.ReadLines(file))
            {
                TrainingUtterance utterance = new TrainingUtterance();
                if (utterance.Parse(line))
                {
                    // These regexes should have already been validated, so no need to check here
                    Regex newRegex = new Regex(utterance.Utterance, REGEX_OPTIONS);
                    returnVal.Add(newRegex);
                }
                else
                {
                    _logger.Log("Error parsing the regex line \"" + line + "\" in " + file.FullName, LogLevel.Err);
                }
            }

            return returnVal;
        }

        public TaggedData ApplyWhitelist(Sentence utterance, bool wasSpeechInput, ILogger queryLogger)
        {
            int dummy;
            foreach (Regex regex in _whitelistRegexes)
            {
                Match match = regex.Match(utterance.OriginalText);
                if (match.Success)
                {
                    queryLogger.Log("Utterance matched by whitelist for " + _domainIntent, LogLevel.Vrb);
                    TaggedData returnVal = new TaggedData();
                    returnVal.Utterance = utterance.OriginalText;
                    // This results in confidences that are between 0.999 and 1.000, but vary based on the number of characters matched, to prevent tiny regexes from trumping more complex ones
                    returnVal.Confidence = 0.999f + (match.Length * 0.00001f);
                    GroupCollection groups = match.Groups;
                    // Convert the named groups in the regex into slots
                    foreach (string groupName in regex.GetGroupNames())
                    {
                        var matchGroup = groups[groupName];
                        // Convert non-numerical groups that matched successfully into slot values
                        if (matchGroup.Success && (groupName.Length > 2 || !int.TryParse(groupName, out dummy)))
                        {
                            SlotValue newSlot = new SlotValue(groupName, matchGroup.Value, wasSpeechInput ? SlotValueFormat.SpokenText : SlotValueFormat.TypedText);
                            newSlot.Annotations[SlotPropertyName.StartIndex] = matchGroup.Index.ToString();
                            newSlot.Annotations[SlotPropertyName.StringLength] = matchGroup.Length.ToString();
                            newSlot.Alternates = new List<string>();
                            returnVal.Slots.Add(newSlot);
                        }
                    }
                    return returnVal;
                }
            }
            return null;
        }

        public IList<RecoResult> ApplyBlacklist(IList<RecoResult> existingResults, Sentence utterance, bool wasSpeechInput, ILogger queryLogger)
        {
            IList<RecoResult> returnVal = new List<RecoResult>();
            foreach (RecoResult r in existingResults)
            {
                bool anyRegexMatches = false;
                foreach (Regex regex in _blacklistRegexes)
                {
                    Match match = regex.Match(utterance.OriginalText);
                    if (match.Success)
                    {
                        
                        anyRegexMatches = true;
                        break;
                    }
                }

                if (anyRegexMatches)
                {
                    queryLogger.Log("Removed hypothesis " + r.Domain + "/" + r.Intent + " because it was matched by the blacklist", LogLevel.Vrb);
                }
                else
                {
                    // No regex match, so pass this result through
                    returnVal.Add(r);
                }
            }
            return returnVal;
        }
    }
}
