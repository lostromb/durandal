using System;
using System.Xml.Linq;

namespace Durandal.Common.NLP.Canonical
{
    /// <summary>
    /// Base class for different types of resources from grammar file (regex, normalization rule, rule, negative rule)
    /// </summary>
    public class Resource
    {
        /// <summary>
        /// Resource Id
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Converts the XElement representation of a resource to its Resource equivalent
        /// </summary>
        /// <param name="resource">XElement representation of a resource</param>
        /// <returns>true if element is successfully converted; otherwise, false</returns>
        public virtual void Parse(XElement resource)
        {
            if (resource == null)
                throw new ArgumentNullException("resource");

            var idAttribute = resource.Attribute(GrammarElements.IdAttribute);
            if (idAttribute == null)
                throw new FormatException("Incorrect format. Resource id is not specified");

            Id = idAttribute.Value;
        }
    }
}
