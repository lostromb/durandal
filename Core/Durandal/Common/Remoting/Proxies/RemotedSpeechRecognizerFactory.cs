using Durandal.Common.Speech.SR;
using System;
using System.Collections.Generic;
using System.Text;
using Durandal.Common.Logger;
using System.Threading.Tasks;
using Durandal.Common.Time;
using Durandal.API;
using System.Threading;
using Durandal.Common.Audio;
using System.IO;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.IO;
using Durandal.Common.Client;
using Durandal.Common.Utils;
using Durandal.Common.Tasks;
using Durandal.Common.Events;
using Durandal.Common.NLP.Language;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Remoting.Proxies
{
    public class RemotedSpeechRecognizerFactory : ISpeechRecognizerFactory
    {
        private readonly RemoteDialogMethodDispatcher _dispatcher;
        private int _disposed = 0;

        public RemotedSpeechRecognizerFactory(RemoteDialogMethodDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~RemotedSpeechRecognizerFactory()
        {
            Dispose(false);
        }
#endif

        /// <inheritdoc />
        public Task<ISpeechRecognizer> CreateRecognitionStream(
            WeakPointer<IAudioGraph> audioGraph,
            string graphNodeName,
            LanguageCode locale,
            ILogger queryLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            return Task.FromResult<ISpeechRecognizer>(new RemotedSpeechRecognizer(audioGraph, _dispatcher, realTime, locale));
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
                _dispatcher.Dispose();
            }
        }

        public bool IsLocaleSupported(LanguageCode locale)
        {
            // if locale is not actually supported then this will throw an exception at runtime
            // this is done because it's potentially non-trivial to enumerate all supported locales
            return true;
        }

        private class RemotedSpeechRecognizer : AbstractAudioSampleTarget, ISpeechRecognizer
        {
            private readonly RemoteDialogMethodDispatcher _dispatcher;
            private readonly IRealTimeProvider _realTime;
            private readonly LanguageCode _locale;
            private readonly MemoryStream _bucket;
            private readonly RawPcmEncoder _pcmEncoder;
            private int _disposed = 0;

            public AsyncEvent<TextEventArgs> IntermediateResultEvent { get; private set; }

            public RemotedSpeechRecognizer(
                WeakPointer<IAudioGraph> graph,
                RemoteDialogMethodDispatcher dispatcher,
                IRealTimeProvider realTime,
                LanguageCode locale) : base(graph, nameof(RemotedSpeechRecognizer), nodeCustomName: null)
            {
                _dispatcher = dispatcher;
                _realTime = realTime;
                _locale = locale;
                _bucket = new MemoryStream();
                InputFormat = AudioSampleFormat.Mono(16000);
                _pcmEncoder = new RawPcmEncoder(graph, InputFormat, "RemotedSpeechRecoPcmEncoder");
                _pcmEncoder.Initialize(new NonRealTimeStreamWrapper(_bucket, false), false, CancellationToken.None, DefaultRealTimeProvider.Singleton).Await();
                IntermediateResultEvent = new AsyncEvent<TextEventArgs>();
            }
            
            protected override void Dispose(bool disposing)
            {
                if (!AtomicOperations.ExecuteOnce(ref _disposed))
                {
                    return;
                }

                try
                {
                    if (disposing)
                    {
                        _pcmEncoder?.Dispose();
                        _bucket?.Dispose();
                    }
                }
                finally
                {
                    base.Dispose(disposing);
                }
            }

            public Task<SpeechRecognitionResult> FinishUnderstandSpeech(CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                AudioData data = new AudioData()
                {
                    Data = new ArraySegment<byte>(_bucket.ToArray()),
                    Codec = _pcmEncoder.Codec,
                    CodecParams = _pcmEncoder.CodecParams
                };

                return _dispatcher.SpeechReco_Recognize(_locale, data, _realTime, cancelToken);
            }

            protected override ValueTask WriteAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                return _pcmEncoder.WriteAsync(buffer, bufferOffset, numSamplesPerChannel, cancelToken, realTime);
            }
        }
    }
}
