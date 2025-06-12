using Durandal.API;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Test.FVT
{
    public class AbstractFunctionalTestValidator
    {
        public virtual string Type { get; }

        public virtual ValidationResponse Validate(FunctionalTestTurnResult response)
        {
            throw new NotImplementedException("Cannot call Validate() on the abstract validator");
        }
    }
}
