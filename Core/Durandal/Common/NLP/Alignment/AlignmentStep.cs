using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.NLP.Alignment
{
    /// <summary>
    /// Used to express lexical alignment via a sequence of edits or substitutions
    /// </summary>
    public enum AlignmentStep
    {
        None, Add, Skip, Match, Edit
    }
}
