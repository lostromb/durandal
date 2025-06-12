using Durandal.Common.Utils;
using Durandal.Common.LG.Statistical.Transformers;
using Durandal.Common.Parsers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Durandal.Common.NLP.Language;

namespace Durandal.Common.LG.Statistical
{
    public class ParsedStatisticalLGTemplate
    {
        // Matches the pattern of most standard identifiers in Durandal
        private static Parser<string> parseIdentifier =
            Parse.Regex(new Regex("[a-zA-Z0-9_-]+"));

        private static Parser<string> parseSlotName =
            Parse.Regex(new Regex("[a-zA-Z0-9\\._-]+"));

        // Parses a string, including [\"] sequences, until it reaches an unescaped terminator ["]
        private static Parser<string> parseEscapedString =
            Parse.Regex(new Regex("(\\\\\\\"|[^\\\"])+"));

        private static Parser<KeyValuePair<string, string>> parseSingleVariant =
            from key in Parse.Regex(new Regex("[a-zA-Z0-9\\._-]+"))
            from delim in Parse.Char(':')
            from value in Parse.Regex(new Regex("[a-zA-Z0-9\\._-]+"))
            select new KeyValuePair<string, string>(key, value);

        private static Parser<string> parseComment =
            from leadingWhitespace in Parse.Chars(' ', '\t').Optional()
            from open in Parse.Char('#')
            from the_rest in Parse.Regex(new Regex("[^\\r\\n]+"))
            select the_rest;

        private static Parser<char> parseSingleWhitespace =
            from optionalComment in parseComment.Optional()
            from whitespace in Parse.Chars(' ', '\r', '\n', '\t').Many()
            select ' ';

        private static Parser<IEnumerable<char>> parseWhitespace =
            parseSingleWhitespace.Many();

        private static Parser<string> parseEOL =
            Parse.Regex(new Regex("[\\r\\n]{1,2}"));

        private static Parser<string> parseEOLWithLeadingWhitespace =
            from comment in parseComment.Optional()
            from newline in Parse.Regex(new Regex("[ \t]*[\\r\\n]{1,2}"))
            select newline;
        
        private static Parser<string> parseEngine =
            from open in Parse.String("[Engine").Or(Parse.String("[engine"))
            from c in Parse.Char(':')
            from engine in Parse.LetterOrDigit.Many().Text()
            from close in Parse.Char(']')
            select engine;

        private static Parser<LanguageCode> parseLocale =
            from text in Parse.Regex(new Regex("[a-zA-Z]+-[a-zA-Z]+"))
            select LanguageCode.Parse(text);

        private static Parser<IEnumerable<LanguageCode>> parseLocales =
            from open in Parse.String("[Locales").Or(Parse.String("[locales"))
            from c in Parse.Char(':')
            from locales in parseLocale.DelimitedBy(Parse.Char(','))
            from close in Parse.Char(']')
            select locales;

        private static Parser<string> parseScriptLine =
            Parse.Regex(new Regex("[^\\[\\r\\n][^\\r\\n]*"));

        private static Parser<string> parseTextLineNonComment =
            Parse.Regex(new Regex("[ \\t]*[^#\\s][^\\r\\n]*"));

        private static Parser<string> parseTextLine =
            Parse.Regex(new Regex("[^\\r\\n]+"));

        private static Parser<string> parseModelBlockHeader =
            from open in Parse.String("[Model")
            from c in Parse.Char(':')
            from name in parseIdentifier
            from close in Parse.Char(']')
            select name;

        // FIXME : this structure requires us to have an empty line after every model. otherwise it just continues assuming the next lines are model training sentences.
        // For now we patch over this by injecting empty lines during the preprocessor step
        private static Parser<ITemplateFileBlock> parseModelBlock =
            from name in parseModelBlockHeader
            from whitespace in parseEOLWithLeadingWhitespace.AtLeastOnce()
            from lines in parseTextLineNonComment.DelimitedBy(parseEOL)
            select new ModelBlock(name, lines);

        private static Parser<KeyValuePair<string, string>> parseTranslationTableEntry =
            from key in parseIdentifier
            from delimiter in Parse.Char('=')
            from value in parseTextLine
            select new KeyValuePair<string, string>(key, value);

        private static Parser<string> parseTranslationTableHeader =
            from open in Parse.String("[TranslationTable")
            from c in Parse.Char(':')
            from name in parseIdentifier
            from close in Parse.Char(']')
            select name;

        private static Parser<ITemplateFileBlock> parseTranslationTableBlock =
            from name in parseTranslationTableHeader
            from whitespace in parseEOLWithLeadingWhitespace.AtLeastOnce()
            from entries in parseTranslationTableEntry.DelimitedBy(parseEOL)
            select new TranslationTable(name, entries);

        private static Parser<string> parseScriptBlockHeader =
            from open in Parse.String("[Script")
            from c in Parse.Char(':')
            from name in parseIdentifier
            from close in Parse.Char(']')
            select name;

        private static Parser<ITemplateFileBlock> parseScriptBlock =
            from name in parseScriptBlockHeader
            from whitespace in parseEOLWithLeadingWhitespace.AtLeastOnce()
            from lines in parseScriptLine.DelimitedBy(parseEOL)
            select new ScriptBlock(name, lines);

        private static Parser<string> parsePhraseBlockHeader =
            from open in Parse.String("[Phrase")
            from c in Parse.Char(':')
            from name in parseIdentifier
            from close in Parse.Char(']')
            select name;

        private static Parser<IPhraseProperty> parseCatchAllPhraseProperty =
            /*from key in (Parse.String("TextModel")
                    .Or(Parse.String("SpokenModel"))
                    .Or(Parse.String("ShortTextModel"))
                    .Or(Parse.String("Text"))
                    .Or(Parse.String("Spoken"))
                    .Or(Parse.String("ShortText"))).Text()*/
            from key in parseIdentifier
            from eq in Parse.Char('=')
            from value in parseTextLine
            select new KeyValuePhraseProperty(key, value);

        private static Parser<ISlotTransformer> parseUppercaseTransformer =
            from funcName in Parse.String("Uppercase").Text()
            select new UppercaseTransformer();

        private static Parser<ISlotTransformer> parseLowercaseTransformer =
            from funcName in Parse.String("Lowercase").Text()
            select new LowercaseTransformer();

        private static Parser<ISlotTransformer> parseCapitalizeTransformer =
            from funcName in Parse.String("Capitalize").Text()
            select new CapitalizeTransformer();

        private static Parser<ISlotTransformer> parseTranslateTransformer =
            from funcName in Parse.String("Translate").Text()
            from open in Parse.Char('(')
            from whitespace1 in Parse.WhiteSpace.Many().Optional()
            from arg in parseIdentifier
            from whitespace2 in Parse.WhiteSpace.Many().Optional()
            from close in Parse.Char(')')
            select new TranslateTransformer(arg);

        private static Parser<ISlotTransformer> parseSubphraseTransformer =
            from funcName in Parse.String("Subphrase").Text()
            from arg in parseIdentifier.Contained(Parse.Char('('), Parse.Char(')'))
            select new SubphraseTransformer(arg);

        private static Parser<ISlotTransformer> parseDateTimeFormatTransformer =
            from funcName in Parse.String("DateTimeFormat").Text()
            from open in Parse.Char('(')
            from whitespace1 in Parse.WhiteSpace.Many().Optional()
            from openquote in Parse.Char('\"')
            from arg in parseEscapedString
            from closequote in Parse.Char('\"')
            from whitespace2 in Parse.WhiteSpace.Many().Optional()
            from close in Parse.Char(')')
            select new DateTimeFormatTransformer(arg);

        private static Parser<ISlotTransformer> parseNumberFormatTransformer =
            from funcName in Parse.String("NumberFormat").Text()
            from open in Parse.Char('(')
            from whitespace1 in Parse.WhiteSpace.Many().Optional()
            from openquote in Parse.Char('\"')
            from arg in parseEscapedString
            from closequote in Parse.Char('\"')
            from whitespace2 in Parse.WhiteSpace.Many().Optional()
            from close in Parse.Char(')')
            select new NumberFormatTransformer(arg);

        private static Parser<ISlotTransformer> parseTrimLeftTransformer =
            from funcName in Parse.String("TrimLeft").Text()
            from open in Parse.Char('(')
            from whitespace1 in Parse.WhiteSpace.Many().Optional()
            from openquote in Parse.Char('\"')
            from arg in parseEscapedString
            from closequote in Parse.Char('\"')
            from whitespace2 in Parse.WhiteSpace.Many().Optional()
            from close in Parse.Char(')')
            select new TrimLeftTransformer(arg);

        private static Parser<ISlotTransformer> parseTrimRightTransformer =
            from funcName in Parse.String("TrimRight").Text()
            from open in Parse.Char('(')
            from whitespace1 in Parse.WhiteSpace.Many().Optional()
            from openquote in Parse.Char('\"')
            from arg in parseEscapedString
            from closequote in Parse.Char('\"')
            from whitespace2 in Parse.WhiteSpace.Many().Optional()
            from close in Parse.Char(')')
            select new TrimRightTransformer(arg);

        private static Parser<ISlotTransformer> parseTransformer =
            parseUppercaseTransformer
            .Or(parseLowercaseTransformer)
            .Or(parseCapitalizeTransformer)
            .Or(parseTranslateTransformer)
            .Or(parseTrimLeftTransformer)
            .Or(parseTrimRightTransformer)
            .Or(parseSubphraseTransformer)
            .Or(parseDateTimeFormatTransformer)
            .Or(parseNumberFormatTransformer);

        private static Parser<char> parseComma =
            from whitespace1 in Parse.WhiteSpace.Many().Optional()
            from comma in Parse.Char(',')
            from whitespace2 in Parse.WhiteSpace.Many().Optional()
            select comma;

        private static Parser<IPhraseProperty> parsePhrasePropertyTransformer =
            from propName in Parse.String("Transformer")
            from amp in Parse.Char('-')
            from slotName in parseSlotName
            from delimiter in Parse.Char('=')
            from chain in parseTransformer.DelimitedBy(parseComma)
            select new TransformerPhraseProperty(slotName, chain);

        private static Parser<IPhraseProperty> parsePhrasePropertyVariantConstraints =
            from propName in Parse.String("VariantConstraints")
            from delimiter in Parse.Char('=')
            from constraints in parseSingleVariant.DelimitedBy(parseComma)
            select new VariantConstraintPhraseProperty(constraints);

        private static Parser<IPhraseProperty> parsePhraseAttributeEntry =
            parsePhrasePropertyTransformer
            .Or(parsePhrasePropertyVariantConstraints)
            .Or(parseCatchAllPhraseProperty);

        private static Parser<ITemplateFileBlock> parsePhraseBlock =
            from name in parsePhraseBlockHeader
            from whitespace in parseEOLWithLeadingWhitespace.AtLeastOnce()
            from properties in parsePhraseAttributeEntry.DelimitedBy(parseEOL)
            select new PhraseBlock(name, properties);

        private static Parser<IEnumerable<ITemplateFileBlock>> parseTemplateBody =
            (parsePhraseBlock
            .Or(parseModelBlock)
            .Or(parseTranslationTableBlock)
            .Or(parseScriptBlock))
            .DelimitedBy(parseWhitespace);

        private static Parser<ParsedStatisticalLGTemplate> parseFileContents =
            from whitespace0 in parseWhitespace.Optional()
            from engineDec in parseEngine
            from whitespace in parseEOLWithLeadingWhitespace.AtLeastOnce()
            from locales in parseLocales
            from whitespace2 in parseEOLWithLeadingWhitespace.AtLeastOnce()
            from blocks in parseTemplateBody
            from whitespace3 in parseWhitespace.Optional()
            select new ParsedStatisticalLGTemplate(engineDec, locales, blocks);

        private static Parser<ParsedStatisticalLGTemplate> parseFile =
            Parse.End(parseFileContents);

        public List<LanguageCode> SupportedLocales { get; private set; }
        public string Engine { get; private set; }
        public List<ITemplateFileBlock> Blocks { get; private set; }

        public string OriginalFileName { get; private set; }

        private ParsedStatisticalLGTemplate(string engine, IEnumerable<LanguageCode> supportedLocales, IEnumerable<ITemplateFileBlock> blocks)
        {
            Engine = engine;
            SupportedLocales = new List<LanguageCode>(supportedLocales);
            Blocks = new List<ITemplateFileBlock>(blocks);
        }

        private static readonly Regex BlockParser = new Regex("\\[(.+?):.+?\\]");

        public static ParsedStatisticalLGTemplate ParseTemplate(IEnumerable<string> input, string originalFileName = "")
        {
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                StringBuilder file = pooledSb.Builder;
                string currentBlockType = "NULL";
                // Do some quick preprocessing here to work around parser limitations
                // Insert an empty line after each [Model] block
                // Replace blank lines in [Script] blocks with padded spaces
                foreach (string line in input)
                {
                    string block = StringUtils.RegexRip(BlockParser, line, 1);
                    if (!string.IsNullOrEmpty(block))
                    {
                        // Detect when we're starting a new block
                        if (string.Equals(currentBlockType, "Model"))
                        {
                            file.AppendLine();
                        }

                        currentBlockType = block;
                    }

                    if (string.Equals(currentBlockType, "Script") &&
                        string.IsNullOrEmpty(line))
                    {
                        file.AppendLine(" ");
                    }
                    else
                    {
                        file.AppendLine(line);
                    }
                }

                // Fixme: this ToString() is a huge allocation that I wish we could avoid - maybe by parsing each block individually or something?
                ParsedStatisticalLGTemplate returnVal = parseFile.Parse(file.ToString());
                returnVal.OriginalFileName = originalFileName;
                return returnVal;
            }
        }
    }
    
    public class PhraseNameWithVariants
    {
        public string Name;
        public IEnumerable<KeyValuePair<string, string>> Variants;

        public PhraseNameWithVariants(string name, IEnumerable<KeyValuePair<string, string>> variants)
        {
            Name = name;
            Variants = variants;
        }
    }

    public interface ITemplateFileBlock
    {
        TemplateFileBlockType BlockType { get; }
    }

    public enum TemplateFileBlockType
    {
        Model,
        Phrase,
        TranslationTable,
        Script
    }

    public class PhraseBlock : ITemplateFileBlock
    {
        public List<IPhraseProperty> Properties { get; private set; }
        public string Name { get; private set; }

        public TemplateFileBlockType BlockType
        {
            get
            {
                return TemplateFileBlockType.Phrase;
            }
        }

        public PhraseBlock(string name, IEnumerable<IPhraseProperty> properties)
        {
            Name = name;
            Properties = new List<IPhraseProperty>(properties);
        }
    }

    public class ModelBlock : ITemplateFileBlock
    {
        public List<string> TrainingLines { get; private set; }
        public string Name { get; private set; }

        public TemplateFileBlockType BlockType
        {
            get
            {
                return TemplateFileBlockType.Model;
            }
        }

        public ModelBlock(string name, IEnumerable<string> trainingLines)
        {
            Name = name;
            TrainingLines = new List<string>(trainingLines);
        }
    }

    public class ScriptBlock : ITemplateFileBlock
    {
        public List<string> CodeLines { get; private set; }
        public string Name { get; private set; }

        public TemplateFileBlockType BlockType
        {
            get
            {
                return TemplateFileBlockType.Script;
            }
        }

        public ScriptBlock(string name, IEnumerable<string> codeLines)
        {
            Name = name;
            CodeLines = new List<string>(codeLines);
        }
    }

    public class TranslationTable : ITemplateFileBlock
    {
        public Dictionary<string, string> Mapping { get; private set; }
        public string Name { get; private set; }

        public TemplateFileBlockType BlockType
        {
            get
            {
                return TemplateFileBlockType.TranslationTable;
            }
        }

        public TranslationTable(string name, IEnumerable<KeyValuePair<string, string>> entries)
        {
            Name = name;
            Mapping = new Dictionary<string, string>();
            foreach (var entry in entries)
            {
                Mapping[entry.Key] = entry.Value;
            }
        }
    }

    public interface IPhraseProperty
    {
        string PropertyName { get; }
    }

    public class KeyValuePhraseProperty : IPhraseProperty
    {
        public string PropertyName { get; private set; }

        public string Value { get; private set; }

        public KeyValuePhraseProperty(string name, string value)
        {
            PropertyName = name;
            Value = value;
        }
    }

    public class TransformerPhraseProperty : IPhraseProperty
    {
        public string PropertyName
        {
            get
            {
                return "Transformer";
            }
        }

        public string SlotName { get; private set; }
        public List<ISlotTransformer> TransformChain { get; private set; }

        public TransformerPhraseProperty(string slotName, IEnumerable<ISlotTransformer> chain)
        {
            SlotName = slotName;
            TransformChain = new List<ISlotTransformer>(chain);
        }
    }

    public class VariantConstraintPhraseProperty : IPhraseProperty
    {
        public string PropertyName
        {
            get
            {
                return "VariantConstraints";
            }
        }
        
        public Dictionary<string, string> VariantConstraints { get; private set; }

        public VariantConstraintPhraseProperty(IEnumerable<KeyValuePair<string, string>> variants)
        {
            VariantConstraints = new Dictionary<string, string>();
            foreach (var kvp in variants)
            {
                if (!VariantConstraints.ContainsKey(kvp.Key))
                {
                    VariantConstraints.Add(kvp.Key, kvp.Value);
                }
                else
                {
                    List<string> stuff = new List<string>();
                    foreach (var a in variants)
                    {
                        stuff.Add(a.Key + ":" + a.Value);
                    }
                    throw new ParseException("A phrase has multiple variant constraints with the same key, which is not allowed. Variants are " + string.Join(",", stuff));
                }
            }
        }
    }
}
