using Durandal.API;
using Durandal.Common.File;
using Durandal.Common.Statistics;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Durandal.Common.NLP.ApproxString
{
    /// <summary>
    /// Represents an object which can perform lexical matching of input strings against a set
    /// of candidates. Note that this is NOT meant to be a generalized indexed search function - it does
    /// not take into account feature novelty and out-of-order tokens. The top results it attempts
    /// to return are those which sound most similar to the entire input, defined by a lexical difference
    /// function implemented by each concrete subclass of this interface.
    /// </summary>
    public interface ILexicalMatcher
    {
        /// <summary>
        /// Indexes a single string
        /// </summary>
        /// <param name="input">A string to index</param>
        void Index(LexicalString input);

        /// <summary>
        /// Indexes a set of strings as a batch
        /// </summary>
        /// <param name="input">The set of strings to index</param>
        void Index(IEnumerable<LexicalString> input);

        /// <summary>
        /// Attempts to match a novel input string against the set of indexed strings.
        /// Returns a possibly empty set of n-best hypotheses, ranked in descending order
        /// </summary>
        /// <param name="input">An input string to be matched</param>
        /// <param name="maxMatches">The maximum number of matches to return</param>
        /// <returns></returns>
        IList<Hypothesis<LexicalString>> Match(LexicalString input, int maxMatches = 5);

        /// <summary>
        /// Serializes this index to a specific external resource that could be reloaded again
        /// using specific constructors for each concrete class.
        /// </summary>
        /// <param name="fileSystem">A resource manager</param>
        /// <param name="targetName">The resource or file to write to</param>
        Task Serialize(IFileSystem fileSystem, VirtualPath targetName);
    }
}