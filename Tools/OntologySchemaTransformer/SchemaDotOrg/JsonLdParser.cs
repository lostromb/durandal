using OntologySchemaTransformer.MetaSchemas;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OntologySchemaTransformer.SchemaDotOrg
{
    public static class JsonLdParser
    {
        public static CompleteOntology ParseJsonLd(string fileName)
        {
            string jsonSchema = File.ReadAllText(fileName);
            SchemaFile parsedFile = JsonConvert.DeserializeObject<SchemaFile>(jsonSchema);

            Dictionary<string, OntologyClass> classes = new Dictionary<string, OntologyClass>();
            Dictionary<string, OntologyEnumeration> enumerations = new Dictionary<string, OntologyEnumeration>();

            foreach (GraphItem item in parsedFile.Graph)
            {
                if (item.Type != null)
                {
                    foreach (string type in item.Type)
                    {
                        if (string.Equals(type, "rdfs:Class"))
                        {
                            OntologyClass newClass = new OntologyClass();
                            newClass.Id = item.Id;
                            newClass.Comment = item.Comment;
                            newClass.Label = item.Label;
                            newClass.Fields = new Dictionary<string, OntologyField>();
                            if (item.SubclassOf == null)
                            {
                                newClass.InheritsFrom = new HashSet<string>();
                            }
                            else
                            {
                                newClass.InheritsFrom = new HashSet<string>(item.SubclassOf.Select((a) => a.Id));
                            }

                            classes.Add(newClass.Id, newClass);
                            break;
                        }
                    }
                }
            }

            // Process the attributes of each class
            foreach (GraphItem item in parsedFile.Graph)
            {
                if (item.Type != null)
                {
                    foreach (string type in item.Type)
                    {
                        if (string.Equals(type, "rdf:Property") &&
                            item.RangeIncludes != null &&
                            item.RangeIncludes.Count > 0 &&
                            item.DomainIncludes != null)
                        {
                            foreach (SchemaItem domainProperty in item.DomainIncludes)
                            {
                                // Find the entity that this references
                                OntologyClass targetClass;
                                if (classes.TryGetValue(domainProperty.Id, out targetClass))
                                {
                                    // Find out the type of this primitive
                                    // Todo: what if it is an enumerated value?
                                    foreach (SchemaItem rangeProperty in item.RangeIncludes)
                                    {
                                        PrimitiveType primType = ParsePrimitiveType(rangeProperty.Id);
                                        if (primType != PrimitiveType.Unknown)
                                        {
                                            string fieldName = ShortenFieldId(item.Id);

                                            if (!targetClass.Fields.ContainsKey(fieldName))
                                            {
                                                OntologyField newField = new OntologyField(fieldName);
                                                targetClass.Fields.Add(fieldName, newField);
                                            }
                                            else
                                            {
                                                Console.WriteLine("Duplicate field " + item.Id + " found in class " + targetClass.Id);
                                            }

                                            string referenceId = rangeProperty.Id;
                                            if (primType != PrimitiveType.Identifier)
                                            {
                                                referenceId = null;
                                            }

                                            targetClass.Fields[fieldName].AddPrimitive(primType, referenceId, item.Comment);
                                        }
                                    }
                                }
                            }
                        }
                        else if (string.Equals(type, "rdfs:Class"))
                        {
                            //Console.WriteLine("This is a class?: " + type + " : " + item.Id);
                        }
                        else if (item.Id.Contains("SchemaDotOrgSources"))
                        {
                            // These are just for attribution and are ignored
                        }
                        else
                        {
                            // If this is an enumerated value the type will be the ID of the thing being enumerated
                            OntologyEnumeration newEnum = new OntologyEnumeration();
                            newEnum.Id = item.Id;
                            newEnum.Comment = item.Comment;
                            newEnum.Label = item.Label;
                            newEnum.Type = type;
                            enumerations.Add(newEnum.Id, newEnum);
                        }
                    }
                }
            }

            return new CompleteOntology()
            {
                Classes = classes,
                Enumerations = enumerations
            };
        }

        private static PrimitiveType ParsePrimitiveType(string rawType)
        {
            PrimitiveType primType = PrimitiveType.Unknown;
            if (string.Equals("http://schema.org/Text", rawType))
            {
                primType = PrimitiveType.Text;
            }
            else if (string.Equals("http://schema.org/Boolean", rawType))
            {
                primType = PrimitiveType.Boolean;
            }
            else if (string.Equals("http://schema.org/Date", rawType))
            {
                primType = PrimitiveType.Date;
            }
            else if (string.Equals("http://schema.org/DateTime", rawType))
            {
                primType = PrimitiveType.DateTime;
            }
            else if (string.Equals("http://schema.org/Time", rawType))
            {
                primType = PrimitiveType.Time;
            }
            else if (string.Equals("http://schema.org/Number", rawType))
            {
                primType = PrimitiveType.Number;
            }
            else
            {
                primType = PrimitiveType.Identifier;
            }

            return primType;
        }

        private static string ShortenFieldId(string id)
        {
            return id.Replace("http://schema.org/", "");
        }
    }
}
