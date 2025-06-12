using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Answers.SmartThingsAnswer
{
    public class CommandResult
    {
        public bool Success { get; set; }
        public string DataSent { get; set; }
        public string DataRecieved { get; set; }
        public string ErrorMessage { get; set; }
    }
}
