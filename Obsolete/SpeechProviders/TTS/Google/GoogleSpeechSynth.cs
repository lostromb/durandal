
namespace Durandal.Common.Speech.TTS.Google
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Net;
    using System.Text.RegularExpressions;
    using System.Web;

    using Durandal.Common.Utils;
    using Durandal.Common.Audio;
    using Durandal.Common.AudioV2;
    using Durandal.Common.Logger;
    using Durandal.Common.Audio.Codecs;
    using System.Threading.Tasks;
    using Durandal.API;

    /// <summary>
    /// Speech synthesizer backed by the Google Translate API
    /// </summary>
    public class GoogleSpeechSynth : ISpeechSynth
    {
        private readonly Regex ssmlExtractor = new Regex("<.+?>");
        private readonly IAudioCodec _mp3Codec;

        public GoogleSpeechSynth(ILogger logger)
        {
            _mp3Codec = new Mp3AudioCodec(logger);
        }
        
        public async Task<SynthesizedSpeech> SynthesizeSpeechAsync(string ssml, string locale, int sampleRate = AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE, ILogger logger = null)
        {
            if (logger == null)
                logger = NullLogger.Singleton;
            string targetLanguage = ConvertLocaleIntoLangCode(locale, logger);
            // Strip the tags from the ssml as this API cannot process them
            string reducedSsml = DurandalUtils.RegexRemove(ssmlExtractor, ssml);
            AudioData returnedSpeech = await UseGoogleTranslateAPI(reducedSsml, targetLanguage, logger);
            if (returnedSpeech == null)
            {
                return null;
            }

            return new SynthesizedSpeech()
            {
                Audio = returnedSpeech,
                Ssml = ssml,
                Locale = locale
            };
        }

        public void Dispose() { }

        public bool IsLocaleSupported(string locale)
        {
            return string.Equals(locale, "en-us", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Retrieves a snippet of audio using the Google Translate API, in mp3 format.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        private async Task<AudioData> UseGoogleTranslateAPI(string text, string targetLanguage, ILogger logger)
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    client.Headers["User-Agent"] = "Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/31.0.1650.63 Safari/537.36";
                    string targetUrl = string.Format("http://translate.google.com/translate_tts?tl={0}&q={1}",
                                                     targetLanguage,
                                                     WebUtility.UrlEncode(text));
                    byte[] result = await client.DownloadDataTaskAsync(targetUrl);
                    // FOR DEBUGGING
                    //File.WriteAllBytes("translate_tts.mp3", result);
                    
                    AudioChunk decodedPcm = AudioUtils.DecompressAudioUsingStream(new ArraySegment<byte>(result), _mp3Codec, string.Empty);
                    return new AudioData()
                    {
                        Codec = PCMCodec.FORMAT_CODE,
                        CodecParams = PCMCodec.CreateCodecParams(decodedPcm.SampleRate),
                        Data = new ArraySegment<byte>(decodedPcm.GetDataAsBytes())
                    };
                }
            }
            catch (WebException e)
            {
                logger.Log("Web exception while calling Google TTS API: " + e.Message, LogLevel.Err);
                return null;
            }
        }

        private string ConvertLocaleIntoLangCode(string locale, ILogger logger)
        {
            string lang = locale.ToLowerInvariant().Substring(0, 2);
            // Unknown or no locale - default to english
            if (lang.Equals("xx"))
            {
                logger.Log("Unknown locale passed to Google TTS synth; assuming English", LogLevel.Wrn);
                return "en";
            }
            return lang;
        }

        public async Task SynthesizeSpeechToStreamAsync(string ssml, string locale, AudioWritePipe outStream, int sampleRate = AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE, ILogger logger = null)
        {
            SynthesizedSpeech block = await SynthesizeSpeechAsync(ssml, locale, sampleRate, logger);
            if (block != null && block.Audio != null && block.Audio.Data != null)
            {
                outStream.Write(block.Audio.Data.Array, block.Audio.Data.Offset, block.Audio.Data.Count);
            }
        }
    }
}
