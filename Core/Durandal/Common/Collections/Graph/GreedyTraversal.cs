using System.Collections.Generic;

namespace Durandal.Common.Collections.Graph
{
    public class GreedyTraversal<E> : ITraversalMethod<E>
    {
        public IList<IList<E>> Traverse(ConfusionNetwork<E> network, int n)
        {
            IList<IList<E>> returnVal = new List<IList<E>>();

            // Start at the start node and pick the most likely edge every time
            ConfusionNetwork<E>.Node currentNode = network.TryGetNode(0);
            if (currentNode == null)
            {
                // Start node doesn't exist.
                return returnVal;
            }

            bool hitTerminalEdge = false;
            IList<E> bestTraversal = new List<E>();
            do
            {
                if (currentNode.EdgesOut.Count == 0)
                {
                    // No more edges remaining to traverse
                    hitTerminalEdge = true;
                }
                else
                {
                    // Pick the most likely edge leading out of the current node
                    ConfusionNetwork<E>.Edge nextTraversal = currentNode.EdgesOut[0];
                    bestTraversal.Add(nextTraversal.UserData);
                    if (nextTraversal.IsLastEdge)
                    {
                        hitTerminalEdge = true;
                    }
                    else
                    {
                        currentNode = network.TryGetNode(nextTraversal.NextNodeIndex);
                        hitTerminalEdge = (currentNode == null);
                    }
                }
            } while (!hitTerminalEdge);

            returnVal.Add(bestTraversal);
            return returnVal;
        }
    }
}
