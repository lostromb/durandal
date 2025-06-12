using Durandal.Common.Audio;
using Durandal.Common.AudioV2;
using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.SpeechRecognition;
using Durandal.Common.Tasks;
using Durandal.API;

namespace Durandal.Common.Speech.SR.Oxford
{
    public class OxfordSpeechRecognizer : ISpeechRecognizer
    {
        private readonly DataRecognitionClient _speechClient;
        private readonly ILogger _logger;
        private readonly string _locale;
        private readonly ManualResetEventSlim _finalResponseEvent = new ManualResetEventSlim();

        private string _lastPartialResult = string.Empty;
        private bool _sentFormat = false;
        private RecognitionResult _finalResponse = null;
        private int _disposed = 0;
        
        public OxfordSpeechRecognizer(ILogger queryLogger, string apiKey, string locale)
        {
            _logger = queryLogger;
            _locale = locale;
            _speechClient = SpeechRecognitionServiceFactory.CreateDataClient(
                SpeechRecognitionMode.ShortPhrase,
                locale,
                apiKey);

            _speechClient.OnResponseReceived += OnDataShortPhraseResponseReceivedHandler;
            _speechClient.OnPartialResponseReceived += OnPartialResponseReceivedHandler;
            _speechClient.OnConversationError += OnConversationErrorHandler;
            _lastPartialResult = string.Empty;
            _sentFormat = false;
        }

        ~OxfordSpeechRecognizer()
        {
            Dispose(false);
        }

        public async Task<string> ContinueUnderstandSpeech(AudioChunk continualData)
        {
            lock (this)
            {
                if (!_sentFormat)
                {
                    SpeechAudioFormat format = new SpeechAudioFormat()
                    {
                        BitsPerSample = 16,
                        AverageBytesPerSecond = continualData.SampleRate * 2,
                        BlockAlign = 2,
                        ChannelCount = 1,
                        EncodingFormat = AudioCompressionType.PCM,
                        SamplesPerSecond = continualData.SampleRate
                    };

                    _speechClient.SendAudioFormat(format);
                    _sentFormat = true;
                }
            }

            if (continualData != null && continualData.DataLength > 0)
            {
                byte[] audio = continualData.GetDataAsBytes();
                _speechClient.SendAudio(audio, audio.Length);
                await DurandalTaskExtensions.NoOpTask;
            }

            lock (_lastPartialResult)
            {
                return _lastPartialResult;
            }
        }

        public async Task<SpeechRecognitionResult> FinishUnderstandSpeech(AudioChunk continualData = null)
        {
            await ContinueUnderstandSpeech(continualData);
            
            _speechClient.EndAudio();

            // Wait for the event flag from the final reco result
            _finalResponseEvent.Wait();

            SpeechRecognitionResult returnVal = new SpeechRecognitionResult();
            returnVal.RecognitionStatus = SpeechRecognitionStatus.NoMatch;

            if (_finalResponse != null)
            {
                foreach (var recognitionHyp in _finalResponse.Results)
                {
                    returnVal.RecognitionStatus = SpeechRecognitionStatus.Success; // FIXME pass through recognition status properly
                    returnVal.RecognizedPhrases.Add(
                        new SpeechRecognizedPhrase()
                        {
                            IPASyllables = recognitionHyp.LexicalForm,
                            DisplayText = recognitionHyp.DisplayText,
                            SREngineConfidence = InterpretConfidence(recognitionHyp.Confidence),
                            InverseTextNormalizationResults = new List<string>() { recognitionHyp.InverseTextNormalizationResult },
                            Locale = _locale,
                            MaskedInverseTextNormalizationResults = new List<string>() { recognitionHyp.MaskedInverseTextNormalizationResult }
                        });
                }
            }

            return returnVal;
        }

        private static float InterpretConfidence(Confidence c)
        {
            switch (c)
            {
                case Confidence.High:
                    return 0.9f;
                case Confidence.Normal:
                    return 0.7f;
                case Confidence.Low:
                    return 0.3f;
                case Confidence.None:
                    return 0.1f;
            }

            return 0.0f;
        }

        private void OnPartialResponseReceivedHandler(object sender, PartialSpeechResponseEventArgs e)
        {
            lock (_lastPartialResult)
            {
                // _logger.Log("Partial " + e.PartialResult);
                _lastPartialResult = e.PartialResult;
            }
        }

        private void OnDataShortPhraseResponseReceivedHandler(object sender, SpeechResponseEventArgs e)
        {
            _finalResponse = e.PhraseResponse;
            _finalResponseEvent.Set();
        }

        private void OnConversationErrorHandler(object sender, SpeechErrorEventArgs e)
        {
            _logger.Log(string.Format("An error occurred in the oxford speech recognizer: {0} {1}", e.SpeechErrorCode.ToString(), e.SpeechErrorText), LogLevel.Err);
            if (e.SpeechErrorCode == SpeechClientStatus.NameNotFound)
            {
                _logger.Log("It seems that there is no active internet connection", LogLevel.Err);
            }
            _finalResponse = null;
            _finalResponseEvent.Set();
        }

        public void Close()
        {
            Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            {
                return;
            }

            if (!disposing) Durandal.Common.Utils.DebugMemoryLeaktracer.TraceDisposableItemFinalized(this.GetType());

            if (disposing)
            {
                _finalResponseEvent.Dispose();
                _speechClient.Dispose();
            }
        }
    }
}
