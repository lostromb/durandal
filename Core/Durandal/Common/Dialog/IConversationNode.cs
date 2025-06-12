using Durandal.API;

namespace Durandal.Common.Dialog
{
    /// <summary>
    /// Defines a node in a conversation tree.
    /// </summary>
    public interface IConversationNode
    {
        string NodeName { get; }
        string HandlerFunction { get; }
        string RetryHandler { get; }
        void CreateCommonEdge(string commonIntent, IConversationNode targetNode);
        void CreateExternalCommonEdge(string commonIntent, string externalDomain, string externalIntent, IConversationNode callbackNode = null);
        void CreateExternalEdge(string internalIntent, string externalDomain, string externalIntent, IConversationNode callbackNode = null);
        void CreateNormalEdge(string intent, IConversationNode targetNode);
        void CreatePromiscuousEdge(IConversationNode targetNode);
        void EnableRetry(PluginContinuation responseHandler);
    }
}