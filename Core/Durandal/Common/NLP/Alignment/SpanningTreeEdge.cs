using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.NLP.Alignment
{
    internal class SpanningTreeEdge
    {
        public LexicalGraphNode A { get; set; }
        public LexicalGraphNode B { get; set; }
        public float Score { get; set; }

        public override int GetHashCode()
        {
            return A.GetHashCode() + B.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            SpanningTreeEdge o = (SpanningTreeEdge)obj;
            return (A.Equals(o.A) && B.Equals(o.B)) || (A.Equals(o.B) && B.Equals(o.A));
        }

        public override string ToString()
        {
            return "(" + Score + ") " + A.ToString() + " => " + B.ToString();
        }
    }
}
