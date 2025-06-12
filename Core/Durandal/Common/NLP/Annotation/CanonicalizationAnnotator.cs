
namespace Durandal.Common.NLP.Annotation
{
    using Durandal.API;
    using Durandal.Common.Utils;
    using Durandal.Common.Config;
    using Durandal.Common.Logger;
    using Durandal.Common.NLP.Canonical;
    using Durandal.Common.NLP.Feature;
    using Durandal.Common.File;
    using Durandal.Common.Ontology;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Durandal.Common.Tasks;
    using Durandal.Common.Dialog;
    using System.Threading;
    using Time;
    using Durandal.Common.NLP.Language;

    /// <summary>
    /// LU Annotator which canonicalizes strings using an external grammar
    /// </summary>
    public class CanonicalizationAnnotator : IAnnotator
    {
        // Configuration key to look for what canonicalizers to load
        private readonly string ConfigKeyName = "Canonicalizers";

        private readonly ISet<string> _loadedDomains = new HashSet<string>();

        // Maps domain/intent/slot names to canonicalization grammars
        private readonly IDictionary<string, Grammar> _grammarMapping = new Dictionary<string, Grammar>();

        // Maps resource names to grammars that we have already loaded, so each file will only be loaded one time
        private readonly IDictionary<string, Grammar> _loadedGrammars = new Dictionary<string, Grammar>();

        private readonly VirtualPath _canonicalDir;
        private readonly IFileSystem _fileSystem;
        private readonly LanguageCode _locale;
        private readonly ILogger _logger;

        public CanonicalizationAnnotator(IFileSystem fileSystem, LanguageCode locale, ILogger logger)
        {
            _fileSystem = fileSystem;
            _locale = locale;
            _logger = logger;
            _canonicalDir = new VirtualPath(RuntimeDirectoryName.CANONICAL_DIR);
        }

        public string Name
        {
            get
            {
                return "canonicalizer";
            }
        }

        public bool Initialize()
        {
            _logger.Log("Starting to load canonicalizers...");
            LoadAllGrammars(_canonicalDir, _fileSystem, _locale, _logger);
            _logger.Log("Done loading canonicalizers");
            return true;
        }

        /// <summary>
        /// Hints to this annotator that the model has been reloaded
        /// </summary>
        public void Reset()
        {
            _loadedDomains.Clear();
            _grammarMapping.Clear();
            _loadedDomains.Clear();
            LoadAllGrammars(_canonicalDir, _fileSystem, _locale, _logger);
        }

        public Task CommitAnnotation(
            object asyncState,
            RecoResult result,
            LURequest originalRequest,
            KnowledgeContext entityContext,
            IConfiguration modelConfig,
            ILogger queryLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            InitalizeDomain(result.Domain, modelConfig, queryLogger);
            
            // Run annotators at the slot level
            foreach (TaggedData tagHyp in result.TagHyps)
            {
                ISet<string> slotsToDrop = new HashSet<string>();

                foreach (SlotValue slotValue in tagHyp.Slots)
                {
                    string canonicalKey = result.Domain + '/' + result.Intent + '/' + slotValue.Name;
                    Grammar normalizer;
                    if (_grammarMapping.TryGetValue(canonicalKey, out normalizer) && normalizer != null)
                    {
                        IList<GrammarMatch> matches = normalizer.Matches(slotValue.Value, queryLogger.Clone("CanonicalGrammar"));
                        if (matches.Count > 0 && !matches[0].NormalizedValue.Equals(slotValue.Value))
                        {
                            queryLogger.Log(string.Format("The slot \"{0}\" matched {1} canonicalization rule(s)", slotValue.Name, matches.Count), LogLevel.Vrb);
                            foreach (GrammarMatch m in matches)
                            {
                                queryLogger.Log(string.Format("RuleId={0} Value={1} NormalizedValue={2}", m.RuleId, m.Value, m.NormalizedValue), LogLevel.Vrb);
                            }

                            // Leave the original value as an annotation, if the value changed
                            if (slotValue.Annotations.ContainsKey(SlotPropertyName.NonCanonicalValue))
                            {
                                // If multiple regexes captured, keep the longest original value
                                if (slotValue.Annotations[SlotPropertyName.NonCanonicalValue].Length < slotValue.Value.Length)
                                {
                                    queryLogger.Log("Dropping non-canonical value \"" + slotValue.Annotations[SlotPropertyName.NonCanonicalValue] + "\" in favor of \"" + slotValue.Value + "\"", LogLevel.Vrb);
                                    slotValue.Annotations.Remove(SlotPropertyName.NonCanonicalValue);
                                    slotValue.Annotations.Add(SlotPropertyName.NonCanonicalValue, slotValue.Value);
                                }
                            }
                            else
                            {
                                slotValue.Annotations.Add(SlotPropertyName.NonCanonicalValue, slotValue.Value);
                            }
                            slotValue.Value = matches[0].NormalizedValue;
                        }

                        if (string.IsNullOrWhiteSpace(slotValue.Value))
                        {
                            slotsToDrop.Add(slotValue.Name);
                        }
                    }
                }

                // If the grammar canonicalized the slot to nothing, drop it
                foreach (string slotToDrop in slotsToDrop)
                {
                    SlotValue droppedSlot = DialogHelpers.TryGetSlot(tagHyp, slotToDrop);
                    if (droppedSlot != null)
                    {
                        queryLogger.Log("Dropping slot \"" + slotToDrop + "\" because its value became empty after canonicalization", LogLevel.Vrb);
                        tagHyp.Slots.Remove(droppedSlot);
                    }
                }
            }

            return DurandalTaskExtensions.NoOpTask;
        }

        /// <summary>
        /// Parses a model config and tries to see what slots canonicalizers are enabled for.
        /// </summary>
        /// <param name="modelDomain"></param>
        /// <param name="modelConfig"></param>
        /// <param name="queryLogger"></param>
        /// <returns></returns>
        protected void InitalizeDomain(string modelDomain, IConfiguration modelConfig, ILogger queryLogger)
        {
            lock (_loadedDomains)
            {
                if (_loadedDomains.Contains(modelDomain))
                {
                    return;
                }

                queryLogger.Log("Initializing canonicalizers for model domain " + modelDomain, LogLevel.Vrb);

                if (modelConfig.ContainsKey(ConfigKeyName))
                {
                    try
                    {
                        foreach (var canonicalParam in modelConfig.GetStringDictionary(ConfigKeyName))
                        {
                            string domainIntentSlot = modelDomain + "/" + canonicalParam.Key;
                            string modelName = canonicalParam.Value;

                            // Check if a canonicalizer for this domain is already specified (should only happen if config is bad)
                            if (_grammarMapping.ContainsKey(domainIntentSlot))
                            {
                                queryLogger.Log("Malformed configuration line in model config for " + modelDomain + " " + canonicalParam + ": Multiple canonicalizers have been specified for this slot", LogLevel.Wrn);
                                continue;
                            }

                            // Is there a canonicalizer for this tag?
                            string grammarName = modelDomain + " " + modelName + ".canonical.xml";

                            // If we don't know about this file, try and reload the grammars in case it is new
                            if (!_loadedGrammars.ContainsKey(grammarName))
                            {
                                LoadAllGrammars(_canonicalDir, _fileSystem, _locale, _logger);
                            }

                            // If it still doesn't exist, throw an error
                            if (_loadedGrammars.ContainsKey(grammarName))
                            {
                                _grammarMapping[domainIntentSlot] = _loadedGrammars[grammarName];
                            }
                            else
                            {
                                // Explicitly setting the value as null will mark this file as "nonexistent" and prevent attempts to reload it
                                queryLogger.Log("Attempted to load the canonicalization grammar " + grammarName + ", but it does not exist!", LogLevel.Err);
                                _grammarMapping[domainIntentSlot] = null;
                            }
                        }
                    }
                    catch (FormatException e)
                    {
                        queryLogger.Log("Malformed configuration line in model config for " + modelDomain + ": " + e.Message, LogLevel.Err);
                    }
                }
                else
                {
                    queryLogger.Log("No canonicalizers found in model config for " + modelDomain + ", moving on...", LogLevel.Vrb);
                }

                _loadedDomains.Add(modelDomain);
            }
        }

        /// <summary>
        /// Inspects the canonicalizer directory, looks for grammars, and loads all of them into _loadedGrammars
        /// </summary>
        /// <param name="canonicalDir"></param>
        /// <param name="fileSystem"></param>
        /// <param name="locale"></param>
        /// <param name="logger"></param>
        private void LoadAllGrammars(VirtualPath canonicalDir, IFileSystem fileSystem, LanguageCode locale, ILogger logger)
        {
            VirtualPath localizedGrammars = canonicalDir.Combine(locale.ToBcp47Alpha2String());

            if (!fileSystem.Exists(localizedGrammars) || fileSystem.WhatIs(localizedGrammars) != ResourceType.Directory)
            {
                logger.Log("The canonicalization directory " + localizedGrammars.FullName + " does not exist or is not a directory", LogLevel.Wrn);
                return;
            }

            _loadedGrammars.Clear();

            foreach (VirtualPath grammarFile in fileSystem.ListFiles(localizedGrammars))
            {
                string fileName = grammarFile.Name;
                if (!fileName.EndsWith("canonical.xml", StringComparison.OrdinalIgnoreCase))
                {
                    logger.Log("Skipping file " + fileName + " as it does not appear to be a canonicalization grammar");
                    continue;
                }

                // Has this shared file already been loaded?
                if (_loadedGrammars.ContainsKey(fileName))
                {
                    continue;
                }

                logger.Log("Loading canonicalizer " + fileName);
                try
                {
                    Grammar newGrammar = new Grammar(fileSystem.OpenStream(grammarFile, FileOpenMode.Open, FileAccessMode.Read)); ;
                    _loadedGrammars[fileName] = newGrammar;
                }
                catch (FormatException e)
                {
                    logger.Log("Error while loading canonicalization grammar " + fileName, LogLevel.Err);
                    logger.Log(e, LogLevel.Err);
                }
            }
        }

        public Task<object> AnnotateStateless(RecoResult input, LURequest originalRequest, IConfiguration modelConfig, ILogger queryLogger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return Task.FromResult<object>(null);
        }
    }
}
