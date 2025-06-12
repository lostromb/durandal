using System;
using System.Collections.Generic;
using Durandal.API;
using System.Reflection;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Runtime;

namespace Durandal.Common.Dialog
{
    public class ConversationTree : IConversationTree
    {
        private readonly IDictionary<string, ConversationNode> _nodes;

        // The default name of the root node.
        private const string ROOT_NODE_NAME = "root";

        // The conversation domain that this tree is limited to
        private readonly string _localDomain;
        
        public ConversationTree(string localDomainName)
        {
            _nodes = new Dictionary<string, ConversationNode>();
            _localDomain = localDomainName;

            // Create the default root node
            ConversationNode rootNode = new ConversationNode(null, ROOT_NODE_NAME, _localDomain);
            _nodes.Add(ROOT_NODE_NAME, rootNode);
        }

        /// <summary>
        /// This is the factory method for adding nodes to this tree. There is no other way to create nodes than this.
        /// The node's name will be derived from the name of its target method
        /// </summary>
        /// <param name="handler"></param>
        /// <returns></returns>
        public IConversationNode CreateNode(PluginContinuation handler)
        {
            string nodeName;
            if (handler == null)
            {
                // If the handler is null (which can happen for external domain "parking" nodes),
                // use "nullnode0" pattern
                int tryCount = 2;
                nodeName = "NodeNull";
                while (_nodes.ContainsKey(nodeName))
                {
                    nodeName = "NodeNull" + tryCount++;
                }
            }
            else
            {
                // For a target method named "Retry", this will create a method named "RetryNode"
                // If there is a name collision, it will try combinations of "RetryNode2", "RetryNode3", etc.
                // The reason this is important is because conversation trees need to be identical between
                // different invocations of a dialogengine, to allow the conversation state to be represented
                // abstractly and machine-independent
                int tryCount = 2;
                string methodName = "Unknown";
                //try
                {
                    dynamic x = handler;
                    // We have to use the fully qualified name of the function, on the off chance that a domain
                    // uses multiple answer functions whose names may collide but which reside in different namespaces
                    methodName = AbstractDialogExecutor.GetNameOfPluginContinuation(handler);
                }
                //catch (Exception) { }
                nodeName = ("Node." + methodName).ToLowerInvariant();
                while (_nodes.ContainsKey(nodeName))
                {
                    nodeName = ("Node." + methodName + tryCount++).ToLowerInvariant();
                }
            }

            return CreateNode(handler, nodeName);
        }

        /// <summary>
        /// This is the factory method for adding nodes to this tree. There is no other way to create nodes than this.
        /// If a node with the given name already exists, this method will return null.
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="nodeName"></param>
        /// <returns></returns>
        public IConversationNode CreateNode(PluginContinuation handler, string nodeName)
        {
            string actualNodeName = nodeName.ToLowerInvariant();
            if (_nodes.ContainsKey(actualNodeName))
            {
                throw new ArgumentException("A node named " + nodeName + " already exists in the conversation tree for " + this._localDomain);
            }

            ConversationNode returnVal;
            if (handler == null)
            {
                // Allow null handlers for things like parking nodes in external domains or origin nodes to be teleported to
                returnVal = new ConversationNode(null, actualNodeName, _localDomain);
            }
            else
            {
                AbstractDialogExecutor.ValidateContinuationMethod(handler);
                string handlerMethodName = AbstractDialogExecutor.GetNameOfPluginContinuation(handler);
                returnVal = new ConversationNode(handlerMethodName, actualNodeName, _localDomain);
            }

            _nodes.Add(actualNodeName, returnVal);
            return returnVal;
        }

        public ConversationNode GetRootNode()
        {
            return _nodes[ROOT_NODE_NAME];
        }

        public void AddStartState(string intent, IConversationNode target)
        {
            GetRootNode().CreateNormalEdge(intent, target);
        }

        public void AddStartState(string intent, PluginContinuation target)
        {
            GetRootNode().CreateNormalEdge(intent, CreateNode(target));
        }

        public ConversationNode GetNode(string nodeName)
        {
            string actualNodeName = nodeName.ToLowerInvariant();
            if (_nodes.ContainsKey(actualNodeName))
            {
                return _nodes[actualNodeName];
            }

            return null;
        }

        public string GetRetryHandlerName(string sourceNodeName)
        {
            if (!string.IsNullOrEmpty(sourceNodeName) &&
                _nodes.ContainsKey(sourceNodeName))
            {
                return _nodes[sourceNodeName].RetryHandler;
            }

            return null;
        }

        public bool HasStartNode(string domain, string intent)
        {
            return TransitionInternal(domain, intent, GetRootNode().Edges) != null;
        }

        public bool TransitionExists(string curNodeName, string domain, string intent)
        {
            return Transition(curNodeName, domain, intent) != null;
        }

        public string Transition(string curNodeName, string domain, string intent)
        {
            IConversationNodeEdge dummy;
            return Transition(curNodeName, domain, intent, out dummy);
        }

        public string Transition(string curNodeName, string domain, string intent, out IConversationNodeEdge edge)
        {
            if (!string.IsNullOrEmpty(curNodeName))
            {
                curNodeName = curNodeName.ToLowerInvariant();
            }
            
            // If the node has a retry handler, mark it as able to handle noreco results
            if (domain.Equals(DialogConstants.COMMON_DOMAIN) &&
                intent.Equals(DialogConstants.NORECO_INTENT) &&
                !string.IsNullOrEmpty(curNodeName) &&
                _nodes.ContainsKey(curNodeName) &&
                _nodes[curNodeName].RetryHandler != null)
            {
                edge = null;
                return curNodeName;
            }

            ConversationNode node = GetRootNode();
            if (!string.IsNullOrEmpty(curNodeName))
            {
                if (!_nodes.TryGetValue(curNodeName, out node))
                {
                    throw new DialogException("Cannot process dialog tree transition because the node \"" + curNodeName + "\" was not found");
                }
            }

            ConversationNodeEdge transitionEdge = TransitionInternal(domain, intent, node.Edges);

            edge = transitionEdge;
            if (transitionEdge == null || transitionEdge.TargetNode == null)
            {
                return null;
            }

            return transitionEdge.TargetNode.NodeName;
        }

        private ConversationNodeEdge TransitionInternal(string domain, string intent, IEnumerable<ConversationNodeEdge> edges)
        {
            ConversationNodeEdge promiscuousEdge = null;
            
            foreach (ConversationNodeEdge edge in edges)
            {
                // The edge is in the common domain (and may or may not transition to an external domain as well)
                if ((edge.Scope == DomainScope.Common || edge.Scope == DomainScope.CommonExternal) &&
                    domain.Equals(DialogConstants.COMMON_DOMAIN) && intent.Equals(edge.Intent))
                {
                    return edge;
                }

                // The edge is in the local domain
                if (edge.Scope != DomainScope.Common &&
                    domain.Equals(_localDomain))
                {
                    if (string.IsNullOrEmpty(edge.Intent))
                    {
                        // save the fallback edge so we can use it if no other edge triggers
                        promiscuousEdge = edge;
                    }
                    else if (intent.Equals(edge.Intent))
                    {
                        return edge;
                    }
                }
            }

            // If there is a promiscuous edge, we will return that instead of null as a fallback
            return promiscuousEdge;
        }

        public string GetNextContinuation(string curNodeName, string domain, string intent)
        {
            if (!string.IsNullOrEmpty(curNodeName))
            {
                curNodeName = curNodeName.ToLowerInvariant();
            }

            ConversationNode node = GetRootNode();
            if (!string.IsNullOrEmpty(curNodeName))
            {
                if (!_nodes.TryGetValue(curNodeName, out node))
                {
                    throw new DialogException("Cannot process dialog tree transition because the node \"" + curNodeName + "\" was not found");
                }
            }

            ConversationNodeEdge promiscuousEdge = null;

            foreach (ConversationNodeEdge edge in node.Edges)
            {
                if (edge.Scope == DomainScope.Common &&
                    domain.Equals(DialogConstants.COMMON_DOMAIN) && intent.Equals(edge.Intent))
                {
                    return edge.TargetNode.HandlerFunction;
                }
                if (edge.Scope != DomainScope.Common &&
                    domain.Equals(_localDomain))
                {
                    if (intent.Equals(edge.Intent))
                    {
                        return edge.TargetNode.HandlerFunction;
                    }
                    else if (string.IsNullOrEmpty(edge.Intent))
                    {
                        // save the fallback edge so we can use it if no other edge triggers
                        promiscuousEdge = edge;
                    }
                }
            }

            // If there is a promiscuous edge, we will return that instead of null as a fallback
            if (promiscuousEdge != null)
            {
                return promiscuousEdge.TargetNode.HandlerFunction;
            }

            return null;
        }

        /*public static ConversationTree BuildDefaultTree(string domain, AnswerContinuation processFunction, IEnumerable<string> intents)
        {
            ConversationTree returnVal = new ConversationTree(domain);
            foreach (string intent in intents)
            {
                ConversationNode newNode = new ConversationNode(intent, processFunction);
                returnVal.AddRootNode(newNode);
            }
            return returnVal;
        }*/

        internal void SetNodeListInternal(IEnumerable<ConversationNode> nodeList)
        {
            foreach (ConversationNode node in nodeList)
            {
                _nodes[node.NodeName] = node;
            }
        }

        public SerializedConversationTree Serialize()
        {
            SerializedConversationTree returnVal = new SerializedConversationTree();
            returnVal.LocalDomain = _localDomain;
            returnVal.Nodes = new List<SerializedConversationNode>();
            foreach (var node in _nodes.Values)
            {
                returnVal.Nodes.Add(node.Serialize());
            }

            return returnVal;
        }
    }
}
