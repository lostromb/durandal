using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.API;
using System.Reflection;
using System.Security;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Durandal.Common.Logger;
using Durandal.Common.Tasks;
using Durandal.Common.MathExt;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Durandal.Common.IO;

namespace Durandal.Common.LG.Statistical
{
    public class RoslynLGScriptCompiler : ILGScriptCompiler
    {
        public IDictionary<string, LgCommon.RunLGScript> Compile(string templateName, IEnumerable<ScriptBlock> scripts, ILogger logger)
        {
            IDictionary<string, LgCommon.RunLGScript> returnVal = new Dictionary<string, LgCommon.RunLGScript>();

            Stopwatch compilerTimer = Stopwatch.StartNew();
            foreach (ScriptBlock script in scripts)
            {
                StringBuilder sourceBuilder = new StringBuilder();
                foreach (string codeLine in script.CodeLines)
                {
                    sourceBuilder.AppendLine(codeLine);
                }

                ScriptOptions opts = ScriptOptions.Default
                    .AddImports(new string[] { "System", "System.Text", "System.Collections.Generic" })
                    .AddReferences(typeof(LGScriptVariables).Assembly)
                    .WithFileEncoding(Encoding.Unicode);
                
                Script<object> compiledScript = CSharpScript.Create(
                    new StringBuilderReadStream(sourceBuilder, Encoding.Unicode),
                    opts,
                    typeof(LGScriptVariables));
                ScriptRunner<object> scriptDelegate = compiledScript.CreateDelegate();
                LgCommon.RunLGScript delMethod = CreateDelegate(scriptDelegate);
                returnVal[script.Name] = delMethod;
            }
            
            compilerTimer.Stop();
            logger.Log("Compiling LG scripts took " + compilerTimer.ElapsedMilliseconds + "ms");

            return returnVal;
        }

        private LgCommon.RunLGScript CreateDelegate(ScriptRunner<object> roslynScript)
        {
            return (substitutions, phraseName, locale, log) =>
            {
                LGScriptVariables vars = new LGScriptVariables(substitutions, phraseName, locale, log);
                roslynScript(vars).Await();
                return vars.PhraseName;
            };
        }

        public class LGScriptVariables
        {
            public LGScriptVariables(
                IDictionary<string, object> substitutions,
                string phraseName,
                string locale,
                Action<string> log)
            {
                Substitutions = substitutions;
                PhraseName = phraseName;
                Locale = locale;
                Log = log;
            }

            public IDictionary<string, object> Substitutions { get; }
            public string PhraseName { get; set; }
            public string Locale { get; }
            public Action<string> Log { get; }
        }
    }
}
