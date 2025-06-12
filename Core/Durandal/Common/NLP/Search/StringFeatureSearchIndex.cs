using Durandal.API;
using Durandal.Common.Collections;
using Durandal.Common.Compression.LZ4;
using Durandal.Common.File;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.NLP.ApproxString;
using Durandal.Common.Statistics;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Durandal.Common.NLP.Search
{
    /// <summary>
    /// A basic implementation of a search index
    /// </summary>
    /// <typeparam name="T">The type of documents to be indexed</typeparam>
    public class StringFeatureSearchIndex<T> : ISearchIndex<T>
    {
        private static readonly IComparer<Hypothesis<T>> HYP_SORTER = new Hypothesis<T>.DescendingComparator();
        
        // feature extractions
        private readonly IApproxStringFeatureExtractor _featureExtractor;

        // maps features to outcome strings
        private readonly IDictionary<string, ISet<string>> _index = new Dictionary<string, ISet<string>>();

        // a logger
        private readonly ILogger _logger;

        // a set of all outcome strings
        private readonly ISet<string> _outcomes = new HashSet<string>();

        // used to cache the count of features for each utterance; used to make sure that all features are fully matched (i.e. negative matching)
        private readonly IDictionary<string, int> _outcomeFeatureCounts = new Dictionary<string, int>();

        // used to apply greater weight to rarer features
        private readonly Counter<string> _featureOccurrenceCounts = new Counter<string>();

        private readonly IDictionary<string, ISet<T>> _documentMapping = new Dictionary<string, ISet<T>>();

        public StringFeatureSearchIndex(IApproxStringFeatureExtractor featureExtractor, ILogger logger)
        {
            _featureExtractor = featureExtractor;
            _logger = logger;
        }

        /// <summary>
        /// Deserialization constructor
        /// </summary>
        /// <param name="featureExtractor"></param>
        /// <param name="logger"></param>
        /// <param name="index"></param>
        /// <param name="outcomes"></param>
        /// <param name="outcomeFeatureCounts"></param>
        /// <param name="featureOccurrenceCounts"></param>
        /// <param name="documentMapping"></param>
        private StringFeatureSearchIndex(
            IApproxStringFeatureExtractor featureExtractor,
            ILogger logger,
            IDictionary<string, ISet<string>> index,
            ISet<string> outcomes,
            IDictionary<string, int> outcomeFeatureCounts,
            Counter<string> featureOccurrenceCounts,
            IDictionary<string, ISet<T>> documentMapping)
        {
            _featureExtractor = featureExtractor;
            _logger = logger;
            _index = index;
            _outcomes = outcomes;
            _outcomeFeatureCounts = outcomeFeatureCounts;
            _featureOccurrenceCounts = featureOccurrenceCounts;
            _documentMapping = documentMapping;
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

        public void Index(string input, T document)
        {
            IList<string> features = _featureExtractor.ExtractFeatures(new LexicalString(input));
            if (features == null || features.Count == 0)
                return;

            // Make sure this input is in our document mapping
            ISet<T> documentsMatchingThisInput;
            if (!_documentMapping.TryGetValue(input, out documentsMatchingThisInput))
            {
                documentsMatchingThisInput = new HashSet<T>();
                _documentMapping[input] = documentsMatchingThisInput;
            }

            if (!documentsMatchingThisInput.Contains(document))
            {
                documentsMatchingThisInput.Add(document);
            }

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
                    _index[feature] = new HashSet<string>();
                }

                if (!_index[feature].Contains(input))
                {
                    _index[feature].Add(input);
                }

                _featureOccurrenceCounts.Increment(feature);
            }
        }

        public void Index(IDictionary<string, T> inputs)
        {
            foreach (var i in inputs)
            {
                Index(i.Key, i.Value);
            }
        }

        /// <summary>
        /// Approximated feature-based beam search through the string index. Returns the top N matches
        /// sorted descending
        /// </summary>
        /// <param name="input"></param>
        /// <param name="maxMatches"></param>
        /// <returns></returns>
        public IList<Hypothesis<T>> Search(string input, int maxMatches = 5)
        {
            IList<string> features = _featureExtractor.ExtractFeatures(new LexicalString(input));
            Counter<string> featureHitCounts = new Counter<string>();
            Counter<string> featureRarities = new Counter<string>();
            ISet<string> allHyps = new HashSet<string>();
            
            float logFeatures = (float)Math.Log10(_index.Count);
            float maxFeatureCount = 0;

            // Sort features from rarest to most common. This will help us cull common features as we go
            string[] sortedFeatures = features.ToArray();
            int featuresCount = sortedFeatures.Length;
            float[] featureCounts = new float[featuresCount];

            for (int c = 0; c < featuresCount; c++)
            {
                ISet<string> outcomes;
                if (_index.TryGetValue(sortedFeatures[c], out outcomes))
                {
                    featureCounts[c] = outcomes.Count;
                }
                else
                {
                    featureCounts[c] = 0;
                }
            }

            ArrayExtensions.Sort(featureCounts, sortedFeatures);

            // This metric is kind of a ghetto optimization that works "ok" generally.
            // what we really need is a statistical metric that says "what are the actual odds that, if we continue searching, we will find a new hyp that outranks all the current ones?"
            // then we can use that to optimize....optimally
            float likelihoodWeHaveTheCorrectHyp = 0;
            
            for (int featureidx = 0; featureidx < featuresCount; featureidx++)
            {
                float featureSearchTimeWeight = (float)featureidx / (float)featuresCount;
                featureSearchTimeWeight = featureSearchTimeWeight * featureSearchTimeWeight;
                string feature = sortedFeatures[featureidx];

                if (feature != null && _index.ContainsKey(feature))
                {
                    float idx = 0;
                    // defines the fractional number of outcomes to skip
                    // when we've already found a lot of hyps, start skipping over the large outcome sets on the assumption that they're less likely to find high confidence outputs
                    float increment = Math.Max(1, likelihoodWeHaveTheCorrectHyp / 100);
                    foreach (string outcome in _index[feature])
                    {
                        idx += 1;
                        if (idx < increment)
                        {
                            continue;
                        }

                        idx -= increment;
                        float featureCount = _featureOccurrenceCounts.GetCount(feature);
                        float featureRarity = Math.Max(0, (logFeatures - (float)Math.Log10(featureCount)));

                        if (!allHyps.Contains(outcome))
                        {
                            allHyps.Add(outcome);
                            likelihoodWeHaveTheCorrectHyp += featureSearchTimeWeight; // Start skipping at a faster rate farther along we are in the feature
                            increment = Math.Max(1, likelihoodWeHaveTheCorrectHyp / 100);
                        }

                        featureHitCounts.Increment(outcome);
                        maxFeatureCount = Math.Max(maxFeatureCount, featureRarities.Increment(outcome, featureRarity));
                    }
                }

                //_logger.Log(outcomesFound);
            }

            if (maxFeatureCount == 0)
            {
                maxFeatureCount = 1;
            }

            float rarityWeight = 0.75f;
            float inverseWeight = 1.0f - rarityWeight;

            // Sort all of the potential hyps that came from feature matching
            List<Hypothesis<T>> returnVal = new List<Hypothesis<T>>();
            foreach (string outcome in allHyps)
            {
                float outcomeFeatureCount = _outcomeFeatureCounts.ContainsKey(outcome) ? _outcomeFeatureCounts[outcome] : features.Count;
                float portionFeaturesMatched = featureHitCounts.GetCount(outcome) / outcomeFeatureCount;
                float featureRarity = featureRarities.GetCount(outcome) / maxFeatureCount;
                // Map outcomes to actual documents
                float confidence = (featureRarity * rarityWeight) + (portionFeaturesMatched * inverseWeight);
                foreach (T document in _documentMapping[outcome])
                {
                    returnVal.Add(new Hypothesis<T>(document, confidence));
                }
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

        public void Serialize(IFileSystem fileSystem, VirtualPath targetName)
        {
            using (Stream baseStream = fileSystem.OpenStream(targetName, FileOpenMode.Create, FileAccessMode.Write))
            {
                using (LZ4Stream compressor = new LZ4Stream(baseStream, LZ4StreamMode.Compress))
                {
                    using (BinaryWriter writer = new BinaryWriter(compressor, StringUtils.UTF8_WITHOUT_BOM, false))
                    {
                        writer.Write(_index.Count);
                        foreach (var item in _index)
                        {
                            writer.Write(item.Key);
                            writer.Write(item.Value.Count);
                            foreach (var item2 in item.Value)
                            {
                                writer.Write(item2);
                            }
                        }

                        writer.Write(_outcomes.Count);
                        foreach (var outcome in _outcomes)
                        {
                            writer.Write(outcome);
                        }

                        writer.Write(_outcomeFeatureCounts.Count);
                        foreach (var count in _outcomeFeatureCounts)
                        {
                            writer.Write(count.Key);
                            writer.Write(count.Value);
                        }

                        writer.Write(_featureOccurrenceCounts.NumItems);
                        foreach (KeyValuePair<string, float> item in _featureOccurrenceCounts)
                        {
                            writer.Write(item.Key);
                            writer.Write(item.Value);
                        }

                        Type docType = typeof(T);
                        // Write the type name of the documents in this index, for type checking later
                        writer.Write(docType.ToString());
                        writer.Write(_documentMapping.Count);
                        foreach (var kvp in _documentMapping)
                        {
                            writer.Write(kvp.Key);
                            writer.Write(kvp.Value.Count);
                            foreach (T document in kvp.Value)
                            {
                                // Figure out how to serialize this document
                                if (docType == typeof(int))
                                    writer.Write((int)((object)document));
                                else if (docType == typeof(string))
                                    writer.Write((string)((object)document));
                                else if (docType == typeof(long))
                                    writer.Write((long)((object)document));
                                else if (docType == typeof(uint))
                                    writer.Write((uint)((object)document));
                                else if (docType == typeof(ulong))
                                    writer.Write((ulong)((object)document));
                                else if (docType == typeof(short))
                                    writer.Write((short)((object)document));
                                else if (docType == typeof(ushort))
                                    writer.Write((ushort)((object)document));
                                else if (docType == typeof(byte))
                                    writer.Write((byte)((object)document));
                                else if (docType == typeof(char))
                                    writer.Write((char)((object)document));
                                else if (docType == typeof(decimal))
                                    writer.Write((decimal)((object)document));
                                else if (docType == typeof(sbyte))
                                    writer.Write((sbyte)((object)document));
                                else
                                    throw new InvalidCastException("Cannot serialize a string index with documents of type \"" + docType.ToString() + "\"");
                            }
                        }

                        writer.Dispose();
                    }
                }

                // For some reason binarywriter does not dispose the base stream even when we explicitly tell it to
                // that's what all these paranoid disposals are for
                baseStream.Dispose();
            }
        }

        public static StringFeatureSearchIndex<T> Deserialize(
            IFileSystem fileSystem,
            VirtualPath sourceStream,
            IApproxStringFeatureExtractor featureExtractor,
            ILogger logger)
        {
            IDictionary<string, ISet<string>> index = new Dictionary<string, ISet<string>>();
            ISet<string> outcomes = new HashSet<string>();
            IDictionary<string, int> outcomeFeatureCounts = new Dictionary<string, int>();
            Counter<string> featureOccurrenceCounts = new Counter<string>();
            IDictionary<string, ISet<T>> documentMapping = new Dictionary<string, ISet<T>>();

            using (Stream baseStream = fileSystem.OpenStream(sourceStream, FileOpenMode.Open, FileAccessMode.Read))
            {
                using (LZ4Stream decompressor = new LZ4Stream(baseStream, LZ4StreamMode.Decompress))
                {
                    using (BinaryReader reader = new BinaryReader(decompressor, StringUtils.UTF8_WITHOUT_BOM, false))
                    {
                        int indexSize = reader.ReadInt32();
                        for (int i = 0; i < indexSize; i++)
                        {
                            string key = reader.ReadString();
                            ISet<string> outputSet = new HashSet<string>();
                            int mapCount = reader.ReadInt32();
                            for (int m = 0; m < mapCount; m++)
                            {
                                string value = reader.ReadString();
                                outputSet.Add(value);
                            }

                            index.Add(key, outputSet);
                        }

                        int outcomeCount = reader.ReadInt32();
                        for (int i = 0; i < outcomeCount; i++)
                        {
                            string outcome = reader.ReadString();
                            outcomes.Add(outcome);
                        }

                        int outcomeFeatureCount = reader.ReadInt32();
                        for (int i = 0; i < outcomeFeatureCount; i++)
                        {
                            string outcome = reader.ReadString();
                            int value = reader.ReadInt32();
                            outcomeFeatureCounts.Add(outcome, value);
                        }

                        int featureOccurrenceCount = reader.ReadInt32();
                        for (int i = 0; i < featureOccurrenceCount; i++)
                        {
                            string feature = reader.ReadString();
                            float count = reader.ReadSingle();
                            featureOccurrenceCounts.Increment(feature, count);
                        }

                        Type docType = typeof(T);
                        string docTypeName = reader.ReadString();
                        if (!string.Equals(docType.ToString(), docTypeName))
                        {
                            throw new InvalidCastException("The serialized string index reports it contains documents of type " + docTypeName + " but it is being deserialized into an index of type " + docType.ToString());
                        }

                        int documentMappingCount = reader.ReadInt32();
                        for (int i = 0; i < documentMappingCount; i++)
                        {
                            string documentString = reader.ReadString();
                            ISet<T> docs = new HashSet<T>();
                            int numDocuments = reader.ReadInt32();
                            for (int m = 0; m < numDocuments; m++)
                            {
                                T doc;
                                // Figure out how to deserialize this document type
                                if (docType == typeof(int))
                                    doc = (T)(object)reader.ReadInt32();
                                else if (docType == typeof(string))
                                    doc = (T)(object)reader.ReadString();
                                else if (docType == typeof(long))
                                    doc = (T)(object)reader.ReadInt64();
                                else if (docType == typeof(uint))
                                    doc = (T)(object)reader.ReadUInt32();
                                else if (docType == typeof(ulong))
                                    doc = (T)(object)reader.ReadUInt64();
                                else if (docType == typeof(short))
                                    doc = (T)(object)reader.ReadInt16();
                                else if (docType == typeof(ushort))
                                    doc = (T)(object)reader.ReadUInt16();
                                else if (docType == typeof(byte))
                                    doc = (T)(object)reader.ReadByte();
                                else if (docType == typeof(char))
                                    doc = (T)(object)reader.ReadChar();
                                else if (docType == typeof(decimal))
                                    doc = (T)(object)reader.ReadDecimal();
                                else if (docType == typeof(sbyte))
                                    doc = (T)(object)reader.ReadSByte();
                                else
                                    throw new InvalidCastException("Cannot deserialize a string index with documents of type \"" + docType.ToString() + "\"");

                                docs.Add(doc);
                            }

                            documentMapping[documentString] = docs;
                        }
                    }
                }
            }

            return new StringFeatureSearchIndex<T>(featureExtractor, logger, index, outcomes, outcomeFeatureCounts, featureOccurrenceCounts, documentMapping);
        }
    }
}
