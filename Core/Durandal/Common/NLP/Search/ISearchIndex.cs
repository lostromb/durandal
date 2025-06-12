

namespace Durandal.Common.NLP.Search
{
    using Durandal.Common.File;
    using Durandal.Common.Statistics;
    using System.Collections.Generic;

    /// <summary>
    /// An interface representing a string search index mapping strings to documents
    /// </summary>
    /// <typeparam name="T">The type of the documents being searched</typeparam>
    public interface ISearchIndex<T>
    {
        /// <summary>
        /// Indexes a single documents
        /// </summary>
        /// <param name="input">A string to index</param>
        /// <param name="document">The document that this string should point to</param>
        void Index(string input, T document);

        /// <summary>
        /// Indexes a set of documents as a batch
        /// </summary>
        /// <param name="inputs">The set of strings to index, each string mapping to a document</param>
        void Index(IDictionary<string, T> inputs);

        /// <summary>
        /// Attempts to match a novel input string against the set of indexed strings.
        /// Returns a possibly empty set of n-best hypotheses, ranked in descending order
        /// </summary>
        /// <param name="input">An input string to be matched</param>
        /// <param name="maxMatches">The maximum number of matches to return</param>
        /// <returns></returns>
        IList<Hypothesis<T>> Search(string input, int maxMatches = 5);

        /// <summary>
        /// Serializes this index to a specific external resource that could be reloaded again
        /// using specific constructors for each concrete class.
        /// </summary>
        /// <param name="fileSystem">A resource manager</param>
        /// <param name="targetName">The resource or file to write to</param>
        void Serialize(IFileSystem fileSystem, VirtualPath targetName);
    }
}