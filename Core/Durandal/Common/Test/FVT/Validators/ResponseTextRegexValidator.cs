using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Durandal.API;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Durandal.Common.Test.FVT.Validators
{
    /// <summary>
    /// Validator which ensures that the response text content of a dialog response contains at least one match of a specified regex
    /// </summary>
    public class ResponseTextRegexValidator : AbstractFunctionalTestValidator
    {
        private static readonly string VALIDATOR_NAME = "ResponseTextRegex";

        public override string Type => VALIDATOR_NAME;

        private readonly Regex _compiledRegex;
        private readonly string _plainTextRegex;

        private ResponseTextRegexValidator(string plaintextRegex)
        {
            _plainTextRegex = plaintextRegex;
            _compiledRegex = new Regex(plaintextRegex);
        }

        public override ValidationResponse Validate(FunctionalTestTurnResult response)
        {
            if (response == null)
            {
                return new ValidationResponse()
                {
                    ValidationPassed = false,
                    FailureReason = "Turn result is null"
                };
            }

            if (response.DialogResponse == null)
            {
                return new ValidationResponse()
                {
                    ValidationPassed = false,
                    FailureReason = "Turn response does not contain a dialog result - this could be because the input was not a regular query"
                };
            }

            if (response.DialogResponse.ResponseHtml == null)
            {
                return new ValidationResponse()
                {
                    ValidationPassed = false,
                    FailureReason = "Turn response has null response text - this should never happen"
                };
            }

            Match m = _compiledRegex.Match(response.DialogResponse.ResponseText);
            if (m.Success)
            {
                return new ValidationResponse()
                {
                    ValidationPassed = true
                };
            }
            else
            {
                return new ValidationResponse()
                {
                    ValidationPassed = false,
                    FailureReason = "Response text \"" + response.DialogResponse.ResponseText + "\" did not match the validation regex \"" + _plainTextRegex + "\""
                };
            }
        }

        public class Parser : IValidatorParser
        {
            public string SupportedValidator => VALIDATOR_NAME;

            public AbstractFunctionalTestValidator CreateFromJsonDefinition(JObject jsonValidatorDefinition, ValidatorFactory validatorFactory)
            {
                JsonRepresentation convertedObj = jsonValidatorDefinition.ToObject<JsonRepresentation>();
                return new ResponseTextRegexValidator(convertedObj.PlaintextRegex);
            }

            private class JsonRepresentation
            {
                [JsonProperty("Regex")]
                public string PlaintextRegex { get; set; }
            }
        }
    }
}
