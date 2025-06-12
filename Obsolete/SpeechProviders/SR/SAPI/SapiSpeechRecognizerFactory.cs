#if !PCL

using Durandal.Common.Audio;
using Durandal.Common.Logger;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.SR.SAPI
{
    public class SapiSpeechRecognizerFactory : ISpeechRecognizerFactory
    {
        private ILogger _logger;

        public SapiSpeechRecognizerFactory(ILogger logger)
        {
            _logger = logger;
        }

        public Task<ISpeechRecognizer> CreateRecognitionStream(string locale, ILogger queryLogger, IRealTimeProvider realTime)
        {
            if (queryLogger == null)
            {
                queryLogger = _logger;
            }

            SapiSpeechRecognizer returnVal = new SapiSpeechRecognizer(AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE, queryLogger);
            returnVal.StartUnderstandSpeech(locale);
            return Task.FromResult<ISpeechRecognizer>(returnVal);
        }

        public void Dispose() { }

        public bool IsLocaleSupported(string locale)
        {
            return string.Equals(locale, "en-us");
        }
    }
}

#endif