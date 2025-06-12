using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Services;
using Durandal.API;
using System.Threading.Tasks;
using Durandal.Common.Tasks;
using Durandal.Common.Ontology;
using Durandal.Common.Logger;
using Durandal.Common.Dialog.Runtime;
using Durandal.Common.Time;
using Durandal.Common.File;
using Durandal;

namespace BVTTestDriver
{
    public class BvtWrapperPluginProvider : IDurandalPluginProvider
    {
        IDurandalPluginProvider _wrappedProvider;
        
        public BvtWrapperPluginProvider(IDurandalPluginProvider wrappedProvider)
        {
            _wrappedProvider = wrappedProvider;
            throw new NotImplementedException("BVTs are out of maintenance now");
        }

        public Task<CrossDomainRequestData> CrossDomainRequest(PluginStrongName targetPlugin, string targetIntent, ILogger queryLogger, IRealTimeProvider realTime)
        {
            return _wrappedProvider.CrossDomainRequest(targetPlugin, targetIntent, queryLogger, realTime);
        }

        public Task<CrossDomainResponseResponse> CrossDomainResponse(PluginStrongName targetPlugin, CrossDomainContext context, ILogger queryLogger, InMemoryDataStore sessionStore, InMemoryDataStore globalUserProfile, KnowledgeContext entityContext, IRealTimeProvider realTime)
        {
            return _wrappedProvider.CrossDomainResponse(targetPlugin, context, queryLogger, sessionStore, globalUserProfile, entityContext, realTime);
        }

        public Task<IEnumerable<PluginStrongName>> GetAllAvailablePlugins(IRealTimeProvider realTime)
        {
            return _wrappedProvider.GetAllAvailablePlugins(realTime);
        }

        //public DurandalPlugin GetPlugin(PluginStrongName id)
        //{
        //    DurandalPlugin wrappedAnswer = _wrappedProvider.GetPlugin(id);
        //    if (wrappedAnswer == null)
        //        return null;
        //    return new BvtStubAnswer(wrappedAnswer);
        //}

        public Task<DialogProcessingResponse> LaunchPlugin(
            PluginStrongName plugin,
            string entryPoint,
            bool isRetry,
            QueryWithContext query,
            ILogger queryLogger,
            InMemoryDataStore localSessionStore,
            UserProfileCollection userProfiles,
            KnowledgeContext entityContext,
            IList<ContextualEntity> contextualEntities,
            IRealTimeProvider realTime)
        {
            return _wrappedProvider.LaunchPlugin(plugin, entryPoint, isRetry, query, queryLogger, localSessionStore, userProfiles, entityContext, contextualEntities, realTime);
        }

        public Task<LoadedPluginInformation> LoadPlugin(PluginStrongName plugin, ILogger logger, IRealTimeProvider realTime)
        {
            return _wrappedProvider.LoadPlugin(plugin, logger, realTime);
        }

        public Task<TriggerProcessingResponse> TriggerPlugin(
            PluginStrongName plugin,
            QueryWithContext query,
            ILogger queryLogger,
            InMemoryDataStore localSessionStore,
            UserProfileCollection userProfiles,
            KnowledgeContext entityContext,
            IList<ContextualEntity> contextualEntities,
            IRealTimeProvider realTime)
        {
            return _wrappedProvider.TriggerPlugin(plugin, query, queryLogger, localSessionStore, userProfiles, entityContext, contextualEntities, realTime);
        }

        public Task<bool> UnloadPlugin(PluginStrongName plugin, ILogger logger, IRealTimeProvider realTime)
        {
            return _wrappedProvider.UnloadPlugin(plugin, logger, realTime);
        }

        public Task<CachedWebData> FetchPluginViewData(PluginStrongName plugin, string path, DateTimeOffset? ifModifiedSince, ILogger traceLogger, IRealTimeProvider realTime)
        {
            return _wrappedProvider.FetchPluginViewData(plugin, path, ifModifiedSince, traceLogger, realTime);
        }

        public void Dispose() { }
    }

    public class BvtStubAnswer : DurandalPlugin
    {
        private DurandalPlugin _wrappedAnswer;

        public BvtStubAnswer(DurandalPlugin wrappedAnswer) : base(wrappedAnswer.PluginId, wrappedAnswer.LUDomain)
        {
            _wrappedAnswer = wrappedAnswer;
        }

        public override async Task<PluginResult> Execute(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            PluginResult returnVal = new PluginResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinuePassively
            };

            returnVal.ResponseData["domain"] = queryWithContext.Understanding.Domain;
            returnVal.ResponseData["intent"] = queryWithContext.Understanding.Intent;
            returnVal.ResponseText = "BVT Test Hook Answer for " + this.LUDomain;
            return returnVal;
        }

        public override async Task<CrossDomainRequestData> CrossDomainRequest(string targetIntent)
        {
            await DurandalTaskExtensions.NoOpTask;
            return new CrossDomainRequestData();
        }

        public override async Task<CrossDomainResponseData> CrossDomainResponse(CrossDomainContext context, IPluginServices pluginServices)
        {
            await DurandalTaskExtensions.NoOpTask;
            return new CrossDomainResponseData();
        }
        
        protected override IConversationTree BuildConversationTree(IConversationTree tree, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            ConversationTree originalConversationTree = _wrappedAnswer.GetConversationTreeSingleton(pluginFileSystem, pluginDataDirectory) as ConversationTree;
            if (originalConversationTree == null)
                return null;

            ConversationTree augmentedConversationTree = new ConversationTree(_wrappedAnswer.LUDomain);
            IDictionary<string, ConversationNode> originalNodes = new Dictionary<string, ConversationNode>();
            EnumerateNodes(originalConversationTree.GetRootNode(), originalNodes);

            IDictionary<string, ConversationNode> augmentedNodes = new Dictionary<string, ConversationNode>();

            // Create all nodes and add them to the tree
            foreach (ConversationNode node in originalNodes.Values)
            {
                if (!node.NodeName.Equals(augmentedConversationTree.GetRootNode().NodeName))
                {
                    ConversationNode newNode = augmentedConversationTree.CreateNode(this.Execute, node.NodeName) as ConversationNode;
                    augmentedNodes.Add(newNode.NodeName, newNode);
                }
            }

            // Specify the starting node set
            foreach (ConversationNodeEdge edge in originalConversationTree.GetRootNode().Edges)
            {
                if (edge.Scope == DomainScope.Local)
                {
                    ConversationNode matchingTargetNode = augmentedNodes[edge.TargetNode.NodeName];
                    augmentedConversationTree.AddStartState(edge.Intent, matchingTargetNode);
                }
            }

            // Now recreate all the other edges between the nodes
            foreach (ConversationNode origNode in originalNodes.Values)
            {
                if (origNode.NodeName.Equals(originalConversationTree.GetRootNode().NodeName))
                    continue;

                ConversationNode augNode = augmentedNodes[origNode.NodeName];

                foreach (ConversationNodeEdge edge in origNode.Edges)
                {
                    if (edge.Scope == DomainScope.Local)
                    {
                        ConversationNode matchingTargetNode = augmentedNodes[edge.TargetNode.NodeName];
                        augNode.CreateNormalEdge(edge.Intent, matchingTargetNode);
                    }
                    else if (edge.Scope == DomainScope.Common)
                    {
                        ConversationNode matchingTargetNode = augmentedNodes[edge.TargetNode.NodeName];
                        augNode.CreateCommonEdge(edge.Intent, matchingTargetNode);
                    }
                    else if (edge.Scope == DomainScope.External)
                    {
                        // External nodes shouldn't actually go out to the other domain; that would be tough to isolate in a test situation,
                        // plus the actual outcome is different from the "expected" outcome because of the hop.
                        // So, treat external edges as a special normal edge.
                        ConversationNode fakeExternalNode = augmentedConversationTree.CreateNode(Execute, "surrogate for " + edge.ExternalDomain + "/" + edge.ExternalIntent) as ConversationNode;
                        augNode.CreateNormalEdge(edge.Intent, fakeExternalNode);
                    }
                }
            }

            return augmentedConversationTree;
        }

        private void EnumerateNodes(ConversationNode thisNode, IDictionary<string, ConversationNode> output)
        {
            // Prevent infinite recursion
            if (output.ContainsKey(thisNode.NodeName))
                return;

            // Add this
            output.Add(thisNode.NodeName, thisNode);

            // Add children recursively
            foreach (ConversationNodeEdge edge in thisNode.Edges)
            {
                if (edge.TargetNode != null)
                    EnumerateNodes((ConversationNode)edge.TargetNode, output);
            }
        }
    }
}
