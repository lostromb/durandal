using Durandal.API;
using Durandal.Common.NLP.Feature;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.NLP.ApproxString
{
    public interface IApproxStringFeatureExtractor
    {
        IList<string> ExtractFeatures(LexicalString input);
    }
}
