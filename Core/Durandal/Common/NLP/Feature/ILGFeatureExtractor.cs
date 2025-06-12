using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.NLP.Feature
{
    public interface ILGFeatureExtractor
    {
        /// <summary>
        /// Extracts features from tag values in an LG string
        /// </summary>
        /// <param name="tags">The set of all tag values, keyed by their name</param>
        /// <param name="groupToTagMap">A mapping from "group" (being the position in the sentence) and the tag which goes into that group</param>
        /// <param name="currentGroup">The current branching group that is being decided</param>
        /// <param name="featuresOut">A non-null list to which output features are appended</param>
        void ExtractTagFeatures(IDictionary<string, string> tags, IDictionary<int, string> groupToTagMap, int currentGroup, List<string> featuresOut);
    }
}
