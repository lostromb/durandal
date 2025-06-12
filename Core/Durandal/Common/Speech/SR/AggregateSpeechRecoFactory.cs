using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Logger;
using System.Threading;
using Durandal.Common.Time;
using Durandal.Common.Audio;
using Durandal.Common.Tasks;
using Durandal.Common.Utils;
using Durandal.Common.NLP.Language;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Speech.SR
{
    /// <summary>
    /// A speech recognizer factory which aggregates several others. The canonical case for this is if you have
    /// a high-quality recognizer that handles specific locales, but you also want to be able to fallback to a
    /// slower recognizer that handles more locales if needed.
    /// </summary>
    public class AggregateSpeechRecoFactory : ISpeechRecognizerFactory
    {
        private ISpeechRecognizerFactory[] _implementations;
        private int _disposed = 0;

        /// <summary>
        /// Creates an aggregate speech reco factory
        /// </summary>
        /// <param name="implementations">The list of speech recognizer implementations. The list is in priority order, with earlier entries taking the highest precedence</param>
        public AggregateSpeechRecoFactory(params ISpeechRecognizerFactory[] implementations)
        {
            _implementations = implementations;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~AggregateSpeechRecoFactory()
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
            // Find the first instance which supports the requested locale
            foreach (ISpeechRecognizerFactory factory in _implementations)
            {
                if (factory.IsLocaleSupported(locale))
                {
                    return factory.CreateRecognitionStream(audioGraph, graphNodeName, locale, queryLogger, cancelToken, realTime);
                }
            }

            // Locale not supported
            return Task.FromResult<ISpeechRecognizer>(null);
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
                foreach (ISpeechRecognizerFactory factory in _implementations)
                {
                    factory.Dispose();
                }
            }
        }

        public bool IsLocaleSupported(LanguageCode locale)
        {
            foreach (ISpeechRecognizerFactory factory in _implementations)
            {
                if (factory.IsLocaleSupported(locale))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
