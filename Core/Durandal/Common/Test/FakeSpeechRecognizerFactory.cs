namespace Durandal.Common.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Durandal.API;
    using Durandal.Common.Audio;
    using Durandal.Common.Logger;
    using Durandal.Common.Speech.SR;
    using System.Threading.Tasks;
    using Durandal.Common.Time;
    using System.Threading;
    using Utils;
    using Client;
    using Tasks;
    using System.Globalization;
    using Durandal.Common.Events;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.ServiceMgmt;

    public class FakeSpeechRecognizerFactory : ISpeechRecognizerFactory
    {
        private readonly bool _addDelay;
        private readonly IDictionary<string, SpeechRecognitionResult> _responseDictionary;
        private readonly AudioSampleFormat _recognizerAudioFormat;
        private int _disposed = 0;

        public FakeSpeechRecognizerFactory(AudioSampleFormat recognizerAudioFormat, bool addDelay = false)
        {
            _addDelay = addDelay;
            _responseDictionary = new Dictionary<string, SpeechRecognitionResult>(StringComparer.OrdinalIgnoreCase);
            _recognizerAudioFormat = recognizerAudioFormat;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~FakeSpeechRecognizerFactory()
        {
            Dispose(false);
        }
#endif

        public bool ShouldThrowExceptionOnIntermediateRecognize { get; set; }
        public bool ShouldThrowExceptionOnFinishRecognize { get; set; }

        /// <inheritdoc />
        public Task<ISpeechRecognizer> CreateRecognitionStream(
            WeakPointer<IAudioGraph> audioGraph,
            string graphNodeName,
            LanguageCode locale,
            ILogger queryLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            SpeechRecognitionResult recoResult;
            if (_responseDictionary.TryGetValue(locale.ToBcp47Alpha2String(), out recoResult))
            {
                FakeSpeechRecognizer reco = new FakeSpeechRecognizer(audioGraph, recoResult, _recognizerAudioFormat, _addDelay, ShouldThrowExceptionOnIntermediateRecognize, ShouldThrowExceptionOnFinishRecognize);
                return Task.FromResult<ISpeechRecognizer>(reco);
            }
            else
            {
                return Task.FromResult<ISpeechRecognizer>(new NullSpeechReco(audioGraph, _recognizerAudioFormat));
            }
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

        public bool IsLocaleSupported(LanguageCode locale)
        {
            return _responseDictionary.ContainsKey(locale.ToBcp47Alpha2String());
        }

        /// <summary>
        /// Clears all mock reco results
        /// </summary>
        public void ClearRecoResults()
        {
            _responseDictionary.Clear();
        }

        /// <summary>
        /// Sets the reco result for the specified locale to the given phrase
        /// </summary>
        /// <param name="locale"></param>
        /// <param name="recognizedText"></param>
        public void SetRecoResult(string locale, string recognizedText)
        {
            SpeechRecognitionResult recoResult = new SpeechRecognitionResult()
            {
                RecognitionStatus = SpeechRecognitionStatus.Success,
                RecognizedPhrases = new List<SpeechRecognizedPhrase>()
            };

            recoResult.RecognizedPhrases.Add(new SpeechRecognizedPhrase()
            {
                DisplayText = recognizedText,
                IPASyllables = string.Empty,
                SREngineConfidence = 0.95f,
                InverseTextNormalizationResults = new List<string>()
                    {
                        recognizedText
                    },
                MaskedInverseTextNormalizationResults = new List<string>()
                    {
                        recognizedText
                    }
            });

            SetRecoResult(locale, recoResult);
        }

        /// <summary>
        /// Sets the reco result for the specified locale to the given value
        /// </summary>
        /// <param name="locale"></param>
        /// <param name="recoResult"></param>
        public void SetRecoResult(string locale, SpeechRecognitionResult recoResult)
        {
            if (recoResult == null)
            {
                _responseDictionary.Remove(locale);
            }
            else
            {
                _responseDictionary[locale] = recoResult;
            }
        }

        private class FakeSpeechRecognizer : AbstractAudioSampleTarget, ISpeechRecognizer
        {
            private readonly SpeechRecognitionResult _returnVal;
            private readonly bool _addDelay;
            private readonly bool _throwExceptionOnIntermediate;
            private readonly bool _throwExceptionOnFinish;

            public FakeSpeechRecognizer(WeakPointer<IAudioGraph> audioGraph, SpeechRecognitionResult returnVal, AudioSampleFormat inputFormat, bool addDelay, bool throwOnIntermediate, bool throwOnFinish)
                : base(audioGraph, nameof(FakeSpeechRecognizer), nodeCustomName: null)
            {
                _returnVal = returnVal;
                _addDelay = addDelay;
                _throwExceptionOnIntermediate = throwOnIntermediate;
                _throwExceptionOnFinish = throwOnFinish;
                IntermediateResultEvent = new AsyncEvent<TextEventArgs>();
                InputFormat = inputFormat;
            }

            public AsyncEvent<TextEventArgs> IntermediateResultEvent { get; private set; }
            
            public async Task<SpeechRecognitionResult> FinishUnderstandSpeech(CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                if (_addDelay)
                {
                    await realTime.WaitAsync(TimeSpan.FromMilliseconds(50), cancelToken).ConfigureAwait(false);
                }

                if (_throwExceptionOnFinish)
                {
                    throw new ArrayTypeMismatchException("Here is a test exception throw by FakeSpeechRecognizer.FinishUnderstandSpeech");
                }

                return _returnVal;
            }

            public void Close() { }

            protected override async ValueTask WriteAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                if (_addDelay)
                {
                    await realTime.WaitAsync(TimeSpan.FromMilliseconds(10), cancelToken).ConfigureAwait(false);
                }
                
                if (_throwExceptionOnIntermediate)
                {
                    throw new ArrayTypeMismatchException("Here is a test exception throw by FakeSpeechRecognizer.WriteAsyncInternal");
                }

                string intermediateResult = Guid.NewGuid().ToString("N").Substring(0, 12);
                IntermediateResultEvent.FireInBackground(this, new TextEventArgs(intermediateResult), NullLogger.Singleton, realTime);
            }
        }
    }
}
