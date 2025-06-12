using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.API
{
    public class RenderedLG
    {
        /// <summary>
        /// The plain-text or displayed text form of the sentence
        /// </summary>
        public string Text = string.Empty;

        /// <summary>
        /// The SSML or spoken form of the sentence
        /// </summary>
        public string Spoken = string.Empty;

        /// <summary>
        /// (Deprecated) The "short text" form of the sentence, when output space is limited
        /// </summary>
        public string ShortText = string.Empty;

        /// <summary>
        /// An optional set of extra string properties that may be output by the phrase. For example, UI hints, avatar state, labels, suggestions, image references, etc.
        /// </summary>
        public IDictionary<string, string> ExtraFields;
    }
}
