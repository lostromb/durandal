namespace Durandal
{
    using Durandal.Common.Time;
    using Durandal.API;
        using Durandal.Common.Compression;
    using Durandal.Common.Compression.LZ4;
    using Durandal.Common.Config;
    using Durandal.Common.File;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.Logger;
    using Durandal.Common.LU;
    using Durandal.Common.NLP;
    using Durandal.Common.NLP.Annotation;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.Statistics.Ranking;
    using Durandal.Common.Ontology;
    using Durandal.Common.NLP.Train;
    using Durandal.Common.Utils;
    using Durandal.Common.Collections;
    using Durandal.Common.IO;
    using Durandal.Common.Tasks;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Durandal.Common.ServiceMgmt;

    public class LanguageUnderstandingEngine : IDisposable
    {
        private LUConfiguration _luConfig;
        private IDictionary<LanguageCode, LanguageModel> _loadedModels = new Dictionary<LanguageCode, LanguageModel>();
        private NLPToolsCollection _languageTools = new NLPToolsCollection();
        private IDictionary<LanguageCode, IList<IAnnotator>> _annotators = new Dictionary<LanguageCode, IList<IAnnotator>>();
        private ISet<LanguageCode> _enabledLocales = new HashSet<LanguageCode>();
        private IAnnotatorProvider _annotatorFactory;
        private IThreadPool _threadPool;
        private DateTimeOffset _lastModelLoad = default(DateTimeOffset);

        private readonly ILogger _logger;
        private readonly IFileSystem _fileSystem;

        private bool _initialized = false;
        private int _disposed = 0;


        /// <summary>
        /// This is a watchdog thread that waits for new models specs to come in, and then does the work of training the model and hot-swapping it
        /// into the global _loadedModels map.
        /// </summary>
        private Task _modelLoadThread = null;

        private CancellationTokenSource _modelLoadThreadCancellizer;

        /// <summary>
        /// When new models are scheduled to be loaded, their specification is put here to be picked up by the watchdog.
        /// When multiple LoadModel() events are queued, this data structure only keeps the most recent one per locale
        /// </summary>
        private MultichannelQueue<LanguageCode, ModelLoadSpecification> _modelLoadQueue;

        /// <summary>
        /// Internal signal that a model definition is ready to be loaded and should be picked up from the queue
        /// </summary>
        private AutoResetEvent _readyToLoadModel = new AutoResetEvent(false);

        /// <summary>
        /// Used to lock access to _loadedModels in the presence of asyncronous model training
        /// </summary>
        private ReaderWriterLockAsync _loadedModelMutex = new ReaderWriterLockAsync();

        public LanguageUnderstandingEngine(LUConfiguration luConfig,
            ILogger logger,
            IFileSystem fileSystem,
            IAnnotatorProvider annotatorFactory,
            IThreadPool threadPool)
        {
            _logger = logger;
            _fileSystem = fileSystem;
            _luConfig = luConfig;
            _annotatorFactory = annotatorFactory;
            _modelLoadQueue = new MultichannelQueue<LanguageCode, ModelLoadSpecification>();
            _threadPool = threadPool;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~LanguageUnderstandingEngine()
        {
            Dispose(false);
        }
#endif

        public void Initialize(IEnumerable<LanguageCode> supportedLocales, ICultureInfoFactory cultureInfoFactory, IRealTimeProvider realTime)
        {
            _logger.Log("Initializing LU Core");
           
            foreach (LanguageCode locale in supportedLocales)
            {
                if (_enabledLocales.Contains(locale))
                {
                    continue;
                }

                _logger.Log("Initializing tools for locale " + locale);

                _languageTools.Add(locale, NLPTools.BuildToolsForLocale(locale, _logger, _fileSystem, cultureInfoFactory));

                // Load all annotators via the AnnotatorFactory
                _annotators.Add(locale, new List<IAnnotator>());
                
                ISet<string> annotatorsToLoad = _luConfig.AnnotatorsToLoad;
                Durandal.Common.Collections.IReadOnlySet<string> availableAnnotators = _annotatorFactory.GetAllAnnotators();

                foreach (string annoName in annotatorsToLoad)
                {
                    if (availableAnnotators.Contains(annoName))
                    {
                        IAnnotator annotator = _annotatorFactory.CreateAnnotator(annoName, locale, _logger);
                        if (annotator != null)
                        {
                            _annotators[locale].Add(annotator);
                        }
                    }
                }
                
                _enabledLocales.Add(locale);
            }

            // Start the model load watchdog thread
            RunModelLoadThread(realTime);
            
            _initialized = true;
        }

        /// <summary>
        /// Returns true if the engine is initialized and ready to load models
        /// </summary>
        public bool Initialized
        {
            get { return _initialized; }
        }

        /// <summary>
        /// Returns true if any language models are loaded
        /// </summary>
        public bool AnyModelLoaded
        {
            get
            {
                int hLock = _loadedModelMutex.EnterReadLock();
                try
                {
                    return _loadedModels.Count != 0;
                }
                finally
                {
                    _loadedModelMutex.ExitReadLock(hLock);
                }
            }
        }
        
        /// <summary>
        /// Returns the set of all models loaded in LU as a list of strings in the form "domain:locale";
        /// </summary>
        public IList<string> LoadedModels
        { 
            get
            {
                int hLock = _loadedModelMutex.EnterReadLock();
                try
                {
                    List<string> allModels = new List<string>();
                    foreach (var loadedModel in _loadedModels)
                    {
                        foreach (string domain in loadedModel.Value.Domains)
                        {
                            allModels.Add(domain + ":" + loadedModel.Key);
                        }
                    }

                    return allModels;
                }
                finally
                {
                    _loadedModelMutex.ExitReadLock(hLock);
                }
            }
        }

        /// <summary>
        /// Returns the set of domains currently loaded across all locales
        /// </summary>
        public ISet<string> LoadedDomains
        {
            get
            {
                int hLock = _loadedModelMutex.EnterReadLock();
                try
                {
                    ISet<string> returnVal = new HashSet<string>();
                    foreach (var loadedModel in _loadedModels)
                    {
                        foreach (string domain in loadedModel.Value.Domains)
                        {
                            if (!returnVal.Contains(domain))
                            {
                                returnVal.Add(domain);
                            }
                        }
                    }

                    return returnVal;
                }
                finally
                {
                    _loadedModelMutex.ExitReadLock(hLock);
                }
            }
        }

        /// <summary>
        /// Gets the list of all package files that are in the current environment (not necessarily loaded)
        /// </summary>
        public string Packages
        {
            get
            {
                List<string> packageNames = /*PackageInstaller.*/GetAllPackageNames(_fileSystem);
                if (packageNames.Count == 0)
                {
                    return "none";
                }
                else
                {
                    return string.Join(",", packageNames);
                }
            }
        }

        public ISet<LanguageCode> EnabledLocales
        {
            get
            {
                return _enabledLocales;
            }
        }

        public DateTimeOffset LastModelLoadTime
        {
            get
            {
                return _lastModelLoad;
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

            if (_modelLoadThread != null)
            {
                _modelLoadThreadCancellizer.Cancel();
                _modelLoadThread.Wait(500);
                _modelLoadThreadCancellizer.Dispose();
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                _loadedModelMutex.Dispose();
                _readyToLoadModel.Dispose();
            }
        }

        /// <summary>
        /// Instructs LU to begin loading a new model. The actual work is done asynchronously, so this method will return immediately
        /// </summary>
        /// <param name="locale">The locale of the model to load</param>
        /// <param name="domainsToInclude">The set of domains to load. If null, loads all domains for which there is training</param>
        /// <param name="customReranker">A reranker to use for the model</param>
        public bool LoadModels(LanguageCode locale, IEnumerable<string> domainsToInclude = null, IReranker customReranker = null)
        {
            if (!_enabledLocales.Contains(locale))
            {
                _logger.Log("LU is not configured to support the locale " + locale, LogLevel.Err);
                return false;
            }

            // Make a local copy of the domain list
            List<string> allDomains = new List<string>();
            if (domainsToInclude != null)
            {
                allDomains.AddRange(domainsToInclude);
            }

            if (allDomains.Count == 0 || (allDomains.Count == 1 && allDomains[0].Equals("*")))
            {
                // If the included domains are empty, look around for all known domains and just use those
                allDomains.Clear();
                allDomains.FastAddRangeCollection(TrainingDataManager.GetAllKnownDomains(locale, _fileSystem, _logger));
            }

            // Queue up a loading spec, overwriting any existing ones for the locale if necessary
            _modelLoadQueue.Enqueue(locale, new ModelLoadSpecification(locale, allDomains, customReranker));

            // And signal the loading thread to pick it up
            _readyToLoadModel.Set();
            _logger.Log("Async model load work item queued for locale " + locale);
            return true;
        }

        private void RunModelLoadThread(IRealTimeProvider realTime)
        {
            _modelLoadThreadCancellizer = new CancellationTokenSource();
            CancellationToken modelThreadCancelToken = _modelLoadThreadCancellizer.Token;
            IRealTimeProvider threadTime = realTime.Fork("LUModelLoadThread");
            _modelLoadThread = DurandalTaskExtensions.LongRunningTaskFactory.StartNew(async () =>
                {
                    try
                    {
                        while (!modelThreadCancelToken.IsCancellationRequested)
                        {
                            // Wait for the signal that a new load spec is available
                            bool gotSignal = _readyToLoadModel.WaitOne(100);
                            if (!gotSignal && threadTime.IsForDebug)
                            {
                                // Consume virtual time if applicable
                                await threadTime.WaitAsync(TimeSpan.FromMilliseconds(100), modelThreadCancelToken).ConfigureAwait(false);
                            }

                            if (gotSignal)
                            {
                                // Then see if there's a load spec on the queue
                                LanguageCode nextKey;
                                ModelLoadSpecification nextSpec;
                                if (_modelLoadQueue.TryDequeue(out nextKey, out nextSpec))
                                {
                                    await LoadModelOnThread(nextSpec, threadTime).ConfigureAwait(false);
                                }

                                // If another model is queued to load, set the trigger now to make sure we load it on the next pass
                                if (_modelLoadQueue.Count > 0)
                                {
                                    _readyToLoadModel.Set();
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.Log(e, LogLevel.Err);
                        _logger.Log("Exception caught while loading new model on thread!", LogLevel.Err);
                    }
                    finally
                    {
                        threadTime.Merge();
                    }
                });

            //_modelLoadThread.IsBackground = true;
            //_modelLoadThread.Name = "LU Model Load Thread";
            //_modelLoadThread.Start();
        }

        private async Task LoadModelOnThread(ModelLoadSpecification spec, IRealTimeProvider realTime)
        {
            _logger.Log("Beginning to load new model for locale " + spec.Locale);

            if (spec.CustomReranker == null)
            {
                spec.CustomReranker = new NullReranker();
            }

            TrainingDataManager trainingProvider = new TrainingDataManager(_logger.Clone("TrainingDataManager"), spec.Locale, _fileSystem, _threadPool);

            // Load the training data
            _logger.Log("Validating training cache...");
            await trainingProvider.ValidateTrainingChecksums(_fileSystem, realTime).ConfigureAwait(false);
            _logger.Log("Expanding training templates...");
            trainingProvider.LoadTrainingData(realTime);
            _logger.Log("Expanding validation templates...");
            trainingProvider.LoadValidationData();
            _logger.Log("Done loading training data.");

            NLPTools nlTools;
            if (!_languageTools.TryGetNLPTools(spec.Locale, out nlTools))
            {
                throw new KeyNotFoundException("Can't find NL tools for locale " + spec.Locale.ToBcp47Alpha2String());
            }

            LanguageModel nextModel = new LanguageModel(
                _luConfig,
                _logger.Clone("LanguageModel"),
                _fileSystem,
                spec.DomainsToInclude,
                spec.Locale,
                trainingProvider,
                nlTools,
                spec.CustomReranker,
                _threadPool);

            nextModel.Train(realTime);
            _logger.Log("Model trained");

            int hLock = _loadedModelMutex.EnterWriteLock();
            try
            {
                _loadedModels[spec.Locale] = nextModel;
                _logger.Log("Finished loading " + spec.Locale + " model");

                // Reset all annotators
                if (_annotators.ContainsKey(spec.Locale))
                {
                    _logger.Log("Resetting annotators...");
                    foreach (IAnnotator annotator in _annotators[spec.Locale])
                    {
                        annotator.Reset();
                    }
                }

                _lastModelLoad = realTime.Time;
            }
            finally
            {
                _loadedModelMutex.ExitWriteLock(hLock);
                GC.Collect();
            }
        }

        public void ValidateModels(LanguageCode locale)
        {
            int hLock = _loadedModelMutex.EnterReadLock();
            try
            {
                if (!_loadedModels.ContainsKey(locale))
                {
                    _logger.Log("Cannot validate model because no model is loaded for locale " + locale + "!", LogLevel.Err);
                }
                else
                {
                    _loadedModels[locale].ValidateModels();
                }
            }
            finally
            {
                _loadedModelMutex.ExitReadLock(hLock);
            }
        }

        /// <summary>
        /// fixme: this is part of PackageInstaller but I copied it here because of PCL compatibility
        /// </summary>
        /// <param name="fileSystem"></param>
        /// <returns></returns>
        private static List<string> GetAllPackageNames(IFileSystem fileSystem)
        {
            List<string> returnVal = new List<string>();

            VirtualPath packageDir = new VirtualPath(RuntimeDirectoryName.PACKAGE_DIR);

            if (fileSystem.Exists(packageDir))
            {
                foreach (VirtualPath file in fileSystem.ListFiles(packageDir))
                {
                    if (file.Extension.Equals(".dupkg", StringComparison.OrdinalIgnoreCase))
                    {
                        returnVal.Add(file.Name);
                    }
                }
            }

            return returnVal;
        }

        /// <summary>
        /// The primary method of this object. Accepts a request for classification (potentially containing multiple queries) and returns
        /// a response containing classification results for each of those queries, processed according to the currently loaded locales and models.
        /// </summary>
        /// <param name="request">The request to be processed</param>
        /// <param name="realTime">Real time</param>
        /// <param name="queryLogger">A logger for the whole request</param>
        /// <returns>The LU classification results</returns>
        public async Task<LUResponse> Classify(LURequest request, IRealTimeProvider realTime, ILogger queryLogger)
        {
            LUResponse response = new LUResponse();

            if (queryLogger == null)
            {
                queryLogger = NullLogger.Singleton;
            }
            response.TraceId = request.TraceId;

            queryLogger.DispatchAsync(
                (delegateLogger, timestamp) =>
                {
                    IDictionary<DataPrivacyClassification, JToken> impressionsDividedByPrivacyClass =
                        CommonInstrumentation.SplitObjectByPrivacyClass(
                            CommonInstrumentation.PrependPath(CommonInstrumentation.ToJObject(request), "$.LU.Request"),
                            delegateLogger.DefaultPrivacyClass,
                            CommonInstrumentation.GetPrivacyMappingsLURequest(),
                            delegateLogger);

                    using (PooledStringBuilder builder = StringBuilderPool.Rent())
                    {
                        foreach (var classifiedLogMsg in impressionsDividedByPrivacyClass)
                        {
                            delegateLogger.Log(
                                CommonInstrumentation.FromJObject(classifiedLogMsg.Value, builder.Builder),
                                LogLevel.Ins,
                                delegateLogger.TraceId,
                                classifiedLogMsg.Key,
                                timestamp);
                            builder.Builder.Length = 0;
                        }
                    }
                });
            
            // Verify that the protocol version is supported
            int expectedProtocolVersion = new LURequest().ProtocolVersion;
            if (request.ProtocolVersion != expectedProtocolVersion)
            {
                queryLogger.Log("Client is using an outdated or unsupported protocol version \"" + request.ProtocolVersion + "\"", LogLevel.Wrn);
            }

            // Verify the locale
            if (!_loadedModels.ContainsKey(request.Locale))
            {
                queryLogger.Log("No LanguageModel is loaded to classify the query! (request locale is " + request.Locale + ")", LogLevel.Err);
                return response;
            }

            // Copy domain scope from the request
            ISet<string> domainScope = null;
            if (request.DomainScope != null)
            {
                domainScope = new HashSet<string>(request.DomainScope);
            }

            // Copy contextual domains from the request
            ISet<string> contextualDomains = null;
            if (request.ContextualDomains != null)
            {
                contextualDomains = new HashSet<string>(request.ContextualDomains);
            }

            // Convert text + speech inputs into a flat set of LUInputs, until I can figure out how to better incorporate text + speech into the LU pipe
            List<LUInput> luInputs = new List<LUInput>();
            if (!string.IsNullOrEmpty(request.TextInput))
            {
                luInputs.Add(new LUInput()
                {
                    Utterance = request.TextInput,
                    LexicalForm = string.Empty
                });
            }
            if (request.SpeechInput != null && request.SpeechInput.RecognizedPhrases != null)
            {
                foreach (var recoPhrase in request.SpeechInput.RecognizedPhrases)
                {
                    string text = recoPhrase.DisplayText;
                    if (recoPhrase.InverseTextNormalizationResults != null && recoPhrase.InverseTextNormalizationResults.Count > 0 && !string.IsNullOrEmpty(recoPhrase.InverseTextNormalizationResults[0]))
                    {
                        text = recoPhrase.InverseTextNormalizationResults[0];
                    }

                    luInputs.Add(new LUInput()
                    {
                        Utterance = text,
                        LexicalForm = recoPhrase.IPASyllables
                    });
                }
            }

            int hLock = await _loadedModelMutex.EnterReadLockAsync().ConfigureAwait(false);
            try
            {
                TrainingDataManager trainingProvider = _loadedModels[request.Locale].TrainingProvider;
                if (luInputs.Count == 1)
                {
                    // Only one utterance - no need to use a thread pool and incur overhead
                    using (UtteranceClassifierThread thread = new UtteranceClassifierThread(
                        luInputs[0],
                        request,
                        queryLogger,
                        domainScope,
                        contextualDomains,
                        _loadedModels,
                        _annotators,
                        _fileSystem,
                        _enabledLocales,
                        _luConfig.TaggerRunThreshold,
                        trainingProvider,
                        CancellationToken.None,
                        realTime))
                    {
                        await thread.Run().ConfigureAwait(false);
                        response.Results.Add(thread.Join());
                    }
                }
                else
                {
                    // More than one utterance - use multithreading
                    IList<UtteranceClassifierThread> threads = new List<UtteranceClassifierThread>();
                    foreach (LUInput utterance in luInputs)
                    {
#pragma warning disable CA2000 // Dispose objects before losing scope
                        UtteranceClassifierThread thread = new UtteranceClassifierThread(
                            utterance,
                            request,
                            queryLogger,
                            domainScope,
                            contextualDomains,
                            _loadedModels,
                            _annotators,
                            _fileSystem,
                            _enabledLocales,
                            _luConfig.TaggerRunThreshold,
                            trainingProvider,
                            CancellationToken.None,
                            realTime);
#pragma warning restore CA2000 // Dispose objects before losing scope
                        threads.Add(thread);
                        _threadPool.EnqueueUserAsyncWorkItem(thread.Run);
                    }

                    // Note: There is an implicit contract that the results will come out in the same order as the input.
                    // That's why we use this change-then-commit pattern which preserves the ordering of the threads as they were created
                    foreach (UtteranceClassifierThread thread in threads)
                    {
                        response.Results.Add(thread.Join());
                        thread.Dispose();
                    }
                }
            }
            finally
            {
                _loadedModelMutex.ExitReadLock(hLock);
            }

            // If there is more than one speech hyp, do homophone analysis
            // FIXME This all needs to change now that we have access to full SR results and confusion network, etc.
            if (request.SpeechInput != null && luInputs.Count > 1)
            {
                IDictionary<string, HashSet<string>> allSlotHomophones = new Dictionary<string, HashSet<string>>();

                foreach (RecognizedPhrase result in response.Results)
                {
                    ExtractHomophones(result.Recognition, ref allSlotHomophones);
                }

                foreach (RecognizedPhrase result in response.Results)
                {
                    // Append homophone alternates to the final slot values for all hyps
                    AppendHomophonesToResult(result.Recognition, allSlotHomophones);
                }
            }

            foreach (RecognizedPhrase result in response.Results)
            {
                queryLogger.LogFormat(LogLevel.Std, DataPrivacyClassification.PrivateContent, "Classification results for \"{0}\"", result.Utterance);
                if (result.Recognition.Count > 0)
                {
                    // Log the results. These used to go to the file logger only and ignore console, but I've recently changed that back so LU is not totally silent.
                    foreach (RecoResult reco in result.Recognition)
                    {
                        LogSemanticFrame(reco, queryLogger, LogLevel.Std);
                    }

                    bool highSentiments = false;
                    foreach (var sentiment in result.Sentiments)
                    {
                        if (sentiment.Value > 0.5)
                        {
                            highSentiments = true;
                            break;
                        }
                    }

                    if (highSentiments)
                    {
                        queryLogger.Log("Sentiments:", LogLevel.Std);
                        foreach (var sentiment in result.Sentiments)
                        {
                            if (sentiment.Value > 0.5)
                            {
                                queryLogger.Log("  " + sentiment.Key + " = " + sentiment.Value, LogLevel.Std);
                            }
                        }
                    }
                }
            }

            queryLogger.DispatchAsync(
                (delegateLogger, timestamp) =>
                {
                    IDictionary<DataPrivacyClassification, JToken> impressionsDividedByPrivacyClass =
                        CommonInstrumentation.SplitObjectByPrivacyClass(
                            CommonInstrumentation.PrependPath(CommonInstrumentation.ToJObject(response), "$.LU.Response"),
                            delegateLogger.DefaultPrivacyClass,
                            CommonInstrumentation.GetPrivacyMappingsLUResponse(),
                            delegateLogger);

                    foreach (var classifiedLogMsg in impressionsDividedByPrivacyClass)
                    {
                        delegateLogger.Log(
                            CommonInstrumentation.FromJObject(classifiedLogMsg.Value),
                            LogLevel.Ins,
                            delegateLogger.TraceId,
                            classifiedLogMsg.Key,
                            timestamp);
                    }
                });
            
            // If the query enables instant tracing, do the trace now
            if (request.RequestFlags.HasFlag(QueryFlags.Trace))
            {
                await queryLogger.Flush(CancellationToken.None, realTime, true).ConfigureAwait(false);
                EventOnlyLogger eventLogger = EventOnlyLogger.TryExtractFromAggregate(queryLogger);
                if (eventLogger != null)
                {
                    ILoggingHistory history = eventLogger.History;
                    response.TraceInfo = new List<InstrumentationEvent>();
                    FilterCriteria filter = new FilterCriteria()
                    {
                        TraceId = queryLogger.TraceId,
                        //Level = LogLevel.Std | LogLevel.Err | LogLevel.Wrn | LogLevel.Vrb
                    };

                    foreach (LogEvent e in history.FilterByCriteria(filter))
                    {
                        // TODO decrypt messages?
                        response.TraceInfo.Add(InstrumentationEvent.FromLogEvent(e));
                    }
                }
            }

            return response;
        }

        private void ExtractHomophones(IEnumerable<RecoResult> recoResults, ref IDictionary<string, HashSet<string>> outputSet)
        {
            foreach (RecoResult r in recoResults)
            {
                HashSet<string> homophonesFoundInThisResult = new HashSet<string>();
                foreach (TaggedData tagHyp in r.TagHyps)
                {
                    if (tagHyp.Slots == null)
                        continue;
                    foreach (SlotValue s in tagHyp.Slots)
                    {
                        if (!homophonesFoundInThisResult.Contains(s.Value))
                        {
                            string combinedKey = r.Domain + "/" + r.Intent + "/" + s.Name;
                            if (!outputSet.ContainsKey(combinedKey))
                            {
                                outputSet[combinedKey] = new HashSet<string>();
                            }
                            if (!outputSet[combinedKey].Contains(s.Value))
                            {
                                outputSet[combinedKey].Add(s.Value);
                            }
                        }
                    }
                }
            }
        }

        private void AppendHomophonesToResult(IEnumerable<RecoResult> recoResults, IDictionary<string, HashSet<string>> homophoneSet)
        {
            foreach (RecoResult rankedHyp in recoResults)
            {
                foreach (TaggedData tagHyp in rankedHyp.TagHyps)
                {
                    if (tagHyp.Slots == null)
                        continue;
                    foreach (SlotValue slot in tagHyp.Slots)
                    {
                        string combinedKey = rankedHyp.Domain + "/" + rankedHyp.Intent + "/" + slot.Name;
                        if (homophoneSet.ContainsKey(combinedKey))
                        {
                            foreach (string potentialAlternate in homophoneSet[combinedKey])
                            {
                                if (!slot.Value.Equals(potentialAlternate, StringComparison.Ordinal))
                                {
                                    slot.Alternates.Add(potentialAlternate);
                                }
                            }
                        }
                    }
                }
            }
        }

        private class UtteranceClassifierThread : IDisposable
        {
            private readonly LUInput local_utterance;
            private readonly LURequest local_request;
            private readonly ILogger local_queryLogger;
            private readonly ISet<string> local_domainScope;
            private readonly ISet<string> local_contextualDomains;
            private readonly IDictionary<LanguageCode, LanguageModel> local_loadedModels;
            private readonly IDictionary<LanguageCode, IList<IAnnotator>> local_annotators;
            private readonly IFileSystem local_fileSystem;
            private readonly ISet<LanguageCode> local_enabledLocales;
            private readonly float local_taggerRunThreshold;
            private readonly TrainingDataManager local_trainingProvider;
            private readonly IRealTimeProvider local_realTime;
            private readonly CancellationToken local_cancelToken;

            private EventWaitHandle _finished = new EventWaitHandle(false, EventResetMode.ManualReset);
            private RecognizedPhrase _returnVal = null;
            private int _disposed = 0;

            public UtteranceClassifierThread(LUInput utterance,
                LURequest request,
                ILogger queryLogger,
                ISet<string> domainScope,
                ISet<string> contextualDomains,
                IDictionary<LanguageCode, LanguageModel> loadedModels,
                IDictionary<LanguageCode, IList<IAnnotator>> annotators,
                IFileSystem fileSystem,
                ISet<LanguageCode> enabledLocales,
                float taggerRunThreshold,
                TrainingDataManager trainingProvider,
                CancellationToken cancelToken,
                IRealTimeProvider realTime)
            {
                local_utterance = utterance;
                local_request = request;
                local_queryLogger = queryLogger;
                local_domainScope = domainScope;
                local_contextualDomains = contextualDomains;
                local_loadedModels = loadedModels;
                local_annotators = annotators;
                local_fileSystem = fileSystem;
                local_enabledLocales = enabledLocales;
                local_taggerRunThreshold = taggerRunThreshold;
                local_trainingProvider = trainingProvider;
                local_realTime = realTime;
                local_cancelToken = cancelToken;
                DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
            }

#if TRACK_IDISPOSABLE_LEAKS
            ~UtteranceClassifierThread()
            {
                Dispose(false);
            }
#endif

            public RecognizedPhrase Join()
            {
                _finished.WaitOne();
                return _returnVal;
            }
            
            public async Task Run()
            {
                _returnVal = local_loadedModels[local_request.Locale].Classify(
                    local_utterance,
                    local_request.SpeechInput != null,
                    local_queryLogger,
                    local_realTime,
                    local_domainScope,
                    local_contextualDomains);

                KnowledgeContext entityContext = new KnowledgeContext();

                // Append annotation (time / ordinal) data to each recognition result
                if (local_request.DoFullAnnotation)
                {
                    foreach (RecoResult thisResult in _returnVal.Recognition)
                    {
                        if (thisResult.Confidence > local_taggerRunThreshold)
                        {
                            await DoExtendedAnnotation(
                                thisResult,
                                local_request,
                                local_queryLogger,
                                local_annotators,
                                local_fileSystem,
                                local_enabledLocales,
                                entityContext,
                                local_cancelToken,
                                local_realTime).ConfigureAwait(false);
                        }
                    }
                }

                // Serialize the entity context to a bond blob
                if (entityContext != null && !entityContext.IsEmpty)
                {
                    using (PooledBuffer<byte> serializedContext = KnowledgeContextSerializer.SerializeKnowledgeContext(entityContext))
                    {
                        byte[] copiedEntityContext = new byte[serializedContext.Length];
                        ArrayExtensions.MemCopy(serializedContext.Buffer, 0, copiedEntityContext, 0, copiedEntityContext.Length);
                        _returnVal.EntityContext = new ArraySegment<byte>(copiedEntityContext);
                    }
                }

                _finished.Set();
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
                    _finished?.Dispose();
                }
            }

            private async Task DoExtendedAnnotation(
                RecoResult oneResult,
                LURequest origRequest,
                ILogger logger,
                IDictionary<LanguageCode, IList<IAnnotator>> annotators,
                IFileSystem fileSystem,
                ISet<LanguageCode> enabledLocales,
                KnowledgeContext entityContext,
                CancellationToken cancelToken,
                IRealTimeProvider realTime)
            {
                if (annotators.ContainsKey(origRequest.Locale))
                {
                    // Try and retrieve the model configuration for this domain, which could contain annotator configuration data
                    bool disposeConfig = false;
                    IConfiguration domainConfig;
                    if (enabledLocales.Contains(origRequest.Locale))
                    {
                        domainConfig = local_trainingProvider.GetDomainConfiguration(oneResult.Domain, realTime);
                    }
                    else
                    {
#pragma warning disable CA2000 // Dispose objects before losing scope
                        domainConfig = new InMemoryConfiguration(logger);
                        disposeConfig = true;
#pragma warning restore CA2000 // Dispose objects before losing scope
                    }

                    try
                    {
                        IList<IAnnotator> theseAnnotators = annotators[origRequest.Locale];
                        Task<object>[] concurrentAnnotatorResults = new Task<object>[theseAnnotators.Count];
                        ILogger[] annotatorLoggers = new ILogger[theseAnnotators.Count];
                        for (int c = 0; c < theseAnnotators.Count; c++)
                        {
                            IAnnotator thisAnno = theseAnnotators[c];
                            annotatorLoggers[c] = logger.Clone("Annotator-" + thisAnno.Name);
                            annotatorLoggers[c].LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "Running annotator \"{0}\" asynchronously", thisAnno.Name);
                            if (realTime.IsForDebug)
                            {
                                if (c == 0)
                                {
                                    // use this thread's time provider as the first annotator's time, to account for the "work" this thread is doing waiting for the annotators to finish
                                    concurrentAnnotatorResults[c] = thisAnno.AnnotateStateless(oneResult, origRequest, domainConfig, annotatorLoggers[c], cancelToken, realTime);
                                }
                                else
                                {
                                    IRealTimeProvider threadLocalTime = realTime.Fork("Annotator-" + thisAnno.Name);
                                    ILogger loggerClosure = annotatorLoggers[c];
                                    concurrentAnnotatorResults[c] = Task.Run<object>(async () =>
                                    {
                                        try
                                        {
                                            return await thisAnno.AnnotateStateless(oneResult, origRequest, domainConfig, loggerClosure, cancelToken, threadLocalTime).ConfigureAwait(false);
                                        }
                                        finally
                                        {
                                            threadLocalTime.Merge();
                                        }
                                    });
                                }

                            }
                            else
                            {
                                concurrentAnnotatorResults[c] = thisAnno.AnnotateStateless(oneResult, origRequest, domainConfig, annotatorLoggers[c], cancelToken, realTime);
                            }
                        }

                        // Let annotators run concurrently
                        foreach (Task<object> annotatorTask in concurrentAnnotatorResults)
                        {
                            await annotatorTask.ConfigureAwait(false);
                        }

                        // Now commit each result transactionally
                        for (int c = 0; c < theseAnnotators.Count; c++)
                        {
                            await theseAnnotators[c].CommitAnnotation(concurrentAnnotatorResults[c].Result, oneResult, origRequest, entityContext, domainConfig, annotatorLoggers[c], cancelToken, realTime).ConfigureAwait(false);
                        }
                    }
                    finally
                    {
                        if (disposeConfig)
                        {
                            domainConfig?.Dispose();
                        }
                    }
                }
                else
                {
                    logger.Log("No annotators are loaded for the locale " + origRequest.Locale, LogLevel.Wrn);
                }
            }
        }

        private static void LogSemanticFrame(RecoResult result, ILogger logger, LogLevel level)
        {
            logger.LogFormat(level, DataPrivacyClassification.SystemMetadata, "{0}/{1} : {2}", result.Domain, result.Intent, result.Confidence);
            if (result.TagHyps.Count > 0)
            {
                TaggedData data = result.MostLikelyTags;
                if (data.Slots.Count > 0)
                {
                    logger.LogFormat(level, DataPrivacyClassification.SystemMetadata, "  Tag confidence {0}", data.Confidence);
                }
                foreach (KeyValuePair<string, string> note in data.Annotations)
                {
                    logger.LogFormat(level, DataPrivacyClassification.PrivateContent, "  Annotation: \"{0}\" = \"{1}\"", note.Key, note.Value);
                }
                foreach (SlotValue tag in data.Slots)
                {
                    logger.LogFormat(level, DataPrivacyClassification.PrivateContent, "  Slot Value: \"{0}\" = \"{1}\"", tag.Name, tag.Value);
                    foreach (KeyValuePair<string, string> note in tag.Annotations)
                    {
                        logger.LogFormat(level, DataPrivacyClassification.PrivateContent, "    Slot Annotation: \"{0}\" = \"{1}\"", note.Key, note.Value);
                    }
                }
            }

            for (int c = 1; c < result.TagHyps.Count; c++)
            {
                TaggedData data = result.TagHyps[c];
                logger.LogFormat(level, DataPrivacyClassification.SystemMetadata, "  Alternate tag hypothesis {0}, confidence {1}", c, data.Confidence);
                foreach (SlotValue tag in data.Slots)
                {
                    logger.LogFormat(level, DataPrivacyClassification.PrivateContent, "    Slot Value: \"{0}\" = \"{1}\"", tag.Name, tag.Value);
                }
            }
        }

        /*private delegate void RunAnnotators(RecoResult oneResult, LURequest origRequest, ILogger logger);

        /// <summary>
        /// A single work item that can be queued to a thread pool
        /// to run extended annotation on an utterance
        /// </summary>
        private class WorkAtom
        {
            private readonly RunAnnotators _workItem;
            private readonly RecoResult _arg1;
            private readonly LURequest _arg2;
            private readonly ILogger _arg3;
            private readonly EventWaitHandle _taskComplete;

            public WorkAtom(RunAnnotators del, RecoResult arg1, LURequest arg2, ILogger arg3)
            {
                _workItem = del;
                _arg1 = arg1;
                _arg2 = arg2;
                _arg3 = arg3;
                _taskComplete = new EventWaitHandle(false, EventResetMode.AutoReset);
            }

            public void Run(object dummy)
            {
                _workItem(_arg1, _arg2, _arg3);
                _taskComplete.Set();
            }

            public EventWaitHandle WaitHandle
            {
                get
                {
                    return _taskComplete;
                }
            }
        }*/
    }
}
