using System;
using System.Collections.Generic;
using System.Linq;

namespace Durandal.Common.Collections.Graph
{
    public class ConfusionNetwork<T>
    {
        private readonly ISet<Edge> _edges;
        private readonly IDictionary<ushort, Node> _nodes;
        private static readonly IComparer<Edge> EdgeComparer = new EdgeScoreComparer();
        
        public struct Edge : IEquatable<Edge>
        {
            public ushort PreviousNodeIndex;
            public ushort NextNodeIndex;
            public bool IsLastEdge;
            public float Score;
            public T UserData;

            public override bool Equals(object obj)
            {
                if (!(obj is Edge))
                {
                    return false;
                }

                Edge other = (Edge)obj;
                return Equals(other);
            }

            public bool Equals(Edge other)
            {
                return PreviousNodeIndex == other.PreviousNodeIndex &&
                       NextNodeIndex == other.NextNodeIndex;
            }

            public override int GetHashCode()
            {
                var hashCode = -1319353824;
                hashCode = hashCode * -1521134295 + PreviousNodeIndex.GetHashCode();
                hashCode = hashCode * -1521134295 + NextNodeIndex.GetHashCode();
                return hashCode;
            }

            public static bool operator ==(Edge left, Edge right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(Edge left, Edge right)
            {
                return !(left == right);
            }
        }

        public class Node
        {
            public ushort Index;
            public List<Edge> EdgesOut;

            public Node(ushort index)
            {
                Index = index;
                EdgesOut = new List<Edge>();
            }
        }

        public ConfusionNetwork(ISet<Edge> edges)
        {
            _edges = edges;
            // Generate the set of nodes dynamically
            _nodes = GenerateNodesFromEdges(_edges);
        }

        public Node StartNode
        {
            get
            {
                return TryGetNode(0);
            }
        }

        public int NumEdges
        {
            get
            {
                return _edges.Count;
            }
        }

        public int NumNodes
        {
            get
            {
                return _nodes.Count;
            }
        }

        public Node TryGetNode(ushort index)
        {
            if (_nodes.ContainsKey(index))
            {
                return _nodes[index];
            }
            return null;
        }

        private IDictionary<ushort, Node> GenerateNodesFromEdges(ISet<Edge> edges)
        {
            IDictionary<ushort, Node> returnVal = new Dictionary<ushort, Node>();
            foreach (Edge thisEdge in edges)
            {
                // If the edge start node doesn't exist, create it
                if (!returnVal.ContainsKey(thisEdge.PreviousNodeIndex))
                {
                    returnVal[thisEdge.PreviousNodeIndex] = new Node(thisEdge.PreviousNodeIndex);
                }
                // Same with the edge end node
                if (!returnVal.ContainsKey(thisEdge.NextNodeIndex))
                {
                    returnVal[thisEdge.NextNodeIndex] = new Node(thisEdge.NextNodeIndex);
                }
                // Now add this edge to the start node's list of edges
                returnVal[thisEdge.PreviousNodeIndex].EdgesOut.Add(thisEdge);
            }
            // Sort the list of edges for each node
            foreach (Node thisNode in returnVal.Values)
            {
                if (thisNode.EdgesOut.Count > 1)
                {
                    thisNode.EdgesOut.Sort(EdgeComparer);
                }
            }
            return returnVal;
        }

        private class EdgeScoreComparer : IComparer<Edge>
        {
            public int Compare(Edge x, Edge y)
            {
                return System.Math.Sign(y.Score - x.Score);
            }
        }

        public IList<T> GetNaiveTraversal()
        {
            IList<IList<T>> result = new GreedyTraversal<T>().Traverse(this, 1);
            if (result.Count > 0)
            {
                return result.FirstOrDefault();
            }
            return new List<T>();
        }

        public IList<IList<T>> GetNBestTraversal(ITraversalMethod<T> method, int n)
        {
            return method.Traverse(this, n);
        }

        /*public static ConfusionNetwork<T> DeserializeFromJson(string json)
        {
            JsonSerializer ser = JsonSerializer.Create(new JsonSerializerSettings());
            JObject data = ser.Deserialize(new JsonTextReader(new StringReader(json))) as JObject;
            ISet<Edge> edges = new HashSet<Edge>();
            foreach (JToken obj in data["Arcs"].Children())
            {
                Edge newEdge = new Edge();
                newEdge.PreviousNodeIndex = obj["PreviousNodeIndex"].Value<ushort>();
                newEdge.NextNodeIndex = obj["NextNodeIndex"].Value<ushort>();
                newEdge.IsLastEdge = obj["IsLastArc"].Value<bool>();
                newEdge.Score = obj["Score"].Value<float>();
                int wordStartIndex = obj["WordStartIndex"].Value<int>();
                newEdge.UserData = data["WordTable"][wordStartIndex].Value<T>();
                edges.Add(newEdge);
            }
            return new ConfusionNetwork<T>(edges);
        }*/
    }
}
