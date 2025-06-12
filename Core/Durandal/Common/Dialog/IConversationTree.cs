using Durandal.API;

namespace Durandal.Common.Dialog
{
    /// <summary>
    /// Defines an abstract conversation tree
    /// </summary>
    public interface IConversationTree
    {
        void AddStartState(string intent, PluginContinuation target);

        void AddStartState(string intent, IConversationNode target);

        IConversationNode CreateNode(PluginContinuation handler);

        IConversationNode CreateNode(PluginContinuation handler, string nodeName);

        bool HasStartNode(string domain, string intent);

        string GetRetryHandlerName(string sourceNodeName);

        string GetNextContinuation(string curNodeName, string domain, string intent);

        bool TransitionExists(string curNodeName, string domain, string intent);

        string Transition(string curNodeName, string domain, string intent);

        string Transition(string curNodeName, string domain, string intent, out IConversationNodeEdge edge);

        SerializedConversationTree Serialize();
    }
}