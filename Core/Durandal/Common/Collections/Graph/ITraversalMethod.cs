using System.Collections.Generic;

namespace Durandal.Common.Collections.Graph
{
    public interface ITraversalMethod<T>
    {
        IList<IList<T>> Traverse(ConfusionNetwork<T> network, int n);
    }
}