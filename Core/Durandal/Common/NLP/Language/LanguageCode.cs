using Durandal.Common.Collections.Interning;
using Durandal.Common.Collections.Interning.Impl;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

namespace Durandal.Common.NLP.Language
{
    /// <summary>
    /// Class representing a BCP 47 language code, supporting the "primary language" (ISO 639), "script", and "region" (ISO 3166) subtags.
    /// Example: "fr", "en-US", "zh-Hans-CN", "fil".
    /// Locale strings use lower-case format and hypens as a separator by default, though they can parse mixed-case and underscores if needed.
    /// </summary>
    public class LanguageCode : IEquatable<LanguageCode>
    {
        // https://www.w3.org/International/questions/qa-choosing-language-tags
        // https://www.iana.org/assignments/language-subtag-registry/language-subtag-registry
        // https://r12a.github.io/app-subtags/

        private const char SEPARATOR_CHAR = '-';
        private static readonly char[] SPLIT_CHARS = new char[] { '-', '_' };
        private static readonly SealedInternalizer_CharIgnoreCase_PerfectHash LANG_CODE_PARSE_DICTIONARY;
        private static readonly LanguageCode[] LANG_CODE_TABLE;

        //mapping from (T)erminology to (B)ibliographic form ISO639
        private static readonly IReadOnlyDictionary<string, string> ISO639_B_Table = new Dictionary<string, string>()
        {
            { "sqi", "alb" },
            { "hye", "arm" },
            { "eus", "baq" },
            { "bod", "tib" },
            { "mya", "bur" },
            { "ces", "cze" },
            { "zho", "chi" },
            { "cym", "wel" },
            { "deu", "ger" },
            { "nld", "dut" },
            { "ell", "gre" },
            { "fas", "per" },
            { "fra", "fre" },
            { "kat", "geo" },
            { "isl", "ice" },
            { "mkd", "mac" },
            { "mri", "mao" },
            { "msa", "may" },
            { "ron", "rum" },
            { "slk", "slo" },
        };

        private readonly Lazy<string> _bcp47Alpha2String;
        private readonly Lazy<string> _bcp47Alpha3String;

        /// <summary>
        /// Initialize parse dictionaries when this class is first touched
        /// </summary>
        static LanguageCode()
        {
            var internalizerData = PrepareInternalizer();
            LANG_CODE_PARSE_DICTIONARY = internalizerData.Item1;
            LANG_CODE_TABLE = internalizerData.Item2;
        }

        /// <summary>
        /// This language code in ISO 639-1 alpha-2 form, e.g. "no" (Norwegian)
        /// </summary>
        public string Iso639_1 { get; private set; }

        /// <summary>
        /// This language code in ISO 639-2 alpha-3 form, e.g. "rus" (Russian)
        /// </summary>
        public string Iso639_2 { get; private set; }

        /// <summary>
        /// Some languages have an ISO-639/T or "Terminology" code, which is more commonly used,
        /// and also have a separate ISO-639/B "Bibliographic" code. This field contains the bibliographic code
        /// if present.
        /// </summary>
        public string Iso639_2B { get; private set; }

        /// <summary>
        /// An optional tag representing the written variant of a language, most commonly
        /// found in "zh-Hant" (Traditional Chinese) and "zh-Hans" (Simplified Chinese), 
        /// where "Hant" and "Hans" are the script subtags.
        /// </summary>
        public string Script { get; private set; }

        /// <summary>
        /// An optional tag representing the region of the world in which this language is
        /// spoken. For example, fr-CA is French as spoken in Canada, fr-FR is French
        /// as spoken in France.
        /// </summary>
        public RegionCode Region { get; private set; }

        private LanguageCode(string iso6391, string iso6392) : this(iso6391, iso6392, null, null)
        {
        }

        private LanguageCode(string iso6391, string iso6392, string script, RegionCode region)
        {
            Iso639_1 = iso6391 ?? string.Empty;
            Iso639_2 = iso6392 ?? string.Empty;

            if (string.IsNullOrEmpty(Iso639_1))
            {
                if (string.IsNullOrEmpty(Iso639_2))
                {
                    throw new ArgumentNullException(nameof(iso6392), "If ISO-639-1 (two-char) language code is null, the ISO-639-2 (three-char) code must be present");
                }
            }
            else if (Iso639_1.Length != 2)
            {
                throw new ArgumentException($"ISO-639-1 language code MUST be exactly two characters long, e.g. \"zh\": got \"{Iso639_1}\"", nameof(iso6391));
            }

            if (!string.IsNullOrEmpty(Iso639_2) && Iso639_2.Length != 3)
            {
                throw new ArgumentException($"ISO-639-2 language code MUST be exactly three characters long, e.g. \"jpn\": got \"{Iso639_2}\"", nameof(iso6392));
            }

            if (!string.IsNullOrEmpty(Iso639_1))
            {
                Iso639_1 = Iso639_1.ToLowerInvariant();
            }

            if (!string.IsNullOrEmpty(Iso639_2))
            {
                Iso639_2 = Iso639_2.ToLowerInvariant();
            }

            string iso639B;
            if (string.IsNullOrEmpty(iso6392) || !ISO639_B_Table.TryGetValue(iso6392, out iso639B))
            {
                iso639B = string.Empty;
            }

            Iso639_2B = iso639B;
            Script = script ?? string.Empty;
            Region = region;
            _bcp47Alpha2String = new Lazy<string>(CreateBcp47Alpha2String, LazyThreadSafetyMode.PublicationOnly);
            _bcp47Alpha3String = new Lazy<string>(CreateBcp47Alpha3String, LazyThreadSafetyMode.PublicationOnly);
        }

        /// <summary>
        /// <b>Usually you should not need to call this method.</b>
        /// Use <see cref="TryParse(string)"/> or one of the static language codes that already exist.
        /// This is intended for creating custom language codes for niche purposes,
        /// such as a specific dialect of some macrolanguage that is not registered already (like pinyin Chinese)
        /// or for handling non-standard language codes that originate from external systems.
        /// </summary>
        /// <param name="iso6391">The ISO-639-1 code, which is the two-char code such as "fr". May be null if only the 639-2 code is specified.</param>
        /// <param name="iso6392">The ISO-639-2 code, which is the three-char code such as "deu"</param>
        public static LanguageCode CreateCustom(string iso6391, string iso6392)
        {
            return new LanguageCode(iso6391, iso6392);
        }

        /// <summary>
        /// <b>Usually you should not need to call this method.</b>
        /// Use <see cref="TryParse(string)"/> or one of the static language codes that already exist.
        /// This is intended for creating custom language codes for niche purposes,
        /// such as a specific dialect of some macrolanguage that is not registered already (like pinyin Chinese)
        /// or for handling non-standard language codes that originate from external systems.
        /// </summary>
        /// <param name="iso6391">The ISO-639-1 code, which is the two-char code such as "fr". May be null if only the 639-2 code is specified.</param>
        /// <param name="iso6392">The ISO-639-2 code, which is the three-char code such as "deu"</param>
        /// <param name="script">The script subtag such as "Latn" for Latin-transcribed written languages. May be null,</param>
        /// <param name="region">The country code where the language is to be used. May be null.</param>
        public static LanguageCode CreateCustomWithScriptAndRegion(string iso6391, string iso6392, string script, RegionCode region)
        {
            return new LanguageCode(iso6391, iso6392, script, region);
        }

        /// <summary>
        /// Reinterprets this language code to be in a specific country, without
        /// changing the script or other parameters.
        /// </summary>
        /// <param name="country">The country to use for the returned language code.</param>
        /// <returns>A new language code with the specified country.</returns>
        public LanguageCode InCountry(RegionCode country)
        {
            return new LanguageCode(Iso639_1, Iso639_2, Script, country);
        }

        /// <summary>
        /// Returns this language code with country information removed. For example, "pt-BR" just becomes "pt".
        /// </summary>
        /// <returns>A country-agnostic language code.</returns>
        public LanguageCode CountryAgnostic()
        {
            return InCountry(null);
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            return Equals(obj as LanguageCode);
        }

        public bool Equals(LanguageCode other)
        {
            if (other == null)
            {
                return false;
            }

            return string.Equals(Iso639_2, other.Iso639_2, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Script, other.Script, StringComparison.OrdinalIgnoreCase) &&
                Equals(Region, other.Region);
        }

        public override int GetHashCode()
        {
            int returnVal = Iso639_2.GetHashCode();

            if (!string.IsNullOrEmpty(Script))
            {
                returnVal = (returnVal << 4) ^ Script.GetHashCode();
            }

            if (Region != null)
            {
                returnVal = (returnVal << 4) ^ Region.GetHashCode();
            }

            return returnVal;
        }

        /// <summary>
        /// The default implementation of LanguageCode.ToString() returns either the BCP 47 alpha-2 format (if available) or the ISO 639-3 format
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            string returnVal = _bcp47Alpha2String.Value;
            if (string.IsNullOrEmpty(returnVal))
            {
                returnVal = _bcp47Alpha3String.Value;
            }

            return returnVal;
        }

        /// <summary>
        /// Renders this language code as a BCP 47 formatted string, using alpha-2 representation for language + region.
        /// For example, "fi", "fr-CA", "zh-Hans-CN"
        /// </summary>
        /// <returns>The formatted language string</returns>
        public string ToBcp47Alpha2String()
        {
            return _bcp47Alpha2String.Value;
        }

        private string CreateBcp47Alpha2String()
        {
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                pooledSb.Builder.Append(Iso639_1);
                if (!string.IsNullOrEmpty(Script))
                {
                    pooledSb.Builder.Append(SEPARATOR_CHAR);
                    pooledSb.Builder.Append(Script);
                }
                if (Region != null)
                {
                    pooledSb.Builder.Append(SEPARATOR_CHAR);
                    if (Region.IsUN_M49Code)
                    {
                        pooledSb.Builder.AppendFormat(CultureInfo.InvariantCulture, "{0:D3}", Region.NumericCode);
                    }
                    else
                    {
                        pooledSb.Builder.Append(Region.Iso3166_1_Alpha2);
                    }
                }

                // The number of possible language code strings is finite and presumably fairly small
                // in an application which could potentially parse language codes very frequently,
                // so this is one niche use case where string interning seems valid.
#if NET5_0_OR_GREATER
                return string.Intern(pooledSb.Builder.ToString());
#else
                return pooledSb.Builder.ToString();
#endif
            }
        }

        /// <summary>
        /// Renders this language code as a BCP 47 formatted string, using alpha-3 representation for language + region.
        /// For example, "fin", "fre-CAN", "chi-Hans-CHN"
        /// </summary>
        /// <returns>The formatted language string</returns>
        public string ToBcp47Alpha3String()
        {
            return _bcp47Alpha3String.Value;
        }

        private string CreateBcp47Alpha3String()
        {
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                pooledSb.Builder.Append(Iso639_2);
                if (!string.IsNullOrEmpty(Script))
                {
                    pooledSb.Builder.Append(SEPARATOR_CHAR);
                    pooledSb.Builder.Append(Script);
                }
                if (Region != null)
                {
                    pooledSb.Builder.Append(SEPARATOR_CHAR);
                    if (Region.IsUN_M49Code)
                    {
                        pooledSb.Builder.AppendFormat(CultureInfo.InvariantCulture, "{0:D3}", Region.NumericCode);
                    }
                    else
                    {
                        pooledSb.Builder.Append(Region.Iso3166_1_Alpha3);
                    }
                }

#if NET5_0_OR_GREATER
                return string.Intern(pooledSb.Builder.ToString());
#else
                return pooledSb.Builder.ToString();
#endif
            }
        }

        /// <summary>
        /// Parses a language code into a known language, potentially with script and region subtags.
        /// If no known locale matches, this will throw an exception.
        /// </summary>
        /// <param name="localeString">The language string to try and match (case insensitive), e.g. "pt", "en-US", "fr_CA", "zh-Hans-CN"</param>
        /// <returns>The parsed language code.</returns>
        public static LanguageCode Parse(string localeString)
        {
            LanguageCode returnVal = TryParse(localeString);
            if (returnVal == null)
            {
                throw new ArgumentException("Could not parse locale string " + localeString);
            }

            return returnVal;
        }

        /// <summary>
        /// Attempts to parse a language code into a known language, potentially with script and region subtags.
        /// </summary>
        /// <param name="localeString">The language string to try and match (case insensitive), e.g. "pt", "en-US", "fr_CA", "zh-Hans-CN"</param>
        /// <returns>The matching language code, or null if parsing failed</returns>
        public static LanguageCode TryParse(string localeString)
        {
            if (string.IsNullOrEmpty(localeString))
            {
                return null;
            }

            return TryParse(localeString.AsSpan());
        }

        /// <summary>
        /// Attempts to parse a language code into a known language, potentially with script and region subtags.
        /// </summary>
        /// <param name="localeSpan">The language span to try and match (case insensitive), e.g. "pt", "en-US", "fr_CA", "zh-Hans-CN"</param>
        /// <returns>The matching language code, or null if parsing failed</returns>
        public static LanguageCode TryParse(ReadOnlySpan<char> localeSpan)
        {
            // Split the input by hyphens and underscores
            LanguageCode parseResult;
            int break1 = localeSpan.IndexOfAny(SPLIT_CHARS);
            if (break1 < 0)
            {
                // It's just a single string e.g. "en".
                if (!TryGetValueFromInternalizer(localeSpan, out parseResult))
                {
                    return null;
                }

                return parseResult;
            }
            else
            {
                int break2 = localeSpan.Slice(break1 + 1).IndexOfAny(SPLIT_CHARS.AsSpan());
                if (break2 < 0)
                {
                    // Only 2 parts, e.g. "pt-BR" or "zh-Hans".
                    // See if part 2 is a country. Otherwise assume it is a script.
                    if (!TryGetValueFromInternalizer(localeSpan.Slice(0, break1), out parseResult))
                    {
                        return null;
                    }

                    RegionCode country = RegionCode.TryParse(localeSpan.Slice(break1 + 1));
                    if (country != null)
                    {
                        return new LanguageCode(parseResult.Iso639_1, parseResult.Iso639_2, null, country);
                    }
                    else
                    {
                        return new LanguageCode(parseResult.Iso639_1, parseResult.Iso639_2, new string(localeSpan.Slice(break1 + 1).ToArray()), null);
                    }
                }
                else
                {
                    break2 += break1 + 1;
                    int break3 = localeSpan.Slice(break2 + 1).IndexOfAny(SPLIT_CHARS.AsSpan());
                    if (break3 < 0)
                    {
                        // 3 parts. We assume this formation can only be lang-script-country
                        if (!TryGetValueFromInternalizer(localeSpan.Slice(0, break1), out parseResult))
                        {
                            return null;
                        }

                        RegionCode country = RegionCode.TryParse(localeSpan.Slice(break2 + 1));
                        if (country == null)
                        {
                            return null;
                        }
                        else
                        {
                            string script = new string(localeSpan.Slice(break1 + 1, break2 - break1 - 1).ToArray());
                            return new LanguageCode(parseResult.Iso639_1, parseResult.Iso639_2, script, country);
                        }
                    }
                    else
                    {
                        // Too many parts. Can't parse
                        return null;
                    }
                }
            }
        }

        private static bool TryGetValueFromInternalizer(ReadOnlySpan<char> input, out LanguageCode returnVal)
        {
            InternedKey<ReadOnlyMemory<char>> key;
            if (LANG_CODE_PARSE_DICTIONARY.TryGetInternalizedKey(input, out key))
            {
                returnVal = LANG_CODE_TABLE[key.Key];
                return true;
            }

            returnVal = null;
            return false;
        }

        public static readonly LanguageCode NO_LANGUAGE = new LanguageCode("xx", "zxx");

        #region The list of all "primary" language codes (meaning, those that exist in both 2-char and 3-char forms)

        public static readonly LanguageCode AFAR			= new LanguageCode("aa", "aar");
        public static readonly LanguageCode ABKHAZIAN		= new LanguageCode("ab", "abk");
        public static readonly LanguageCode AFRIKAANS		= new LanguageCode("af", "afr");
        public static readonly LanguageCode AKAN			= new LanguageCode("ak", "aka");
        public static readonly LanguageCode ALBANIAN		= new LanguageCode("sq", "sqi");
        public static readonly LanguageCode AMHARIC			= new LanguageCode("am", "amh");
        public static readonly LanguageCode ARABIC			= new LanguageCode("ar", "ara");
        public static readonly LanguageCode ARAGONESE		= new LanguageCode("an", "arg");
        public static readonly LanguageCode ARMENIAN		= new LanguageCode("hy", "hye");
        public static readonly LanguageCode ASSAMESE		= new LanguageCode("as", "asm");
        public static readonly LanguageCode AVARIC			= new LanguageCode("av", "ava");
        public static readonly LanguageCode AVESTAN			= new LanguageCode("ae", "ave");
        public static readonly LanguageCode AYMARA			= new LanguageCode("ay", "aym");
        public static readonly LanguageCode AZERBAIJANI		= new LanguageCode("az", "aze");
        public static readonly LanguageCode BAMBARA			= new LanguageCode("bm", "bam");
        public static readonly LanguageCode BASHKIR			= new LanguageCode("ba", "bak");
        public static readonly LanguageCode BASQUE			= new LanguageCode("eu", "eus");
        public static readonly LanguageCode BELARUSIAN		= new LanguageCode("be", "bel");
        public static readonly LanguageCode BENGALI			= new LanguageCode("bn", "ben");
        public static readonly LanguageCode BIHARI			= new LanguageCode("bh", "bih");
        public static readonly LanguageCode BISLAMA			= new LanguageCode("bi", "bis");
        public static readonly LanguageCode BOKMAL			= new LanguageCode("nb", "nob");
        public static readonly LanguageCode BOSNIAN			= new LanguageCode("bs", "bos");
        public static readonly LanguageCode BRETON			= new LanguageCode("br", "bre");
        public static readonly LanguageCode BULGARIAN		= new LanguageCode("bg", "bul");
        public static readonly LanguageCode BURMESE			= new LanguageCode("my", "mya");
        public static readonly LanguageCode CATALAN			= new LanguageCode("ca", "cat");
        public static readonly LanguageCode CENTRAL_KHMER	= new LanguageCode("km", "khm");
        public static readonly LanguageCode CHAMORRO		= new LanguageCode("ch", "cha");
        public static readonly LanguageCode CHECHEN			= new LanguageCode("ce", "che");
        public static readonly LanguageCode CHICHEWA		= new LanguageCode("ny", "nya");

        /// <summary>
        /// Special note: This tag refers to the Chinese "macro-language" as commonly represented in legacy programs as "zh-CN".
        /// It is preferable to use a more specific tag such as <see cref="CHINESE_MANDARIN"/> in new code.
        /// This language code is also sometimes used to represent "traditional Chinese script" for written applications,
        /// though "zh-Hant" might be more appropriate.
        /// </summary>
        public static readonly LanguageCode CHINESE			= new LanguageCode("zh", "zho");
        public static readonly LanguageCode CHURCH_SLAVIC	= new LanguageCode("cu", "chu");
        public static readonly LanguageCode CHUVASH			= new LanguageCode("cv", "chv");
        public static readonly LanguageCode CORNISH			= new LanguageCode("kw", "cor");
        public static readonly LanguageCode CORSICAN		= new LanguageCode("co", "cos");
        public static readonly LanguageCode CREE			= new LanguageCode("cr", "cre");
        public static readonly LanguageCode CROATIAN		= new LanguageCode("hr", "hrv");
        public static readonly LanguageCode CZECH			= new LanguageCode("cs", "ces");
        public static readonly LanguageCode DANISH			= new LanguageCode("da", "dan");
        public static readonly LanguageCode DIVEHI			= new LanguageCode("dv", "div");
        public static readonly LanguageCode DUTCH_FLEMISH	= new LanguageCode("nl", "nld");
        public static readonly LanguageCode DZONGKHA		= new LanguageCode("dz", "dzo");
        public static readonly LanguageCode ENGLISH			= new LanguageCode("en", "eng");
        public static readonly LanguageCode ESPERANTO		= new LanguageCode("eo", "epo");
        public static readonly LanguageCode ESTONIAN		= new LanguageCode("et", "est");
        public static readonly LanguageCode EWE				= new LanguageCode("ee", "ewe");
        public static readonly LanguageCode FAROESE			= new LanguageCode("fo", "fao");
        public static readonly LanguageCode FIJIAN			= new LanguageCode("fj", "fij");
        public static readonly LanguageCode FINNISH			= new LanguageCode("fi", "fin");
        public static readonly LanguageCode FRENCH			= new LanguageCode("fr", "frA");
        public static readonly LanguageCode FULAH			= new LanguageCode("ff", "ful");
        public static readonly LanguageCode GAELIC			= new LanguageCode("gd", "gla");
        public static readonly LanguageCode GALICIAN		= new LanguageCode("gl", "glg");
        public static readonly LanguageCode GANDA			= new LanguageCode("lg", "lug");
        public static readonly LanguageCode GEORGIAN		= new LanguageCode("ka", "kat");
        public static readonly LanguageCode GERMAN			= new LanguageCode("de", "deu");
        public static readonly LanguageCode GREEK_MODERN	= new LanguageCode("el", "ell");
        public static readonly LanguageCode GUARANI			= new LanguageCode("gn", "grn");
        public static readonly LanguageCode GUJARATI		= new LanguageCode("gu", "guj");
        public static readonly LanguageCode HAITIAN 		= new LanguageCode("ht", "hat");
        public static readonly LanguageCode HAUSA			= new LanguageCode("ha", "hau");
        public static readonly LanguageCode HEBREW			= new LanguageCode("he", "heb");
        public static readonly LanguageCode HERERO			= new LanguageCode("hz", "her");
        public static readonly LanguageCode HINDI			= new LanguageCode("hi", "hin");
        public static readonly LanguageCode HIRI_MOTU		= new LanguageCode("ho", "hmo");
        public static readonly LanguageCode HUNGARIAN		= new LanguageCode("hu", "hun");
        public static readonly LanguageCode ICELANDIC		= new LanguageCode("is", "isl");
        public static readonly LanguageCode IDO				= new LanguageCode("io", "ido");
        public static readonly LanguageCode IGBO			= new LanguageCode("ig", "ibo");
        public static readonly LanguageCode INDONESIAN		= new LanguageCode("id", "ind");
        public static readonly LanguageCode INUKTITUT		= new LanguageCode("iu", "iku");
        public static readonly LanguageCode INUPIAQ			= new LanguageCode("ik", "ipk");
        public static readonly LanguageCode IRISH			= new LanguageCode("ga", "gle");
        public static readonly LanguageCode ITALIAN			= new LanguageCode("it", "ita");
        public static readonly LanguageCode JAPANESE		= new LanguageCode("ja", "jpn");
        public static readonly LanguageCode JAVANESE		= new LanguageCode("jv", "jav");
        public static readonly LanguageCode KALAALLISUT		= new LanguageCode("kl", "kal");
        public static readonly LanguageCode KANNADA			= new LanguageCode("kn", "kan");
        public static readonly LanguageCode KANURI			= new LanguageCode("kr", "kau");
        public static readonly LanguageCode KASHMIRI		= new LanguageCode("ks", "kas");
        public static readonly LanguageCode KAZAKH			= new LanguageCode("kk", "kaz");
        public static readonly LanguageCode KIKUYU			= new LanguageCode("ki", "kik");
        public static readonly LanguageCode KINYARWANDA		= new LanguageCode("rw", "kin");
        public static readonly LanguageCode KIRGHIZ 		= new LanguageCode("ky", "kir");
        public static readonly LanguageCode KOMI			= new LanguageCode("kv", "kom");
        public static readonly LanguageCode KONGO			= new LanguageCode("kg", "kon");
        public static readonly LanguageCode KOREAN			= new LanguageCode("ko", "kor");
        public static readonly LanguageCode KUANYAMA		= new LanguageCode("kj", "kua");
        public static readonly LanguageCode KURDISH			= new LanguageCode("ku", "kur");
        public static readonly LanguageCode LAO				= new LanguageCode("lo", "lao");
        public static readonly LanguageCode LATIN			= new LanguageCode("la", "lat");
        public static readonly LanguageCode LATVIAN			= new LanguageCode("lv", "lav");
        public static readonly LanguageCode LIMBURGAN		= new LanguageCode("li", "lim");
        public static readonly LanguageCode LINGALA			= new LanguageCode("ln", "lin");
        public static readonly LanguageCode LITHUANIAN		= new LanguageCode("lt", "lit");
        public static readonly LanguageCode LUBA_KATANGA	= new LanguageCode("lu", "lub");
        public static readonly LanguageCode LUXEMBOURGISH	= new LanguageCode("lb", "ltz");
        public static readonly LanguageCode MACEDONIAN		= new LanguageCode("mk", "mkd");
        public static readonly LanguageCode MALAGASY		= new LanguageCode("mg", "mlg");
        public static readonly LanguageCode MALAY			= new LanguageCode("ms", "msa");
        public static readonly LanguageCode MALAYALAM		= new LanguageCode("ml", "mal");
        public static readonly LanguageCode MALTESE			= new LanguageCode("mt", "mlt");
        public static readonly LanguageCode MANX			= new LanguageCode("gv", "glv");
        public static readonly LanguageCode MAORI			= new LanguageCode("mi", "mri");
        public static readonly LanguageCode MARATHI			= new LanguageCode("mr", "mar");
        public static readonly LanguageCode MARSHALLESE		= new LanguageCode("mh", "mah");
        public static readonly LanguageCode MONGOLIAN		= new LanguageCode("mn", "mon");
        public static readonly LanguageCode NAURU			= new LanguageCode("na", "nau");
        public static readonly LanguageCode NAVAJO			= new LanguageCode("nv", "nav");
        public static readonly LanguageCode NDEBELE_NORTH	= new LanguageCode("nd", "nde");
        public static readonly LanguageCode NDEBELE_SOUTH	= new LanguageCode("nr", "nbl");
        public static readonly LanguageCode NDONGA			= new LanguageCode("ng", "ndo");
        public static readonly LanguageCode NEPALI			= new LanguageCode("ne", "nep");
        public static readonly LanguageCode NORTHERN_SAMI	= new LanguageCode("se", "sme");
        public static readonly LanguageCode NORWEGIAN		= new LanguageCode("no", "nor");
        public static readonly LanguageCode NYNORSK			= new LanguageCode("nn", "nno");
        public static readonly LanguageCode OCCITAN			= new LanguageCode("oc", "oci");
        public static readonly LanguageCode OJIBWA			= new LanguageCode("oj", "oji");
        public static readonly LanguageCode ORIYA			= new LanguageCode("or", "ori");
        public static readonly LanguageCode OROMO			= new LanguageCode("om", "orm");
        public static readonly LanguageCode OSSETIAN		= new LanguageCode("os", "oss");
        public static readonly LanguageCode PALI			= new LanguageCode("pi", "pli");
        public static readonly LanguageCode PANJABI			= new LanguageCode("pa", "pan");
        public static readonly LanguageCode PERSIAN			= new LanguageCode("fa", "fas");
        public static readonly LanguageCode POLISH			= new LanguageCode("pl", "pol");
        public static readonly LanguageCode PORTUGUESE		= new LanguageCode("pt", "por");
        public static readonly LanguageCode PUSHTO			= new LanguageCode("ps", "pus");
        public static readonly LanguageCode QUECHUA			= new LanguageCode("qu", "que");
        public static readonly LanguageCode ROMANIAN		= new LanguageCode("ro", "ron");
        public static readonly LanguageCode ROMANSH			= new LanguageCode("rm", "roh");
        public static readonly LanguageCode RUNDI			= new LanguageCode("rn", "run");
        public static readonly LanguageCode RUSSIAN			= new LanguageCode("ru", "rus");
        public static readonly LanguageCode SAMOAN			= new LanguageCode("sm", "smo");
        public static readonly LanguageCode SANGO			= new LanguageCode("sg", "sag");
        public static readonly LanguageCode SANSKRIT		= new LanguageCode("sa", "san");
        public static readonly LanguageCode SARDINIAN		= new LanguageCode("sc", "srd");
        public static readonly LanguageCode SERBIAN			= new LanguageCode("sr", "srp");
        public static readonly LanguageCode SHONA			= new LanguageCode("sn", "sna");
        public static readonly LanguageCode SICHUAN_YI		= new LanguageCode("ii", "iii");
        public static readonly LanguageCode SINDHI			= new LanguageCode("sd", "snd");
        public static readonly LanguageCode SINHALA 		= new LanguageCode("si", "sin");
        public static readonly LanguageCode SLOVAK			= new LanguageCode("sk", "slk");
        public static readonly LanguageCode SLOVENIAN		= new LanguageCode("sl", "slv");
        public static readonly LanguageCode SOMALI			= new LanguageCode("so", "som");
        public static readonly LanguageCode SOTHO_SOUTHERN	= new LanguageCode("st", "sot");
        public static readonly LanguageCode SPANISH 		= new LanguageCode("es", "spa");
        public static readonly LanguageCode SUNDANESE		= new LanguageCode("su", "sun");
        public static readonly LanguageCode SWAHILI			= new LanguageCode("sw", "swa");
        public static readonly LanguageCode SWATI			= new LanguageCode("ss", "ssw");
        public static readonly LanguageCode SWEDISH			= new LanguageCode("sv", "swe");
        public static readonly LanguageCode TAGALOG			= new LanguageCode("tl", "tgl");
        public static readonly LanguageCode TAHITIAN		= new LanguageCode("ty", "tah");
        public static readonly LanguageCode TAJIK			= new LanguageCode("tg", "tgk");
        public static readonly LanguageCode TAMIL			= new LanguageCode("ta", "tam");
        public static readonly LanguageCode TATAR			= new LanguageCode("tt", "tat");
        public static readonly LanguageCode TELUGU			= new LanguageCode("te", "tel");
        public static readonly LanguageCode THAI			= new LanguageCode("th", "tha");
        public static readonly LanguageCode TIBETAN			= new LanguageCode("bo", "bod");
        public static readonly LanguageCode TIGRINYA		= new LanguageCode("ti", "tir");
        public static readonly LanguageCode TONGA			= new LanguageCode("to", "ton");
        public static readonly LanguageCode TSONGA			= new LanguageCode("ts", "tso");
        public static readonly LanguageCode TSWANA			= new LanguageCode("tn", "tsn");
        public static readonly LanguageCode TURKISH			= new LanguageCode("tr", "tur");
        public static readonly LanguageCode TURKMEN			= new LanguageCode("tk", "tuk");
        public static readonly LanguageCode TWI				= new LanguageCode("tw", "twi");
        public static readonly LanguageCode UIGHUR			= new LanguageCode("ug", "uig");
        public static readonly LanguageCode UKRAINIAN		= new LanguageCode("uk", "ukr");
        public static readonly LanguageCode URDU			= new LanguageCode("ur", "urd");
        public static readonly LanguageCode UZBEK			= new LanguageCode("uz", "uzb");
        public static readonly LanguageCode VENDA			= new LanguageCode("ve", "ven");
        public static readonly LanguageCode VIETNAMESE		= new LanguageCode("vi", "vie");
        public static readonly LanguageCode VOLAPUK			= new LanguageCode("vo", "vol");
        public static readonly LanguageCode WALLOON			= new LanguageCode("wa", "wln");
        public static readonly LanguageCode WELSH			= new LanguageCode("cy", "cym");
        public static readonly LanguageCode WESTERN_FRISIAN	= new LanguageCode("fy", "fry");
        public static readonly LanguageCode WOLOF			= new LanguageCode("wo", "wol");
        public static readonly LanguageCode XHOSA			= new LanguageCode("xh", "xho");
        public static readonly LanguageCode YIDDISH			= new LanguageCode("yi", "yid");
        public static readonly LanguageCode YORUBA			= new LanguageCode("yo", "yor");
        public static readonly LanguageCode ZHUANG			= new LanguageCode("za", "zha");
        public static readonly LanguageCode ZULU			= new LanguageCode("zu", "zul");

        #endregion

        #region Extended codes only present in ISO 639-2

        public static readonly LanguageCode UNCODED_LANG = new LanguageCode(string.Empty, "mis");   //
        public static readonly LanguageCode MULTIPLE_LANGS = new LanguageCode(string.Empty, "mul"); // present in ISO 639-2 but not in 639-1
        public static readonly LanguageCode UNDETERMINED = new LanguageCode(string.Empty, "und");   //

        public static readonly LanguageCode ACHINESE 		        	= new LanguageCode(string.Empty, "ace");
        public static readonly LanguageCode ACOLI 		            	= new LanguageCode(string.Empty, "ach");
        public static readonly LanguageCode ADANGME 	        		= new LanguageCode(string.Empty, "ada");
        public static readonly LanguageCode ADYGHE 		            	= new LanguageCode(string.Empty, "ady");
        public static readonly LanguageCode AFRIHILI 	        		= new LanguageCode(string.Empty, "afh");
        public static readonly LanguageCode AINU 		            	= new LanguageCode(string.Empty, "ain");
        public static readonly LanguageCode AKKADIAN 		        	= new LanguageCode(string.Empty, "akk");
        public static readonly LanguageCode ALEUT 		            	= new LanguageCode(string.Empty, "ale");
        public static readonly LanguageCode SOUTHERN_ALTAI 		    	= new LanguageCode(string.Empty, "alt");
        public static readonly LanguageCode ENGLISH_OLD 		       	= new LanguageCode(string.Empty, "ang");
        public static readonly LanguageCode ANGIKA 		            	= new LanguageCode(string.Empty, "anp");
        public static readonly LanguageCode ARAMAIC 		        	= new LanguageCode(string.Empty, "arc");
        public static readonly LanguageCode MAPUDUNGUN 		        	= new LanguageCode(string.Empty, "arn");
        public static readonly LanguageCode ARAPAHO 	        		= new LanguageCode(string.Empty, "arp");
        public static readonly LanguageCode ARAWAK 		            	= new LanguageCode(string.Empty, "arw");
        public static readonly LanguageCode ASTURIAN 		        	= new LanguageCode(string.Empty, "ast");
        public static readonly LanguageCode AWADHI 		            	= new LanguageCode(string.Empty, "awa");
        public static readonly LanguageCode BALINESE 		        	= new LanguageCode(string.Empty, "ban");
        public static readonly LanguageCode BASA 		            	= new LanguageCode(string.Empty, "bas");
        public static readonly LanguageCode BEJA 		            	= new LanguageCode(string.Empty, "bej");
        public static readonly LanguageCode BEMBA 		            	= new LanguageCode(string.Empty, "bem");
        public static readonly LanguageCode BHOJPURI 		        	= new LanguageCode(string.Empty, "bho");
        public static readonly LanguageCode BINI 		            	= new LanguageCode(string.Empty, "bin");
        public static readonly LanguageCode SIKSIKA 		        	= new LanguageCode(string.Empty, "bla");
        public static readonly LanguageCode BRAJ 		            	= new LanguageCode(string.Empty, "bra");
        public static readonly LanguageCode BUGINESE 		        	= new LanguageCode(string.Empty, "bug");
        public static readonly LanguageCode BLIN 		            	= new LanguageCode(string.Empty, "byn");
        public static readonly LanguageCode CADDO 		            	= new LanguageCode(string.Empty, "cad");
        public static readonly LanguageCode GALIBI_CARIB 	    		= new LanguageCode(string.Empty, "car");
        public static readonly LanguageCode CEBUANO 		        	= new LanguageCode(string.Empty, "ceb");
        public static readonly LanguageCode CHIBCHA 		        	= new LanguageCode(string.Empty, "chb");
        public static readonly LanguageCode CHAGATAI 		        	= new LanguageCode(string.Empty, "chg");
        public static readonly LanguageCode CHUUKESE 		        	= new LanguageCode(string.Empty, "chk");
        public static readonly LanguageCode CHINOOK_JARGON 		    	= new LanguageCode(string.Empty, "chn");
        public static readonly LanguageCode CHOCTAW 		        	= new LanguageCode(string.Empty, "cho");
        public static readonly LanguageCode CHIPEWYAN 		        	= new LanguageCode(string.Empty, "chp");
        public static readonly LanguageCode CHEROKEE 		        	= new LanguageCode(string.Empty, "chr");
        public static readonly LanguageCode CHEYENNE 		        	= new LanguageCode(string.Empty, "chy");
        public static readonly LanguageCode MONTENEGRIN 	    		= new LanguageCode(string.Empty, "cnr");
        public static readonly LanguageCode COPTIC 		            	= new LanguageCode(string.Empty, "cop");
        public static readonly LanguageCode CRIMEAN_TATAR 		    	= new LanguageCode(string.Empty, "crh");
        public static readonly LanguageCode KASHUBIAN 		        	= new LanguageCode(string.Empty, "csb");
        public static readonly LanguageCode DAKOTA 		            	= new LanguageCode(string.Empty, "dak");
        public static readonly LanguageCode DARGWA 		            	= new LanguageCode(string.Empty, "dar");
        public static readonly LanguageCode DOGRIB 		            	= new LanguageCode(string.Empty, "dgr");
        public static readonly LanguageCode LOWER_SORBIAN 		    	= new LanguageCode(string.Empty, "dsb");
        public static readonly LanguageCode DUALA 		            	= new LanguageCode(string.Empty, "dua");
        public static readonly LanguageCode DUTCH_MIDDLE 		      	= new LanguageCode(string.Empty, "dum");
        public static readonly LanguageCode DYULA 		            	= new LanguageCode(string.Empty, "dyu");
        public static readonly LanguageCode EFIK 		            	= new LanguageCode(string.Empty, "efi");
        public static readonly LanguageCode EGYPTIAN 		        	= new LanguageCode(string.Empty, "egy");
        public static readonly LanguageCode EKAJUK 		            	= new LanguageCode(string.Empty, "eka");
        public static readonly LanguageCode ELAMITE 		        	= new LanguageCode(string.Empty, "elx");
        public static readonly LanguageCode ENGLISH_MIDDLE 	    		= new LanguageCode(string.Empty, "enm");
        public static readonly LanguageCode EWONDO 		            	= new LanguageCode(string.Empty, "ewo");
        public static readonly LanguageCode FANG 		            	= new LanguageCode(string.Empty, "fan");
        public static readonly LanguageCode FANTI 		            	= new LanguageCode(string.Empty, "fat");
        public static readonly LanguageCode FILIPINO 		        	= new LanguageCode(string.Empty, "fil");
        public static readonly LanguageCode FON 		            	= new LanguageCode(string.Empty, "fon");
        public static readonly LanguageCode FRENCH_MIDDLE 	    		= new LanguageCode(string.Empty, "frm");
        public static readonly LanguageCode FRENCH_OLD 		        	= new LanguageCode(string.Empty, "fro");
        public static readonly LanguageCode NORTHERN_FRISIAN 			= new LanguageCode(string.Empty, "frr");
        public static readonly LanguageCode EAST_FRISIAN_LOW_SAXON 		= new LanguageCode(string.Empty, "frs");
        public static readonly LanguageCode FRIULIAN 		        	= new LanguageCode(string.Empty, "fur");
        public static readonly LanguageCode GA 		                	= new LanguageCode(string.Empty, "gaa");
        public static readonly LanguageCode GAYO 	            		= new LanguageCode(string.Empty, "gay");
        public static readonly LanguageCode GEEZ 		            	= new LanguageCode(string.Empty, "gez");
        public static readonly LanguageCode GILBERTESE 	        		= new LanguageCode(string.Empty, "gil");
        public static readonly LanguageCode GERMAN_MIDDLE_HIGH 			= new LanguageCode(string.Empty, "gmh");
        public static readonly LanguageCode GERMAN_OLD_HIGH 			= new LanguageCode(string.Empty, "goh");
        public static readonly LanguageCode GORONTALO 		        	= new LanguageCode(string.Empty, "gor");
        public static readonly LanguageCode GOTHIC 		            	= new LanguageCode(string.Empty, "got");
        public static readonly LanguageCode GREEK_ANCIENT 	    		= new LanguageCode(string.Empty, "grc");
        public static readonly LanguageCode SWISS_GERMAN 		    	= new LanguageCode(string.Empty, "gsw");
        public static readonly LanguageCode GWICHIN 		        	= new LanguageCode(string.Empty, "gwi");
        public static readonly LanguageCode HAWAIIAN 		        	= new LanguageCode(string.Empty, "haw");
        public static readonly LanguageCode HILIGAYNON 		        	= new LanguageCode(string.Empty, "hil");
        public static readonly LanguageCode HITTITE 		        	= new LanguageCode(string.Empty, "hit");
        public static readonly LanguageCode UPPER_SORBIAN 	    		= new LanguageCode(string.Empty, "hsb");
        public static readonly LanguageCode HUPA 		            	= new LanguageCode(string.Empty, "hup");
        public static readonly LanguageCode IBAN 		            	= new LanguageCode(string.Empty, "iba");
        public static readonly LanguageCode ILOKO 		               	= new LanguageCode(string.Empty, "ilo");
        public static readonly LanguageCode INGUSH 		             	= new LanguageCode(string.Empty, "inh");
        public static readonly LanguageCode LOJBAN 		               	= new LanguageCode(string.Empty, "jbo");
        public static readonly LanguageCode JUDEO_PERSIAN 	    		= new LanguageCode(string.Empty, "jpr");
        public static readonly LanguageCode KARA_KALPAK 	    		= new LanguageCode(string.Empty, "kaa");
        public static readonly LanguageCode KABYLE 		            	= new LanguageCode(string.Empty, "kab");
        public static readonly LanguageCode KACHIN 		            	= new LanguageCode(string.Empty, "kac");
        public static readonly LanguageCode KAMBA 		            	= new LanguageCode(string.Empty, "kam");
        public static readonly LanguageCode KAWI 		            	= new LanguageCode(string.Empty, "kaw");
        public static readonly LanguageCode KABARDIAN 		        	= new LanguageCode(string.Empty, "kbd");
        public static readonly LanguageCode KHASI 		            	= new LanguageCode(string.Empty, "kha");
        public static readonly LanguageCode KHOTANESE 		        	= new LanguageCode(string.Empty, "kho");
        public static readonly LanguageCode KIMBUNDU 	        		= new LanguageCode(string.Empty, "kmb");
        public static readonly LanguageCode KOSRAEAN 		        	= new LanguageCode(string.Empty, "kos");
        public static readonly LanguageCode KARACHAY_BALKAR 			= new LanguageCode(string.Empty, "krc");
        public static readonly LanguageCode KARELIAN 		        	= new LanguageCode(string.Empty, "krl");
        public static readonly LanguageCode KURUKH 		            	= new LanguageCode(string.Empty, "kru");
        public static readonly LanguageCode KUMYK 		            	= new LanguageCode(string.Empty, "kum");
        public static readonly LanguageCode KUTENAI 	        		= new LanguageCode(string.Empty, "kut");
        public static readonly LanguageCode LADINO 		            	= new LanguageCode(string.Empty, "lad");
        public static readonly LanguageCode LAMBA 		            	= new LanguageCode(string.Empty, "lam");
        public static readonly LanguageCode LEZGHIAN 		        	= new LanguageCode(string.Empty, "lez");
        public static readonly LanguageCode MONGO 		            	= new LanguageCode(string.Empty, "lol");
        public static readonly LanguageCode LOZI 		            	= new LanguageCode(string.Empty, "loz");
        public static readonly LanguageCode LUBA_LULUA 		        	= new LanguageCode(string.Empty, "lua");
        public static readonly LanguageCode LUISENO 		        	= new LanguageCode(string.Empty, "lui");
        public static readonly LanguageCode LUNDA 		            	= new LanguageCode(string.Empty, "lun");
        public static readonly LanguageCode LUO 		            	= new LanguageCode(string.Empty, "luo");
        public static readonly LanguageCode LUSHAI 		            	= new LanguageCode(string.Empty, "lus");
        public static readonly LanguageCode MADURESE 	        		= new LanguageCode(string.Empty, "mad");
        public static readonly LanguageCode MAGAHI 		            	= new LanguageCode(string.Empty, "mag");
        public static readonly LanguageCode MAITHILI 		        	= new LanguageCode(string.Empty, "mai");
        public static readonly LanguageCode MAKASAR 	        		= new LanguageCode(string.Empty, "mak");
        public static readonly LanguageCode MASAI 		            	= new LanguageCode(string.Empty, "mas");
        public static readonly LanguageCode MOKSHA 		            	= new LanguageCode(string.Empty, "mdf");
        public static readonly LanguageCode MANDAR 		            	= new LanguageCode(string.Empty, "mdr");
        public static readonly LanguageCode MENDE 		            	= new LanguageCode(string.Empty, "men");
        public static readonly LanguageCode IRISH_MIDDLE 	    		= new LanguageCode(string.Empty, "mga");
        public static readonly LanguageCode MIKMAQ 		            	= new LanguageCode(string.Empty, "mic");
        public static readonly LanguageCode MINANGKABAU 	    		= new LanguageCode(string.Empty, "min");
        public static readonly LanguageCode MANCHU 		            	= new LanguageCode(string.Empty, "mnc");
        public static readonly LanguageCode MANIPURI 		        	= new LanguageCode(string.Empty, "mni");
        public static readonly LanguageCode MOHAWK 		            	= new LanguageCode(string.Empty, "moh");
        public static readonly LanguageCode MOSSI 		            	= new LanguageCode(string.Empty, "mos");
        public static readonly LanguageCode CREEK 		            	= new LanguageCode(string.Empty, "mus");
        public static readonly LanguageCode MIRANDESE 	        		= new LanguageCode(string.Empty, "mwl");
        public static readonly LanguageCode ERZYA 		            	= new LanguageCode(string.Empty, "myv");
        public static readonly LanguageCode NEAPOLITAN 	        		= new LanguageCode(string.Empty, "nap");
        public static readonly LanguageCode LOW_GERMAN 	        		= new LanguageCode(string.Empty, "nds");
        public static readonly LanguageCode NEPAL_BHASA 	    		= new LanguageCode(string.Empty, "new");
        public static readonly LanguageCode NIAS 		             	= new LanguageCode(string.Empty, "nia");
        public static readonly LanguageCode NIUEAN 		             	= new LanguageCode(string.Empty, "niu");
        public static readonly LanguageCode NOGAI 		             	= new LanguageCode(string.Empty, "nog");
        public static readonly LanguageCode NORSE_OLD 	        		= new LanguageCode(string.Empty, "non");
        public static readonly LanguageCode NKO 		            	= new LanguageCode(string.Empty, "nqo");
        public static readonly LanguageCode PEDI 		              	= new LanguageCode(string.Empty, "nso");
        public static readonly LanguageCode CLASSICAL_NEWARI 			= new LanguageCode(string.Empty, "nwc");
        public static readonly LanguageCode NYAMWEZI 		        	= new LanguageCode(string.Empty, "nym");
        public static readonly LanguageCode NYANKOLE 		        	= new LanguageCode(string.Empty, "nyn");
        public static readonly LanguageCode NYORO 		            	= new LanguageCode(string.Empty, "nyo");
        public static readonly LanguageCode NZIMA 		            	= new LanguageCode(string.Empty, "nzi");
        public static readonly LanguageCode OSAGE 		            	= new LanguageCode(string.Empty, "osa");
        public static readonly LanguageCode TURKISH_OTTOMAN 			= new LanguageCode(string.Empty, "ota");
        public static readonly LanguageCode PANGASINAN 		        	= new LanguageCode(string.Empty, "pag");
        public static readonly LanguageCode PAHLAVI 		        	= new LanguageCode(string.Empty, "pal");
        public static readonly LanguageCode PAMPANGA 		        	= new LanguageCode(string.Empty, "pam");
        public static readonly LanguageCode PAPIAMENTO 		        	= new LanguageCode(string.Empty, "pap");
        public static readonly LanguageCode PALAUAN 		        	= new LanguageCode(string.Empty, "pau");
        public static readonly LanguageCode PERSIAN_OLD 	    		= new LanguageCode(string.Empty, "peo");
        public static readonly LanguageCode PHOENICIAN 		        	= new LanguageCode(string.Empty, "phn");
        public static readonly LanguageCode POHNPEIAN 		        	= new LanguageCode(string.Empty, "pon");
        public static readonly LanguageCode PROVENÇAL_OLD 		    	= new LanguageCode(string.Empty, "pro");
        public static readonly LanguageCode RAPANUI 		        	= new LanguageCode(string.Empty, "rap");
        public static readonly LanguageCode RAROTONGAN 		        	= new LanguageCode(string.Empty, "rar");
        public static readonly LanguageCode AROMANIAN 		        	= new LanguageCode(string.Empty, "rup");
        public static readonly LanguageCode SANDAWE 		        	= new LanguageCode(string.Empty, "sad");
        public static readonly LanguageCode YAKUT 			            = new LanguageCode(string.Empty, "sah");
        public static readonly LanguageCode SAMARITAN_ARAMAIC 			= new LanguageCode(string.Empty, "sam");
        public static readonly LanguageCode SASAK 		            	= new LanguageCode(string.Empty, "sas");
        public static readonly LanguageCode SANTALI 		        	= new LanguageCode(string.Empty, "sat");
        public static readonly LanguageCode SICILIAN 		        	= new LanguageCode(string.Empty, "scn");
        public static readonly LanguageCode SCOTS 		            	= new LanguageCode(string.Empty, "sco");
        public static readonly LanguageCode SELKUP 		            	= new LanguageCode(string.Empty, "sel");
        public static readonly LanguageCode IRISH_OLD 		        	= new LanguageCode(string.Empty, "sga");
        public static readonly LanguageCode SHAN 		            	= new LanguageCode(string.Empty, "shn");
        public static readonly LanguageCode SIDAMO 			            = new LanguageCode(string.Empty, "sid");
        public static readonly LanguageCode SOUTHERN_SAMI 	    		= new LanguageCode(string.Empty, "sma");
        public static readonly LanguageCode LULE_SAMI 		        	= new LanguageCode(string.Empty, "smj");
        public static readonly LanguageCode INARI_SAMI 		        	= new LanguageCode(string.Empty, "smn");
        public static readonly LanguageCode SKOLT_SAMI 		        	= new LanguageCode(string.Empty, "sms");
        public static readonly LanguageCode SONINKE 		        	= new LanguageCode(string.Empty, "snk");
        public static readonly LanguageCode SOGDIAN 		        	= new LanguageCode(string.Empty, "sog");
        public static readonly LanguageCode SRANAN_TONGO 		    	= new LanguageCode(string.Empty, "srn");
        public static readonly LanguageCode SERER 		            	= new LanguageCode(string.Empty, "srr");
        public static readonly LanguageCode SUKUMA 		            	= new LanguageCode(string.Empty, "suk");
        public static readonly LanguageCode SUSU 		            	= new LanguageCode(string.Empty, "sus");
        public static readonly LanguageCode SUMERIAN 		        	= new LanguageCode(string.Empty, "sux");
        public static readonly LanguageCode CLASSICAL_SYRIAC 			= new LanguageCode(string.Empty, "syc");
        public static readonly LanguageCode TIMNE 		            	= new LanguageCode(string.Empty, "tem");
        public static readonly LanguageCode TERENO 		            	= new LanguageCode(string.Empty, "ter");
        public static readonly LanguageCode TETUM 		            	= new LanguageCode(string.Empty, "tet");
        public static readonly LanguageCode TIGRE 		            	= new LanguageCode(string.Empty, "tig");
        public static readonly LanguageCode TIV 		            	= new LanguageCode(string.Empty, "tiv");
        public static readonly LanguageCode TOKELAU 	        		= new LanguageCode(string.Empty, "tkl");
        public static readonly LanguageCode KLINGON 		        	= new LanguageCode(string.Empty, "tlh");
        public static readonly LanguageCode TLINGIT 		        	= new LanguageCode(string.Empty, "tli");
        public static readonly LanguageCode TONGA_NYASA 	    		= new LanguageCode(string.Empty, "tog");
        public static readonly LanguageCode TOK_PISIN 		        	= new LanguageCode(string.Empty, "tpi");
        public static readonly LanguageCode TSIMSHIAN 		        	= new LanguageCode(string.Empty, "tsi");
        public static readonly LanguageCode TUMBUKA 		        	= new LanguageCode(string.Empty, "tum");
        public static readonly LanguageCode TUVALU 		            	= new LanguageCode(string.Empty, "tvl");
        public static readonly LanguageCode TUVINIAN 	        		= new LanguageCode(string.Empty, "tyv");
        public static readonly LanguageCode UDMURT 		            	= new LanguageCode(string.Empty, "udm");
        public static readonly LanguageCode UGARITIC 		        	= new LanguageCode(string.Empty, "uga");
        public static readonly LanguageCode UMBUNDU 	        		= new LanguageCode(string.Empty, "umb");
        public static readonly LanguageCode VAI 		            	= new LanguageCode(string.Empty, "vai");
        public static readonly LanguageCode VOTIC 		            	= new LanguageCode(string.Empty, "vot");
        public static readonly LanguageCode WOLAITTA 	        		= new LanguageCode(string.Empty, "wal");
        public static readonly LanguageCode WARAY 		            	= new LanguageCode(string.Empty, "war");
        public static readonly LanguageCode WASHO 		            	= new LanguageCode(string.Empty, "was");
        public static readonly LanguageCode KALMYK 		            	= new LanguageCode(string.Empty, "xal");
        public static readonly LanguageCode YAO 		            	= new LanguageCode(string.Empty, "yao");
        public static readonly LanguageCode YAPESE 		            	= new LanguageCode(string.Empty, "yap");
        public static readonly LanguageCode BLISSYMBOLS 	    		= new LanguageCode(string.Empty, "zbl");
        public static readonly LanguageCode ZENAGA 		            	= new LanguageCode(string.Empty, "zen");
        public static readonly LanguageCode STANDARD_MOROCCAN_TAMAZIGHT = new LanguageCode(string.Empty, "zgh");
        public static readonly LanguageCode ZUNI 			            = new LanguageCode(string.Empty, "zun");
        public static readonly LanguageCode BALUCHI 		        	= new LanguageCode(string.Empty, "bal");
        public static readonly LanguageCode BIKOL 			            = new LanguageCode(string.Empty, "bik");
        public static readonly LanguageCode BURIAT 			            = new LanguageCode(string.Empty, "bua");
        public static readonly LanguageCode MARI 		            	= new LanguageCode(string.Empty, "chm");
        public static readonly LanguageCode DELAWARE 	        		= new LanguageCode(string.Empty, "del");
        public static readonly LanguageCode SLAVE 		            	= new LanguageCode(string.Empty, "den");
        public static readonly LanguageCode DINKA 			            = new LanguageCode(string.Empty, "din");
        public static readonly LanguageCode DOGRI 		            	= new LanguageCode(string.Empty, "doi");
        public static readonly LanguageCode GBAYA 			            = new LanguageCode(string.Empty, "gba");
        public static readonly LanguageCode GONDI 		            	= new LanguageCode(string.Empty, "gon");
        public static readonly LanguageCode GREBO 		            	= new LanguageCode(string.Empty, "grb");
        public static readonly LanguageCode HAIDA 		            	= new LanguageCode(string.Empty, "hai");
        public static readonly LanguageCode HMONG 		            	= new LanguageCode(string.Empty, "hmn");
        public static readonly LanguageCode JUDEO_ARABIC 	    		= new LanguageCode(string.Empty, "jrb");
        public static readonly LanguageCode KONKANI 	        		= new LanguageCode(string.Empty, "kok");
        public static readonly LanguageCode KPELLE 		            	= new LanguageCode(string.Empty, "kpe");
        public static readonly LanguageCode LAHNDA 		            	= new LanguageCode(string.Empty, "lah");
        public static readonly LanguageCode MANDINGO 	          		= new LanguageCode(string.Empty, "man");
        public static readonly LanguageCode MARWARI 	        		= new LanguageCode(string.Empty, "mwr");
        public static readonly LanguageCode RAJASTHANI 	        		= new LanguageCode(string.Empty, "raj");
        public static readonly LanguageCode ROMANY 		            	= new LanguageCode(string.Empty, "rom");
        public static readonly LanguageCode SYRIAC 		            	= new LanguageCode(string.Empty, "syr");
        public static readonly LanguageCode TAMASHEK 	        		= new LanguageCode(string.Empty, "tmh");
        public static readonly LanguageCode ZAPOTEC 	        		= new LanguageCode(string.Empty, "zap");
        public static readonly LanguageCode ZAZA 		            	= new LanguageCode(string.Empty, "zza");
        public static readonly LanguageCode CHINESE_MANDARIN            = new LanguageCode(string.Empty, "cmn");
        public static readonly LanguageCode CHINESE_YUE_CANTONESE       = new LanguageCode(string.Empty, "yue");

        // TODO there's potentially a lot more subdialects that are assigned a code but not represented here.
        // Presumably such users would use CreateCustom to just make those locales themselves if they need to use them.

        #endregion

        // The list of "convenience" codes that are combined languages + countries,
        // but are used frequently enough to warrant a shortcut
        // Must be declared after the primary languages above otherwise you get a null reference!
        public static readonly LanguageCode DE_DE = GERMAN.InCountry(RegionCode.GERMANY);
        public static readonly LanguageCode EN_US = ENGLISH.InCountry(RegionCode.UNITED_STATES_OF_AMERICA);
        public static readonly LanguageCode EN_GB = ENGLISH.InCountry(RegionCode.UNITED_KINGDOM);
        public static readonly LanguageCode ES_ES = SPANISH.InCountry(RegionCode.SPAIN);
        public static readonly LanguageCode FR_FR = FRENCH.InCountry(RegionCode.FRANCE);
        public static readonly LanguageCode ZH_CN = CHINESE.InCountry(RegionCode.CHINA);

        /// <summary>
        /// Lazily instantiates the parse dictionary
        /// </summary>
        /// <returns></returns>
        private static Tuple<SealedInternalizer_CharIgnoreCase_PerfectHash, LanguageCode[]> PrepareInternalizer()
        {
            int internalizedKeyCounter = 0;
            List<LanguageCode> allLangCodes = new List<LanguageCode>();
            List<KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>> internalizerData =
                new List<KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>>();

            #region Building the parse list
            AddLanguageToParseList(AFAR, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(ABKHAZIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(AFRIKAANS, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(AKAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(ALBANIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(AMHARIC, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(ARABIC, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(ARAGONESE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(ARMENIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(ASSAMESE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(AVARIC, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(AVESTAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(AYMARA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(AZERBAIJANI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(BAMBARA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(BASHKIR, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(BASQUE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(BELARUSIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(BENGALI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(BIHARI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(BISLAMA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(BOKMAL, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(BOSNIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(BRETON, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(BULGARIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(BURMESE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(CATALAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(CENTRAL_KHMER, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(CHAMORRO, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(CHECHEN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(CHICHEWA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(CHINESE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(CHURCH_SLAVIC, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(CHUVASH, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(CORNISH, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(CORSICAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(CREE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(CROATIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(CZECH, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(DANISH, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(DIVEHI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(DUTCH_FLEMISH, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(DZONGKHA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(ENGLISH, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(ESPERANTO, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(ESTONIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(EWE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(FAROESE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(FIJIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(FINNISH, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(FRENCH, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(FULAH, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(GAELIC, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(GALICIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(GANDA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(GEORGIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(GERMAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(GREEK_MODERN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(GUARANI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(GUJARATI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(HAITIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(HAUSA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(HEBREW, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(HERERO, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(HINDI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(HIRI_MOTU, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(HUNGARIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(ICELANDIC, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(IDO, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(IGBO, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(INDONESIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(INUKTITUT, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(INUPIAQ, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(IRISH, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(ITALIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(JAPANESE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(JAVANESE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(KALAALLISUT, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(KANNADA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(KANURI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(KASHMIRI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(KAZAKH, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(KIKUYU, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(KINYARWANDA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(KIRGHIZ, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(KOMI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(KONGO, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(KOREAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(KUANYAMA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(KURDISH, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(LAO, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(LATIN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(LATVIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(LIMBURGAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(LINGALA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(LITHUANIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(LUBA_KATANGA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(LUXEMBOURGISH, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(MACEDONIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(MALAGASY, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(MALAY, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(MALAYALAM, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(MALTESE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(MANX, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(MAORI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(MARATHI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(MARSHALLESE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(MONGOLIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(NAURU, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(NAVAJO, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(NDEBELE_NORTH, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(NDEBELE_SOUTH, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(NDONGA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(NEPALI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(NORTHERN_SAMI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(NORWEGIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(NYNORSK, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(OCCITAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(OJIBWA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(ORIYA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(OROMO, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(OSSETIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(PALI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(PANJABI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(PERSIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(POLISH, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(PORTUGUESE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(PUSHTO, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(QUECHUA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(ROMANIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(ROMANSH, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(RUNDI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(RUSSIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SAMOAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SANGO, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SANSKRIT, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SARDINIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SERBIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SHONA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SICHUAN_YI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SINDHI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SINHALA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SLOVAK, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SLOVENIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SOMALI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SOTHO_SOUTHERN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SPANISH, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SUNDANESE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SWAHILI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SWATI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SWEDISH, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(TAGALOG, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(TAHITIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(TAJIK, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(TAMIL, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(TATAR, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(TELUGU, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(THAI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(TIBETAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(TIGRINYA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(TONGA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(TSONGA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(TSWANA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(TURKISH, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(TURKMEN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(TWI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(UIGHUR, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(UKRAINIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(URDU, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(UZBEK, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(VENDA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(VIETNAMESE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(VOLAPUK, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(WALLOON, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(WELSH, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(WESTERN_FRISIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(WOLOF, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(XHOSA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(YIDDISH, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(YORUBA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(ZHUANG, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(ZULU, allLangCodes, internalizerData, ref internalizedKeyCounter);

            AddLanguageToParseList(UNCODED_LANG, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(MULTIPLE_LANGS, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(UNDETERMINED, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(NO_LANGUAGE, allLangCodes, internalizerData, ref internalizedKeyCounter);

            AddLanguageToParseList(ACHINESE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(ACOLI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(ADANGME, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(ADYGHE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(AFRIHILI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(AINU, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(AKKADIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(ALEUT, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SOUTHERN_ALTAI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(ENGLISH_OLD, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(ANGIKA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(ARAMAIC, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(MAPUDUNGUN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(ARAPAHO, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(ARAWAK, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(ASTURIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(AWADHI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(BALINESE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(BASA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(BEJA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(BEMBA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(BHOJPURI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(BINI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SIKSIKA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(BRAJ, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(BUGINESE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(BLIN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(CADDO, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(GALIBI_CARIB, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(CEBUANO, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(CHIBCHA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(CHAGATAI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(CHUUKESE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(CHINOOK_JARGON, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(CHOCTAW, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(CHIPEWYAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(CHEROKEE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(CHEYENNE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(MONTENEGRIN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(COPTIC, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(CRIMEAN_TATAR, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(KASHUBIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(DAKOTA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(DARGWA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(DOGRIB, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(LOWER_SORBIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(DUALA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(DUTCH_MIDDLE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(DYULA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(EFIK, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(EGYPTIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(EKAJUK, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(ELAMITE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(ENGLISH_MIDDLE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(EWONDO, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(FANG, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(FANTI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(FILIPINO, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(FON, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(FRENCH_MIDDLE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(FRENCH_OLD, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(NORTHERN_FRISIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(EAST_FRISIAN_LOW_SAXON, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(FRIULIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(GA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(GAYO, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(GEEZ, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(GILBERTESE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(GERMAN_MIDDLE_HIGH, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(GERMAN_OLD_HIGH, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(GORONTALO, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(GOTHIC, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(GREEK_ANCIENT, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SWISS_GERMAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(GWICHIN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(HAWAIIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(HILIGAYNON, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(HITTITE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(UPPER_SORBIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(HUPA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(IBAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(ILOKO, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(INGUSH, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(LOJBAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(JUDEO_PERSIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(KARA_KALPAK, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(KABYLE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(KACHIN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(KAMBA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(KAWI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(KABARDIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(KHASI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(KHOTANESE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(KIMBUNDU, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(KOSRAEAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(KARACHAY_BALKAR, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(KARELIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(KURUKH, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(KUMYK, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(KUTENAI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(LADINO, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(LAMBA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(LEZGHIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(MONGO, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(LOZI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(LUBA_LULUA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(LUISENO, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(LUNDA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(LUO, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(LUSHAI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(MADURESE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(MAGAHI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(MAITHILI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(MAKASAR, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(MASAI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(MOKSHA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(MANDAR, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(MENDE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(IRISH_MIDDLE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(MIKMAQ, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(MINANGKABAU, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(MANCHU, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(MANIPURI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(MOHAWK, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(MOSSI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(CREEK, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(MIRANDESE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(ERZYA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(NEAPOLITAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(LOW_GERMAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(NEPAL_BHASA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(NIAS, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(NIUEAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(NOGAI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(NORSE_OLD, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(NKO, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(PEDI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(CLASSICAL_NEWARI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(NYAMWEZI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(NYANKOLE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(NYORO, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(NZIMA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(OSAGE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(TURKISH_OTTOMAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(PANGASINAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(PAHLAVI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(PAMPANGA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(PAPIAMENTO, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(PALAUAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(PERSIAN_OLD, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(PHOENICIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(POHNPEIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(PROVENÇAL_OLD, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(RAPANUI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(RAROTONGAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(AROMANIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SANDAWE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(YAKUT, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SAMARITAN_ARAMAIC, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SASAK, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SANTALI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SICILIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SCOTS, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SELKUP, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(IRISH_OLD, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SHAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SIDAMO, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SOUTHERN_SAMI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(LULE_SAMI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(INARI_SAMI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SKOLT_SAMI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SONINKE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SOGDIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SRANAN_TONGO, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SERER, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SUKUMA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SUSU, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SUMERIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(CLASSICAL_SYRIAC, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(TIMNE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(TERENO, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(TETUM, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(TIGRE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(TIV, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(TOKELAU, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(KLINGON, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(TLINGIT, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(TONGA_NYASA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(TOK_PISIN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(TSIMSHIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(TUMBUKA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(TUVALU, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(TUVINIAN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(UDMURT, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(UGARITIC, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(UMBUNDU, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(VAI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(VOTIC, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(WOLAITTA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(WARAY, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(WASHO, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(KALMYK, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(YAO, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(YAPESE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(BLISSYMBOLS, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(ZENAGA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(STANDARD_MOROCCAN_TAMAZIGHT, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(ZUNI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(BALUCHI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(BIKOL, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(BURIAT, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(MARI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(DELAWARE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SLAVE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(DINKA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(DOGRI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(GBAYA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(GONDI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(GREBO, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(HAIDA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(HMONG, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(JUDEO_ARABIC, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(KONKANI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(KPELLE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(LAHNDA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(MANDINGO, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(MARWARI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(RAJASTHANI, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(ROMANY, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(SYRIAC, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(TAMASHEK, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(ZAPOTEC, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(ZAZA, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(CHINESE_MANDARIN, allLangCodes, internalizerData, ref internalizedKeyCounter);
            AddLanguageToParseList(CHINESE_YUE_CANTONESE, allLangCodes, internalizerData, ref internalizedKeyCounter);
            #endregion

            SealedInternalizer_CharIgnoreCase_PerfectHash internalizer = new SealedInternalizer_CharIgnoreCase_PerfectHash(internalizerData);
            return new Tuple<SealedInternalizer_CharIgnoreCase_PerfectHash, LanguageCode[]>(internalizer, allLangCodes.ToArray());
        }

        private static void AddLanguageToParseList(
            LanguageCode code,
            List<LanguageCode> langCodes,
            List<KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>> internalizerData,
            ref int langCodeIndex)
        {
            langCodes.Add(code);

            if (!string.IsNullOrEmpty(code.Iso639_1))
            {
                internalizerData.Add(new KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>(
                    new InternedKey<ReadOnlyMemory<char>>(langCodeIndex),
                    code.Iso639_1.AsMemory()));
            }

            if (!string.IsNullOrEmpty(code.Iso639_2))
            {
                internalizerData.Add(new KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>(
                    new InternedKey<ReadOnlyMemory<char>>(langCodeIndex),
                    code.Iso639_2.AsMemory()));
            }

            if (!string.IsNullOrEmpty(code.Iso639_2B))
            {
                internalizerData.Add(new KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>(
                    new InternedKey<ReadOnlyMemory<char>>(langCodeIndex),
                    code.Iso639_2B.AsMemory()));
            }

            langCodeIndex++;
        }
    }
}
