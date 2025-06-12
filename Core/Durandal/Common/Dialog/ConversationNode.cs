using System;
using System.Collections.Generic;
using System.Linq;
using Durandal.API;
using System.Reflection;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Runtime;

namespace Durandal.Common.Dialog
{
    public class ConversationNode : IConversationNode
    {
        private readonly ISet<ConversationNodeEdge> _edges;
        private readonly string _localDomain; // For debugging
        private readonly string _guid;

        /// <summary>
        /// The unique name of this node. Always represented in lowercase.
        /// </summary>
        public string NodeName
        {
            get;
            private set;
        }

        /// <summary>
        /// The fully qualified name of the function that handles this step in the conversation, e.g. "MyPlugin.Namespace.RunDialog"
        /// </summary>
        public string HandlerFunction
        {
            get;
            private set;
        }

        /// <summary>
        /// The fully qualified name of the function that handles retries at this step of the conversation, e.g. "MyPlugin.Namespace.RepromptForInput".
        /// If null, the default handler function will be used for retries.
        /// </summary>
        public string RetryHandler
        {
            get;
            private set;
        }

        // Override default constructor and make it private
        private ConversationNode() { }

        /// <summary>
        /// Only a ConversationTree should be able to create a ConversationNode
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="nodeName"></param>
        /// <param name="localDomain"></param>
        /// <param name="retryHandler"></param>
        internal ConversationNode(string handler, string nodeName, string localDomain, string retryHandler = null)
        {
            _localDomain = localDomain;
            HandlerFunction = handler;
            RetryHandler = retryHandler;
            NodeName = nodeName;
            _guid = Guid.NewGuid().ToString("N");
            _edges = new HashSet<ConversationNodeEdge>();
        }

        /// <summary>
        /// Creates an edge that will transition from this node to another one when a specified intent triggers within this domain.
        /// </summary>
        /// <param name="intent">The intent (within this answer's domain) which will trigger this transition</param>
        /// <param name="targetNode">The conversation node to go to</param>
        public void CreateNormalEdge(string intent, IConversationNode targetNode)
        {
            if (string.IsNullOrWhiteSpace(intent))
            {
                throw new ArgumentException("Target intent cannot be null");
            }
            if (targetNode == null)
            {
                throw new ArgumentNullException("targetNode");
            }

            ConversationNodeEdge edge = new ConversationNodeEdge(intent, DomainScope.Local, targetNode);
            _edges.Add(edge);
        }

        /// <summary>
        /// Creates an edge that will transition from this node to another one when a specified intent triggers within the common domain.
        /// This is typically used for things like answering yes/no questions or for retries.
        /// </summary>
        /// <param name="commonIntent">The intent within the commondomain which will trigger this transition</param>
        /// <param name="targetNode">The conversation node to go to</param>
        public void CreateCommonEdge(string commonIntent, IConversationNode targetNode)
        {
            if (string.IsNullOrWhiteSpace(commonIntent))
            {
                throw new ArgumentException("Target intent cannot be null");
            }
            if (targetNode == null)
            {
                throw new ArgumentNullException("targetNode");
            }
            
            ConversationNodeEdge edge = new ConversationNodeEdge(commonIntent, DomainScope.Common, targetNode);
            _edges.Add(edge);
        }

        /// <summary>
        /// Creates an edge that will transition from this node to another node inside of another answer domain. Triggering this node
        /// will initiate a CrossDomainRequest/CrossDomainResponse between the two answer plugins.
        /// </summary>
        /// <param name="internalIntent">The intent (within THIS ANSWER'S domain) which will trigger this transition</param>
        /// <param name="externalDomain">The domain of the plugin to transition to</param>
        /// <param name="externalIntent">The intent that will "start the conversation" inside the other plugin. This should be provided as part of that plugin's cross-domain API</param>
        /// <param name="callbackNode">The node that the conversation will resume from if the external domain finishes while this domain continues</param>
        public void CreateExternalEdge(string internalIntent, string externalDomain, string externalIntent, IConversationNode callbackNode = null)
        {
            if (string.IsNullOrWhiteSpace(internalIntent))
            {
                throw new ArgumentException("internalIntent cannot be null");
            }
            if (string.IsNullOrWhiteSpace(externalDomain))
            {
                throw new ArgumentException("externalDomain cannot be null");
            }
            if (string.IsNullOrWhiteSpace(externalIntent))
            {
                throw new ArgumentException("externalIntent cannot be null");
            }
            
            ConversationNodeEdge edge = new ConversationNodeEdge(internalIntent, externalDomain, externalIntent, callbackNode, false);
            _edges.Add(edge);
        }

        /// <summary>
        /// Creates an edge that will transition from this node to another node inside of another answer domain, and the
        /// triggering intent is something inside the common domain. Triggering this node
        /// will initiate a CrossDomainRequest/CrossDomainResponse between the two answer plugins.
        /// </summary>
        /// <param name="commonIntent">The intent (within the COMMON domain) which will trigger this transition</param>
        /// <param name="externalDomain">The domain of the plugin to transition to</param>
        /// <param name="externalIntent">The intent that will "start the conversation" inside the other plugin. This should be provided as part of that plugin's cross-domain API</param>
        /// <param name="callbackNode">The node that the conversation will resume from if the external domain finishes while this domain continues</param>
        public void CreateExternalCommonEdge(string commonIntent, string externalDomain, string externalIntent, IConversationNode callbackNode = null)
        {
            if (string.IsNullOrWhiteSpace(commonIntent))
            {
                throw new ArgumentException("internalIntent cannot be null");
            }
            if (string.IsNullOrWhiteSpace(externalDomain))
            {
                throw new ArgumentException("externalDomain cannot be null");
            }
            if (string.IsNullOrWhiteSpace(externalIntent))
            {
                throw new ArgumentException("externalIntent cannot be null");
            }

            ConversationNodeEdge edge = new ConversationNodeEdge(commonIntent, externalDomain, externalIntent, callbackNode, true);
            _edges.Add(edge);
        }

        /// <summary>
        /// Creates an edge that will transition to the given node for _any_ unhandled intent that is within the current answer domain.
        /// This is used for fallback or other weird / special cases.
        /// </summary>
        /// <param name="targetNode">The node to fall back to</param>
        public void CreatePromiscuousEdge(IConversationNode targetNode)
        {
            if (targetNode == null)
            {
                throw new ArgumentNullException("targetNode");
            }
            
            ConversationNodeEdge edge = new ConversationNodeEdge(targetNode);
            _edges.Add(edge);
        }

        public IEnumerable<ConversationNodeEdge> Edges
        {
            get
            {
                return _edges.AsEnumerable();
            }
        }

        protected bool Equals(ConversationNode other)
        {
            return string.Equals(_guid, other._guid);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ConversationNode) obj);
        }

        public override int GetHashCode()
        {
            return _guid.GetHashCode();
        }

        /// <summary>
        /// Registers a function that will be triggered whenever this conversation node is unable to transition anywhere.
        /// In such cases, it is assumed that the system wants some specific input from the user, which can be prompted
        /// for inside of the retry handler.
        /// </summary>
        /// <param name="responseHandler"></param>
        public void EnableRetry(PluginContinuation responseHandler)
        {
            RetryHandler = AbstractDialogExecutor.GetNameOfPluginContinuation(responseHandler);
        }

        internal void AddEdgeInternal(ConversationNodeEdge edge)
        {
            _edges.Add(edge);
        }

        public SerializedConversationNode Serialize()
        {
            SerializedConversationNode returnVal = new SerializedConversationNode();
            returnVal.HandlerFunction = HandlerFunction;
            returnVal.NodeName = NodeName;
            returnVal.RetryHandler = RetryHandler;
            returnVal.Edges = new List<SerializedConversationEdge>();
            foreach (var edge in _edges)
            {
                returnVal.Edges.Add(edge.Serialize());
            }

            return returnVal;
        }
    }
}
