using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Audio;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System.Threading;
using Durandal.Common.Utils;
using Durandal.Common.NLP.Language;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Speech.SR.Remote
{
    public class RemoteSpeechRecognizerFactory : ISpeechRecognizerFactory
    {
        private ILogger _logger;
        private WeakPointer<ISocketFactory> _socketProvider;
        private IAudioCodecFactory _codec;
        private string _codecToUse;
        private int _readTimeout;
        private string _remoteHost;
        private int _remotePort;
        private int _disposed = 0;

        public RemoteSpeechRecognizerFactory(
            IAudioCodecFactory codec,
            string codecToUse,
            WeakPointer<ISocketFactory> socketProvider,
            string remoteHost,
            int remotePort,
            ILogger logger,
            int timeout = 3000)
        {
            _logger = logger;
            _codecToUse = codecToUse;
            _socketProvider = socketProvider;
            _codec = codec;
            _readTimeout = timeout;
            _remoteHost = remoteHost;
            _remotePort = remotePort;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~RemoteSpeechRecognizerFactory()
        {
            Dispose(false);
        }
#endif

        /// <inheritdoc />
        public async Task<ISpeechRecognizer> CreateRecognitionStream(
            WeakPointer<IAudioGraph> audioGraph,
            string graphNodeName,
            LanguageCode locale,
            ILogger queryLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            RemoteSpeechRecognizer returnVal = new RemoteSpeechRecognizer(
                audioGraph,
                AudioSampleFormat.Mono(16000),
                graphNodeName,
                _codec,
                _codecToUse,
                _socketProvider,
                _remoteHost,
                _remotePort,
                _logger,
                _readTimeout);
            await returnVal.Initialize(locale, audioGraph, cancelToken, realTime).ConfigureAwait(false);
            return returnVal;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
            }
        }

        public bool IsLocaleSupported(LanguageCode locale)
        {
            return string.Equals(locale.ToBcp47Alpha2String(), "en-US");
        }
    }
}
