using Durandal.Common.Ontology;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace OntologySchemaTransformer.MSO
{
    public static class MSOSchemaParser
    {
        public static CompleteOntology ParseMSOSchemas(DirectoryInfo rdfFileDirectory)
        {
            List<MSODomainDefinition> domainDefs = new List<MSODomainDefinition>();
            List<MSOTypeDefinition> types = new List<MSOTypeDefinition>();
            List<MSOEnumDefinition> enums = new List<MSOEnumDefinition>();
            List<MSOPropertyDefinition> props = new List<MSOPropertyDefinition>();

            foreach (FileInfo file in rdfFileDirectory.EnumerateFiles("*.xml"))
            {
                Console.WriteLine("Parsing " + file.Name);
                XmlDocument document = new XmlDocument();
                document.Load(file.FullName);

                foreach (var child in document.ChildNodes)
                {
                    XmlElement elem = child as XmlElement;
                    if (elem != null)
                    {
                        if (string.Equals(elem.Name, "rdf:RDF", StringComparison.OrdinalIgnoreCase))
                        {
                            foreach (var description in elem.ChildNodes)
                            {
                                XmlElement descriptionElem = description as XmlElement;
                                if (descriptionElem != null &&
                                    string.Equals(descriptionElem.Name, "rdf:Description", StringComparison.OrdinalIgnoreCase))
                                {
                                    ParseMsoDescriptionBlock(descriptionElem, domainDefs, types, enums, props);
                                }
                            }
                        }
                    }
                }
            }

            Dictionary<string, OntologyClass> classes = new Dictionary<string, OntologyClass>();
            Dictionary<string, OntologyEnumeration> enumerations = new Dictionary<string, OntologyEnumeration>();

            foreach (var msoClass in types)
            {
                OntologyClass newClass = new OntologyClass()
                {
                    Id = msoClass.Id,
                    Label = msoClass.Name,
                    Comment = msoClass.Description,
                    Fields = new Dictionary<string, OntologyField>(),
                    InheritsFrom = new HashSet<string>(msoClass.Includes)
                };
                
                classes[msoClass.Id] = newClass;
            }

            foreach (var msoProperty in props)
            {
                string subject = msoProperty.PropertyType;
                string target = msoProperty.ExpectedType;
                string fieldShortId = TrimNamespaceFromMsoId(msoProperty.Id);

                if (!classes.ContainsKey(subject))
                {
                    Console.WriteLine("Cannot find subject " + subject + " of property " + msoProperty.Id);
                    continue;
                }

                OntologyClass subjectClass = classes[subject];
                if (!subjectClass.Fields.ContainsKey(fieldShortId))
                {
                    // Create new field if necessary
                    subjectClass.Fields[fieldShortId] = new OntologyField(fieldShortId);
                }

                // Add the primitive value of this property to the field
                PrimitiveType primType = PrimitiveType.Unknown;
                string referenceId = null;
                if (string.Equals(target, "http://knowledge.microsoft.com/mso/type.decimal"))
                {
                    primType = PrimitiveType.Number;
                }
                else if (string.Equals(target, "http://knowledge.microsoft.com/mso/type.boolean"))
                {
                    primType = PrimitiveType.Boolean;
                }
                else if (string.Equals(target, "http://knowledge.microsoft.com/mso/type.string") ||
                    string.Equals(target, "http://knowledge.microsoft.com/mso/type.text") ||
                    string.Equals(target, "http://knowledge.microsoft.com/mso/type.uri"))
                { 
                    primType = PrimitiveType.Text;
                }
                else if (string.Equals(target, "http://knowledge.microsoft.com/mso/type.datetime"))
                {
                    primType = PrimitiveType.DateTime;
                }
                else if (string.Equals(target, "http://knowledge.microsoft.com/mso/type.unit"))
                {
                    primType = PrimitiveType.Unknown; // ????
                }
                else if (string.Equals(target, "http://knowledge.microsoft.com/mso/type.type") ||
                    string.Equals(target, "http://knowledge.microsoft.com/mso/type.property") ||
                    string.Equals(target, "http://knowledge.microsoft.com/mso/type.key") ||
                    string.Equals(target, "http://knowledge.microsoft.com/mso/type.triple") ||
                    string.Equals(target, "http://knowledge.microsoft.com/mso/type.domain_group") ||
                    string.Equals(target, "http://knowledge.microsoft.com/mso/type.domain") ||
                    string.Equals(target, "http://knowledge.microsoft.com/mso/type.category") ||
                    string.Equals(target, "http://knowledge.microsoft.com/mso/type.namespace"))
                {
                    primType = PrimitiveType.Unknown;
                }
                else if (string.Equals(target, "http://knowledge.microsoft.com/mso/type.object"))
                {
                    // This appears to be used for fields expecting an enumerated object reference
                    primType = PrimitiveType.Identifier;
                    referenceId = target;
                }
                else if (target.StartsWith("http://knowledge.microsoft.com/mso/type."))
                {
                    primType = PrimitiveType.Unknown;
                }
                else
                {
                    primType = PrimitiveType.Identifier;
                    referenceId = target;
                }

                if (primType != PrimitiveType.Unknown)
                {
                    subjectClass.Fields[fieldShortId].AddPrimitive(primType, referenceId, msoProperty.Description);
                }
            }

            foreach (var enumeration in enums)
            {
                if (enumeration.IsDeprecated)
                {
                    continue;
                }

                OntologyEnumeration newEnum = new OntologyEnumeration()
                {
                    Id = enumeration.Id,
                    Label = enumeration.Name,
                    Comment = enumeration.Description,
                    Type = enumeration.EnumType
                };

                enumerations.Add(enumeration.Id, newEnum);
            }

            return new CompleteOntology()
            {
                Classes = classes,
                Enumerations = enumerations
            };
        }

        private static void ParseMsoDescriptionBlock(XmlElement elem, List<MSODomainDefinition> domainDefs, List<MSOTypeDefinition> types, List<MSOEnumDefinition> enums, List<MSOPropertyDefinition> props)
        {
            if (!elem.HasAttribute("rdf:about"))
            {
                return;
            }

            string aboutId = elem.GetAttribute("rdf:about");
            string domainGroupName = null;
            string objectName = null;
            HashSet<string> objectTypes = new HashSet<string>();
            string objectDescription = null;
            string typeCategory = null;
            List<string> typeDomain = new List<string>();
            List<string> typeIncludes = new List<string>();
            string propertyExpectedType = null;
            string propertyType = null;
            bool deprecationInfoExists = false;

            foreach (var child in elem.ChildNodes)
            {
                XmlElement childElement = child as XmlElement;
                if (childElement == null)
                {
                    continue;
                }

                string rdfResource = null;
                string xmlContent = null;
                if (childElement.HasAttribute("rdf:resource"))
                {
                    rdfResource = childElement.GetAttribute("rdf:resource");
                }
                if (!string.IsNullOrEmpty(childElement.InnerText))
                {
                    xmlContent = childElement.InnerText;
                }


                if (string.Equals("mso:type.domain.domain_group", childElement.Name, StringComparison.OrdinalIgnoreCase))
                {
                    // <mso:type.domain.domain_group rdf:resource="http://knowledge.microsoft.com/domain_group/business" />
                    domainGroupName = rdfResource;
                }
                else if (string.Equals("mso:type.object.name", childElement.Name, StringComparison.OrdinalIgnoreCase))
                {
                    // <mso:type.object.name xml:lang="en">Advertising Slogan</mso:type.object.name>
                    if (!childElement.HasAttribute("xml:lang") || string.Equals("en", childElement.GetAttribute("xml:lang")))
                    {
                        objectName = xmlContent;
                    }
                }
                else if (string.Equals("mso:type.object.type", childElement.Name, StringComparison.OrdinalIgnoreCase))
                {
                    // <mso:type.object.type rdf:resource="http://knowledge.microsoft.com/mso/type.property" />
                    objectTypes.Add(rdfResource);
                }
                else if (string.Equals("mso:type.object.description", childElement.Name, StringComparison.OrdinalIgnoreCase))
                {
                    // <mso:type.object.description xml:lang="en">Virtual property relating an mso:business.advertising_slogan to its mso:commerce.brand_slogan.</mso:type.object.description>
                    if (!childElement.HasAttribute("xml:lang") || string.Equals("en", childElement.GetAttribute("xml:lang")))
                    {
                        objectDescription = xmlContent;
                    }
                }
                else if (string.Equals("mso:type.type.category", childElement.Name, StringComparison.OrdinalIgnoreCase))
                {
                    // <mso:type.type.category rdf:resource="http://knowledge.microsoft.com/mso/type.category.value_type" /> SINGULAR
                    typeCategory = rdfResource;
                }
                else if (string.Equals("mso:type.type.domain", childElement.Name, StringComparison.OrdinalIgnoreCase))
                {
                    // <mso:type.type.domain rdf:resource="http://knowledge.microsoft.com/mso/business" /> SINGULAR
                    typeDomain.Add(rdfResource);
                }
                else if (string.Equals("mso:type.type.includes", childElement.Name, StringComparison.OrdinalIgnoreCase))
                {
                    // <mso:type.type.includes rdf:resource="http://knowledge.microsoft.com/mso/time.event" /> PLURAL
                    typeIncludes.Add(rdfResource);
                }
                else if (string.Equals("mso:type.property.expected_type", childElement.Name, StringComparison.OrdinalIgnoreCase))
                {
                    // <mso:type.property.expected_type rdf:resource="http://knowledge.microsoft.com/mso/organization.organization" /> SINGULAR
                    propertyExpectedType = rdfResource;
                }
                else if (string.Equals("mso:type.property.min_cardinality", childElement.Name, StringComparison.OrdinalIgnoreCase))
                {
                    // <mso:type.property.min_cardinality rdf:datatype="http://www.w3.org/2001/XMLSchema#int">1</mso:type.property.min_cardinality>
                }
                else if (string.Equals("mso:type.property.max_cardinality", childElement.Name, StringComparison.OrdinalIgnoreCase))
                {
                    // <mso:type.property.max_cardinality rdf:datatype="http://www.w3.org/2001/XMLSchema#int">1</mso:type.property.max_cardinality>
                }
                else if (string.Equals("mso:type.property.type", childElement.Name, StringComparison.OrdinalIgnoreCase))
                {
                    // <mso:type.property.type rdf:resource="http://knowledge.microsoft.com/mso/business.acquisition" /> SINGULAR
                    propertyType = rdfResource;
                }
                else if (string.Equals("mso:type.property.inverse_of", childElement.Name, StringComparison.OrdinalIgnoreCase))
                {
                    // <mso:type.property.inverse_of rdf:resource="mso:commerce.brand_slogan.advertising_slogan" />
                }
                else if (string.Equals("mso:type.type.deprecation_info", childElement.Name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals("mso:type.property.deprecation_info", childElement.Name, StringComparison.OrdinalIgnoreCase))
                {
                    // <mso:type.type.deprecation_info xml:lang="en">There is no need to reify slogans. Successor:[mso:commerce.brand.brand_slogan]</mso:type.type.deprecation_info>
                    deprecationInfoExists = true;
                }
                else if (string.Equals("mso:type.object.alias", childElement.Name, StringComparison.OrdinalIgnoreCase))
                {
                    // <mso:type.object.alias xml:lang="en">Video Arcade</mso:type.object.alias>
                }
                else if (string.Equals("mso:type.property.unit", childElement.Name, StringComparison.OrdinalIgnoreCase))
                {
                    // <mso:type.property.unit rdf:resource="http://knowledge.microsoft.com/mso/type.unit.kilobit" />
                }
                else if (string.Equals("mso:type.property.subproperty", childElement.Name, StringComparison.OrdinalIgnoreCase))
                {
                    // ????
                }
                else if (string.Equals("type.property.required", childElement.Name, StringComparison.OrdinalIgnoreCase))
                {
                    // ????
                }
                else
                {
                    Console.WriteLine("Unknown property name " + childElement.Name);
                }
            }

            // Now we have extracted all we need to try and figure out what type of thing this is
            if (objectTypes.Contains("http://knowledge.microsoft.com/mso/type.domain"))
            {
                MSODomainDefinition def = new MSODomainDefinition()
                {
                    Id = aboutId,
                    Name = objectName,
                    DomainGroup = domainGroupName
                };

                domainDefs.Add(def);
            }
            else if (objectTypes.Contains("http://knowledge.microsoft.com/mso/type.type"))
            {
                MSOTypeDefinition type = new MSOTypeDefinition()
                {
                    Id = aboutId,
                    Name = objectName,
                    Category = typeCategory,
                    Description = objectDescription,
                    Domains = typeDomain,
                    Includes = typeIncludes,
                    IsDeprecated = deprecationInfoExists
                };

                types.Add(type);
            }
            else if (objectTypes.Contains("http://knowledge.microsoft.com/mso/type.property"))
            {
                MSOPropertyDefinition prop = new MSOPropertyDefinition()
                {
                    Id = aboutId,
                    Name = objectName,
                    Description = objectDescription,
                    PropertyType = propertyType,
                    ExpectedType = propertyExpectedType,
                    IsDeprecated = deprecationInfoExists,
                };

                props.Add(prop);
            }
            else
            {
                MSOEnumDefinition e = new MSOEnumDefinition()
                {
                    Id = aboutId,
                    Name = objectName,
                    Description = objectDescription,
                    IsDeprecated = deprecationInfoExists,
                    EnumType = objectTypes.First()
                };

                enums.Add(e);
            }
        }

        private static string TrimNamespaceFromMsoId(string id)
        {
            return id.Replace("http://knowledge.microsoft.com/mso/", "");
        }
    }
}
