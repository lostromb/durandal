using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.API
{
    public static class LgCommon
    {
        /// <summary>
        /// A delegate that allows custom code that generates language output. To be used by callers
        /// that manually register precompiled bits of code as entire phrases, not as in-line scripts (see the <see cref="RunLGScript"/> delegate for that) 
        /// </summary>
        /// <param name="substitutions">The list of substitutions currently specified in the pattern</param>
        /// <param name="logger">A logger</param>
        /// <param name="clientContext">The current query's context</param>
        /// <returns>A set of rendered LG strings</returns>
        public delegate RenderedLG RunLanguageGeneration(
            IDictionary<string, object> substitutions,
            ILogger logger,
            ClientContext clientContext);

        /// <summary>
        /// The actual delegate implementation for arbitrary LG scripts that run
        /// inside the statistical engine
        /// </summary>
        /// <param name="substitutions">The set of all substitutions (parameters) in the LG request, before transformations have run</param>
        /// <param name="phraseName">The name of the current phrase. If the script modifies this value, control will be passed to that phrase.</param>
        /// <param name="locale">The locale of the request</param>
        /// <param name="Log">A method to write log messages</param>
        /// <returns>The updated phrase name, if it was modified during script execution</returns>
        public delegate string RunLGScript(
            IDictionary<string, object> substitutions,
            string phraseName,
            string locale,
            Action<string> Log);
    }
}
