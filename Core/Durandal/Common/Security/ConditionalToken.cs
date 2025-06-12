using Durandal.Common.MathExt;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Security
{
    public class ConditionalToken
    {
        public bool Success;
        public BigInteger Token;

        public ConditionalToken(bool success, BigInteger token = null)
        {
            Success = success;
            Token = token;
        }
    }
}
