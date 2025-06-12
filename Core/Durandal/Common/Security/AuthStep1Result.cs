using Durandal.Common.MathExt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Security
{
    /// <summary>
    /// 
    /// </summary>
    public class AuthStep1Result
    {
        public AuthStep1Result()
        {
            FirstTurnSuccess = false;
            SecondTurnRequired = false;
            ResponseToken = null;
            ErrorMessage = null;
        }

        /// <summary>
        /// If true, the server is now aware of the client
        /// </summary>
        public bool FirstTurnSuccess { get; set; }

        /// <summary>
        /// If true, the server has sent a challenge token which must be decoded before the client can get its shared secret.
        /// If false, the server assumes the client already knows the shared secret.
        /// </summary>
        public bool SecondTurnRequired { get; set; }

        public BigInteger ResponseToken { get; set; }
        public string ErrorMessage { get; set; }
    }
}
