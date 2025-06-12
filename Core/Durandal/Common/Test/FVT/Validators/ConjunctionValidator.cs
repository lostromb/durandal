using Durandal.API;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Test.FVT.Validators
{
    /// <summary>
    /// Meta-validator which ensures that ALL sub-validators ran successfully
    /// </summary>
    public class ConjunctionValidator : AbstractFunctionalTestValidator
    {
        private static readonly string VALIDATOR_NAME = "All";

        public override string Type => VALIDATOR_NAME;

        private readonly List<AbstractFunctionalTestValidator> _validations;

        private ConjunctionValidator(List<AbstractFunctionalTestValidator> validations)
        {
            _validations = validations;
        }

        public override ValidationResponse Validate(FunctionalTestTurnResult response)
        {
            foreach (var validator in _validations)
            {
                ValidationResponse individualResult = validator.Validate(response);
                if (!individualResult.ValidationPassed)
                {
                    return individualResult;
                }
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
                return CreateFromValidatorList(convertedObj.PolymorphicValidations, validatorFactory);
            }

            public AbstractFunctionalTestValidator CreateFromValidatorList(List<JObject> validatorList, ValidatorFactory validatorFactory)
            {
                List<AbstractFunctionalTestValidator> decodedValidations = new List<AbstractFunctionalTestValidator>();
                foreach (JObject polymorphicValidation in validatorList)
                {
                    decodedValidations.Add(validatorFactory.BuildValidator(polymorphicValidation));
                }

                return new ConjunctionValidator(decodedValidations);
            }

            private class JsonRepresentation
            {
                [JsonProperty("Validations")]
                public List<JObject> PolymorphicValidations { get; set; }
            }
        }
    }
}
