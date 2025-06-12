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
    /// <summary>
    /// Speech synthesizer which instantly returns a canned AudioSample
    /// </summary>
    public class FixedSpeechSynth : ISpeechSynth
    {
        private readonly LanguageCode _locale;
        private readonly AudioSample _fixedSample;
        private int _disposed = 0;

        public FixedSpeechSynth(LanguageCode supportedLocale, AudioSample sampleToReturn)
        {
            _locale = supportedLocale;
            _fixedSample = sampleToReturn;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        public FixedSpeechSynth(LanguageCode supportedLocale, TimeSpan lengthOfRandomAudio)
        {
            _locale = supportedLocale;

            // Mix some random audio right here
            AudioSampleFormat format = AudioSampleFormat.Mono(16000);
            using (IAudioGraph disposableGraph = new AudioGraph(AudioGraphCapabilities.None))
            {
                WeakPointer<IAudioGraph> graph = new WeakPointer<IAudioGraph>(disposableGraph);
                using (LinearMixer mixer = new LinearMixer(graph, format, nodeCustomName: null))
                using (BucketAudioSampleTarget bucket = new BucketAudioSampleTarget(graph, format, nodeCustomName: null))
                {
                    mixer.ConnectOutput(bucket);
                    mixer.AddInput(new SineWaveSampleSource(graph, format, null, 440, 0.1f), channelToken: null, takeOwnership: true);
                    mixer.AddInput(new SineWaveSampleSource(graph, format, null, 1204, 0.1f), channelToken: null, takeOwnership: true);
                    mixer.AddInput(new SineWaveSampleSource(graph, format, null, 1794, 0.1f), channelToken: null, takeOwnership: true);
                    mixer.AddInput(new SineWaveSampleSource(graph, format, null, 2509, 0.1f), channelToken: null, takeOwnership: true);
                    mixer.AddInput(new SineWaveSampleSource(graph, format, null, 4431, 0.1f), channelToken: null, takeOwnership: true);
                    mixer.AddInput(new SineWaveSampleSource(graph, format, null, 5810, 0.1f), channelToken: null, takeOwnership: true);
                    bucket.ReadSamplesFromInput((int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, lengthOfRandomAudio), CancellationToken.None, DefaultRealTimeProvider.Singleton).Await();
                    _fixedSample = bucket.GetAllAudio();
                }
            }

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~FixedSpeechSynth()
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
            byte[] convertedData = new byte[_fixedSample.LengthSamplesPerChannel * 2 * _fixedSample.Format.NumChannels];
            AudioMath.ConvertSamples_FloatTo2BytesIntLittleEndian(_fixedSample.Data.Array, _fixedSample.Data.Offset, convertedData, 0, _fixedSample.LengthSamplesPerChannel);
            return Task.FromResult(new SynthesizedSpeech()
            {
                Audio = new AudioData()
                {
                    Codec = RawPcmCodecFactory.CODEC_NAME_PCM_S16LE,
                    CodecParams = CommonCodecParamHelper.CreateCodecParams(_fixedSample.Format),
                    Data = new ArraySegment<byte>(convertedData)
                },
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
            return Task.FromResult<IAudioSampleSource>(new FixedAudioSampleSource(parentGraph, _fixedSample, nodeCustomName: null));
        }
    }
}
