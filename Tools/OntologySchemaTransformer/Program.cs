using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OntologySchemaTransformer.MetaSchemas;
using OntologySchemaTransformer.MSO;
using OntologySchemaTransformer.SchemaDotOrg;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace OntologySchemaTransformer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Converts ontology .json files into CSharp code");
                Console.WriteLine("    -compile");
                Console.WriteLine("              -in (directory containing json schema files)");
                Console.WriteLine("              -out (directory to put output code files)");
                Console.WriteLine("              -namespace (C# namespace for the generated code)");
                Console.WriteLine("              -visibility (C# visibility e.g. public, internal)");
                Console.WriteLine("              -select (List of type IDs you are interested in converting - all others will be stubbed as generic references)");
                Console.WriteLine("    -jsonld");
                Console.WriteLine("              -in (Path to the jsonld schema file)");
                Console.WriteLine("              -out (directory to put output schema files)");
                Console.WriteLine("    -rdf");
                Console.WriteLine("              -in (directory containing rdf xml files)");
                Console.WriteLine("              -out (directory to put output schema files)");
                return;
            }

            IDictionary<string, string[]> commandLineParams = ParseCommandLineOptions(args);

            // Find which mode we are running in
            if (commandLineParams.ContainsKey("compile"))
            {
                if (!commandLineParams.ContainsKey("in"))
                {
                    Console.WriteLine("Missing -in parameter");
                    return;
                }
                if (!commandLineParams.ContainsKey("out"))
                {
                    Console.WriteLine("Missing -out parameter");
                    return;
                }

                DirectoryInfo schemaDirectory = new DirectoryInfo(commandLineParams["in"][0]);
                DirectoryInfo outputDirectory = new DirectoryInfo(commandLineParams["out"][0]);
                Console.WriteLine("Generating C# code...");
                string targetNamespace = "Ontology";
                string classVisibility = "public";
                if (commandLineParams.ContainsKey("namespace"))
                {
                    targetNamespace = commandLineParams["namespace"][0];
                }
                else
                {
                    Console.WriteLine("No -namespace specified, using \"Ontology\" as default");
                }

                if (commandLineParams.ContainsKey("visibility"))
                {
                    classVisibility = commandLineParams["visibility"][0].ToLowerInvariant();
                }

                ISet<string> selectiveTypes = null;
                if (commandLineParams.ContainsKey("select"))
                {
                    selectiveTypes = new HashSet<string>(commandLineParams["select"]);
                }

                ConvertSchemasToCSharp(schemaDirectory.EnumerateFiles("*.json"), outputDirectory, targetNamespace, classVisibility, selectiveTypes);
            }
            else if (commandLineParams.ContainsKey("jsonld"))
            {
                if (!commandLineParams.ContainsKey("in"))
                {
                    Console.WriteLine("Missing -in parameter");
                    return;
                }
                if (!commandLineParams.ContainsKey("out"))
                {
                    Console.WriteLine("Missing -out parameter");
                    return;
                }

                FileInfo inputFile = new FileInfo(commandLineParams["in"][0]);
                DirectoryInfo outDirectory = new DirectoryInfo(commandLineParams["out"][0]);
                CompleteOntology parsedFile = JsonLdParser.ParseJsonLd(inputFile.FullName);
                Console.WriteLine("Found " + parsedFile.Classes.Count + " classes. Convering to JSON...");
                ConvertCompleteOntologyToIndividualSchemas(parsedFile, outDirectory);
                Console.WriteLine("Done");
            }
            else if (commandLineParams.ContainsKey("rdf"))
            {
                if (!commandLineParams.ContainsKey("in"))
                {
                    Console.WriteLine("Missing -in parameter");
                    return;
                }
                if (!commandLineParams.ContainsKey("out"))
                {
                    Console.WriteLine("Missing -out parameter");
                    return;
                }

                DirectoryInfo inputDirectory = new DirectoryInfo(commandLineParams["in"][0]);
                DirectoryInfo outDirectory = new DirectoryInfo(commandLineParams["out"][0]);
                CompleteOntology parsedFile = MSOSchemaParser.ParseMSOSchemas(inputDirectory);
                Console.WriteLine("Found " + parsedFile.Classes.Count + " classes. Convering to JSON...");
                ConvertCompleteOntologyToIndividualSchemas(parsedFile, outDirectory);
                Console.WriteLine("Done");
            }
            else
            {
                Console.WriteLine("Unknown mode");
            }
        }
        
        private static void ConvertSchemasToCSharp(IEnumerable<FileInfo> schemaFiles, DirectoryInfo outputDirectory, string targetNamespace, string classVisibility, ISet<string> selectiveTypes = null)
        {
            List<OntologyEnumeration> enumerations = new List<OntologyEnumeration>();
            List<OntologyClass> classes = new List<OntologyClass>();

            foreach (FileInfo schemaFile in schemaFiles)
            {
                string fileContents = File.ReadAllText(schemaFile.FullName);
                JObject rawJson = JObject.Parse(fileContents);
                Console.WriteLine("Parsing " + schemaFile.Name);
                // Does it have a type? Then it is an instance (an enumerated constant)
                if (rawJson["Type"] != null)
                {
                    OntologyEnumeration e = rawJson.ToObject<OntologyEnumeration>();
                    enumerations.Add(e);
                }
                else
                {
                    OntologyClass c = rawJson.ToObject<OntologyClass>();
                    classes.Add(c);
                }
            }

            InMemoryClassResolver dependencyResolver = new InMemoryClassResolver();
            foreach (OntologyClass c in classes)
            {
                dependencyResolver.Add(c);
            }

            // Filter the list of classes to the root set, if specified
            IDictionary<string, OntologyClass> filterSet = new Dictionary<string, OntologyClass>();

            if (selectiveTypes == null)
            {
                foreach (OntologyClass c in classes)
                {
                    filterSet[c.Id] = c;
                }
            }
            else
            {
                foreach (string selectiveType in selectiveTypes)
                {
                    Console.WriteLine("Filtering schemas related to root entity " + selectiveType);
                    RecurseTouchedTypes(filterSet, selectiveType, dependencyResolver, new HashSet<string>());
                    Console.WriteLine("Now there are " + filterSet.Count + " schemas in the filter set");
                }
            }

            dependencyResolver = new InMemoryClassResolver();
            foreach (OntologyClass c in filterSet.Values)
            {
                dependencyResolver.Add(c);
            }

            foreach (OntologyEnumeration e in enumerations)
            {
                if (!filterSet.ContainsKey(e.Type))
                {
                    continue;
                }

                string schemaName = SanitizeFileName(e.Id);
                string fileName = Path.Combine(outputDirectory.FullName, schemaName + ".cs");
                Console.WriteLine("Generating code for " + schemaName);
                using (FileStream writeStream = new FileStream(fileName, FileMode.Create, FileAccess.Write))
                {
                    using (TextWriter writer = new StreamWriter(writeStream))
                    {
                        CSharpCodeGenerator.GenerateCode(writer, e, targetNamespace, classVisibility);
                    }
                }
            }

            foreach (OntologyClass c in filterSet.Values)
            {
                string schemaName = SanitizeFileName(c.Id);
                string fileName = Path.Combine(outputDirectory.FullName, schemaName + ".cs");
                Console.WriteLine("Generating code for " + schemaName);
                using (FileStream writeStream = new FileStream(fileName, FileMode.Create, FileAccess.Write))
                {
                    using (TextWriter writer = new StreamWriter(writeStream))
                    {
                        CSharpCodeGenerator.GenerateCode(writer, c, targetNamespace, classVisibility, dependencyResolver);
                    }
                }
            }
        }

        private static void RecurseTouchedTypes(IDictionary<string, OntologyClass> target, string rootName, IClassResolver resolver, HashSet<string> touchedTypes)
        {
            if (touchedTypes.Contains(rootName))
            {
                return;
            }
            touchedTypes.Add(rootName);

            OntologyClass root = resolver.GetClass(rootName);
            if (root == null)
            {
                return;
            }

            if (!target.ContainsKey(root.Id))
            {
                target[root.Id] = root;
            }

            foreach (string inheritance in root.InheritsFrom)
            {
                RecurseTouchedTypes(target, inheritance, resolver, touchedTypes);
            }

            //foreach (var field in root.Fields.Values)
            //{
            //    foreach (var primitive in field.Values)
            //    {
            //        if (primitive.Type == PrimitiveType.Identifier)
            //        {
            //            RecurseTouchedTypes(target, primitive.ReferencedId, resolver, touchedTypes);
            //        }
            //    }
            //}
        }

        private static void ConvertCompleteOntologyToIndividualSchemas(CompleteOntology parsedFile, DirectoryInfo outputDirectory)
        {
            JsonSerializerSettings serializerSettings = new JsonSerializerSettings()
            {
                Formatting = Newtonsoft.Json.Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
                Converters = new List<JsonConverter>()
                {
                    new PrimitiveTypeJsonConverter()
                }
            };

            // Write each class as a json file
            foreach (OntologyClass o in parsedFile.Classes.Values)
            {
                string outputFileName = SanitizeFileName(o.Id) + ".json";
                FileInfo outputFile = new FileInfo(Path.Combine(outputDirectory.FullName, outputFileName));
                Console.WriteLine("Writing " + outputFile.Name);
                File.WriteAllText(outputFile.FullName, JsonConvert.SerializeObject(o, serializerSettings), Encoding.UTF8);
            }

            // And enumerations too
            foreach (OntologyEnumeration o in parsedFile.Enumerations.Values)
            {
                string outputFileName = SanitizeFileName(o.Id) + ".json";
                FileInfo outputFile = new FileInfo(Path.Combine(outputDirectory.FullName, outputFileName));
                Console.WriteLine("Writing " + outputFile.Name);
                File.WriteAllText(outputFile.FullName, JsonConvert.SerializeObject(o, serializerSettings), Encoding.UTF8);
            }
        }

        private static IDictionary<string, string[]> ParseCommandLineOptions(string[] args)
        {
            IDictionary<string, string[]> returnVal = new Dictionary<string, string[]>();

            string currentParamName = null;
            List<string> currentParamGroup = new List<string>();
            for (int idx = 0; idx < args.Length; idx++)
            {
                string currentArg = args[idx];
                if (string.IsNullOrEmpty(currentArg))
                {
                    continue;
                }

                if (currentArg.Equals("-"))
                {
                    continue;
                }

                if (currentArg.StartsWith("-"))
                {
                    if (currentParamName != null)
                    {
                        returnVal.Add(currentParamName, currentParamGroup.ToArray());
                    }

                    currentParamName = currentArg.TrimStart('-');
                    currentParamGroup.Clear();
                }
                else
                {
                    currentParamGroup.Add(currentArg);
                }
            }

            if (currentParamName != null)
            {
                returnVal.Add(currentParamName, currentParamGroup.ToArray());
            }

            return returnVal;
        }

        private static string SanitizeFileName(string fileName)
        {
            fileName = fileName.Replace("http://schema.org/", "");
            fileName = fileName.Replace("http://knowledge.microsoft.com/mso/", "");

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }

            return fileName;
        }
    }
}
