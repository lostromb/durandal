using Durandal.API;
using Durandal.Common.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Test.FVT.Validators
{
    /// <summary>
    /// Meta-validator which ensures that AT LEAST ONE sub-validator ran successfully.
    /// </summary>
    public class DisjunctionValidator : AbstractFunctionalTestValidator
    {
        private static readonly string VALIDATOR_NAME = "Any";

        public override string Type => VALIDATOR_NAME;

        private readonly List<AbstractFunctionalTestValidator> _validations;

        private DisjunctionValidator(List<AbstractFunctionalTestValidator> validations)
        {
            _validations = validations;
        }

        public override ValidationResponse Validate(FunctionalTestTurnResult response)
        {
            List<ValidationResponse> individualResponses = new List<ValidationResponse>();
            foreach (var validator in _validations)
            {
                ValidationResponse individualResult = validator.Validate(response);
                if (individualResult.ValidationPassed)
                {
                    return individualResult;
                }
                else
                {
                    individualResponses.Add(individualResult);
                }
            }

            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                StringBuilder failureMessageBuilder = pooledSb.Builder;
                failureMessageBuilder.AppendLine("None of the \"" + VALIDATOR_NAME + "\" validation clauses passed successfully.");
                foreach (var resp in individualResponses)
                {
                    failureMessageBuilder.AppendLine(resp.FailureReason);
                }

                return new ValidationResponse()
                {
                    ValidationPassed = false,
                    FailureReason = failureMessageBuilder.ToString()
                };
            }
        }

        public class Parser : IValidatorParser
        {
            public string SupportedValidator => VALIDATOR_NAME;

            public AbstractFunctionalTestValidator CreateFromJsonDefinition(JObject jsonValidatorDefinition, ValidatorFactory validatorFactory)
            {
                JsonRepresentation convertedObj = jsonValidatorDefinition.ToObject<JsonRepresentation>();
                List<AbstractFunctionalTestValidator> decodedValidations = new List<AbstractFunctionalTestValidator>();
                foreach (JObject polymorphicValidation in convertedObj.PolymorphicValidations)
                {
                    decodedValidations.Add(validatorFactory.BuildValidator(polymorphicValidation));
                }

                return new DisjunctionValidator(decodedValidations);
            }

            private class JsonRepresentation
            {
                [JsonProperty("Validations")]
                public List<JObject> PolymorphicValidations { get; set; }
            }
        }
    }
}
