using System.Collections.Generic;
using Durandal.Common.MathExt;
using Durandal.Common.Statistics;

namespace Durandal.Common.Collections.Graph
{
    public class OffByOneTraversal<E> : ITraversalMethod<E>
    {
        private static readonly IComparer<Hypothesis<IList<E>>> FinalResultSorter = new Hypothesis<IList<E>>.DescendingComparator();

        private float _minConfidence;

        public OffByOneTraversal(float minConfidence = 0)
        {
            _minConfidence = minConfidence;
        }

        public IList<IList<E>> Traverse(ConfusionNetwork<E> network, int n)
        {
            // Start at the start node and pick the most likely edge every time
            if (network.TryGetNode(0) == null)
            {
                // Start node doesn't exist.
                return new List<IList<E>>();
            }

            List<Hypothesis<IList<E>>> intermediateList = new List<Hypothesis<IList<E>>>();
            ISet<IList<E>> pathsTaken = new HashSet<IList<E>>();
            LinkedList<int> pathsToTake = new LinkedList<int>();

            pathsToTake.AddFirst(-1);

            // Try all the possibilities
            while (pathsToTake.Count > 0)
            {
                int decision = pathsToTake.First.Value;
                pathsToTake.RemoveFirst();
                float pathScore;
                IList<E> path = TraverseUsingChoices(network, decision, out pathScore);
                // Has this path already been taken?
                if (!PathAlreadyTaken(pathsTaken, path))
                {
                    pathsTaken.Add(path);
                    intermediateList.Add(new Hypothesis<IList<E>>(path, pathScore));
                    pathsToTake.AddLast(decision + 1);
                }
            }
            
            // Sort the intermediate results and cull to the highest n-best
            intermediateList.Sort(FinalResultSorter);

            IList<IList<E>> returnVal = new List<IList<E>>();
            for (int c = 0; c < FastMath.Min(intermediateList.Count, n); c++)
            {
                returnVal.Add(intermediateList[c].Value);
            }

            return returnVal;
        }

        private IList<E> TraverseUsingChoices(ConfusionNetwork<E> network, int branchIndex, out float pathScore)
        {
            int currentDecisionPoint = 0;
            ConfusionNetwork<E>.Node currentNode = network.TryGetNode(0);
            pathScore = 0;
            bool hitTerminalEdge = false;
            IList<E> traversal = new List<E>();
            do
            {
                if (currentNode.EdgesOut.Count == 0)
                {
                    // No more edges remaining to traverse
                    hitTerminalEdge = true;
                }
                else 
                {
                    ConfusionNetwork<E>.Edge nextTraversal = currentNode.EdgesOut[0];
                    if (currentNode.EdgesOut.Count > 1)
                    {
                        // Multiple choices. See if this is our decision point
                        if (branchIndex == currentDecisionPoint && currentNode.EdgesOut[1].Score >= _minConfidence)
                        {
                            // Choose the 2nd best edge for this case only
                            nextTraversal = currentNode.EdgesOut[1];
                        }
                        currentDecisionPoint++;
                    }
                    traversal.Add(nextTraversal.UserData);
                    pathScore += nextTraversal.Score;
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

            // Score this path based on the average score of all the edges traversed
            if (traversal.Count > 0)
            {
                pathScore /= traversal.Count;
            }
                
            return traversal;
        }

        private static bool PathAlreadyTaken(ISet<IList<E>> pathsTaken, IList<E> path)
        {
            foreach (IList<E> takenPath in pathsTaken)
            {
                bool pathMatches = (takenPath.Count == path.Count);
                for (int c = 0; c < takenPath.Count && pathMatches; c++)
                {
                    if (!takenPath[c].Equals(path[c]))
                        pathMatches = false;
                }
                if (pathMatches)
                    return true;
            }
            return false;
        }
    }
}
