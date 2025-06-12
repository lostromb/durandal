using System;
using System.Xml.Linq;

namespace Durandal.Common.Time.Timex.Resources
{
    /// <summary>
    /// Base class for different types of resources from grammar file (regex, normalization rule, rule, negative rule)
    /// </summary>
    public class GrammarResource
    {
        /// <summary>
        /// Resource Id
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Scope of the rule
        /// </summary>
        public ResourceScope Scope { get; set; }

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
                throw new TimexException("Incorrect format. Resource id is not specified");

            Id = idAttribute.Value;

            var scopeAttribute = resource.Attribute(GrammarElements.ScopeAttribute);
            if (scopeAttribute == null)
                throw new TimexException(string.Format("Incorrect format. Resource {0}. Resource scope is not specified", Id));

            ResourceScope scope;
            if (!Enum.TryParse(scopeAttribute.Value, true, out scope))
                throw new TimexException(string.Format("Incorrect format. Resource {0}. Resource scope is not valid", Id));
            
            Scope = scope;
        }
    }
}
