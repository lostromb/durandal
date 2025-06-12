using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.Http
{
    /// <summary>
    /// Represents the interface for a mutable collection of HTTP headers.
    /// </summary>
    public interface IHttpHeaders : IEnumerable<KeyValuePair<string, IReadOnlyCollection<string>>>
    {
        /// <summary>
        /// Gets or sets a single value for a header. If there are multiple values, GET will return
        /// the first in the collection, and SET will override the entire value set. As such,
        /// this property is generally provided for convenience but may have the risk of hiding
        /// or clobbering data that you don't intend to.
        /// If no headers are found, this method returns null. Empty string is a valid header value.
        /// </summary>
        /// <param name="headerName">The header to check for</param>
        /// <returns>The first header value found with the given key, or null if not found.</returns>
        string this[string headerName] { get; set; }

        /// <summary>
        /// Returns the number of distinct header keys in this header collection.
        /// </summary>
        int KeyCount { get; }

        /// <summary>
        /// Adds a header to the collection. If a header with the given name already exists, it will
        /// be appended to the collection, retaining any existing values.
        /// </summary>
        /// <param name="headerName">The name of the header to add (case insensitive).</param>
        /// <param name="headerValue">The header value to append</param>
        void Add(string headerName, string headerValue);

        /// <summary>
        /// Sets the value of a specific header. If one or more headers already exist with that name,
        /// the existing ones will be removed first.
        /// </summary>
        /// <param name="headerName">The name of the header (case insensitive).</param>
        /// <param name="headerValue">The value to set for the header</param>
        void Set(string headerName, string headerValue);

        /// <summary>
        /// Removes all instances of the specified header name from the collection.
        /// </summary>
        /// <param name="headerName">The header to remove (case insensitive).</param>
        void Remove(string headerName);

        /// <summary>
        /// Checks whether this header collection contains the specified header name.
        /// All header comparisons are case insensitive.
        /// </summary>
        /// <param name="headerName">The header name to check for (case insensitive).</param>
        /// <returns>True if that header is present</returns>
        bool ContainsKey(string headerName);

        /// <summary>
        /// Checks whether any header with the specified key contains the specified list value.
        /// This method is intended for headers like Accept-Encoding, which often have "list" formats
        /// such as "deflate, gzip;q=1.0, *;q=0.5". This method checks against each individual value
        /// _in the list_, taking into account the fact that multiple headers with different values is
        /// semantically equivalent to a single header with a list of values.
        /// See RFC 7230 3.2.2 "Field Order"
        /// </summary>
        /// <param name="headerName">The header name to check. If this header appears multiple times, all instances will be checked. Case insensitive.</param>
        /// <param name="expectedValue">The list value to look for (whitespace will be trimmed).</param>
        /// <param name="stringComparison">The string comparison to use</param>
        /// <returns>True if the given value was found.</returns>
        bool ContainsValue(string headerName, string expectedValue, StringComparison stringComparison);

        /// <summary>
        /// Enumerates a list of values for the given header name, according to the format for
        /// multi-value headers (a comma-separated list), commonly things like the Transfer-Encoding or Set-Cookie headers.
        /// For example, "Accept-Encoding: br, gzip, deflate" would enumerate "br", "gzip" and "deflate" individually.
        /// Likewise if each individual value was on its own duplicate header line.
        /// This method will treat one header with multiple values, several headers with single values, or combinations
        /// of the two as semantically equivalent.
        /// </summary>
        /// <param name="headerName">The header name to enumerate (case insensitive).</param>
        /// <returns>All of the potentially comma-separated values for this header</returns>
        IEnumerable<string> EnumerateValueList(string headerName);

        /// <summary>
        /// Attempts to fetch a single header from the header collection. If there
        /// are multiple header values with the same name, this will return
        /// the first one that matches.
        /// All header name comparisons are case insensitive.
        /// </summary>
        /// <param name="headerName">The header name to fetch (case insensitive).</param>
        /// <param name="headerValue">A string to store the output</param>
        /// <returns>True if fetch succeeded</returns>
        bool TryGetValue(string headerName, out string headerValue);
    }
}
