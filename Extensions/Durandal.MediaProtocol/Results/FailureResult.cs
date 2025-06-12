using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.MediaProtocol
{
    public class FailureResult : MediaResult
    {
        public override string Result
        {
            get
            {
                return "Failure";
            }
        }

        public string ErrorMessage { get; set; }

        public FailureResult()
        {
        }

        public FailureResult(string errorMessage)
        {
            ErrorMessage = errorMessage;
        }
    }
}
