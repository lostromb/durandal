using Durandal.Common.Audio.Codecs;
using Durandal.Common.AudioV2;
using Durandal.Common.Logger;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.SR.GoogleLegacy
{
    public class GoogleLegacySpeechRecognizerFactory : ISpeechRecognizerFactory
    {
        private ILogger _logger;
        private bool _intermediateReco;
        private IAudioCodec _flacCodec;

        // FIXME: Replace "enableIntermediateReco" with just another recognizer factory for the intermediate results stream
        public GoogleLegacySpeechRecognizerFactory(ILogger logger, bool enableIntermediateReco)
        {
            _logger = logger;
            _intermediateReco = enableIntermediateReco;
            _flacCodec = new FlacAudioCodec(_logger.Clone("FlacCodec"));
        }

        public async Task<ISpeechRecognizer> CreateRecognitionStream(string locale, ILogger queryLogger, IRealTimeProvider realTime)
        {
            if (queryLogger == null)
            {
                queryLogger = _logger;
            }

            GoogleLegacySpeechRecognizer returnVal = new GoogleLegacySpeechRecognizer(queryLogger, _flacCodec, _intermediateReco);
            returnVal.StartUnderstandSpeech(locale);
            return await Task.FromResult(returnVal);
        }

        public void Dispose() { }

        public bool IsLocaleSupported(string locale)
        {
            return string.Equals(locale, "en-us");
        }
    }
}
