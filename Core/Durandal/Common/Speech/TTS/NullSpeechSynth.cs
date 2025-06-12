using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using Durandal.Common.Tasks;
using Durandal.Common.Audio;
using Durandal.API;
using Durandal.Common.Audio.Codecs;
using System.Threading;
using Durandal.Common.Time;
using Durandal.Common.NLP.Language;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Speech.TTS
{
    public class NullSpeechSynth : ISpeechSynth
    {
        private int _disposed = 0;

        public NullSpeechSynth()
        {
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        /// <inheritdoc />
        public Task<SynthesizedSpeech> SynthesizeSpeechAsync(SpeechSynthesisRequest request, CancellationToken cancelToken, IRealTimeProvider realTime, ILogger logger = null)
        {
            return Task.FromResult<SynthesizedSpeech>(null);
        }

        /// <inheritdoc />
        public Task<IAudioSampleSource> SynthesizeSpeechToStreamAsync(SpeechSynthesisRequest request, WeakPointer<IAudioGraph> graph, CancellationToken cancelToken, IRealTimeProvider realTime, ILogger logger = null)
        {
            return Task.FromResult<IAudioSampleSource>(null);
        }

        /// <inheritdoc />
        public bool IsLocaleSupported(LanguageCode locale)
        {
            return false;
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
    }
}
