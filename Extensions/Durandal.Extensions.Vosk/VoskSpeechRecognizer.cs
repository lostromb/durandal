using Durandal.Common.Audio;
using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Tasks;
using Durandal.API;
using Newtonsoft.Json;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Durandal.Common.Client;
using Durandal.Common.IO;
using Durandal.Common.Events;
using Durandal.Common.Speech.SR;
using Durandal.Common.NLP.Language;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Collections;
using System.Diagnostics;
using Durandal.Common.IO.Json;
using Durandal.Common.NLP.Alignment;
using Durandal.Common.NLP;
using Durandal.Extensions.Vosk.Adapter;
using Durandal.Common.Cache;

namespace Durandal.Extensions.Vosk
{
    internal class VoskSpeechRecognizer : AbstractAudioSampleTarget, ISpeechRecognizer
    {
        private readonly ILogger _logger;
        private readonly LockFreeCache<VoskRecognizer> _recognizerCache;
        private readonly VoskRecognizer _recognizer;
        private readonly NLPTools _nlTools;
        private readonly LanguageCode _locale;
        private string _lastIntermediateResult = string.Empty;
        private int _disposed = 0;

        internal VoskSpeechRecognizer(
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat inputFormat,
            string nodeCustomName,
            ILogger queryLogger,
            LanguageCode locale,
            LockFreeCache<VoskRecognizer> recognizerCache,
            VoskRecognizer recognizer,
            NLPTools nlTools) // may be null
            : base(graph, nameof(VoskSpeechRecognizer), nodeCustomName)
        {
            _logger = queryLogger.AssertNonNull(nameof(queryLogger));
            _recognizer = recognizer.AssertNonNull(nameof(recognizer));
            _recognizerCache = recognizerCache.AssertNonNull(nameof(recognizerCache));
            _locale = locale.AssertNonNull(nameof(locale));
            _recognizer.SetMaxAlternatives(5);
            _recognizer.SetWords(true);
            _recognizer.SetPartialWords(true);
            _nlTools = nlTools;
            IntermediateResultEvent = new AsyncEvent<TextEventArgs>();
            InputFormat = inputFormat;
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~VoskSpeechRecognizer()
        {
            Dispose(false);
        }
#endif

        public AsyncEvent<TextEventArgs> IntermediateResultEvent { get; private set; }

        protected override async ValueTask WriteAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            using (PooledBuffer<short> scratch = BufferPool<short>.Rent(numSamplesPerChannel))
            {
                AudioMath.ConvertSamples_FloatToInt16(buffer, bufferOffset, scratch.Buffer, 0, numSamplesPerChannel);
                bool isCompleteResult = _recognizer.AcceptWaveform(scratch.Buffer, numSamplesPerChannel); // Vosk doesn't seem to like [-1.0, 1.0] normalized float input, so convert to int16

                if (isCompleteResult)
                {
                    // ??????
                    //string s = _recognizer.Result();
                    //Console.WriteLine(s);
                }
                else
                {
                    string partialResultJson = _recognizer.PartialResult();
                    PartialRecognitionResult prr = JsonConvert.DeserializeObject<PartialRecognitionResult>(partialResultJson);
                    if (!string.Equals(prr.Partial, _lastIntermediateResult))
                    {
                        _lastIntermediateResult = prr.Partial;
                        await IntermediateResultEvent.Fire(this, new TextEventArgs(_lastIntermediateResult), realTime).ConfigureAwait(false);
                    }
                }
            }
        }

        public Task<SpeechRecognitionResult> FinishUnderstandSpeech(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            SpeechRecognitionResult returnVal = new SpeechRecognitionResult();
            string finalResult = _recognizer.FinalResult();
            if (string.IsNullOrEmpty(finalResult))
            {
                returnVal.RecognitionStatus = SpeechRecognitionStatus.NoMatch;
                return Task.FromResult(returnVal);
            }

            FinalRecognitionResult parsedResult = JsonConvert.DeserializeObject<FinalRecognitionResult>(finalResult);
            if (parsedResult == null || parsedResult.Alternatives.Count == 0)
            {
                returnVal.RecognitionStatus = SpeechRecognitionStatus.NoMatch;
                return Task.FromResult(returnVal);
            }

            returnVal.RecognitionStatus = SpeechRecognitionStatus.Success;

            foreach (RecognitionHyp hyp in parsedResult.Alternatives)
            {
                SpeechRecognizedPhrase convertedPhrase = new SpeechRecognizedPhrase()
                {
                    DisplayText = hyp.Text,
                    LexicalForm = hyp.Text,
                    Locale = _locale.ToBcp47Alpha2String(),
                    SREngineConfidence = hyp.Confidence,
                };

                IPronouncer pronouncer = _nlTools?.Pronouncer;
                if (pronouncer != null)
                {
                    convertedPhrase.IPASyllables = pronouncer.PronouncePhraseAsString(hyp.Result.Select((s) => s.Word));
                }

                if (hyp.Result != null && hyp.Result.Count > 0)
                {
                    convertedPhrase.AudioTimeOffset = hyp.Result[0].Start;
                    convertedPhrase.AudioTimeLength = hyp.Result[hyp.Result.Count - 1].End - hyp.Result[0].Start;
                    foreach (RecognizedWord word in hyp.Result)
                    {
                        convertedPhrase.PhraseElements.Add(new SpeechPhraseElement()
                        {
                            AudioTimeOffset = word.Start,
                            AudioTimeLength = word.End - word.Start,
                            DisplayText = word.Word,
                            LexicalForm = word.Word,
                            IPASyllables = pronouncer == null ? string.Empty : pronouncer.PronounceAsString(word.Word),
                        });
                    }
                }

                returnVal.RecognizedPhrases.Add(convertedPhrase);
            }

            return Task.FromResult(returnVal);
        }

        public void Close()
        {
            Dispose();
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
                    var localReco = _recognizer;
                    if (!_recognizerCache.TryEnqueue(ref localReco))
                    {
                        // Couldn't recycle it, so make sure to actually dispose of native resources
                        localReco.Dispose();
                    }
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        private class PartialRecognitionResult
        {
            [JsonProperty("partial")]
            public string Partial { get; set; }

            [JsonProperty("partial_result")]
            public List<RecognizedWord> PartialResult { get; set; }
        }

        private class FinalRecognitionResult
        {
            [JsonProperty("alternatives")]
            public List<RecognitionHyp> Alternatives { get; set; }
        }

        private class RecognitionHyp
        {
            [JsonProperty("confidence")]
            public float Confidence { get; set; }

            [JsonProperty("result")]
            public List<RecognizedWord> Result { get; set; }

            [JsonProperty("text")]
            public string Text { get; set; }
        }

        private class RecognizedWord
        {
            [JsonProperty("conf")]
            public float? Conf { get; set; }

            [JsonProperty("start")]
            [JsonConverter(typeof(VoskTimeSpanJsonParser))]
            public TimeSpan Start { get; set; }

            [JsonProperty("end")]
            [JsonConverter(typeof(VoskTimeSpanJsonParser))]
            public TimeSpan End { get; set; }

            [JsonProperty("word")]
            public string Word { get; set; }
        }
    }
}
