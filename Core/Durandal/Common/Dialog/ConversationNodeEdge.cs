using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Durandal.API;

namespace Durandal.Common.Dialog
{
    public class ConversationNodeEdge : IConversationNodeEdge
    {
        public DomainScope Scope
        {
            get;
            private set;
        }

        public string ExternalDomain
        {
            get;
            private set;
        }

        public string ExternalIntent
        {
            get;
            private set;
        }

        public string Intent
        {
            get;
            private set;
        }

        public IConversationNode TargetNode
        {
            get;
            private set;
        }

        // Override default constructor and make it private
        private ConversationNodeEdge() { }

        /// <summary>
        /// Creates a standard "internal" edge. This edge connects one node to another
        /// </summary>
        /// <param name="intent"></param>
        /// <param name="domainScope"></param>
        /// <param name="targetNode"></param>
        internal ConversationNodeEdge(string intent, DomainScope domainScope, IConversationNode targetNode)
        {
            Scope = domainScope;
            ExternalDomain = string.Empty;
            ExternalIntent = string.Empty;
            Intent = intent;
            TargetNode = targetNode;
        }

        /// <summary>
        /// Creates a "promiscuous" node that will transition to the given node for any unhandled intent within this answer's domain.
        /// </summary>
        /// <param name="targetNode"></param>
        internal ConversationNodeEdge(IConversationNode targetNode)
        {
            Scope = DomainScope.Local;
            ExternalDomain = string.Empty;
            ExternalIntent = string.Empty;
            Intent = string.Empty;
            TargetNode = targetNode;
        }

        /// <summary>
        /// Creates an external edge. This edge links out to an external domain, for use in cross-domain calls.
        /// </summary>
        /// <param name="intent"></param>
        /// <param name="externalDomain"></param>
        /// <param name="externalIntent"></param>
        /// <param name="targetNode">The node on this domain's tree that the conversation is "parked" at after the cross-domain call executes. Used for callbacks</param>
        /// <param name="usesCommonDomain"></param>
        internal ConversationNodeEdge(string intent, string externalDomain, string externalIntent, IConversationNode targetNode, bool usesCommonDomain)
        {
            if (usesCommonDomain)
                Scope = DomainScope.CommonExternal;
            else
                Scope = DomainScope.External;
            ExternalDomain = externalDomain;
            ExternalIntent = externalIntent;
            Intent = intent;
            TargetNode = targetNode;
        }

        /// <summary>
        /// Used internally for deserialization
        /// </summary>
        /// <param name="intent"></param>
        /// <param name="externalDomain"></param>
        /// <param name="externalIntent"></param>
        /// <param name="targetNode"></param>
        /// <param name="domainScope"></param>
        internal ConversationNodeEdge(string intent, DomainScope domainScope, IConversationNode targetNode, string externalDomain, string externalIntent)
        {
            Scope = domainScope;
            ExternalDomain = externalDomain;
            ExternalIntent = externalIntent;
            Intent = intent;
            TargetNode = targetNode;
        }

        public SerializedConversationEdge Serialize()
        {
            SerializedConversationEdge returnVal = new SerializedConversationEdge();
            returnVal.Intent = Intent;
            returnVal.Scope = Scope;
            returnVal.TargetNodeName = TargetNode?.NodeName;
            returnVal.ExternalDomain = ExternalDomain;
            returnVal.ExternalIntent = ExternalIntent;
            return returnVal;
        }
    }
}
