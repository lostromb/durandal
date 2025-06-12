

namespace RazorCompile
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// An incredibly shoddy implementation of the Razor Web View compiler.
    /// There are certain C# syntax combinations which are currently unsupported. If in doubt, use a @ to escape.
    /// </summary>
    public class RazorTemplate
    {
        private List<string> _renderedLines = new List<string>();
        private List<string> _usingDeclarations = new List<string>();
        private List<VariableDeclaration> _variableDeclarations = new List<VariableDeclaration>();
        private readonly Regex XML_PARSER = new Regex("(<!.+?>)|(<[^ ]+?)((?: +.+?=\".+?\")* *\\/?>)([^<]*)");
        private readonly Regex LEADIN_PARSER = new Regex("^[^<]+");
        private string _viewName;
        private string _namespace = "RazorViews";

        private static readonly Regex SUBST_REGEX = new Regex("@(?:\\((.+?)\\)|([a-zA-Z0-9\\.()\\{\\}]+))");

        public RazorTemplate(string viewName)
        {
            _viewName = viewName;
            _renderedLines.Add("//// DO NOT MODIFY!!! THIS FILE IS AUTOGENED AND WILL BE OVERWRITTEN!!! ////");
            _renderedLines.Add(string.Empty);
            _renderedLines.Add("using System;");
            _renderedLines.Add("using System.Text;");
            _renderedLines.Add("using System.Collections;");
            _renderedLines.Add("using System.Collections.Generic;");
        }

        public IList<string> Render(IList<string> inputLines)
        {
            try
            {
                int endOfHeaders = this.ReadHeaders(inputLines);
                StringBuilder xmlBuilder = new StringBuilder();
                for (int line = endOfHeaders; line < inputLines.Count; line++)
                {
                    xmlBuilder.AppendLine(inputLines[line]);
                }

                // Write @using declarations
                _renderedLines.AddRange(_usingDeclarations);

                // Write the leadin
                _renderedLines.Add("namespace " + _namespace);
                _renderedLines.Add("{");
                _renderedLines.Add("    public class " + _viewName);
                _renderedLines.Add("    {");
            
                // Write @var declarations
                foreach (VariableDeclaration var in _variableDeclarations)
                {
                    _renderedLines.Add("        public " + var.Type + " " + var.Name + " {get; set;}");
                }

                // Write initializers in the constructor
                _renderedLines.Add("        public " + _viewName + "()");
                _renderedLines.Add("        {");
                foreach (VariableDeclaration var in _variableDeclarations)
                {
                    if (!string.IsNullOrEmpty(var.Initializer))
                    {
                        _renderedLines.Add("            " + var.Name + " = " + var.Initializer + ";");
                    }
                }
                _renderedLines.Add("        }");
                _renderedLines.Add("        public string Render()");
                _renderedLines.Add("        {");
                _renderedLines.Add("            StringBuilder returnVal = new StringBuilder();");

                // Process the HTML body
                ReadHtml(xmlBuilder.ToString(), endOfHeaders);

                // Write the leadout
                _renderedLines.Add("            return returnVal.ToString();");
                _renderedLines.Add("        }");
                _renderedLines.Add("    }");
                _renderedLines.Add("}");
            }
            catch (Exception e)
            {
                _renderedLines.Add(e.GetType().ToString());
                _renderedLines.Add(e.Message);
                _renderedLines.Add(e.Source);
                _renderedLines.Add(e.StackTrace);
            }
            return _renderedLines;
        }

        private int ReadHeaders(IList<string> inputLines)
        {
            int curLine = -1;
            foreach (string line in inputLines)
            {
                curLine++;
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                if (line.StartsWith("@"))
                {
                    // It's one of the header-only values like @model or @using
                    string namespaceDec = TryExtractNamespace(line);
                    if (namespaceDec != null)
                    {
                        _namespace = namespaceDec;
                        continue;
                    }

                    string usingDec = TryExtractUsing(line);
                    if (usingDec != null)
                    {
                        _usingDeclarations.Add("using " + usingDec + ";");
                        continue;
                    }

                    VariableDeclaration varDec = TryExtractVar(line);
                    if (varDec != null)
                    {
                        _variableDeclarations.Add(varDec);
                        continue;
                    }

                    break;
                }

                break;
            }
            return curLine;
        }

        private void ReadHtml(string htmlString, int lineNumberOffset)
        {
            // Read the lead-in, if any
            Match leadInMatch = LEADIN_PARSER.Match(htmlString);
            if (leadInMatch.Success)
            {
                IList<string> splitLines = SplitLines(leadInMatch.Value);
                foreach (string line in splitLines)
                {
                    string transformedLine = this.TransformLine(line);
                    if (transformedLine != null)
                    {
                        _renderedLines.Add(transformedLine);
                    }
                }
            }

            foreach (Match parserMatch in XML_PARSER.Matches(htmlString))
            {
                HandleRegexMatch(parserMatch);
            }
        }

        private void HandleRegexMatch(Match parserMatch)
        {
            // 1 is DOCTYPE tag
            // 2 is the tag name
            // 3 is the tag attributes with terminator
            // 4 is the body content
            if (parserMatch.Groups[1].Success)
            {
                // It's a !DOCTYPE tag. Pass it through
                _renderedLines.Add("            returnVal.AppendLine(\"" + EscapeCSharpString(parserMatch.Groups[1].Value) + "\");");
            }
            else
            {
                IList<string> splitLines = SplitLines(parserMatch.Groups[4].Value);
                bool isJavascriptBlock = parserMatch.Groups[2].Value.Equals("<script", StringComparison.OrdinalIgnoreCase);
                // Echo the current tag
                _renderedLines.Add("            returnVal.AppendLine(\"" + EscapeCSharpString(parserMatch.Groups[2].Value) + ApplySubstitutionInHtml(parserMatch.Groups[3].Value) + "\");");
                
                // And then handle whataver comes after it (its body text content, if any)
                foreach (string line in splitLines)
                {
                    string transformedLine = this.TransformLine(line, isJavascriptBlock);
                    if (transformedLine != null)
                    {
                        _renderedLines.Add(transformedLine);
                    }
                }
            }
        }

        private string TransformLine(string input, bool isJavascriptBlock = false)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;
            if (!isJavascriptBlock && IsCodeLine(input))
            {
                if (IsPureCode(input))
                {
                    // It's a code line like "@foreach (var x in Model)"
                    return "            " + ApplySubstitutionInCode(input.Trim());
                }
                else
                {
                    // Usually this means it's an HTML line that begins with code, like "@Model.Value<br/>", intended to be echoed
                    return "            returnVal.AppendLine(\"" + ApplySubstitutionInHtml(input.Trim()) + "\");";
                }
            }
            else if (isJavascriptBlock && IsPureCodeInsideJavascript(input))
            {
                // It's a code line inside of a javascript block (be careful!)
                return "            " + ApplySubstitutionInCode(input.Trim());
            }
            else
            {
                // It's a plain HTML (not Javascript) line, possibly containing "@" tokens that need substitution
                return "            returnVal.AppendLine(\"" + ApplySubstitutionInHtml(input) + "\");";
            }
        }

        private static IList<string> SplitLines(string input)
        {
            List<string> returnVal = new List<string>();
            Regex splitter = new Regex("(.+?)(\\r|\\n|$)+");
            foreach (Match m in splitter.Matches(input))
            {
                returnVal.Add(m.Groups[1].Value);
            }

            return returnVal;
        }

        private static bool IsCodeLine(string inputLine)
        {
            string trimmedLine = inputLine.TrimStart(' ', '\t');
            return trimmedLine.StartsWith("@") ||
                   trimmedLine.StartsWith("{") ||
                   trimmedLine.StartsWith("}") ||
                   trimmedLine.EndsWith(";");
        }

        /// <summary>
        /// Indicates that no string data can possibly be emitted by the current line
        /// </summary>
        /// <param name="inputLine"></param>
        /// <returns></returns>
        private static bool IsPureCode(string inputLine)
        {
            string trimmedLine = inputLine.ToLowerInvariant();
            if (trimmedLine.Contains("//"))
            {
                trimmedLine = trimmedLine.Substring(0, trimmedLine.IndexOf("//"));
            }
            trimmedLine = trimmedLine.Trim(' ', '\t');

            return trimmedLine.StartsWith("@foreach") ||
                   trimmedLine.StartsWith("@for") ||
                   trimmedLine.StartsWith("@while") ||
                   trimmedLine.StartsWith("@if") ||
                   trimmedLine.StartsWith("@else") ||
                   trimmedLine.StartsWith("@{") ||
                   trimmedLine.StartsWith("{") ||
                   trimmedLine.StartsWith("}") ||
                   trimmedLine.EndsWith(";");
        }

        private static bool IsPureCodeInsideJavascript(string inputLine)
        {
            string trimmedLine = inputLine.ToLowerInvariant();
            if (trimmedLine.Contains("//"))
            {
                trimmedLine = trimmedLine.Substring(0, trimmedLine.IndexOf("//"));
            }
            trimmedLine = trimmedLine.Trim(' ', '\t');

            return trimmedLine.StartsWith("@foreach") ||
                   trimmedLine.StartsWith("@for") ||
                   trimmedLine.StartsWith("@while") ||
                   trimmedLine.StartsWith("@if") ||
                   trimmedLine.StartsWith("@else") ||
                   trimmedLine.StartsWith("@{") ||
                   trimmedLine.StartsWith("@}");
        }

        private static VariableDeclaration TryExtractVar(string input)
        {
            if (input.Trim().ToLowerInvariant().StartsWith("@var"))
            {
                string line = input.Substring(4).Trim().TrimEnd(';');
                VariableDeclaration returnVal = new VariableDeclaration();
                int typeIndex = line.IndexOf(' ');
                int initializerIndex = line.IndexOf('=');
                if (typeIndex < 0)
                {
                    throw new FormatException("The variable declaration has no type: " + line);
                }
                returnVal.Type = line.Substring(0, typeIndex);
                if (initializerIndex > typeIndex)
                {
                    // Variable contains an initializer; save it for later
                    returnVal.Name = line.Substring(typeIndex + 1, initializerIndex - typeIndex - 1).Trim();
                    returnVal.Initializer = line.Substring(initializerIndex + 1).Trim();
                }
                else
                {
                    returnVal.Name = line.Substring(typeIndex + 1).Trim();
                    returnVal.Initializer = string.Empty;
                }
                return returnVal;
            }
            return null;
        }

        private static string TryExtractUsing(string input)
        {
            if (input.Trim().ToLowerInvariant().StartsWith("@using"))
            {
                return input.Substring(6).Trim();
            }
            return null;
        }

        private static string TryExtractNamespace(string input)
        {
            if (input.Trim().ToLowerInvariant().StartsWith("@namespace"))
            {
                return input.Substring(10).Trim();
            }
            return null;
        }

        private static string ApplySubstitutionInCode(string inputLine)
        {
            inputLine = EscapeCSharpString(inputLine);
            foreach (Match m in SUBST_REGEX.Matches(inputLine))
            {
                if (m.Groups[1].Success)
                {
                    inputLine = inputLine.Replace(m.Value, m.Groups[1].Value);
                }
                else
                {
                    inputLine = inputLine.Replace(m.Value, m.Groups[2].Value);
                }
            }
            return inputLine;
        }

        private static string ApplySubstitutionInHtml(string inputLine)
        {
            //inputLine = inputLine.Replace("\"", "\\\"");
            inputLine = EscapeCSharpString(inputLine);
            foreach (Match m in SUBST_REGEX.Matches(inputLine))
            {
                if (m.Groups[1].Success)
                {
                    inputLine = inputLine.Replace(m.Value, "\" + " + m.Groups[1].Value + " + \"");
                }
                else
                {
                    inputLine = inputLine.Replace(m.Value, "\" + " + m.Groups[2].Value + " + \"");
                }
            }
            return inputLine;
        }

        private static string EscapeCSharpString(string input)
        {
            return input.Replace("\\", "\\\\")
                        .Replace("\'", "\\\'")
                        .Replace("\"", "\\\"")
                        .Replace("\r", "\\\r")
                        .Replace("\n", "\\\n");
        }

        private class VariableDeclaration
        {
            public string Type;
            public string Name;
            public string Initializer;
        }
    }
}
