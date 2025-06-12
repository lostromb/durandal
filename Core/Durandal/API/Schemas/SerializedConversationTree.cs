using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Runtime;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.API
{
    public class SerializedConversationEdge
    {
        public string Intent { get; set; }
        public string TargetNodeName { get; set; }
        public DomainScope Scope { get; set; }
        public string ExternalDomain { get; set; }
        public string ExternalIntent { get; set; }
    }

    public class SerializedConversationNode
    {
        public string NodeName { get; set; }
        public string HandlerFunction { get; set; }
        public string RetryHandler { get; set; }
        public List<SerializedConversationEdge> Edges { get; set; }
    }

    public class SerializedConversationTree
    {
        public string LocalDomain { get; set; }
        public List<SerializedConversationNode> Nodes { get; set; }

        public IConversationTree Deserialize()
        {
            ConversationTree returnVal = new ConversationTree(LocalDomain);
            IDictionary<string, ConversationNode> reifiedNodes = new Dictionary<string, ConversationNode>();

            // Do a first pass just indexing the nodes that exist
            foreach (SerializedConversationNode node in Nodes)
            {
                ConversationNode reifiedNode = new ConversationNode(node.HandlerFunction, node.NodeName, LocalDomain, node.RetryHandler);
                reifiedNodes[node.NodeName] = reifiedNode;
            }

            // Now we can hook up edges and bind them to actual node references
            foreach (SerializedConversationNode node in Nodes)
            {
                ConversationNode reifiedNode = reifiedNodes[node.NodeName];
                foreach (SerializedConversationEdge edge in node.Edges)
                {
                    ConversationNode targetNode = string.IsNullOrEmpty(edge.TargetNodeName) ? null : reifiedNodes[edge.TargetNodeName];
                    ConversationNodeEdge reifiedEdge = new ConversationNodeEdge(edge.Intent, edge.Scope, targetNode, edge.ExternalDomain, edge.ExternalIntent);
                    reifiedNode.AddEdgeInternal(reifiedEdge);
                }
            }

            returnVal.SetNodeListInternal(reifiedNodes.Values);
            return returnVal;
        }
    }
}
