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
    public class DomainClassifierTrainer : ModelTrainer
    {
        // Inputs
        private readonly string threadLocal_domain;
        private readonly IList<CrossTrainingRule> threadLocal_crossTrainingRules;
        private readonly IConfiguration threadLocal_domainConfig;
        private readonly ICompactIndex<string> threadLocal_stringIndex;
        private readonly TrainingDataList<DomainIntentContextFeature> threadLocal_negativeTrainingData;
        private readonly TrainingDataManager threadLocal_training;
        private readonly IFileSystem threadLocal_fileSystem;
        private readonly VirtualPath threadLocal_modelDir;
        private readonly ILogger threadLocal_logger;

        // Outputs
        public BinaryClassifier DomainClassifier;
        public ISet<string> RegexOnlyDomains = new HashSet<string>();
        public string ModelDomain;

        private int _disposed = 0;

        public DomainClassifierTrainer(string domain,
            IConfiguration domainConfig,
            ICompactIndex<string> stringIndex,
            TrainingDataList<DomainIntentContextFeature> negativeTrainingData,
            TrainingDataManager training,
            IFileSystem fileSystem,
            VirtualPath modelDir,
            ILogger logger,
            IList<CrossTrainingRule> crossTrainingRules)
        {
            threadLocal_crossTrainingRules = crossTrainingRules;
            threadLocal_domain = domain;
            threadLocal_domainConfig = domainConfig;
            threadLocal_stringIndex = stringIndex;
            threadLocal_negativeTrainingData = negativeTrainingData;
            threadLocal_training = training;
            threadLocal_fileSystem = fileSystem;
            threadLocal_modelDir = modelDir;
            threadLocal_logger = logger;
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

            // Is this domain defined entirely by regexes?
            if (threadLocal_training.GetKnownIntents(threadLocal_domain).Count == 0 &&
                threadLocal_training.GetKnownRegexIntents(threadLocal_domain).Count > 0)
            {
                threadLocal_logger.Log("All intents for domain " + threadLocal_domain + " are defined by regexes; skipping statistical model training...");
                RegexOnlyDomains.Add(threadLocal_domain);
            }
            else
            {
                DomainClassifier = TrainDomainClassifier(positiveTrainingData);
            }
            GC.Collect();
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

        private BinaryClassifier TrainDomainClassifier(TrainingDataList<DomainIntentContextFeature> positiveTrainingData)
        {
            string modelName = "DomainClassifier-" + threadLocal_domain;
            BinaryClassifier returnVal = new BinaryClassifier(
                threadLocal_logger.Clone(modelName),
                threadLocal_fileSystem,
                threadLocal_stringIndex,
                modelName);

            threadLocal_logger.Log("Training domain classifier for " + threadLocal_domain);
            ICrossTrainFilter<DomainIntentContextFeature> _crossTrainFilter = new DomainCrossTrainFilter(threadLocal_domain, threadLocal_crossTrainingRules);

            returnVal.TrainFromData(
                positiveTrainingData,
                threadLocal_negativeTrainingData,
                new VirtualPath(threadLocal_modelDir.FullName + "\\" + threadLocal_domain + ".featureweights"),
                new DomainTrainingEventReaderBalanced(positiveTrainingData, threadLocal_negativeTrainingData, threadLocal_domain, _crossTrainFilter),
                threadLocal_domainConfig);
            return returnVal;
        }
    }
}
