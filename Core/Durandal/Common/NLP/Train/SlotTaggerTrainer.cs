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
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.NLP.Train
{
    public class SlotTaggerTrainer : ModelTrainer
    {
        // Inputs
        private readonly string threadLocal_domain;
        private readonly WeakPointer<IConfiguration> threadLocal_domainConfig;
        private readonly WeakPointer<ICompactIndex<string>> threadLocal_stringIndex;
        private readonly TrainingDataManager threadLocal_training;
        private readonly IFileSystem threadLocal_fileSystem;
        private readonly IWordBreaker threadLocal_wordBreaker;
        private readonly VirtualPath threadLocal_modelDir;
        private readonly ILogger threadLocal_logger;
        private readonly IStatisticalTrainer threadLocal_trainer;
        private readonly float threadLocal_taggerConfidenceCutoff;

        // Outputs
        public Dictionary<string, CRFTagger> SlotTaggers;
        public string ModelDomain;

        private int _disposed = 0;

        public SlotTaggerTrainer(string domain,
            WeakPointer<IConfiguration> domainConfig,
            WeakPointer<ICompactIndex<string>> stringIndex,
            TrainingDataList<DomainIntentContextFeature> negativeTrainingData,
            TrainingDataManager training,
            IWordBreaker wordBreaker,
            IFileSystem fileSystem,
            VirtualPath modelDir,
            ILogger logger,
            IStatisticalTrainer trainer,
            float taggerConfidenceCutoff)
        {
            threadLocal_domain = domain;
            threadLocal_domainConfig = domainConfig;
            threadLocal_stringIndex = stringIndex;
            threadLocal_training = training;
            threadLocal_fileSystem = fileSystem;
            threadLocal_modelDir = modelDir;
            threadLocal_logger = logger;
            threadLocal_taggerConfidenceCutoff = taggerConfidenceCutoff;
            threadLocal_wordBreaker = wordBreaker;
            ModelDomain = domain;
            threadLocal_trainer = trainer;
        }

// Leak tracing destructor already implemented in base class
//#if TRACK_IDISPOSABLE_LEAKS
//        ~SlotTaggerTrainer()
//        {
//            Dispose(false);
//        }
//#endif

        public void Run()
        {
            SlotTaggers = TrainSlotTaggers();

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

        private Dictionary<string, CRFTagger> TrainSlotTaggers()
        {
            Dictionary<string, CRFTagger> returnVal = new Dictionary<string, CRFTagger>();

            foreach (string intent in threadLocal_training.GetKnownIntents(threadLocal_domain))
            {
                DomainIntent domainIntent = new DomainIntent(threadLocal_domain, intent);

                // Are there slots for this intent, and by extension, a need for a CRF model?
                if (threadLocal_training.GetKnownTags(threadLocal_domain, intent).Count > 1)
                {
                    VirtualPath trainingFileName = threadLocal_training.GetTagFeaturesFile(threadLocal_domain, intent);

                    returnVal[domainIntent.ToString()] = new CRFTagger(
                        threadLocal_trainer,
                        threadLocal_logger.Clone("CRFTagger-" + domainIntent),
                        threadLocal_taggerConfidenceCutoff,
                        threadLocal_fileSystem,
                        threadLocal_stringIndex,
                        threadLocal_wordBreaker);
                    threadLocal_logger.Log("Training taggers for " + domainIntent);

                    returnVal[domainIntent.ToString()].TrainFromData(
                        trainingFileName,
                        threadLocal_domain,
                        intent,
                        threadLocal_modelDir.Combine(threadLocal_domain + " " + intent),
                        threadLocal_domainConfig.Value);
                }
            }

            return returnVal;
        }
    }
}
