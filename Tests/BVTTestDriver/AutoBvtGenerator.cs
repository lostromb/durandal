using Durandal.API;


namespace BVTTestDriver
{
    using Durandal.Common.NLP.Train;
    using Durandal.Common.Logger;
    using Durandal.Common.File;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Durandal.Common.NLP.Tagging;
    using Durandal.Common.MathExt;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Runtime;
    using Durandal;
    using Durandal.Common.NLP.Language;

    public class AutoBvtGenerator
    {
        private readonly ILogger _logger;
        private readonly IList<TrainingDataTemplate> _allTrainingData;
        private readonly string _domain;
        private readonly ConversationTree _conversationTree;
        private IDictionary<string, List<TrainingUtterance>> _expandedTraining;
        private readonly IRandom _random;

        public AutoBvtGenerator(ILogger logger, DurandalPlugin answerPlugin, IList<TrainingDataTemplate> trainingTemplates)
        {
            _allTrainingData = trainingTemplates;
            _logger = logger;
            _domain = answerPlugin.LUDomain;
            _conversationTree = answerPlugin.GetConversationTreeSingleton(NullFileSystem.Singleton, VirtualPath.Root) as ConversationTree;

            // Use deterministic random so that test results don't vary too much
            _random = new FastRandom(_domain.GetHashCode());
            _expandedTraining = new Dictionary<string, List<TrainingUtterance>>();
            ExpandAllTrainingFiles();
        }

        public IList<IList<TestUtterance>> GenerateConversations(int desiredCount)
        {
            IList<IList<TestUtterance>> returnVal = new List<IList<TestUtterance>>();
            for (int c = 0; c < desiredCount; c++)
            {
                IList<TestUtterance> convo = GenerateConversation();
                if (convo.Count != 0)
                    returnVal.Add(convo);
            }
            return returnVal;
        }

        private IList<TestUtterance> GenerateConversation()
        {
            IList<TestUtterance> returnVal = new List<TestUtterance>();
            if (_conversationTree == null)
            {
                // No tree; just use a random training utterance
                List<string> allIntents = new List<string>(_expandedTraining.Keys);
                if (allIntents.Count == 0)
                {
                    return returnVal;
                }
                string targetIntent = SelectRandomFromList(allIntents);

                TrainingUtterance nextUtterance = SelectRandomFromList(_expandedTraining[targetIntent]);
                returnVal.Add(new TestUtterance()
                    {
                        Id = 0,
                        ExpectedDomain = _domain,
                        ExpectedIntent = targetIntent,
                        Input = TaggedDataSplitter.StripTags(nextUtterance.Utterance),
                        TaggedInput = nextUtterance.Utterance
                    });

                return returnVal;
            }

            ConversationNode curNode = _conversationTree.GetRootNode();
            int id = 0;
            while (true)
            {
                List<ConversationNodeEdge> edges = new List<ConversationNodeEdge>(curNode.Edges);
                if (edges.Count == 0)
                {
                    // Dead end
                    return returnVal;
                }
                ConversationNodeEdge nextEdge = SelectRandomFromList(edges);
                if (nextEdge.Scope == DomainScope.Local)
                {
                    // Try a local domain edge
                    string intent = nextEdge.Intent;
                    if (!_expandedTraining.ContainsKey(intent))
                    {
                        return returnVal;
                    }

                    TrainingUtterance nextUtterance = SelectRandomFromList(_expandedTraining[intent]);
                    returnVal.Add(new TestUtterance()
                        {
                            Id = id++,
                            ExpectedDomain = _domain,
                            ExpectedIntent = intent,
                            Input = TaggedDataSplitter.StripTags(nextUtterance.Utterance),
                            TaggedInput = nextUtterance.Utterance
                        });

                    // And traverse the tree
                    curNode = nextEdge.TargetNode as ConversationNode;
                }
                else if (nextEdge.Scope == DomainScope.Common)
                {
                    // TODO: Try a common domain edge
                    return returnVal;
                }
                else if (nextEdge.Scope == DomainScope.External)
                {
                    string intent = nextEdge.Intent;
                    if (!_expandedTraining.ContainsKey(intent))
                    {
                        return returnVal;
                    }
                    TrainingUtterance nextUtterance = SelectRandomFromList(_expandedTraining[intent]);
                    returnVal.Add(new TestUtterance()
                        {
                            Id = id++,
                            ExpectedDomain = _domain,
                            ExpectedIntent = intent,
                            Input = TaggedDataSplitter.StripTags(nextUtterance.Utterance),
                            TaggedInput = nextUtterance.Utterance
                        });

                    // And end the conversation
                    return returnVal;
                }

                // Break the conversation at a random time to prevent infinite loops
                if (_random.NextDouble() < 0.05)
                {
                    return returnVal;
                }
            }
        }

        private T SelectRandomFromList<T>(List<T> items)
        {
            return items[_random.NextInt(items.Count)];
        }

        private void ExpandAllTrainingFiles()
        {
            foreach (TrainingDataTemplate t in _allTrainingData)
            {
                ITrainingDataStream generator = new TemplateFileExpanderBalanced(t, _logger, 0.1f, 3);

                // Index the resulting utterances
                int linesWritten = 0;
                while (generator.MoveNext() && linesWritten++ < generator.RecommendedOutputCount)
                {
                    TrainingUtterance utterance = generator.Current;

                    if (!_domain.Equals(utterance.Domain))
                        continue;

                    if (!_expandedTraining.ContainsKey(utterance.Intent))
                    {
                        _expandedTraining[utterance.Intent] = new List<TrainingUtterance>();
                    }
                    _expandedTraining[utterance.Intent].Add(utterance);
                }
            }
        }

        public static IList<TrainingDataTemplate> ParseTrainingFiles(IFileSystem luResourceManager, LanguageCode locale, ILogger logger)
        {
            IList<TrainingDataTemplate> returnVal = new List<TrainingDataTemplate>();

            VirtualPath validationDirectory = new VirtualPath(RuntimeDirectoryName.VALIDATION_DIR + "\\" + locale.ToBcp47Alpha2String() + "\\");
            if (!luResourceManager.Exists(validationDirectory))
            {
                logger.LogFormat(LogLevel.Err, DataPrivacyClassification.SystemMetadata, "Could not find validation data in {0}", validationDirectory.FullName);
                return returnVal;
            }

            foreach (VirtualPath templateFile in luResourceManager.ListFiles(validationDirectory))
            {
                try
                {
                    if (templateFile.Extension.Equals(".template", StringComparison.OrdinalIgnoreCase))
                    {
                        // Inspect the template to determine which domains and intents are covered
                        TrainingDataTemplate template = new TrainingDataTemplate(templateFile, luResourceManager, locale, logger);
                        returnVal.Add(template);
                    }
                }
                catch (FormatException e)
                {
                    logger.LogFormat(LogLevel.Err, DataPrivacyClassification.SystemMetadata, "Format exception while loading validation template {0}", templateFile);
                    logger.Log(e, LogLevel.Err);
                }
            }
            return returnVal;
        }
    }
}
