using System;
using System.Xml.Linq;

namespace Durandal.Common.Time.Timex.Resources
{
    /// <summary>
    /// Represents regular expression resource from grammar file
    /// </summary>
    public sealed class RegexResource : GrammarResource
    {
        /// <summary>
        /// String representation of a regular expression
        /// </summary>
        public string Expression { get; private set; }

        /// <summary>
        /// Converts the XElement representation of a regular expression to its RegexResource equivalent
        /// </summary>
        /// <param name="resource">XElement representation of a resource</param>
        /// <returns>true if element is successfully converted; otherwise, false</returns>
        public override void Parse(XElement resource)
        {
            if (resource == null)
                throw new ArgumentNullException("resource");

            base.Parse(resource);

            var expressionAttribute = resource.Attribute(GrammarElements.ExpressionAttribute);
            if (expressionAttribute == null)
                throw new TimexException(string.Format("Incorrect format. Regex {0}. Regex expression is not specified", Id));

            Expression = expressionAttribute.Value;
        }
    }
}
