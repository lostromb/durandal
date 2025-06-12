namespace Durandal.Common.LG.Statistical
{
    using Durandal.API;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Durandal.Common.Logger;
    using Durandal.Common.File;
    using System.Text.RegularExpressions;
    using System.IO;
    using Durandal.Common.Collections.Indexing;
    using Durandal.Common.NLP.Feature;
    using Durandal.Common.MathExt;
    using Durandal.Common.Utils;
    using Durandal.Common.NLP;
    using Durandal.Common.Config;
    using Durandal.Common.NLP.Language;

    /// <summary>
    /// A language generation engine which is powered by statistical learning processes. Each phrase in this engine
    /// is usually backed by a neural model which learns from lexical divergence &amp; word features to adapt the generated
    /// output to try and be grammatically correct. This engine is able to handle complex transformer chains, variable-length
    /// readouts, mixed locales, and more. The downside is that it requires time on initialization to train its required models.
    /// </summary>
    public class StatisticalLGEngine : ILGEngine
    {
        /// <summary>
        /// maps from phrase name with variants -> list of phrase variations
        /// </summary>
        private IDictionary<VariantConfig, IList<StatisticalLGPattern>> _phrases;
        
        private IDictionary<LocalizedKey, StatisticalLGCustomCodeWrapper> _customCodePhrases;

        /// <summary>
        /// maps from modelname:locale -> model
        /// </summary>
        private IDictionary<LocalizedKey, StatisticalLGPhrase> _models;

        private IDictionary<LocalizedKey, LgCommon.RunLGScript> _scripts;

        private IDictionary<string, Dictionary<string, string>> _translationTables;

        private ILogger _logger;
        private IRandom _rand;
        private string _domain;
        private ILGScriptCompiler _scriptCompiler;
        private IFileSystem _fileSystem;

        /// <summary>
        /// A default client context used if none else is available
        /// </summary>
        private readonly ClientContext EmptyClientContext = new ClientContext();

        public static async Task<StatisticalLGEngine> Create(
            IFileSystem fileSystem,
            ILogger logger,
            string domain,
            ILGScriptCompiler scriptCompiler,
            IList<VirtualPath> sourceFiles,
            INLPToolsCollection nlTools)
        {
            StatisticalLGEngine returnVal = new StatisticalLGEngine(fileSystem, logger, domain, scriptCompiler);
            await returnVal.Initialize(sourceFiles, nlTools).ConfigureAwait(false);
            return returnVal;
        }

        private StatisticalLGEngine(IFileSystem fileSystem,
            ILogger logger,
            string domain,
            ILGScriptCompiler scriptCompiler)
        {
            _logger = logger;
            _domain = domain;
            _scriptCompiler = scriptCompiler;
            _rand = new FastRandom(3);
            _phrases = new Dictionary<VariantConfig, IList<StatisticalLGPattern>>();
            _customCodePhrases = new Dictionary<LocalizedKey, StatisticalLGCustomCodeWrapper>();
            _scripts = new Dictionary<LocalizedKey, LgCommon.RunLGScript>();
            _fileSystem = fileSystem;
        }

        private async Task Initialize(IList<VirtualPath> sourceFiles, INLPToolsCollection nlTools)
        {
            List<ParsedStatisticalLGTemplate> parsedFiles = new List<ParsedStatisticalLGTemplate>();

            foreach (VirtualPath sourceFile in sourceFiles)
            {
                if (await _fileSystem.ExistsAsync(sourceFile).ConfigureAwait(false))
                {
                    ParsedStatisticalLGTemplate parsedFile = await ParseFile(sourceFile, _fileSystem).ConfigureAwait(false);
                    if (parsedFile != null)
                    {
                        parsedFiles.Add(parsedFile);
                    }
                }
                else
                {
                    _logger.Log("Statistical LG template file " + sourceFile + " doesn't exist! This should never happen", LogLevel.Err);
                }
            }

            // Train all of the phrases and then index them
            _models = new Dictionary<LocalizedKey, StatisticalLGPhrase>();
            _translationTables = new Dictionary<string, Dictionary<string, string>>();

            foreach (ParsedStatisticalLGTemplate parsedTemplate in parsedFiles)
            {
                LanguageCode fallbackLocale = null;
                IDictionary<LanguageCode, LanguageCode> localeMappings = new Dictionary<LanguageCode, LanguageCode>();
                IList<ScriptBlock> scriptBlocks = new List<ScriptBlock>();

                // Find locales that are not supported and calculate their fallbacks.
                // Then use that locale mapping to train models
                foreach (LanguageCode locale in parsedTemplate.SupportedLocales)
                {
                    NLPTools testTools;
                    LanguageCode supportedToolLocale;
                    if (nlTools.TryGetNLPTools(locale, out testTools, out supportedToolLocale))
                    {
                        if (testTools.FeaturizationWordBreaker == null)
                        {
                            _logger.Log("Wordbreaker is null for locale " + locale + ". Statistical LG may be broken or fall back to a different locale.", LogLevel.Wrn);
                        }
                        else if (testTools.LGFeatureExtractor == null)
                        {
                            _logger.Log("LG featurizer is null for locale " + locale + ". Statistical LG may be broken or fall back to a different locale.", LogLevel.Wrn);
                        }
                        else
                        {
                            // Set the "fallback" locale to be the first one that has tools which are supported by this template
                            fallbackLocale = supportedToolLocale;
                            break;
                        }
                    }
                }

                if (fallbackLocale == null)
                {
                    _logger.Log("NL tools are not initialized for any of the locales {" + string.Join(",", parsedTemplate.SupportedLocales) + "} in template file " + parsedTemplate.OriginalFileName + ". Statistical LG will not work.", LogLevel.Err);
                    continue;
                }

                foreach (LanguageCode potentialLocale in parsedTemplate.SupportedLocales)
                {
                    NLPTools testTools;
                    LanguageCode supportedToolsLocale;
                    if (nlTools.TryGetNLPTools(potentialLocale, out testTools, out supportedToolsLocale))
                    {
                        localeMappings[potentialLocale] = supportedToolsLocale;
                    }
                    else
                    {
                        localeMappings[potentialLocale] = fallbackLocale;
                    }
                }
                
                foreach (var block in parsedTemplate.Blocks)
                {
                    if (block.BlockType == TemplateFileBlockType.Model)
                    {
                        foreach (LanguageCode perceivedLocale in parsedTemplate.SupportedLocales)
                        {
                            ModelBlock model = block as ModelBlock;
                            TrainModelAndAddToCollection(
                                _domain,
                                model.Name,
                                model.TrainingLines,
                                _logger,
                                perceivedLocale,
                                localeMappings,
                                nlTools,
                                _fileSystem,
                                _models,
                                true);
                        }
                    }
                    else if (block.BlockType == TemplateFileBlockType.Script)
                    {
                        if (_scriptCompiler == null)
                        {
                            _logger.Log("The LG template file contains scripts, but no script compiler is enabled in the engine. Scripts will be ignored.", LogLevel.Err);
                        }
                        else
                        {
                            ScriptBlock script = block as ScriptBlock;
                            scriptBlocks.Add(script);
                        }
                    }
                    else if (block.BlockType == TemplateFileBlockType.Phrase)
                    {
                        // Keeps track of what locales we have already generated this phrase for. Only generate each
                        // pair of actual locale -> phrase once
                        Dictionary<LanguageCode, StatisticalLGPattern> patternsAlreadyCreated = new Dictionary<LanguageCode, StatisticalLGPattern>();
                        
                        foreach (LanguageCode perceivedLocale in parsedTemplate.SupportedLocales)
                        {
                            PhraseBlock phrase = block as PhraseBlock;
                            LanguageCode actualLocale = localeMappings[perceivedLocale];
                            NLPTools thisLocaleTools;
                            if (!nlTools.TryGetNLPTools(actualLocale, out thisLocaleTools))
                            {
                                throw new KeyNotFoundException("Can't find NL tools for locale " + actualLocale.ToBcp47Alpha2String());
                            }

                            Dictionary<string, string> thisPhraseVariants = new Dictionary<string, string>();

                            StatisticalLGPattern newPattern = new StatisticalLGPattern(_logger, EmptyClientContext, nlTools, this);
                            newPattern.Name = phrase.Name;
                            newPattern.Locale = actualLocale;
                            foreach (var property in phrase.Properties)
                            {
                                if (property is KeyValuePhraseProperty)
                                {
                                    KeyValuePhraseProperty kvp = property as KeyValuePhraseProperty;
                                    if (kvp.PropertyName.Equals("TextModel", StringComparison.OrdinalIgnoreCase))
                                    {
                                        newPattern.SetTextModel(new LocalizedKey(kvp.Value, actualLocale));
                                    }
                                    else if (kvp.PropertyName.Equals("ShortTextModel", StringComparison.OrdinalIgnoreCase))
                                    {
                                        newPattern.SetShortTextModel(new LocalizedKey(kvp.Value, actualLocale));
                                    }
                                    else if (kvp.PropertyName.Equals("SpokenModel", StringComparison.OrdinalIgnoreCase))
                                    {
                                        newPattern.SetSpokenModel(new LocalizedKey(kvp.Value, actualLocale));
                                    }
                                    else if (kvp.PropertyName.Equals("Text", StringComparison.OrdinalIgnoreCase))
                                    {
                                        // Generate a new model right here
                                        string modelName = "InlineModel-" + phrase.Name + _rand.NextInt();
                                        
                                        TrainModelAndAddToCollection(
                                            _domain,
                                            modelName,
                                            new string[] { kvp.Value },
                                            _logger,
                                            perceivedLocale,
                                            localeMappings,
                                            nlTools,
                                            _fileSystem,
                                            _models,
                                            false);
                                        
                                        newPattern.SetTextModel(new LocalizedKey(modelName, actualLocale));
                                    }
                                    else if (kvp.PropertyName.Equals("ShortText", StringComparison.OrdinalIgnoreCase))
                                    {
                                        string modelName = "InlineModel-" + phrase.Name + _rand.NextInt();

                                        TrainModelAndAddToCollection(
                                            _domain,
                                            modelName,
                                            new string[] { kvp.Value },
                                            _logger,
                                            perceivedLocale,
                                            localeMappings,
                                            nlTools,
                                            _fileSystem,
                                            _models,
                                            false);

                                        newPattern.SetShortTextModel(new LocalizedKey(modelName, actualLocale));
                                    }
                                    else if (kvp.PropertyName.Equals("Spoken", StringComparison.OrdinalIgnoreCase))
                                    {
                                        string modelName = "InlineModel-" + phrase.Name + _rand.NextInt();

                                        TrainModelAndAddToCollection(
                                            _domain,
                                            modelName,
                                            new string[] { kvp.Value },
                                            _logger,
                                            perceivedLocale,
                                            localeMappings,
                                            nlTools,
                                            _fileSystem,
                                            _models,
                                            false);

                                        newPattern.SetSpokenModel(new LocalizedKey(modelName, actualLocale));
                                    }
                                    else if (kvp.PropertyName.Equals("Script", StringComparison.OrdinalIgnoreCase))
                                    {
                                        newPattern.AddScriptName(kvp.Value);
                                    }
                                    else
                                    {
                                        // It's some field we don't know about. Assume it is custom LG data
                                        newPattern.SetExtraField(kvp.PropertyName, kvp.Value);
                                    }
                                }
                                else if (property is TransformerPhraseProperty)
                                {
                                    // Set the transformers for this pattern
                                    TransformerPhraseProperty transformerProp = property as TransformerPhraseProperty;
                                    newPattern.AddSlotTransformer(transformerProp.SlotName, transformerProp.TransformChain);
                                }
                                else if (property is VariantConstraintPhraseProperty)
                                {
                                    // Set the transformers for this pattern
                                    VariantConstraintPhraseProperty variantProp = property as VariantConstraintPhraseProperty;
                                    thisPhraseVariants = variantProp.VariantConstraints;
                                }
                            }

                            Dictionary<string, string> variantsWithoutLocale = thisPhraseVariants;
                            if (variantsWithoutLocale.ContainsKey("locale"))
                            {
                                variantsWithoutLocale.Remove("locale");
                            }
                            variantsWithoutLocale["locale"] = actualLocale.ToBcp47Alpha2String();
                            VariantConfig phraseKeyActualLocale = new VariantConfig(phrase.Name.ToLowerInvariant(), variantsWithoutLocale);
                            variantsWithoutLocale.Remove("locale");
                            variantsWithoutLocale["locale"] = perceivedLocale.ToBcp47Alpha2String();
                            VariantConfig phraseKeyPerceivedLocale = new VariantConfig(phrase.Name.ToLowerInvariant(), variantsWithoutLocale);
                            variantsWithoutLocale.Remove("locale");

                            // TODO: Implement locale-specific phrases (such as having a phrase variant only for British english but not American)

                            if (patternsAlreadyCreated.ContainsKey(actualLocale))
                            {
                                if (!patternsAlreadyCreated.ContainsKey(perceivedLocale))
                                {
                                    if (!_phrases.ContainsKey(phraseKeyPerceivedLocale))
                                    {
                                        _phrases[phraseKeyPerceivedLocale] = new List<StatisticalLGPattern>();
                                    }

                                    _phrases[phraseKeyPerceivedLocale].Add(patternsAlreadyCreated[actualLocale]);
                                    patternsAlreadyCreated[perceivedLocale] = patternsAlreadyCreated[actualLocale];
                                }
                                else
                                {
                                    _logger.Log("Could not create a fallback locale phrase for " + phrase.Name + ":" + perceivedLocale + ". This should never happen", LogLevel.Err);
                                }

                                continue;
                            }

                            if (!_phrases.ContainsKey(phraseKeyActualLocale))
                            {
                                _phrases[phraseKeyActualLocale] = new List<StatisticalLGPattern>();
                            }

                            _phrases[phraseKeyActualLocale].Add(newPattern);
                            patternsAlreadyCreated[actualLocale] = newPattern;

                            // Also create a redirect if we have remapped this locale
                            if (!patternsAlreadyCreated.ContainsKey(perceivedLocale))
                            {
                                LocalizedKey tempPhraseKey = new LocalizedKey();

                                if (!_phrases.ContainsKey(phraseKeyPerceivedLocale))
                                {
                                    _phrases[phraseKeyPerceivedLocale] = new List<StatisticalLGPattern>();
                                }

                                _phrases[phraseKeyPerceivedLocale].Add(newPattern);
                                patternsAlreadyCreated[perceivedLocale] = newPattern;
                            }
                        }
                    }
                    else if (block.BlockType == TemplateFileBlockType.TranslationTable)
                    {
                        TranslationTable translationBlock = block as TranslationTable;
                        _translationTables.Add(translationBlock.Name, translationBlock.Mapping);
                    }
                }

                // Compile all scripts for this template
                if (_scriptCompiler != null)
                {
                    IDictionary<string, LgCommon.RunLGScript> compiledScripts = _scriptCompiler.Compile(parsedTemplate.OriginalFileName, scriptBlocks, _logger.Clone("LGScriptCompiler"));
                    foreach (var compiledScript in compiledScripts)
                    {
                        foreach (LanguageCode perceivedLocale in parsedTemplate.SupportedLocales)
                        {
                            _scripts[new LocalizedKey(compiledScript.Key, perceivedLocale)] = compiledScript.Value;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Retrieves a single pattern from the set of LG models available.
        /// </summary>
        /// <param name="patternName">The name of the pattern to retrieve</param>
        /// <param name="clientContext">The current query's context</param>
        /// <param name="logger">A query-specific logger (optional)</param>
        /// <param name="debug">If true, output debug messages</param>
        /// <param name="phraseNum">If set, specify a deterministic phrase ID to use for output. This is used mostly for deterministic unit tests.</param>
        /// <returns>The desired pattern. If none exists, a non-null empty pattern will be returned.</returns>
        public ILGPattern GetPattern(string patternName, ClientContext clientContext, ILogger logger = null, bool debug = false, int? phraseNum = null)
        {
            return GetPattern(patternName, clientContext, null, logger, debug, phraseNum);
        }

        public LgCommon.RunLGScript GetScript(string scriptName, LanguageCode locale)
        {
            var key = new LocalizedKey(scriptName, locale);
            if (_scripts.ContainsKey(key))
            {
                return _scripts[key];
            }

            return null;
        }

        /// <summary>
        /// Retrieves a single pattern from the set of LG models available.
        /// </summary>
        /// <param name="patternName">The name of the pattern to retrieve</param>
        /// <param name="clientContext">The current query's context</param>
        /// <param name="extraVariants">The set of variant constraints to be used to find a valid pattern. Locale should NOT be included in this set.</param>
        /// <param name="logger">A query-specific logger (optional)</param>
        /// <param name="debug">Enable verbose debug logging</param>
        /// <param name="phraseNum">The phrase variant to select, if multiple variants of the phrase are specified. If null, a random phrase will be picked</param>
        /// <returns>The desired pattern. If none exists, a non-null empty pattern will be returned.</returns>
        public ILGPattern GetPattern(string patternName, ClientContext clientContext, IDictionary<string, string> extraVariants = null, ILogger logger = null, bool debug = false, int? phraseNum = null)
        {
            ILogger queryLogger = logger ?? _logger;
            queryLogger = queryLogger.Clone("LGPattern-" + patternName);

            Dictionary<string, string> localeSpecificVariants = extraVariants != null ? new Dictionary<string, string>(extraVariants) : new Dictionary<string, string>();
            if (clientContext.Locale != null &&
                !string.Equals(clientContext.Locale.Iso639_1, LanguageCode.NO_LANGUAGE.Iso639_1, StringComparison.Ordinal))
            {
                localeSpecificVariants["locale"] = clientContext.Locale.ToBcp47Alpha2String();
            }
            IList<StatisticalLGPattern> localeSpecificResult = VariantConfig.SelectByVariants(_phrases, patternName.ToLowerInvariant(), localeSpecificVariants);

            if (localeSpecificResult != null)
            {
                // A locale-specific pattern exists. Return it
                // (actually, return a clone so the caller cannot modify the actual patterns themselves)
                queryLogger.Log("Using statistical LG template " + patternName, LogLevel.Vrb);
                return GetRandomPatternFromList(localeSpecificResult, phraseNum.GetValueOrDefault(_rand.NextInt())).Clone(queryLogger, clientContext, debug);
            }

            // Look in custom code patterns as well
            StatisticalLGCustomCodeWrapper customCodePattern;
            if (_customCodePhrases.TryGetValue(new LocalizedKey(patternName, clientContext.Locale), out customCodePattern) ||
                _customCodePhrases.TryGetValue(new LocalizedKey(patternName, LanguageCode.NO_LANGUAGE), out customCodePattern))
            {
                return customCodePattern.Clone(queryLogger, clientContext, debug);
            }

            LanguageCode requestedLocale = clientContext.Locale ?? LanguageCode.NO_LANGUAGE;
            queryLogger.Log("LG pattern \"" + patternName + ":" + requestedLocale.ToBcp47Alpha2String() + "\" doesn't exist", LogLevel.Wrn);
            return new NullLGPattern(patternName, clientContext.Locale);
        }

        /// <summary>
        /// Used for patterns that apply to UI elements or similar things where only text is needed
        /// </summary>
        /// <param name="patternName">The name of the pattern to retrieve</param>
        /// <param name="clientContext">The current query's context</param>
        /// <param name="logger">The current query's logger (optional)</param>
        /// <param name="debug">If true, output debug messages</param>
        /// <param name="phraseNum">If set, specify a deterministic phrase ID to use for output. This is used mostly for deterministic unit tests.</param>
        /// <returns>The "Text" field of the LG pattern, or empty string if the pattern is not found</returns>
        public async Task<string> GetText(string patternName, ClientContext clientContext, ILogger logger = null, bool debug = false, int? phraseNum = null)
        {
            ILGPattern p = GetPattern(patternName, clientContext, logger, debug, phraseNum);
            return (await p.Render().ConfigureAwait(false)).Text;
        }

        public void RegisterCustomCode(string patternName, LgCommon.RunLanguageGeneration method, LanguageCode locale)
        {
            StatisticalLGCustomCodeWrapper codeWrapper = new StatisticalLGCustomCodeWrapper(patternName, method, locale, _logger);
            LocalizedKey patternKey = new LocalizedKey(patternName, locale);
            _customCodePhrases[patternKey] = codeWrapper;
        }

        public IEnumerable<VariantConfig> GetAllPatternNames()
        {
            return _phrases.Keys;
        }

        /// <summary>
        /// maps from table name -> table mapping
        /// </summary>
        internal IDictionary<string, Dictionary<string, string>> TranslationTables
        {
            get
            {
                return _translationTables;
            }
        }

        /// <summary>
        /// maps from modelname with locale -> model
        /// </summary>
        internal IDictionary<LocalizedKey, StatisticalLGPhrase> Models
        {
            get
            {
                return _models;
            }
        }

        /// <summary>
        /// maps from phrase name with variants -> list of phrase variations
        /// </summary>
        internal IDictionary<VariantConfig, IList<StatisticalLGPattern>> Patterns
        {
            get
            {
                return _phrases;
            }
        }

        private ILGPattern GetRandomPatternFromList(IList<StatisticalLGPattern> patterns, int phraseNum)
        {
            return patterns[Math.Abs(phraseNum) % patterns.Count];
        }

        /// <summary>
        /// Accepts a block of data defining a model (a model name + training strings). If a model with the same name already exists, nothing will
        /// happen except potential locale key redirects. If the model name is new, train a statistical phrase using the data,
        /// add it to the models collection, and setup appropriate locale redirects
        /// </summary>
        /// <param name="domain">The domain of the plugin that uses these models</param>
        /// <param name="modelName">The unique name of the model</param>
        /// <param name="trainingLines">All training data for the model</param>
        /// <param name="logger">A logger</param>
        /// <param name="perceivedLocale">The apparent locale that this model should be invokable for</param>
        /// <param name="localeMappings">A dictionary of mappings from perceived locale to actual locale</param>
        /// <param name="nlTools">All NL tools currently available</param>
        /// <param name="cacheManager">A resourcemanager to manage cached models files</param>
        /// <param name="models">A mutable dictionary of trained statistical phrases. Will usually be modified in this function.</param>
        /// <param name="useCache">If false, don't try and load / save the trained model to cache</param>
        private static void TrainModelAndAddToCollection(
            string domain,
            string modelName,
            IEnumerable<string> trainingLines,
            ILogger logger,
            LanguageCode perceivedLocale,
            IDictionary<LanguageCode, LanguageCode> localeMappings,
            INLPToolsCollection nlTools,
            IFileSystem cacheManager,
            IDictionary<LocalizedKey, StatisticalLGPhrase> models,
            bool useCache)
        {
            // TODO: Implement model-specific locales
            LanguageCode actualLocale = localeMappings[perceivedLocale];
            NLPTools thisLocaleTools;
            if (!nlTools.TryGetNLPTools(actualLocale, out thisLocaleTools))
            {
                throw new KeyNotFoundException("Couldn't find NL tools for locale " + actualLocale.ToBcp47Alpha2String());
            }

            LocalizedKey modelKeyActualLocale = new LocalizedKey(modelName, actualLocale);
            LocalizedKey modelKeyPerceivedLocale = new LocalizedKey(modelName, perceivedLocale);
            if (models.ContainsKey(modelKeyActualLocale))
            {
                // this could happen if one locale fell back to another one that already exists
                // In this case just create a reference to the already existing value
                if (!models.ContainsKey(modelKeyPerceivedLocale))
                {
                    models[modelKeyPerceivedLocale] = models[modelKeyActualLocale];
                }

                return;
            }

            IWordBreaker breaker = thisLocaleTools.FeaturizationWordBreaker;
            ILGFeatureExtractor lgFeaturizer = thisLocaleTools.LGFeatureExtractor;

            StatisticalLGPhrase newPhrase = new StatisticalLGPhrase(modelName, actualLocale, logger, breaker, lgFeaturizer);
            bool loadedFromCache;
            VirtualPath cacheFileName =
                useCache ?
                new VirtualPath(RuntimeDirectoryName.CACHE_DIR + "\\" + actualLocale + "\\" + domain + " " + modelName + ".lg") :
                null;

            newPhrase.Initialize(trainingLines, out loadedFromCache, cacheManager, cacheFileName);

            models[modelKeyActualLocale] = newPhrase;

            // Also create a redirect if we have remapped this locale
            if (!models.ContainsKey(modelKeyPerceivedLocale))
            {
                models[modelKeyPerceivedLocale] = newPhrase;
            }
        }

        private async Task<ParsedStatisticalLGTemplate> ParseFile(VirtualPath sourceFile, IFileSystem fileSystem)
        {
            IEnumerable<string> entireFile = await fileSystem.ReadLinesAsync(sourceFile).ConfigureAwait(false);

            try
            {
                ParsedStatisticalLGTemplate returnVal = ParsedStatisticalLGTemplate.ParseTemplate(entireFile, sourceFile.FullName);

                // Ensure there are no duplicate locales
                HashSet<LanguageCode> locales = new HashSet<LanguageCode>();
                foreach (LanguageCode locale in returnVal.SupportedLocales)
                {
                    if (locales.Contains(locale))
                    {
                        _logger.Log("The locale \"" + locale.ToBcp47Alpha2String() + "\" was specified multiple times in the supported locales for template " + sourceFile, LogLevel.Err);
                        return null;
                    }

                    locales.Add(locale);
                }

                // TODO Ensure that there is no more than 1 transformer chain per slot per phrase

                return returnVal;
            }
            catch (Exception e)
            {
                _logger.Log("Error while parsing statistical LG template " + sourceFile.FullName, LogLevel.Err);
                _logger.Log(e);
            }

            return null;
        }
    }
}
