using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Durandal.Common.Time.Timex.Resources
{
    /// <summary>
    /// Represents normalization resource from grammar file
    /// </summary>
    public sealed class NormalizationResource : GrammarResource
    {
        private readonly IDictionary<string, string> _normalizedValues;
        private readonly bool _caseSensitive;

        public NormalizationResource(bool caseSensitive)
        {
            _normalizedValues = new Dictionary<string, string>();
            _caseSensitive = caseSensitive;
        }

        /// <summary>
        /// Adds a pair value/normalized value to a resource
        /// </summary>
        /// <param name="value">Value to be normalized</param>
        /// <param name="normalizedValue">Normalized value</param>
        /// <returns></returns>
        public void AddValue(string value, string normalizedValue)
        {
            if (_caseSensitive)
            {
                _normalizedValues[value] = normalizedValue;
            }
            else
            {
                _normalizedValues[value.ToLowerInvariant()] = normalizedValue;
            }
        }

        /// <summary>
        /// Returns normalized value for a value
        /// </summary>
        /// <param name="value">Value to be normalized</param>
        /// <returns></returns>
        public string Normalize(string value)
        {
            string normalizedValue;

            if (_caseSensitive)
            {
                if (_normalizedValues.TryGetValue(value, out normalizedValue))
                {
                    return normalizedValue;
                }
            }
            else
            {
                if (_normalizedValues.TryGetValue(value.ToLowerInvariant(), out normalizedValue))
                {
                    return normalizedValue;
                }
            }

            return value;
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

            var items = resource.Elements(GrammarElements.Item);
            foreach (var item in items)
            {
                // extract tag element with normalization value
                var itemTag = item.Element(GrammarElements.Tag);
                if (itemTag != null)
                    itemTag.Remove();

                var itemValue = item.Value;
                var itemNormalizedValue = itemTag != null ? itemTag.Value : item.Value;

                AddValue(itemValue.Trim(), itemNormalizedValue.Trim());
            }
        }
    }
}
