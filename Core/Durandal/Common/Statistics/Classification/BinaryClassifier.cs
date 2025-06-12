using Durandal.Common.Statistics.SharpEntropy;
using Durandal.Common.Statistics.SharpEntropy.IO;
using Durandal.Common.NLP.Feature;

namespace Durandal.Common.Statistics.Classification
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using Durandal.Common.Config;
    using Durandal.Common.Logger;

    using Durandal.Common.NLP.Train;

    using Durandal.Common.Utils;
    using Durandal.Common.File;
    using Durandal.Common.Collections.Indexing;

    public class BinaryClassifier
    {
        private GisModel model;

        private readonly ILogger _logger;
        private readonly ICompactIndex<string> _stringIndex;
        private readonly IFileSystem _fileSystem;
        private readonly string _modelName;
        private readonly bool _highQuality;

        public BinaryClassifier(ILogger logger, IFileSystem fileSystem, ICompactIndex<string> stringIndex, string modelName, bool maximizeQuality = true)
        {
            this._logger = logger;
            this._stringIndex = stringIndex;
            this._fileSystem = fileSystem;
            this._modelName = modelName;
            this._highQuality = maximizeQuality;
        }

        public static bool IsTrainingRequired(VirtualPath cachedWeightFile, IFileSystem fileSystem, IConfiguration modelConfig)
        {
            if (fileSystem.Exists(cachedWeightFile))
            {
                // Cache exists
                return false;
            }
            if (modelConfig.ContainsKey("regexintents") && !modelConfig.ContainsKey("intents"))
            {
                // This domain model is defined by regexes only, and therefore needs no training
                return false;
            }
            return true;
        }

        //public static bool IsDomainTrainingRequired(VirtualPath cachedWeightFile, IFileSystem fileSystem, Configuration modelConfig)
        //{
        //    if (fileSystem.Exists(cachedWeightFile))
        //    {
        //        // Cache exists
        //        return false;
        //    }
        //    if (modelConfig.ContainsKey("regexintents") && !modelConfig.ContainsKey("intents"))
        //    {
        //        // This domain model is defined by regexes only, and therefore needs no training
        //        return false;
        //    }

        //    return true;
        //}

        //public static bool IsIntentTrainingRequired(VirtualPath cachedWeightFile, IFileSystem fileSystem, Configuration modelConfig, string intentName)
        //{
        //    if (fileSystem.Exists(cachedWeightFile))
        //    {
        //        // Cache exists
        //        return false;
        //    }
        //    if (modelConfig.ContainsKey("regexintents") && modelConfig.GetVectorString("regexintents").Contains(intentName))
        //    {
        //        // This intent model is defined by regexes only, and therefore needs no training
        //        return false;
        //    }

        //    return true;
        //}

        public void TrainFromData(
            TrainingDataList<DomainIntentContextFeature> positiveEvents,
            TrainingDataList<DomainIntentContextFeature> negativeEvents,
            VirtualPath cachedWeightFile,
            ITrainingEventReader eventReader,
            IConfiguration modelConfig)
        {
            Stopwatch timer = new Stopwatch();
            timer.Start();

            if (!IsTrainingRequired(cachedWeightFile, _fileSystem, modelConfig))
            {
                // Load the cache if possible
                this.LoadCache(cachedWeightFile);
                timer.Stop();
            }
            else if (positiveEvents == null || positiveEvents.TrainingData.Count == 0)
            {
                _logger.Log("No training data could be found to train model! (Are your training files in the right directory?)", LogLevel.Err);
            }
            else
            {
                if (negativeEvents == null || negativeEvents.TrainingData.Count == 0)
                {
                    _logger.Log("No negative training data was found for this model. This usually means your scenario only contains 1 domain. The resulting model will fire for all queries. (If this warning persists, try flushing your cache)", LogLevel.Wrn);
                }

                if (_highQuality)
                {
                    GisTrainer trainer = new GisTrainer(this._stringIndex);
                    //trainer.UseSlackParameter = true;
                    //trainer.Smoothing = true;
                    //trainer.SmoothingObservation = 0.1;
                    ITrainingDataIndexer indexer = new TwoPassDataIndexer(eventReader, _fileSystem);
                    trainer.TrainModel(100, indexer);
                    this.model = new GisModel(trainer);
                }
                else
                {
                    GisTrainer trainer = new GisTrainer(this._stringIndex);
                    ITrainingDataIndexer indexer = new OnePassDataIndexer(eventReader);
                    trainer.TrainModel(50, indexer);
                    this.model = new GisModel(trainer);
                }

                if (this.model.OutcomeCount == 1)
                {
                    _logger.Log("The model \"" + _modelName + "\" is degenerate (only 1 outcome)", LogLevel.Vrb);
                }

                timer.Stop();
                this._logger.Log("Model trained in " + timer.ElapsedMilliseconds + " ms.", LogLevel.Std);

                this.WriteCache(cachedWeightFile);
            }

        }

        public long GetMemoryUse()
        {
            return this.model == null ? 0 : this.model.GetMemoryUse();
        }

        private void WriteCache(VirtualPath cacheFileName)
        {
            //PlainTextGisModelWriter writer = new PlainTextGisModelWriter();
            if (this.model != null)
            {
                BinaryGisModelWriter writer = new BinaryGisModelWriter(this._stringIndex);
                using (Stream writeStream = _fileSystem.OpenStream(cacheFileName, FileOpenMode.Create, FileAccessMode.Write))
                {
                    writer.Persist(this.model, writeStream);
                }
            }
        }

        private void LoadCache(VirtualPath cacheFileName)
        {
            //PlainTextGisModelReader reader = new PlainTextGisModelReader(cacheFileName);
            using (Stream readStream = _fileSystem.OpenStream(cacheFileName, FileOpenMode.Open, FileAccessMode.Read))
            {
                BinaryGisModelReader reader = new BinaryGisModelReader(readStream, this._stringIndex);
                this.model = new GisModel(reader);
            }
        }

        /// <summary>
        /// Returns the probability that the outcome is TRUE
        /// </summary>
        /// <param name="featureSet"></param>
        /// <returns></returns>
        public float Classify(string[] featureSet)
        {
            // Is the positive outcome even possible?
            /*int idx = this.model.GetOutcomeIndex("1");
            if (idx < 0)
            {
                return 0.0f;
            }*/

            if (this.model == null)
            {
                _logger.Log("The model \"" + this._modelName + "\" was initialized, but the model is null!", LogLevel.Wrn);
                return 0.0f;
            }

            // Add all features to the context and evaluate
            float[] probabilities = this.model.Evaluate(featureSet);
            return probabilities[this.model.GetOutcomeIndex("1")];
        }
    }
}
