using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Test.FVT
{
    public class ValidatorFactory
    {
        private readonly IDictionary<string, IValidatorParser> _parsers;

        public ValidatorFactory()
        {
            _parsers = new Dictionary<string, IValidatorParser>();
        }

        public void AddParser(IValidatorParser parser)
        {
            if (parser == null)
            {
                throw new ArgumentNullException(nameof(parser));
            }

            if (_parsers.ContainsKey(parser.SupportedValidator))
            {
                throw new ArgumentException("A parser for the type \"" + parser.SupportedValidator + "\" already exists");
            }

            _parsers[parser.SupportedValidator] = parser;
        }

        public AbstractFunctionalTestValidator BuildValidator(JObject toParse)
        {
            if (toParse == null)
            {
                throw new ArgumentNullException(nameof(toParse));
            }

            if (toParse["Type"] == null)
            {
                throw new ArgumentException("JObject does not have a \"type\" property; this can't be parsed as a validator");
            }

            string validatorName = toParse["Type"].Value<string>();

            IValidatorParser parser;
            if (!_parsers.TryGetValue(validatorName, out parser))
            {
                throw new NotSupportedException("No parser is loaded to handle the validator \"" + validatorName + "\"");
            }

            return parser.CreateFromJsonDefinition(toParse, this);
        }
    }
}
