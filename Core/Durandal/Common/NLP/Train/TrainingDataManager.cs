using System;
using System.Collections.Generic;
using System.IO;
using Durandal.API;
using Durandal.Common.Utils;

namespace Durandal.Common.NLP.Train
{
    using System.Collections.Concurrent;
    using System.Text.RegularExpressions;
    using System.Threading;

    using Durandal.Common.NLP.Feature;

    using Durandal.Common.Config;
    using Durandal.Common.Utils;
    using Durandal.Common.Logger;
    using Durandal.Common.NLP;

    using Durandal.Common.File;
    using System.Threading.Tasks;
    using System.Linq;
    using Durandal.Common.NLP.Tagging;
    using Durandal.Common.Tasks;
    using Durandal.Common.IO;
    using Durandal.Common.Time;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.ServiceMgmt;

    public class TrainingDataManager
    {
        // Set of all known training domains
        private readonly ISet<string> _knownDomains;

        // Mapping from "domain" -> set of intents for that domain
        private readonly IDictionary<string, ISet<string>> _knownIntents;

        // _knownIntents, but for intents that are defined entirely or partially by regexes
        private readonly IDictionary<string, ISet<string>> _knownRegexIntents;

        // Mapping from "domain" -> set of tags for that domain
        private readonly IDictionary<string, ISet<string>> _knownTags;

        // _knownTags, but for tags that are defined entirely or partially by regexes
        private readonly IDictionary<string, ISet<string>> _knownRegexTags;

        // Mapping from "domain intent" -> set of possible tags for that intent
        private readonly IDictionary<string, ISet<string>> _knownTagsPerIntent;

        // _knownTagsPerIntent, but for tags that are defined entirely or partially by regexes
        private readonly IDictionary<string, ISet<string>> _knownRegexTagsPerIntent;

        private readonly IDictionary<string, IConfiguration> _domainConfigs;

        private IThreadPool _threadPool;

        private readonly IFileSystem _fileSystem;
        private readonly LanguageCode _locale;
        private readonly VirtualPath _trainingDir;
        private readonly VirtualPath _validationDir;
        private readonly VirtualPath _modelDir;
        private readonly VirtualPath _cacheDir;
        private readonly VirtualPath _modelConfigDir;
        private readonly ILogger _logger;
        private const float trainingDataExpansionMultiplier = 1.0f;
        private const float validationDataExpansionMultiplier = 0.5f;

        // Small training templates will be artificially expanded to reach this number of training instances
        private const int MIN_TRAINING_INSTANCES = 6000;

        public TrainingDataManager(ILogger logger,
            LanguageCode locale,
            IFileSystem fileSystem,
            IThreadPool threadPool)
        {
            _logger = logger;
            _locale = locale;
            _threadPool = threadPool;
            _knownDomains = new HashSet<string>();
            _knownIntents = new Dictionary<string, ISet<string>>();
            _knownRegexIntents = new Dictionary<string, ISet<string>>();
            _knownTags = new Dictionary<string, ISet<string>>();
            _knownRegexTags = new Dictionary<string, ISet<string>>();
            _knownTagsPerIntent = new Dictionary<string, ISet<string>>();
            _knownRegexTagsPerIntent = new Dictionary<string, ISet<string>>();
            _domainConfigs = new Dictionary<string, IConfiguration>();
            _fileSystem = fileSystem;

            _trainingDir = new VirtualPath(RuntimeDirectoryName.TRAINING_DIR + "\\" + _locale);
            _validationDir = new VirtualPath(RuntimeDirectoryName.VALIDATION_DIR + "\\" + _locale);
            _cacheDir = new VirtualPath(RuntimeDirectoryName.CACHE_DIR + "\\" + _locale);
            _modelDir = new VirtualPath(RuntimeDirectoryName.MODEL_DIR + "\\" + _locale);
            _modelConfigDir = new VirtualPath(RuntimeDirectoryName.MODELCONFIG_DIR + "\\" + _locale);
        }
        
        public ISet<string> GetKnownDomains()
        {
            return _knownDomains;
        }

        public ISet<string> GetKnownIntents(string domain)
        {
            if (_knownIntents.ContainsKey(domain))
                return _knownIntents[domain];
            return new HashSet<string>();
        }

        public ISet<string> GetKnownRegexIntents(string domain)
        {
            if (_knownRegexIntents.ContainsKey(domain))
                return _knownRegexIntents[domain];
            return new HashSet<string>();
        }

        public ISet<string> GetKnownTags(string domain)
        {
            if (_knownTags.ContainsKey(domain))
                return _knownTags[domain];
            return new HashSet<string>();
        }

        public ISet<string> GetKnownRegexTags(string domain)
        {
            if (_knownRegexTags.ContainsKey(domain))
                return _knownRegexTags[domain];
            return new HashSet<string>();
        }

        public ISet<string> GetKnownTags(string domain, string intent)
        {
            if (_knownTagsPerIntent.ContainsKey(domain + " " + intent))
                return _knownTagsPerIntent[domain + " " + intent];
            return new HashSet<string>();
        }

        public ISet<string> GetKnownRegexTags(string domain, string intent)
        {
            if (_knownRegexTagsPerIntent.ContainsKey(domain + " " + intent))
                return _knownRegexTagsPerIntent[domain + " " + intent];
            return new HashSet<string>();
        }

        private bool ProcessTemplateChunk(
            IEnumerable<TrainingUtterance> trainingGroup,
            IDictionary<string, ISet<string>> tagsPerGroup,
            Dictionary<string, IConfiguration> domainConfigurations,
            WriteStreamMultiplexer trainingFileWriters,
            IRealTimeProvider realTime)
        {
            bool allFilesExist = true;

            foreach (TrainingUtterance templateLine in trainingGroup)
            {
                // Add the domain to known domains
                if (!_knownDomains.Contains(templateLine.Domain))
                {
                    _knownDomains.Add(templateLine.Domain);
                    IConfiguration newDomainConfig = this.GetDomainConfiguration(templateLine.Domain, realTime);
                    domainConfigurations.Add(templateLine.Domain, newDomainConfig);
                }

                // Add the intent to known intents
                if (!_knownIntents.ContainsKey(templateLine.Domain))
                {
                    _knownIntents[templateLine.Domain] = new HashSet<string>();
                }
                if (!_knownIntents[templateLine.Domain].Contains(templateLine.Intent))
                {
                    _knownIntents[templateLine.Domain].Add(templateLine.Intent);
                    GetDomainConfiguration(templateLine.Domain, realTime).Set("intents", _knownIntents[templateLine.Domain]);
                }

                string domainIntent = templateLine.Domain + " " + templateLine.Intent;

                // Parse all the tags in the training template set (before it is expanded)
                // This prevents us from parsing the entire actual training file
                // Collect the set of all tags in the domain, and all tags divided by intent
                if (!_knownTags.ContainsKey(templateLine.Domain))
                {
                    _knownTags[templateLine.Domain] = new HashSet<string>();
                    _knownTags[templateLine.Domain].Add("O");
                    GetDomainConfiguration(templateLine.Domain, realTime).Set("alltags", _knownTags[templateLine.Domain]);
                }

                if (!_knownTagsPerIntent.ContainsKey(domainIntent))
                {
                    _knownTagsPerIntent[domainIntent] = new HashSet<string>();
                    _knownTagsPerIntent[domainIntent].Add("O");
                    GetDomainConfiguration(templateLine.Domain, realTime).Set("tags_" + templateLine.Intent, _knownTagsPerIntent[domainIntent]);
                }

                HashSet<string> tagNames = TaggedDataSplitter.ExtractTagNames(templateLine.Utterance);

                // Union the "direct" tag names (those appearing in the template line) with "indirect" tag names (those appearing inside of groups referenced by this pattern)
                if (tagsPerGroup != null)
                {
                    // Find all group names
                    MatchCollection groupsReferencedInThisPattern = GROUP_REFERENCE_MATCHER.Matches(templateLine.Utterance);
                    foreach (Match m in groupsReferencedInThisPattern)
                    {
                        ISet<string> indirectTags;
                        if (tagsPerGroup.TryGetValue(m.Groups[1].Value, out indirectTags))
                        {
                            tagNames.UnionWith(indirectTags);
                        }
                    }
                }
                
                foreach (string tagName in tagNames)
                {
                    if (!_knownTags[templateLine.Domain].Contains(tagName))
                    {
                        _knownTags[templateLine.Domain].Add(tagName);
                        GetDomainConfiguration(templateLine.Domain, realTime).Set("alltags", _knownTags[templateLine.Domain]);
                    }
                    if (!_knownTagsPerIntent[domainIntent].Contains(tagName))
                    {
                        _knownTagsPerIntent[domainIntent].Add(tagName);
                        GetDomainConfiguration(templateLine.Domain, realTime).Set("tags_" + templateLine.Intent, _knownTagsPerIntent[domainIntent]);
                    }
                }

                VirtualPath outputFileName = _cacheDir.Combine(domainIntent + ".train");
                if (!_fileSystem.Exists(outputFileName) &&
                    !trainingFileWriters.StreamExists(outputFileName))
                {
                    allFilesExist = false;
                }
            }

            return allFilesExist;
        }

        private bool ProcessRegexChunk(
            IEnumerable<TrainingUtterance> trainingGroup,
            Dictionary<string, IConfiguration> domainConfigurations,
            WriteStreamMultiplexer trainingFileWriters,
            IRealTimeProvider realTime)
        {
            bool allFilesExist = true;

            foreach (TrainingUtterance templateLine in trainingGroup)
            {
                // Add the domain to known domains
                if (!_knownDomains.Contains(templateLine.Domain))
                {
                    _knownDomains.Add(templateLine.Domain);
                    IConfiguration newDomainConfig = this.GetDomainConfiguration(templateLine.Domain, realTime);
                    domainConfigurations.Add(templateLine.Domain, newDomainConfig);
                }

                // Add the intent to known intents
                if (!_knownRegexIntents.ContainsKey(templateLine.Domain))
                {
                    _knownRegexIntents[templateLine.Domain] = new HashSet<string>();
                }
                if (!_knownRegexIntents[templateLine.Domain].Contains(templateLine.Intent))
                {
                    _knownRegexIntents[templateLine.Domain].Add(templateLine.Intent);
                    GetDomainConfiguration(templateLine.Domain, realTime).Set("regexintents", _knownRegexIntents[templateLine.Domain]);
                }

                string domainIntent = templateLine.Domain + " " + templateLine.Intent;

                // Parse all the tags in the training template set (before it is expanded)
                // This prevents us from parsing the entire actual training file
                // Collect the set of all tags in the domain, and all tags divided by intent
                if (!_knownRegexTags.ContainsKey(templateLine.Domain))
                {
                    _knownRegexTags[templateLine.Domain] = new HashSet<string>();
                    GetDomainConfiguration(templateLine.Domain, realTime).Set("allregextags", _knownRegexTags[templateLine.Domain]);
                }

                if (!_knownRegexTagsPerIntent.ContainsKey(domainIntent))
                {
                    _knownRegexTagsPerIntent[domainIntent] = new HashSet<string>();
                    GetDomainConfiguration(templateLine.Domain, realTime).Set("regextags_" + templateLine.Intent, _knownRegexTagsPerIntent[domainIntent]);
                }

                ISet<string> tagNames;

                tagNames = ParseTagsFromRegex(templateLine.Utterance);

                foreach (string tagName in tagNames)
                {
                    if (!_knownRegexTags[templateLine.Domain].Contains(tagName))
                    {
                        _knownRegexTags[templateLine.Domain].Add(tagName);
                        GetDomainConfiguration(templateLine.Domain, realTime).Set("allregextags", _knownRegexTags[templateLine.Domain]);
                    }
                    if (!_knownRegexTagsPerIntent[domainIntent].Contains(tagName))
                    {
                        _knownRegexTagsPerIntent[domainIntent].Add(tagName);
                        GetDomainConfiguration(templateLine.Domain, realTime).Set("regextags_" + templateLine.Intent, _knownRegexTagsPerIntent[domainIntent]);
                    }
                }

                VirtualPath whitelistFileName = _modelDir.Combine(domainIntent + ".whitelist");
                VirtualPath blacklistFileName = _modelDir.Combine(domainIntent + ".blacklist");
                if (!_fileSystem.Exists(whitelistFileName) &&
                    !trainingFileWriters.StreamExists(whitelistFileName))
                {
                    allFilesExist = false;
                }
                if (!_fileSystem.Exists(blacklistFileName) &&
                    !trainingFileWriters.StreamExists(blacklistFileName))
                {
                    allFilesExist = false;
                }
            }

            return allFilesExist;
        }

        /// <summary>
        /// Reads a regex string and extracts all of the named capture groups from it, returning them as a set
        /// </summary>
        /// <param name="regex"></param>
        /// <returns></returns>
        private ISet<string> ParseTagsFromRegex(string regex)
        {
            // Compile the regex and find all group names that are non-integer
            Regex testRegex = new Regex(regex);
            int dummy;
            HashSet<string> returnVal = new HashSet<string>();
            foreach (string groupName in testRegex.GetGroupNames())
            {
                if (groupName.Length > 2 || !int.TryParse(groupName, out dummy))
                {
                    returnVal.Add(groupName);
                }
            }
            return returnVal;
        }
        
        public void LoadTrainingData(IRealTimeProvider realTime)
        {
            if (_fileSystem.WhatIs(_trainingDir) != ResourceType.Directory)
            {
                _logger.Log("Training directory " + _trainingDir + " does not exist or is not a directory", LogLevel.Err);
                return;
            }

            WriteStreamMultiplexer trainingFileWriters = new WriteStreamMultiplexer(_fileSystem);
            Dictionary<string, IConfiguration> domainConfigurations = new Dictionary<string, IConfiguration>();
            IList<TemplateExpansionThread> threads = new List<TemplateExpansionThread>();
            
            foreach (VirtualPath templateFile in _fileSystem.ListFiles(_trainingDir))
            {
                try
                {
                    if (templateFile.Extension.Equals(".template", StringComparison.OrdinalIgnoreCase))
                    {
                        bool allFilesExist = true;
                        // Inspect the template to determine which domains and intents are covered
                        TrainingDataTemplate template = new TrainingDataTemplate(templateFile, _fileSystem, _locale, _logger, true);

                        // Process indirect group dependencies which is needed to determine what tags apply to expanded patterns
                        IDictionary<string, ISet<string>> tagsPerGroup = ExtractTagsFromGroups(template);
                        allFilesExist &= this.ProcessTemplateChunk(template.Patterns, tagsPerGroup, domainConfigurations, trainingFileWriters, realTime);
                        allFilesExist &= this.ProcessTemplateChunk(template.Statics, null, domainConfigurations, trainingFileWriters, realTime);
                        allFilesExist &= this.ProcessRegexChunk(template.RegexWhitelist, domainConfigurations, trainingFileWriters, realTime);

                        // Executed only if .train files do not exist for this domain
                        if (!allFilesExist)
                        {
#pragma warning disable CA2000 // Dispose objects before losing scope
                            TemplateExpansionThread newThread = new TemplateExpansionThread(template, _fileSystem, _cacheDir, _modelDir, trainingFileWriters, _logger);
#pragma warning restore CA2000 // Dispose objects before losing scope
                            threads.Add(newThread);
                        }
                    }
                }
                catch (FormatException e)
                {
                    _logger.Log("Format exception while loading training template " + templateFile, LogLevel.Err);
                    _logger.Log(e, LogLevel.Err);
                }
            }

            // We have to delay running the thread until this point to prevent race conditions where
            // multiple files for the same intent can get confused about whether their training exists already
            // or not
            foreach (var thread in threads)
            {
                _threadPool.EnqueueUserWorkItem(thread.Run);
            }

            foreach (var thread in threads)
            {
                thread.Join();
                thread.Dispose();
            }

            trainingFileWriters.Dispose();
        }

        private static readonly Regex GROUP_REFERENCE_MATCHER = new Regex("\\{(\\S+)\\}");

        private static IDictionary<string, ISet<string>> ExtractTagsFromGroups(TrainingDataTemplate template)
        {
            IDictionary<string, ISet<string>> tagsPerGroup = new Dictionary<string, ISet<string>>();

            foreach (TrainingUtterance utterance in template.Patterns)
            {
                MatchCollection groupsReferencedInThisPattern = GROUP_REFERENCE_MATCHER.Matches(utterance.Utterance);
                foreach (Match m in groupsReferencedInThisPattern)
                {
                    string groupName = m.Groups[1].Value;
                    if (!tagsPerGroup.ContainsKey(groupName))
                    {
                        string[] groupItems = template.GetGroup(groupName);
                        if (groupItems != null)
                        {
                            HashSet<string> allTags = new HashSet<string>();
                            foreach (string item in groupItems)
                            {
                                allTags.UnionWith(TaggedDataSplitter.ExtractTagNames(item));
                            }

                            tagsPerGroup.Add(groupName, allTags);
                        }
                    }
                }
            }

            return tagsPerGroup;
        }

        private class TemplateExpansionThread : IDisposable
        {
            private readonly TrainingDataTemplate _template;
            private readonly IFileSystem _fileSystem;
            private readonly VirtualPath _cacheDir;
            private readonly VirtualPath _modelDir;
            private readonly WeakPointer<WriteStreamMultiplexer> _trainingFileWriters;
            private readonly ILogger _logger;
            private readonly EventWaitHandle _finished;
            private int _disposed = 0;

            public TemplateExpansionThread(
                TrainingDataTemplate template,
                IFileSystem fileSystem,
                VirtualPath cacheDir,
                VirtualPath modelDir,
                WriteStreamMultiplexer trainingFileWriters,
                ILogger logger)
            {
                _template = template;
                _fileSystem = fileSystem;
                _cacheDir = cacheDir;
                _modelDir = modelDir;
                _trainingFileWriters = new WeakPointer<WriteStreamMultiplexer>(trainingFileWriters);
                _logger = logger;
                _finished = new ManualResetEvent(false);
                DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
            }

#if TRACK_IDISPOSABLE_LEAKS
            ~TemplateExpansionThread()
            {
                Dispose(false);
            }
#endif

            /// <summary>
            /// Expands a single training template and writes its output to a shared pool of domain/intent training writers
            /// </summary>
            public void Run()
            {
                try
                {
                    using (ITrainingDataStream templateExpander =
                        new TemplateFileExpanderBalanced(_template, _logger, trainingDataExpansionMultiplier, MIN_TRAINING_INSTANCES))
                    {
                        int linesWritten = 0;
                        while (templateExpander.MoveNext() && linesWritten++ < templateExpander.RecommendedOutputCount)
                        {
                            TrainingUtterance trainingLine = templateExpander.Current;
                            string domainIntent = trainingLine.Domain + " " + trainingLine.Intent;
                            VirtualPath outputFileName = _cacheDir.Combine(domainIntent + ".train");
                            _trainingFileWriters.Value.GetStream(outputFileName, true).WriteLine(trainingLine);
                        }

                        _logger.Log("From the template " + _template.OriginalFileName.FullName + " I get " + linesWritten + " dynamic training phrases", LogLevel.Vrb);

                        // Write the regex whitelist and blacklist files as well
                        foreach (TrainingUtterance trainingLine in _template.RegexWhitelist)
                        {
                            string domainIntent = trainingLine.Domain + " " + trainingLine.Intent;
                            VirtualPath outputFileName = _modelDir.Combine(domainIntent + ".whitelist");
                            _trainingFileWriters.Value.GetStream(outputFileName, true).WriteLine(trainingLine);
                        }
                        foreach (TrainingUtterance trainingLine in _template.RegexBlacklist)
                        {
                            string domainIntent = trainingLine.Domain + " " + trainingLine.Intent;
                            VirtualPath outputFileName = _modelDir.Combine(domainIntent + ".blacklist");
                            _trainingFileWriters.Value.GetStream(outputFileName, true).WriteLine(trainingLine);
                        }
                    }
                }
                catch (FormatException e)
                {
                    _logger.Log("Format exception while expanding training templates", LogLevel.Err);
                    _logger.Log(e, LogLevel.Err);
                }
                finally
                {
                    _finished.Set();
                }
            }

            public void Join()
            {
                _finished.WaitOne();
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
        }

        public void LoadValidationData()
        {
            if (_fileSystem.WhatIs(_validationDir) != ResourceType.Directory)
            {
                _logger.Log("Validation directory " + _validationDir + " does not exist or is not a directory", LogLevel.Wrn);
                return;
            }
            Dictionary<string, StreamWriter> trainingFileWriters = new Dictionary<string, StreamWriter>();
            foreach (VirtualPath templateFile in _fileSystem.ListFiles(_validationDir))
            {
                if (templateFile.Extension.Equals(".template", StringComparison.OrdinalIgnoreCase))
                {
                    TrainingDataTemplate template = new TrainingDataTemplate(templateFile, _fileSystem, _locale, _logger, true, false);

                    using (ITrainingDataStream templateExpander = new TemplateFileExpanderBalanced(template, _logger, validationDataExpansionMultiplier, 1))
                    {
                        // Just expand the validation data and write it back out to the file
                        int linesWritten = 0;
                        while (templateExpander.MoveNext() && linesWritten++ < templateExpander.RecommendedOutputCount)
                        {
                            TrainingUtterance trainingLine = templateExpander.Current;
                            string domainIntent = trainingLine.Domain + " " + trainingLine.Intent;
                            VirtualPath outputFileName = _cacheDir.Combine(domainIntent + ".validate");
                            if (!trainingFileWriters.ContainsKey(domainIntent))
                            {
                                if (_fileSystem.Exists(outputFileName))
                                {
                                    continue;
                                }

                                trainingFileWriters[domainIntent] = new StreamWriter(_fileSystem.OpenStream(outputFileName, FileOpenMode.Create, FileAccessMode.Write));
                            }
                            trainingFileWriters[domainIntent].WriteLine(trainingLine);
                        }
                    }
                }
            }

            foreach (StreamWriter trainingOut in trainingFileWriters.Values)
            {
                trainingOut.Dispose();
            }
        }

        public void FeaturizeTrainingData(NLPTools langTools, IFileSystem fileSystem)
        {
            _logger.Log("Running feature extractors...");

            WriteStreamMultiplexer domainFileWriters = new WriteStreamMultiplexer(_fileSystem);
            IList<FeaturizerThread> threads = new List<FeaturizerThread>();

            if (_fileSystem.Exists(_cacheDir))
            {
                foreach (VirtualPath trainFile in _fileSystem.ListFiles(_cacheDir))
                {
                    if (trainFile.Extension.Equals(".train", StringComparison.OrdinalIgnoreCase))
                    {
#pragma warning disable CA2000 // Dispose objects before losing scope
                        FeaturizerThread newThread = new FeaturizerThread(
                            trainFile,
                            _cacheDir,
                            _fileSystem,
                            langTools,
                            domainFileWriters,
                            _knownTagsPerIntent);
#pragma warning restore CA2000 // Dispose objects before losing scope
                        threads.Add(newThread);
                        _threadPool.EnqueueUserWorkItem(newThread.Run);
                    }
                }
            }
            else
            {
                _logger.Log("No training data was found, so no work to do here");
            }

            // WaitOnMultiple until all threads are finished
            foreach (FeaturizerThread thread in threads)
            {
                thread.Join();
                thread.Dispose();
            }

            // And clean up
            domainFileWriters.Dispose();

            //FeaturizeNegativeTrainingFile(langTools, fileSystem);
        }

        private class FeaturizerThread : IDisposable
        {
            private readonly VirtualPath _trainingFile;
            private readonly VirtualPath _cacheDirectory;
            private readonly IFileSystem _fileSystem;
            private readonly NLPTools _nlTools;
            private readonly WeakPointer<WriteStreamMultiplexer> _domainFileWriters;
            private readonly IDictionary<string, ISet<string>> _knownTagsPerIntent;
            private readonly EventWaitHandle _finished;
            private int _disposed;

            public FeaturizerThread(
                VirtualPath trainingFile,
                VirtualPath cacheDirectory,
                IFileSystem fileSystem,
                NLPTools langTools,
                WriteStreamMultiplexer domainFileWriters,
                IDictionary<string, ISet<string>> knownTagsPerIntent)
            {
                _trainingFile = trainingFile;
                _cacheDirectory = cacheDirectory;
                _fileSystem = fileSystem;
                _nlTools = langTools;
                _domainFileWriters = new WeakPointer<WriteStreamMultiplexer>(domainFileWriters);
                _knownTagsPerIntent = knownTagsPerIntent;
                _finished = new ManualResetEvent(false);
                DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
            }

#if TRACK_IDISPOSABLE_LEAKS
            ~FeaturizerThread()
            {
                Dispose(false);
            }
#endif

            /// <summary>
            /// Processes a single training file and extracts domain and tag features
            /// </summary>
            public void Run()
            {
                // TODO: ERROR HANDLING HERE
                string fileName = _trainingFile.Name.Substring(0, _trainingFile.Name.LastIndexOf('.'));
                string[] fileNameParts = fileName.Split(' ');
                string domain = fileNameParts[0];
                string domainIntent = fileNameParts[0] + " " + fileNameParts[1];

                // Skip files that already exist
                VirtualPath domainFileName = _cacheDirectory.Combine(domainIntent + ".domainfeatures");
                if (!_fileSystem.Exists(domainFileName))
                {
                    // Train each part-of-speech tagger using its own personal data set
                    using (Stream trainingStream = _fileSystem.OpenStream(_trainingFile, FileOpenMode.Open, FileAccessMode.Read))
                    {
                        _nlTools.DomainFeaturizer.ExtractTrainingFeatures(
                            trainingStream,
                            _domainFileWriters.Value.GetStream(domainFileName));
                    }
                }

                // Determine if there are even any tags in this data set
                // If so, run the tag featurizer
                VirtualPath tagFileName = _cacheDirectory.Combine(domainIntent + ".tagfeatures");
                if (_knownTagsPerIntent.ContainsKey(domainIntent) &&
                    _knownTagsPerIntent[domainIntent].Count > 1 &&
                    !_fileSystem.Exists(tagFileName))
                {
                    using (Stream trainingStream = _fileSystem.OpenStream(_trainingFile, FileOpenMode.Open, FileAccessMode.Read))
                    {
                        _nlTools.TagFeaturizer.ExtractTrainingFeatures(
                            trainingStream,
                            _fileSystem.OpenStream(tagFileName, FileOpenMode.Create, FileAccessMode.Write),
                            _nlTools.FeaturizationWordBreaker,
                            _knownTagsPerIntent[domainIntent]);
                    }
                }

                _finished.Set();
            }

            public void Join()
            {
                _finished.WaitOne();
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
                    _finished.Dispose();
                }
            }
        }

        // what is this used for??
        /*private void FeaturizeNegativeTrainingFile(LanguageSpecificTools langTools, IFileSystem fileSystem)
        {
            VirtualPath inputFileName = new VirtualPath("data\\negative_training.txt");
            VirtualPath outputFileName = _cacheDir + "\\negativetraining.features";

            if (fileSystem.Exists(inputFileName) && !fileSystem.Exists(outputFileName))
            {
                Stream trainingStream = fileSystem.ReadStream(inputFileName);
                TrainingDataList<DomainIntentContextFeature> domainData = langTools.DomainFeaturizer.ExtractTrainingFeatures(
                    trainingStream, langTools.Wordbreaker);
                 domainData.SaveToFile(outputFileName, fileSystem);
            }
        }*/

        public VirtualPath[] GetTrainingFiles(string domain)
        {
            List<VirtualPath> returnVal = new List<VirtualPath>();
            if (_knownDomains.Contains(domain))
            {
                foreach (string intent in _knownIntents[domain])
                {
                    returnVal.Add(GetTrainingFile(domain, intent));
                }
            }
            return returnVal.ToArray();
        }

        public VirtualPath GetTrainingFile(string domain, string intent)
        {
            return _cacheDir.Combine(domain + " " + intent + ".train");
        }

        public VirtualPath[] GetValidationFiles(string domain)
        {
            List<VirtualPath> returnVal = new List<VirtualPath>();
            if (_knownDomains.Contains(domain))
            {
                foreach (string intent in _knownIntents[domain])
                {
                    returnVal.Add(GetValidationFile(domain, intent));
                }
            }
            return returnVal.ToArray();
        }

        public VirtualPath GetValidationFile(string domain, string intent)
        {
            return _cacheDir.Combine(domain + " " + intent + ".validate");
        }

        public VirtualPath GetTagFeaturesFile(string domain, string intent)
        {
            return _cacheDir.Combine(domain + " " + intent + ".tagfeatures");
        }

        public VirtualPath GetDomainIntentFeaturesFile(string domain, string intent)
        {
            return _cacheDir.Combine(domain + " " + intent + ".domainfeatures");
        }

        public VirtualPath GetCacheDirectory()
        {
            return _cacheDir;
        }

        /// <summary>
        /// Returns the configuration that is specific to the model of a certain domain.
        /// This is always guaranteed to exist, even if it doesn't contain anything
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="realTime"></param>
        /// <returns></returns>
        public IConfiguration GetDomainConfiguration(string domain, IRealTimeProvider realTime)
        {
            lock (_domainConfigs)
            {
                string key = domain + "/" + _locale;
                if (!_domainConfigs.ContainsKey(key))
                {
                    _domainConfigs[key] = GetDomainConfigurationFile(domain, _locale, _fileSystem, _logger, realTime).Await();
                }

                return _domainConfigs[key];
            }
        }

        /// <summary>
        /// Returns (and potentially creates) the model-specific configuration for a given model and locale.
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="locale"></param>
        /// <param name="fileSystem"></param>
        /// <param name="logger"></param>
        /// <param name="realTime"></param>
        /// <returns></returns>
        private static async Task<IConfiguration> GetDomainConfigurationFile(string domain, LanguageCode locale, IFileSystem fileSystem, ILogger logger, IRealTimeProvider realTime)
        {
            VirtualPath modelConfigDir = new VirtualPath(RuntimeDirectoryName.MODELCONFIG_DIR + ("\\" + locale.ToBcp47Alpha2String()));
            VirtualPath modelDir = new VirtualPath(RuntimeDirectoryName.MODEL_DIR + ("\\" + locale.ToBcp47Alpha2String()));

            // Look in the model config directory for "seed" config files which contain domain-custom configs
            // If that file exists, copy it to the (temporary) models directory and make it into the de facto config
            // All the autogenerated stuff will then be appended to that file
            VirtualPath sourceFile = modelConfigDir.Combine(domain + ".modelconfig.ini");
            VirtualPath targetFile = modelDir.Combine(domain + ".modelconfig.ini");
            VirtualPath targetConfig = modelDir.Combine(domain + ".modelconfig.ini");
            if (!fileSystem.Exists(targetFile) && fileSystem.Exists(sourceFile))
            {
                IEnumerable<string> allConfig = fileSystem.ReadLines(sourceFile);
                fileSystem.WriteLines(targetFile, allConfig);
            }

            return await IniFileConfiguration.Create(logger, targetConfig, fileSystem, realTime).ConfigureAwait(false);
        }

        public async Task ValidateTrainingChecksums(IFileSystem fileSystem, IRealTimeProvider realTime)
        {
            using (IConfiguration checksumConfig = await IniFileConfiguration.Create(_logger, _cacheDir.Combine("checksums.cache"), _fileSystem, realTime).ConfigureAwait(false))
            {
                if (!fileSystem.Exists(_trainingDir) || fileSystem.WhatIs(_trainingDir) != ResourceType.Directory)
                {
                    return;
                }

                // Checksum each training input file for the each domain
                foreach (VirtualPath file in _fileSystem.ListFiles(_trainingDir))
                {
                    if (file.Extension.Equals(".template", StringComparison.OrdinalIgnoreCase))
                    {
                        int checksum = await FileHelpers.CalculateFileCRC32C(file, fileSystem).ConfigureAwait(false);
                        string key = "training " + StringUtils.SanitizeFileName(file.Name);
                        if (checksumConfig.ContainsKey(key) && checksumConfig.GetInt32(key) != checksum)
                        {
                            string invalidDomain = ExtractDomainFromTemplateFile(file, fileSystem);
                            if (!string.IsNullOrEmpty(invalidDomain))
                            {
                                await ClearAllFilesForDomain(invalidDomain, realTime).ConfigureAwait(false);
                            }
                        }

                        checksumConfig.Set(key, checksum);
                    }
                }

                // Also look at each model config file
                if (_fileSystem.Exists(_modelConfigDir))
                {
                    foreach (VirtualPath file in _fileSystem.ListFiles(_modelConfigDir))
                    {
                        if (file.Name.EndsWith(".modelconfig.ini", StringComparison.OrdinalIgnoreCase))
                        {
                            int checksum = await FileHelpers.CalculateFileCRC32C(file, fileSystem).ConfigureAwait(false);
                            string key = "config " + StringUtils.SanitizeFileName(file.Name);
                            if (checksumConfig.ContainsKey(key) && checksumConfig.GetInt32(key) != checksum)
                            {
                                string invalidDomain = file.Name.Substring(0, file.Name.IndexOf('.'));
                                if (!string.IsNullOrEmpty(invalidDomain))
                                {
                                    await ClearAllFilesForDomain(invalidDomain, realTime).ConfigureAwait(false);
                                }
                            }

                            checksumConfig.Set(key, checksum);
                        }
                    }
                }

                // We could keep checksums for validation files too but there's no real benefit at this point
                //if (!fileSystem.Exists(_validationDir) || !fileSystem.IsContainer(_validationDir))
                //{
                //    return;
                //}

                //foreach (VirtualPath file in _fileSystem.ListFiles(_validationDir))
                //{
                //    if (file.Extension.Equals(".template", StringComparison.OrdinalIgnoreCase))
                //    {
                //        int checksum = CalculateFileChecksum(file, fileSystem);
                //        string key = "validation " + StringUtils.SanitizeFileName(file.Name);
                //        if (checksumConfig.ContainsKey(key) && checksumConfig.GetInt(key) != checksum)
                //        {
                //            string invalidDomain = ExtractDomainFromTemplateFile(file, fileSystem);
                //            if (!string.IsNullOrEmpty(invalidDomain))
                //            {
                //                ClearAllFilesForDomain(invalidDomain);
                //            }
                //        }
                //        checksumConfig.Set(key, checksum);
                //    }
                //}
            }
        }

        private static readonly Regex TEMPLATE_DOMAIN_PARSER = new Regex("^[^\\. ]+");

        private string ExtractDomainFromTemplateFile(VirtualPath templateFile, IFileSystem fileSystem)
        {
            // Since each template file can only have one domain, we don't have to read the file to find out what it is
            Match m = TEMPLATE_DOMAIN_PARSER.Match(templateFile.Name);
            if (m.Success)
            {
                return m.Value;
            }

            return null;
            
            //ISet<string> returnVal = new HashSet<string>();
            //using (StreamReader fileIn = new StreamReader(fileSystem.ReadStream(templateFile)))
            //{
            //    while (!fileIn.EndOfStream)
            //    {
            //        string l = fileIn.ReadLine();
            //        if (l != null && !l.StartsWith("#") && !l.StartsWith(";") && l.Contains("/") && l.Contains("\t"))
            //        {
            //            string domain = l.Substring(0, l.IndexOf('/'));
            //            if (!returnVal.Contains(domain))
            //                returnVal.Add(domain);
            //        }
            //    }
            //    fileIn.Close();
            //}
            //return returnVal;
        }

        private async Task ClearAllFilesForDomain(string domain, IRealTimeProvider realTime)
        {
            _logger.Log("Regenerating training data for \"" + domain + "\" domain (" + _locale + ")");
            // Delete .train and .validate cache files
            foreach (VirtualPath file in await _fileSystem.ListFilesAsync(_cacheDir).ConfigureAwait(false))
            {
                if (file.Name.StartsWith(domain, StringComparison.OrdinalIgnoreCase) &&
                    (file.Extension.Equals(".train", StringComparison.OrdinalIgnoreCase) ||
                    file.Extension.Equals(".validate", StringComparison.OrdinalIgnoreCase) ||
                    file.Extension.Equals(".tagfeatures", StringComparison.OrdinalIgnoreCase) ||
                    file.Extension.Equals(".domainfeatures", StringComparison.OrdinalIgnoreCase) ||
                    file.Extension.Equals(".whitelist", StringComparison.OrdinalIgnoreCase) ||
                    file.Extension.Equals(".blacklist", StringComparison.OrdinalIgnoreCase)))
                {
                    await _fileSystem.DeleteAsync(file).ConfigureAwait(false);
                }
            }

            // Delete model files and nmodelconfig files
            if (await _fileSystem.ExistsAsync(_modelDir).ConfigureAwait(false))
            {
                foreach (VirtualPath file in await _fileSystem.ListFilesAsync(_modelDir).ConfigureAwait(false))
                {
                    if (file.Name.StartsWith(domain, StringComparison.OrdinalIgnoreCase) &&
                        (file.Extension.Equals(".featureweights", StringComparison.OrdinalIgnoreCase) ||
                        file.Name.Equals(domain + ".modelconfig.ini", StringComparison.OrdinalIgnoreCase)))
                    {
                        await _fileSystem.DeleteAsync(file).ConfigureAwait(false);
                    }
                }
            }

            // Delete ranking data
            VirtualPath rankingData = _cacheDir.Combine("ranking.features");
            if (await _fileSystem.ExistsAsync(rankingData).ConfigureAwait(false))
            {
                await _fileSystem.DeleteAsync(rankingData).ConfigureAwait(false);
            }

            // Delete the common negative training data (if we don't delete this, a newly trained intent model may not pick up new intents)
            VirtualPath domainFeatures = _cacheDir.Combine("all.domainfeatures");
            if (await _fileSystem.ExistsAsync(domainFeatures).ConfigureAwait(false))
            {
                await _fileSystem.DeleteAsync(domainFeatures).ConfigureAwait(false);
            }

            // And reset the config
            if (_domainConfigs.ContainsKey(domain))
            {
                _domainConfigs.Remove(domain);
            }

            _domainConfigs[domain] = await IniFileConfiguration.Create(_logger, _modelDir.Combine(domain + ".modelconfig.ini"), _fileSystem, realTime).ConfigureAwait(false);
        }

        public static IList<VirtualPath> FindTrainingFilesForDomain(string domain, LanguageCode locale, IFileSystem luFileSystem, VirtualPath rootDir, ILogger logger)
        {
            return SearchForFiles(domain, locale, luFileSystem, logger, rootDir.Combine(RuntimeDirectoryName.TRAINING_DIR + "\\" + locale.ToBcp47Alpha2String()));
        }

        public static IList<VirtualPath> FindValidationFilesForDomain(string domain, LanguageCode locale, IFileSystem luFileSystem, VirtualPath rootDir, ILogger logger)
        {
            return SearchForFiles(domain, locale, luFileSystem, logger, rootDir.Combine(RuntimeDirectoryName.VALIDATION_DIR + "\\" + locale.ToBcp47Alpha2String()));
        }

        // TODO make this work for all locales, not just one at a time
        private static IList<VirtualPath> SearchForFiles(string domain, LanguageCode locale, IFileSystem luResourceManager, ILogger logger, VirtualPath rootDir)
        {
            IList<VirtualPath> returnVal = new List<VirtualPath>();
            if (!luResourceManager.Exists(rootDir))
            {
                return returnVal;
            }

            string fullNamePattern = domain + ".template";
            string partNamePattern = domain + " ";

            foreach (VirtualPath trainingFile in luResourceManager.ListFiles(rootDir))
            {
                // Inspect the filename and make sure it either equals or begins with the target domain.
                if (trainingFile.Name.Equals(fullNamePattern) || trainingFile.Name.StartsWith(partNamePattern))
                {
                    returnVal.Add(trainingFile);
                }
            }
            return returnVal;
        }

        private static readonly Regex _trainingFileDomainRegex = new Regex("^([a-zA-Z0-9_]+)( .+)?\\.template$");

        /// <summary>
        /// Uses template data files to determine all the domains for which we have training in this environment, for the given locale
        /// </summary>
        /// <param name="locale"></param>
        /// <param name="luFileSystem"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        public static ISet<string> GetAllKnownDomains(LanguageCode locale, IFileSystem luFileSystem, ILogger logger)
        {
            HashSet<string> returnVal = new HashSet<string>();
            
            VirtualPath trainingDir = new VirtualPath(RuntimeDirectoryName.TRAINING_DIR + ("\\" + locale.ToBcp47Alpha2String()));

            if (!luFileSystem.Exists(trainingDir))
            {
                return returnVal;
            }

            foreach (VirtualPath trainingFile in luFileSystem.ListFiles(trainingDir))
            {
                // Inspect the filename and make sure it either equals or begins with the target domain.
                Match m = _trainingFileDomainRegex.Match(trainingFile.Name);
                if (m.Success && !returnVal.Contains(m.Groups[1].Value))
                {
                    returnVal.Add(m.Groups[1].Value);
                }
            }

            return returnVal;
        }
    }
}
