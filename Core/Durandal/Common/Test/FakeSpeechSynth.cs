using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Audio;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using Durandal.Common.Tasks;
using Durandal.Common.Speech.TTS;
using Durandal.Common.Speech;
using Durandal.API;
using Durandal.Common.Audio.Codecs;
using System.Threading;
using Durandal.Common.Audio.Components;
using Durandal.Common.Time;
using Durandal.Common.NLP.Language;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Test
{
    public class FakeSpeechSynth : ISpeechSynth
    {
        private readonly LanguageCode _locale;
        private int _disposed = 0;

        public FakeSpeechSynth(LanguageCode supportedLocale)
        {
            _locale = supportedLocale;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~FakeSpeechSynth()
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

        public async Task<SynthesizedSpeech> SynthesizeSpeechAsync(SpeechSynthesisRequest request, CancellationToken cancelToken, IRealTimeProvider realTime, ILogger logger = null)
        {
            byte[] chars = Encoding.UTF8.GetBytes(request.Ssml);
            short[] values = new short[chars.Length];
            for (int c = 0; c < chars.Length; c++)
            {
                values[c] = chars[c];
            }

            await realTime.WaitAsync(TimeSpan.FromMilliseconds(10), cancelToken).ConfigureAwait(false);

            return new SynthesizedSpeech()
            {
                Audio = new AudioData()
                {
                    Codec = RawPcmCodecFactory.CODEC_NAME_PCM_S16LE,
                    CodecParams = CommonCodecParamHelper.CreateCodecParams(AudioSampleFormat.Mono(16000)),
                    Data = new ArraySegment<byte>(chars)
                },
                Ssml = request.Ssml,
                Locale = request.Locale.ToBcp47Alpha2String(),
                PlainText = request.Plaintext
            };
        }

        public bool IsLocaleSupported(LanguageCode locale)
        {
            return object.Equals(locale, _locale);
        }

        public async Task<IAudioSampleSource> SynthesizeSpeechToStreamAsync(SpeechSynthesisRequest request, WeakPointer<IAudioGraph> parentGraph, CancellationToken cancelToken, IRealTimeProvider realTime, ILogger logger = null)
        {
            byte[] chars = Encoding.UTF8.GetBytes(request.Ssml);
            float[] values = new float[chars.Length];
            for (int c = 0; c < chars.Length; c++)
            {
                values[c] = chars[c];
            }

            AudioSample sample = new AudioSample(values, AudioSampleFormat.Mono(16000));
            await realTime.WaitAsync(TimeSpan.FromMilliseconds(10), cancelToken).ConfigureAwait(false);
            return new FixedAudioSampleSource(parentGraph, sample, nodeCustomName: null);
        }
    }
}
