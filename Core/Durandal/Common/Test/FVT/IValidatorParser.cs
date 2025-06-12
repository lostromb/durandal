using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Test.FVT
{
    public interface IValidatorParser
    {
        string SupportedValidator { get; }

        AbstractFunctionalTestValidator CreateFromJsonDefinition(
            JObject jsonValidatorDefinition,
            ValidatorFactory validatorFactory);
    }
}
