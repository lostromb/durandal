using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;

namespace Durandal.Common.NLP.Canonical
{
    /// <summary>
    /// Represents normalization resource from grammar file
    /// </summary>
    public sealed class NormalizationResource : Resource
    {
        private readonly IDictionary<string, string> _normalizedValues;
        private bool _strict = false;

        public NormalizationResource()
        {
            _normalizedValues = new Dictionary<string, string>();
        }

        /// <summary>
        /// Adds a pair value/normalized value to a resource
        /// </summary>
        /// <param name="value">Value to be normalized</param>
        /// <param name="normalizedValue">Normalized value</param>
        /// <returns></returns>
        public void AddValue(string value, string normalizedValue)
        {
            _normalizedValues[value.ToLowerInvariant()] = normalizedValue;
        }

        /// <summary>
        /// Returns normalized value for a value
        /// </summary>
        /// <param name="value">Value to be normalized</param>
        /// <param name="queryLogger"></param>
        /// <returns></returns>
        public string Normalize(string value, ILogger queryLogger)
        {
            string normalizedValue;
            if (_normalizedValues.TryGetValue(value.ToLowerInvariant(), out normalizedValue))
            {
                queryLogger.Log(string.Format("Phrase component \"{0}\" normalized by rule {1} into \"{2}\"", value, Id, normalizedValue), LogLevel.Vrb);
                return normalizedValue;
            }

            if (_strict)
            {
                queryLogger.Log(string.Format("Phrase component \"{0}\" was erased by strict normalization rule {1}", value, Id), LogLevel.Vrb);
                return string.Empty;
            }
            else
            {
                return value;
            }
        }

        /// <summary>
        /// Converts the XElement representation of a normalization rule to its NormalizationResource equivalent
        /// </summary>
        /// <param name="resource">XElement representation of a resource</param>
        /// <returns>true if element is successfully converted; otherwise, false</returns>
        public override void Parse(XElement resource)
        {
            if (resource == null)
                throw new ArgumentNullException("resource");

            base.Parse(resource);

            // parse "strict=true" if applicable
            if (resource.Attribute(GrammarElements.StrictAttribute) != null)
            {
                if (!bool.TryParse(resource.Attribute(GrammarElements.StrictAttribute).Value, out _strict))
                {
                    _strict = false;
                }
            }

            var items = resource.Elements(GrammarElements.Item);
            foreach (var item in items)
            {
                if (item.Attribute(GrammarElements.InputAttribute) == null)
                {
                    throw new FormatException(Id + " is missing at least one input attribute");
                }
                if (item.Attribute(GrammarElements.OutputAttribute) == null)
                {
                    throw new FormatException(Id + " is missing at least one output attribute");
                }
                var itemValue = item.Attribute(GrammarElements.InputAttribute).Value;
                var itemNormalizedValue = item.Attribute(GrammarElements.OutputAttribute).Value;

                AddValue(itemValue.Trim(), itemNormalizedValue.Trim());
            }
        }
    }
}
