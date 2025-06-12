using Durandal.Common.Config;
using Durandal.Common.Config.Accessors;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.LU
{
    /// <summary>
    /// Provides a view over an IConfiguration object that contains options specific to LanguageUnderstandingEngine
    /// </summary>
    public class LUConfiguration
    {
        private readonly WeakPointer<IConfiguration> _internal;
        private readonly ILogger _logger;

        public LUConfiguration(WeakPointer<IConfiguration> container, ILogger logger)
        {
            _internal = container.AssertNonNull(nameof(container));
            _logger = logger;
        }

        public IConfiguration GetBase()
        {
            return _internal.Value;
        }

        /// <summary>
        /// An optional value that sets aside a specific domain as a "sentiment" domain.
        /// Models from this domain will generate sentiments rather than intents.
        /// </summary>
        public string SentimentDomainName
        {
            get
            {
                return _internal.Value.GetString("sentimentDomainName", "");
            }
            set
            {
                _internal.Value.Set("sentimentDomainName", value);
            }
        }

        /// <summary>
        /// The initial set of rules to use for crosstraining between domains.
        /// Training within the same domain is always implied unless a rule explicitly forbids it.
        /// The default rule *:* defines a fully connected crosstrained model across all domains and intents.
        /// </summary>
        public string DefaultCrossTrainingRules
        {
            get
            {
                return _internal.Value.GetString("defaultCrossTrainingRules", "*:*");
            }
            set
            {
                _internal.Value.Set("defaultCrossTrainingRules", value);
            }
        }
        
        /// <summary>
        /// Minimum domain+intent confidence for a model result to be included in the final LU results
        /// </summary>
        public float AbsoluteDomainIntentConfidenceCutoff
        {
            get
            {
                return _internal.Value.GetFloat32("absoluteDomainIntentConfidenceCutoff", 0.75f);
            }
            set
            {
                _internal.Value.Set("absoluteDomainIntentConfidenceCutoff", value);
            }
        }

        public IConfigValue<float> AbsoluteDomainIntentConfidenceCutoff_CreateAccessor()
        {
            return _internal.Value.CreateFloat32Accessor(_logger, "absoluteDomainIntentConfidenceCutoff", 0.75f);
        }

        /// <summary>
        /// Minimum RELATIVE domain+intent confidence for a model result to be included in the final LU results
        /// </summary>
        public float RelativeDomainIntentConfidenceCutoff
        {
            get
            {
                return _internal.Value.GetFloat32("relativeDomainIntentConfidenceCutoff", 0.85f);
            }
            set
            {
                _internal.Value.Set("relativeDomainIntentConfidenceCutoff", value);
            }
        }

        public IConfigValue<float> RelativeDomainIntentConfidenceCutoff_CreateAccessor()
        {
            return _internal.Value.CreateFloat32Accessor(_logger, "relativeDomainIntentConfidenceCutoff", 0.85f);
        }

        /// <summary>
        /// Minimum domain+intent confidence for a crf tagger to be run
        /// </summary>
        public float TaggerRunThreshold
        {
            get
            {
                return _internal.Value.GetFloat32("taggerRunThreshold", 0.45f);
            }
            set
            {
                _internal.Value.Set("taggerRunThreshold", value);
            }
        }

        public IConfigValue<float> TaggerRunThreshold_CreateAccessor()
        {
            return _internal.Value.CreateFloat32Accessor(_logger, "taggerRunThreshold", 0.45f);
        }

        /// <summary>
        /// Minimum confidence, RELATIVE TO most likely confidence, for a crf tagger to keep a tag hypothesis
        /// </summary>
        public float TaggerConfidenceCutoff
        {
            get
            {
                return _internal.Value.GetFloat32("taggerConfidenceCutoff", 0.90f);
            }
            set
            {
                _internal.Value.Set("taggerConfidenceCutoff", value);
            }
        }

        public ISet<string> AnnotatorsToLoad
        {
            get
            {
                return new HashSet<string>(_internal.Value.GetStringList("annotatorsToLoad"));
            }
            set
            {
                _internal.Value.Set("annotatorsToLoad", value);
            }
        }

        /// <summary>
        /// The mechanism to use for storing the language data index. Options are "basic", "compressed", and "file"
        /// </summary>
        public string MemoryPagingScheme
        {
            get
            {
                return _internal.Value.GetString("memoryPagingScheme", "basic");
            }
            set
            {
                _internal.Value.Set("memoryPagingScheme", value);
            }
        }
    }
}
