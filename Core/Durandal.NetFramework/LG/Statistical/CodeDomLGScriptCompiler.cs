using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using Durandal.Common.MathExt;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Durandal.Common.LG.Statistical
{
    public class CodeDomLGScriptCompiler : ILGScriptCompiler
    {
        private readonly IRandom _rand = new FastRandom();

        public IDictionary<string, LgCommon.RunLGScript> Compile(string templateName, IEnumerable<ScriptBlock> scripts, ILogger logger)
        {
            IDictionary<string, LgCommon.RunLGScript> returnVal = new Dictionary<string, LgCommon.RunLGScript>();
            IRandom random = new FastRandom();
            int numScripts = 0;

            StringBuilder sourceBuilder = new StringBuilder();
            string generatedClassName = StringUtils.RegexReplace(new Regex("[^a-zA-Z0-9_]"), templateName, "_");
            generatedClassName = generatedClassName + "_" + _rand.NextInt().ToString();

            IDictionary<int, string> scriptToLineMapping = new Dictionary<int, string>();
            sourceBuilder.AppendLine("using System;");
            sourceBuilder.AppendLine("using System.Collections.Generic;");
            sourceBuilder.AppendLine("using System.Text;");
            sourceBuilder.AppendLine("");
            sourceBuilder.AppendLine("namespace CompiledLGScripts.Codegen");
            sourceBuilder.AppendLine("{");
            sourceBuilder.AppendLine("    public static class " + generatedClassName);
            sourceBuilder.AppendLine("    {");
            int curLine = 9;
            foreach (ScriptBlock script in scripts)
            {
                numScripts++;
                sourceBuilder.AppendLine("        public static string RunLGScript_" + script.Name + "(");
                sourceBuilder.AppendLine("            IDictionary <string, object> Substitutions,");
                sourceBuilder.AppendLine("            string PhraseName,");
                sourceBuilder.AppendLine("            string Locale,");
                sourceBuilder.AppendLine("            Action<string> Log)");
                int numberOfBrackets = random.NextInt(1, 5);
                sourceBuilder.Append("        ");
                sourceBuilder.Append('{', numberOfBrackets);
                sourceBuilder.AppendLine();
                curLine += 5;
                scriptToLineMapping[curLine] = script.Name;
                foreach (string codeLine in script.CodeLines)
                {
                    string modifiedCodeLine = codeLine.Replace("return;", "return PhraseName;");
                    sourceBuilder.AppendLine(modifiedCodeLine);
                    curLine += 1;
                }

                sourceBuilder.AppendLine("        return PhraseName;");
                sourceBuilder.Append("        ");
                sourceBuilder.Append('}', numberOfBrackets);
                sourceBuilder.AppendLine();
                curLine += 2;
            }
            sourceBuilder.AppendLine("    }");
            sourceBuilder.AppendLine("}");
            
            if (numScripts == 0)
            {
                return returnVal;
            }

            // Compile the assembly and then reflect into it to pull out our compiled script methods
            Stopwatch compilerTimer = Stopwatch.StartNew();
            Assembly compiledSource = CompileAssembly(sourceBuilder.ToString(), logger, templateName, scriptToLineMapping);
            Type compiledType = compiledSource.GetType("CompiledLGScripts.Codegen." + generatedClassName);

            // Make sure that the script did not inject any extra methods which would suggest something malicious is happening
            if (numScripts != compiledType.GetMethods().Length - 4) // subtract 4 to account for Equals, Hashcode, ToString, and GetType
            {
                throw new FormatException("LG scripts did not generate 1:1 methods; aborting script compilation");
            }

            foreach (ScriptBlock script in scripts)
            {
                MethodInfo method = compiledType.GetMethod("RunLGScript_" + script.Name);
                LgCommon.RunLGScript delMethod = Delegate.CreateDelegate(
                    typeof(LgCommon.RunLGScript),
                    method,
                    false) as LgCommon.RunLGScript;
                returnVal[script.Name] = delMethod;
            }
            compilerTimer.Stop();
            logger.Log("Compiling LG scripts took " + compilerTimer.ElapsedMilliseconds + "ms");

            return returnVal;
        }

        /// <summary>
        /// Compiles C# source code to in memory assembly
        /// </summary>
        /// <param name="source">Source code to compile</param>
        /// <param name="logger">A logger for the operation</param>
        /// <param name="templateName">The name of the template being compiled (used for debug reporting)</param>
        /// <param name="scriptToLineMapping">A mapping of line number -> script name, used when unwinding errors that may happen during compilation</param>
        /// <returns>Compiled assembly</returns>
        [SecurityCritical]
        private static Assembly CompileAssembly(string source, ILogger logger, string templateName, IDictionary<int, string> scriptToLineMapping)
        {
            logger.Log("Compiling C# scripts for LG template " + templateName);
            //Console.Write(source);
            
            using (var provider = CodeDomProvider.CreateProvider("CSharp"))
            {
                var parameters = new CompilerParameters
                {
                    GenerateInMemory = true,
                    GenerateExecutable = false,
                    ReferencedAssemblies = { "System.dll" },
                };

                var results = provider.CompileAssemblyFromSource(parameters, source);
                if (results.Errors.HasErrors ||
                    results.Errors.HasWarnings)
                {
                    // If there were compiler errors, tell the user exactly what happened and where to find it
                    string[] lines = source.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    StringBuilder errorMessage = new StringBuilder();
                    errorMessage.AppendLine("An error occurred while loading the LG template \"" + templateName + "\".");
                    int numLinesToReadBack = 5;
                    foreach (CompilerError error in results.Errors)
                    {
                        int scriptStartLine = 0;
                        string scriptName = "Unknown script";
                        foreach (int line in scriptToLineMapping.Keys)
                        {
                            if (line > scriptStartLine && line <= error.Line)
                            {
                                scriptStartLine = line;
                                scriptName = scriptToLineMapping[line];
                            }
                        }
                        int relativeLineNum = error.Line - scriptStartLine + 1;
                        errorMessage.AppendLine("Script name: " + scriptName + " | Line: " + relativeLineNum + " | Message: " + error.ErrorText);
                        for (int line = Math.Max(scriptStartLine, error.Line - numLinesToReadBack); line <= error.Line; line++)
                        {
                            if (line >= 0 && line < lines.Length)
                            {
                                errorMessage.AppendLine(lines[line]);
                            }
                        }
                    }

                    throw new Exception(errorMessage.ToString());
                }

                return results.CompiledAssembly;
            }
        }
    }
}
