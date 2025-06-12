using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Audio;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using Durandal.Common.Tasks;
using Durandal.Common.Speech.TTS;
using Durandal.Common.Speech;
using Durandal.API;
using Durandal.Common.Audio.Codecs;
using System.Threading;
using Durandal.Common.Instrumentation;
using System.Diagnostics;
using Durandal.Common.MathExt;
using Durandal.Common.Time;
using Durandal.Common.Audio.Components;
using Durandal.Common.NLP.Language;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Test
{
    public class FakeInstrumentedSpeechSynth : ISpeechSynth
    {
        private readonly string _locale;
        private int _disposed = 0;

        public FakeInstrumentedSpeechSynth(string supportedLocale)
        {
            _locale = supportedLocale;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~FakeInstrumentedSpeechSynth()
        {
            Dispose(false);
        }
#endif

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

        public Task<SynthesizedSpeech> SynthesizeSpeechAsync(SpeechSynthesisRequest request, CancellationToken cancelToken, IRealTimeProvider realTime, ILogger logger = null)
        {
            AudioData response = DialogTestHelpers.GenerateAudioData(AudioSampleFormat.Mono(16000), 2000);

            return Task.FromResult(new SynthesizedSpeech()
            {
                Audio = response,
                Ssml = request.Ssml,
                Locale = request.Locale.ToBcp47Alpha2String(),
                PlainText = request.Plaintext
            });
        }

        public bool IsLocaleSupported(LanguageCode locale)
        {
            return object.Equals(locale, _locale);
        }

        public Task<IAudioSampleSource> SynthesizeSpeechToStreamAsync(SpeechSynthesisRequest request, WeakPointer<IAudioGraph> parentGraph, CancellationToken cancelToken, IRealTimeProvider realTime, ILogger logger = null)
        {
            AudioSample response = DialogTestHelpers.GenerateUtterance(AudioSampleFormat.Mono(16000), 2000);
            return Task.FromResult<IAudioSampleSource>(new FixedAudioSampleSource(parentGraph, response, nodeCustomName: null));
        }
    }
}
