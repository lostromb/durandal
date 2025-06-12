namespace Durandal.Common.Speech.TTS
{
    using Durandal.API;
    using Durandal.Common.Audio;
    using Durandal.Common.Logger;
    using Durandal.Common.Utils;
    using System;
    using System.Threading.Tasks;
    using Durandal.Common.Time;
    using System.Threading;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.ServiceMgmt;

    /// <summary>
    /// A simple interface that defines a TTS engine
    /// </summary>
    public interface ISpeechSynth : IDisposable
    {
        /// <summary>
        /// Accepts an SSML string and renders it as an encoded audio blob with metadata
        /// </summary>
        /// <param name="request">The input to the synthesizer, containing SSML, locale, and voice preferences.</param>
        /// <param name="cancelToken">Cancellation token</param>
        /// <param name="realTime">Real time</param>
        /// <param name="traceLogger">An optional tracing logger for debugging</param>
        /// <returns>An object representing the synthesized speech</returns>
        Task<SynthesizedSpeech> SynthesizeSpeechAsync(
            SpeechSynthesisRequest request,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            ILogger traceLogger = null);

        /// <summary>
        /// Creates an audio sample source that will stream synthesized speech audio asynchronously
        /// </summary>
        /// <param name="request">The input to the synthesizer, containing SSML, locale, and voice preferences.</param>
        /// <param name="parentGraph">The audio graph that the generated sample source will be a part of</param>
        /// <param name="cancelToken">Cancellation token</param>
        /// <param name="realTime">Real time</param>
        /// <param name="traceLogger">An optional tracing logger for debugging</param>
        /// <returns>An audio sample source that will produce the synthesized audio</returns>
        Task<IAudioSampleSource> SynthesizeSpeechToStreamAsync(
            SpeechSynthesisRequest request,
            WeakPointer<IAudioGraph> parentGraph,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            ILogger traceLogger = null);

        /// <summary>
        /// Tests if the given locale is supported by this synth
        /// </summary>
        /// <param name="locale">The locale string e.g. "en-US"</param>
        /// <returns>True if the locale is supported for synthesis</returns>
        bool IsLocaleSupported(LanguageCode locale);
    }
}
