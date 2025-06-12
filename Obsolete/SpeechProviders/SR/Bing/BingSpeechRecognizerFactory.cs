using Durandal.Common.Logger;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.SR.Bing
{
    public class BingSpeechRecognizerFactory : ISpeechRecognizerFactory
    {
        private ILogger _logger;
        private bool _intermediateReco;

        // FIXME: Replace "enableIntermediateReco" with just another recognizer factory for the intermediate results stream
        public BingSpeechRecognizerFactory(ILogger logger, bool enableIntermediateReco)
        {
            _logger = logger;
            _intermediateReco = enableIntermediateReco;
        }

        public Task<ISpeechRecognizer> CreateRecognitionStream(string locale, ILogger queryLogger, IRealTimeProvider realTime)
        {
            if (queryLogger == null)
            {
                queryLogger = _logger;
            }

            BingSpeechRecognizer returnVal = new BingSpeechRecognizer(queryLogger, _intermediateReco);
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
