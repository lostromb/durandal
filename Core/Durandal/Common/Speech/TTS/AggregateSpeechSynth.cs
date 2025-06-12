using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Audio;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using System.Threading;
using Durandal.API;
using Durandal.Common.Time;
using Durandal.Common.NLP.Language;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Speech.TTS
{
    /// <summary>
    /// A speech synth which aggregates several others. The canonical case for this is if you have
    /// a high-quality synth that handles specific locales (probably hosted on-device), but you also
    /// want to be able to fallback to a slower synth that handles more locales if needed.
    /// </summary>
    public class AggregateSpeechSynth : ISpeechSynth
    {
        private ISpeechSynth[] _implementations;
        private int _disposed = 0;

        public AggregateSpeechSynth(params ISpeechSynth[] implementations)
        {
            _implementations = implementations;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~AggregateSpeechSynth()
        {
            Dispose(false);
        }
#endif

        /// <inheritdoc />
        public Task<SynthesizedSpeech> SynthesizeSpeechAsync(SpeechSynthesisRequest request, CancellationToken cancelToken, IRealTimeProvider realTime, ILogger logger = null)
        {
            // Find the first instance which supports the requested locale
            foreach (ISpeechSynth synth in _implementations)
            {
                if (synth.IsLocaleSupported(request.Locale))
                {
                    return synth.SynthesizeSpeechAsync(request, cancelToken, realTime, logger);
                }
            }

            return null;
        }

        /// <inheritdoc />
        public bool IsLocaleSupported(LanguageCode locale)
        {
            foreach (ISpeechSynth synth in _implementations)
            {
                if (synth.IsLocaleSupported(locale))
                {
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc />
        public Task<IAudioSampleSource> SynthesizeSpeechToStreamAsync(SpeechSynthesisRequest request, WeakPointer<IAudioGraph> parentGraph, CancellationToken cancelToken, IRealTimeProvider realTime, ILogger logger = null)
        {
            foreach (ISpeechSynth synth in _implementations)
            {
                if (synth.IsLocaleSupported(request.Locale))
                {
                    return synth.SynthesizeSpeechToStreamAsync(request, parentGraph, cancelToken, realTime, logger);
                }
            }

            return null;
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
                foreach (ISpeechSynth synth in _implementations)
                {
                    synth.Dispose();
                }
            }
        }
    }
}
