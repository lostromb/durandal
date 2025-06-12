using Durandal.Common.Utils;
using OntologySchemaTransformer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OntologySchemaTransformer
{
    public static class CSharpCodeGenerator
    {
        private static readonly Regex _fieldNameSanitizer = new Regex("[^\\w\\$\\<\\>]", RegexOptions.Compiled);
        private static readonly Regex _variableNameSanitizer = new Regex("[^\\w_\\.]", RegexOptions.Compiled);
        private static readonly Regex _typeNameWithNamespaceSanitizer = new Regex("[^\\.\\w\\$\\<\\>]", RegexOptions.Compiled);
        private static readonly Regex _reservedWordDetector = new Regex("^(?:private|public|class|params|internal|event|sealed|const|readonly|static|namespace|goto|if|else|while|for|switch|new|override|void)\\W?$", RegexOptions.Compiled);
        private static readonly Regex _reservedWordWithPrimitivesDetector = new Regex("^(?:private|public|class|params|internal|event|sealed|const|readonly|static|namespace|goto|if|else|while|for|switch|new|override|void|int|short|long|float|double|byte|sbyte|ushort|uint|ulong|bool|string|object)\\W?$", RegexOptions.Compiled);
        
        public static void GenerateCode(TextWriter writer, OntologyClass toGenerate, string targetNamespace, string classVisibility, IClassResolver dependencyResolver = null)
        {
            HashSet<string> allParentClasses = GetAllParentsRecursive(toGenerate, dependencyResolver);
            List<OntologyField> allFields = GenerateInheritedFieldsRecursive(toGenerate, dependencyResolver, null);
            List<CSharpFieldDefinition> convertedFields = new List<CSharpFieldDefinition>();

            string codeId = ConvertTypeIdToCodeId(toGenerate.Id);
            string safeTypeName = ConvertClassIdToCodeId(codeId);

            foreach (OntologyField field in allFields)
            {
                convertedFields.AddRange(ConvertToCSharpFields(field, safeTypeName, targetNamespace, dependencyResolver));
            }

            writer.WriteLine("using System;");
            writer.WriteLine("using System.Collections.Generic;");
            writer.WriteLine("using System.Linq;");
            writer.WriteLine("using System.Text;");
            writer.WriteLine("using System.Threading.Tasks;");
            writer.WriteLine("using Durandal.Common.Ontology;");
            writer.WriteLine("");
            writer.WriteLine("namespace " + targetNamespace);
            writer.WriteLine("{");
            writer.WriteLine("    /// <summary>");
            writer.WriteLine("    /// <para>" + EscapeXml(toGenerate.Label) + "</para>");
            if (!string.IsNullOrEmpty(toGenerate.Comment))
            {
                foreach (string line in toGenerate.Comment.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    writer.WriteLine("    /// <para>" + EscapeXml(line) + "</para>");
                }
            }
            writer.WriteLine("    /// </summary>");
            writer.WriteLine("    " + classVisibility + " class " + safeTypeName + " : Entity");
            writer.WriteLine("    {");
            if (allParentClasses.Count == 0)
            {
                writer.WriteLine("        private static HashSet<string> _inheritance = new HashSet<string>();");
            }
            else
            {
                writer.WriteLine("        private static HashSet<string> _inheritance = new HashSet<string>() { \"" + string.Join("\", \"", allParentClasses) + "\" };");
            }
            writer.WriteLine("");
            writer.WriteLine("        public " + safeTypeName + "(KnowledgeContext context, string entityId = null) : base(context, \"" + toGenerate.Id + "\", entityId) { Initialize(); }");
            writer.WriteLine("");
            writer.WriteLine("        /// <summary>");
            writer.WriteLine("        /// Casting constructor");
            writer.WriteLine("        /// </summary>");
            writer.WriteLine("        /// <param name=\"castFrom\">The entity that this one is being cast from</param>");
            writer.WriteLine("        public " + safeTypeName + "(Entity castFrom) : base(castFrom, \"" + toGenerate.Id + "\") { Initialize(); }");
            writer.WriteLine("");
            writer.WriteLine("        protected override ISet<string> InheritsFromInternal");
            writer.WriteLine("        {");
            writer.WriteLine("            get");
            writer.WriteLine("            {");
            writer.WriteLine("                return _inheritance;");
            writer.WriteLine("            }");
            writer.WriteLine("        }");
            writer.WriteLine("");
            writer.WriteLine("        private void Initialize()");
            writer.WriteLine("        {");
            foreach (CSharpFieldDefinition field in convertedFields)
            {
                writer.WriteLine("            " + field.ShortFieldName + " = new " + field.FieldType + "(_context, EntityId, \"" + field.GraphAttributeName + "\");");
            }
            writer.WriteLine("        }");
            writer.WriteLine("");

            foreach (CSharpFieldDefinition field in convertedFields)
            {
                writer.WriteLine("        /// <summary>");
                if (!string.IsNullOrEmpty(field.InheritedFrom))
                {
                    writer.WriteLine("        /// <para>(From " + EscapeXml(field.InheritedFrom) + ")</para>");
                }
                if (!string.IsNullOrEmpty(field.Comment))
                {
                    foreach (string line in field.Comment.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        writer.WriteLine("        /// <para>" + EscapeXml(line) + "</para>");
                    }
                }
                writer.WriteLine("        /// </summary>");
                writer.WriteLine("        public " + field.FieldType + " " + field.ShortFieldName + " { get; private set; }");
            }

            writer.WriteLine("    }");
            writer.WriteLine("}");
        }

        public static void GenerateCode(TextWriter writer, OntologyEnumeration toGenerate, string targetNamespace, string classVisibility, IClassResolver dependencyResolver = null)
        {
            string codeId = ConvertTypeIdToCodeId(toGenerate.Id);
            string safeTypeName = ConvertClassIdToCodeId(codeId);

            // Find out the type name of the thing we are trying to implement, so we can construct it

            string fullyQualifiedTypeName = targetNamespace + "." + safeTypeName;

            string parentTypeName = targetNamespace + "." + ConvertClassIdToCodeId(ConvertTypeIdToCodeId(toGenerate.Type));

            writer.WriteLine("using System;");
            writer.WriteLine("using System.Collections.Generic;");
            writer.WriteLine("using System.Linq;");
            writer.WriteLine("using System.Text;");
            writer.WriteLine("using System.Threading.Tasks;");
            writer.WriteLine("using Durandal.Common.Ontology;");
            writer.WriteLine("");
            writer.WriteLine("namespace " + targetNamespace);
            writer.WriteLine("{");
            writer.WriteLine("    /// <summary>");
            writer.WriteLine("    /// <para>" + EscapeXml(toGenerate.Label) + "</para>");
            if (!string.IsNullOrEmpty(toGenerate.Comment))
            {
                foreach (string line in toGenerate.Comment.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    writer.WriteLine("    /// <para>" + line + "</para>");
                }
            }
            writer.WriteLine("    /// </summary>");
            writer.WriteLine("    " + classVisibility + " class " + safeTypeName + " : " + parentTypeName);
            writer.WriteLine("    {");
            writer.WriteLine("        public " + safeTypeName + "(KnowledgeContext context, string entityId = null) : base(context, \"enum://" + toGenerate.Id + "\") { }");
            writer.WriteLine("    }");
            writer.WriteLine("}");
        }

        private static List<OntologyField> GenerateInheritedFieldsRecursive(OntologyClass toGenerate, IClassResolver classResolver, HashSet<string> fieldsWritten = null)
        {
            if (fieldsWritten == null)
            {
                fieldsWritten = new HashSet<string>();
            }

            List<OntologyField> returnVal = new List<OntologyField>();

            if (toGenerate.Fields != null)
            {
                foreach (OntologyField field in toGenerate.Fields.Values)
                {
                    if (!fieldsWritten.Contains(field.Name))
                    {
                        foreach (OntologyFieldPrimitive prim in field.Values)
                        {
                            prim.InheritedFrom = toGenerate.Id;
                        }

                        returnVal.Add(ReplaceEntityFieldWithPrimitiveIfPossible(field, classResolver));
                        fieldsWritten.Add(field.Name);
                    }
                }
            }

            if (classResolver != null && toGenerate.InheritsFrom != null)
            {
                foreach (string parentClassId in toGenerate.InheritsFrom)
                {
                    OntologyClass parentClass = classResolver.GetClass(parentClassId);
                    if (parentClass != null)
                    {
                        returnVal.AddRange(GenerateInheritedFieldsRecursive(parentClass, classResolver, fieldsWritten));
                    }
                    else
                    {
                        // If we can't resolve the base type then have the field refer to an anonymous entity
                        Console.WriteLine("Unresolved entity reference " + parentClassId);
                    }
                }
            }

            return returnVal;
        }

        private static OntologyField ReplaceEntityFieldWithPrimitiveIfPossible(OntologyField input, IClassResolver classResolver)
        {
            foreach (OntologyFieldPrimitive prim in input.Values)
            {
                HashSet<PrimitiveType> topLevelTypes = new HashSet<PrimitiveType>();
                if (prim.Type == PrimitiveType.Identifier)
                {
                    HashSet<string> allTopLevelParentTypes = FindTopLevelParentsRecursive(prim.ReferencedId, classResolver);
                    // If the top level parent type map only contains one primitive, replace this field with that primitive
                    if (allTopLevelParentTypes.Count == 1)
                    {
                        // This should probably be implemented better using some kind of entity -> fixed primitive map
                        string baseType = allTopLevelParentTypes.Single();
                        if (string.Equals(baseType, "http://schema.org/Text"))
                        {
                            prim.BasePrimitiveType = PrimitiveType.Text;
                        }
                        else if (string.Equals(baseType, "http://schema.org/Number"))
                        {
                            prim.BasePrimitiveType = PrimitiveType.Number;
                        }
                    }
                }
            }

            return input;
        }

        private static HashSet<string> FindTopLevelParentsRecursive(string currentTypeId, IClassResolver classResolver, HashSet<string> parents = null)
        {
            if (parents == null)
            {
                parents = new HashSet<string>();
            }

            OntologyClass currentType = classResolver.GetClass(currentTypeId);
            if (currentType == null)
            {
                if (!parents.Contains(currentTypeId))
                {
                    parents.Add(currentTypeId);
                }
            }
            else if (currentType.InheritsFrom == null || currentType.InheritsFrom.Count == 0)
            {
                if (!parents.Contains(currentTypeId))
                {
                    parents.Add(currentTypeId);
                }
            }
            else
            {
                foreach (string parentType in currentType.InheritsFrom)
                {
                    parents = FindTopLevelParentsRecursive(parentType, classResolver, parents);
                }
            }

            return parents;
        }

        private static HashSet<string> GetAllParentsRecursive(OntologyClass schema, IClassResolver classResolver)
        {
            HashSet<string> returnVal = new HashSet<string>();

            if (schema.InheritsFrom != null)
            {
                returnVal.UnionWith(schema.InheritsFrom);

                if (classResolver != null)
                {
                    foreach (string parentClassId in schema.InheritsFrom)
                    {
                        OntologyClass parentClass = classResolver.GetClass(parentClassId);
                        if (parentClass != null)
                        {
                            returnVal.UnionWith(GetAllParentsRecursive(parentClass, classResolver));
                        }
                        else
                        {
                            // If we can't resolve the parent type then have the field refer to an anonymous entity
                            Console.WriteLine("Unresolved inheritance reference " + parentClassId);
                        }
                    }
                }
            }

            return returnVal;
        }

        private class CSharpFieldDefinition
        {
            public string ShortFieldName { get; set; }
            public string FullyQualifiedFieldName { get; set; }
            public string FieldType { get; set; }
            public string GraphAttributeName { get; set; }
            public string Comment { get; set; }
            public string InheritedFrom { get; set; }
        }

        private static List<CSharpFieldDefinition> ConvertToCSharpFields(OntologyField field, string className, string targetNamespace, IClassResolver classResolver)
        {
            string safeFieldName = RemoveNonVariableNameChars(GetShortReferenceId(ConvertTypeIdToCodeId(field.Name)));
            string fieldNamespace = targetNamespace + "." + field.Name;
            string safeFieldNameUpper = char.ToUpperInvariant(safeFieldName[0]) + safeFieldName.Substring(1);
            // Detect clashes where the field name is the name of the enclosing class
            if (string.Equals(safeFieldNameUpper, className))
            {
                safeFieldNameUpper = safeFieldNameUpper + "_";
            }
            
            HashSet<PrimitiveType> coveredTypes = new HashSet<PrimitiveType>();
            List<OntologyFieldPrimitive> filteredPrimitives = new List<OntologyFieldPrimitive>();
            foreach (OntologyFieldPrimitive f in field.Values)
            {
                if (f.Type == PrimitiveType.Identifier)
                {
                    filteredPrimitives.Add(f);
                }

                // This is used to prevent creating a union type of two of the same type of primitive
                // One common case of this is a field that can be either Date or DateTime - collapse those into one
                // Or there could be a field that is Text or URL and both of those boil down to Text
                if (f.Type != PrimitiveType.Identifier &&
                    !coveredTypes.Contains(f.Type))
                {
                    filteredPrimitives.Add(f);
                    coveredTypes.Add(f.Type);

                    if (f.Type == PrimitiveType.Date)
                    {
                        coveredTypes.Add(PrimitiveType.Time);
                        coveredTypes.Add(PrimitiveType.DateTime);
                    }
                    else if (f.Type == PrimitiveType.Time)
                    {
                        coveredTypes.Add(PrimitiveType.Date);
                        coveredTypes.Add(PrimitiveType.DateTime);
                    }
                    else if (f.Type == PrimitiveType.DateTime)
                    {
                        coveredTypes.Add(PrimitiveType.Date);
                        coveredTypes.Add(PrimitiveType.Time);
                    }
                }
            }

            bool isUniontype = filteredPrimitives.Count > 1;

            List<CSharpFieldDefinition> returnVal = new List<CSharpFieldDefinition>();

            foreach (OntologyFieldPrimitive thisField in filteredPrimitives)
            {
                string fieldType = "null";
                string shortFieldType = "null";
                switch (thisField.Type)
                {
                    case PrimitiveType.Boolean:
                        fieldType = "bool";
                        shortFieldType = fieldType;
                        break;
                    case PrimitiveType.Text:
                        fieldType = "string";
                        shortFieldType = fieldType;
                        break;
                    case PrimitiveType.Number:
                        fieldType = "number";
                        shortFieldType = fieldType;
                        break;
                    case PrimitiveType.DateTime:
                    case PrimitiveType.Date:
                    case PrimitiveType.Time:
                        fieldType = "time";
                        shortFieldType = fieldType;
                        break;
                    case PrimitiveType.Identifier:
                        // If this field is a reference to another entity type, can we resolve that type right now?
                        OntologyClass resolvedReferenceType = classResolver.GetClass(thisField.ReferencedId);
                        fieldType = ConvertClassIdToCodeId(ConvertTypeIdToCodeId(thisField.ReferencedId));
                        shortFieldType = RemoveNonVariableNameChars(GetShortReferenceId(fieldType));

                        if (resolvedReferenceType == null)
                        {
                            // If not, then we have to stub this property using a generic entity reference on the assumption
                            // that the code generator won't ever actually compile the type for this
                            fieldType = "Entity";
                            // Keep ShortFieldType as-is because the actual name of the property should reflect the expected type, even if it's stubbed
                        }

                        break;
                }

                string unionTypeSuffix = string.Empty;
                // Give a unique name for each union field value
                if (isUniontype)
                {
                    unionTypeSuffix = "_as_" + shortFieldType;
                }

                CSharpFieldDefinition singleDefinition = new CSharpFieldDefinition();
                singleDefinition.ShortFieldName = safeFieldNameUpper + unionTypeSuffix;
                singleDefinition.FullyQualifiedFieldName = fieldNamespace + safeFieldNameUpper + unionTypeSuffix;
                singleDefinition.GraphAttributeName = safeFieldName;
                singleDefinition.Comment = thisField.Comment;
                singleDefinition.InheritedFrom = thisField.InheritedFrom;

                switch (thisField.BasePrimitiveType)
                {
                    case PrimitiveType.Identifier:
                        singleDefinition.FieldType = "IdentifierValue<" + fieldType + ">";
                        break;
                    case PrimitiveType.DateTime:
                    case PrimitiveType.Date:
                    case PrimitiveType.Time:
                        singleDefinition.FieldType = "TimeValue";
                        break;
                    case PrimitiveType.Boolean:
                        singleDefinition.FieldType = "BooleanValue";
                        break;
                    case PrimitiveType.Number:
                        singleDefinition.FieldType = "NumberValue";
                        break;
                    case PrimitiveType.Text:
                        singleDefinition.FieldType = "TextValue";
                        break;
                    default:
                        singleDefinition.FieldType = "TextValue";
                        break;
                }

                returnVal.Add(singleDefinition);
            }

            return returnVal;
        }

        private static string RemoveNonVariableNameChars(string typeId)
        {
            typeId = RegexReplace(_variableNameSanitizer, typeId, "_");
            return typeId;
        }

        private static string ConvertClassIdToCodeId(string classId)
        {
            classId = RemoveNonVariableNameChars(classId);
            classId = classId.Replace('.', '_');
            classId = char.ToUpper(classId[0]) + classId.Substring(1);
            return classId;
        }

        private static string RemoveInvalidChars(string typeId)
        {
            typeId = RegexReplace(_fieldNameSanitizer, typeId, "_");
            return typeId;
        }

        private static string RemoveNonNamespaceChars(string typeId)
        {
            typeId = RegexReplace(_typeNameWithNamespaceSanitizer, typeId, "_");
            return typeId;
        }

        private static string RemoveReservedWords(string typeId, bool includingPrimitives = true)
        {
            MatchCollection reservedWordMatches;
            if (includingPrimitives)
            {
                reservedWordMatches = _reservedWordWithPrimitivesDetector.Matches(typeId);
            }
            else
            {
                reservedWordMatches = _reservedWordDetector.Matches(typeId);
            }

            foreach (Match m in reservedWordMatches)
            {
                typeId = typeId.Replace(m.Value, "_" + m.Value);
            }
            return typeId;
        }

        /// <summary>
        /// Replaces parts of a string, using a regex for matching.
        /// </summary>
        /// <param name="expression">The expression to use as the matcher</param>
        /// <param name="input">The input string to operate on</param>
        /// <param name="replacement">The string to replace the matches with</param>
        /// <returns>The modified string</returns>
        private static string RegexReplace(Regex expression, string input, string replacement, int maxReplacements = -1)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            MatchCollection matches = expression.Matches(input);

            string returnVal = string.Empty;
            int lastIndex = 0;
            int replacements = 0;

            foreach (Match match in matches)
            {
                returnVal += input.Substring(lastIndex, match.Index - lastIndex);
                lastIndex = match.Index + match.Length;
                returnVal += replacement;
                replacements++;
                if (maxReplacements > 0 && replacements >= maxReplacements)
                    break;
            }

            returnVal += input.Substring(lastIndex);

            return returnVal;
        }

        private static string ConvertTypeIdToCodeId(string typeId)
        {
            return typeId.Replace("http://schema.org/", "")
                .Replace("http://knowledge.microsoft.com/mso/", "");
        }

        private static string GetShortReferenceId(string typeId)
        {
            if (typeId.Contains("."))
            {
                return typeId.Substring(typeId.LastIndexOf('.') + 1);
            }

            return typeId;
        }

        private static string EscapeXml(string input)
        {
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                StringBuilder builder = pooledSb.Builder;
                builder.Append(input);
                builder.Replace("&", "&amp;");
                builder.Replace("<", "&lt;");
                builder.Replace(">", "&gt;");
                builder.Replace("&#34;", "&quot;");
                builder.Replace("'", "&apos;");
                return builder.ToString();
            }
        }
    }
}
