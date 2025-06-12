using Durandal.Common.Logger;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.SR.Oxford
{
    public class OxfordSpeechRecognizerFactory : ISpeechRecognizerFactory
    {
        private readonly string _apiKey;
        private readonly ILogger _logger;

        private static readonly ISet<string> SUPPORTED_LOCALES = new HashSet<string>(new string[]
            {
                "ar-eg", "ca-es", "da-dk", "de-de", "en-au", "en-ca", "en-gb", "en-in", "en-nz",
                "en-us", "es-es", "es-mx", "fi-fi", "fr-ca", "fr-fr", "hi-in", "it-it", "ja-jp",
                "ko-kr", "nb-no", "nl-nl", "pl-pl", "pt-br", "pt-pt", "ru-ru", "sv-se", "zh-cn",
                "zh-hk", "zh-tw"
            });

        public OxfordSpeechRecognizerFactory(ILogger logger, string apiKey)
        {
            _logger = logger;
            _apiKey = apiKey;
        }

        public Task<ISpeechRecognizer> CreateRecognitionStream(string locale, ILogger queryLogger, IRealTimeProvider realTime)
        {
            if (queryLogger == null)
            {
                queryLogger = _logger;
            }
            
            return Task.FromResult<ISpeechRecognizer>(new OxfordSpeechRecognizer(queryLogger, _apiKey, locale));
        }

        public void Dispose() { }

        public bool IsLocaleSupported(string locale)
        {
            if (string.IsNullOrEmpty(locale))
            {
                return false;
            }

            return SUPPORTED_LOCALES.Contains(locale.ToLowerInvariant());
        }
    }
}
