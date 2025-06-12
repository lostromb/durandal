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
    /// Validator which ensures that the dialog response did not contain an error message
    /// </summary>
    public class NoErrorMessageValidator : AbstractFunctionalTestValidator
    {
        private static readonly string VALIDATOR_NAME = "NoErrorMessage";

        public override string Type => VALIDATOR_NAME;

        private NoErrorMessageValidator() { }

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

            if (!string.IsNullOrEmpty(response.DialogResponse.ErrorMessage))
            {
                return new ValidationResponse()
                {
                    ValidationPassed = false,
                    FailureReason = "The dialog response contained an error message: " + response.DialogResponse.ErrorMessage
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
                return new NoErrorMessageValidator();
            }
        }
    }
}
