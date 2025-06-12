using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.Http
{
    /// <summary>
    /// Represents a mutable collection of HTTP form parameters.
    /// These appear most commonly as URL ("Get") parameters,
    /// but can be represented equivalently as an HTTP Form (X-Multipart-FormData)
    /// </summary>
    public interface IHttpFormParameters : IEnumerable<KeyValuePair<string, IReadOnlyCollection<string>>>
    {
        /// <summary>
        /// Gets or sets a single value for a parameter. If there are multiple values, GET will return
        /// the first in the collection, and SET will override the entire value set.
        /// </summary>
        string this[string parameterName] { get; set; }

        /// <summary>
        /// Returns the number of distinct parameter keys in this parameter collection.
        /// </summary>
        int KeyCount { get; }

        /// <summary>
        /// Adds a parameter to the collection. If a parameter with the given name already exists, it will
        /// be appended to the collection, retaining any existing values. The value is allowed to be null or empty, in which case
        /// the parameter will be stored as a key only (this is a rare case that some servers use for their query parameters).
        /// </summary>
        /// <param name="parameterName">The name of the parameter to add.</param>
        /// <param name="value">The value of the new parameter</param>
        void Add(string parameterName, string value);

        /// <summary>
        /// Attempts to fetch a single parameter from the parameter collection. If there
        /// are multiple parameter values with the same name, this will return
        /// the first one that matches.
        /// Parameter lookups depend on the case sensitivity specified at collection creation, but is usually case-insensitive.
        /// </summary>
        /// <param name="parameterName">The parameter name to fetch</param>
        /// <param name="parameterValue">A string to store the output</param>
        /// <returns>True if fetch succeeded</returns>
        bool TryGetValue(string parameterName, out string parameterValue);

        /// <summary>
        /// Checks whether this parameter collection contains the specified parameter name.
        /// Parameter lookups depend on the case sensitivity specified at collection creation, but is usually case-insensitive.
        /// </summary>
        /// <param name="parameterName">The parameter name to check for.</param>
        /// <returns>True if that parameter is present</returns>
        bool ContainsKey(string parameterName);

        /// <summary>
        /// Removes all instances of the specified parameter name from the collection.
        /// </summary>
        /// <param name="parameterName">The parameter to remove</param>
        void Remove(string parameterName);

        /// <summary>
        /// Sets the value of a specific parameter. If one or more parameters already exist with that name,
        /// the existing ones will be removed first. The value is allowed to be null or empty, in which case
        /// the parameter will be stored as a key only (this is a rare case that some servers use for their query parameters).
        /// </summary>
        /// <param name="parameterName">The name of the parameter</param>
        /// <param name="value">The value to set for the parameter</param>
        void Set(string parameterName, string value);

        /// <summary>
        /// Converts this parameter collection to a new simple dictionary of entries.
        /// If any of the keys have multiple values, an exception will be thrown because the results
        /// cannot be contained in a flat dictionary.
        /// </summary>
        /// <returns>A newly instantiated dictionary containing the form parameters.
        /// The case sensitivity of the dictionary will also match this collection.</returns>
        IDictionary<string, string> ToSimpleDictionary();

        /// <summary>
        /// Gets an enumerable for all parameter values with the specified name.
        /// This is used to access multiple parameter values with the same key.
        /// Parameter lookups depend on the case sensitivity specified at collection creation, but is usually case-insensitive.
        /// </summary>
        /// <param name="parameterName"></param>
        /// <returns></returns>
        IReadOnlyCollection<string> GetAllParameterValues(string parameterName);
    }
}
