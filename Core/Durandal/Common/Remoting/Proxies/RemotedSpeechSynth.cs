using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Text;
using Durandal.Common.Tasks;
using Durandal.API;
using Durandal.Common.Remoting.Protocol;
using System.Threading;
using Durandal.Common.Speech.TTS;
using Durandal.Common.Audio;
using System.Threading.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.IO;
using System.IO;
using Durandal.Common.NLP.Language;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Remoting.Proxies
{
    public class RemotedSpeechSynth : ISpeechSynth
    {
        private readonly WeakPointer<RemoteDialogMethodDispatcher> _dispatcher;
        private readonly IRealTimeProvider _realTime;
        private int _disposed = 0;
            
        public RemotedSpeechSynth(
            RemoteDialogMethodDispatcher dispatcher,
            IRealTimeProvider realTime)
        {
            _dispatcher = new WeakPointer<RemoteDialogMethodDispatcher>(dispatcher);
            _realTime = realTime;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~RemotedSpeechSynth()
        {
            Dispose(false);
        }
#endif

        /// <inheritdoc />
        public bool IsLocaleSupported(LanguageCode locale)
        {
            // if locale is not actually supported then this will throw an exception at runtime
            // this is done because it's potentially non-trivial to enumerate all supported locales
            return true;
        }

        /// <inheritdoc />
        public Task<SynthesizedSpeech> SynthesizeSpeechAsync(SpeechSynthesisRequest request, CancellationToken cancelToken, IRealTimeProvider realTime, ILogger logger = null)
        {
            return _dispatcher.Value.SpeechSynth_Synthesize(request, realTime, cancelToken);
        }

        /// <inheritdoc />
        public async Task<IAudioSampleSource> SynthesizeSpeechToStreamAsync(SpeechSynthesisRequest request, WeakPointer<IAudioGraph> parentGraph, CancellationToken cancelToken, IRealTimeProvider realTime, ILogger logger = null)
        {
            SynthesizedSpeech speech = await SynthesizeSpeechAsync(request, cancelToken, realTime, logger).ConfigureAwait(false);
            if (!string.Equals(speech.Audio.Codec, RawPcmCodecFactory.CODEC_NAME_PCM_S16LE))
            {
                throw new FormatException("Expected synthesized speech in PCM format, but it was " + speech.Audio.Codec);
            }

            RawPcmDecoder decoder = new RawPcmDecoder(parentGraph, speech.Audio.CodecParams, "RemoteSpeechSynthPcmDecoder");
#pragma warning disable CA2000 // Dispose objects before losing scope (ownership of stream is transferred to the decoder)
            MemoryStream pcmDataStream = new MemoryStream(speech.Audio.Data.Array, speech.Audio.Data.Offset, speech.Audio.Data.Count, false);
            if (AudioInitializationResult.Success !=  await decoder.Initialize(new NonRealTimeStreamWrapper(pcmDataStream, true), true, cancelToken, realTime).ConfigureAwait(false))
#pragma warning restore CA2000 // Dispose objects before losing scope
            {
                throw new Exception("Couldn't initialize PCM codec. This shouldn't be possible");
            }

            return decoder;
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