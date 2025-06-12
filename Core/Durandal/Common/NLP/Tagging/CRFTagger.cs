
namespace Durandal.Common.NLP.Tagging
{
    using Durandal.API;
        using Durandal.Common.Config;
    using Durandal.Common.Dialog;
    using Durandal.Common.File;
    using Durandal.Common.Logger;
    using Durandal.Common.NLP.Alignment;
    using Durandal.Common.Statistics.Classification;
    using Durandal.Common.Statistics.SharpEntropy;
    using Durandal.Common.NLP.Feature;
    using Durandal.Common.Collections.Indexing;
    using Durandal.Common.NLP.Train;
    using Durandal.Common.Statistics;
    using System;
    using System.Collections.Generic;
    using Durandal.Common.Utils;
    using Durandal.Common.ServiceMgmt;

    public class CRFTagger
    {
        private readonly WeakPointer<ICompactIndex<string>> _stringIndex;
        private readonly IDictionary<Compact<string>, int> _nodeNameMapping;
        private readonly ILogger _logger;
        private readonly IFileSystem _fileSyste;
        private readonly IWordBreaker _wordBreaker;
        private IStatisticalTrainer _modelTrainer;
        private IStatisticalClassifier[] classifiers;
        // this field is really only used for debugging right now
        private string _domainIntent = string.Empty;

        private ISet<string> _knownTags;
        private float _confidenceCutoff;

        // Determines how convoluted the lattice should become. Lower numbers
        // yield more possible alternates, but also take more time to calculate
        private const float BRANCH_THRESHOLD = 0.4f;
        private const int LATTICE_HEIGHT = 4;
        private const string STKN = "stkn";
        private const string ETKN = "etkn";
        private const string NOTAG = "O";
        private const float MODEL_QUALITY = 0.5f;

        /// <summary>
        /// Creates a new CRF tagger but does not initialize it
        /// </summary>
        /// <param name="modelTrainer">The training implementation</param>
        /// <param name="logger">A logger</param>
        /// <param name="minConfidence">The minimum confidence of hypotheses to output</param>
        /// <param name="fileSystem">A resource manager for accessing training and cache files</param>
        /// <param name="masterStringIndex">A shared string pool to use for memory compaction</param>
        /// <param name="wordBreaker">An optional wordbreaker, used for lexical parsing in some cases</param>
        public CRFTagger(IStatisticalTrainer modelTrainer, ILogger logger, float minConfidence, IFileSystem fileSystem, WeakPointer<ICompactIndex<string>> masterStringIndex, IWordBreaker wordBreaker = null)
        {
            _logger = logger;
            _knownTags = new HashSet<string>();
            _stringIndex = masterStringIndex;
            _nodeNameMapping = new Dictionary<Compact<string>, int>();
            _confidenceCutoff = minConfidence;
            _fileSyste = fileSystem;
            _wordBreaker = wordBreaker;
            _modelTrainer = modelTrainer;
        }

        public static bool IsTrainingRequired(IStatisticalTrainer modelTrainer, VirtualPath cacheFileRoot, IFileSystem fileSyste, IConfiguration domainConfig, string intent, ILogger logger)
        {
            // Are there tags for this intent?
            if (domainConfig.ContainsKey("tags_" + intent) &&
                domainConfig.GetStringList("tags_" + intent).Count == 1)
            {
                return false;
            }

            if (domainConfig.ContainsKey("nodenames_" + intent))
            {
                foreach (string classifierTag in domainConfig.GetStringList("nodenames_" + intent))
                {
                    if (modelTrainer.IsTrainingRequired(
                            cacheFileRoot + " " + classifierTag))
                    {
                        return true;
                    }
                }

                return false;
            }
            else
            {
                //Invalid config - tags are listed for this intent, but there is no listing of the tag nodes. Assume the crfs are untrained
                return true;
            }
        }

        /// <summary>
        /// Trains a tagger, either from raw training data or from a previously created cache
        /// </summary>
        /// <param name="trainingFileName"></param>
        /// <param name="domain"></param>
        /// <param name="intent"></param>
        /// <param name="cacheFileRoot"></param>
        /// <param name="domainConfig"></param>
        public void TrainFromData(
            VirtualPath trainingFileName,
            string domain,
            string intent,
            VirtualPath cacheFileRoot,
            IConfiguration domainConfig)
        {
            _nodeNameMapping.Clear();
            _domainIntent = domain + "/" + intent;
            // Gather the list of known tags from the data
            foreach (string tag in domainConfig.GetStringList("alltags"))   
            {
                _knownTags.Add(tag);
            }

            ISet<string> nodeNames = new HashSet<string>();

            // Create the list of nodes in the CRF graph
            List<TrainingDataList<SimpleOutcomeFeature>> trainingData = new List<TrainingDataList<SimpleOutcomeFeature>>();
            Compact<string> nodeName = _stringIndex.Value.Store(STKN);
            _nodeNameMapping[nodeName] = 0;
            nodeNames.Add(STKN);
            trainingData.Add(new TrainingDataList<SimpleOutcomeFeature>(SimpleOutcomeFeature.CreateStatic));
            
            if (!IsTrainingRequired(_modelTrainer, cacheFileRoot, _fileSyste, domainConfig, intent, _logger))
            {
                // Load the cache
                TrainingDataList<SimpleOutcomeFeature> dummy = new TrainingDataList<SimpleOutcomeFeature>(SimpleOutcomeFeature.CreateStatic);
                foreach (string n in domainConfig.GetStringList("nodenames_" + intent))
                {
                    Compact<string> one = _stringIndex.Value.Store(n);
                    if (!_nodeNameMapping.ContainsKey(one))
                    {
                        _nodeNameMapping[one] = _nodeNameMapping.Count;
                        nodeNames.Add(n);
                    }
                }

                classifiers = new IStatisticalClassifier[_nodeNameMapping.Count];
                foreach (Compact<string> classifierTag in _nodeNameMapping.Keys)
                {
                    string thisClassifierTag = _stringIndex.Value.Retrieve(classifierTag);
                    int nodeIndex = _nodeNameMapping[classifierTag];
                    classifiers[nodeIndex] = _modelTrainer.TrainClassifier(new FeatureReader(dummy), cacheFileRoot + " " + thisClassifierTag, _stringIndex.Value, thisClassifierTag, MODEL_QUALITY);
                }
            }
            else
            {
                // Load the training data
                TrainingDataList<TagFeature> tagData = new TrainingDataList<TagFeature>(trainingFileName, _fileSyste, _logger, TagFeature.CreateStatic);
                
                foreach (TagFeature feature in tagData.TrainingData)
                {
                    Compact<string> one = _stringIndex.Value.Store(feature.SourceTag);
                    if (!_nodeNameMapping.ContainsKey(one))
                    {
                        _nodeNameMapping[one] = _nodeNameMapping.Count;
                        nodeNames.Add(feature.SourceTag);
                        trainingData.Add(new TrainingDataList<SimpleOutcomeFeature>(SimpleOutcomeFeature.CreateStatic));
                    }

                    trainingData[_nodeNameMapping[one]].Append(new SimpleOutcomeFeature(feature.FeatureName, feature.DestTag));
                }

                domainConfig.Set("nodenames_" + intent, nodeNames);

                // And train the tagger for each
                classifiers = new IStatisticalClassifier[_nodeNameMapping.Count];
                foreach (Compact<string> classifierTag in _nodeNameMapping.Keys)
                {
                    string thisClassifierTag = _stringIndex.Value.Retrieve(classifierTag);
                    int nodeIndex = _nodeNameMapping[classifierTag];
                    classifiers[nodeIndex] = _modelTrainer.TrainClassifier(new FeatureReader(trainingData[nodeIndex]), cacheFileRoot + " " + thisClassifierTag, _stringIndex.Value, thisClassifierTag, MODEL_QUALITY);
                }
            }
        }

        /// <summary>
        /// shim class until I can plumb TrainingEvents throughout the whole training process
        /// </summary>
        private class FeatureReader : ITrainingEventReader
        {
            private TrainingDataList<SimpleOutcomeFeature> data;
            private int index = 0;

            public FeatureReader(TrainingDataList<SimpleOutcomeFeature> trainingData)
            {
                data = trainingData;
            }

            public TrainingEvent ReadNextEvent()
            {
                SimpleOutcomeFeature feature = data.TrainingData[index++];
                string[] context = { feature.FeatureName };
                return new TrainingEvent(feature.Outcome, context);
            }

            public bool HasNext()
            {
                return index < data.TrainingData.Count - 1;
            }
        }

        public long GetMemoryUse()
        {
            long returnVal = 0;
            foreach (IStatisticalClassifier e in classifiers)
            {
                returnVal += e.GetMemoryUse();
            }
            return returnVal;
        }

        // TODO: This should use querylogger
        public IList<TaggedData> Classify(Sentence utterance, ITagFeatureExtractor featureExtractor, bool wasSpeechQuery)
        {
            IList<TaggedData> returnVal = new List<TaggedData>();

            // Don't break on empty input
            if (utterance.Words.Count == 0)
            {
                TaggedData finalTag = new TaggedData()
                    {
                        Utterance = utterance.OriginalText,
                        Annotations = new Dictionary<string, string>(),
                        Slots = new List<SlotValue>(),
                        Confidence = 1.0f,
                    };
                returnVal.Add(finalTag);
                return returnVal;
            }

            HypothesisNode[] lattice = GenerateTaggerLattice(utterance, featureExtractor);
            IList<HypothesisNode[]> traversals = new List<HypothesisNode[]>();
            foreach (HypothesisNode node in lattice)
            {
                if (node != null)
                {
                    traversals.Add(BuildHypothesisHistoryFromBackpointers(node));
                }
            }

            // Find the highest-confidence tag hyp
            float highestConf = 0.0f;
            foreach (HypothesisNode[] traversal in traversals)
            {
                float confidence = traversal[traversal.Length - 1].TotalScore / utterance.Words.Count;
                highestConf = Math.Max(confidence, highestConf);
            }

            foreach (HypothesisNode[] traversal in traversals)
            {
                float confidence = traversal[traversal.Length - 1].TotalScore / utterance.Words.Count;
                if (returnVal.Count == 0 || confidence > highestConf * _confidenceCutoff)
                {
                    TaggedData finalTag = ConstructTaggedDataFromLatticeHypothesis(utterance, traversal, wasSpeechQuery);
                    finalTag.Confidence = confidence;
                    returnVal.Add(finalTag);
                }
            }

            return returnVal;
        }

        /// <summary>
        /// Constructs a sensical TaggedData object from a list of disparate TaggedWords.
        /// TODO: Write a better handler for isolated tag groups (if two separate phrases have the same tag,
        /// this current code will also wrap everything between them)
        /// </summary>
        /// <param name="utterance"></param>
        /// <param name="hypotheses"></param>
        /// <param name="wasSpeechQuery"></param>
        /// <returns></returns>
        private TaggedData ConstructTaggedDataFromLatticeHypothesis(Sentence utterance, HypothesisNode[] hypotheses, bool wasSpeechQuery)
        {
            TaggedSentence rawTags = new TaggedSentence();
            rawTags.Utterance = utterance;

            for (int c = 0; c < hypotheses.Length; c++)
            {
                HypothesisNode node = hypotheses[c];
                TaggedWord newWord = new TaggedWord();
                newWord.Word = utterance.Words[c];
                newWord.Tags = node.GuessedTags;
                rawTags.Words.Add(newWord);
            }
            
            // Convert tagged words into tagged phrases
            Dictionary<string, Tuple<int, int>> tagPositions = new Dictionary<string, Tuple<int, int>>();

            // Assert that the tagged utterance and wordbroken utterance are identical
            if (utterance.Length != rawTags.Words.Count)
                throw new ArgumentException("Tagged utterance did not match wordbreaker output!");
            for (int wordIndex = 0; wordIndex < utterance.Length; wordIndex++)
            {
                if (!rawTags.Words[wordIndex].Word.Equals(utterance.Words[wordIndex]))
                    throw new ArgumentException("Tagged utterance did not match wordbreaker output!");
            }

            for (int wordIndex = 0; wordIndex < utterance.Length; wordIndex++)
            {
                // Use the original indices provided by wordbreaker to determine the tag bounds
                int startIndex = utterance.Indices[wordIndex];
                int endIndex = utterance.Indices[wordIndex] + utterance.Words[wordIndex].Length;
                foreach (string tag in rawTags.Words[wordIndex].Tags)
                {
                    if (tag.Equals(NOTAG))
                        continue;
                    if (!tagPositions.ContainsKey(tag))
                    {
                        tagPositions[tag] = new Tuple<int, int>(startIndex, endIndex);
                    }
                    else
                    {
                        tagPositions[tag] = new Tuple<int, int>(tagPositions[tag].Item1, endIndex);
                    }
                }
            }

            TaggedData returnVal = new TaggedData()
                {
                    Utterance = utterance.OriginalText,
                    Confidence = 1.0f
                };

            // Perform lexical alignment to provide the lexical form for all slot values
            // This final data structure will map tag names -> lexical strings
            IDictionary<string, string> tagLexicalForms = new Dictionary<string, string>();

            if (_wordBreaker != null &&
                !string.IsNullOrEmpty(utterance.LexicalForm) &&
                !utterance.OriginalText.Equals(utterance.LexicalForm, StringComparison.OrdinalIgnoreCase))
            {
                _logger.Log("Preparing lexical alignment data...", LogLevel.Vrb);
                Sentence lexicalSentence = _wordBreaker.Break(utterance.LexicalForm);
                AlignmentStep[] alignmentData = LexicalAlignment.Align(utterance.Words, lexicalSentence.Words);

                int displayWordIdx = 0;
                int lexicalWordIdx = 0;
                int fragmentStartIdx = 0;
                string lexicalFragment = string.Empty;

                foreach (AlignmentStep step in alignmentData)
                {
                    if (step == AlignmentStep.Match || step == AlignmentStep.Edit)
                    {
                        if (!string.IsNullOrEmpty(lexicalFragment))
                        {
                            // We have an aligned fragment. Find the tag it belongs to
                            foreach (string tagName in tagPositions.Keys)
                            {
                                // Check each tag and see whether this fragment falls within its bounds
                                if (fragmentStartIdx >= tagPositions[tagName].Item1 &&
                                    fragmentStartIdx < tagPositions[tagName].Item2)
                                {
                                    if (!tagLexicalForms.ContainsKey(tagName))
                                    {
                                        tagLexicalForms[tagName] = lexicalFragment;
                                    }
                                    else
                                    {
                                        // TODO: The lexical form always assumes space-delimited words. Does that hold for all languages?
                                        tagLexicalForms[tagName] += " " + lexicalFragment;
                                    }
                                }
                            }
                        }
                        
                        if (displayWordIdx < utterance.Words.Count)
                        {
                            fragmentStartIdx = utterance.Indices[displayWordIdx++];
                        }
                        else
                        {
                            fragmentStartIdx = utterance.Indices[displayWordIdx - 1];
                            _logger.Log("Lexical fragment start index exceeded the bounds of the input. I think I need to throw an exception here?", LogLevel.Wrn);
                            // throw new IndexOutOfRangeException("Lexical fragment cannot extend beyond the edge of the display-form utterance");
                        }

                        if (lexicalWordIdx < lexicalSentence.Words.Count)
                        {
                            lexicalFragment = lexicalSentence.Words[lexicalWordIdx++];
                        }
                        else
                        {
                            lexicalFragment = string.Empty;
                        }
                    }
                    else if (step == AlignmentStep.Add)
                    {
                        displayWordIdx++;
                    }
                    else if (step == AlignmentStep.Skip)
                    {
                        lexicalFragment += " " + lexicalSentence.Words[lexicalWordIdx++];
                    }
                }
                if (!string.IsNullOrEmpty(lexicalFragment))
                {
                    // It's possible there's an orphaned fragment. Add it to the tag list like the others.
                    foreach (string tagName in tagPositions.Keys)
                    {
                        // Check each tag and see whether this fragment falls within its bounds
                        if (fragmentStartIdx >= tagPositions[tagName].Item1 &&
                            fragmentStartIdx < tagPositions[tagName].Item2)
                        {
                            if (!tagLexicalForms.ContainsKey(tagName))
                            {
                                tagLexicalForms[tagName] = lexicalFragment;
                            }
                            else
                            {
                                tagLexicalForms[tagName] += " " + lexicalFragment;
                            }
                        }
                    }
                }

                _logger.Log("Lexical alignment finished", LogLevel.Vrb);
            }

            foreach (string tagName in tagPositions.Keys)
            {
                int startIndex = tagPositions[tagName].Item1;
                int length = tagPositions[tagName].Item2 - tagPositions[tagName].Item1;
                string tagValue = utterance.OriginalText.Substring(startIndex, length);

                SlotValue newTag = new SlotValue(tagName,
                    tagValue,
                    wasSpeechQuery ? SlotValueFormat.SpokenText : SlotValueFormat.TypedText);
                newTag.SetProperty(SlotPropertyName.StartIndex, startIndex.ToString());
                newTag.SetProperty(SlotPropertyName.StringLength, length.ToString());
                newTag.Alternates = new List<string>();
                newTag.LexicalForm = tagLexicalForms.ContainsKey(tagName) ? tagLexicalForms[tagName] : tagValue;
                returnVal.Slots.Add(newTag);
            }

            return returnVal;
        }

        private HypothesisNode[] GenerateTaggerLattice(Sentence utterance, ITagFeatureExtractor featureExtractor)
        {
            HypothesisComparer sorter = new HypothesisComparer();
            
            HypothesisNode rootNode = new HypothesisNode();
            rootNode.ThisWordIndex = -1;
            rootNode.ThisClassifierNode = _stringIndex.Value.GetIndex(STKN);
            rootNode.TotalScore = 0;
            rootNode.ThisScore = 0;
            rootNode.TagHistory = new string[0][];

            HypothesisNode[] lattice = new HypothesisNode[LATTICE_HEIGHT];
            lattice[0] = rootNode;

            for (int word = 0; word < utterance.Length; word++)
            {
                // Expand the current set of hypotheses
                List<HypothesisNode> newNodes = new List<HypothesisNode>();
                for (int hyp = 0; hyp < LATTICE_HEIGHT; hyp++)
                {
                    if (lattice[hyp] != null)
                        ClassifyOne(lattice[hyp], utterance, featureExtractor, ref newNodes);
                }

                // Sort the list of results
                newNodes.Sort(sorter);

                // And use those to iterate the lattice
                for (int c = 0; c < LATTICE_HEIGHT && c < newNodes.Count; c++)
                {
                    lattice[c] = newNodes[c];
                }
            }

            return lattice;
        }

        private void ClassifyOne(HypothesisNode currentNode, Sentence utterance, ITagFeatureExtractor extractor, ref List<HypothesisNode> output)
        {
            // We have reached the end token. Just append a "O" tag and return.
            if (currentNode.ThisClassifierNode == _stringIndex.Value.GetIndex(ETKN) ||
                currentNode.ThisClassifierNode == _stringIndex.Value.GetNullIndex())
            {
                float conf = 0.5f;// TODO: What confidence to use here?
                HypothesisNode newHyp = new HypothesisNode();
                newHyp.ThisWordIndex = currentNode.ThisWordIndex + 1;
                newHyp.ThisScore = conf;
                // next.ThisWord = next.ThisWordIndex < utterance.Words.Count ? utterance.Words[next.ThisWordIndex] : null; 
                newHyp.TotalScore = currentNode.TotalScore + conf;
                newHyp.ThisClassifierNode = _stringIndex.Value.GetIndex(ETKN);
                newHyp.GuessedTags.Add(NOTAG);
                newHyp.Backpointer = currentNode;
                output.Add(newHyp);
            }
            else
            {
                int wordIndex = currentNode.ThisWordIndex;
                if (wordIndex < 0)
                    wordIndex = 0;

                string[] features = extractor.ExtractFeatures(utterance, currentNode.TagHistory, wordIndex);
                IList<Hypothesis<string>> posResults;
                float topResultConfidence = 0;
                if (!_nodeNameMapping.ContainsKey(currentNode.ThisClassifierNode))
                {
                    posResults = new List<Hypothesis<string>>();
                    _logger.Log("The CRF model attempted to make an invalid transition! This usually means the model was retrained without being completely reinitialized. Flush your cache and try again", LogLevel.Wrn);
                }
                else
                {
                    posResults = classifiers[_nodeNameMapping[currentNode.ThisClassifierNode]].ClassifyAll(features);
                    if (posResults.Count > 0)
                    {
                        topResultConfidence = posResults[0].Conf;
                    }
                }
                
                foreach (Hypothesis<string> posResult in posResults)
                {
                    if (posResult.Conf < topResultConfidence * BRANCH_THRESHOLD)
                        continue;

                    HypothesisNode newHyp = new HypothesisNode();
                    newHyp.ThisWordIndex = currentNode.ThisWordIndex + 1;
                    newHyp.ThisScore = posResult.Conf;
                    // next.ThisWord = next.ThisWordIndex < utterance.Words.Count ? utterance.Words[next.ThisWordIndex] : null; 
                    newHyp.TotalScore = currentNode.TotalScore + posResult.Conf;
                    newHyp.ThisClassifierNode = _stringIndex.Value.GetIndex(posResult.Value);
                    newHyp.Backpointer = currentNode;

                    if (currentNode.ThisWordIndex + 1 < utterance.Length)
                    {
                        // Is "O" (no tag) the top result? If so, skip all tagging for this word
                        if (!posResult.Value.Equals(NOTAG) &&
                            !posResult.Value.Equals(ETKN))
                        {
                            // Passed the tag threshold, add it to the current tag set
                            string[] allTags = posResult.Value.Split('+');
                            
                            foreach (string tagToAdd in allTags)
                            {
                                newHyp.GuessedTags.Add(tagToAdd);
                            }
                        }
                        else
                            newHyp.GuessedTags.Add(NOTAG);
                    }

                    newHyp.TagHistory = AppendTagHistory(currentNode.TagHistory, newHyp);

                    // Add a new hypothesis to the list
                    output.Add(newHyp);
                }
            }
        }

        private class HypothesisComparer : IComparer<HypothesisNode>
        {
            public int Compare(HypothesisNode x, HypothesisNode y)
            {
                return Math.Sign(y.TotalScore - x.TotalScore);
            }
        }

        private class HypothesisNode
        {
            public List<string> GuessedTags = new List<string>();
            public int ThisWordIndex;
            public Compact<string> ThisClassifierNode;
            public float TotalScore;
            public float ThisScore;
            public HypothesisNode Backpointer;
            public string[][] TagHistory;
        }

        private static HypothesisNode[] BuildHypothesisHistoryFromBackpointers(HypothesisNode tail)
        {
            HypothesisNode[] returnVal = new HypothesisNode[tail.ThisWordIndex + 1];
            HypothesisNode cur = tail;
            for (int c = tail.ThisWordIndex; c >= 0; c--)
            {
                returnVal[c] = cur;
                // Assert (cur.Backpointer != null)
                cur = cur.Backpointer;
            }
            return returnVal;
        }
        
        private static string[][] AppendTagHistory(string[][] tagHistory, HypothesisNode node)
        {
            int count = node.ThisWordIndex + 1;
            string[][] returnVal = new string[count][];
            if (count > 1)
            {
                for (int c = 0; c < count - 1; c++)
                {
                    returnVal[c] = tagHistory[c];
                }
            }
            returnVal[count - 1] = node.GuessedTags.ToArray();
            return returnVal;
        }
    }
}
