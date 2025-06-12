using Durandal.Common.NLP.Language;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.MathExt;
using System.Diagnostics.Metrics;

namespace Durandal.Tests.Common.NLP
{
    [TestClass]
    public class LocaleTests
    {
        [TestMethod]
        public void TestLanguageCodeTryParsePrimaryLanguageAlpha2()
        {
            LanguageCode code = LanguageCode.TryParse("ur");
            Assert.AreEqual(LanguageCode.URDU.Iso639_1, code.Iso639_1);
            Assert.AreEqual(LanguageCode.URDU.Iso639_2, code.Iso639_2);
            Assert.AreEqual(string.Empty, code.Script);
            Assert.IsNull(code.Region);
        }

        [TestMethod]
        public void TestLanguageCodeTryParsePrimaryLanguageAlpha2Uppercase()
        {
            LanguageCode code = LanguageCode.TryParse("UR");
            Assert.AreEqual(LanguageCode.URDU.Iso639_1, code.Iso639_1);
            Assert.AreEqual(LanguageCode.URDU.Iso639_2, code.Iso639_2);
            Assert.AreEqual(string.Empty, code.Script);
            Assert.IsNull(code.Region);
        }

        [TestMethod]
        public void TestLanguageCodeTryParsePrimaryLanguageAlpha3()
        {
            LanguageCode code = LanguageCode.TryParse("lav");
            Assert.AreEqual(LanguageCode.LATVIAN.Iso639_1, code.Iso639_1);
            Assert.AreEqual(LanguageCode.LATVIAN.Iso639_2, code.Iso639_2);
            Assert.AreEqual(string.Empty, code.Script);
            Assert.IsNull(code.Region);
        }

        [TestMethod]
        public void TestLanguageCodeTryParsePrimaryLanguageAlpha3Uppercase()
        {
            LanguageCode code = LanguageCode.TryParse("LAV");
            Assert.AreEqual(LanguageCode.LATVIAN.Iso639_1, code.Iso639_1);
            Assert.AreEqual(LanguageCode.LATVIAN.Iso639_2, code.Iso639_2);
            Assert.AreEqual(string.Empty, code.Script);
            Assert.IsNull(code.Region);
        }

        [TestMethod]
        public void TestLanguageCodeTryParsePrimaryLanguageUnknown()
        {
            LanguageCode code = LanguageCode.TryParse("zx");
            Assert.IsNull(code);
        }

        [TestMethod]
        public void TestLanguageCodeTryParseSingleChar()
        {
            LanguageCode code = LanguageCode.TryParse("5");
            Assert.IsNull(code);
        }

        [TestMethod]
        public void TestLanguageCodeTryParseRareThreeChar()
        {
            LanguageCode code = LanguageCode.TryParse("fil");
            Assert.AreEqual(LanguageCode.FILIPINO.Iso639_1, code.Iso639_1);
            Assert.AreEqual(LanguageCode.FILIPINO.Iso639_2, code.Iso639_2);
            Assert.AreEqual(string.Empty, code.Script);
            Assert.IsNull(code.Region);
        }

        [TestMethod]
        public void TestLanguageCodeTryParseLanguageAndScript()
        {
            LanguageCode code = LanguageCode.TryParse("zh-Hans");
            Assert.AreEqual(LanguageCode.CHINESE.Iso639_1, code.Iso639_1);
            Assert.AreEqual(LanguageCode.CHINESE.Iso639_2, code.Iso639_2);
            Assert.AreEqual("Hans", code.Script);
            Assert.IsNull(code.Region);
        }

        [TestMethod]
        public void TestLanguageCodeTryParseLanguageAndScriptUppercase()
        {
            LanguageCode code = LanguageCode.TryParse("ZH-HANS");
            Assert.AreEqual(LanguageCode.CHINESE.Iso639_1, code.Iso639_1);
            Assert.AreEqual(LanguageCode.CHINESE.Iso639_2, code.Iso639_2);
            Assert.AreEqual("HANS", code.Script);
            Assert.IsNull(code.Region);
        }

        [TestMethod]
        public void TestLanguageCodeTryParseLanguageAndScriptUnderscore()
        {
            LanguageCode code = LanguageCode.TryParse("zh_Hant");
            Assert.AreEqual(LanguageCode.CHINESE.Iso639_1, code.Iso639_1);
            Assert.AreEqual(LanguageCode.CHINESE.Iso639_2, code.Iso639_2);
            Assert.AreEqual("Hant", code.Script);
            Assert.IsNull(code.Region);
        }

        [TestMethod]
        public void TestLanguageCodeTryParseLanguageAndRegion()
        {
            LanguageCode code = LanguageCode.TryParse("fr-CA");
            Assert.AreEqual(LanguageCode.FRENCH.Iso639_1, code.Iso639_1);
            Assert.AreEqual(LanguageCode.FRENCH.Iso639_2, code.Iso639_2);
            Assert.AreEqual(string.Empty, code.Script);
            Assert.AreEqual(RegionCode.CANADA, code.Region);
        }

        [TestMethod]
        public void TestLanguageCodeTryParseLanguageAndRegionUnderscore()
        {
            LanguageCode code = LanguageCode.TryParse("fr_CA");
            Assert.AreEqual(LanguageCode.FRENCH.Iso639_1, code.Iso639_1);
            Assert.AreEqual(LanguageCode.FRENCH.Iso639_2, code.Iso639_2);
            Assert.AreEqual(string.Empty, code.Script);
            Assert.AreEqual(RegionCode.CANADA, code.Region);
        }

        [TestMethod]
        public void TestLanguageCodeTryParseLanguageScriptRegion()
        {
            LanguageCode code = LanguageCode.TryParse("zh-Hans-TW");
            Assert.AreEqual(LanguageCode.CHINESE.Iso639_1, code.Iso639_1);
            Assert.AreEqual(LanguageCode.CHINESE.Iso639_2, code.Iso639_2);
            Assert.AreEqual("Hans", code.Script);
            Assert.AreEqual(RegionCode.TAIWAN, code.Region);
        }

        [TestMethod]
        public void TestLanguageCodeTryParseLanguageScriptRegionUnderscore()
        {
            LanguageCode code = LanguageCode.TryParse("zh_Hans_TW");
            Assert.AreEqual(LanguageCode.CHINESE.Iso639_1, code.Iso639_1);
            Assert.AreEqual(LanguageCode.CHINESE.Iso639_2, code.Iso639_2);
            Assert.AreEqual("Hans", code.Script);
            Assert.AreEqual(RegionCode.TAIWAN, code.Region);
        }

        [TestMethod]
        public void TestLanguageCodeTryParseLanguageScriptRegionBibliographicCodes()
        {
            LanguageCode code = LanguageCode.TryParse("chi-Hans-TWN");
            Assert.AreEqual(LanguageCode.CHINESE.Iso639_1, code.Iso639_1);
            Assert.AreEqual(LanguageCode.CHINESE.Iso639_2, code.Iso639_2);
            Assert.AreEqual("Hans", code.Script);
            Assert.AreEqual(RegionCode.TAIWAN, code.Region);
        }

        [TestMethod]
        public void TestLanguageCodeTryParseLanguageRegionNumericRegion()
        {
            LanguageCode code = LanguageCode.TryParse("pt-076");
            Assert.AreEqual(LanguageCode.PORTUGUESE.Iso639_1, code.Iso639_1);
            Assert.AreEqual(LanguageCode.PORTUGUESE.Iso639_2, code.Iso639_2);
            Assert.AreEqual(string.Empty, code.Script);
            Assert.AreEqual(RegionCode.BRAZIL, code.Region);
            Assert.AreEqual("pt-BR", code.ToBcp47Alpha2String());
            Assert.AreEqual("por-BRA", code.ToBcp47Alpha3String());
            Assert.AreEqual("pt-BR", code.ToString());
        }

        [TestMethod]
        public void TestLanguageCodeTryParseLanguageScriptRegionUnknownRegion()
        {
            LanguageCode code = LanguageCode.TryParse("zh-Hans-ZZ");
            Assert.IsNull(code);
        }

        [TestMethod]
        public void TestLanguageCodeTryParseLanguageScriptRegionUnknownLanguage()
        {
            LanguageCode code = LanguageCode.TryParse("zz-Hans-CN");
            Assert.IsNull(code);
        }

        [TestMethod]
        public void TestLanguageCodeTryParseLanguageSpanishLatinAmerica()
        {
            LanguageCode code = LanguageCode.TryParse("es_419");
            Assert.IsNotNull(code);
            Assert.AreEqual(LanguageCode.SPANISH.Iso639_1, code.Iso639_1);
            Assert.AreEqual(LanguageCode.SPANISH.Iso639_2, code.Iso639_2);
            Assert.AreEqual(string.Empty, code.Script);
            Assert.AreEqual(RegionCode.REGION_LATIN_AMERICA_CARIBBEAN, code.Region);
            Assert.AreEqual("es-419", code.ToBcp47Alpha2String());
            Assert.AreEqual("spa-419", code.ToBcp47Alpha3String());
            Assert.AreEqual("es-419", code.ToString());
        }

        [TestMethod]
        public void TestLanguageCodeTryParseTooManyParts()
        {
            // Zurich german, we don't support the spec at a high enough level to represent this
            LanguageCode code = LanguageCode.TryParse("gsw-u-sd-chzh");
            Assert.IsNull(code);
        }

        [TestMethod]
        public void TestLanguageCodeTryParseEmptyString()
        {
            LanguageCode code = LanguageCode.TryParse(string.Empty);
            Assert.IsNull(code);
        }

        [TestMethod]
        public void TestLanguageCodeTryParseNullString()
        {
            LanguageCode code = LanguageCode.TryParse(null);
            Assert.IsNull(code);
        }

        [TestMethod]
        public void TestLanguageCodeTryParseFuzz()
        {
            byte[] randField = new byte[1000];
            IRandom rand = new FastRandom();
            for (int pass = 0; pass < 10000; pass++)
            {
                int length = rand.NextInt(1, 1000);
                for (int c = 0; c < length; c++)
                {
                    randField[c] = (byte)rand.NextInt(1, 128);
                }

                LanguageCode.TryParse(Encoding.ASCII.GetString(randField, 0, length));
            }
        }

        [TestMethod]
        public void TestLanguageCodeToStringBCP47Alpha2_1part()
        {
            LanguageCode code = LanguageCode.TryParse("FI");
            Assert.AreEqual("fi", code.ToBcp47Alpha2String());
        }

        [TestMethod]
        public void TestLanguageCodeToStringBCP47Alpha2_2parts()
        {
            LanguageCode code = LanguageCode.TryParse("PT_BR");
            Assert.AreEqual("pt-BR", code.ToBcp47Alpha2String());
        }

        [TestMethod]
        public void TestLanguageCodeToStringBCP47Alpha2_3parts()
        {
            LanguageCode code = LanguageCode.TryParse("ZH_Hans_tw");
            Assert.AreEqual("zh-Hans-TW", code.ToBcp47Alpha2String());
        }

        [TestMethod]
        public void TestLanguageCodeToStringBCP47Alpha3_1part()
        {
            LanguageCode code = LanguageCode.TryParse("FI");
            Assert.AreEqual("fin", code.ToBcp47Alpha3String());
        }

        [TestMethod]
        public void TestLanguageCodeToStringBCP47Alpha3_2parts()
        {
            LanguageCode code = LanguageCode.TryParse("PT_BR");
            Assert.AreEqual("por-BRA", code.ToBcp47Alpha3String());
        }

        [TestMethod]
        public void TestLanguageCodeToStringBCP47Alpha3_3parts()
        {
            LanguageCode code = LanguageCode.TryParse("SR_Cyrl_RS");
            Assert.AreEqual("srp-Cyrl-SRB", code.ToBcp47Alpha3String());
        }

        [TestMethod]
        public void TestLanguageCodeCustomTwoCharOnly()
        {
            LanguageCode code = LanguageCode.CreateCustom("NP", null);
            Assert.AreEqual("np", code.ToBcp47Alpha2String());
            Assert.AreEqual(string.Empty, code.ToBcp47Alpha3String());
            Assert.AreEqual("np", code.ToString());
            Assert.AreEqual("np", code.Iso639_1);
            Assert.AreEqual(string.Empty, code.Iso639_2);
            Assert.AreEqual(string.Empty, code.Iso639_2B);
        }

        [TestMethod]
        public void TestLanguageCodeCustomThreeCharOnly()
        {
            LanguageCode code = LanguageCode.CreateCustom(null, "NEO");
            Assert.AreEqual(string.Empty, code.ToBcp47Alpha2String());
            Assert.AreEqual("neo", code.ToBcp47Alpha3String());
            Assert.AreEqual("neo", code.ToString());
            Assert.AreEqual(string.Empty, code.Iso639_1);
            Assert.AreEqual("neo", code.Iso639_2);
            Assert.AreEqual(string.Empty, code.Iso639_2B);
        }

        [TestMethod]
        public void TestLanguageCodeCustomNullStringCodes()
        {
            try
            {
                LanguageCode code = LanguageCode.CreateCustom(null, null);
                Assert.Fail("Expected an ArgumentNullException");
            }
            catch (ArgumentNullException) { }
        }

        [TestMethod]
        public void TestLanguageCodeCustomInvalidLength2()
        {
            try
            {
                LanguageCode code = LanguageCode.CreateCustom("BAD", "NEO");
                Assert.Fail("Expected an ArgumentException");
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void TestLanguageCodeCustomInvalidLength3()
        {
            try
            {
                LanguageCode code = LanguageCode.CreateCustom("NP", "BADD");
                Assert.Fail("Expected an ArgumentException");
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void TestLanguageCodeCustomScript()
        {
            LanguageCode code = LanguageCode.CreateCustomWithScriptAndRegion("NP", "NEO", "Latn", null);
            Assert.AreEqual("np-Latn", code.ToBcp47Alpha2String());
            Assert.AreEqual("neo-Latn", code.ToBcp47Alpha3String());
            Assert.AreEqual("np-Latn", code.ToString());
            Assert.AreEqual("Latn", code.Script);
        }

        [TestMethod]
        public void TestLanguageCodeCustomRegion()
        {
            LanguageCode code = LanguageCode.CreateCustomWithScriptAndRegion("NP", "NEO", null, RegionCode.JAPAN);
            Assert.AreEqual("np-JP", code.ToBcp47Alpha2String());
            Assert.AreEqual("neo-JPN", code.ToBcp47Alpha3String());
            Assert.AreEqual("np-JP", code.ToString());
            Assert.AreEqual(string.Empty, code.Script);
        }

        [TestMethod]
        public void TestLanguageCodeCustomScriptAndRegion()
        {
            LanguageCode code = LanguageCode.CreateCustomWithScriptAndRegion("NP", "NEO", "Latn", RegionCode.JAPAN);
            Assert.AreEqual("np-Latn-JP", code.ToBcp47Alpha2String());
            Assert.AreEqual("neo-Latn-JPN", code.ToBcp47Alpha3String());
            Assert.AreEqual("np-Latn-JP", code.ToString());
        }

        [TestMethod]
        public void TestLanguageCodeCountryAgnostic()
        {
            LanguageCode baseCode = LanguageCode.ENGLISH;
            LanguageCode specificCode = baseCode.InCountry(RegionCode.BRAZIL);
            Assert.AreEqual("en", baseCode.ToBcp47Alpha2String());
            Assert.AreEqual("en-BR", specificCode.ToBcp47Alpha2String());
            Assert.AreNotEqual(baseCode, specificCode);
            LanguageCode agnostic = specificCode.CountryAgnostic();
            Assert.AreEqual(baseCode, agnostic);
            Assert.AreEqual("en", agnostic.ToBcp47Alpha2String());
        }

        [TestMethod]
        public void TestLanguageCodeEquals()
        {
            Assert.AreEqual(LanguageCode.FRENCH, LanguageCode.FRENCH);
            Assert.AreNotEqual(LanguageCode.SWAHILI, LanguageCode.GREEK_MODERN);
            Assert.IsTrue(LanguageCode.GREEK_MODERN.Equals(LanguageCode.GREEK_MODERN));
            Assert.IsFalse(LanguageCode.ZULU.Equals(LanguageCode.CHINESE));
            Assert.AreEqual(LanguageCode.FRENCH.GetHashCode(), LanguageCode.FRENCH.GetHashCode());
            Assert.AreEqual(LanguageCode.FRENCH.GetHashCode(), LanguageCode.TryParse("FRA").GetHashCode());
            Assert.AreNotEqual(LanguageCode.FRENCH.GetHashCode(), LanguageCode.GERMAN.GetHashCode());

            LanguageCode fancyHash1 = LanguageCode.CreateCustomWithScriptAndRegion("NP", "NEO", "Latn", RegionCode.JAPAN);
            LanguageCode fancyHash2 = LanguageCode.CreateCustomWithScriptAndRegion("NP", "NEO", "Latn", RegionCode.JAPAN);
            Assert.AreEqual(fancyHash1, fancyHash2);
            Assert.AreEqual(fancyHash1.GetHashCode(), fancyHash2.GetHashCode());

            fancyHash1 = LanguageCode.CreateCustomWithScriptAndRegion("NP", "NEO", "Latn", null);
            fancyHash2 = LanguageCode.CreateCustomWithScriptAndRegion("NP", "NEO", "Latn", null);
            Assert.AreEqual(fancyHash1, fancyHash2);
            Assert.AreEqual(fancyHash1.GetHashCode(), fancyHash2.GetHashCode());

            fancyHash1 = LanguageCode.CreateCustomWithScriptAndRegion("NP", "NEO", null, RegionCode.JAPAN);
            fancyHash2 = LanguageCode.CreateCustomWithScriptAndRegion("NP", "NEO", null, RegionCode.JAPAN);
            Assert.AreEqual(fancyHash1, fancyHash2);
            Assert.AreEqual(fancyHash1.GetHashCode(), fancyHash2.GetHashCode());

            fancyHash1 = LanguageCode.CreateCustomWithScriptAndRegion("NP", "NEO", "Latn", null);
            fancyHash2 = LanguageCode.CreateCustomWithScriptAndRegion("NP", "NEO", null, RegionCode.JAPAN);
            Assert.AreNotEqual(fancyHash1, fancyHash2);
            Assert.AreNotEqual(fancyHash1.GetHashCode(), fancyHash2.GetHashCode());

            LanguageCode lang = null;
            object obj = null;
            Assert.IsFalse(LanguageCode.CHINESE.Equals(lang));
            Assert.IsFalse(LanguageCode.CHINESE.Equals(obj));
            lang = LanguageCode.GERMAN;
            obj = LanguageCode.GERMAN;
            Assert.IsFalse(LanguageCode.CHINESE.Equals(lang));
            Assert.IsFalse(LanguageCode.CHINESE.Equals(obj));
            lang = LanguageCode.CHINESE;
            obj = LanguageCode.CHINESE;
            Assert.IsTrue(LanguageCode.CHINESE.Equals(lang));
            Assert.IsTrue(LanguageCode.CHINESE.Equals(obj));
        }

        [TestMethod]
        public void TestLanguageCodeParseExact()
        {
            LanguageCode code = LanguageCode.Parse("zho");
            Assert.IsNotNull(code);

            try
            {
                LanguageCode.Parse("bad");
                Assert.Fail("Expected an ArgumentException");
            }
            catch (ArgumentException) { }
        }




        [TestMethod]
        public void TestRegionCodeTryParseAlpha2()
        {
            RegionCode code = RegionCode.TryParse("MX");
            Assert.AreEqual(RegionCode.MEXICO, code);
        }

        [TestMethod]
        public void TestRegionCodeTryParseAlpha2Lower()
        {
            RegionCode code = RegionCode.TryParse("mx");
            Assert.AreEqual(RegionCode.MEXICO, code);
        }

        [TestMethod]
        public void TestRegionCodeTryParseAlpha3()
        {
            RegionCode code = RegionCode.TryParse("MNE");
            Assert.AreEqual(RegionCode.MONTENEGRO, code);
        }

        [TestMethod]
        public void TestRegionCodeTryParseAlpha3Lower()
        {
            RegionCode code = RegionCode.TryParse("mne");
            Assert.AreEqual(RegionCode.MONTENEGRO, code);
        }

        [TestMethod]
        public void TestRegionCodeTryParseNumericUnpadded()
        {
            RegionCode code = RegionCode.TryParse("36");
            Assert.AreEqual(RegionCode.AUSTRALIA, code);
        }

        [TestMethod]
        public void TestRegionCodeTryParseNumericPadded()
        {
            RegionCode code = RegionCode.TryParse("036");
            Assert.AreEqual(RegionCode.AUSTRALIA, code);
        }

        [TestMethod]
        public void TestRegionCodeTryParseNumeric()
        {
            RegionCode code = RegionCode.TryParse("392");
            Assert.AreEqual(RegionCode.JAPAN, code);
        }

        [TestMethod]
        public void TestRegionCodeTryParseNonExistentCountry()
        {
            RegionCode code = RegionCode.TryParse("HAX");
            Assert.IsNull(code);
        }

        [TestMethod]
        public void TestRegionCodeTryParseEmptyString()
        {
            RegionCode code = RegionCode.TryParse(string.Empty);
            Assert.IsNull(code);
        }

        [TestMethod]
        public void TestRegionCodeTryParseNull()
        {
            RegionCode code = RegionCode.TryParse(null);
            Assert.IsNull(code);
        }

        [TestMethod]
        public void TestRegionCodeTryParseFuzz()
        {
            byte[] randField = new byte[1000];
            IRandom rand = new FastRandom();
            for (int pass = 0; pass < 10000; pass++)
            {
                int length = rand.NextInt(1, 1000);
                for (int c = 0; c < length; c++)
                {
                    randField[c] = (byte)rand.NextInt(1, 128);
                }

                RegionCode.TryParse(Encoding.ASCII.GetString(randField, 0, length));
            }
        }

        [TestMethod]
        public void TestRegionCode3166Equality()
        {
            Assert.AreEqual(RegionCode.HUNGARY, RegionCode.HUNGARY);
            Assert.AreNotEqual(RegionCode.MEXICO, RegionCode.EGYPT);
            Assert.IsTrue(RegionCode.GREECE.Equals(RegionCode.GREECE));
            Assert.IsFalse(RegionCode.EL_SALVADOR.Equals(RegionCode.QATAR));
            Assert.AreEqual(RegionCode.HUNGARY.GetHashCode(), RegionCode.HUNGARY.GetHashCode());
            Assert.AreEqual(RegionCode.HUNGARY.GetHashCode(), RegionCode.TryParse("HUN").GetHashCode());
            Assert.AreNotEqual(RegionCode.MEXICO.GetHashCode(), RegionCode.EGYPT.GetHashCode());

            RegionCode country = RegionCode.BRAZIL;
            object obj = RegionCode.BRAZIL;
            Assert.IsFalse(RegionCode.MEXICO.Equals(country));
            Assert.IsFalse(RegionCode.MEXICO.Equals(obj));
            country = RegionCode.MEXICO;
            obj = RegionCode.MEXICO;
            Assert.IsTrue(RegionCode.MEXICO.Equals(country));
            Assert.IsTrue(RegionCode.MEXICO.Equals(obj));
        }

        [TestMethod]
        public void TestRegionCodeM49Equality()
        {
            Assert.AreEqual(RegionCode.REGION_ASIA, RegionCode.REGION_ASIA);
            Assert.AreNotEqual(RegionCode.REGION_ASIA, RegionCode.REGION_CARIBBEAN);
            Assert.IsTrue(RegionCode.REGION_CARIBBEAN.Equals(RegionCode.REGION_CARIBBEAN));
            Assert.IsFalse(RegionCode.REGION_ASIA.Equals(RegionCode.REGION_CARIBBEAN));
            Assert.AreEqual(RegionCode.REGION_MICRONESIA.GetHashCode(), RegionCode.REGION_MICRONESIA.GetHashCode());
            Assert.AreEqual(RegionCode.REGION_MICRONESIA.GetHashCode(), RegionCode.TryParse("057").GetHashCode());
            Assert.AreNotEqual(RegionCode.REGION_ASIA.GetHashCode(), RegionCode.REGION_CARIBBEAN.GetHashCode());
        }

        [TestMethod]
        public void TestRegionCodeWeirdEquality()
        {
            Assert.IsTrue(RegionCode.CreateCustomIso3166("NP", "NEO", 0).Equals(RegionCode.CreateCustomIso3166("NP", "NEO", 0)));
            Assert.IsTrue(RegionCode.CreateCustomIso3166("NP", "NEO", 316).Equals(RegionCode.CreateCustomIso3166("np", "neo", 316)));
            Assert.IsTrue(RegionCode.CreateCustomIso3166("NP", "NEO", 316).Equals(RegionCode.CreateCustomUNM49(316)));
        }

        [TestMethod]
        public void TestRegionCodeEqualsNull()
        {
            object obj = null;
            RegionCode country = null;
            Assert.IsFalse(RegionCode.MEXICO.Equals(obj));
            Assert.IsFalse(RegionCode.MEXICO.Equals(country));
        }

        [TestMethod]
        public void TestRegionCodeEqualsUnrelatedObject()
        {
            Assert.IsFalse(RegionCode.BELIZE.Equals("something else"));
        }

        [TestMethod]
        public void TestRegionCodeAlpha3ToString()
        {
            Assert.AreEqual("MEX", RegionCode.MEXICO.ToString());
        }

        [TestMethod]
        public void TestRegionCodeNumericToString()
        {
            Assert.AreEqual("029", RegionCode.REGION_CARIBBEAN.ToString());
        }

        [TestMethod]
        public void TestRegionCodeCustomISO3166()
        {
            RegionCode customRegion = RegionCode.CreateCustomIso3166("NP", "NEO", 965);
            Assert.AreEqual("NP", customRegion.Iso3166_1_Alpha2);
            Assert.AreEqual("NEO", customRegion.Iso3166_1_Alpha3);
            Assert.AreEqual(965, customRegion.NumericCode);
            Assert.AreEqual("NEO", customRegion.ToString());
            Assert.IsFalse(customRegion.IsUN_M49Code);
        }

        [TestMethod]
        public void TestRegionCodeCustomUNM49()
        {
            RegionCode customRegion = RegionCode.CreateCustomUNM49(001);
            Assert.AreEqual(null, customRegion.Iso3166_1_Alpha2);
            Assert.AreEqual(null, customRegion.Iso3166_1_Alpha3);
            Assert.AreEqual(001, customRegion.NumericCode);
            Assert.AreEqual("001", customRegion.ToString());
            Assert.IsTrue(customRegion.IsUN_M49Code);
        }

        [TestMethod]
        public void TestRegionCodeCustomISO3166_NullAlpha2()
        {
            try
            {
                RegionCode customRegion = RegionCode.CreateCustomIso3166(null, "NEO", 965);
                Assert.Fail("Expected an ArgumentNullException");
            }
            catch (ArgumentNullException) { }
        }

        [TestMethod]
        public void TestRegionCodeCustomISO3166_NullAlpha3()
        {
            try
            {
                RegionCode customRegion = RegionCode.CreateCustomIso3166("NP", null, 965);
                Assert.Fail("Expected an ArgumentNullException");
            }
            catch (ArgumentNullException) { }
        }

        [TestMethod]
        public void TestRegionCodeCustomISO3166_InvalidLength2()
        {
            try
            {
                RegionCode customRegion = RegionCode.CreateCustomIso3166("BAD", "NEO", 965);
                Assert.Fail("Expected an ArgumentException");
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void TestRegionCodeCustomISO3166_InvalidLength3()
        {
            try
            {
                RegionCode customRegion = RegionCode.CreateCustomIso3166("NP", "BADD", 965);
                Assert.Fail("Expected an ArgumentException");
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void TestRegionCodeCustomISO3166_InvalidNumericCode()
        {
            try
            {
                RegionCode customRegion = RegionCode.CreateCustomIso3166("NP", "NEO", 1000);
                Assert.Fail("Expected an ArgumentOutOfRangeException");
            }
            catch (ArgumentOutOfRangeException) { }
        }

        [TestMethod]
        public void TestRegionCodeCustomUNM49_InvalidNumericCode()
        {
            try
            {
                RegionCode customRegion = RegionCode.CreateCustomUNM49(1000);
                Assert.Fail("Expected an ArgumentOutOfRangeException");
            }
            catch (ArgumentOutOfRangeException) { }
        }
    }
}
