using System;
using System.Collections.Generic;
using System.Text;
using Durandal.API;
using Durandal.Common.Logger;

namespace Durandal.Common.LG.Statistical
{
    public interface ILGScriptCompiler
    {
        IDictionary<string, LgCommon.RunLGScript> Compile(string templateName, IEnumerable<ScriptBlock> scripts, ILogger logger);
    }
}
