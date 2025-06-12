using Durandal.Common.Statistics.Classification;
using Durandal.Common.NLP.Feature;
using Durandal.Common.NLP.Tagging;
using Durandal.Common.NLP.Train;
using Durandal.Common.Config;
using Durandal.Common.Logger;
using Durandal.Common.Collections.Indexing;
using Durandal.Common.File;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Utils;

namespace Durandal.Common.NLP.Train
{
    public class IntentClassifierTrainer : ModelTrainer
    {
        // Inputs
        private readonly string threadLocal_domain;
        private readonly IConfiguration threadLocal_domainConfig;
        private readonly ICompactIndex<string> threadLocal_stringIndex;
        private readonly IList<CrossTrainingRule> threadLocal_crossTrainingRules;
        private readonly TrainingDataList<DomainIntentContextFeature> threadLocal_negativeTrainingData;
        private readonly TrainingDataManager threadLocal_training;
        private readonly IFileSystem threadLocal_fileSystem;
        private readonly VirtualPath threadLocal_modelDir;
        private readonly ILogger threadLocal_logger;

        // Outputs
        public Dictionary<string, BinaryClassifier> IntentClassifiers;
        public string ModelDomain;

        private int _disposed = 0;

        public IntentClassifierTrainer(string domain,
            IConfiguration domainConfig,
            ICompactIndex<string> stringIndex,
            TrainingDataList<DomainIntentContextFeature> negativeTrainingData,
            TrainingDataManager training,
            IFileSystem fileSystem,
            VirtualPath modelDir,
            ILogger logger,
            IList<CrossTrainingRule> crossTrainingRules)
        {
            threadLocal_domain = domain;
            threadLocal_domainConfig = domainConfig;
            threadLocal_stringIndex = stringIndex;
            threadLocal_negativeTrainingData = negativeTrainingData;
            threadLocal_training = training;
            threadLocal_fileSystem = fileSystem;
            threadLocal_modelDir = modelDir;
            threadLocal_logger = logger;
            threadLocal_crossTrainingRules = crossTrainingRules;
            ModelDomain = domain;
        }

        public void Run()
        {
            TrainingDataList<DomainIntentContextFeature> positiveTrainingData = new TrainingDataList<DomainIntentContextFeature>(DomainIntentContextFeature.CreateStatic);

            // See if we need to build the training corpus at all
            bool trainingDataNeeded = BinaryClassifier.IsTrainingRequired(
                new VirtualPath(threadLocal_modelDir.FullName + "\\" + threadLocal_domain + ".featureweights"),
                threadLocal_fileSystem,
                threadLocal_domainConfig);

            foreach (string intent in threadLocal_training.GetKnownIntents(threadLocal_domain))
            {
                if (BinaryClassifier.IsTrainingRequired(
                    new VirtualPath(threadLocal_modelDir.FullName + "\\" + threadLocal_domain + " " + intent + ".featureweights"),
                    threadLocal_fileSystem,
                    threadLocal_domainConfig))
                {
                    trainingDataNeeded = true;
                }
            }

            if (trainingDataNeeded)
            {
                foreach (string intent in threadLocal_training.GetKnownIntents(threadLocal_domain))
                {
                    VirtualPath positiveTrainingFile = threadLocal_training.GetDomainIntentFeaturesFile(threadLocal_domain, intent);
                    if (threadLocal_fileSystem.Exists(positiveTrainingFile))
                    {
                        TrainingDataList<DomainIntentContextFeature> moreTrainingData =
                            new TrainingDataList<DomainIntentContextFeature>(positiveTrainingFile, threadLocal_fileSystem, threadLocal_logger, DomainIntentContextFeature.CreateStatic);
                        positiveTrainingData.Append(moreTrainingData);
                    }
                }
            }

            IntentClassifiers = TrainIntentClassifiers(positiveTrainingData);

            Done();
        }

        protected override void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            try
            {
                if (disposing)
                {
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        private Dictionary<string, BinaryClassifier> TrainIntentClassifiers(TrainingDataList<DomainIntentContextFeature> positiveTrainingData)
        {
            Dictionary<string, BinaryClassifier> returnVal = new Dictionary<string, BinaryClassifier>();

            foreach (string intent in threadLocal_training.GetKnownIntents(threadLocal_domain))
            {
                DomainIntent domainIntent = new DomainIntent(threadLocal_domain, intent);

                threadLocal_logger.Log("Training intent classifier for " + domainIntent);
                string modelName = "IntentClassifier-" + domainIntent;
                returnVal[intent] = new BinaryClassifier(
                    threadLocal_logger.Clone(modelName),
                    threadLocal_fileSystem,
                    threadLocal_stringIndex,
                    modelName);

                ICrossTrainFilter<DomainIntentContextFeature> _crossTrainFilter = new IntentCrossTrainFilter(threadLocal_domain, intent, threadLocal_crossTrainingRules);

                returnVal[intent].TrainFromData(
                    positiveTrainingData,
                    threadLocal_negativeTrainingData,
                    new VirtualPath(threadLocal_modelDir.FullName + "\\" +
                                    threadLocal_domain + " " + intent +
                                    ".featureweights"),
                                    new IntentTrainingEventReaderBalanced(positiveTrainingData, threadLocal_negativeTrainingData, threadLocal_domain, intent, _crossTrainFilter),
                                    threadLocal_domainConfig);
            }
            return returnVal;
        }
    }
}
