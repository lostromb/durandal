namespace RazorCompile
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Spark.Parser;
    using Spark.Compiler;
    using Spark;
    using Spark.FileSystem;
    using Spark.Compiler.CSharp;
    using System.Text.RegularExpressions;
    using System.IO;
    using Spark.Bindings;
    using Spark.Parser.Syntax;

    public class SparkTemplate
    {
        private List<string> _renderedLines = new List<string>();
        private List<string> _usingDeclarations = new List<string>();
        private List<VariableDeclaration> _variableDeclarations = new List<VariableDeclaration>();
        private string _viewName;
        private string _namespace = "RazorViews";

        public SparkTemplate(string viewName)
        {
            _viewName = viewName;
            _renderedLines.Add("//// DO NOT MODIFY!!! THIS FILE IS AUTOGENED AND WILL BE OVERWRITTEN!!! ////");
            _renderedLines.Add(string.Empty);
            _renderedLines.Add("using System;");
            _renderedLines.Add("using System.IO;");
            _renderedLines.Add("using System.Text;");
            _renderedLines.Add("using System.Collections;");
            _renderedLines.Add("using System.Collections.Generic;");
        }

        private IList<string> GetRawSourceCode(IList<string> inputLines, string viewName)
        {
            StringBuilder sourceFile = new StringBuilder();
            foreach (string line in inputLines)
            {
                sourceFile.AppendLine(line);
            }

            InMemoryViewFolder virtualViewFolder = new InMemoryViewFolder();
            virtualViewFolder.Add(viewName, sourceFile.ToString());
            
            SparkSettings settings = new SparkSettings();
            settings.SetPageBaseType(typeof(AbstractSparkView));
            SparkViewEngine engine = new SparkViewEngine(settings);
            engine.ViewFolder = virtualViewFolder;

            ViewLoader loader = new ViewLoader();
            loader.ViewFolder = engine.ViewFolder;
            loader.BindingProvider = engine.BindingProvider;
            loader.AttributeBehaviour = AttributeBehaviour.CodeOriented;
            loader.ExtensionFactory = engine.ExtensionFactory;
            loader.PartialProvider = engine.PartialProvider;
            loader.PartialReferenceProvider = engine.PartialReferenceProvider;
            loader.SyntaxProvider = engine.SyntaxProvider;
            loader.Load(viewName);
            var chunks = loader.GetEverythingLoaded();

            CSharpViewCompiler compiler = new CSharpViewCompiler();
            compiler.GenerateSourceCode(chunks, new List<IList<Chunk>>(chunks));

            
            /*
            SparkViewDescriptor descriptor = new SparkViewDescriptor().AddTemplate("dummy.spark");
            ISparkViewEntry compiledView = engine.CreateEntry(descriptor);*/

            return SplitLines(compiler.SourceCode);
        }

        public IList<string> Render(IList<string> inputLines, string viewName)
        {
            try
            {
                // Read the file's headers (namespace, variable declarations, imports...)
                int endOfHeaders = this.ReadHeaders(inputLines, viewName);
                _renderedLines.AddRange(_usingDeclarations);
                IList<string> compilerInput = new List<string>();
                for (int c = endOfHeaders; c < inputLines.Count; c++)
                {
                    compilerInput.Add(inputLines[c]);
                }

                // Write the leadin
                _renderedLines.Add("namespace " + _namespace);
                _renderedLines.Add("{");
                _renderedLines.Add("    public class " + _viewName);
                _renderedLines.Add("    {");

                _renderedLines.Add("        private StringWriter Output;");

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

                // Write the render function wrapper
                _renderedLines.Add("        }");
                _renderedLines.Add("        public string Render()");
                _renderedLines.Add("        {");
                _renderedLines.Add("            StringBuilder returnVal = new StringBuilder();");
                _renderedLines.Add("            Output = new StringWriter(returnVal);");
                _renderedLines.Add("            RenderViewLevel0();");
                _renderedLines.Add("            return returnVal.ToString();");
                _renderedLines.Add("        }");

                // Run the Spark compiler to generate the RenderViewLevel0() function
                IList<string> rawSourceCode = GetRawSourceCode(compilerInput, viewName);
                IList<string> renderDelegateFunction = ExtractRenderLevel0Function(rawSourceCode);
                foreach (string generatedCodeLine in renderDelegateFunction)
                {
                    _renderedLines.Add("    " + generatedCodeLine);
                }

                // Write the leadout
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

        private IList<string> ExtractRenderLevel0Function(IList<string> generatedSourceCode)
        {
            IList<string> returnVal = new List<string>();
            bool insideFunction = false;
            foreach (string line in generatedSourceCode)
            {
                if (!insideFunction && line.Contains("private void RenderViewLevel0()"))
                {
                    insideFunction = true;
                }

                if (insideFunction)
                {
                    returnVal.Add(line);
                }

                if (insideFunction && line.Equals("    }"))
                {
                    return returnVal;
                }
            }

            return returnVal;
        }

        private int ReadHeaders(IList<string> inputLines, string viewName)
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

                    VariableDeclaration varDec = TryExtractVar(line, curLine, viewName);
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

        private static VariableDeclaration TryExtractVar(string input, int lineNumber, string viewName)
        {
            if (input.Trim().ToLowerInvariant().StartsWith("@var"))
            {
                string line = input.Substring(4).Trim().TrimEnd(';');
                VariableDeclaration returnVal = new VariableDeclaration();
                int typeIndex = line.IndexOf(' ');
                int initializerIndex = line.IndexOf('=');
                if (typeIndex < 0)
                {
                    throw new FormatException("The variable declaration has no type: " + line + " (" + viewName + " line " + lineNumber + ")");
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

        private class VariableDeclaration
        {
            public string Type;
            public string Name;
            public string Initializer;
        }

        private class FakePartialReferenceProvider : IPartialReferenceProvider
        {
            public IEnumerable<string> GetPaths(string viewPath, bool allowCustomReferencePath)
            {
                return new List<string>();
            }
        }

        private class FakePartialProvider : IPartialProvider
        {
            public IEnumerable<string> GetPaths(string viewPath)
            {
                return new List<string>();
            }
        }

        private class FakeExtensionFactory : ISparkExtensionFactory
        {
            public ISparkExtension CreateExtension(Spark.Compiler.NodeVisitors.VisitorContext context, Spark.Parser.Markup.ElementNode node)
            {
                return null;
            }
        }
    }
}
