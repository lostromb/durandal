using Durandal.API;
using Durandal.Common.Compression.LZ4;
using Durandal.Common.File;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.NLP.Alignment;
using Durandal.Common.NLP.Language;
using Durandal.Common.Statistics;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.NLP.ApproxString
{
    /// <summary>
    /// Defines an index that is able to perform fuzzy string matching relatively quickly. The matching is based on non-statistical feature extraction and matching,
    /// followed by a basic edit distance operation on the culled candidate set. This is usually orders of magnitude faster than calculating edit distance on each item in the index.
    /// This version is slightly faster at the expense of memory because it does not use string pooling
    /// </summary>
    public class ApproxStringMatchingIndex : ILexicalMatcher
    {
        private static readonly IComparer<Hypothesis<LexicalString>> HYP_SORTER = new Hypothesis<LexicalString>.DescendingComparator();

        /// <summary>
        /// used for serialization. If you change the serialized format you should also change this
        /// </summary>
        private const uint MAGIC_NUMBER = 0x61F39E5BU;

        /// <summary>
        /// The desired beam search size, expressed as a multiple of the total unculled search set
        /// </summary>
        private const double NBEAM_FACTOR = 0.03;

        /// <summary>
        /// The minimum beam search size
        /// </summary>
        private const int MIN_NBEAM_SIZE = 50;

        // feature extractions
        private readonly IApproxStringFeatureExtractor _featureExtractor;

        // maps features to outcome strings
        private readonly IDictionary<string, ISet<LexicalString>> _index;

        // a logger
        private readonly ILogger _logger;

        // the locale of strings that this index is matching against
        private readonly LanguageCode _locale;

        // a set of all outcome strings
        private readonly ISet<LexicalString> _outcomes;

        // used to cache the count of features for each utterance; used to make sure that all features are fully matched (i.e. negative matching)
        private readonly IDictionary<LexicalString, int> _outcomeFeatureCounts;

        public ApproxStringMatchingIndex(
            IApproxStringFeatureExtractor featureExtractor,
            LanguageCode locale,
            ILogger logger)
        {
            _featureExtractor = featureExtractor;
            _logger = logger;
            _locale = locale;
            _index = new Dictionary<string, ISet<LexicalString>>();
            _outcomes = new HashSet<LexicalString>();
            _outcomeFeatureCounts = new Dictionary<LexicalString, int>();
        }

        /// <summary>
        /// Deserialization constructor
        /// </summary>
        /// <param name="featureExtractor"></param>
        /// <param name="logger"></param>
        /// <param name="locale"></param>
        /// <param name="index"></param>
        /// <param name="outcomes"></param>
        /// <param name="outcomeFeatureCounts"></param>
        private ApproxStringMatchingIndex(
            IApproxStringFeatureExtractor featureExtractor,
            ILogger logger,
            LanguageCode locale,
            IDictionary<string, ISet<LexicalString>> index,
            ISet<LexicalString> outcomes,
            IDictionary<LexicalString, int> outcomeFeatureCounts)
        {
            _featureExtractor = featureExtractor;
            _logger = logger;
            _index = index;
            _outcomes = outcomes;
            _outcomeFeatureCounts = outcomeFeatureCounts;
            _locale = locale;
        }

        public int OutcomeCount
        {
            get
            {
                return _outcomes.Count;
            }
        }

        public int FeatureCount
        {
            get
            {
                return _index.Count;
            }
        }

        public void Index(LexicalString input)
        {
            IList<string> features = _featureExtractor.ExtractFeatures(input);
            if (features == null || features.Count == 0)
                return;
            
            if (!_outcomes.Contains(input))
            {
                _outcomes.Add(input);
                _outcomeFeatureCounts.Add(input, features.Count);
            }
            else
            {
                // We've already indexed this outcome, so just ignore it
                return;
            }

            foreach (var feature in features)
            {
                if (!_index.ContainsKey(feature))
                {
                    _index[feature] = new HashSet<LexicalString>();
                }

                if (!_index[feature].Contains(input))
                {
                    _index[feature].Add(input);
                }
            }
        }

        public void Index(IEnumerable<LexicalString> input)
        {
            foreach (LexicalString i in input)
            {
                Index(i);
            }
        }

        private static float Score(LexicalString a, LexicalString b, LanguageCode locale)
        {
            if (string.IsNullOrEmpty(a.SpokenForm) ||
                string.IsNullOrEmpty(b.SpokenForm))
            {
                // Lexical comparison is not possible because lexical forms are not given and we don't have NL tools
                return Math.Max(0, 1f - StringUtils.NormalizedEditDistance(a.WrittenForm, b.WrittenForm));
            }
            
            float lexicalDist = InternationalPhoneticAlphabet.EditDistance(a.SpokenForm, b.SpokenForm, locale);
            if (lexicalDist == 0)
            {
                // If pronunciations are the same, fall back to actual spelling edit distance
                float writtenDist = StringUtils.NormalizedEditDistance(a.WrittenForm, b.WrittenForm);

                return Math.Max(0, 1f - ((writtenDist + lexicalDist) / 2));
            }
            else
            {
                return Math.Max(0, 1f - lexicalDist);
            }
        }

        /// <summary>
        /// Exhaustive search and compare with all strings in the index. Very slow, hence the name
        /// </summary>
        /// <param name="input"></param>
        /// <param name="maxMatches"></param>
        /// <returns></returns>
        public IList<Hypothesis<LexicalString>> MatchSlow(LexicalString input, int maxMatches = 5)
        {
            List<Hypothesis<LexicalString>> unculledReturnVal = new List<Hypothesis<LexicalString>>();
            foreach (LexicalString outcome in _outcomes)
            {
                float conf = Score(outcome, input, _locale);
                unculledReturnVal.Add(new Hypothesis<LexicalString>(outcome, conf));
            }

            unculledReturnVal.Sort(HYP_SORTER);

            int numMatches = Math.Min(maxMatches, unculledReturnVal.Count);
            List<Hypothesis<LexicalString>> returnVal = new List<Hypothesis<LexicalString>>();
            for (int c = 0; c < numMatches; c++)
            {
                Hypothesis<LexicalString> x = unculledReturnVal[c];
                returnVal.Add(new Hypothesis<LexicalString>(x.Value, x.Conf));
            }

            returnVal.Sort(HYP_SORTER);

            return returnVal;
        }

        // used to tune the ideal initial counter size
        // initial measurement shows that the hit counts ~= features.Count * 240;
        //private static StaticAverage counterEffeciency = new StaticAverage();

        /// <summary>
        /// Approximated feature-based beam search through the string index. Returns the top N matches
        /// sorted, descending, based on edit distance from the input
        /// </summary>
        /// <param name="input"></param>
        /// <param name="maxMatches"></param>
        /// <returns></returns>
        public IList<Hypothesis<LexicalString>> Match(LexicalString input, int maxMatches = 5)
        {
            IList<string> features = _featureExtractor.ExtractFeatures(input);
            int initialCounterSize = features.Count * 500;
            Counter<LexicalString> featureHitCounts = new Counter<LexicalString>(initialCounterSize);
            ISet<LexicalString> allHyps = new HashSet<LexicalString>();

            foreach (var feature in features)
            {
                if (feature != null && _index.ContainsKey(feature))
                {
                    //_logger.Log("Feature " + _stringIndex.Retrieve(compactFeature) + " has outcome count " + _index[compactFeature].Count);

                    foreach (LexicalString outcome in _index[feature])
                    {
                        if (!allHyps.Contains(outcome))
                        {
                            allHyps.Add(outcome);
                        }

                        featureHitCounts.Increment(outcome);
                    }
                }

                //_logger.Log("Total potential outcomes is now " + allHyps.Count);
            }

            //double tableEfficiency = ((double)featureHitCounts.NumItems / (double)initialCounterSize);
            //counterEffeciency.Add(tableEfficiency);
            //_logger.Log("Table efficiency is " + counterEffeciency.Average);

            // Sort all of the potential hyps that came from feature matching
            List<Hypothesis<LexicalString>> unculledReturnVal = new List<Hypothesis<LexicalString>>();
            foreach (LexicalString outcome in allHyps)
            {
                float outcomeFeatureCount = _outcomeFeatureCounts.ContainsKey(outcome) ? _outcomeFeatureCounts[outcome] : features.Count;
                unculledReturnVal.Add(new Hypothesis<LexicalString>(outcome, featureHitCounts.GetCount(outcome) / outcomeFeatureCount));
            }

            unculledReturnVal.Sort(HYP_SORTER);

            // Now take the top few hyps (determined by beam search size) from that operation and actually calculate the edit distance from the input.
            // This gives us the final confidence score.
            int beamSearchSize = Math.Min(Math.Max(MIN_NBEAM_SIZE, (int)(NBEAM_FACTOR * unculledReturnVal.Count)), unculledReturnVal.Count);
            List<Hypothesis<LexicalString>> returnVal = new List<Hypothesis<LexicalString>>();
            for (int c = 0; c < beamSearchSize; c++)
            {
                LexicalString hyp = unculledReturnVal[c].Value;
                float conf = Score(input, hyp, _locale);
                returnVal.Add(new Hypothesis<LexicalString>(hyp, conf));
            }

            returnVal.Sort(HYP_SORTER);

            int numMatches = Math.Min(maxMatches, returnVal.Count);

            // Now actually cull it to the desired user's result size
            if (returnVal.Count > numMatches)
            {
                returnVal.RemoveRange(numMatches, returnVal.Count - numMatches);
            }

            return returnVal;
        }

        public async Task Serialize(IFileSystem fileSystem, VirtualPath targetName)
        {
            using (Stream baseStream = await fileSystem.OpenStreamAsync(targetName, FileOpenMode.Create, FileAccessMode.Write).ConfigureAwait(false))
            {
                byte[] magicNumberBytes = BitConverter.GetBytes(MAGIC_NUMBER);
                baseStream.Write(magicNumberBytes, 0, 4);
                using (LZ4Stream compressor = new LZ4Stream(baseStream, LZ4StreamMode.Compress))
                {
                    using (BinaryWriter writer = new BinaryWriter(compressor, StringUtils.UTF8_WITHOUT_BOM, false))
                    {
                        writer.Write(_index.Count);
                        foreach (var item in _index)
                        {
                            writer.Write(item.Key);
                            writer.Write(item.Value.Count);
                            foreach (LexicalString item2 in item.Value)
                            {
                                item2.Serialize(writer);
                            }
                        }

                        writer.Write(_outcomes.Count);
                        foreach (LexicalString outcome in _outcomes)
                        {
                            outcome.Serialize(writer);
                        }

                        writer.Write(_outcomeFeatureCounts.Count);
                        foreach (KeyValuePair<LexicalString, int> count in _outcomeFeatureCounts)
                        {
                            count.Key.Serialize(writer);
                            writer.Write(count.Value);
                        }

                        writer.Dispose();
                    }
                }

                // For some reason binarywriter does not dispose the base stream even when we explicitly tell it to
                // that's what all these paranoid disposals are for
                baseStream.Dispose();
            }
        }

        public static async Task<ApproxStringMatchingIndex> Deserialize(
            IFileSystem fileSystem,
            VirtualPath sourceFile,
            IApproxStringFeatureExtractor featureExtractor,
            ILogger logger,
            LanguageCode locale)
        {
            IDictionary<string, ISet<LexicalString>> index = new Dictionary<string, ISet<LexicalString>>();
            ISet<LexicalString> outcomes = new HashSet<LexicalString>();
            IDictionary<LexicalString, int> outcomeFeatureCounts = new Dictionary<LexicalString, int>();

            if (!(await fileSystem.ExistsAsync(sourceFile).ConfigureAwait(false)))
            {
                throw new FileNotFoundException("Serialized string index file not found", sourceFile.FullName);
            }

            using (Stream baseStream = await fileSystem.OpenStreamAsync(sourceFile, FileOpenMode.Open, FileAccessMode.Read).ConfigureAwait(false))
            {
                byte[] magicNumberBytes = new byte[4];
                baseStream.Read(magicNumberBytes, 0, 4);
                uint magicNum = BitConverter.ToUInt32(magicNumberBytes, 0);
                if (MAGIC_NUMBER != magicNum)
                {
                    throw new InvalidDataException("Serialized string matching index is corrupt or invalid.");
                }

                try
                {
                    using (LZ4Stream decompressor = new LZ4Stream(baseStream, LZ4StreamMode.Decompress))
                    {
                        using (BinaryReader reader = new BinaryReader(decompressor, StringUtils.UTF8_WITHOUT_BOM, false))
                        {
                            int indexSize = reader.ReadInt32();
                            for (int i = 0; i < indexSize; i++)
                            {
                                string key = reader.ReadString();
                                ISet<LexicalString> outputSet = new HashSet<LexicalString>();
                                int mapCount = reader.ReadInt32();
                                for (int m = 0; m < mapCount; m++)
                                {
                                    outputSet.Add(LexicalString.Deserialize(reader));
                                }

                                index.Add(key, outputSet);
                            }

                            int outcomeCount = reader.ReadInt32();
                            for (int i = 0; i < outcomeCount; i++)
                            {
                                outcomes.Add(LexicalString.Deserialize(reader));
                            }

                            int outcomeFeatureCount = reader.ReadInt32();
                            for (int i = 0; i < outcomeFeatureCount; i++)
                            {
                                LexicalString key = LexicalString.Deserialize(reader);
                                int value = reader.ReadInt32();
                                outcomeFeatureCounts.Add(key, value);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    throw new InvalidDataException("Serialized string matching index is corrupt or invalid", e);
                }
            }

            return new ApproxStringMatchingIndex(featureExtractor, logger, locale, index, outcomes, outcomeFeatureCounts);
        }
    }
}
