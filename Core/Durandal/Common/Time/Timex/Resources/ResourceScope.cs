using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Time.Timex.Resources
{
    public enum ResourceScope
    {
        /// <summary>
        /// Resource can only be referenced by other resources
        /// </summary>
        Private,

        /// <summary>
        /// Resource is directly used by parser and can also be referenced by other resources
        /// </summary>
        Public
    }
}
