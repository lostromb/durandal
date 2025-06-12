using Durandal.API;
using Durandal.Common.NLP.ApproxString;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Services;
using Durandal.CommonViews;
using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Durandal.Common.IO;
using Durandal.Common.NLP.Language.English;
using Durandal.Common.File;
using Durandal.Common.MathExt;
using Durandal.Common.Statistics;
using Durandal.Common.NLP.Language;

namespace Durandal.Plugins.SideSpeech
{
    public class ChitChatEngine
    {
        private readonly LanguageCode _locale;
        private readonly ApproxStringMatchingIndex _intentMatcher;
        private readonly IDictionary<Regex, string> _regexIntents;
        private readonly IDictionary<string, string> _intents = new Dictionary<string, string>();
        private readonly IDictionary<string, IList<ChitChatResponse>> _responses = new Dictionary<string, IList<ChitChatResponse>>();
        private readonly IList<ChitChatConversation> _conversations = new List<ChitChatConversation>();
        private readonly IDictionary<string, ChitChatNode> _nodes = new Dictionary<string, ChitChatNode>();
        private readonly ICustomCodeProvider _codeProvider;
        private readonly IRandom _rand;

        public ChitChatEngine(LanguageCode locale, ILogger logger, IRandom rand = null, CustomResponseGenerator customResponseProvider = null)
        {
            _intentMatcher = new ApproxStringMatchingIndex(new EnglishWholeWordApproxStringFeatureExtractor(), locale, logger);
            _regexIntents = new Dictionary<Regex, string>();
            _locale = locale;
            _codeProvider = customResponseProvider;
            _rand = rand ?? new FastRandom();
        }

        public bool Initialize(IEnumerable<Stream> configurationStreams, ILogger logger)
        {
            try
            {
                foreach (Stream fileStream in configurationStreams)
                {
                    string currentRegion = null;
                    IList<string> linesInCurrentRegion = new List<string>();

                    using (StreamReader reader = new StreamReader(fileStream))
                    {
                        while (!reader.EndOfStream)
                        {
                            string nextLine = reader.ReadLine();
                            if (nextLine == null)
                            {
                                continue;
                            }

                            nextLine = nextLine.Trim();

                            if (string.IsNullOrEmpty(nextLine) || nextLine.StartsWith("#"))
                            {
                                continue;
                            }

                            if (nextLine.StartsWith("["))
                            {
                                ParseRegion(currentRegion, linesInCurrentRegion, logger);
                                currentRegion = nextLine.Trim('[', ']');
                                linesInCurrentRegion.Clear();
                            }
                            else
                            {
                                linesInCurrentRegion.Add(nextLine);
                            }
                        }

                        ParseRegion(currentRegion, linesInCurrentRegion, logger);
                    }
                }

                return ValidateConversations(logger);
            }
            catch (Exception e)
            {
                logger.Log("Exception while creating chit-chat engine: " + e.Message);
                return false;
            }
        }

        private bool ValidateConversations(ILogger logger)
        {
            bool valid = true;

            logger.Log("Validating chitchat configuration...");

            ISet<string> allIntents = new HashSet<string>();
            
            foreach (string intent in _intents.Values)
            {
                if (!allIntents.Contains(intent))
                {
                    allIntents.Add(intent);
                }
            }

            foreach (string intent in _regexIntents.Values)
            {
                if (!allIntents.Contains(intent))
                {
                    allIntents.Add(intent);
                }
            }

            ISet<string> unusedIntents = new HashSet<string>(allIntents);
            ISet<string> unusedResponses = new HashSet<string>(_responses.Keys);
            ISet<string> conversationNames = new HashSet<string>();

            // Iterate through all conversations and make sure that the intents, responses, and nodes are all consistent
            foreach (ChitChatConversation convo in _conversations)
            {
                if (conversationNames.Contains(convo.Name))
                {
                    logger.Log("There are multiple conversations named \"" + convo.Name + "\"", LogLevel.Wrn);
                    valid = false;
                }
                else
                {
                    conversationNames.Add(convo.Name);
                }

                foreach (ChitChatTransition t in convo.Transitions)
                {
                    if (!allIntents.Contains(t.Intent))
                    {
                        logger.Log("Conversation \"" + convo.Name + "\" references an unknown intent \"" + t.Intent + "\"", LogLevel.Err);
                        valid = false;
                    }
                    else if (unusedIntents.Contains(t.Intent))
                    {
                        unusedIntents.Remove(t.Intent);
                    }

                    if (!string.Equals("START", t.StartNode, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!_nodes.ContainsKey(t.StartNode))
                        {
                            logger.Log("Conversation \"" + convo.Name + "\" has a transition from an unknown node \"" + t.StartNode + "\"", LogLevel.Err);
                            valid = false;
                        }
                        else
                        {
                            ChitChatNode node = _nodes[t.StartNode];
                            if (!_responses.ContainsKey(node.ResponseName))
                            {
                                logger.Log("Conversation \"" + convo.Name + "\" (node \"" + node.Name + "\") references an unknown response \"" + node.ResponseName + "\"", LogLevel.Err);
                                valid = false;
                            }
                            else if (unusedResponses.Contains(node.ResponseName))
                            {
                                unusedResponses.Remove(node.ResponseName);
                            }
                        }
                    }

                    if (!_nodes.ContainsKey(t.TargetNode))
                    {
                        logger.Log("Conversation \"" + convo.Name + "\" has a transition to an unknown node \"" + t.StartNode + "\"", LogLevel.Err);
                        valid = false;
                    }
                    else
                    {
                        ChitChatNode node = _nodes[t.TargetNode];
                        if (!_responses.ContainsKey(node.ResponseName))
                        {
                            logger.Log("Conversation \"" + convo.Name + "\" (node \"" + node.Name + "\") references an unknown response \"" + node.ResponseName + "\"", LogLevel.Err);
                            valid = false;
                        }
                        else if (unusedResponses.Contains(node.ResponseName))
                        {
                            unusedResponses.Remove(node.ResponseName);
                        }
                    }
                }
            }

            // Issue a warning for unused parts
            foreach (string unusedIntent in unusedIntents)
            {
                logger.Log("Chitchat config contains an unreferenced intent \"" + unusedIntent + "\"", LogLevel.Wrn);
            }
            foreach (string unusedResponse in unusedResponses)
            {
                logger.Log("Chitchat config contains an unreferenced response \"" + unusedResponse + "\"", LogLevel.Wrn);
            }

            return valid;
        }

        public async Task CreateTrainingFile(string domain, string intent, VirtualPath targetTrainingFile, IFileSystem resourceManager, ILogger logger)
        {
            if (resourceManager.Exists(targetTrainingFile))
            {
                resourceManager.Delete(targetTrainingFile);
            }

            using (StreamWriter writeStream = new StreamWriter(await resourceManager.OpenStreamAsync(targetTrainingFile, FileOpenMode.Create, FileAccessMode.Write)))
            {
                logger.Log("Writing chitchat training data to " + targetTrainingFile);
                foreach (string utterance in _intents.Keys)
                {
                    await writeStream.WriteLineAsync(string.Format("{0}/{1}\t{2}", domain, intent, utterance));
                }

                writeStream.Close();
            }
        }

        private void ParseRegion(string tag, IList<string> lines, ILogger logger)
        {
            if (string.IsNullOrEmpty(tag) || lines == null || lines.Count == 0)
            {
                return;
            }

            // See what type of tag it is, then parse it accordingly
            if (tag.StartsWith("conversation/", StringComparison.OrdinalIgnoreCase))
            {
                ChitChatConversation convo = ParseConversation(tag.Substring(tag.IndexOf('/') + 1), lines);
                _conversations.Add(convo);
            }
            else if (tag.StartsWith("simple_response/", StringComparison.OrdinalIgnoreCase))
            {
                Tuple<ChitChatConversation, ChitChatNode, ChitChatResponse> simpleResponses = ParseSimpleResponse(tag.Substring(tag.IndexOf('/') + 1), lines);
                if (simpleResponses == null)
                {
                    logger.Log("Could not parse simple response \"" + tag + "\"", LogLevel.Wrn);
                }
                else
                {
                    _conversations.Add(simpleResponses.Item1);
                    _nodes.Add(simpleResponses.Item2.Name, simpleResponses.Item2);

                    if (simpleResponses.Item3 != null)
                    {
                        List<ChitChatResponse> rList = new List<ChitChatResponse>();
                        rList.Add(simpleResponses.Item3);
                        _responses.Add(simpleResponses.Item3.Name, rList);
                    }
                }
            }
            else if (tag.StartsWith("intent/", StringComparison.OrdinalIgnoreCase))
            {
                ChitChatIntent intent = ParseIntent(tag.Substring(tag.IndexOf('/') + 1), lines);
                foreach (string utterance in intent.Utterances)
                {
                    _intentMatcher.Index(new LexicalString(utterance));
                }

                foreach (var trigger in intent.Utterances)
                {
                    if (_intents.ContainsKey(trigger))
                    {
                        logger.Log("There are two chitchat intents which share the utterance \"" + trigger + "\": " + _intents[trigger] + " and " + intent.Name, LogLevel.Wrn);
                    }
                    else
                    {
                        _intents[trigger] = intent.Name;
                    }
                }
                foreach (var regex in intent.Regexes)
                {
                    Regex r = new Regex(regex, RegexOptions.Compiled);
                    if (_regexIntents.ContainsKey(r))
                    {
                        logger.Log("There are two chitchat intents which share the regex \"" + regex + "\"", LogLevel.Wrn);
                    }
                    else
                    {
                        _regexIntents[r] = intent.Name;
                    }
                }
            }
            else if (tag.StartsWith("node/", StringComparison.OrdinalIgnoreCase))
            {
                ChitChatNode node = ParseNode(tag.Substring(tag.IndexOf('/') + 1), lines);
                _nodes.Add(node.Name, node);
            }
            else if (tag.StartsWith("response/", StringComparison.OrdinalIgnoreCase))
            {
                ChitChatResponse response = ParseResponse(tag.Substring(tag.IndexOf('/') + 1), lines);
                if (!_responses.ContainsKey(response.Name))
                {
                    _responses[response.Name] = new List<ChitChatResponse>();
                }

                _responses[response.Name].Add(response);
            }
            else
            {
                logger.Log("Unknown data group found in chitchat config: \"" + tag + "\"", LogLevel.Wrn);
            }
        }

        private ChitChatConversation ParseConversation(string name, IList<string> lines)
        {
            ChitChatConversation returnVal = new ChitChatConversation();
            returnVal.Name = name;
            returnVal.Transitions = new List<ChitChatTransition>();
            foreach (string line in lines)
            {
                string[] parts = line.Split('\t');
                if (parts.Length == 3)
                {
                    returnVal.Transitions.Add(new ChitChatTransition(parts[0], parts[1], parts[2]));
                }
            }
            return returnVal;
        }

        private ChitChatIntent ParseIntent(string name, IList<string> lines)
        {
            ChitChatIntent returnVal = new ChitChatIntent();
            returnVal.Name = name;
            returnVal.Utterances = new List<string>();
            returnVal.Regexes = new List<string>();
            foreach (string line in lines)
            {
                if (line.StartsWith("regex:", StringComparison.OrdinalIgnoreCase))
                    returnVal.Regexes.Add(line.Substring(6));
                else
                    returnVal.Utterances.Add(line.ToLowerInvariant());
            }
            return returnVal;
        }

        private ChitChatNode ParseNode(string name, IList<string> lines)
        {
            ChitChatNode returnVal = new ChitChatNode();
            returnVal.Name = name;
            foreach (string line in lines)
            {
                int idx = line.IndexOf("=");
                if (idx > 0)
                {
                    string key = line.Substring(0, idx);
                    string value = line.Substring(idx + 1);

                    if (key.Equals("Response", StringComparison.OrdinalIgnoreCase))
                    {
                        returnVal.ResponseName = value;
                    }
                }
            }
            return returnVal;
        }

        private Tuple<ChitChatConversation, ChitChatNode, ChitChatResponse> ParseSimpleResponse(string name, IList<string> lines)
        {
            string intent = null;
            string responseName = null;
            foreach (string line in lines)
            {
                int idx = line.IndexOf("=");
                if (idx > 0)
                {
                    string key = line.Substring(0, idx);
                    string value = line.Substring(idx + 1);

                    if (key.Equals("Intent", StringComparison.OrdinalIgnoreCase))
                    {
                        intent = value;
                    }
                    else if (key.Equals("Response", StringComparison.OrdinalIgnoreCase))
                    {
                        responseName = value;
                    }
                }
            }

            if (string.IsNullOrEmpty(intent))
            {
                return null;
            }

            ChitChatResponse response = null;
            if (string.IsNullOrEmpty(responseName))
            {
                responseName = Guid.NewGuid().ToString("N");
                response = ParseResponse(responseName, lines);
            }

            ChitChatNode node = new ChitChatNode();
            node.Name = Guid.NewGuid().ToString("N");
            node.ResponseName = responseName;

            ChitChatConversation conversation = new ChitChatConversation();
            conversation.Name = name;
            conversation.Transitions = new List<ChitChatTransition>();
            conversation.Transitions.Add(new ChitChatTransition("START", intent, node.Name));
            
            return new Tuple<ChitChatConversation, ChitChatNode, ChitChatResponse>(conversation, node, response);
        }

        private ChitChatResponse ParseResponse(string name, IList<string> lines)
        {
            ChitChatResponse returnVal = new ChitChatResponse();
            returnVal.Name = name;
            foreach (string line in lines)
            {
                int idx = line.IndexOf("=");
                if (idx > 0)
                {
                    string key = line.Substring(0, idx);
                    string value = line.Substring(idx + 1);

                    if (key.Equals("Text", StringComparison.OrdinalIgnoreCase))
                    {
                        returnVal.Text = value;
                    }
                    else if (key.Equals("CustomHandler", StringComparison.OrdinalIgnoreCase))
                    {
                        returnVal.CustomCodeFunction = value;
                    }
                    else if (key.Equals("Image", StringComparison.OrdinalIgnoreCase))
                    {
                        returnVal.Image = value;
                    }
                }
            }

            return returnVal;
        }

        public async Task<PluginResult> AttemptChat(QueryWithContext queryWithContext, IPluginServices services)
        {
            LexicalString input = new LexicalString(queryWithContext.Understanding.Utterance.OriginalText);

            // chat_phrase_id is a monontonously increasing integer value which is initialized to a random number between 0 and 1000 and kept in the session store.
            // The idea is that, if the user triggers the same chitchat intent multiple times in a row, a different response will be selected each time
            // because the phrase id will always increment by 1 each turn.
            int phraseId;
            if (services.SessionStore.ContainsKey("chat_phrase_id"))
            {
                phraseId = services.SessionStore.GetInt("chat_phrase_id") + 1;
            }
            else
            {
                phraseId = _rand.NextInt(0, 1000);
            }

            // Try regexes first
            string intent = null;
            int longestMatchLength = 0;
            foreach (var kvp in _regexIntents)
            {
                Match m = kvp.Key.Match(input.WrittenForm);
                if (m.Success && m.Length > longestMatchLength)
                {
                    intent = kvp.Value;
                    longestMatchLength = m.Length; // this is done to ensure that if multiple regexes match, the longest capture group will take precedence
                    services.Logger.Log("Chit-chat matched regex pseudo-intent \"" + kvp.Value + "\"");
                }
            }

            if (intent == null)
            {
                IList<Hypothesis<LexicalString>> matchList = _intentMatcher.Match(input, 1);
                if (matchList == null || matchList.Count == 0)
                    return null;

                Hypothesis<LexicalString> topMatch = matchList[0];

                if (topMatch.Conf < 0.89f)
                    return null;

                if (!_intents.ContainsKey(topMatch.Value.WrittenForm))
                    return null;

                intent = _intents[topMatch.Value.WrittenForm];
                services.Logger.Log("Chit-chat matched pseudo-intent " + intent + " with confidence " + topMatch.Conf);
            }

            string currentNode;
            if (!services.SessionStore.TryGetString("convo_node", out currentNode))
            {
                currentNode = null;
            }

            ChitChatNode multiturnNode = null;
            ChitChatNode conversationStartingNode = null;

            foreach (ChitChatConversation convo in _conversations)
            {
                foreach (ChitChatTransition possibleTransition in convo.Transitions)
                {
                    if (possibleTransition.StartNode.Equals("START") && possibleTransition.Intent.Equals(intent))
                    {
                        string nodeName = possibleTransition.TargetNode;
                        if (!_nodes.TryGetValue(nodeName, out conversationStartingNode))
                        {
                            return ReturnError("Attempted to transition to nonexistent conversation node \"" + nodeName + "\"!", services.Logger);
                        }
                    }

                    if (currentNode != null && possibleTransition.StartNode.Equals(currentNode) && possibleTransition.Intent.Equals(intent))
                    {
                        string nodeName = possibleTransition.TargetNode;
                        if (!_nodes.TryGetValue(nodeName, out multiturnNode))
                        {
                            return ReturnError("Attempted to transition to nonexistent conversation node \"" + nodeName + "\"!", services.Logger);
                        }
                    }

                    //if (multiturnNode != null)
                    //    break;
                }

                //if (multiturnNode != null)
                //    break;
            }

            if (multiturnNode == null && conversationStartingNode == null)
            {
                services.Logger.Log("No conversation starting node matched for pseudo-intent " + intent, LogLevel.Wrn);
                return null;
            }

            ChitChatNode nextNode = multiturnNode ?? conversationStartingNode;

            string responseName = nextNode.ResponseName;

            if (!_responses.ContainsKey(responseName))
            {
                return ReturnError("There is no chitchat response named \"" + responseName + "\"!", services.Logger);
            }
            
            services.SessionStore.Put("convo_node", nextNode.Name);
            services.SessionStore.Put("chat_phrase_id", phraseId);

            IList<ChitChatResponse> possibleResponses = _responses[responseName];
            ChitChatResponse responseToUse = possibleResponses[phraseId % possibleResponses.Count];

            if (!string.IsNullOrEmpty(responseToUse.CustomCodeFunction))
            {
                if (_codeProvider == null)
                {
                    return ReturnError("Custom code invoked, but there is no code provider in the chit-chat engine!", services.Logger);
                }

                PluginContinuation responseMethod = _codeProvider.GetFunction(responseToUse.CustomCodeFunction);
                if (responseMethod == null)
                {
                    return ReturnError("The custom code function \"" + responseToUse.CustomCodeFunction + "\" does not exist!", services.Logger);
                }

                PluginResult result = await responseMethod(queryWithContext, services);
                result.MultiTurnResult = MultiTurnBehavior.ContinuePassively;
                return result;
            }
            else
            {
                MessageView html = new MessageView()
                {
                    UseHtml5 = true,
                    Content = responseToUse.Text,
                    ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                };

                if (!string.IsNullOrEmpty(responseToUse.Image))
                {
                    html.Image = responseToUse.Image;
                }

                return new PluginResult(Result.Success)
                {
                    ResponseSsml = responseToUse.Text,
                    ResponseText = responseToUse.Text,
                    ResponseHtml = html.Render(),
                    MultiTurnResult = MultiTurnBehavior.ContinuePassively
                };
            }
        }

        private static PluginResult ReturnError(string message, ILogger logger)
        {
            logger.Log(message, LogLevel.Err);
            return new PluginResult(Result.Failure)
            {
                ErrorMessage = message
            };
        }
    }
}
