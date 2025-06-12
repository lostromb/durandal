using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Durandal.Common.Time.Timex.Actions
{
    using Durandal.Common.Time.Timex.Resources;

    public interface IActionProvider
    {
        void AppendMethod(string ruleId, string tagKey, string script);

        void Compile(IDictionary<string, NormalizationResource> normalizationResources);

        TagAction GetMethod(string ruleId, string tagKey);
    }
}
