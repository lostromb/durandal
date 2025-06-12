using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Durandal.Common.Time.Timex.Actions
{
    /// <summary>
    /// The method signature for methods that are provided by this class's compiled assemblies.
    /// Particular methods can be retrieved using GetTagAction(methodname)
    /// </summary>
    /// <param name="timex"></param>
    /// <param name="value"></param>
    public delegate void TagAction(IDictionary<string, string> timex, string value);
}
