using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.MediaProtocol
{
    /// <summary>
    /// Indicates that the command was received but it was either not executed, or its execution had no effect
    /// </summary>
    public class NotExecutedResult : MediaResult
    {
        public override string Result
        {
            get
            {
                return "NotExecuted";
            }
        }

        public string Message { get; set; }

        public NotExecutedResult()
        {
        }

        public NotExecutedResult(string message)
        {
            Message = message;
        }
    }
}
