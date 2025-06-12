using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Dialog
{
    public class DialogException : Exception
    {
        public DialogException() : base("An error occurred in the dialog engine") { }
        public DialogException(string errorMessage) : base(errorMessage) { }
        public DialogException(string errorMessage, Exception innerException) : base(errorMessage, innerException) { }
    }
}
