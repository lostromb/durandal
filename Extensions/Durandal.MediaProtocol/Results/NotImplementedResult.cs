using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.MediaProtocol
{
    public class NotImplementedResult : MediaResult
    {
        public override string Result
        {
            get
            {
                return "NotImplemented";
            }
        }
    }
}
