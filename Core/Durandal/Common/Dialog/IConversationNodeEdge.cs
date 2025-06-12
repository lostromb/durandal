using Durandal.API;

namespace Durandal.Common.Dialog
{
    /// <summary>
    /// Defines an edge between two nodes in a conversation tree.
    /// </summary>
    public interface IConversationNodeEdge
    {
        string ExternalDomain { get; }
        string ExternalIntent { get; }
        string Intent { get; }
        DomainScope Scope { get; }
        IConversationNode TargetNode { get; }
    }
}