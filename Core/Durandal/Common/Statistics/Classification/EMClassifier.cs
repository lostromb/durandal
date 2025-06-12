using Durandal.Common.NLP.Feature;

namespace Durandal.Common.Statistics.Classification
{
    using Durandal.Common.File;
    using Durandal.Common.Logger;
    using Durandal.Common.Collections.Indexing;
    using Durandal.Common.NLP.Train;
    using Durandal.Common.Statistics;
    using Durandal.Common.Utils;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using Durandal.Common.IO;

    public class EMClassifier
    {
        private static readonly string FILE_EXT = ".em";
        private const int TRAINING_PASSES = 10;

        private readonly ILogger _logger;
        private readonly ICompactIndex<string> _stringIndex;
        private readonly IFileSystem _fileSystem;
        private IDictionary<Compact<string>, int> _featureMapping;
        private IDictionary<Compact<string>, int> _domainMapping;
        private float[][] featureWeights;
        private bool useSquaredFeatureWeights;
        private const float TRAINING_INCREMENT = 0.03f;
        private bool useSqrtFeatureCounts;
        private string singleOutputOverride;
        private bool useSingleOutputOverride;
        private string _modelName;

        public EMClassifier(bool squaredFeatureWeights, bool sqrtFeatureCounts, ICompactIndex<string> masterStringIndex, ILogger logger, IFileSystem fileSystem, string modelName)
        {
            useSquaredFeatureWeights = squaredFeatureWeights;
            useSqrtFeatureCounts = sqrtFeatureCounts;
            _logger = logger;
            _featureMapping = new Dictionary<Compact<string>, int>();
            _domainMapping = new Dictionary<Compact<string>, int>();
            singleOutputOverride = null;
            useSingleOutputOverride = false;
            _stringIndex = masterStringIndex;
            _fileSystem = fileSystem;
            _modelName = modelName;
        }

        public static bool IsTrainingRequired(VirtualPath cachedWeightFile, IFileSystem fileSystem)
        {
            // Is the cached file already there?
            if (fileSystem.Exists(cachedWeightFile + FILE_EXT))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="trainingData"></param>
        /// <param name="cachedWeightFile"></param>
        /// <returns>True if the cache was used</returns>
        public void TrainFromData(
            TrainingDataList<TrainingEvent> trainingData,
            VirtualPath cachedWeightFile)
        {
            Stopwatch timer = new Stopwatch();
            timer.Start();

            if (_fileSystem.Exists(cachedWeightFile + FILE_EXT))
            {
                // Load the cache if possible
                LoadCache(cachedWeightFile + FILE_EXT);
                return;
            }

            float[][] featureCounts;

            // Index the features and domains
            string[] allFeatures = new string[trainingData.TrainingData.Count];
            string[] allDomains = new string[trainingData.TrainingData.Count];
            int idx = 0;
            foreach (TrainingEvent feature in trainingData.TrainingData)
            {
                allDomains[idx] = feature.Outcome;
                allFeatures[idx++] = feature.Context[0];
            }
            int didx = 0;
            int fidx = 0;
            for (int t = 0; t < allFeatures.Length; t++)
            {
                Compact<string> domain = _stringIndex.Store(allDomains[t]);
                if (!_domainMapping.ContainsKey(domain))
                {
                    _domainMapping[domain] = didx++;
                    singleOutputOverride = allDomains[t];
                }

                Compact<string> feature = _stringIndex.Store(allFeatures[t]);
                if (!_featureMapping.ContainsKey(feature))
                {
                    _featureMapping[feature] = fidx++;
                }
            }

            if (_domainMapping.Count > 1)
            {
                singleOutputOverride = null;
                useSingleOutputOverride = false;
            }
            else if (singleOutputOverride != null)
            {
                // If there is only 1 possible output classification, this model can just hardwire
                // itself to only return that value.
                // Stop all processing in this case and free a bunch of our memory
                useSingleOutputOverride = true;
                _featureMapping = null;
                _domainMapping = null;
                featureWeights = null;

                // Write the override value to the cached model file
                WriteCache(cachedWeightFile + FILE_EXT);
                return;
            }
            else
            {
                _logger.Log("EM classifier \"" + _modelName + "\" is being trained with no output domains! This will not work", LogLevel.Err);
            }

            // Build the matrix and accumulate counts
            int numFeatures = _featureMapping.Count;
            int numDomains = _domainMapping.Count;
            featureCounts = new float[numDomains][];
            for (int c = 0; c < numDomains; c++)
            {
                featureCounts[c] = new float[numFeatures];
            }

            for (int t = 0; t < allFeatures.Length; t++)
            {
                Compact<string> feature = _stringIndex.GetIndex(allFeatures[t]);
                Compact<string> domain = _stringIndex.GetIndex(allDomains[t]);
                featureCounts[_domainMapping[domain]][_featureMapping[feature]] += 1;
            }

            featureWeights = Train(featureCounts, numDomains, numFeatures, useSqrtFeatureCounts);

            timer.Stop();

            // Print out the top features
            /*for (int dom = 0; dom < numDomains; dom++)
            {
                Console.WriteLine("Top feature weights for {0}", _domainIndex.Retrieve(dom));
                SortedList<float, string> topFeatures = new SortedList<float, string>(new DescendingComparer());
                for (int feat = 0; feat < numFeatures; feat++)
                {
                    if (!topFeatures.ContainsKey(featureWeights[dom][feat]))
                    topFeatures.Add(featureWeights[dom][feat], _featureIndex.Retrieve(feat));
                }
                var enumerator = topFeatures.GetEnumerator();
                int count = 8;
                while (count-- > 0 && enumerator.MoveNext())
                {
                    Console.WriteLine("{0} : {1}", enumerator.Current.Value, enumerator.Current.Key);
                }

                Console.WriteLine("Bottom feature weights for {0}", _domainIndex.Retrieve(dom));
                SortedList<float, string> bottomFeatures = new SortedList<float, string>();
                for (int feat = 0; feat < numFeatures; feat++)
                {
                    if (!bottomFeatures.ContainsKey(featureWeights[dom][feat]))
                        bottomFeatures.Add(featureWeights[dom][feat], _featureIndex.Retrieve(feat));
                }
                enumerator = bottomFeatures.GetEnumerator();
                count = 8;
                while (count-- > 0 && enumerator.MoveNext())
                {
                    Console.WriteLine("{0} : {1}", enumerator.Current.Value, enumerator.Current.Key);
                }
            }*/

            _logger.Log("Model trained in " + timer.ElapsedMilliseconds + " ms.", LogLevel.Std);

            WriteCache(cachedWeightFile + FILE_EXT);
        }

        // untested!
        //private void WriteCacheCSV(string cacheFileName)
        //{
        //    using (StreamWriter fileOut = new StreamWriter(cacheFileName))
        //    {
        //        fileOut.WriteLine(_domainMapping.Count);
        //        fileOut.WriteLine(_featureMapping.Count);
        //        fileOut.Write("Weights,");
        //        int idx = 0;
        //        foreach (Compact<string> domain in _domainMapping.Keys)
        //        {
        //            fileOut.Write(_stringIndex.Retrieve(domain));
        //            if (idx++ < _domainMapping.Count - 1)
        //                fileOut.Write(",");
        //        }
        //        fileOut.WriteLine();
        //        idx = 0;
        //        foreach (Compact<string> feature in _featureMapping.Keys)
        //        {
        //            fileOut.Write(_stringIndex.Retrieve(feature) + ",");
        //            foreach (Compact<string> domain in _domainMapping.Keys)
        //            {
        //                fileOut.Write(featureWeights[_domainMapping[domain]][_featureMapping[feature]]);
        //                if (idx++ < _domainMapping.Count - 1)
        //                    fileOut.Write(",");
        //            }
        //            fileOut.WriteLine();
        //        }
        //        fileOut.WriteLine();
        //        fileOut.Close();
        //    }
        //}

        //private void LoadCacheCSV(string cacheFileName)
        //{
        //    using (StreamReader fileIn = new StreamReader(cacheFileName))
        //    {
        //        int numDomains = int.Parse(fileIn.ReadLine());
        //        int numFeatures = int.Parse(fileIn.ReadLine());
        //        string[] columnNames = fileIn.ReadLine().Split(',');
        //        int didx = 0;
        //        for (int c = 1; c < columnNames.Length; c++)
        //        {
        //            Compact<string> domain = _stringIndex.Store(columnNames[c]);
        //            if (!_domainMapping.ContainsKey(domain))
        //            {
        //                _domainMapping[domain] = didx++;
        //            }
        //        }

        //        featureWeights = new float[numDomains][];
        //        for (int c = 0; c < numDomains; c++)
        //        {
        //            featureWeights[c] = new float[numFeatures];
        //        }

        //        int fidx = 0;
        //        while (!fileIn.EndOfStream)
        //        {
        //            string[] parts = fileIn.ReadLine().Split(',');
        //            if (parts.Length < numDomains + 1)
        //                continue;
        //            Compact<string> feature = _stringIndex.Store(parts[0]);
        //            if (!_featureMapping.ContainsKey(feature))
        //            {
        //                _featureMapping[feature] = fidx;
        //            }

        //            for (int c = 1; c < numDomains + 1; c++)
        //            {
        //                featureWeights[c - 1][fidx] = float.Parse(parts[c]);
        //            }
        //            fidx++;
        //        }
        //        fileIn.Close();
        //    }
        //}

        private void WriteCache(VirtualPath cacheFileName)
        {
            // If this model has a single output, we write a different kind of model file that just contains the fixed outcome
            if (singleOutputOverride != null)
            {
                using (NonRealTimeStream writeStream = _fileSystem.OpenStream(cacheFileName, FileOpenMode.Create, FileAccessMode.Write))
                using (Utf8StreamWriter writer = new Utf8StreamWriter(writeStream))
                {
                    writer.Write("OUTCOME\n" + singleOutputOverride);
                }
            }
            else
            {
                using (BinaryWriter fileOut = new BinaryWriter(_fileSystem.OpenStream(cacheFileName, FileOpenMode.Create, FileAccessMode.Write)))
                {
                    fileOut.Write(_domainMapping.Count);
                    fileOut.Write(_featureMapping.Count);
                    foreach (Compact<string> domain in _domainMapping.Keys)
                    {
                        fileOut.Write(_stringIndex.Retrieve(domain));
                    }
                    foreach (Compact<string> feature in _featureMapping.Keys)
                    {
                        fileOut.Write(_stringIndex.Retrieve(feature));
                        foreach (int domain in _domainMapping.Values)
                        {
                            fileOut.Write(featureWeights[domain][_featureMapping[feature]]);
                        }
                    }
                }
            }
        }

        private void LoadCache(VirtualPath cacheFileName)
        {
            // Is it an override file?
            using (Stream readStream = _fileSystem.OpenStream(cacheFileName, FileOpenMode.Open, FileAccessMode.Read))
            {
                byte[] header = new byte[128];
                int read = readStream.Read(header, 0, 11);
                // The first 3 bytes are the UTF BOM (0xEFBBBF) so skip them
                if (read == 11 && "OUTCOME\n".Equals(Encoding.UTF8.GetString(header, 3, 8)))
                {
                    read = readStream.Read(header, 0, 128);
                    useSingleOutputOverride = true;
                    singleOutputOverride = Encoding.UTF8.GetString(header, 0, read);
                    _featureMapping = null;
                    _domainMapping = null;
                    featureWeights = null;
                    return;
                }
            }

            using (BinaryReader fileIn = new BinaryReader(_fileSystem.OpenStream(cacheFileName, FileOpenMode.Open, FileAccessMode.Read)))
            {
                int numDomains = fileIn.ReadInt32();
                int numFeatures = fileIn.ReadInt32();
                int didx = 0;
                for (int c = 0; c < numDomains; c++)
                {
                    Compact<string> domain = _stringIndex.Store(fileIn.ReadString());
                    if (!_domainMapping.ContainsKey(domain))
                    {
                        _domainMapping[domain] = didx++;
                    }
                }
                featureWeights = new float[numDomains][];
                for (int c = 0; c < numDomains; c++)
                {
                    featureWeights[c] = new float[numFeatures];
                }
                int fidx = 0;
                for (int feat = 0; feat < numFeatures; feat++)
                {
                    Compact<string> feature = _stringIndex.Store(fileIn.ReadString());
                    if (!_featureMapping.ContainsKey(feature))
                    {
                        _featureMapping[feature] = fidx++;
                    }

                    foreach (int domain in _domainMapping.Values)
                    {
                        featureWeights[domain][_featureMapping[feature]] = fileIn.ReadSingle();
                    }
                }

                fileIn.Dispose();
            }
        }

        public long GetMemoryUse()
        {
            long returnVal = 0;
            if (useSingleOutputOverride)
            {
                returnVal += Encoding.UTF8.GetByteCount(singleOutputOverride);
            }
            else
            {
                returnVal += _featureMapping.Count * 8L;
                returnVal += _domainMapping.Count * 8L;
                foreach (float[] x in featureWeights)
                {
                    returnVal += (long)x.Length * 4L;
                }
            }
            return returnVal;
        }

        // Returns all classifications for the given input feature set.
        public List<Hypothesis<string>> ClassifyAll(string[] featureSet)
        {
            if (useSingleOutputOverride)
            {
                List<Hypothesis<string>> returnVal = new List<Hypothesis<string>>();
                returnVal.Add(new Hypothesis<string>(singleOutputOverride, 1.0f));
                return returnVal;
            }
            float normalizingFactor = featureSet.Length;
            // Precalculate the hashes for all the features - this speeds up classification
            Compact<string>[] featureHashes = new Compact<string>[featureSet.Length];
            for (int c = 0; c < featureSet.Length; c++)
            {
                featureHashes[c] = _stringIndex.GetIndex(featureSet[c]);
            }
            List<Hypothesis<string>> results = new List<Hypothesis<string>>();
            foreach (Compact<string> domainName in _domainMapping.Keys)
            {
                float likelihood = 0;
                if (useSquaredFeatureWeights)
                {
                    foreach (Compact<string> featureName in featureHashes)
                    {
                        if (_featureMapping.ContainsKey(featureName))
                        {
                            float val = featureWeights[_domainMapping[domainName]][_featureMapping[featureName]];
                            likelihood += val * Math.Abs(val) / normalizingFactor;
                        }
                    }
                }
                else
                {
                    foreach (Compact<string> featureName in featureHashes)
                    {
                        if (_featureMapping.ContainsKey(featureName))
                        {
                            likelihood += (featureWeights[_domainMapping[domainName]][_featureMapping[featureName]] / normalizingFactor);
                        }
                    }
                }
                // Apply sigmoid to the final result - since the usual range is like 0.4 to 0.75, remap that to roughly 0 - 1 confidence
                results.Add(new Hypothesis<string>(_stringIndex.Retrieve(domainName), Sigmoid(likelihood)));
            }

            results.Sort(new Hypothesis<string>.DescendingComparator());

            return results;
        }

        public Hypothesis<string>? Classify(string[] featureSet)
        {
            List<Hypothesis<string>> results = ClassifyAll(featureSet);

            // Pick the top result, if it's there
            if (results.Count == 0)
            {
                return null;
            }

            return results[0];
        }

        private static float[][] Train(float[][] featureCounts, int numDomains, int numFeatures, bool useSqrtFeatCounts)
        {
            float[][] featureWeights = new float[numDomains][];
            for (int c = 0; c < numDomains; c++)
            {
                featureWeights[c] = new float[numFeatures];
            }

            if (useSqrtFeatCounts)
            {
                for (int x = 0; x < numDomains; x++)
                {
                    for (int y = 0; y < numFeatures; y++)
                    {
                        // Find the (magnitude) sqrt of each feature count
                        if (featureCounts[x][y] != 0)
                        {
                            float abs = Math.Abs(featureCounts[x][y]);
                            featureCounts[x][y] *= (float)Math.Sqrt(abs) / abs;
                        }
                    }
                }
            }

            float averageFeatureCount = 0;
            // Normalize the feature weight matrix
            for (int x = 0; x < numDomains; x++)
            {
                for (int y = 0; y < numFeatures; y++)
                {
                    averageFeatureCount += Math.Abs(featureCounts[x][y]);
                }
            }
            averageFeatureCount /= (numDomains * numFeatures);
            float trainingIncrement = 0.01f;
            for (int x = 0; x < numDomains; x++)
            {
                for (int y = 0; y < numFeatures; y++)
                {
                    featureCounts[x][y] = (featureCounts[x][y] / averageFeatureCount) * trainingIncrement;
                }
            }

            // Train
            for (int pass = 0; pass < TRAINING_PASSES; pass++)
            {
                // Apply offset
                for (int x = 0; x < numDomains; x++)
                {
                    for (int y = 0; y < numFeatures; y++)
                    {
                        featureWeights[x][y] += featureCounts[x][y];
                    }
                }
                // Normalize feature count
                for (int y = 0; y < numFeatures; y++)
                {
                    float sum = 0;
                    for (int x = 0; x < numDomains; x++)
                    {
                        sum += Math.Abs(featureWeights[x][y]);
                    }
                    if (sum > 0)
                    {
                        for (int x = 0; x < numDomains; x++)
                        {
                            featureWeights[x][y] /= sum;
                        }
                    }
                }
            }

            return featureWeights;
        }

        /// <summary>
        /// Returns the nearest power-of-two value that is larger than the given value.
        /// ex: "100" returns "128", "4100" returns "8192", etc.
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        /*public static int NPOT(int val)
        {
            double logBase = Math.Log((double)val, 2);
            double upperBound = Math.Ceiling(logBase);
            int nearestPowerOfTwo = (int)Math.Pow(2, upperBound);
            return nearestPowerOfTwo;
        }*/

        public static float Sigmoid(float val)
        {
            return 1.0f - (1.0f / (1.0f + (float)Math.Exp(((val * 9.0f) - 3.5f))));
        }

        /*private static float[] LinearizeBuffer(float[][] input, int width, int height)
        {
            float[] returnVal = new float[width * height];
            int index = 0;
            int x, y;
            for (x = 0; x < width; x++)
            {
                for (y = 0; y < height; y++)
                {
                    returnVal[index++] = input[x][y];
                }
            }
            return returnVal;
        }

        private static float[][] UnlinearizeBuffer(float[] input, int width, int height)
        {
            float[][] returnVal = new float[width][];
            for (int dom = 0; dom < width; dom++)
            {
                returnVal[dom] = new float[height];
            }
            int x = 0;
            int y = 0;
            for (int idx = 0; idx < width * height; idx++)
            {
                returnVal[x][y] = input[idx];
                y += 1;
                if (y >= height)
                {
                    y = 0;
                    x += 1;
                }
            }
            return returnVal;
        }*/
    }
}