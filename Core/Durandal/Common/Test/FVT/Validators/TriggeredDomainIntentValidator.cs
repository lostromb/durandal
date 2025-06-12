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
    /// Validator which ensures the SelectedRecoResult in a dialog response matches a given domain + intent
    /// </summary>
    public class TriggeredDomainIntentValidator : AbstractFunctionalTestValidator
    {
        private static readonly string VALIDATOR_NAME = "TriggeredDomainIntent";

        public override string Type => VALIDATOR_NAME;

        private readonly string _expectedDomain;
        private readonly string _expectedIntent;

        private TriggeredDomainIntentValidator(string expectedDomain = null, string expectedIntent = null)
        {
            _expectedDomain = expectedDomain;
            _expectedIntent = expectedIntent;
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

            if (response.DialogResponse.SelectedRecoResult == null)
            {
                return new ValidationResponse()
                {
                    ValidationPassed = false,
                    FailureReason = "Turn response has no selected reco result - this should not happen"
                };
            }

            if (!string.IsNullOrWhiteSpace(_expectedDomain) &&
                !string.Equals(_expectedDomain, response.DialogResponse.SelectedRecoResult.Domain, StringComparison.OrdinalIgnoreCase))
            {
                return new ValidationResponse()
                {
                    ValidationPassed = false,
                    FailureReason = "The triggered domain/intent \"" + response.DialogResponse.SelectedRecoResult.Domain + "/" + response.DialogResponse.SelectedRecoResult.Intent + "\" did not match the expected domain \"" + _expectedDomain + "\""
                };
            }

            if (!string.IsNullOrWhiteSpace(_expectedIntent) &&
                !string.Equals(_expectedIntent, response.DialogResponse.SelectedRecoResult.Intent, StringComparison.OrdinalIgnoreCase))
            {
                return new ValidationResponse()
                {
                    ValidationPassed = false,
                    FailureReason = "The triggered domain/intent \"" + response.DialogResponse.SelectedRecoResult.Domain + "/" + response.DialogResponse.SelectedRecoResult.Intent + "\" did not match the expected intent \"" + _expectedIntent + "\""
                };
            }

            return new ValidationResponse()
            {
                ValidationPassed = true
            };
        }

        public class Parser : IValidatorParser
        {
            public string SupportedValidator => VALIDATOR_NAME;

            public AbstractFunctionalTestValidator CreateFromJsonDefinition(JObject jsonValidatorDefinition, ValidatorFactory validatorFactory)
            {
                JsonRepresentation convertedObj = jsonValidatorDefinition.ToObject<JsonRepresentation>();
                return new TriggeredDomainIntentValidator(
                    expectedDomain: convertedObj.Domain,
                    expectedIntent: convertedObj.Intent);
            }

            private class JsonRepresentation
            {
                [JsonProperty("Domain")]
                public string Domain { get; set; }

                [JsonProperty("Intent")]
                public string Intent { get; set; }
            }
        }
    }
}
