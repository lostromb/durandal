using Durandal.Common.Collections.Interning;
using Durandal.Common.Collections.Interning.Impl;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Durandal.Common.NLP.Language
{
    // TODO- Not sure if it would be worth refactoring into:
    // RegionCode (Abstract class)
    //   - ISO3166RegionCode (subclass)
    //   - UNM49RegionCode (subclass)
    // The fact that there's a lot of overlap between, say, a numeric 3-digit code 
    // that could be either ISO3166 or UNM49 makes this blurry though.

    /// <summary>
    /// Class representing an ISO 3166 country / territory code, either in alpha-2, alpha-3, or numeric formats.
    /// Example: "DE", "CAN", "024".
    /// Can also be used to represent UN-M49 codes such as "419" for Latin America, or other regions broader
    /// in scope than a single country or territory.
    /// </summary>
    public class RegionCode : IEquatable<RegionCode>
    {
        private static readonly SealedInternalizer_CharIgnoreCase_PerfectHash REGION_CODE_PARSE_DICTIONARY;
        private static readonly RegionCode[] REGION_CODE_TABLE;

        /// <summary>
        /// Initialize parse dictionaries when this class is first touched
        /// </summary>
        static RegionCode()
        {
            var internalizerData = PrepareInternalizer();
            REGION_CODE_PARSE_DICTIONARY = internalizerData.Item1;
            REGION_CODE_TABLE = internalizerData.Item2;
        }

        /// <summary>
        /// This country code in ISO 3166-1 alpha-2 form, e.g. "AU" (Australia)
        /// </summary>
        public string Iso3166_1_Alpha2 { get; private set; }

        /// <summary>
        /// This country code in ISO 3166-1 alpha-3 form, e.g. "CAN" (Canada)
        /// </summary>
        public string Iso3166_1_Alpha3 { get; private set; }

        /// <summary>
        /// This country code in either ISO 3166-1 numeric form, e.g. 246 (Finland),
        /// or less commonly using a UN-M49 code which is typically used to denote
        /// broader regions such as "419" for Latin America.
        /// Typically this number is zero-padded to 3 digits in written form, use
        /// ToString("D3", CultureInfo.InvariantCulture) to achieve this.
        /// </summary>
        public ushort NumericCode { get; private set; }

        /// <summary>
        /// Indicates whether this region's numeric code is a UN-M49 code or a more common
        /// ISO-3166-1 numeric code.
        /// </summary>
        public bool IsUN_M49Code { get; private set; }

        private RegionCode(string iso3166_1_alpha2, string iso3166_1_alpha3, ushort iso3166_1_numeric)
        {
            Iso3166_1_Alpha2 = iso3166_1_alpha2.AssertNonNull(nameof(iso3166_1_alpha2));
            Iso3166_1_Alpha3 = iso3166_1_alpha3.AssertNonNull(nameof(iso3166_1_alpha3));
            NumericCode = iso3166_1_numeric;
            IsUN_M49Code = false;

            if (Iso3166_1_Alpha2.Length != 2)
            {
                throw new ArgumentException($"ISO-3166-1 Alpha-2 country code MUST be exactly two characters long, e.g. \"jp\": got \"{Iso3166_1_Alpha2}\"", nameof(Iso3166_1_Alpha2));
            }

            if (Iso3166_1_Alpha3.Length != 3)
            {
                throw new ArgumentException($"ISO-3166-1 Alpha-3 country code MUST be exactly three characters long, e.g. \"jpn\": got \"{Iso3166_1_Alpha3}\"", nameof(Iso3166_1_Alpha3));
            }

            if (NumericCode >= 1000)
            {
                throw new ArgumentOutOfRangeException($"ISO-3166-1 numeric country code must be less than 4 digits long: got {NumericCode}", nameof(NumericCode));
            }
        }

        private RegionCode(ushort unm49)
        {
            Iso3166_1_Alpha2 = null;
            Iso3166_1_Alpha3 = null;
            NumericCode = unm49;
            IsUN_M49Code = true;

            if (NumericCode >= 1000)
            {
                throw new ArgumentOutOfRangeException($"Un-M49 numeric country code must be less than 4 digits long: got {NumericCode}", nameof(NumericCode));
            }
        }

        /// <summary>
        /// <b>Usually you should not need to call this method.</b>
        /// Use <see cref="TryParse(string)"/> or one of the static country codes that already exist.
        /// This is intended for creating custom country codes for niche purposes,
        /// such as handling non-standard country codes that originate from external systems.
        /// </summary>
        /// <param name="iso3166_1_alpha2">The ISO-3166-1 alpha-2 code, which is the two-char code such as "fr".</param>
        /// <param name="iso3166_1_alpha3">The ISO-3166-1 alpha-3 code, which is the three-char code such as "fra".</param>
        /// <param name="iso3166_1_numeric">The ISO-3166-1 numeric code, such 250.</param>
        public static RegionCode CreateCustomIso3166(string iso3166_1_alpha2, string iso3166_1_alpha3, ushort iso3166_1_numeric)
        {
            return new RegionCode(iso3166_1_alpha2, iso3166_1_alpha3, iso3166_1_numeric);
        }

        /// <summary>
        /// <b>Usually you should not need to call this method.</b>
        /// Use <see cref="TryParse(string)"/> or one of the static country codes that already exist.
        /// This is intended for creating custom country codes for niche purposes,
        /// such as handling non-standard country codes that originate from external systems.
        /// </summary>
        /// <param name="un_m49_numeric">The UN-M49 numeric code, such 419.</param>
        public static RegionCode CreateCustomUNM49(ushort un_m49_numeric)
        {
            return new RegionCode(un_m49_numeric);
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            return Equals(obj as RegionCode);
        }

        public bool Equals(RegionCode other)
        {
            if (other == null)
            {
                return false;
            }
            
            if (NumericCode > 0)
            {
                // this allows comparison between 3166 and M49 codes
                return NumericCode == other.NumericCode;
            }
            else
            {
                // allow comparison between predefined codes and custom-created codes if that comes up somehow
                return string.Equals(Iso3166_1_Alpha3, other.Iso3166_1_Alpha3, StringComparison.OrdinalIgnoreCase);
            }
        }

        public override int GetHashCode()
        {
            if (IsUN_M49Code)
            {
                return NumericCode.GetHashCode();
            }
            else
            {
                return Iso3166_1_Alpha3.GetHashCode();
            }
        }

        /// <summary>
        /// The default implementation of CountryCode.ToString() returns the Alpha-3 format,
        /// or the numeric format if not available (such as with UN-M49 region codes that do
        /// not have an alphabetic representation)
        /// </summary>
        /// <returns>The string representation of this country code.</returns>
        public override string ToString()
        {
            if (!string.IsNullOrEmpty(Iso3166_1_Alpha3))
            {
                return Iso3166_1_Alpha3;
            }
            //else if (!string.IsNullOrEmpty(Iso3166_1_Alpha2))
            //{
            //    return Iso3166_1_Alpha2;
            //}
            else
            {
                return NumericCode.ToString("D3", CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// Attempts to parse a country code, either in alpha-2, alpha-3, or numeric format,
        /// into a structured country code. If no country matches with what is currently registered,
        /// this method returns null.
        /// </summary>
        /// <param name="code">The code to try and parse (case insensitive), such as "BR", "ISL", "442"</param>
        /// <returns>The matching country code, or null if not found</returns>
        public static RegionCode TryParse(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                return null;
            }

            return TryParse(code.AsSpan());
        }

        public static RegionCode TryParse(ReadOnlySpan<char> code)
        {
            InternedKey<ReadOnlyMemory<char>> key;
            if (REGION_CODE_PARSE_DICTIONARY.TryGetInternalizedKey(code, out key))
            {
                return REGION_CODE_TABLE[key.Key];
            }

            return null;
        }

        public static readonly RegionCode UNKNOWN_COUNTRY                              = new RegionCode("XX", "XXX", 000);

        #region ISO Country codes
        public static readonly RegionCode AFGHANISTAN									= new RegionCode("AF", "AFG", 004);
        public static readonly RegionCode ALAND_ISLANDS							    	= new RegionCode("AX", "ALA", 248);
        public static readonly RegionCode ALBANIA										= new RegionCode("AL", "ALB", 008);
        public static readonly RegionCode ALGERIA										= new RegionCode("DZ", "DZA", 012);
        public static readonly RegionCode AMERICAN_SAMOA								= new RegionCode("AS", "ASM", 016);
        public static readonly RegionCode ANDORRA										= new RegionCode("AD", "AND", 020);
        public static readonly RegionCode ANGOLA										= new RegionCode("AO", "AGO", 024);
        public static readonly RegionCode ANGUILLA										= new RegionCode("AI", "AIA", 660);
        public static readonly RegionCode ANTARCTICA									= new RegionCode("AQ", "ATA", 010);
        public static readonly RegionCode ANTIGUA_AND_BARBUDA							= new RegionCode("AG", "ATG", 028);
        public static readonly RegionCode ARGENTINA								    	= new RegionCode("AR", "ARG", 032);
        public static readonly RegionCode ARMENIA										= new RegionCode("AM", "ARM", 051);
        public static readonly RegionCode ARUBA									    	= new RegionCode("AW", "ABW", 533);
        public static readonly RegionCode AUSTRALIA								    	= new RegionCode("AU", "AUS", 036);
        public static readonly RegionCode AUSTRIA										= new RegionCode("AT", "AUT", 040);
        public static readonly RegionCode AZERBAIJAN									= new RegionCode("AZ", "AZE", 031);
        public static readonly RegionCode BAHAMAS										= new RegionCode("BS", "BHS", 044);
        public static readonly RegionCode BAHRAIN										= new RegionCode("BH", "BHR", 048);
        public static readonly RegionCode BANGLADESH									= new RegionCode("BD", "BGD", 050);
        public static readonly RegionCode BARBADOS										= new RegionCode("BB", "BRB", 052);
        public static readonly RegionCode BELARUS										= new RegionCode("BY", "BLR", 112);
        public static readonly RegionCode BELGIUM										= new RegionCode("BE", "BEL", 056);
        public static readonly RegionCode BELIZE										= new RegionCode("BZ", "BLZ", 084);
        public static readonly RegionCode BENIN									    	= new RegionCode("BJ", "BEN", 204);
        public static readonly RegionCode BERMUDA										= new RegionCode("BM", "BMU", 060);
        public static readonly RegionCode BHUTAN										= new RegionCode("BT", "BTN", 064);
        public static readonly RegionCode BOLIVIA										= new RegionCode("BO", "BOL", 068);
        public static readonly RegionCode BONAIRE_SINT_EUSTATIUS_AND_SABA	    		= new RegionCode("BQ", "BES", 535);
        public static readonly RegionCode BOSNIA_AND_HERZEGOVINA						= new RegionCode("BA", "BIH", 070);
        public static readonly RegionCode BOTSWANA										= new RegionCode("BW", "BWA", 072);
        public static readonly RegionCode BOUVET_ISLAND							    	= new RegionCode("BV", "BVT", 074);
        public static readonly RegionCode BRAZIL										= new RegionCode("BR", "BRA", 076);
        public static readonly RegionCode BRITISH_INDIAN_OCEAN_TERRITORY				= new RegionCode("IO", "IOT", 086);
        public static readonly RegionCode BRITISH_VIRGIN_ISLANDS						= new RegionCode("VG", "VGB", 092);
        public static readonly RegionCode BRUNEI_DARUSSALAM							    = new RegionCode("BN", "BRN", 096);
        public static readonly RegionCode BULGARIA										= new RegionCode("BG", "BGR", 100);
        public static readonly RegionCode BURKINA_FASO									= new RegionCode("BF", "BFA", 854);
        public static readonly RegionCode BURUNDI										= new RegionCode("BI", "BDI", 108);
        public static readonly RegionCode COTE_D_IVOIRE							    	= new RegionCode("CI", "CIV", 384);
        public static readonly RegionCode CABO_VERDE									= new RegionCode("CV", "CPV", 132);
        public static readonly RegionCode CAMBODIA										= new RegionCode("KH", "KHM", 116);
        public static readonly RegionCode CAMEROON										= new RegionCode("CM", "CMR", 120);
        public static readonly RegionCode CANADA										= new RegionCode("CA", "CAN", 124);
        public static readonly RegionCode CAYMAN_ISLANDS								= new RegionCode("KY", "CYM", 136);
        public static readonly RegionCode CENTRAL_AFRICAN_REPUBLIC						= new RegionCode("CF", "CAF", 140);
        public static readonly RegionCode CHAD											= new RegionCode("TD", "TCD", 148);
        public static readonly RegionCode CHILE									    	= new RegionCode("CL", "CHL", 152);
        public static readonly RegionCode CHINA								    		= new RegionCode("CN", "CHN", 156);
        public static readonly RegionCode CHRISTMAS_ISLAND								= new RegionCode("CX", "CXR", 162);
        public static readonly RegionCode COCOS_KEELING_ISLANDS				    		= new RegionCode("CC", "CCK", 166);
        public static readonly RegionCode COLOMBIA										= new RegionCode("CO", "COL", 170);
        public static readonly RegionCode COMOROS										= new RegionCode("KM", "COM", 174);
        public static readonly RegionCode COOK_ISLANDS									= new RegionCode("CK", "COK", 184);
        public static readonly RegionCode COSTA_RICA									= new RegionCode("CR", "CRI", 188);
        public static readonly RegionCode CROATIA										= new RegionCode("HR", "HRV", 191);
        public static readonly RegionCode CUBA											= new RegionCode("CU", "CUB", 192);
        public static readonly RegionCode CURACAO										= new RegionCode("CW", "CUW", 531);
        public static readonly RegionCode CYPRUS										= new RegionCode("CY", "CYP", 196);
        public static readonly RegionCode CZECHIA										= new RegionCode("CZ", "CZE", 203);
        public static readonly RegionCode DEMOCRATIC_REPUBLIC_OF_THE_CONGO				= new RegionCode("CD", "COD", 180);
        public static readonly RegionCode DENMARK										= new RegionCode("DK", "DNK", 208);
        public static readonly RegionCode DJIBOUTI										= new RegionCode("DJ", "DJI", 262);
        public static readonly RegionCode DOMINICA										= new RegionCode("DM", "DMA", 212);
        public static readonly RegionCode DOMINICAN_REPUBLIC							= new RegionCode("DO", "DOM", 214);
        public static readonly RegionCode DUTCH_SINT_MAARTEN							= new RegionCode("SX", "SXM", 534);
        public static readonly RegionCode ECUADOR										= new RegionCode("EC", "ECU", 218);
        public static readonly RegionCode EGYPT									    	= new RegionCode("EG", "EGY", 818);
        public static readonly RegionCode EL_SALVADOR									= new RegionCode("SV", "SLV", 222);
        public static readonly RegionCode EQUATORIAL_GUINEA						    	= new RegionCode("GQ", "GNQ", 226);
        public static readonly RegionCode ERITREA										= new RegionCode("ER", "ERI", 232);
        public static readonly RegionCode ESTONIA										= new RegionCode("EE", "EST", 233);
        public static readonly RegionCode ESWATINI										= new RegionCode("SZ", "SWZ", 748);
        public static readonly RegionCode ETHIOPIA										= new RegionCode("ET", "ETH", 231);
        public static readonly RegionCode FALKLAND_ISLANDS								= new RegionCode("FK", "FLK", 238);
        public static readonly RegionCode FAROE_ISLANDS							    	= new RegionCode("FO", "FRO", 234);
        public static readonly RegionCode FIJI											= new RegionCode("FJ", "FJI", 242);
        public static readonly RegionCode FINLAND										= new RegionCode("FI", "FIN", 246);
        public static readonly RegionCode FRANCE										= new RegionCode("FR", "FRA", 250);
        public static readonly RegionCode FRENCH_GUIANA						    		= new RegionCode("GF", "GUF", 254);
        public static readonly RegionCode FRENCH_POLYNESIA								= new RegionCode("PF", "PYF", 258);
        public static readonly RegionCode FRENCH_SAINT_MARTIN							= new RegionCode("MF", "MAF", 663);
        public static readonly RegionCode FRENCH_SOUTHERN_TERRITORIES					= new RegionCode("TF", "ATF", 260);
        public static readonly RegionCode GABON									    	= new RegionCode("GA", "GAB", 266);
        public static readonly RegionCode GAMBIA										= new RegionCode("GM", "GMB", 270);
        public static readonly RegionCode GEORGIA										= new RegionCode("GE", "GEO", 268);
        public static readonly RegionCode GERMANY										= new RegionCode("DE", "DEU", 276);
        public static readonly RegionCode GHANA										    = new RegionCode("GH", "GHA", 288);
        public static readonly RegionCode GIBRALTAR								    	= new RegionCode("GI", "GIB", 292);
        public static readonly RegionCode GREECE										= new RegionCode("GR", "GRC", 300);
        public static readonly RegionCode GREENLAND								    	= new RegionCode("GL", "GRL", 304);
        public static readonly RegionCode GRENADA										= new RegionCode("GD", "GRD", 308);
        public static readonly RegionCode GUADELOUPE									= new RegionCode("GP", "GLP", 312);
        public static readonly RegionCode GUAM											= new RegionCode("GU", "GUM", 316);
        public static readonly RegionCode GUATEMALA								    	= new RegionCode("GT", "GTM", 320);
        public static readonly RegionCode GUERNSEY										= new RegionCode("GG", "GGY", 831);
        public static readonly RegionCode GUINEA										= new RegionCode("GN", "GIN", 324);
        public static readonly RegionCode GUINEA_BISSAU						    		= new RegionCode("GW", "GNB", 624);
        public static readonly RegionCode GUYANA										= new RegionCode("GY", "GUY", 328);
        public static readonly RegionCode HAITI									    	= new RegionCode("HT", "HTI", 332);
        public static readonly RegionCode HEARD_ISLAND_AND_MCDONALD_ISLANDS		    	= new RegionCode("HM", "HMD", 334);
        public static readonly RegionCode HOLY_SEE										= new RegionCode("VA", "VAT", 336);
        public static readonly RegionCode HONDURAS										= new RegionCode("HN", "HND", 340);
        public static readonly RegionCode HONG_KONG								    	= new RegionCode("HK", "HKG", 344);
        public static readonly RegionCode HUNGARY										= new RegionCode("HU", "HUN", 348);
        public static readonly RegionCode ICELAND										= new RegionCode("IS", "ISL", 352);
        public static readonly RegionCode INDIA									    	= new RegionCode("IN", "IND", 356);
        public static readonly RegionCode INDONESIA								    	= new RegionCode("ID", "IDN", 360);
        public static readonly RegionCode IRAN											= new RegionCode("IR", "IRN", 364);
        public static readonly RegionCode IRAQ											= new RegionCode("IQ", "IRQ", 368);
        public static readonly RegionCode IRELAND										= new RegionCode("IE", "IRL", 372);
        public static readonly RegionCode ISLE_OF_MAN									= new RegionCode("IM", "IMN", 833);
        public static readonly RegionCode ISRAEL										= new RegionCode("IL", "ISR", 376);
        public static readonly RegionCode ITALY									    	= new RegionCode("IT", "ITA", 380);
        public static readonly RegionCode JAMAICA										= new RegionCode("JM", "JAM", 388);
        public static readonly RegionCode JAPAN									    	= new RegionCode("JP", "JPN", 392);
        public static readonly RegionCode JERSEY										= new RegionCode("JE", "JEY", 832);
        public static readonly RegionCode JORDAN										= new RegionCode("JO", "JOR", 400);
        public static readonly RegionCode KAZAKHSTAN									= new RegionCode("KZ", "KAZ", 398);
        public static readonly RegionCode KENYA									    	= new RegionCode("KE", "KEN", 404);
        public static readonly RegionCode KIRIBATI										= new RegionCode("KI", "KIR", 296);
        public static readonly RegionCode KUWAIT										= new RegionCode("KW", "KWT", 414);
        public static readonly RegionCode KYRGYZSTAN									= new RegionCode("KG", "KGZ", 417);
        public static readonly RegionCode LAOS											= new RegionCode("LA", "LAO", 418);
        public static readonly RegionCode LATVIA										= new RegionCode("LV", "LVA", 428);
        public static readonly RegionCode LEBANON										= new RegionCode("LB", "LBN", 422);
        public static readonly RegionCode LESOTHO										= new RegionCode("LS", "LSO", 426);
        public static readonly RegionCode LIBERIA										= new RegionCode("LR", "LBR", 430);
        public static readonly RegionCode LIBYA										    = new RegionCode("LY", "LBY", 434);
        public static readonly RegionCode LIECHTENSTEIN								    = new RegionCode("LI", "LIE", 438);
        public static readonly RegionCode LITHUANIA									    = new RegionCode("LT", "LTU", 440);
        public static readonly RegionCode LUXEMBOURG									= new RegionCode("LU", "LUX", 442);
        public static readonly RegionCode MACAO										    = new RegionCode("MO", "MAC", 446);
        public static readonly RegionCode MADAGASCAR									= new RegionCode("MG", "MDG", 450);
        public static readonly RegionCode MALAWI										= new RegionCode("MW", "MWI", 454);
        public static readonly RegionCode MALAYSIA										= new RegionCode("MY", "MYS", 458);
        public static readonly RegionCode MALDIVES										= new RegionCode("MV", "MDV", 462);
        public static readonly RegionCode MALI											= new RegionCode("ML", "MLI", 466);
        public static readonly RegionCode MALTA										    = new RegionCode("MT", "MLT", 470);
        public static readonly RegionCode MARSHALL_ISLANDS								= new RegionCode("MH", "MHL", 584);
        public static readonly RegionCode MARTINIQUE									= new RegionCode("MQ", "MTQ", 474);
        public static readonly RegionCode MAURITANIA									= new RegionCode("MR", "MRT", 478);
        public static readonly RegionCode MAURITIUS									    = new RegionCode("MU", "MUS", 480);
        public static readonly RegionCode MAYOTTE										= new RegionCode("YT", "MYT", 175);
        public static readonly RegionCode MEXICO										= new RegionCode("MX", "MEX", 484);
        public static readonly RegionCode MICRONESIA									= new RegionCode("FM", "FSM", 583);
        public static readonly RegionCode MOLDOVA										= new RegionCode("MD", "MDA", 498);
        public static readonly RegionCode MONACO										= new RegionCode("MC", "MCO", 492);
        public static readonly RegionCode MONGOLIA										= new RegionCode("MN", "MNG", 496);
        public static readonly RegionCode MONTENEGRO									= new RegionCode("ME", "MNE", 499);
        public static readonly RegionCode MONTSERRAT									= new RegionCode("MS", "MSR", 500);
        public static readonly RegionCode MOROCCO										= new RegionCode("MA", "MAR", 504);
        public static readonly RegionCode MOZAMBIQUE									= new RegionCode("MZ", "MOZ", 508);
        public static readonly RegionCode MYANMAR										= new RegionCode("MM", "MMR", 104);
        public static readonly RegionCode NAMIBIA										= new RegionCode("NA", "NAM", 516);
        public static readonly RegionCode NAURU										    = new RegionCode("NR", "NRU", 520);
        public static readonly RegionCode NEPAL										    = new RegionCode("NP", "NPL", 524);
        public static readonly RegionCode NETHERLANDS								   	= new RegionCode("NL", "NLD", 528);
        public static readonly RegionCode NEW_CALEDONIA								    = new RegionCode("NC", "NCL", 540);
        public static readonly RegionCode NEW_ZEALAND									= new RegionCode("NZ", "NZL", 554);
        public static readonly RegionCode NICARAGUA								    	= new RegionCode("NI", "NIC", 558);
        public static readonly RegionCode NIGER									    	= new RegionCode("NE", "NER", 562);
        public static readonly RegionCode NIGERIA										= new RegionCode("NG", "NGA", 566);
        public static readonly RegionCode NIUE											= new RegionCode("NU", "NIU", 570);
        public static readonly RegionCode NORFOLK_ISLAND								= new RegionCode("NF", "NFK", 574);
        public static readonly RegionCode NORTH_KOREA									= new RegionCode("KP", "PRK", 408);
        public static readonly RegionCode NORTH_MACEDONIA								= new RegionCode("MK", "MKD", 807);
        public static readonly RegionCode NORTHERN_MARIANA_ISLANDS						= new RegionCode("MP", "MNP", 580);
        public static readonly RegionCode NORWAY										= new RegionCode("NO", "NOR", 578);
        public static readonly RegionCode OMAN											= new RegionCode("OM", "OMN", 512);
        public static readonly RegionCode PAKISTAN										= new RegionCode("PK", "PAK", 586);
        public static readonly RegionCode PALAU									    	= new RegionCode("PW", "PLW", 585);
        public static readonly RegionCode PALESTINE									    = new RegionCode("PS", "PSE", 275);
        public static readonly RegionCode PANAMA										= new RegionCode("PA", "PAN", 591);
        public static readonly RegionCode PAPUA_NEW_GUINEA								= new RegionCode("PG", "PNG", 598);
        public static readonly RegionCode PARAGUAY										= new RegionCode("PY", "PRY", 600);
        public static readonly RegionCode PERU											= new RegionCode("PE", "PER", 604);
        public static readonly RegionCode PHILIPPINES									= new RegionCode("PH", "PHL", 608);
        public static readonly RegionCode PITCAIRN										= new RegionCode("PN", "PCN", 612);
        public static readonly RegionCode POLAND										= new RegionCode("PL", "POL", 616);
        public static readonly RegionCode PORTUGAL										= new RegionCode("PT", "PRT", 620);
        public static readonly RegionCode PUERTO_RICO									= new RegionCode("PR", "PRI", 630);
        public static readonly RegionCode QATAR										    = new RegionCode("QA", "QAT", 634);
        public static readonly RegionCode REUNION										= new RegionCode("RE", "REU", 638);
        public static readonly RegionCode REPUBLIC_OF_THE_CONGO					    	= new RegionCode("CG", "COG", 178);
        public static readonly RegionCode ROMANIA										= new RegionCode("RO", "ROU", 642);
        public static readonly RegionCode RUSSIA										= new RegionCode("RU", "RUS", 643);
        public static readonly RegionCode RWANDA										= new RegionCode("RW", "RWA", 646);
        public static readonly RegionCode SAINT_BARTHELEMY								= new RegionCode("BL", "BLM", 652);
        public static readonly RegionCode SAINT_HELENA_ASCENSION_AND_TRISTAN_DA_CUNHA	= new RegionCode("SH", "SHN", 654);
        public static readonly RegionCode SAINT_KITTS_AND_NEVIS						    = new RegionCode("KN", "KNA", 659);
        public static readonly RegionCode SAINT_LUCIA									= new RegionCode("LC", "LCA", 662);
        public static readonly RegionCode SAINT_PIERRE_AND_MIQUELON					    = new RegionCode("PM", "SPM", 666);
        public static readonly RegionCode SAINT_VINCENT_AND_THE_GRENADINES				= new RegionCode("VC", "VCT", 670);
        public static readonly RegionCode SAMOA										    = new RegionCode("WS", "WSM", 882);
        public static readonly RegionCode SAN_MARINO									= new RegionCode("SM", "SMR", 674);
        public static readonly RegionCode SAO_TOME_AND_PRINCIPE						    = new RegionCode("ST", "STP", 678);
        public static readonly RegionCode SAUDI_ARABIA									= new RegionCode("SA", "SAU", 682);
        public static readonly RegionCode SENEGAL										= new RegionCode("SN", "SEN", 686);
        public static readonly RegionCode SERBIA										= new RegionCode("RS", "SRB", 688);
        public static readonly RegionCode SEYCHELLES									= new RegionCode("SC", "SYC", 690);
        public static readonly RegionCode SIERRA_LEONE									= new RegionCode("SL", "SLE", 694);
        public static readonly RegionCode SINGAPORE									    = new RegionCode("SG", "SGP", 702);
        public static readonly RegionCode SLOVAKIA										= new RegionCode("SK", "SVK", 703);
        public static readonly RegionCode SLOVENIA										= new RegionCode("SI", "SVN", 705);
        public static readonly RegionCode SOLOMON_ISLANDS								= new RegionCode("SB", "SLB", 090);
        public static readonly RegionCode SOMALIA										= new RegionCode("SO", "SOM", 706);
        public static readonly RegionCode SOUTH_AFRICA									= new RegionCode("ZA", "ZAF", 710);
        public static readonly RegionCode SOUTH_GEORGIA_AND_SOUTH_SANDWICH_ISLANDS 	    = new RegionCode("GS", "SGS", 239);
        public static readonly RegionCode SOUTH_KOREA									= new RegionCode("KR", "KOR", 410);
        public static readonly RegionCode SOUTH_SUDAN									= new RegionCode("SS", "SSD", 728);
        public static readonly RegionCode SPAIN										    = new RegionCode("ES", "ESP", 724);
        public static readonly RegionCode SRI_LANKA									    = new RegionCode("LK", "LKA", 144);
        public static readonly RegionCode SUDAN										    = new RegionCode("SD", "SDN", 729);
        public static readonly RegionCode SURINAME										= new RegionCode("SR", "SUR", 740);
        public static readonly RegionCode SVALBARD_AND_JAN_MAYEN						= new RegionCode("SJ", "SJM", 744);
        public static readonly RegionCode SWEDEN										= new RegionCode("SE", "SWE", 752);
        public static readonly RegionCode SWITZERLAND									= new RegionCode("CH", "CHE", 756);
        public static readonly RegionCode SYRIA										    = new RegionCode("SY", "SYR", 760);
        public static readonly RegionCode TAIWAN										= new RegionCode("TW", "TWN", 158);
        public static readonly RegionCode TAJIKISTAN									= new RegionCode("TJ", "TJK", 762);
        public static readonly RegionCode TANZANIA										= new RegionCode("TZ", "TZA", 834);
        public static readonly RegionCode THAILAND										= new RegionCode("TH", "THA", 764);
        public static readonly RegionCode TIMOR_LESTE									= new RegionCode("TL", "TLS", 626);
        public static readonly RegionCode TOGO											= new RegionCode("TG", "TGO", 768);
        public static readonly RegionCode TOKELAU										= new RegionCode("TK", "TKL", 772);
        public static readonly RegionCode TONGA										    = new RegionCode("TO", "TON", 776);
        public static readonly RegionCode TRINIDAD_AND_TOBAGO							= new RegionCode("TT", "TTO", 780);
        public static readonly RegionCode TUNISIA										= new RegionCode("TN", "TUN", 788);
        public static readonly RegionCode TURKEY										= new RegionCode("TR", "TUR", 792);
        public static readonly RegionCode TURKMENISTAN									= new RegionCode("TM", "TKM", 795);
        public static readonly RegionCode TURKS_AND_CAICOS_ISLANDS						= new RegionCode("TC", "TCA", 796);
        public static readonly RegionCode TUVALU										= new RegionCode("TV", "TUV", 798);
        public static readonly RegionCode UGANDA										= new RegionCode("UG", "UGA", 800);
        public static readonly RegionCode UKRAINE										= new RegionCode("UA", "UKR", 804);
        public static readonly RegionCode UNITED_ARAB_EMIRATES							= new RegionCode("AE", "ARE", 784);
        public static readonly RegionCode UNITED_KINGDOM								= new RegionCode("GB", "GBR", 826);
        public static readonly RegionCode UNITED_STATED_MINOR_OUTLYING_ISLANDS			= new RegionCode("UM", "UMI", 581);
        public static readonly RegionCode UNITED_STATES_OF_AMERICA						= new RegionCode("US", "USA", 840);
        public static readonly RegionCode URUGUAY										= new RegionCode("UY", "URY", 858);
        public static readonly RegionCode US_VIRGIN_ISLANDS							    = new RegionCode("VI", "VIR", 850);
        public static readonly RegionCode UZBEKISTAN									= new RegionCode("UZ", "UZB", 860);
        public static readonly RegionCode VANUATU										= new RegionCode("VU", "VUT", 548);
        public static readonly RegionCode VENEZUELA									    = new RegionCode("VE", "VEN", 862);
        public static readonly RegionCode VIETNAM										= new RegionCode("VN", "VNM", 704);
        public static readonly RegionCode WALLIS_AND_FUTUNA							    = new RegionCode("WF", "WLF", 876);
        public static readonly RegionCode WESTERN_SAHARA								= new RegionCode("EH", "ESH", 732);
        public static readonly RegionCode YEMEN										    = new RegionCode("YE", "YEM", 887);
        public static readonly RegionCode ZAMBIA										= new RegionCode("ZM", "ZMB", 894);
        public static readonly RegionCode ZIMBABWE										= new RegionCode("ZW", "ZWE", 716);
        #endregion

        #region UN M-49 regions

        public static readonly RegionCode REGION_AFRICA                             = new RegionCode(002);
        public static readonly RegionCode REGION_AMERICAS                           = new RegionCode(019);
        public static readonly RegionCode REGION_ASIA                               = new RegionCode(142);
        public static readonly RegionCode REGION_AUSTRALIA_AND_NEW_ZEALAND          = new RegionCode(053);
        public static readonly RegionCode REGION_CARIBBEAN                          = new RegionCode(029);
        public static readonly RegionCode REGION_CENTRAL_AMERICA                    = new RegionCode(013);
        public static readonly RegionCode REGION_CENTRAL_ASIA                       = new RegionCode(143);
        public static readonly RegionCode REGION_EASTERN_AFRICA                     = new RegionCode(014);
        public static readonly RegionCode REGION_EASTERN_ASIA                       = new RegionCode(030);
        public static readonly RegionCode REGION_EASTERN_EUROPE                     = new RegionCode(151);
        public static readonly RegionCode REGION_EUROPE                             = new RegionCode(150);
        public static readonly RegionCode REGION_LANDLOCKED_DEVELOPING_COUNTRIES    = new RegionCode(432);
        public static readonly RegionCode REGION_LATIN_AMERICA_CARIBBEAN            = new RegionCode(419);
        public static readonly RegionCode REGION_LEAST_DEVELOPED_COUNTRIES          = new RegionCode(199);
        public static readonly RegionCode REGION_MELANESIA                          = new RegionCode(054);
        public static readonly RegionCode REGION_MICRONESIA                         = new RegionCode(057);
        public static readonly RegionCode REGION_MIDDLE_AFRICA                      = new RegionCode(017);

        /// <summary>
        /// "North America" consists of Northern America, the Caribbean, and Central America
        /// </summary>
        public static readonly RegionCode REGION_NORTH_AMERICA                      = new RegionCode(003);
        public static readonly RegionCode REGION_NORTHERN_AFRICA                    = new RegionCode(015);

        /// <summary>
        /// "Northern America" consists of the USA, Canada, Greenland, Bermuda, Saint Pierre and Miquelon
        /// </summary>
        public static readonly RegionCode REGION_NORTHERN_AMERICA                   = new RegionCode(021);
        public static readonly RegionCode REGION_NORTHERN_EUROPE                    = new RegionCode(154);
        public static readonly RegionCode REGION_OCEANIA                            = new RegionCode(009);
        public static readonly RegionCode REGION_POLYNESIA                          = new RegionCode(061);
        public static readonly RegionCode REGION_SMALL_ISLAND_DEVELOPING_STATES     = new RegionCode(722);
        public static readonly RegionCode REGION_SOUTH_AMERICA                      = new RegionCode(005);
        public static readonly RegionCode REGION_SOUTHEAST_ASIA                     = new RegionCode(035);
        public static readonly RegionCode REGION_SOUTHERN_AFRICA                    = new RegionCode(018);
        public static readonly RegionCode REGION_SOUTHERN_ASIA                      = new RegionCode(034);
        public static readonly RegionCode REGION_SOUTHERN_EUROPE                    = new RegionCode(039);
        public static readonly RegionCode REGION_SUBSAHARAN_AFRICA                  = new RegionCode(202);
        public static readonly RegionCode REGION_WESTERN_AFRICA                     = new RegionCode(011);
        public static readonly RegionCode REGION_WESTERN_ASIA                       = new RegionCode(145);
        public static readonly RegionCode REGION_WESTERN_EUROPE                     = new RegionCode(155);
        public static readonly RegionCode REGION_WORLD                              = new RegionCode(001);

        #endregion

        /// <summary>
        /// Lazily instantiates the parse dictionary
        /// </summary>
        /// <returns></returns>
        private static Tuple<SealedInternalizer_CharIgnoreCase_PerfectHash, RegionCode[]> PrepareInternalizer()
        {
            int internalizedKeyCounter = 0;
            List<RegionCode> allCountryCodes = new List<RegionCode>();
            List<KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>> internalizerData =
                new List<KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>>();

            #region Building the parse list
            AddCountryToParseList(UNKNOWN_COUNTRY, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(AFGHANISTAN, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(ALAND_ISLANDS, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(ALBANIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(ALGERIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(AMERICAN_SAMOA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(ANDORRA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(ANGOLA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(ANGUILLA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(ANTARCTICA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(ANTIGUA_AND_BARBUDA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(ARGENTINA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(ARMENIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(ARUBA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(AUSTRALIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(AUSTRIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(AZERBAIJAN, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(BAHAMAS, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(BAHRAIN, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(BANGLADESH, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(BARBADOS, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(BELARUS, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(BELGIUM, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(BELIZE, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(BENIN, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(BERMUDA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(BHUTAN, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(BOLIVIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(BONAIRE_SINT_EUSTATIUS_AND_SABA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(BOSNIA_AND_HERZEGOVINA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(BOTSWANA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(BOUVET_ISLAND, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(BRAZIL, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(BRITISH_INDIAN_OCEAN_TERRITORY, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(BRITISH_VIRGIN_ISLANDS, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(BRUNEI_DARUSSALAM, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(BULGARIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(BURKINA_FASO, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(BURUNDI, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(COTE_D_IVOIRE, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(CABO_VERDE, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(CAMBODIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(CAMEROON, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(CANADA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(CAYMAN_ISLANDS, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(CENTRAL_AFRICAN_REPUBLIC, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(CHAD, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(CHILE, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(CHINA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(CHRISTMAS_ISLAND, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(COCOS_KEELING_ISLANDS, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(COLOMBIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(COMOROS, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(COOK_ISLANDS, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(COSTA_RICA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(CROATIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(CUBA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(CURACAO, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(CYPRUS, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(CZECHIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(DEMOCRATIC_REPUBLIC_OF_THE_CONGO, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(DENMARK, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(DJIBOUTI, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(DOMINICA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(DOMINICAN_REPUBLIC, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(DUTCH_SINT_MAARTEN, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(ECUADOR, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(EGYPT, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(EL_SALVADOR, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(EQUATORIAL_GUINEA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(ERITREA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(ESTONIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(ESWATINI, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(ETHIOPIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(FALKLAND_ISLANDS, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(FAROE_ISLANDS, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(FIJI, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(FINLAND, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(FRANCE, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(FRENCH_GUIANA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(FRENCH_POLYNESIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(FRENCH_SAINT_MARTIN, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(FRENCH_SOUTHERN_TERRITORIES, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(GABON, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(GAMBIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(GEORGIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(GERMANY, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(GHANA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(GIBRALTAR, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(GREECE, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(GREENLAND, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(GRENADA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(GUADELOUPE, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(GUAM, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(GUATEMALA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(GUERNSEY, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(GUINEA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(GUINEA_BISSAU, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(GUYANA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(HAITI, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(HEARD_ISLAND_AND_MCDONALD_ISLANDS, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(HOLY_SEE, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(HONDURAS, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(HONG_KONG, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(HUNGARY, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(ICELAND, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(INDIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(INDONESIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(IRAN, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(IRAQ, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(IRELAND, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(ISLE_OF_MAN, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(ISRAEL, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(ITALY, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(JAMAICA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(JAPAN, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(JERSEY, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(JORDAN, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(KAZAKHSTAN, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(KENYA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(KIRIBATI, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(KUWAIT, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(KYRGYZSTAN, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(LAOS, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(LATVIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(LEBANON, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(LESOTHO, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(LIBERIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(LIBYA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(LIECHTENSTEIN, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(LITHUANIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(LUXEMBOURG, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(MACAO, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(MADAGASCAR, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(MALAWI, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(MALAYSIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(MALDIVES, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(MALI, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(MALTA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(MARSHALL_ISLANDS, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(MARTINIQUE, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(MAURITANIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(MAURITIUS, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(MAYOTTE, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(MEXICO, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(MICRONESIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(MOLDOVA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(MONACO, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(MONGOLIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(MONTENEGRO, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(MONTSERRAT, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(MOROCCO, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(MOZAMBIQUE, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(MYANMAR, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(NAMIBIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(NAURU, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(NEPAL, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(NETHERLANDS, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(NEW_CALEDONIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(NEW_ZEALAND, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(NICARAGUA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(NIGER, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(NIGERIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(NIUE, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(NORFOLK_ISLAND, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(NORTH_KOREA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(NORTH_MACEDONIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(NORTHERN_MARIANA_ISLANDS, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(NORWAY, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(OMAN, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(PAKISTAN, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(PALAU, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(PALESTINE, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(PANAMA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(PAPUA_NEW_GUINEA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(PARAGUAY, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(PERU, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(PHILIPPINES, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(PITCAIRN, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(POLAND, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(PORTUGAL, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(PUERTO_RICO, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(QATAR, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(REUNION, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(REPUBLIC_OF_THE_CONGO, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(ROMANIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(RUSSIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(RWANDA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(SAINT_BARTHELEMY, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(SAINT_HELENA_ASCENSION_AND_TRISTAN_DA_CUNHA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(SAINT_KITTS_AND_NEVIS, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(SAINT_LUCIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(SAINT_PIERRE_AND_MIQUELON, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(SAINT_VINCENT_AND_THE_GRENADINES, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(SAMOA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(SAN_MARINO, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(SAO_TOME_AND_PRINCIPE, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(SAUDI_ARABIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(SENEGAL, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(SERBIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(SEYCHELLES, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(SIERRA_LEONE, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(SINGAPORE, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(SLOVAKIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(SLOVENIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(SOLOMON_ISLANDS, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(SOMALIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(SOUTH_AFRICA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(SOUTH_GEORGIA_AND_SOUTH_SANDWICH_ISLANDS, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(SOUTH_KOREA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(SOUTH_SUDAN, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(SPAIN, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(SRI_LANKA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(SUDAN, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(SURINAME, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(SVALBARD_AND_JAN_MAYEN, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(SWEDEN, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(SWITZERLAND, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(SYRIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(TAIWAN, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(TAJIKISTAN, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(TANZANIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(THAILAND, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(TIMOR_LESTE, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(TOGO, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(TOKELAU, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(TONGA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(TRINIDAD_AND_TOBAGO, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(TUNISIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(TURKEY, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(TURKMENISTAN, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(TURKS_AND_CAICOS_ISLANDS, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(TUVALU, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(UGANDA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(UKRAINE, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(UNITED_ARAB_EMIRATES, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(UNITED_KINGDOM, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(UNITED_STATED_MINOR_OUTLYING_ISLANDS, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(UNITED_STATES_OF_AMERICA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(URUGUAY, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(US_VIRGIN_ISLANDS, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(UZBEKISTAN, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(VANUATU, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(VENEZUELA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(VIETNAM, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(WALLIS_AND_FUTUNA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(WESTERN_SAHARA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(YEMEN, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(ZAMBIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(ZIMBABWE, allCountryCodes, internalizerData, ref internalizedKeyCounter);


            AddCountryToParseList(REGION_AFRICA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(REGION_AMERICAS, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(REGION_ASIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(REGION_AUSTRALIA_AND_NEW_ZEALAND, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(REGION_CARIBBEAN, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(REGION_CENTRAL_AMERICA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(REGION_CENTRAL_ASIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(REGION_EASTERN_AFRICA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(REGION_EASTERN_ASIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(REGION_EASTERN_EUROPE, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(REGION_EUROPE, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(REGION_LANDLOCKED_DEVELOPING_COUNTRIES, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(REGION_LATIN_AMERICA_CARIBBEAN, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(REGION_LEAST_DEVELOPED_COUNTRIES, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(REGION_MELANESIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(REGION_MICRONESIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(REGION_MIDDLE_AFRICA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(REGION_NORTH_AMERICA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(REGION_NORTHERN_AFRICA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(REGION_NORTHERN_AMERICA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(REGION_NORTHERN_EUROPE, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(REGION_OCEANIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(REGION_POLYNESIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(REGION_SMALL_ISLAND_DEVELOPING_STATES, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(REGION_SOUTH_AMERICA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(REGION_SOUTHEAST_ASIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(REGION_SOUTHERN_AFRICA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(REGION_SOUTHERN_ASIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(REGION_SOUTHERN_EUROPE, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(REGION_SUBSAHARAN_AFRICA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(REGION_WESTERN_AFRICA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(REGION_WESTERN_ASIA, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(REGION_WESTERN_EUROPE, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            AddCountryToParseList(REGION_WORLD, allCountryCodes, internalizerData, ref internalizedKeyCounter);
            #endregion

            SealedInternalizer_CharIgnoreCase_PerfectHash internalizer = new SealedInternalizer_CharIgnoreCase_PerfectHash(internalizerData);
            return new Tuple<SealedInternalizer_CharIgnoreCase_PerfectHash, RegionCode[]>(internalizer, allCountryCodes.ToArray());
        }

        private static void AddCountryToParseList(
            RegionCode code,
            List<RegionCode> countryCodes,
            List<KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>> internalizerData,
            ref int countryCodeIndex)
        {
            countryCodes.Add(code);

            if (!string.IsNullOrEmpty(code.Iso3166_1_Alpha2))
            {
                internalizerData.Add(new KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>(
                    new InternedKey<ReadOnlyMemory<char>>(countryCodeIndex),
                    code.Iso3166_1_Alpha2.AsMemory()));
            }

            if (!string.IsNullOrEmpty(code.Iso3166_1_Alpha3))
            {
                internalizerData.Add(new KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>(
                    new InternedKey<ReadOnlyMemory<char>>(countryCodeIndex),
                    code.Iso3166_1_Alpha3.AsMemory()));
            }

            if (code.NumericCode < 100)
            {
                // Unpadded number representation e.g. "60" for Bermuda. Non-standard but we still want to be able to parse.
                internalizerData.Add(new KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>(
                    new InternedKey<ReadOnlyMemory<char>>(countryCodeIndex),
                    code.NumericCode.ToString(CultureInfo.InvariantCulture).AsMemory()));
            }

            // Zero-padded number representation e.g. "036" for Australia
            internalizerData.Add(new KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>(
                new InternedKey<ReadOnlyMemory<char>>(countryCodeIndex),
                code.NumericCode.ToString("D3", CultureInfo.InvariantCulture).AsMemory())); 

            countryCodeIndex++;
        }
    }
}
