//using Durandal.API;
////using Durandal.Common.Compression;
//using Durandal.Common.Compression.LZ4;
//using Durandal.Common.File;
//using Durandal.Common.Logger;
//using Durandal.Common.NLP.Feature;
//using Durandal.Common.Collections.Indexing;
//using Durandal.Common.Utils;
//using Durandal.Common.MathExt;
//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace Durandal.Common.NLP.ApproxString
//{
//    /// <summary>
//    /// Defines an index that is able to perform fuzzy string matching relatively quickly. The matching is based on non-statistical feature extraction and matching,
//    /// followed by a basic edit distance operation on the culled candidate set. This is usually orders of magnitude faster than calculating edit distance on each item in the index.
//    /// This version is "compact" because it stores its data inside a CompactIndex for memory compression
//    /// </summary>
//    public class ApproxStringMatchingIndexCompact : ILexicalMatcher
//    {
//        private static readonly IComparer<Hypothesis<Compact<string>>> COMPACT_HYP_SORTER = new Hypothesis<Compact<string>>.DescendingComparator();
//        private static readonly IComparer<Hypothesis<string>> HYP_SORTER = new Hypothesis<string>.DescendingComparator();

//        /// <summary>
//        /// used for serialization
//        /// </summary>
//        private const uint MAGIC_NUMBER = 0xE95AB47CU;

//        /// <summary>
//        /// The desired beam search size, expressed as a multiple of the total unculled search set
//        /// </summary>
//        private const double NBEAM_FACTOR = 0.03;

//        /// <summary>
//        /// The minimum beam search size
//        /// </summary>
//        private const int MIN_NBEAM_SIZE = 50;

//        // feature extractions
//        private readonly IApproxStringFeatureExtractor _featureExtractor;

//        // string pooling
//        private readonly ICompactIndex<string> _stringIndex;

//        // maps features to outcome strings
//        private readonly IDictionary<Compact<string>, ISet<Compact<string>>> _index = new Dictionary<Compact<string>, ISet<Compact<string>>>();

//        // a logger
//        private readonly ILogger _logger;

//        // a set of all outcome strings
//        private readonly ISet<Compact<string>> _outcomes = new HashSet<Compact<string>>();

//        // used to cache the count of features for each utterance; used to make sure that all features are fully matched (i.e. negative matching)
//        private readonly IDictionary<Compact<string>, int> _outcomeFeatureCounts = new Dictionary<Compact<string>, int>();

//        private readonly NLPTools.EditDistanceComparer _editDistanceAlgo;
        
//        public ApproxStringMatchingIndexCompact(IApproxStringFeatureExtractor featureExtractor, ICompactIndex<string> index, ILogger logger, NLPTools.EditDistanceComparer editDistanceAlgorithm = null)
//        {
//            _featureExtractor = featureExtractor;
//            _stringIndex = index;
//            _logger = logger;
//            _editDistanceAlgo = editDistanceAlgorithm ?? StringUtils.NormalizedEditDistance;
//        }

//        /// <summary>
//        /// Deserialization constructor
//        /// </summary>
//        /// <param name="featureExtractor"></param>
//        /// <param name="index"></param>
//        /// <param name="logger"></param>
//        private ApproxStringMatchingIndexCompact(
//            IApproxStringFeatureExtractor featureExtractor,
//            ICompactIndex<string> stringIndex,
//            ILogger logger,
//            IDictionary<Compact<string>, ISet<Compact<string>>> index,
//            ISet<Compact<string>> outcomes,
//            IDictionary<Compact<string>, int> outcomeFeatureCounts,
//            NLPTools.EditDistanceComparer editDistanceAlgorithm = null)
//        {
//            _featureExtractor = featureExtractor;
//            _stringIndex = stringIndex;
//            _logger = logger;
//            _index = index;
//            _outcomes = outcomes;
//            _outcomeFeatureCounts = outcomeFeatureCounts;
//            _editDistanceAlgo = editDistanceAlgorithm ?? StringUtils.NormalizedEditDistance;
//        }

//        public int OutcomeCount
//        {
//            get
//            {
//                return _outcomes.Count;
//            }
//        }

//        public int FeatureCount
//        {
//            get
//            {
//                return _index.Count;
//            }
//        }

//        public void Index(string input)
//        {
//            IList<string> features = _featureExtractor.ExtractFeatures(input);
//            if (features == null || features.Count == 0)
//                return;

//            //_logger.Log("Indexing " + input);
//            Compact<string> compactInput = _stringIndex.Store(input);
//            if (!_outcomes.Contains(compactInput))
//            {
//                _outcomes.Add(compactInput);
//                _outcomeFeatureCounts.Add(compactInput, features.Count);
//            }
//            else
//            {
//                // We've already indexed this outcome, so just ignore it
//                return;
//            }

//            foreach (var feature in features)
//            {
//                Compact<string> compactFeature = _stringIndex.Store(feature);
//                if (!_index.ContainsKey(compactFeature))
//                {
//                    _index[compactFeature] = new HashSet<Compact<string>>();
//                }
                
//                if (!_index[compactFeature].Contains(compactInput))
//                {
//                    _index[compactFeature].Add(compactInput);
//                }
//            }
//        }

//        public void Index(IEnumerable<string> input)
//        {
//            foreach (string i in input)
//            {
//                Index(i);
//            }
//        }

//        /// <summary>
//        /// Exhaustive search and compare with all strings in the index. Very slow, hence the name
//        /// </summary>
//        /// <param name="input"></param>
//        /// <param name="maxMatches"></param>
//        /// <returns></returns>
//        public IList<Hypothesis<string>> MatchSlow(string input, int maxMatches = 5)
//        {
//            List<Hypothesis<Compact<string>>> unculledReturnVal = new List<Hypothesis<Compact<string>>>();
//            foreach (Compact<string> outcome in _outcomes)
//            {
//                string x = _stringIndex.Retrieve(outcome);
//                float conf = 1.0f - _editDistanceAlgo(x, input);
//                unculledReturnVal.Add(new Hypothesis<Compact<string>>(outcome, conf));
//            }

//            unculledReturnVal.Sort(COMPACT_HYP_SORTER);
            
//            int numMatches = Math.Min(maxMatches, unculledReturnVal.Count);
//            List<Hypothesis<string>> returnVal = new List<Hypothesis<string>>();
//            for (int c = 0; c < numMatches; c++)
//            {
//                Hypothesis<Compact<string>> x = unculledReturnVal[c];
//                returnVal.Add(new Hypothesis<string>(_stringIndex.Retrieve(x.Value), x.Conf));
//            }

//            returnVal.Sort(HYP_SORTER);

//            return returnVal;
//        }

//        /// <summary>
//        /// Approximated feature-based beam search through the string index. Returns the top N matches
//        /// sorted, descending, based on edit distance from the input
//        /// </summary>
//        /// <param name="input"></param>
//        /// <param name="maxMatches"></param>
//        /// <returns></returns>
//        public IList<Hypothesis<string>> Match(string input, int maxMatches = 5)
//        {
//            IList<string> features = _featureExtractor.ExtractFeatures(input);
//            Counter<Compact<string>> featureHitCounts = new Counter<Compact<string>>();
//            ISet<Compact<string>> allHyps = new HashSet<Compact<string>>();
            
//            foreach (var feature in features)
//            {
//                Compact<string> compactFeature = _stringIndex.GetIndex(feature);
//                if (compactFeature != _stringIndex.GetNullIndex() && _index.ContainsKey(compactFeature))
//                {
//                    //_logger.Log("Feature " + _stringIndex.Retrieve(compactFeature) + " has outcome count " + _index[compactFeature].Count);

//                    foreach (Compact<string> outcome in _index[compactFeature])
//                    {
//                        if (!allHyps.Contains(outcome))
//                        {
//                            allHyps.Add(outcome);
//                        }

//                        featureHitCounts.Increment(outcome);
//                    }
//                }

//                //_logger.Log("Total potential outcomes is now " + allHyps.Count);
//            }

//            // Sort all of the potential hyps that came from feature matching
//            List<Hypothesis<Compact<string>>> unculledReturnVal = new List<Hypothesis<Compact<string>>>();
//            foreach (Compact<string> outcome in allHyps)
//            {
//                float outcomeFeatureCount = _outcomeFeatureCounts.ContainsKey(outcome) ? _outcomeFeatureCounts[outcome] : features.Count;
//                unculledReturnVal.Add(new Hypothesis<Compact<string>>(outcome, featureHitCounts.GetCount(outcome) / outcomeFeatureCount));
//            }

//            unculledReturnVal.Sort(COMPACT_HYP_SORTER);

//            // Now take the top few hyps (determined by beam search size) from that operation and actually calculate the edit distance from the input.
//            // This gives us the final confidence score.
//            int beamSearchSize = Math.Min(Math.Max(MIN_NBEAM_SIZE, (int)(NBEAM_FACTOR * unculledReturnVal.Count)), unculledReturnVal.Count);
//            List<Hypothesis<string>> returnVal = new List<Hypothesis<string>>();
//            for (int c = 0; c < beamSearchSize; c++)
//            {
//                string hyp = _stringIndex.Retrieve(unculledReturnVal[c].Value);
//                float conf = 1.0f - _editDistanceAlgo(input, hyp);
//                returnVal.Add(new Hypothesis<string>(hyp, conf));
//            }

//            returnVal.Sort(HYP_SORTER);

//            int numMatches = Math.Min(maxMatches, returnVal.Count);

//            // Now actually cull it to the desired user's result size
//            if (returnVal.Count > numMatches)
//            {
//                returnVal.RemoveRange(numMatches, returnVal.Count - numMatches);
//            }

//            return returnVal;
//        }

//        public async Task Serialize(IFileSystem fileSystem, VirtualPath targetName)
//        {
//            using (Stream baseStream = await fileSystem.OpenStreamAsync(targetName, FileOpenMode.Create, FileAccessMode.Write))
//            {
//                byte[] magicNumberBytes = BitConverter.GetBytes(MAGIC_NUMBER);
//                baseStream.Write(magicNumberBytes, 0, 4);
//                using (LZ4Stream compressor = new LZ4Stream(baseStream, LZ4StreamMode.Compress))
//                {
//                    using (BinaryWriter writer = new BinaryWriter(compressor, Encoding.UTF8, false))
//                    {
//                        writer.Write(_index.Count);
//                        foreach (var item in _index)
//                        {
//                            writer.Write(_stringIndex.Retrieve(item.Key));
//                            writer.Write(item.Value.Count);
//                            foreach (var item2 in item.Value)
//                            {
//                                writer.Write(_stringIndex.Retrieve(item2));
//                            }
//                        }

//                        writer.Write(_outcomes.Count);
//                        foreach (var outcome in _outcomes)
//                        {
//                            writer.Write(_stringIndex.Retrieve(outcome));
//                        }

//                        writer.Write(_outcomeFeatureCounts.Count);
//                        foreach (var count in _outcomeFeatureCounts)
//                        {
//                            writer.Write(_stringIndex.Retrieve(count.Key));
//                            writer.Write(count.Value);
//                        }

//                        writer.Dispose();
//                    }
//                }

//                // For some reason binarywriter does not dispose the base stream even when we explicitly tell it to
//                // that's what all these paranoid disposals are for
//                baseStream.Dispose();
//            }
//        }

//        public static async Task<ApproxStringMatchingIndexCompact> Deserialize(
//            IFileSystem fileSystem,
//            VirtualPath sourceFile,
//            IApproxStringFeatureExtractor featureExtractor,
//            ICompactIndex<string> stringIndex,
//            ILogger logger,
//            NLPTools.EditDistanceComparer editDistanceAlgorithm)
//        {
//            IDictionary<Compact<string>, ISet<Compact<string>>> index = new Dictionary<Compact<string>, ISet<Compact<string>>>();
//            ISet<Compact<string>> outcomes = new HashSet<Compact<string>>();
//            IDictionary<Compact<string>, int> outcomeFeatureCounts = new Dictionary<Compact<string>, int>();

//            if (!(await fileSystem.ExistsAsync(sourceFile)))
//            {
//                throw new FileNotFoundException("Serialized string index file not found", sourceFile.FullName);
//            }

//            using (Stream baseStream = await fileSystem.OpenStreamAsync(sourceFile, FileOpenMode.Open, FileAccessMode.Read))
//            {
//                byte[] magicNumberBytes = new byte[4];
//                baseStream.Read(magicNumberBytes, 0, 4);
//                uint magicNum = BitConverter.ToUInt32(magicNumberBytes, 0);
//                if (MAGIC_NUMBER != magicNum)
//                {
//                    throw new InvalidDataException("Serialized string matching index is corrupt or invalid.");
//                }

//                try
//                {
//                    using (LZ4Stream decompressor = new LZ4Stream(baseStream, LZ4StreamMode.Decompress))
//                    {
//                        using (BinaryReader reader = new BinaryReader(decompressor, Encoding.UTF8, false))
//                        {
//                            int indexSize = reader.ReadInt32();
//                            for (int i = 0; i < indexSize; i++)
//                            {
//                                string key = reader.ReadString();
//                                ISet<Compact<string>> outputSet = new HashSet<Compact<string>>();
//                                int mapCount = reader.ReadInt32();
//                                for (int m = 0; m < mapCount; m++)
//                                {
//                                    string value = reader.ReadString();
//                                    outputSet.Add(stringIndex.Store(value));
//                                }

//                                index.Add(stringIndex.Store(key), outputSet);
//                            }

//                            int outcomeCount = reader.ReadInt32();
//                            for (int i = 0; i < outcomeCount; i++)
//                            {
//                                string outcome = reader.ReadString();
//                                outcomes.Add(stringIndex.Store(outcome));
//                            }

//                            int outcomeFeatureCount = reader.ReadInt32();
//                            for (int i = 0; i < outcomeFeatureCount; i++)
//                            {
//                                string outcome = reader.ReadString();
//                                int value = reader.ReadInt32();
//                                outcomeFeatureCounts.Add(stringIndex.Store(outcome), value);
//                            }
//                        }
//                    }
//                }
//                catch (Exception e)
//                {
//                    throw new InvalidDataException("Serialized string matching index is corrupt or invalid", e);
//                }
//            }

//            return new ApproxStringMatchingIndexCompact(featureExtractor, stringIndex, logger, index, outcomes, outcomeFeatureCounts, editDistanceAlgorithm);
//        }
//    }
//}
