using Durandal.API;
using Durandal.Common.Compression;
using Durandal.Common.File;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.NLP;
using Durandal.Common.NLP.Alignment;
using Durandal.Common.Statistics.Classification;
using Durandal.Common.Statistics.SharpEntropy;
using Durandal.Common.NLP.Feature;
using Durandal.Common.Collections.Indexing;
using Durandal.Common.NLP.Tagging;
using Durandal.Common.Statistics;
using Durandal.Common.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Durandal.Common.NLP.Language;
using System.Buffers;
using Durandal.Common.IO;
using Durandal.Common.Collections;

namespace Durandal.Common.LG.Statistical
{
    /// <summary>
    /// Represents a single phrase that is modeled by statistical LG.
    /// </summary>
    public class StatisticalLGPhrase
    {
        private const int VERSION_NUM = 7;

        /// <summary>
        /// Used to extract valid SSML markup from within the text of a phrase
        /// </summary>
        private static readonly Regex SSML_TAG_RIPPER = new Regex("<\\/?(?:speak|say-as|break|p|phoneme|s|voice|emphasis|prosody|sub)(?: .+?>|\\/?>)");

        private const float TRAINING_QUALITY = 1.0f;
        private List<LGSurfaceForm[]> _surfaceForms;
        private IDictionary<string, int> _tagToGroupMapping;
        private IDictionary<int, string> _groupToTagMapping;
        private NeuralModel[] _decisionModels;
        private ILogger _logger;
        private string _modelName;
        private LanguageCode _phraseLocale;
        private int _inputDataHash;
        private bool _debugMode;

        private ILGFeatureExtractor _featureExtractor;
        private IWordBreaker _wordBreaker;

        public IEnumerable<string> SubstitutionFieldNames
        {
            get
            {
                return _tagToGroupMapping.Keys;
            }
        }

        /// <summary>
        /// Main constructor which accepts full training data and NL tools, as well as an optional input stream
        /// pointing to a cached model, which will be used if possible to avoid retraining.
        /// </summary>
        /// <param name="modelName"></param>
        /// <param name="locale"></param>
        /// <param name="logger"></param>
        /// <param name="wordBreaker"></param>
        /// <param name="featureExtractor"></param>
        /// <param name="debug"></param>
        public StatisticalLGPhrase(string modelName, LanguageCode locale, ILogger logger, IWordBreaker wordBreaker, ILGFeatureExtractor featureExtractor, bool debug = false)
        {
            _logger = logger;
            _featureExtractor = featureExtractor;
            _modelName = modelName;
            _phraseLocale = locale;
            _wordBreaker = wordBreaker;
            _debugMode = debug;
        }

        /// <summary>
        /// An alternate constructor which assumes that the model will be loaded directly from a serialized form.
        /// If loading fails there is no fallback and this method will throw an InvalidDataException.
        /// You do not need to call Initialize() after using this constructor.
        /// </summary>
        /// <param name="modelName"></param>
        /// <param name="locale"></param>
        /// <param name="logger"></param>
        /// <param name="featureExtractor"></param>
        /// <param name="modelCacheManager"></param>
        /// <param name="cachedFileName"></param>
        public StatisticalLGPhrase(string modelName, LanguageCode locale, ILogger logger, ILGFeatureExtractor featureExtractor, IFileSystem modelCacheManager, VirtualPath cachedFileName)
        {
            _logger = logger;
            _featureExtractor = featureExtractor;
            _modelName = modelName;
            _phraseLocale = locale;

            if (cachedFileName == null)
            {
                throw new ArgumentNullException("cachedFileName");
            }

            if (!TryInitFromCachedFile(modelCacheManager, cachedFileName, modelName, locale, 0, true))
            {
                throw new InvalidDataException("The stream for the cached LG model was invalid, could not be read, or the model is in an unknown format");
            }
        }

        public void Initialize(IEnumerable<string> training)
        {
            bool dummy;
            Initialize(training, out dummy, null, null);
        }

        public void Initialize(IEnumerable<string> training, out bool loadedFromCache, IFileSystem fileSystem, VirtualPath cacheFileName)
        {
            if (training == null)
            {
                throw new ArgumentNullException("training");
            }

            // Calculate the hash of all the inputs
            // Fixme: this is multiple enumeration of the input which could cause problems
            _inputDataHash = 0;
            foreach (string x in training)
            {
                _inputDataHash += x.GetHashCode();
            }

            // First see if we have a valid cache
            if (fileSystem != null && cacheFileName != null)
            {
                if (TryInitFromCachedFile(fileSystem, cacheFileName, _modelName, _phraseLocale, _inputDataHash, false))
                {
                    loadedFromCache = true;
                    return;
                }
            }

            loadedFromCache = false;

            // The model failed to load or the hash did not match, meaning the training has been invalidated.
            // In this case, we recreate and retrain this entire model
            _tagToGroupMapping = new Dictionary<string, int>();
            _groupToTagMapping = new Dictionary<int, string>();

            GroupingAlignmentResult alignment = LexicalAlignment.PerformGroupingAlignment(training, _wordBreaker, _logger, 5, _debugMode);

            _surfaceForms = alignment.Groups;

            if (_debugMode)
            {
                foreach (LGSurfaceForm[] groups in alignment.Groups)
                {
                    string[] reifiedSurfaceForms = new string[groups.Length];
                    for (int c = 0; c < groups.Length; c++)
                    {
                        reifiedSurfaceForms[c] = groups[c].ToString();
                    }

                    _logger.Log("{" + string.Join("|", reifiedSurfaceForms) + "}", LogLevel.Vrb);
                }
            }

            // Figure out which tags go to which groups
            for (int c = 0; c < _surfaceForms.Count; c++)
            {
                foreach (LGSurfaceForm line in _surfaceForms[c])
                {
                    if (line.Tokens.Count > 0)
                    {
                        string tag = line.Tokens[0].Tag;
                        if (!string.IsNullOrEmpty(tag) && !_tagToGroupMapping.ContainsKey(tag))
                        {
                            _tagToGroupMapping[tag] = c;
                            _groupToTagMapping[c] = tag;
                            break;
                        }
                    }
                }
            }

            int numGroups = _surfaceForms.Count;

            // Extract features from the training against the lattice to use as inputs for the classifier
            List<TrainingEvent>[] trainingEvents = AlignTrainingWithLatticeAndExtractFeatures(alignment.TaggedInputs, _surfaceForms, _featureExtractor, _groupToTagMapping, numGroups, _logger, _modelName);

            // Train the statistical classifier
            _decisionModels = new NeuralModel[numGroups];
            Stopwatch timer = Stopwatch.StartNew();
            for (int c = 0; c < numGroups; c++)
            {
                // See if a model is even necessary (only necessary when the next group has more than 1 choice and it is not a tag group)
                if (_surfaceForms[c].Length > 1 && !_groupToTagMapping.ContainsKey(c))
                {
                    //VirtualPath modelCacheFile = new VirtualPath(DirectoryName.CACHE_DIR + "\\" + _modelName + " " + _phraseLocale + " " + c);
                    _decisionModels[c] = new NeuralModel(_logger);
                    _decisionModels[c].Train(new BasicTrainingEventReader(trainingEvents[c]), null, null, TRAINING_QUALITY);

                    //ITrainingDataIndexer indexer = new OnePassDataIndexer(new BasicTrainingEventReader(trainingEvents[c]));
                    //GisTrainer trainer = new GisTrainer(_stringIndex);
                    //trainer.TrainModel(100, indexer);
                    //_decisionModels[c] = new GisModel(trainer);

                    // test: evaluate the model on the first training input
                    //var outcomes = _decisionModels[c].Classify(trainingEvents[c][0].Context);
                    //_logger.Log(string.Join(",", trainingEvents[c][0].Context));
                    //_logger.Log(string.Join(",", outcomes));
                }
            }

            timer.Stop();
            _logger.Log(CommonInstrumentation.GenerateInstancedLatencyEntry(CommonInstrumentation.Key_Latency_LG_Train, _modelName, timer), LogLevel.Ins);

            // To save some memory, forget extra surface forms that are part of tag groups (since they will be overwritten anyways)
            foreach (int tagGroup in _groupToTagMapping.Keys)
            {
                if (_surfaceForms[tagGroup].Length > 1)
                {
                    _surfaceForms[tagGroup] = new LGSurfaceForm[1] { _surfaceForms[tagGroup][0] };
                }
            }

            // Cache the result
            if (fileSystem != null && cacheFileName != null)
            {
                Serialize(fileSystem, cacheFileName);
            }
        }

        private List<TrainingEvent>[] AlignTrainingWithLatticeAndExtractFeatures(
            List<TaggedSentence> sentences,
            List<LGSurfaceForm[]> surfaceForms,
            ILGFeatureExtractor featureExtractor,
            IDictionary<int, string> groupToTagMapping,
            int numGroups,
            ILogger logger,
            string modelName)
        {
            List<TrainingEvent>[] trainingEvents = new List<TrainingEvent>[numGroups];
            for (int c = 0; c < numGroups; c++)
            {
                trainingEvents[c] = new List<TrainingEvent>();
            }

            int[] offsets = new int[numGroups];
            int offsetIdx = 0;
            int offsetCounter = 0;

            // Make a big regular expression representing the lattice
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                StringBuilder regexBuilder = pooledSb.Builder;
                regexBuilder.Append("^");
                int formIdx = 0;
                foreach (LGSurfaceForm[] forms in surfaceForms)
                {
                    IList<string> options = new List<string>();
                    foreach (LGSurfaceForm form in forms)
                    {
                        string stringForm = "(" + Regex.Escape(form.ToString()) + ")";
                        options.Add(stringForm);
                    }

                    if (groupToTagMapping.ContainsKey(formIdx))
                    {
                        // wrap substitution points with explicit boundary tokens to distinguish
                        // tokens that are within tag bounds from those that are without
                        regexBuilder.Append(TAG_BOUND_REGEX_CHAR);
                    }

                    // output the whole list of options as a non-capture group representing this entire surface form
                    regexBuilder.Append("(?:");
                    regexBuilder.Append(string.Join("|", options));
                    regexBuilder.Append(")");

                    if (groupToTagMapping.ContainsKey(formIdx))
                    {
                        // as above
                        regexBuilder.Append(TAG_BOUND_REGEX_CHAR);
                    }

                    offsets[offsetIdx++] = offsetCounter;
                    offsetCounter += options.Count;
                    formIdx++;
                }
                regexBuilder.Append("$");

                Regex finalRegex = new Regex(regexBuilder.ToString());

                foreach (TaggedSentence sentence in sentences)
                {
                    Match m = finalRegex.Match(BuildBoundedAlignmentStringFromTaggedData(sentence));
                    if (!m.Success)
                    {
                        logger.Log("LG training data did not match back up with the alignment lattice for " + modelName + ". \"" + sentence.Utterance.OriginalText + "\" will be skipped. Adding more training data will usually fix this", LogLevel.Wrn);
                        continue;
                    }

                    // Determine which path was taken based on the capture groups that matched
                    offsetIdx = 0;
                    int[] choices = new int[numGroups];
                    for (int captureGroup = 1; captureGroup < m.Groups.Count; captureGroup++)
                    {
                        if (m.Groups[captureGroup].Success)
                        {
                            choices[offsetIdx] = (captureGroup - 1) - offsets[offsetIdx];
                            offsetIdx++;
                        }
                    }

                    IList<string> choiceFeatures = new List<string>();
                    for (int currentGroup = 0; currentGroup < surfaceForms.Count; currentGroup++)
                    {
                        if (surfaceForms[currentGroup].Length > 1 && !groupToTagMapping.ContainsKey(currentGroup))
                        {
                            string choice = choices[currentGroup].ToString();
                            List<string> currentFeatures = new List<string>();

                            // Extract features from the tags
                            IDictionary<string, string> tags = ExtractTagPhrases(sentence);
                            featureExtractor.ExtractTagFeatures(tags, groupToTagMapping, currentGroup, currentFeatures);

                            // Also append the decision history as features
                            currentFeatures.FastAddRangeList(choiceFeatures);

                            if (currentFeatures.Count > 0)
                            {
                                TrainingEvent newEvent = new TrainingEvent(choice, currentFeatures.ToArray());
                                trainingEvents[currentGroup].Add(newEvent);
                            }

                            // This feature is for the choice that was actually taken by this specific training instance.
                            choiceFeatures.Add("g-" + currentGroup + "=" + choice);
                        }
                    }
                }

                return trainingEvents;
            }
        }

        private const string TAG_BOUND_CHAR = "\n";
        private const string TAG_BOUND_REGEX_CHAR = "\\\n";

        /// <summary>
        /// There is a major bug in the regex matcher where a capture group's bounds could "leak" into the area reserved for
        /// substitutions, which would lead to false positives in the branching model and lead to bad sentences.
        /// So this function's purpose is to insert hard breaks at all substitution (tag) boundaries so the regex is forced to honor them.
        /// </summary>
        /// <param name="sentence"></param>
        /// <returns></returns>
        private static string BuildBoundedAlignmentStringFromTaggedData(TaggedSentence sentence)
        {
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                StringBuilder returnVal = pooledSb.Builder;
                int numWords = sentence.Utterance.Length;
                string currentTag = "O";
                for (int wordIdx = 0; wordIdx < numWords; wordIdx++)
                {
                    string tag = string.Join(",", sentence.Words[wordIdx].Tags);
                    if (!string.Equals("O", currentTag) && !string.Equals(currentTag, tag))
                    {
                        returnVal.Append(TAG_BOUND_CHAR);
                    }
                    returnVal.Append(sentence.Utterance.NonTokens[wordIdx]);
                    if (!string.Equals("O", tag) && !string.Equals(currentTag, tag))
                    {
                        returnVal.Append(TAG_BOUND_CHAR);
                    }
                    returnVal.Append(sentence.Utterance.Words[wordIdx]);
                    currentTag = tag;
                }

                if (!string.Equals("O", currentTag))
                {
                    returnVal.Append(TAG_BOUND_CHAR);
                }

                returnVal.Append(sentence.Utterance.NonTokens[numWords]);
                return returnVal.ToString();
            }
        }

        private static List<TrainingEvent>[] AlignTrainingWithLatticeAndExtractFeaturesOld(
            List<TaggedSentence> sentences,
            List<LGSurfaceForm[]> surfaceForms,
            ILGFeatureExtractor featureExtractor,
            IDictionary<int, string> groupToTagMapping,
            int numGroups,
            ILogger logger,
            string modelName)
        {
            List<TrainingEvent>[] trainingEvents = new List<TrainingEvent>[numGroups];
            for (int c = 0; c < numGroups; c++)
            {
                trainingEvents[c] = new List<TrainingEvent>();
            }

            IList<string> choiceFeatures = new List<string>();
            foreach (TaggedSentence sentence in sentences)
            {
                int currentGroup = 0;
                int currentToken = 0;
                while (currentGroup < surfaceForms.Count)
                {
                    // Iterate through tokens in the tagged data, and see which LG groups they map to
                    int choiceIdx;
                    int tokensRemaining = sentence.Words.Count - currentToken;
                    bool matchFound = false;
                    int emptyGroupIdx = -1;

                    for (choiceIdx = 0; choiceIdx < surfaceForms[currentGroup].Length; choiceIdx++)
                    {
                        LGSurfaceForm group = surfaceForms[currentGroup][choiceIdx];
                        int groupLen = group.Length;
                        if (tokensRemaining < groupLen)
                            continue;

                        bool mismatch = false;
                        int tokenIdx = 0;
                        int sentenceWordIdx = currentToken;
                        for (tokenIdx = 0; tokenIdx < groupLen && sentenceWordIdx < sentence.Words.Count;)
                        {
                            LGToken token = group.Tokens[tokenIdx];
                            if (string.IsNullOrEmpty(token.Token))
                            {
                                // It's an empty token (or I should say, the empty branch of a conditional subphrase) that we just skip over.
                                // Save this for a fallback later
                                emptyGroupIdx = currentGroup;
                                break;
                            }

                            if (!token.Token.Equals(sentence.Words[sentenceWordIdx].Word))
                            {
                                mismatch = true;
                                break;
                            }

                            tokenIdx++;
                            sentenceWordIdx++;
                        }

                        if (mismatch)
                        {
                            continue;
                        }

                        matchFound = true;
                        break;
                    }

                    if (!matchFound && emptyGroupIdx < 0)
                    {
                        // This error can sometimes happen for very sparse data sets or some cases where the wordbreaker doesn't understand the input.
                        logger.Log("LG training data did not match back up with the alignment lattice for " + modelName + ". \"" + sentence.Utterance.OriginalText + "\" will be skipped. Adding more training data will usually fix this", LogLevel.Wrn);
                        break;
                    }

                    if (!matchFound && emptyGroupIdx >= 0)
                    {
                        currentToken += surfaceForms[currentGroup][choiceIdx].Length;
                        currentGroup++;
                    }
                    else
                    {
                        // Extract features for this decision
                        // Only make a feature for branches where there actually is a choice
                        if (surfaceForms[currentGroup].Length > 1 && !groupToTagMapping.ContainsKey(currentGroup))
                        {
                            List<string> currentFeatures = new List<string>();

                            // Extract features from the tags
                            IDictionary<string, string> tags = ExtractTagPhrases(sentence);
                            featureExtractor.ExtractTagFeatures(tags, groupToTagMapping, currentGroup, currentFeatures);

                            // Also append the decision history as features
                            currentFeatures.FastAddRangeList(choiceFeatures);

                            if (currentFeatures.Count > 0)
                            {
                                TrainingEvent newEvent = new TrainingEvent(choiceIdx.ToString(), currentFeatures.ToArray());
                                trainingEvents[currentGroup].Add(newEvent);
                            }

                            // This feature is for the choice that was actually taken by this specific training instance.
                            choiceFeatures.Add("g-" + currentGroup + "=" + choiceIdx.ToString());
                        }

                        currentToken += surfaceForms[currentGroup][choiceIdx].Length;
                        currentGroup++;
                    }
                }

                choiceFeatures.Clear();
            }

            return trainingEvents;
        }

        private static IDictionary<string, string> ExtractTagPhrases(TaggedSentence sentence)
        {
            Dictionary<string, string> returnVal = new Dictionary<string, string>();
            string currentTag = null;
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                StringBuilder wordBuilder = pooledSb.Builder;
                for (int tok = 0; tok < sentence.Words.Count; tok++)
                {
                    TaggedWord word = sentence.Words[tok];
                    string thisTag = string.Join(string.Empty, word.Tags);

                    if (!string.Equals(currentTag, thisTag))
                    {
                        // New tag boundary.
                        if (currentTag != null)
                        {
                            if (!currentTag.Equals("O"))
                            {
                                returnVal.Add(currentTag, wordBuilder.ToString());
                            }

                            wordBuilder.Clear();
                        }

                        currentTag = thisTag;
                    }

                    if (wordBuilder.Length > 0)
                    {
                        wordBuilder.Append(sentence.Utterance.NonTokens[tok]);
                    }

                    wordBuilder.Append(word.Word);
                }

                if (wordBuilder.Length > 0 && !currentTag.Equals("O"))
                {
                    returnVal.Add(currentTag, wordBuilder.ToString());
                }

                return returnVal;
            }
        }

        /// <summary>
        /// Applies substitutions to this phrase to generate the surface form statistically
        /// </summary>
        /// <param name="substitutions"></param>
        /// <param name="isSSML"></param>
        /// <param name="queryLogger"></param>
        /// <returns></returns>
        public string Render(IDictionary<string, string> substitutions, bool isSSML, ILogger queryLogger)
        {
            IDictionary<string, string> filledSubstitutions = new Dictionary<string, string>();
            foreach (string tagName in _groupToTagMapping.Values)
            {
                if (!substitutions.ContainsKey(tagName))
                {
                    filledSubstitutions[tagName] = string.Empty;
                }
                else
                {
                    filledSubstitutions[tagName] = substitutions[tagName];
                }
            }

            queryLogger.Log("Evaluating LG pattern " + this._modelName, LogLevel.Vrb);


            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                StringBuilder returnVal = pooledSb.Builder;
                if (isSSML)
                {
                    returnVal.AppendFormat("<speak version=\"1.0\" " +
                                     "xmlns=\"http://www.w3.org/2001/10/synthesis\" " +
                                     //"xmlns:mstts=\"http://www.w3.org/2001/mstts\" " +
                                     //"xmlns:emo=\"http://www.w3.org/2009/10/emotionml\" " +
                                     "xml:lang=\"{0}\">", _phraseLocale);
                }

                List<string> choiceFeatures = new List<string>();

                // Crawl through the statistical tree and evaluate each branch
                for (int curNode = 0; curNode < _surfaceForms.Count; curNode++)
                {
                    // Is this group a tag substitution point?
                    if (_groupToTagMapping.ContainsKey(curNode))
                    {
                        string desiredTag = _groupToTagMapping[curNode];

                        LGSurfaceForm thisGroup = _surfaceForms[curNode][0];
                        // Appending pre- and post-nontokens technically inserts whitespace that is
                        // not part of the tag field, but it is necessary in order to preserve
                        // proper whitespace when tag bounds are adjacent to others or to the sentence bounds
                        // The hackish part here is that we just picked the default surface form [0] when choosing
                        // non-tokens to insert, which breaks if different surface forms have different surrounding whitespace
                        returnVal.Append(thisGroup.Tokens[0].NonTokensPre);
                        returnVal.Append(filledSubstitutions[desiredTag]);
                        returnVal.Append(thisGroup.Tokens[thisGroup.Length - 1].NonTokensPost);
                        if (_debugMode)
                        {
                            queryLogger.Log("EMITTING SUBSTITUTION\t\"" + filledSubstitutions[desiredTag] + "\"", LogLevel.Vrb);
                        }
                    }

                    // Is there a model for this node?
                    else if (_decisionModels[curNode] != null)
                    {
                        if (_debugMode)
                        {
                            queryLogger.Log("Evaluating model at node " + curNode, LogLevel.Vrb);
                        }

                        // Extract tag features from the substitutions
                        List<string> currentFeatures = new List<string>();
                        currentFeatures.FastAddRangeList(choiceFeatures);
                        _featureExtractor.ExtractTagFeatures(filledSubstitutions, _groupToTagMapping, curNode, currentFeatures);
                        string[] context = currentFeatures.ToArray();
                        
                        // Evaluate the model
                        var model = _decisionModels[curNode];
                        Hypothesis<string>? outcome = model.Classify(context);
                        if (!outcome.HasValue)
                        {
                            throw new Exception("Invalid lattice transition while evaluating LG model " + _modelName + ":" + _phraseLocale + ". Rendered so far: \"" + returnVal.ToString() + "\". This is normally caused by mismatched opening/closing tags inside LG training data.");
                        }

                        string bestPathString = outcome.Value.Value;

                        int bestPath = int.Parse(bestPathString);
                        LGSurfaceForm thisGroup = _surfaceForms[curNode][bestPath];

                        if (_debugMode)
                        {
                            queryLogger.Log(string.Join(",", context), LogLevel.Vrb);
                            queryLogger.Log(string.Join(",", model.GetOutcomeNames()), LogLevel.Vrb);
                            queryLogger.Log("Outcome is " + outcome.Value.ToString(), LogLevel.Vrb);
                            queryLogger.Log("EMITTING DYNAMIC TOKEN[" + curNode + "]\t\"" + thisGroup.ToString() + "\"", LogLevel.Vrb);
                        }

                        foreach (LGToken token in thisGroup.Tokens)
                        {
                            returnVal.Append(token.NonTokensPre);
                            returnVal.Append(token.Token);
                            returnVal.Append(token.NonTokensPost);
                        }

                        // Add the feature for the path we just took
                        choiceFeatures.Add("g-" + curNode + "=" + bestPathString);
                    }
                    // There's no decision; just iterate to the next chain
                    else
                    {
                        LGSurfaceForm thisGroup = _surfaceForms[curNode][0];
                        if (_debugMode)
                        {
                            queryLogger.Log("EMITTING FIXED TOKEN[" + curNode + "]\t\"" + thisGroup.ToString() + "\"", LogLevel.Vrb);
                        }

                        foreach (LGToken token in thisGroup.Tokens)
                        {
                            returnVal.Append(token.NonTokensPre);
                            returnVal.Append(token.Token);
                            returnVal.Append(token.NonTokensPost);
                        }
                    }
                }

                // If we are not rendering SSML, strip the applicable tags from the output
                if (!isSSML)
                {
                    return StringUtils.RegexRemove(SSML_TAG_RIPPER, returnVal.ToString());
                }
                else
                {
                    returnVal.Append("</speak>");
                    return returnVal.ToString();
                }
            }
        }

        private void Serialize(IFileSystem fileSystem, VirtualPath cachedFileName)
        {
            try
            {
                using (Stream writeStream = fileSystem.OpenStream(cachedFileName, FileOpenMode.Create, FileAccessMode.Write))
                {
                    using (BinaryWriter writer = new BinaryWriter(writeStream, StringUtils.UTF8_WITHOUT_BOM, true))
                    {
                        writer.Write(VERSION_NUM);
                        writer.Write(_modelName);
                        writer.Write(_phraseLocale.ToBcp47Alpha3String());
                        writer.Write(_inputDataHash);

                        if (_tagToGroupMapping == null)
                        {
                            writer.Write((int)0);
                        }
                        else
                        {
                            writer.Write(_tagToGroupMapping.Count);
                            foreach (var kvp in _tagToGroupMapping)
                            {
                                writer.Write(kvp.Key);
                                writer.Write(kvp.Value);
                            }
                        }

                        if (_groupToTagMapping == null)
                        {
                            writer.Write((int)0);
                        }
                        else
                        {
                            writer.Write(_groupToTagMapping.Count);
                            foreach (var kvp in _groupToTagMapping)
                            {
                                writer.Write(kvp.Key);
                                writer.Write(kvp.Value);
                            }
                        }

                        writer.Write(_surfaceForms.Count);
                        foreach (LGSurfaceForm[] formArray in _surfaceForms)
                        {
                            writer.Write(formArray.Length);
                            foreach (LGSurfaceForm surfaceForm in formArray)
                            {
                                writer.Write(surfaceForm.Length);
                                foreach (LGToken token in surfaceForm.Tokens)
                                {
                                    writer.Write(token.Token ?? "");
                                    writer.Write(token.Tag ?? "");
                                    writer.Write(token.NonTokensPre ?? "");
                                    writer.Write(token.NonTokensPost ?? "");

                                    if (token.Attributes == null)
                                    {
                                        writer.Write((int)0);
                                    }
                                    else
                                    {
                                        writer.Write(token.Attributes.Count);
                                        foreach (var kvp in token.Attributes)
                                        {
                                            writer.Write(kvp.Key);
                                            writer.Write(kvp.Value);
                                        }
                                    }
                                }
                            }
                        }

                        writer.Write(_decisionModels.Length);
                        foreach (NeuralModel decisionModel in _decisionModels)
                        {
                            if (decisionModel == null)
                            {
                                writer.Write(false);
                            }
                            else
                            {
                                writer.Write(true);
                                decisionModel.Serialize(writer);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _logger.Log("An error occurred while serializing LG model \"" + _modelName + ":" + _phraseLocale + "\": " + e.Message, LogLevel.Err);
            }
        }

        private bool TryInitFromCachedFile(IFileSystem fileSystem, VirtualPath cacheFileName, string modelName, LanguageCode locale, int inputDataHash, bool force)
        {
            try
            {
                if (!fileSystem.Exists(cacheFileName))
                {
                    return false;
                }

                using (Stream readStream = fileSystem.OpenStream(cacheFileName, FileOpenMode.Open, FileAccessMode.Read))
                {
                    using (BinaryReader reader = new BinaryReader(readStream, StringUtils.UTF8_WITHOUT_BOM, true))
                    {
                        int versionNum = reader.ReadInt32();
                        if (versionNum != VERSION_NUM)
                        {
                            _logger.Log("Cached LG phrase model " + cacheFileName + " claims to be version " + versionNum + " but I am expecting " + VERSION_NUM + "; ignoring", LogLevel.Wrn);
                            return false;
                        }

                        _modelName = reader.ReadString();
                        _phraseLocale = LanguageCode.Parse(reader.ReadString());
                        _inputDataHash = reader.ReadInt32();

                        if (!force && inputDataHash != _inputDataHash)
                        {
                            // Hash mismatch
                            return false;
                        }

                        _tagToGroupMapping = new Dictionary<string, int>();
                        int size = reader.ReadInt32();
                        for (int c = 0; c < size; c++)
                        {
                            string key = reader.ReadString();
                            int value = reader.ReadInt32();
                            _tagToGroupMapping[key] = value;
                        }

                        _groupToTagMapping = new Dictionary<int, string>();
                        size = reader.ReadInt32();
                        for (int c = 0; c < size; c++)
                        {
                            int key = reader.ReadInt32();
                            string value = reader.ReadString();
                            _groupToTagMapping[key] = value;
                        }

                        int surfaceFormCount = reader.ReadInt32();
                        _surfaceForms = new List<LGSurfaceForm[]>(surfaceFormCount);
                        for (int x = 0; x < surfaceFormCount; x++)
                        {
                            int formArrayCount = reader.ReadInt32();
                            LGSurfaceForm[] formArray = new LGSurfaceForm[formArrayCount];
                            for (int y = 0; y < formArrayCount; y++)
                            {
                                LGSurfaceForm surfaceForm = new LGSurfaceForm();
                                int formLength = reader.ReadInt32();
                                for (int z = 0; z < formLength; z++)
                                {
                                    string tok = reader.ReadString();
                                    string tag = reader.ReadString();
                                    string pre = reader.ReadString();
                                    string post = reader.ReadString();
                                    LGToken token = new LGToken(tok, pre, post, string.IsNullOrEmpty(tag) ? null : tag);
                                    int numAttributes = reader.ReadInt32();
                                    for (int w = 0; w < numAttributes; w++)
                                    {
                                        string k = reader.ReadString();
                                        string v = reader.ReadString();
                                        token.Attributes.Add(k, v);
                                    }

                                    surfaceForm.Tokens.Add(token);
                                }

                                formArray[y] = surfaceForm;
                            }

                            _surfaceForms.Add(formArray);
                        }

                        int numDecisionModels = reader.ReadInt32();
                        _decisionModels = new NeuralModel[numDecisionModels];
                        for (int x = 0; x < numDecisionModels; x++)
                        {
                            bool modelExists = reader.ReadBoolean();
                            if (modelExists)
                            {
                                _decisionModels[x] = NeuralModel.Deserialize(reader, _logger);
                            }
                            else
                            {
                                _decisionModels[x] = null;
                            }
                        }
                    }
                }

                return true;
            }
            catch (FileNotFoundException)
            {
                _logger.Log("No cached LG model found for \"" + modelName + ":" + locale + "\"; retraining will be required", LogLevel.Vrb);
            }
            catch (Exception e)
            {
                _logger.Log("Error occurred while loading cached LG model \"" + modelName + ":" + locale + "\"", LogLevel.Err);
                _logger.Log(e, LogLevel.Err);
            }

            return false;
        }
    }
}
