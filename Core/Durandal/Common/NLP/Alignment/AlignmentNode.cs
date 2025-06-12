using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.NLP.Alignment
{
    /// <summary>
    /// These objects form a grid of values inside the DP table which calculates lexical alignment
    /// </summary>
    public class AlignmentNode
    {
        public int Score;
        public int Distance;
        public AlignmentStep Step;
        public AlignmentNode Backpointer;
    }
}
