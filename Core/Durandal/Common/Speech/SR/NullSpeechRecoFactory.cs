using Durandal.Common.Audio;
using Durandal.Common.Logger;
using Durandal.Common.NLP.Language;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.SR
{
    public sealed class NullSpeechRecoFactory : ISpeechRecognizerFactory
    {
        private static readonly ISpeechRecognizerFactory _singleton = new NullSpeechRecoFactory();

        public static ISpeechRecognizerFactory Singleton
        {
            get
            {
                return _singleton;
            }
        }

        private NullSpeechRecoFactory()
        {
        }

        public void Dispose()
        {
        }

        public bool IsLocaleSupported(LanguageCode locale)
        {
            return true;
        }

        /// <inheritdoc />
        public Task<ISpeechRecognizer> CreateRecognitionStream(
            WeakPointer<IAudioGraph> audioGraph,
            string graphNodeName,
            LanguageCode locale,
            ILogger queryLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            return Task.FromResult<ISpeechRecognizer>(new NullSpeechReco(audioGraph, AudioSampleFormat.Mono(16000)));
        }
    }
}
