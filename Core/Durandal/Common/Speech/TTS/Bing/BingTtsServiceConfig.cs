using Durandal.API;
using Durandal.Common.NLP.Language;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.TTS.Bing
{
    public static class BingTtsServiceConfig
    {
        public static readonly string TokenPath = "/sts/v1.0/issueToken";

        public static Uri GetTokenUri(string region)
        {
            return new Uri("https://" + region + ".api.cognitive.microsoft.com");
        }

        public static Uri GetServiceUri(string region)
        {
            return new Uri("https://" + region + ".tts.speech.microsoft.com/cognitiveservices/v1");
        }

        private static List<Voice> _voiceList =
            new List<Voice>()
        {
            new Voice(LanguageCode.Parse("ar-EG"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (ar-EG, Hoda)"),
            new Voice(LanguageCode.Parse("ar-SA"), VoiceGender.Male, "Microsoft Server Speech Text to Speech Voice (ar-SA, Naayf)"),
            new Voice(LanguageCode.Parse("ca-ES"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (ca-ES, HerenaRUS)"),
            new Voice(LanguageCode.Parse("cs-CZ"), VoiceGender.Male, "Microsoft Server Speech Text to Speech Voice (cs-CZ, Vit)"),
            new Voice(LanguageCode.Parse("da-DK"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (da-DK, HelleRUS)"),
            new Voice(LanguageCode.Parse("de-AT"), VoiceGender.Male, "Microsoft Server Speech Text to Speech Voice (de-AT, Michael)"),
            new Voice(LanguageCode.Parse("de-CH"), VoiceGender.Male, "Microsoft Server Speech Text to Speech Voice (de-CH, Karsten)"),
            new Voice(LanguageCode.Parse("de-DE"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (de-DE, Hedda) "),
            new Voice(LanguageCode.Parse("de-DE"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (de-DE, HeddaRUS)"),
            new Voice(LanguageCode.Parse("de-DE"), VoiceGender.Male, "Microsoft Server Speech Text to Speech Voice (de-DE, Stefan, Apollo) "),
            new Voice(LanguageCode.Parse("el-GR"), VoiceGender.Male, "Microsoft Server Speech Text to Speech Voice (el-GR, Stefanos)"),
            new Voice(LanguageCode.Parse("en-AU"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (en-AU, Catherine) "),
            new Voice(LanguageCode.Parse("en-AU"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (en-AU, HayleyRUS)"),
            new Voice(LanguageCode.Parse("en-CA"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (en-CA, Linda)"),
            new Voice(LanguageCode.Parse("en-CA"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (en-CA, HeatherRUS)"),
            new Voice(LanguageCode.Parse("en-GB"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (en-GB, Susan, Apollo)"),
            new Voice(LanguageCode.Parse("en-GB"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (en-GB, HazelRUS)"),
            new Voice(LanguageCode.Parse("en-GB"), VoiceGender.Male, "Microsoft Server Speech Text to Speech Voice (en-GB, George, Apollo)"),
            new Voice(LanguageCode.Parse("en-IE"), VoiceGender.Male, "Microsoft Server Speech Text to Speech Voice (en-IE, Shaun)"),
            new Voice(LanguageCode.Parse("en-IN"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (en-IN, Heera, Apollo)"),
            new Voice(LanguageCode.Parse("en-IN"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (en-IN, PriyaRUS)"),
            new Voice(LanguageCode.Parse("en-IN"), VoiceGender.Male, "Microsoft Server Speech Text to Speech Voice (en-IN, Ravi, Apollo) "),
            new Voice(LanguageCode.Parse("en-US"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (en-US, JessaNeural)"),
            new Voice(LanguageCode.Parse("en-US"), VoiceGender.Male, "Microsoft Server Speech Text to Speech Voice (en-US, GuyNeural)"),
            new Voice(LanguageCode.Parse("en-US"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (en-US, ZiraRUS)"),
            new Voice(LanguageCode.Parse("en-US"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (en-US, JessaRUS)"),
            new Voice(LanguageCode.Parse("en-US"), VoiceGender.Male, "Microsoft Server Speech Text to Speech Voice (en-US, BenjaminRUS)"),
            new Voice(LanguageCode.Parse("es-ES"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (es-ES, Laura, Apollo)"),
            new Voice(LanguageCode.Parse("es-ES"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (es-ES, HelenaRUS)"),
            new Voice(LanguageCode.Parse("es-ES"), VoiceGender.Male, "Microsoft Server Speech Text to Speech Voice (es-ES, Pablo, Apollo)"),
            new Voice(LanguageCode.Parse("es-MX"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (es-MX, HildaRUS)"),
            new Voice(LanguageCode.Parse("es-MX"), VoiceGender.Male, "Microsoft Server Speech Text to Speech Voice (es-MX, Raul, Apollo)"),
            new Voice(LanguageCode.Parse("fi-FI"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (fi-FI, HeidiRUS)"),
            new Voice(LanguageCode.Parse("fr-CA"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (fr-CA, Caroline)"),
            new Voice(LanguageCode.Parse("fr-CA"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (fr-CA, HarmonieRUS)"),
            new Voice(LanguageCode.Parse("fr-CH"), VoiceGender.Male, "Microsoft Server Speech Text to Speech Voice (fr-CH, Guillaume)"),
            new Voice(LanguageCode.Parse("fr-FR"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (fr-FR, Julie, Apollo)"),
            new Voice(LanguageCode.Parse("fr-FR"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (fr-FR, HortenseRUS)"),
            new Voice(LanguageCode.Parse("fr-FR"), VoiceGender.Male, "Microsoft Server Speech Text to Speech Voice (fr-FR, Paul, Apollo)"),
            new Voice(LanguageCode.Parse("he-IL"), VoiceGender.Male, "Microsoft Server Speech Text to Speech Voice (he-IL, Asaf)"),
            new Voice(LanguageCode.Parse("hi-IN"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (hi-IN, Kalpana, Apollo)"),
            new Voice(LanguageCode.Parse("hi-IN"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (hi-IN, Kalpana)"),
            new Voice(LanguageCode.Parse("hi-IN"), VoiceGender.Male, "Microsoft Server Speech Text to Speech Voice (hi-IN, Hemant)"),
            new Voice(LanguageCode.Parse("hu-HU"), VoiceGender.Male, "Microsoft Server Speech Text to Speech Voice (hu-HU, Szabolcs)"),
            new Voice(LanguageCode.Parse("id-ID"), VoiceGender.Male, "Microsoft Server Speech Text to Speech Voice (id-ID, Andika)"),
            new Voice(LanguageCode.Parse("it-IT"), VoiceGender.Male, "Microsoft Server Speech Text to Speech Voice (it-IT, Cosimo, Apollo)"),
            new Voice(LanguageCode.Parse("ja-JP"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (ja-JP, Ayumi, Apollo)"),
            new Voice(LanguageCode.Parse("ja-JP"), VoiceGender.Male, "Microsoft Server Speech Text to Speech Voice (ja-JP, Ichiro, Apollo)"),
            new Voice(LanguageCode.Parse("ko-KR"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (ko-KR, HeamiRUS)"),
            new Voice(LanguageCode.Parse("nb-NO"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (nb-NO, HuldaRUS)"),
            new Voice(LanguageCode.Parse("nl-NL"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (nl-NL, HannaRUS)"),
            new Voice(LanguageCode.Parse("pl-PL"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (pl-PL, PaulinaRUS)"),
            new Voice(LanguageCode.Parse("pt-BR"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (pt-BR, HeloisaRUS)"),
            new Voice(LanguageCode.Parse("pt-BR"), VoiceGender.Male, "Microsoft Server Speech Text to Speech Voice (pt-BR, Daniel, Apollo)"),
            new Voice(LanguageCode.Parse("pt-PT"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (pt-PT, HeliaRUS)"),
            new Voice(LanguageCode.Parse("ro-RO"), VoiceGender.Male, "Microsoft Server Speech Text to Speech Voice (ro-RO, Andrei)"),
            new Voice(LanguageCode.Parse("ru-RU"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (ru-RU, Irina, Apollo)"),
            new Voice(LanguageCode.Parse("ru-RU"), VoiceGender.Male, "Microsoft Server Speech Text to Speech Voice (ru-RU, Pavel, Apollo)"),
            new Voice(LanguageCode.Parse("sk-SK"), VoiceGender.Male, "Microsoft Server Speech Text to Speech Voice (sk-SK, Filip)"),
            new Voice(LanguageCode.Parse("sv-SE"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (sv-SE, HedvigRUS)"),
            new Voice(LanguageCode.Parse("th-TH"), VoiceGender.Male, "Microsoft Server Speech Text to Speech Voice (th-TH, Pattara)"),
            new Voice(LanguageCode.Parse("tr-TR"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (tr-TR, SedaRUS)"),
            new Voice(LanguageCode.Parse("zh-CN"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (zh-CN, HuihuiRUS)"),
            new Voice(LanguageCode.Parse("zh-CN"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (zh-CN, Yaoyao, Apollo)"),
            new Voice(LanguageCode.Parse("zh-CN"), VoiceGender.Male, "Microsoft Server Speech Text to Speech Voice (zh-CN, Kangkang, Apollo)"),
            new Voice(LanguageCode.Parse("zh-HK"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (zh-HK, Tracy, Apollo)"),
            new Voice(LanguageCode.Parse("zh-HK"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (zh-HK, TracyRUS)"),
            new Voice(LanguageCode.Parse("zh-HK"), VoiceGender.Male, "Microsoft Server Speech Text to Speech Voice (zh-HK, Danny, Apollo)"),
            new Voice(LanguageCode.Parse("zh-TW"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (zh-TW, Yating, Apollo)"),
            new Voice(LanguageCode.Parse("zh-TW"), VoiceGender.Female, "Microsoft Server Speech Text to Speech Voice (zh-TW, HanHanRUS)"),
            new Voice(LanguageCode.Parse("zh-TW"), VoiceGender.Male, "Microsoft Server Speech Text to Speech Voice (zh-TW, Zhiwei, Apollo)"),
        };

        public static bool IsLocaleSupported(LanguageCode locale)
        {
            return SelectVoice(locale, VoiceGender.Female) != null;
        }

        public static List<Voice> VoiceList
        {
            get
            {
                return _voiceList;
            } 
        }

        /// <summary>
        /// Attempts to pick a suitable voice given the input locale and preferred gender
        /// </summary>
        /// <param name="locale">Speech locale</param>
        /// <param name="gender">Desired gender</param>
        /// <returns>The selected voice config, or null if none was found</returns>
        public static Voice SelectVoice(LanguageCode locale, VoiceGender gender)
        {
            if (locale == null)
            {
                return null;
            }

            // Check for exact matches first
            foreach (Voice voice in VoiceList)
            {
                if (string.Equals(locale.ToBcp47Alpha2String(), voice.Locale.ToBcp47Alpha2String(), StringComparison.OrdinalIgnoreCase) &&
                    gender == voice.Gender)
                {
                    return voice;
                }
            }

            // Now check for voices in the same locale but different gender
            foreach (Voice voice in VoiceList)
            {
                if (string.Equals(locale.ToBcp47Alpha2String(), voice.Locale.ToBcp47Alpha2String(), StringComparison.OrdinalIgnoreCase))
                {
                    return voice;
                }
            }

            // Now check for voices in the same language, same gender, but different region
            foreach (Voice voice in VoiceList)
            {
                if (string.Equals(locale.Iso639_1, voice.Locale.Iso639_1, StringComparison.OrdinalIgnoreCase) &&
                    gender == voice.Gender)
                {
                    return voice;
                }
            }

            // Now check for voices in the same language, any gender, different region
            foreach (Voice voice in VoiceList)
            {
                if (string.Equals(locale.Iso639_1, voice.Locale.Iso639_1, StringComparison.OrdinalIgnoreCase))
                {
                    return voice;
                }
            }

            return null;
        }
    }
}
