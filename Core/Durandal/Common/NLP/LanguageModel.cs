
namespace Durandal.Common.NLP
{
    using Durandal.API;
        using Durandal.Common.Config;
    using Durandal.Common.Dialog;
    using Durandal.Common.File;
    using Durandal.Common.Logger;
    using Durandal.Common.LU;
    using Durandal.Common.Statistics.Classification;
    using Durandal.Common.NLP.Feature;
    using Durandal.Common.Collections.Indexing;
    using Durandal.Common.Statistics.Ranking;
    using Durandal.Common.NLP.Tagging;
    using Durandal.Common.NLP.Train;
    using Durandal.Common.Utils;
    using Durandal.Common.IO;
    using Durandal.Common.MathExt;
    using Durandal.Common.Statistics;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using Durandal.Common.Config.Accessors;
    using Durandal.Common.Collections;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.ServiceMgmt;


    /// <summary>
    /// This class represents a classifier and tagger that spans many domains and one language.
    /// </summary>
    public class LanguageModel : IDisposable
    {
        // Set of all answer domains in this model
        private readonly Durandal.Common.Collections.IReadOnlySet<string> _domains;

        // The model's locale
        private readonly LanguageCode _locale;

        // The master string compaction index used for the entire model
        // This object will share and compact memory in-place to reduce LM memory usage substantially
        private ICompactIndex<string> _masterStringIndex;

        private readonly VirtualPath _modelDir;
        private readonly VirtualPath _cacheDir;
        private readonly TrainingDataManager _training;
        private readonly IFileSystem _fileSystem;
        private readonly LUConfiguration _config;
        private readonly ILogger _logger;
        private WeakPointer<IThreadPool> _threadPool;

        private readonly Dictionary<string, BinaryClassifier> _domainClassifiers;
        private readonly Dictionary<string, Dictionary<string, BinaryClassifier>> _intentClassifiers;
        private readonly Dictionary<string, CRFTagger> _tagClassifiers;
        private readonly Dictionary<string, RegexClassifier> _regexClassifiers;
        private readonly ISet<string> _regexOnlyDomains;
        private readonly NLPTools _languageTools;
        private IReranker _clientDefinedReranker;
        private IReranker _statisticalRanker;
        private ISet<string> _sentimentDomains;
        private IStatisticalTrainer _crfTrainer;
        private int _disposed = 0;

        /// <summary>
        /// If the combined confidence is lower than this, the result will be culled
        /// </summary>
        private readonly IConfigValue<float> _absoluteDomainIntentConfidenceCutoff;

        /// <summary>
        /// If the combined confidence is lower than (highest confidence * this), the result will be culled
        /// </summary>
        private readonly IConfigValue<float> _relativeDomainIntentConfidenceCutoff;

        /// <summary>
        /// If the combined confidence is lower than this, the tagger will not run
        /// </summary>
        private readonly IConfigValue<float> _taggerConfidenceCutoff;

        public LanguageModel(
            LUConfiguration config,
            ILogger logger,
            IFileSystem fileSystem,
            IEnumerable<string> domains,
            LanguageCode locale,
            TrainingDataManager trainingProvider,
            NLPTools languageTools,
            IReranker reranker,
            IThreadPool threadPool)
        {
            _config = config;
            _training = trainingProvider;
            _logger = logger;
            _fileSystem = fileSystem;
            _locale = locale;
            _absoluteDomainIntentConfidenceCutoff = _config.AbsoluteDomainIntentConfidenceCutoff_CreateAccessor();
            _relativeDomainIntentConfidenceCutoff = _config.RelativeDomainIntentConfidenceCutoff_CreateAccessor();
            _taggerConfidenceCutoff = _config.TaggerRunThreshold_CreateAccessor();
            _clientDefinedReranker = reranker;
            _threadPool = new WeakPointer<IThreadPool>(threadPool);

            _languageTools = languageTools;

            _modelDir = new VirtualPath(RuntimeDirectoryName.MODEL_DIR + "\\" + _locale);
            _cacheDir = new VirtualPath(RuntimeDirectoryName.CACHE_DIR + "\\" + _locale);

            _domainClassifiers = new Dictionary<string, BinaryClassifier>();
            _intentClassifiers = new Dictionary<string, Dictionary<string, BinaryClassifier>>();
            _tagClassifiers = new Dictionary<string, CRFTagger>();
            _regexClassifiers = new Dictionary<string, RegexClassifier>();
            _regexOnlyDomains = new HashSet<string>();
            _sentimentDomains = new HashSet<string>();

            _domains = new ReadOnlySetWrapper<string>(new HashSet<string>(domains));

            _crfTrainer = new MaxEntClassifierTrainer(_logger.Clone("CRFTrainer"), _fileSystem);
            //_crfTrainer = new NeuralModelTrainer(_logger.Clone("NeuralTrainer"));

            // Make sure the feature data is in order
            _training.FeaturizeTrainingData(_languageTools, _fileSystem);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~LanguageModel()
        {
            Dispose(false);
        }
#endif

        private TrainingDataList<DomainIntentContextFeature> CreateCombinedDomainTrainingFile(float fractionToUse)
        {
            VirtualPath fileName = _cacheDir.Combine("all.domainfeatures");
            if (_fileSystem.Exists(fileName))
            {
                // Load from cache
                return new TrainingDataList<DomainIntentContextFeature>(fileName, _fileSystem, _logger, DomainIntentContextFeature.CreateStatic);
            }

            TrainingDataList<DomainIntentContextFeature> returnVal = new TrainingDataList<DomainIntentContextFeature>(DomainIntentContextFeature.CreateStatic);
            float counter = 0;
            foreach (string domain in _domains)
            {
                foreach (string intent in _training.GetKnownIntents(domain))
                {
                    VirtualPath oneDomainFile = _training.GetDomainIntentFeaturesFile(domain, intent);
                    if (_fileSystem.Exists(oneDomainFile))
                    {
                        using (StreamReader reader = new StreamReader(_fileSystem.OpenStream(oneDomainFile, FileOpenMode.Open, FileAccessMode.Read)))
                        {
                            while (!reader.EndOfStream)
                            {
                                string line = reader.ReadLine();
                                counter += fractionToUse;
                                while (counter > 1)
                                {
                                    DomainIntentContextFeature newFeature = new DomainIntentContextFeature(line);
                                    returnVal.Append(newFeature);
                                    counter -= 1;
                                }
                            }
                        }
                    }
                }
            }

            // Cache the file for later
            returnVal.SaveToFile(fileName, _fileSystem);
            return returnVal;
        }

        public Durandal.Common.Collections.IReadOnlySet<string> Domains
        {
            get { return _domains; }
        }

        public TrainingDataManager TrainingProvider
        {
            get { return _training; }
        }

        private bool IsTrainingRequired(IRealTimeProvider realTime)
        {
            foreach (string domain in _domains)
            {
                IConfiguration domainConfig = _training.GetDomainConfiguration(domain, realTime);

                if (BinaryClassifier.IsTrainingRequired(
                        new VirtualPath(_modelDir.FullName + "\\" + domain + ".featureweights"),
                        _fileSystem,
                        domainConfig))
                {
                    return true;
                }

                foreach (string intent in _training.GetKnownIntents(domain))
                {
                    if (BinaryClassifier.IsTrainingRequired(
                        new VirtualPath(_modelDir.FullName + "\\" + domain + " " + intent + ".featureweights"),
                        _fileSystem,
                        domainConfig))
                    {
                        return true;
                    }

                    if (domainConfig.ContainsKey("alltags") && domainConfig.GetStringList("alltags").Count > 1)
                    {
                        // Tag training data exists, check if the tagger needs training
                        if (CRFTagger.IsTrainingRequired(_crfTrainer, _modelDir.Combine(domain + " " + intent), _fileSystem, domainConfig, intent, _logger))
                        {
                            return true;
                        }
                    }
                }
            }
            /*if (LinearConstraintReranker.IsTrainingRequired(_cacheDir, _fileSystem))
            {
                return true;
            }*/
            return false;
        }

        public void Train(IRealTimeProvider realTime)
        {
            _logger.Log("Determining if training is required...");
            // Determine if there's anything to train
            bool trainingRequired = this.IsTrainingRequired(realTime);
            if (trainingRequired)
            {
                // This type of index performs better when training new models,
                // plus it handles multithreaded training
                _masterStringIndex = ConcurrentCompactIndex<string>.BuildStringIndex();
                _logger.Log("Training required = TRUE");
            }
            else
            {
                string memoryPagingScheme = _config.MemoryPagingScheme;
                if (string.IsNullOrEmpty(memoryPagingScheme))
                {
                    memoryPagingScheme = "basic";
                }

                if (string.Equals(memoryPagingScheme, "basic", StringComparison.OrdinalIgnoreCase))
                {
                    // todo: can I remove the concurrency on this index once it is read-only?
                    _masterStringIndex = ConcurrentCompactIndex<string>.BuildStringIndex();
                }
                else if (string.Equals(memoryPagingScheme, "compressed", StringComparison.OrdinalIgnoreCase))
                {
                    // This type of index performs better when loading cached models, and uses in-place memory compression
                    _masterStringIndex = new BlockTransformCompactIndex<string>(new StringByteConverter(), new LZ4CompressedMemoryPageStorage(true, 100), 5000, 0);
                }
                else if (string.Equals(memoryPagingScheme, "file", StringComparison.OrdinalIgnoreCase))
                {
#pragma warning disable CA2000 // Dispose objects before losing scope
                    _masterStringIndex = new BlockTransformCompactIndex<string>(
                        new StringByteConverter(),
                        new PagingFilePageStorage(
                            _fileSystem,
                            new VirtualPath(RuntimeDirectoryName.CACHE_DIR + "\\pagefile.dat"),
                            32768),
                        32768,
                        10);
#pragma warning restore CA2000 // Dispose objects before losing scope
                }

                _logger.Log("Training required = FALSE");
            }

            Stopwatch trainingTimer = new Stopwatch();
            trainingTimer.Start();

            TrainStatisticalModels(_masterStringIndex, realTime);

            //TrainStatisticalRanker(_masterStringIndex);

            trainingTimer.Stop();
            _logger.Log("Total training time was " + trainingTimer.ElapsedMilliseconds + " ms.", LogLevel.Std);
            _logger.Log("LM memory usage is reported as " + (GetMemoryUse() / 1024) + " Kb", LogLevel.Std);
            _logger.Log("Total CLR memory usage is reported as " + (GC.GetTotalMemory(false) / 1024) + " Kb", LogLevel.Std);
        }

        public long GetMemoryUse()
        {
            long returnVal = _masterStringIndex == null ? 0 : _masterStringIndex.MemoryUse;
            foreach (CRFTagger t in this._tagClassifiers.Values)
            {
                returnVal += t.GetMemoryUse();
            }
            foreach (string domain in _domains)
            {
                returnVal += Encoding.UTF8.GetByteCount(domain);
                if (_domainClassifiers.ContainsKey(domain))
                {
                    returnVal += _domainClassifiers[domain].GetMemoryUse();
                }
                if (_intentClassifiers.ContainsKey(domain))
                {
                    foreach (var intentModel in this._intentClassifiers[domain].Values)
                    {
                        returnVal += intentModel.GetMemoryUse();
                    }
                }
            }
            if (_languageTools != null)
            {
                returnVal += _languageTools.GetMemoryUse();
            }
            return returnVal;
        }

        public RecognizedPhrase Classify(
            LUInput utterance,
            bool wasSpeechInput,
            ILogger queryLogger,
            IRealTimeProvider realTime,
            ISet<string> domainScope = null,
            ISet<string> contextualDomains = null)
        {
            Sentence wordBrokenUtterance = _languageTools.FeaturizationWordBreaker.Break(utterance);

            List<RecoResult> recoResults = new List<RecoResult>();

            RecognizedPhrase returnVal = new RecognizedPhrase()
            {
                Utterance = utterance.Utterance,
                Sentiments = new Dictionary<string, float>()
            };

            foreach (string domain in _domainClassifiers.Keys)
            {
                // Apply domain scoping
                if (domainScope != null && domainScope.Count > 0 && !domainScope.Contains(domain))
                    continue;
                
                IConfiguration domainConfig = _training.GetDomainConfiguration(domain, realTime);
                ISet<string> multiturnIntents = domainConfig.ContainsKey("MultiturnIntents") ? new HashSet<string>(domainConfig.GetStringList("MultiturnIntents")) : new HashSet<string>();

                IList<RecoResult> oneResult = ClassifyOne(domain, wordBrokenUtterance, wasSpeechInput, queryLogger, multiturnIntents, contextualDomains);
                if (oneResult != null)
                {
                    // Was it a regular domain or a sentiment domain?
                    if (!_sentimentDomains.Contains(domain))
                    {
                        recoResults.FastAddRangeList(oneResult);
                    }
                    else
                    {
                        foreach (RecoResult r in oneResult)
                        {
                            returnVal.Sentiments.Add(r.Domain + "/" + r.Intent, r.Confidence);
                        }
                    }
                }
            }

            foreach (string regexDomain in _regexOnlyDomains)
            {
                if (domainScope != null && domainScope.Count > 0 && !domainScope.Contains(regexDomain))
                    continue;

                IConfiguration domainConfig = _training.GetDomainConfiguration(regexDomain, realTime);
                ISet<string> multiturnIntents = domainConfig.ContainsKey("MultiturnIntents") ? new HashSet<string>(domainConfig.GetStringList("MultiturnIntents")) : new HashSet<string>();

                IList<RecoResult> oneResult = ClassifyOneUsingRegexes(regexDomain, wordBrokenUtterance, wasSpeechInput, queryLogger, multiturnIntents, contextualDomains);
                if (oneResult != null)
                {
                    if (!_sentimentDomains.Contains(regexDomain))
                    {
                        recoResults.FastAddRangeList(oneResult);
                    }
                    else
                    {
                        foreach (RecoResult r in oneResult)
                        {
                            returnVal.Sentiments.Add(r.Domain + "/" + r.Intent, r.Confidence);
                        }
                    }
                }
            }

            if (recoResults.Count > 0)
            {
                // Catch any invalid confidences
                foreach (RecoResult r in recoResults)
                {
                    if (float.IsNaN(r.Confidence))
                    {
                        r.Confidence = 0.0f;
                    }
                    else if (float.IsInfinity(r.Confidence))
                    {
                        r.Confidence = 1.0f;
                    }
                }

                // Run statistical ranking
                if (this._statisticalRanker != null)
                {
                    this._statisticalRanker.Rerank(ref recoResults);
                }

                // Sort by default confidence
                recoResults.Sort();

                // And then the client-defined ranker, if any
                if (this._clientDefinedReranker != null)
                {
                    this._clientDefinedReranker.Rerank(ref recoResults);
                }

                float highestConf = 0.0f;
                foreach (RecoResult r in recoResults)
                {
                    if (!DialogConstants.COMMON_DOMAIN.Equals(r.Domain, StringComparison.OrdinalIgnoreCase))
                    {
                        highestConf = Math.Max(highestConf, r.Confidence);
                    }
                }

                if ((queryLogger.ValidLogLevels | LogLevel.Vrb) != 0)
                {
                    queryLogger.Log("Raw LU results:", LogLevel.Vrb);
                    foreach (RecoResult r in recoResults)
                    {
                        queryLogger.Log(r.Domain + "/" + r.Intent + ": " + r.Confidence, LogLevel.Vrb);
                    }
                }

                // Apply cutoff to the post-ranking results
                List<RecoResult> culledSortedResults = new List<RecoResult>();
                foreach (RecoResult r in recoResults)
                {
                    if (r.Confidence > highestConf * _relativeDomainIntentConfidenceCutoff.Value &&
                        r.Confidence > _absoluteDomainIntentConfidenceCutoff.Value)
                    {
                        culledSortedResults.Add(r);
                    }
                }

                recoResults = culledSortedResults;

                // FIXME why do we sort the list again after reranking? Doesn't that nullify whatever custom ranking just happened?
                recoResults.Sort();
            }

            returnVal.Recognition = recoResults;

            return returnVal;
        }

        private delegate IList<RecoResult> RunClassifier(string domain, Sentence utterance, bool wasSpeechInput, ILogger queryLogger);

        /// <summary>
        /// Runs all classifiers within a single domain on a thread
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="utterance"></param>
        /// <param name="wasSpeechInput"></param>
        /// <param name="queryLogger"></param>
        /// <param name="multiturnIntents">Domains that are relevant and for which we should allow triggering multiturn-only intents</param>
        /// <param name="contextualDomains">Domains that have valid entries in the conversation stack</param>
        /// <returns></returns>
        private IList<RecoResult> ClassifyOne(
            string domain,
            Sentence utterance,
            bool wasSpeechInput,
            ILogger queryLogger,
            ISet<string> multiturnIntents,
            ISet<string> contextualDomains)
        {
            // Classify its domain and intent
            string[] domainFeatures = _languageTools.DomainFeaturizer.ExtractFeatures(utterance);

            Hypothesis<string> domainConfidence = new Hypothesis<string>(domain, _domainClassifiers[domain].Classify(domainFeatures));

            IList<RecoResult> finalResults = new List<RecoResult>();

            bool isSentimentDomain = _sentimentDomains.Contains(domain);

            if (domainConfidence.Conf > 0.0001 || isSentimentDomain)
            {
                int intentCount = _intentClassifiers[domain].Count;
                
                // Run through all the intents in this domain and classify them all
                foreach (string intent in _intentClassifiers[domain].Keys)
                {
                    // Apply contextual domain scoping here (filter out multiturn-only intents that are not part of the current context)
                    if (contextualDomains != null &&
                        multiturnIntents.Contains(intent) &&
                        !contextualDomains.Contains(domain))
                    {
                        continue;
                    }

                    float combinedConfidence = domainConfidence.Conf;
                    Hypothesis<string> intentConfidence = new Hypothesis<string>(intent, combinedConfidence);

                    if (intentCount > 1)
                    {
                        intentConfidence.Conf = _intentClassifiers[domain][intent].Classify(domainFeatures);
                    }
                    else
                    {
                        // If the intent model is degenerate (i.e. only one possible outcome) then don't even bother running the intent model.
                        intentConfidence.Conf = 1.0f;
                    }

                    combinedConfidence = CombineConfidences(domainConfidence.Conf, intentConfidence.Conf);

                    // Skip low-quality results at this point
                    // FIXME Magic numbers being user as thresholds here
                    if (!isSentimentDomain &&
                        (combinedConfidence < 0.0001f ||
                        intentConfidence.Conf < 0.01f))
                    {
                        continue;
                    }

                    // Cap the domain confidence (somewhat artificially) to 0.999.
                    // The reason is that regexes return a score between 0.999 and 1.0 and they should always take precedence over statistical results
                    combinedConfidence = Math.Min(combinedConfidence, 0.999f);

                    queryLogger.Log("Combined conf " + domain + ":" + domainConfidence.Conf + " + " + intent + ":" + intentConfidence.Conf + " = " + combinedConfidence, LogLevel.Vrb);

                    IList<TaggedData> allTagHyps;
                    DomainIntent domainAndIntent = new DomainIntent(domain, intent);

                    if (!isSentimentDomain &&
                        _tagClassifiers.ContainsKey(domainAndIntent.ToString()) &&
                        combinedConfidence > _taggerConfidenceCutoff.Value)
                    {
                        // Run the POS tagger
                        CRFTagger thisDomainTagger = this._tagClassifiers[domainAndIntent.ToString()];
                        allTagHyps = thisDomainTagger.Classify(utterance, _languageTools.TagFeaturizer, wasSpeechInput);

                        if (allTagHyps == null)
                        {
                            queryLogger.Log("CRF tagger returned null! Recovering by using stub value...", LogLevel.Err);
                            allTagHyps = new List<TaggedData>();
                            allTagHyps.Add(new TaggedData()
                            {
                                Utterance = utterance.OriginalText,
                                Confidence = 0.0f
                            });
                        }

                        // Remove slots with empty values
                        allTagHyps = CullEmptySlots(domainAndIntent, allTagHyps, queryLogger);
                    }
                    else
                    {
                        // Create a TaggedData instance with no tags, if no tagger is needed
                        allTagHyps = new List<TaggedData>();
                        allTagHyps.Add(new TaggedData
                        {
                            Utterance = utterance.OriginalText,
                            Confidence = 1.0f // No tags are even possible, so we can say that tag accuracy is 100%
                        });
                    }

                    // Create one final reco result for every tag hypothesis from CRF, for this intent
                    RecoResult newRecoResult = new RecoResult()
                    {
                        Domain = domain,
                        Intent = intent,
                        Confidence = combinedConfidence,
                        Utterance = utterance,
                        Source = "StatisticalModel"
                    };

                    newRecoResult.TagHyps.FastAddRangeList(allTagHyps);
                    finalResults.Add(newRecoResult);
                }
            }

            // Get regex whitelist results
            IList<RecoResult> regexResults = ClassifyOneUsingRegexes(domain, utterance, wasSpeechInput, queryLogger, multiturnIntents, contextualDomains);

            // Add the regex results to the final output, overriding any existing results
            foreach (var regexResult in regexResults)
            {
                finalResults = RemoveRecoResultIfAlreadyPresent(finalResults, regexResult.Domain, regexResult.Intent);
                finalResults.Add(regexResult);
            }

            // Now run the regex blacklist
            foreach (string regexIntent in _training.GetKnownRegexIntents(domain))
            {
                DomainIntent domainAndIntent = new DomainIntent(domain, regexIntent);
                finalResults = _regexClassifiers[domainAndIntent.ToString()].ApplyBlacklist(finalResults, utterance, wasSpeechInput, queryLogger);
            }

            return finalResults;
        }

        private IList<RecoResult> ClassifyOneUsingRegexes(string domain,
            Sentence utterance,
            bool wasSpeechInput,
            ILogger queryLogger,
            ISet<string> multiturnIntents,
            ISet<string> contextualDomains)
        {
            IList<RecoResult> returnVal = new List<RecoResult>();

            // Run all regex rules for this domain, if applicable
            foreach (string regexIntent in _training.GetKnownRegexIntents(domain))
            {
                DomainIntent domainAndIntent = new DomainIntent(domain, regexIntent);
                if (!_regexClassifiers.ContainsKey(domainAndIntent.ToString()))
                {
                    continue;
                }

                // Apply contextual domain scoping here (filter out multiturn-only intents that are not part of the current context)
                if (contextualDomains != null &&
                    multiturnIntents.Contains(regexIntent) &&
                    !contextualDomains.Contains(domain))
                {
                    continue;
                }

                TaggedData whitelistResults = _regexClassifiers[domainAndIntent.ToString()].ApplyWhitelist(utterance, wasSpeechInput, queryLogger);
                if (whitelistResults == null)
                {
                    continue;
                }

                // Remove slots with empty values
                CullEmptySlots(domainAndIntent, whitelistResults, queryLogger);

                RecoResult newRecoResult = new RecoResult()
                {
                    Domain = domain,
                    Intent = regexIntent,
                    Confidence = whitelistResults.Confidence,
                    Utterance = utterance,
                    Source = "RegexRule"
                };
                newRecoResult.TagHyps.Add(whitelistResults);
                returnVal.Add(newRecoResult);

                // Run the blacklist as well
                returnVal = _regexClassifiers[domainAndIntent.ToString()].ApplyBlacklist(returnVal, utterance, wasSpeechInput, queryLogger);
            }

            return returnVal;
        }

        private static IList<RecoResult> RemoveRecoResultIfAlreadyPresent(IList<RecoResult> results, string domain, string intent)
        {
            IList<RecoResult> returnVal = new List<RecoResult>();
            foreach (RecoResult r in results)
            {
                if (!r.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase) &&
                    !r.Intent.Equals(intent, StringComparison.OrdinalIgnoreCase))
                    returnVal.Add(r);
            }
            return returnVal;
        }

        /// <summary>
        /// Calculates a weighed average confidence from domain classifier + intent classifier confidences
        /// </summary>
        /// <param name="domainConfidence"></param>
        /// <param name="intentConfidence"></param>
        /// <returns></returns>
        private static float CombineConfidences(float domainConfidence, float intentConfidence)
        {
            float finalConfidence;

            // Rather than use that mathematical curve, we use a data-driven curve defined by a large hardcoded matrix
            int x = Math.Max(0, Math.Min(_confidenceCurveDimension - 1, (int)Math.Floor(domainConfidence * _confidenceCurveDimension)));
            int y = Math.Max(0, Math.Min(_confidenceCurveDimension - 1, (int)Math.Floor(intentConfidence * _confidenceCurveDimension)));

            // use linear interpolation of 4 points to find the final value
            float x_lerp = (domainConfidence * _confidenceCurveDimension) - (float)x;
            float y_lerp = (intentConfidence * _confidenceCurveDimension) - (float)y;
            float topLeft = _confidenceCurve[y][x];
            float topRight = _confidenceCurve[y][x + 1];
            float bottomLeft = _confidenceCurve[y + 1][x];
            float bottomRight = _confidenceCurve[y + 1][x + 1];
            finalConfidence = (((topLeft * (1 - x_lerp)) + (topRight * x_lerp)) * (1 - y_lerp)) +
                              (((bottomLeft * (1 - x_lerp)) + (bottomRight * x_lerp)) * y_lerp);

            //float domainWeight = 0.4f;  
            //float intentWeight = 1.0f - domainWeight;

            // The best algorithm - 99.07%
            //finalConfidence = (float)((Math.Pow(domainConfidence, 0.333) * domainWeight) + (Math.Pow(intentConfidence, 0.333) * intentWeight));

            // Geometric mean - 98.81%
            //finalConfidence = (float)(Math.Sqrt(domainConfidence) + Math.Sqrt(intentConfidence)) / 2f;
            // cubed - 99.07%
            //finalConfidence = (float)(Math.Pow(domainConfidence, 0.333) + Math.Pow(intentConfidence, 0.333)) / 2f;

            // Something else - 98.94%
            //finalConfidence = (float)Math.Sqrt(domainConfidence + intentConfidence) / 1.414f;
            // cubed - 98.88%
            //finalConfidence = (float)Math.Pow(domainConfidence + intentConfidence, 0.333) / 1.414f;


            // Other, failed equations

            // Arithmetic mean - 95.96%
            //finalConfidence = (domainConfidence + intentConfidence) / 2f;

            // Euclidean distance - 97.49%
            //finalConfidence = (float)Math.Sqrt((domainConfidence * domainConfidence) + (intentConfidence * intentConfidence)) / 1.414f;
            // cubed - 96.53%
            //finalConfidence = (float)Math.Pow((domainConfidence * domainConfidence) + (intentConfidence * intentConfidence), 0.333) / 1.414f;

            // Another something else - 
            //finalConfidence = (float)(Math.Sqrt(domainConfidence) * Math.Sqrt(intentConfidence));
            // cubed - 
            //finalConfidence = (float)(Math.Pow(domainConfidence, 0.333) * Math.Pow(intentConfidence, 0.333));

            // Clamp the return val
            return Math.Max(0, Math.Min(1, finalConfidence));
        }

        /// <summary>
        /// Slot values after canonicalization or other operations may end up with an empty value.
        /// This method will remove all slots with empty values from the given set of tag hypotheses
        /// </summary>
        /// <param name="domainIntent"></param>
        /// <param name="allTags"></param>
        /// <param name="queryLogger"></param>
        /// <returns></returns>
        private static IList<TaggedData> CullEmptySlots(DomainIntent domainIntent, IList<TaggedData> allTags, ILogger queryLogger)
        {
            foreach (var tags in allTags)
            {
                CullEmptySlots(domainIntent, tags, queryLogger);
            }

            // Since culling may have collapsed several hypotheses into one, we need to deduplicate the list now
            // Remember that we want to preserve the highest-confidence hyps, so the ordering
            // of the list is important here
            IList<TaggedData> returnVal = new List<TaggedData>();
            foreach (TaggedData original in allTags)
            {
                bool exists = false;
                foreach (TaggedData updated in returnVal)
                {
                    if (TagHypothesisEquals(original, updated))
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                    returnVal.Add(original);
            }

            return returnVal;
        }

        /// <summary>
        /// Slot values after canonicalization or other operations may end up with an empty value.
        /// This method will remove all slots with empty values from the given tag set
        /// </summary>
        /// <param name="domainIntent"></param>
        /// <param name="tags"></param>
        /// <param name="queryLogger"></param>
        private static void CullEmptySlots(DomainIntent domainIntent, TaggedData tags, ILogger queryLogger)
        {
            List<SlotValue> slotsToKeep = new List<SlotValue>();
            foreach (SlotValue s in tags.Slots)
            {
                if (!string.IsNullOrEmpty(s.Value))
                {
                    slotsToKeep.Add(s);
                }
                else
                {
                    queryLogger.Log("Dropping slot \"" + domainIntent.ToString() + "/" + s.Name + "\" because it has an empty value after canonicalization", LogLevel.Vrb);
                }
            }
            tags.Slots = slotsToKeep;
        }

        /// <summary>
        /// This method is not perfect - doesn't deep dive into annotations and such
        /// </summary>
        /// <param name="one"></param>
        /// <param name="two"></param>
        /// <returns></returns>
        private static bool TagHypothesisEquals(TaggedData one, TaggedData two)
        {
            if (!string.Equals(one.Utterance, two.Utterance))
                return false;

            if (one.Slots.Count != two.Slots.Count)
                return false;

            foreach (SlotValue slot in one.Slots)
            {
                SlotValue otherSlot = null;
                foreach (SlotValue s in two.Slots)
                {
                    if (string.Equals(s.Name, slot.Name))
                    {
                        otherSlot = s;
                    }
                }

                if (otherSlot == null)
                    return false;

                if (!string.Equals(slot.Value, otherSlot.Value))
                    return false;

                if (!string.Equals(slot.Format, otherSlot.Format))
                    return false;
            }

            return true;
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                _masterStringIndex?.Dispose();
                _taggerConfidenceCutoff?.Dispose();
                _relativeDomainIntentConfidenceCutoff?.Dispose();
                _absoluteDomainIntentConfidenceCutoff?.Dispose();
            }
        }

        private void TrainStatisticalModels(ICompactIndex<string> stringIndex, IRealTimeProvider realTime)
        {
            TrainingDataList<DomainIntentContextFeature> negativeTrainingData = CreateCombinedDomainTrainingFile(1.0f);

            IList<ModelTrainer> trainers = new List<ModelTrainer>();
            IList<DomainClassifierTrainer> domainTrainers = new List<DomainClassifierTrainer>();
            IList<IntentClassifierTrainer> intentTrainers = new List<IntentClassifierTrainer>();
            IList<SlotTaggerTrainer> slotTrainers = new List<SlotTaggerTrainer>();

            string defaultCrossTrainingRules = _config.DefaultCrossTrainingRules;

            // Build the set of cross-training rules
            _logger.Log("Compiling cross-training rules...");
            IList<CrossTrainingRule> crossTrainingRules = CrossDomainRuleTrainer.ConstructCrossTrainingRules(_domains, _training, _logger, defaultCrossTrainingRules, realTime);

            foreach (string domain in _domains)
            {
                IConfiguration domainConfig = _training.GetDomainConfiguration(domain, realTime);

#pragma warning disable CA2000 // Dispose objects before losing scope
                // Build a thread pool and execute a set of work items
                DomainClassifierTrainer domainWorkItem = new DomainClassifierTrainer(
                    domain,
                    domainConfig,
                    stringIndex,
                    negativeTrainingData,
                    _training,
                    _fileSystem,
                    _modelDir,
                    _logger,
                    crossTrainingRules);
                trainers.Add(domainWorkItem);
                domainTrainers.Add(domainWorkItem);
                _threadPool.Value.EnqueueUserWorkItem(domainWorkItem.Run);

                IntentClassifierTrainer intentWorkItem = new IntentClassifierTrainer(
                    domain,
                    domainConfig, // TODO replace these with weak pointers to string index and config
                    stringIndex,
                    negativeTrainingData,
                    _training,
                    _fileSystem,
                    _modelDir,
                    _logger,
                    crossTrainingRules);
                trainers.Add(intentWorkItem);
                intentTrainers.Add(intentWorkItem);
                _threadPool.Value.EnqueueUserWorkItem(intentWorkItem.Run);

                SlotTaggerTrainer slotWorkItem = new SlotTaggerTrainer(
                    domain,
                    new WeakPointer<IConfiguration>(domainConfig),
                    new WeakPointer<ICompactIndex<string>>(stringIndex),
                    negativeTrainingData,
                    _training,
                    _languageTools.FeaturizationWordBreaker,
                    _fileSystem,
                    _modelDir,
                    _logger,
                    _crfTrainer,
                    _config.TaggerConfidenceCutoff);
#pragma warning restore CA2000 // Dispose objects before losing scope
                trainers.Add(slotWorkItem);
                slotTrainers.Add(slotWorkItem);
                _threadPool.Value.EnqueueUserWorkItem(slotWorkItem.Run);

                // Is there regex data for this intent? Load it
                foreach (string regexIntent in _training.GetKnownRegexIntents(domain))
                {
                    DomainIntent domainIntent = new DomainIntent(domain, regexIntent);
                    RegexClassifier regexer = TryLoadRegexes(domainIntent);
                    if (regexer != null)
                    {
                        _regexClassifiers[domainIntent.ToString()] = regexer;
                    }
                }

                // Is this domain a sentiment domain? Mark it as such
                if (domainConfig.ContainsKey("SentimentDomain") && domainConfig.GetBool("SentimentDomain", false))
                {
                    _sentimentDomains.Add(domain);
                }
            }

            foreach (ModelTrainer thread in trainers)
            {
                thread.Join();
            }

            // Merge each thread's results
            foreach (DomainClassifierTrainer thread in domainTrainers)
            {
                if (thread.DomainClassifier != null)
                {
                    _domainClassifiers[thread.ModelDomain] = thread.DomainClassifier;
                }

                foreach (string regexDomain in thread.RegexOnlyDomains)
                {
                    _regexOnlyDomains.Add(regexDomain);
                }

                thread.Dispose();
            }

            foreach (IntentClassifierTrainer thread in intentTrainers)
            {
                _intentClassifiers[thread.ModelDomain] = thread.IntentClassifiers;
                thread.Dispose();
            }

            foreach (SlotTaggerTrainer thread in slotTrainers)
            {
                foreach (var slotTagger in thread.SlotTaggers)
                {
                    _tagClassifiers.Add(slotTagger.Key, slotTagger.Value);
                }

                thread.Dispose();
            }
        }



        /// <summary>
        /// Attempts to find a regex whitelist/blacklist file for the given domain and intent
        /// and load them into the _regexClassifiers map.
        /// </summary>
        /// <param name="domainIntent"></param>
        private RegexClassifier TryLoadRegexes(DomainIntent domainIntent)
        {
            VirtualPath whitelistFile = _modelDir.Combine(domainIntent.Domain + " " + domainIntent.Intent + ".whitelist");
            VirtualPath blacklistFile = _modelDir.Combine(domainIntent.Domain + " " + domainIntent.Intent + ".blacklist");
            if (_fileSystem.Exists(whitelistFile) || _fileSystem.Exists(blacklistFile))
            {
                _logger.Log("Loading regex data for " + domainIntent + "...");
                return new RegexClassifier(
                    domainIntent,
                    _fileSystem,
                    whitelistFile,
                    blacklistFile,
                    _logger.Clone("RegexClassifier-" + domainIntent.ToString()));
            }
            return null;
        }

        private void TrainStatisticalRanker(ICompactIndex<string> stringIndex, IRealTimeProvider realTime)
        {
            _logger.Log("Setting up statistical ranker...");
            List<RankingFeature> allRankingFeatures = new List<RankingFeature>();

            // Are the features cached?
            VirtualPath rankingFeatureCache = _cacheDir.Combine("ranking.features");

            if (_fileSystem.Exists(rankingFeatureCache))
            {
                _logger.Log("Using ranker cache");
                // Read the cache
                using (StreamReader cacheIn = new StreamReader(_fileSystem.OpenStream(rankingFeatureCache, FileOpenMode.Open, FileAccessMode.Read)))
                {
                    while (!cacheIn.EndOfStream)
                    {
                        string nextLine = cacheIn.ReadLine();
                        if (nextLine == null) continue;

                        RankingFeature newFeat = new RankingFeature();
                        if (newFeat.Parse(nextLine))
                        {
                            allRankingFeatures.Add(newFeat);
                        }
                    }
                }
            }
            else
            {
                // Extract ranking features from validation data
                _logger.Log("Extracting ranking features...");
                RankingFeatureExtractor rankingFeatureExtractor = new RankingFeatureExtractor();
                int utteranceId = 0;

                // Set the thresholds to minimum here (so we get the most reco results possible)
                //float oldAbsoluteCutoff = _absoluteDomainIntentConfidenceCutoff;
                //float oldRelativeCutoff = _relativeDomainIntentConfidenceCutoff;
                //_absoluteDomainIntentConfidenceCutoff = 0.1f;
                //_relativeDomainIntentConfidenceCutoff = 0.0f;

                foreach (string domain in _domains)
                {
                    foreach (string intent in _training.GetKnownIntents(domain))
                    {
                        VirtualPath validationFile = _training.GetValidationFile(domain, intent);
                        if (!_fileSystem.Exists(validationFile))
                            continue;

                        using (StreamReader reader = new StreamReader(_fileSystem.OpenStream(validationFile, FileOpenMode.Open, FileAccessMode.Read)))
                        {
                            while (!reader.EndOfStream)
                            {
                                string[] parts = reader.ReadLine().Split('\t');
                                if (parts.Length < 2)
                                    continue;

                                TaggedData goldenUtterance = TaggedDataSplitter.ParseSlots(parts[1], _languageTools.FeaturizationWordBreaker);
                                RecoResult expectedResult = new RecoResult()
                                {
                                    Confidence = 1.0f,
                                    Domain = domain,
                                    Intent = intent,
                                };
                                expectedResult.TagHyps.Add(goldenUtterance);
                                LUInput speechHyp = new LUInput()
                                {
                                    Utterance = goldenUtterance.Utterance,
                                    LexicalForm = string.Empty
                                };
                                RecognizedPhrase results = Classify(speechHyp, true, _logger, realTime);
                                IList<RankingFeature> features = rankingFeatureExtractor.ExtractTrainingFeatures(
                                    utteranceId++,
                                    results.Recognition,
                                    expectedResult);
                                allRankingFeatures.FastAddRangeList(features);
                            }
                        }
                    }
                }

                // Restore the old thresholds
                //_absoluteDomainIntentConfidenceCutoff = oldAbsoluteCutoff;
                //_relativeDomainIntentConfidenceCutoff = oldRelativeCutoff;

                // Write the feature cache
                using (StreamWriter cacheOut = new StreamWriter(_fileSystem.OpenStream(rankingFeatureCache, FileOpenMode.Create, FileAccessMode.Write)))
                {
                    foreach (RankingFeature feat in allRankingFeatures)
                    {
                        cacheOut.WriteLine(feat.ToString());
                    }
                }

                _logger.Log("Done with feature extraction");
            }
            _logger.Log("Training ranker");
            RegressionTreeReranker statisticalRanker = new RegressionTreeReranker(
                _logger.Clone("RegressionTreeRanker"),
                _masterStringIndex,
                _fileSystem,
                _modelDir);
            statisticalRanker.Train(allRankingFeatures);
            this._statisticalRanker = statisticalRanker;
            _logger.Log("Done");
        }

        private static void MeasureInaccuracy(
            IList<TaggedData> actual,
            TaggedData golden,
            out float precision,
            out float recall,
            out bool perfect)
        {
            float recallNum = 0;
            float recallDenom = 0;

            float precisionNum = 0;
            float precisionDenom = 0;

            foreach (SlotValue goldenTag in golden.Slots)
            {
                string tagName = goldenTag.Name;
                string expectedTagValue = goldenTag.Value;
                string actualTagValue = String.Empty;
                foreach (SlotValue actualTag in actual[0].Slots)
                {
                    if (actualTag.Name == tagName)
                    {
                        actualTagValue = actualTag.Value;
                        break;
                    }
                }

                // If goldenTagValue is a subset of actualTagValue, recall = true
                if (actualTagValue.Contains(expectedTagValue))
                {
                    recallNum += 1;
                }
                recallDenom += 1;

                // If actualTagValue is exactly equal to goldenTagValue, precision = true;
                if (actualTagValue.Equals(expectedTagValue))
                {
                    precisionNum += 1;
                }
                precisionDenom += 1;
            }

            recall = -1.0f;
            if (recallDenom > 0)
            {
                recall = recallNum / recallDenom;
            }

            precision = -1.0f;
            if (precisionDenom > 0)
            {
                precision = precisionNum / precisionDenom;
            }

            perfect = IsPerfectContextualInterpretationPossible(golden.Slots, actual);
        }

        /// <summary>
        /// Returns the quality level of a classification, in units of standard deviations of the correct score above the mean score
        /// </summary>
        /// <param name="results"></param>
        /// <param name="expectedDomain"></param>
        /// <param name="expectedIntent"></param>
        /// <returns></returns>
        private static float CalculateClassificationQuality(List<RecoResult> results, string expectedDomain, string expectedIntent)
        {
            RecoResult goodResult = null;
            StaticAverage mean = new StaticAverage();
            foreach (RecoResult r in results)
            {
                if (r.Domain.Equals(expectedDomain) && r.Intent.Equals(expectedIntent))
                {
                    goodResult = r;
                }

                mean.Add(r.Confidence);
            }

            if (goodResult == null)
                return 0;

            StaticAverage variance = new StaticAverage();
            foreach (RecoResult r in results)
            {
                variance.Add((r.Confidence - mean.Average) * (r.Confidence - mean.Average));
            }

            float stDev = (float)Math.Sqrt(variance.Average);

            if (stDev == 0)
            {
                return 0;
            }

            float quality = (goodResult.Confidence - (float)mean.Average) / stDev;

            return quality;
        }

        // Search exhaustively through all results to see if perfect contextual interpretation is possible
        private static bool IsPerfectContextualInterpretationPossible(IList<SlotValue> goldenSlots, IList<TaggedData> actualData)
        {
            foreach (TaggedData taggedHyp in actualData)
            {
                bool allSlotsMatched = true;
                foreach (SlotValue goldenTag in goldenSlots)
                {
                    string tagName = goldenTag.Name;
                    string expectedTagValue = goldenTag.Value;
                    foreach (SlotValue actualTag in taggedHyp.Slots)
                    {
                        if (actualTag.Name == tagName)
                        {
                            if (!actualTag.Value.Equals(expectedTagValue) &&
                                (actualTag.Alternates == null ||
                                !actualTag.Alternates.Contains(expectedTagValue)))
                            {
                                allSlotsMatched = false;
                            }
                        }
                    }
                }
                if (allSlotsMatched)
                    return true;
            }
            return false;
        }

        public void ValidateModels()
        {
            ILogger validationLogger = _logger.Clone("LUValidator");
            validationLogger.Log("Starting model validation for " + _locale + "...");
            Stopwatch validateTimer = new Stopwatch();
            validateTimer.Start();

            //float oldAbsoluteCutoff = _absoluteDomainIntentConfidenceCutoff;
            //float oldRelativeCutoff = _relativeDomainIntentConfidenceCutoff;
            //_absoluteDomainIntentConfidenceCutoff = 0.001f;
            //_relativeDomainIntentConfidenceCutoff = 0.0f;

            int totalTests = 0;
            ConfusionMatrix confusionMatrix = new ConfusionMatrix(validationLogger);
            Counter<string> domainClassificationErrors = new Counter<string>();
            IDictionary<string, StaticAverage> domainQualities = new Dictionary<string, StaticAverage>();
            StaticAverage totalModelQuality = new StaticAverage();
            Counter<string> domainConfidences = new Counter<string>();
            Counter<string> domainIntentConfidences = new Counter<string>();
            Counter<string> domainPrecision = new Counter<string>();
            Counter<string> domainRecall = new Counter<string>();
            Counter<string> testsPerDomain = new Counter<string>();
            Counter<string> testsWithSlotsPerDomain = new Counter<string>();
            Counter<string> perfectContextualInterpretationsPerDomain = new Counter<string>();
            Counter<string> testsPerDomainIntent = new Counter<string>();

            foreach (string domain in _domains)
            {
                domainQualities[domain] = new StaticAverage();
                HashSet<string> contextualDomains = new HashSet<string>() { domain };

                foreach (string intent in _training.GetKnownIntents(domain))
                {
                    string domainIntent = domain + "/" + intent;

                    VirtualPath validationFile = _training.GetValidationFile(domain, intent);
                    if (!_fileSystem.Exists(validationFile))
                        continue;
                    
                    validationLogger.Log("Validating " + domainIntent + "... ");
                    int classificationErrorsThisTest = 0;
                    int thisTestCount = 0;
                    using (StreamReader reader = new StreamReader(_fileSystem.OpenStream(validationFile, FileOpenMode.Open, FileAccessMode.Read)))
                    {
                        while (!reader.EndOfStream)
                        {
                            string[] parts = reader.ReadLine().Split('\t');
                            if (parts.Length < 2)
                                continue;
                            string taggedUtterance = parts[1];

                            TaggedData goldenUtterance = TaggedDataSplitter.ParseSlots(taggedUtterance, _languageTools.FeaturizationWordBreaker);

                            totalTests++;
                            testsPerDomain.Increment(domain);
                            testsPerDomainIntent.Increment(domainIntent);
                            LUInput speechHyp = new LUInput()
                            {
                                Utterance = goldenUtterance.Utterance,
                                LexicalForm = string.Empty
                            };
                            
                            RecognizedPhrase recoPhrase = Classify(speechHyp, true, _logger, null, contextualDomains);
                            List<RecoResult> results = recoPhrase.Recognition;
                            thisTestCount++;
                            confusionMatrix.IncrementExpected(domainIntent);

                            // Find this classification's quality in units of standard deviations above mean
                            float thisQuality = CalculateClassificationQuality(results, domain, intent);
                            domainQualities[domain].Add(thisQuality);
                            totalModelQuality.Add(thisQuality);

                            if (results.Count == 0)
                            {
                                domainClassificationErrors.Increment(domain);
                                confusionMatrix.Increment("no result", domainIntent);
                                validationLogger.Log("No classification results for \"" + goldenUtterance.Utterance + "\" (expected " + domainIntent + ")");
                                classificationErrorsThisTest++;
                                continue;
                            }
                            if (!(results[0].Domain + "/" + results[0].Intent).Equals(domainIntent))
                            {
                                domainClassificationErrors.Increment(domain);
                                classificationErrorsThisTest++;
                                confusionMatrix.Increment(results[0].Domain + "/" + results[0].Intent, domainIntent);
                                validationLogger.Log("Misrecognized \"" + goldenUtterance.Utterance + "\" as " + (results[0].Domain + "/" + results[0].Intent), LogLevel.Vrb);
                                continue;
                            }

                            domainConfidences.Increment(domain, (float)results[0].Confidence);
                            domainIntentConfidences.Increment(domain + "/" + intent, (float)results[0].Confidence);

                            // Run canonicalizers on the golden data so they'll compare properly
                            // Canonicalize(goldenUtterance, new DomainIntent(domain, intent), validationLogger);

                            float recall;
                            float precision;
                            bool perfect;

                            MeasureInaccuracy(results[0].TagHyps,
                                        goldenUtterance,
                                        out precision, out recall, out perfect);
                            if (precision >= 0 && recall >= 0)
                            {
                                domainPrecision.Increment(domain, precision);
                                domainRecall.Increment(domain, recall);
                                testsWithSlotsPerDomain.Increment(domain);
                                if (perfect)
                                {
                                    perfectContextualInterpretationsPerDomain.Increment(domain);
                                }
                            }
                        }
                    }
                }
            }

            //_absoluteDomainIntentConfidenceCutoff = oldAbsoluteCutoff;
            //_relativeDomainIntentConfidenceCutoff = oldRelativeCutoff;

            validateTimer.Stop();

            if (totalTests > 0)
            {
                Counter<string> CERperDomain = new Counter<string>();
                foreach (KeyValuePair<string, float> t in domainConfidences)
                {
                    CERperDomain.Increment(t.Key, (domainClassificationErrors.GetCount(t.Key) /
                                                        Math.Max(1, testsPerDomain.GetCount(t.Key))));
                }

                float averageCER = 0f;
                foreach (KeyValuePair<string, float> t in domainConfidences)
                {
                    averageCER += CERperDomain.GetCount(t.Key);
                }
                averageCER /= Math.Max(1, domainConfidences.NumItems);

                Counter<string> TPCIperDomain = new Counter<string>();
                foreach (KeyValuePair<string, float> slottedDomain in testsWithSlotsPerDomain)
                {
                    TPCIperDomain.Increment(slottedDomain.Key, perfectContextualInterpretationsPerDomain.GetCount(slottedDomain.Key) /
                        Math.Max(1, slottedDomain.Value));
                }

                float averageTCPI = 0f;
                foreach (KeyValuePair<string, float> t in TPCIperDomain)
                {
                    averageTCPI += t.Value;
                }
                averageTCPI /= Math.Max(1, TPCIperDomain.NumItems);

                // Or else we just ran full validation, write data to the cache
                validationLogger.Log("VALIDATION COMPLETED");
                validationLogger.Log("Total tests run: " + totalTests);
                validationLogger.Log("Elapsed time: " + validateTimer.ElapsedMilliseconds + "ms");
                validationLogger.Log("Average time: " + ((float)validateTimer.ElapsedMilliseconds / totalTests) + "ms");
                validationLogger.Log("Quality (Standard deviations above mean): " + totalModelQuality.Average);
                validationLogger.Log("Overall classification error rate: " + averageCER);
                validationLogger.Log("Theoretical contextual perfect interpretation rate (TCPI): " + averageTCPI);
                float overallConfidence = 0;

                int longestDomainName = GetLongestStringLength(domainConfidences);

                foreach (KeyValuePair<string, float> domainAndCount in domainConfidences)
                {
                    float testsForThisDomain = Math.Max(1, testsPerDomain.GetCount(domainAndCount.Key));
                    float testsWithSlotsForThisDomain = Math.Max(1, testsWithSlotsPerDomain.GetCount(domainAndCount.Key));
                    float thisConfidence = domainAndCount.Value / testsForThisDomain;
                    float thisPrecision = domainPrecision.GetCount(domainAndCount.Key) / testsWithSlotsForThisDomain;
                    float thisRecall = domainRecall.GetCount(domainAndCount.Key) / testsWithSlotsForThisDomain;
                    float thisClassificationErrorRate = CERperDomain.GetCount(domainAndCount.Key);
                    float thisQuality = (float)domainQualities[domainAndCount.Key].Average;
                    float fScore = 0;

                    if (thisPrecision + thisRecall > 0)
                    {
                        fScore = (2 * thisPrecision * thisRecall) / (thisPrecision + thisRecall);
                    }

                    if (testsWithSlotsPerDomain.GetCount(domainAndCount.Key) > 0)
                    {
                        string warningString = thisConfidence < 0.8 || fScore < 0.8 || thisClassificationErrorRate > 0.06 ?
                            "WARNING!!!" : String.Empty;
                        validationLogger.Log(string.Format(
                            "{0,-" + longestDomainName + "}  Conf={1:F4}  Qual={2:F4}  CER={3:F4}  Prec={4:F4}  Rcl={5:F4}  F1={6:F4}  {7}",
                            domainAndCount.Key, thisConfidence, thisQuality, thisClassificationErrorRate, thisPrecision, thisRecall, fScore, warningString));
                    }
                    else
                    {
                        string warningString = thisConfidence < 0.8 || thisClassificationErrorRate > 0.06 ?
                            "WARNING!!!" : String.Empty;
                        validationLogger.Log(string.Format(
                            "{0,-" + longestDomainName + "}  Conf={1:F4}  Qual={2:F4}  CER={3:F4}  {4}",
                            domainAndCount.Key, thisConfidence, thisQuality, thisClassificationErrorRate, warningString));
                    }

                    overallConfidence += thisConfidence;
                }

                validationLogger.Log("Overall domain confidence: " + (overallConfidence / domainConfidences.NumItems));

                // Output the confusion matrix
                validationLogger.Log("Here are some confusion matrix results:");
                confusionMatrix.PrintTopCells(10);
                confusionMatrix.WriteToCSV(_fileSystem.OpenStream(new VirtualPath("confusion_matrix.csv"), FileOpenMode.Create, FileAccessMode.Write));

                validationLogger.Log("Confusion matrix written to confusion_matrix.csv");
            }
            else
            {
                validationLogger.Log("No validation data found.");
            }
        }

        private static int GetLongestStringLength(Counter<string> counter)
        {
            int longest = 0;
            foreach (KeyValuePair<string, float> domain in counter)
            {
                longest = Math.Max(longest, domain.Key.Length);
            }

            return longest;
        }

        /// <summary>
        /// A single work item that can be queued to a thread pool
        /// to classify and tag an utterance
        /// </summary>
        private class WorkAtom : IDisposable
        {
            private IList<RecoResult> _returnVal = null;
            private readonly RunClassifier _workItem;
            private readonly string _domainIntent;
            private readonly Sentence _utterance;
            private readonly bool _wasSpeech;
            private readonly EventWaitHandle _taskComplete;
            private readonly ILogger _queryLogger;
            private int _disposed = 0;

            public WorkAtom(RunClassifier del, string domainIntent, Sentence utterance, bool wasSpeechInput, ILogger queryLogger)
            {
                _workItem = del;
                _domainIntent = domainIntent;
                _utterance = utterance;
                _wasSpeech = wasSpeechInput;
                _queryLogger = queryLogger;
                _taskComplete = new EventWaitHandle(false, EventResetMode.AutoReset);
                DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
            }

#if TRACK_IDISPOSABLE_LEAKS
            ~WorkAtom()
            {
                Dispose(false);
            }
#endif

            public void Run(object dummy)
            {
                _returnVal = _workItem(_domainIntent, _utterance, _wasSpeech, _queryLogger);
                _taskComplete.Set();
            }

            public EventWaitHandle WaitHandle
            {
                get
                {
                    return _taskComplete;
                }
            }

            public IList<RecoResult> ReturnValue
            {
                get
                {
                    return _returnVal;
                }
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (!AtomicOperations.ExecuteOnce(ref _disposed))
                {
                    return;
                }

                DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

                if (disposing)
                {
                    _taskComplete?.Dispose();
                }
            }
        }


        private static int _confidenceCurveDimension = 40;

        /// <summary>
        /// This curve represents the expected model confidence given a domain score (X) and an intent score (Y)
        /// </summary>
        private static readonly float[][] _confidenceCurve = new float[][] {
            new float[] {0.0100f,0.0101f,0.0102f,0.0108f,0.0118f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0220f,0.0191f,0.0170f,0.0167f,0.0155f,0.0147f,0.0133f,0.0131f,0.0000f,0.0000f,0.0000f,0.0003f,0.0003f,0.0000f},
            new float[] {0.0091f,0.0090f,0.0091f,0.0096f,0.0105f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0254f,0.0199f,0.0172f,0.0163f,0.0155f,0.0148f,0.0138f,0.0120f,0.0114f,0.0103f,0.0002f,0.0005f,0.0005f,0.0006f,0.0009f},
            new float[] {0.0084f,0.0083f,0.0084f,0.0087f,0.0095f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0246f,0.0197f,0.0166f,0.0151f,0.0150f,0.0142f,0.0134f,0.0116f,0.0112f,0.0102f,0.0004f,0.0007f,0.0007f,0.0008f,0.0009f},
            new float[] {0.0081f,0.0080f,0.0080f,0.0081f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0268f,0.0201f,0.0176f,0.0153f,0.0149f,0.0136f,0.0129f,0.0115f,0.0115f,0.0110f,0.0006f,0.0009f,0.0007f,0.0008f,0.0009f},
            new float[] {0.0078f,0.0077f,0.0077f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0227f,0.0183f,0.0168f,0.0152f,0.0150f,0.0139f,0.0134f,0.0130f,0.0013f,0.0009f,0.0009f,0.0010f,0.0008f,0.0009f},
            new float[] {0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0342f,0.0229f,0.0204f,0.0180f,0.0167f,0.0163f,0.0164f,0.0165f,0.0019f,0.0017f,0.0012f,0.0016f,0.0014f,0.0013f},
            new float[] {0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0422f,0.0339f,0.0282f,0.0273f,0.0289f,0.0299f,0.0032f,0.0035f,0.0038f,0.0046f,0.0029f,0.0032f,0.0031f},
            new float[] {0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0014f,0.0011f,0.0031f,0.0031f,0.0048f,0.0045f,0.0036f,0.0037f,0.0054f,0.0064f,0.0065f,0.0060f,0.0076f,0.0087f},
            new float[] {0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0025f,0.0016f,0.0046f,0.0036f,0.0063f,0.0055f,0.0046f,0.0061f,0.0055f,0.0094f,0.0122f,0.0099f,0.0098f,0.0112f},
            new float[] {0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0033f,0.0030f,0.0082f,0.0107f,0.0094f,0.0088f,0.0075f,0.0086f,0.0082f,0.0115f,0.0150f,0.0135f,0.0138f,0.0125f},
            new float[] {0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0043f,0.0094f,0.0130f,0.0129f,0.0140f,0.0104f,0.0108f,0.0107f,0.0197f,0.0228f,0.0211f,0.0245f,0.0255f},
            new float[] {0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0043f,0.0043f,0.0058f,0.0101f,0.0085f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0058f,0.0210f,0.0197f,0.0170f,0.0174f,0.0166f,0.0163f,0.0266f,0.0342f,0.0316f,0.0293f,0.0323f,0.0313f},
            new float[] {0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0041f,0.0042f,0.0048f,0.0063f,0.0091f,0.0278f,0.0185f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0081f,0.0222f,0.0276f,0.0260f,0.0245f,0.0226f,0.0550f,0.0466f,0.0373f,0.0386f,0.0360f,0.0375f},
            new float[] {0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0047f,0.0054f,0.0074f,0.0098f,0.0120f,0.0175f,0.0294f,0.0313f,0.0250f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0253f,0.0426f,0.0319f,0.0294f,0.0500f,0.0431f,0.0398f,0.0425f,0.0478f,0.0510f},
            new float[] {0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0066f,0.0069f,0.0086f,0.0115f,0.0169f,0.0185f,0.0217f,0.0313f,0.0263f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0133f,0.0227f,0.0341f,0.0686f,0.0654f,0.0571f,0.0466f,0.0479f,0.0500f},
            new float[] {0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0081f,0.0077f,0.0096f,0.0149f,0.0185f,0.0200f,0.0208f,0.0313f,0.0303f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.2985f,0.2778f,0.2716f,0.2366f,0.2292f,0.0549f,0.0511f,0.0621f,0.0584f,0.0513f,0.0544f},
            new float[] {0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0141f,0.0119f,0.0143f,0.0161f,0.0189f,0.0208f,0.0213f,0.0333f,0.0385f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.4000f,0.3774f,0.3385f,0.2785f,0.2683f,0.2418f,0.2500f,0.0543f,0.0530f,0.0652f,0.0504f,0.0584f},
            new float[] {0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0143f,0.0154f,0.0164f,0.0159f,0.0217f,0.0227f,0.0250f,0.0385f,0.0435f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.2062f,0.2128f,0.2041f,0.2095f,0.1897f,0.1803f,0.2750f,0.5180f,0.4551f,0.3276f,0.3167f,0.3184f,0.0413f},
            new float[] {0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0222f,0.0179f,0.0208f,0.0244f,0.0244f,0.0417f,0.0417f,0.0000f,0.0000f,0.0000f,0.0833f,0.0909f,0.1429f,0.1000f,0.0000f,0.0000f,0.2316f,0.2292f,0.3697f,0.3659f,0.3629f,0.3258f,0.4947f,0.6050f,0.4557f,0.3758f,0.3585f,0.3314f,0.3333f},
            new float[] {0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0256f,0.0222f,0.0303f,0.0455f,0.0417f,0.0000f,0.0000f,0.0000f,0.1111f,0.1250f,0.2500f,0.1429f,0.1429f,0.1111f,0.0278f,0.2391f,0.3805f,0.3761f,0.3750f,0.3659f,0.5367f,0.5314f,0.6788f,0.4500f,0.4260f,0.3419f,0.3605f,0.3723f},
            new float[] {0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.4375f,0.7333f,0.6875f,0.4545f,0.4545f,0.2500f,0.2500f,0.2500f,0.4000f,0.0645f,0.0645f,0.4019f,0.3874f,0.3805f,0.3782f,0.3719f,0.5828f,0.5689f,0.5439f,0.5924f,0.3926f,0.3759f,0.3600f,0.3714f},
            new float[] {0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.5833f,0.7857f,0.7857f,0.7857f,0.9231f,0.8571f,0.8571f,0.7143f,0.5000f,0.5000f,0.5000f,0.0877f,0.0690f,0.4369f,0.4412f,0.4356f,0.4381f,0.4434f,0.5988f,0.5828f,0.5434f,0.5987f,0.4091f,0.3901f,0.3396f,0.3464f},
            new float[] {0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.7000f,0.7000f,0.7857f,0.7857f,0.7857f,0.8000f,0.8571f,0.8571f,0.7500f,0.7778f,0.6000f,0.6000f,0.0877f,0.0847f,0.3418f,0.4592f,0.4600f,0.4455f,0.4393f,0.5833f,0.5632f,0.5549f,0.6115f,0.3709f,0.3017f,0.3598f,0.3757f},
            new float[] {0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.7000f,0.7857f,0.7857f,0.9531f,0.9531f,0.9538f,0.9538f,0.9688f,0.9697f,0.7778f,0.6000f,0.6000f,0.7143f,0.1071f,0.3418f,0.3418f,0.4600f,0.4500f,0.4144f,0.5706f,0.5789f,0.7426f,0.5030f,0.4231f,0.4000f,0.3947f,0.3799f},
            new float[] {0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.7000f,0.7857f,0.9531f,0.9531f,0.9531f,0.9538f,0.9538f,1.0000f,0.9848f,0.9831f,0.6667f,0.7500f,1.0000f,0.1071f,0.3590f,0.3462f,0.3457f,0.3034f,0.6063f,0.5302f,0.6326f,0.8333f,0.6250f,0.5496f,0.5469f,0.5469f,0.4318f},
            new float[] {0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.7000f,0.7857f,0.9531f,0.9531f,0.9531f,0.9545f,0.9565f,1.0000f,1.0000f,1.0000f,1.0000f,1.0000f,1.0000f,1.0000f,0.9655f,0.3544f,0.3544f,0.5620f,0.5625f,0.8300f,0.8232f,0.8421f,0.6691f,0.6528f,0.5708f,0.5610f,0.4550f},
            new float[] {0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.7000f,0.9531f,0.9531f,0.9545f,0.9552f,0.9559f,1.0000f,1.0000f,0.9737f,0.9701f,0.9677f,0.8333f,0.8182f,0.7500f,0.9091f,0.9688f,0.9878f,0.9022f,0.8776f,0.8861f,0.8343f,0.7927f,0.6255f,0.6134f,0.5000f,0.4466f,0.4581f},
            new float[] {0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.7000f,0.9500f,0.9545f,0.9552f,0.9559f,0.9583f,1.0000f,0.9726f,0.9706f,0.9710f,0.9683f,0.8182f,0.8182f,0.8462f,0.9302f,0.9762f,0.9912f,0.9206f,0.9324f,0.9136f,0.8462f,0.7952f,0.6481f,0.5882f,0.5833f,0.5000f,0.5198f},
            new float[] {0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.5952f,0.9434f,0.9259f,0.9273f,0.9524f,0.9516f,0.9552f,0.9630f,1.0000f,0.8039f,0.9726f,0.9701f,0.9826f,0.9823f,0.9683f,0.8750f,0.9545f,0.9388f,0.9790f,0.9862f,0.9600f,0.9363f,0.9528f,0.9059f,0.8770f,0.6777f,0.6507f,0.6552f,0.5302f,0.5343f},
            new float[] {0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0000f,0.0097f,0.0415f,0.0000f,0.3876f,0.5253f,0.7445f,0.8110f,0.8240f,0.8268f,1.0000f,1.0000f,1.0000f,0.7917f,0.8077f,0.7732f,0.8483f,0.8435f,0.8296f,0.9524f,0.9531f,0.9787f,0.9600f,0.9770f,0.9730f,0.9800f,0.9605f,0.9469f,0.9610f,0.9419f,0.9219f,0.8404f,0.7399f,0.7625f,0.7304f,0.6327f},
            new float[] {0.0000f,0.0000f,0.0000f,0.0000f,0.0010f,0.0039f,0.0106f,0.0491f,0.3118f,0.4225f,0.6939f,0.7305f,0.7984f,0.8268f,0.8268f,0.8650f,1.0000f,0.8765f,0.8230f,0.7895f,0.8503f,0.8456f,0.8446f,0.8435f,0.7229f,0.9559f,0.9684f,0.9712f,0.9789f,0.9774f,0.9906f,0.9915f,0.9820f,0.9839f,0.9707f,0.9520f,0.9089f,0.8866f,0.8438f,0.7825f,0.7695f},
            new float[] {0.0000f,0.0000f,0.0000f,0.0008f,0.0011f,0.0048f,0.0144f,0.0381f,0.3089f,0.3789f,0.7025f,0.7324f,0.8000f,0.8344f,0.8477f,0.7660f,0.7713f,0.6963f,0.8788f,0.8291f,0.8562f,0.8456f,0.7767f,0.7788f,0.7356f,0.9571f,0.9706f,0.9816f,0.9897f,0.9864f,0.9951f,0.9878f,0.9905f,0.9911f,0.9827f,0.9852f,0.9314f,0.9243f,0.8970f,0.8922f,0.9022f},
            new float[] {0.0000f,0.0000f,0.0008f,0.0009f,0.0012f,0.0045f,0.0134f,0.1788f,0.2186f,0.5270f,0.6713f,0.6667f,0.7088f,0.6859f,0.6806f,0.7108f,0.7157f,0.6282f,0.6957f,0.7007f,0.8293f,0.7850f,0.7636f,0.8365f,0.8357f,0.9796f,0.9848f,0.9886f,0.9886f,0.9953f,0.9844f,0.9893f,0.9901f,0.9905f,0.9890f,0.9902f,0.9582f,0.9443f,0.9370f,0.9298f,0.9116f},
            new float[] {0.0000f,0.0000f,0.0007f,0.0008f,0.0014f,0.0055f,0.0174f,0.2077f,0.2356f,0.3799f,0.5390f,0.6170f,0.6544f,0.6250f,0.6129f,0.6391f,0.5502f,0.4709f,0.4683f,0.5650f,0.7034f,0.7055f,0.7857f,0.8564f,0.7772f,0.8889f,0.9338f,0.9582f,0.9659f,0.9921f,0.9890f,0.9898f,0.9901f,0.9887f,0.9833f,0.9749f,0.9530f,0.9458f,0.9405f,0.9436f,0.9390f},
            new float[] {0.0000f,0.0000f,0.0006f,0.0009f,0.0016f,0.0079f,0.0628f,0.1626f,0.2222f,0.2787f,0.4842f,0.4907f,0.5380f,0.5349f,0.4254f,0.5185f,0.5470f,0.4232f,0.4118f,0.3366f,0.5149f,0.7673f,0.6375f,0.6440f,0.6241f,0.7381f,0.7979f,0.8674f,0.9475f,0.9495f,0.9448f,0.9814f,0.9758f,0.9790f,0.9731f,0.9728f,0.9743f,0.9440f,0.9328f,0.9421f,0.9510f},
            new float[] {0.0001f,0.0001f,0.0008f,0.0011f,0.0017f,0.0081f,0.0408f,0.0674f,0.1798f,0.2613f,0.3507f,0.4257f,0.4809f,0.5613f,0.4410f,0.4165f,0.3875f,0.3223f,0.2459f,0.2973f,0.4488f,0.5428f,0.6223f,0.5939f,0.6136f,0.7493f,0.8038f,0.8653f,0.8703f,0.9408f,0.9401f,0.9416f,0.9612f,0.9550f,0.9558f,0.9360f,0.9365f,0.9504f,0.9474f,0.9535f,0.9574f},
            new float[] {0.0001f,0.0001f,0.0003f,0.0010f,0.0014f,0.0046f,0.0249f,0.0490f,0.0787f,0.1538f,0.1752f,0.2855f,0.3494f,0.3884f,0.3835f,0.2950f,0.3497f,0.3320f,0.2582f,0.3168f,0.3352f,0.4987f,0.6195f,0.5774f,0.6219f,0.7207f,0.8136f,0.8285f,0.8863f,0.9316f,0.9380f,0.9316f,0.9472f,0.9554f,0.9391f,0.9388f,0.9490f,0.9650f,0.9709f,0.9717f,0.9718f},
            new float[] {0.0001f,0.0001f,0.0003f,0.0005f,0.0012f,0.0034f,0.0181f,0.0396f,0.0698f,0.0989f,0.1760f,0.1940f,0.2840f,0.3147f,0.3390f,0.3211f,0.2291f,0.2605f,0.2506f,0.3298f,0.2995f,0.4000f,0.5591f,0.6066f,0.6400f,0.7208f,0.7750f,0.8211f,0.8582f,0.8845f,0.9269f,0.9279f,0.9446f,0.9457f,0.9355f,0.9456f,0.9631f,0.9670f,0.9695f,0.9702f,0.9729f},
            new float[] {0.0001f,0.0001f,0.0004f,0.0006f,0.0007f,0.0034f,0.0150f,0.0353f,0.0641f,0.0789f,0.1144f,0.1368f,0.1393f,0.2124f,0.2778f,0.2424f,0.2197f,0.2434f,0.2351f,0.3158f,0.2733f,0.4384f,0.5216f,0.5271f,0.5548f,0.6792f,0.7280f,0.8232f,0.8456f,0.8752f,0.9202f,0.9262f,0.9410f,0.9388f,0.9287f,0.9434f,0.9628f,0.9649f,0.9682f,0.9688f,0.9714f},
            new float[] {0.0001f,0.0001f,0.0004f,0.0007f,0.0007f,0.0018f,0.0114f,0.0284f,0.0441f,0.0893f,0.1156f,0.1380f,0.1402f,0.1983f,0.2236f,0.2510f,0.2253f,0.2381f,0.1873f,0.2060f,0.2588f,0.3321f,0.5161f,0.5326f,0.5455f,0.6685f,0.7227f,0.8118f,0.8205f,0.8601f,0.9073f,0.9171f,0.9322f,0.9297f,0.9249f,0.9444f,0.9614f,0.9623f,0.9658f,0.9680f,0.9704f},
            new float[] {0.0001f,0.0001f,0.0003f,0.0007f,0.0009f,0.0019f,0.0042f,0.0278f,0.0418f,0.0715f,0.1093f,0.1414f,0.1466f,0.2091f,0.1846f,0.2097f,0.1882f,0.1746f,0.2006f,0.1858f,0.2837f,0.3212f,0.4053f,0.5315f,0.5387f,0.6386f,0.7052f,0.7892f,0.8129f,0.8747f,0.8795f,0.8964f,0.9250f,0.9235f,0.9140f,0.9385f,0.9610f,0.9629f,0.9659f,0.9676f,0.9729f}
            };
    }
}
