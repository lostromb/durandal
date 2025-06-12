
namespace Durandal.Common.Statistics.Classification
{
    using Durandal.Common.File;
    using Durandal.Common.Logger;
    using Durandal.Common.Statistics.SharpEntropy;
    using Durandal.Common.Statistics.SharpEntropy.IO;
    using Durandal.Common.Collections.Indexing;
    using Durandal.Common.Statistics;
    using Durandal.Common.Utils;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using Durandal.Common.IO;

    public class MaxEntClassifier : IStatisticalClassifier
    {
        private static readonly string FILE_EXT = ".gis";

        private readonly ILogger _logger;
        private readonly ICompactIndex<string> _stringIndex;
        private readonly IFileSystem _fileSystem;
        
        private GisModel model;
        private bool useSingleOutputOverride;
        private string singleOutputOverride;
        private string _modelName;
        
        public MaxEntClassifier(ICompactIndex<string> stringIndex, ILogger logger, IFileSystem fileSystem, string modelName)
        {
            _logger = logger;
            _fileSystem = fileSystem;
            useSingleOutputOverride = false;
            _stringIndex = stringIndex;
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

        public long GetMemoryUse()
        {
            if (useSingleOutputOverride)
            {
                return Encoding.UTF8.GetByteCount(singleOutputOverride);
            }
            else
            {
                long returnVal = model.GetMemoryUse();
                return returnVal;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="trainingData"></param>
        /// <param name="cachedWeightFile"></param>
        /// <param name="modelQuality">Model quality parameter, when 1 is the default</param>
        /// <returns>True if the cache was used</returns>
        public void TrainFromData(ITrainingEventReader trainingData,
            VirtualPath cachedWeightFile,
            float modelQuality = 1.0f)
        {
            Stopwatch timer = new Stopwatch();
            timer.Start();

            if (_fileSystem.Exists(cachedWeightFile + FILE_EXT))
            {
                // Load the cache if possible
                LoadCache(cachedWeightFile + FILE_EXT);
                return;
            }
            
            ITrainingDataIndexer indexer = new TwoPassDataIndexer(trainingData, _fileSystem);

            string[] outcomeList = indexer.GetOutcomeLabels();
            if (outcomeList.Length == 0)
            {
                _logger.Log("GIS classifier \"" + _modelName + "\" is being trained with no output domains! This will not work", LogLevel.Err);
            }
            else if (outcomeList.Length == 1)
            {
                // If there is only 1 possible output classification, this model can just hardwire
                // itself to only return that value.
                // Stop all processing in this case and free a bunch of our memory
                useSingleOutputOverride = true;
                singleOutputOverride = outcomeList[0];
                model = null;

                // Write the override value to the cached model file
                WriteCache(cachedWeightFile + FILE_EXT);
                return;
            }
            else
            {
                GisTrainer trainer = new GisTrainer(_stringIndex);
                trainer.TrainModel((int)(modelQuality * 200), indexer);
                model = new GisModel(trainer);

                _logger.Log("Model trained in " + timer.ElapsedMilliseconds + " ms.", LogLevel.Std);

                WriteCache(cachedWeightFile + FILE_EXT);
            }
        }

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
                //PlainTextGisModelWriter writer = new PlainTextGisModelWriter();
                BinaryGisModelWriter writer = new BinaryGisModelWriter(_stringIndex);
                writer.Persist(model, _fileSystem.OpenStream(cacheFileName, FileOpenMode.Create, FileAccessMode.Write));
            }
        }

        private void LoadCache(VirtualPath cacheFileName)
        {
            // Is it an override file?
            using (Stream readStream = _fileSystem.OpenStream(cacheFileName, FileOpenMode.Open, FileAccessMode.Read))
            {
                byte[] header = new byte[128];
                int read = readStream.Read(header, 0, 3);

                if (read == 3 &&
                    header[0] == 0xEF &&
                    header[1] == 0xBB &&
                    header[2] == 0xBF)
                {
                    // Skip byte order mark if present
                    // BUGBUG this read is not reliable
                    read = readStream.Read(header, 0, 8);
                }
                else
                {
                    read += readStream.Read(header, 3, 5);
                }

                if (read == 8)
                {
                    if ("OUTCOME\n".Equals(Encoding.UTF8.GetString(header, 0, 8)))
                    {
                        read = readStream.Read(header, 0, 128);
                        useSingleOutputOverride = true;
                        singleOutputOverride = Encoding.UTF8.GetString(header, 0, read);
                        model = null;
                        return;
                    }
                }
            }
            
            //PlainTextGisModelReader reader = new PlainTextGisModelReader(cacheFileName);
            BinaryGisModelReader reader = new BinaryGisModelReader(_fileSystem.OpenStream(cacheFileName, FileOpenMode.Open, FileAccessMode.Read), _stringIndex);
            model = new GisModel(reader);
        }

        // Returns all classifications for the given input feature set.
        public List<Hypothesis<string>> ClassifyAll(string[] featureSet)
        {
            List<Hypothesis<string>> results = new List<Hypothesis<string>>();
            if (useSingleOutputOverride)
            {
                results.Add(new Hypothesis<string>(singleOutputOverride, 1.0f));
            }
            else
            {
                float[] probabilities = model.Evaluate(featureSet);
                for (int c = 0; c < probabilities.Length; c++)
                {
                    string outcomeName = model.GetOutcomeName(c);
                    results.Add(new Hypothesis<string>(outcomeName, probabilities[c]));
                }

                results.Sort(new Hypothesis<string>.DescendingComparator());
            }
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
    }
}