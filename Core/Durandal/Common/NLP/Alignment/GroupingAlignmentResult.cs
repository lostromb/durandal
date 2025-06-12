using Durandal.Common.LG;
using Durandal.Common.NLP.Tagging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.NLP.Alignment
{
    public class GroupingAlignmentResult
    {
        public List<LGSurfaceForm[]> Groups { get; set; }
        public List<TaggedSentence> TaggedInputs { get; set; }
    }
}
