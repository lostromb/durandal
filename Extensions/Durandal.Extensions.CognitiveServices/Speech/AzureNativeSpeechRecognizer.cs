using Durandal.Common.Audio;
using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Durandal.Common.Tasks;
using Durandal.API;
using Microsoft.CognitiveServices.Speech.Audio;
using Newtonsoft.Json;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Durandal.Common.Client;
using Durandal.Common.IO;
using Durandal.Common.Events;
using Durandal.Common.Speech.SR;
using Durandal.Common.NLP.Language;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Extensions.CognitiveServices.Speech
{
    public class AzureNativeSpeechRecognizer : AbstractAudioSampleTarget, ISpeechRecognizer
    {
        private static readonly TimeSpan FINAL_READ_TIMEOUT = TimeSpan.FromSeconds(2);

        private readonly SpeechRecognizer _speechClient;
        private readonly SpeechConfig _speechConfig;
        private readonly PushAudioInputStream _audioStream;
        private readonly ILogger _logger;
        private readonly LanguageCode _locale;
        private readonly ManualResetEventSlim _finalResponseEvent = new ManualResetEventSlim();

        private string _lastPartialResult = string.Empty;
        private Microsoft.CognitiveServices.Speech.SpeechRecognitionResult _finalResponse = null;
        private int _disposed = 0;

        public AsyncEvent<TextEventArgs> IntermediateResultEvent { get; private set; }

        private AzureNativeSpeechRecognizer(
            WeakPointer<IAudioGraph> graph,
            string nodeCustomName,
            ILogger queryLogger,
            string authToken,
            string azureRegion,
            LanguageCode locale)
            : base(graph, nameof(AzureNativeSpeechRecognizer), nodeCustomName)
        {
            _logger = queryLogger;
            IntermediateResultEvent = new AsyncEvent<TextEventArgs>();
            _speechConfig = SpeechConfig.FromAuthorizationToken(authToken, azureRegion);
            _speechConfig.OutputFormat = OutputFormat.Detailed;

            // https://docs.microsoft.com/en-US/azure/cognitive-services/speech-service/language-support#speech-to-text
            _locale = locale.AssertNonNull(nameof(locale));
            _speechConfig.SpeechRecognitionLanguage = locale.ToBcp47Alpha2String();
            _audioStream = AudioInputStream.CreatePushStream(AudioStreamFormat.GetDefaultInputFormat());
            AudioConfig audioConfig = AudioConfig.FromStreamInput(_audioStream);
            _speechClient = new SpeechRecognizer(_speechConfig, audioConfig);
            _speechClient.Recognized += OnDataShortPhraseResponseReceivedHandler;
            _speechClient.Recognizing += OnPartialResponseReceivedHandler;
            _speechClient.Canceled += OnConversationErrorHandler;

            _lastPartialResult = string.Empty;
            InputFormat = AudioSampleFormat.Mono(16000);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~AzureNativeSpeechRecognizer()
        {
            Dispose(false);
        }
#endif

        public static async Task<AzureNativeSpeechRecognizer> Create(
            WeakPointer<IAudioGraph> graph,
            string nodeCustomName,
            ILogger queryLogger,
            string authToken,
            string azureRegion,
            LanguageCode locale)
        {
            AzureNativeSpeechRecognizer returnVal = new AzureNativeSpeechRecognizer(graph, nodeCustomName, queryLogger, authToken, azureRegion, locale);
            await returnVal._speechClient.StartContinuousRecognitionAsync();
            return returnVal;
        }

        protected override ValueTask WriteAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int bytesInThisSlice = numSamplesPerChannel * sizeof(short);
            using (PooledBuffer<byte> byteBuf = BufferPool<byte>.Rent(bytesInThisSlice))
            {
                AudioMath.ConvertSamples_FloatTo2BytesIntLittleEndian(buffer, bufferOffset, byteBuf.Buffer, 0, numSamplesPerChannel);
                _audioStream.Write(byteBuf.Buffer, bytesInThisSlice);
            }

            return new ValueTask();
        }

        public async Task<Durandal.API.SpeechRecognitionResult> FinishUnderstandSpeech(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            byte[] silence = new byte[64000];
            _audioStream.Write(silence, silence.Length);
            await _speechClient.StopContinuousRecognitionAsync();

            Durandal.API.SpeechRecognitionResult returnVal = new Durandal.API.SpeechRecognitionResult();

            // Wait for the event flag from the final reco result
            if (_finalResponseEvent.Wait(FINAL_READ_TIMEOUT))
            {
                returnVal.RecognitionStatus = SpeechRecognitionStatus.NoMatch;

                if (_finalResponse == null)
                {
                    returnVal.RecognitionStatus = Durandal.API.SpeechRecognitionStatus.Error;
                }
                else
                {
                    string rawResponse = _finalResponse.Properties.GetProperty(PropertyId.SpeechServiceResponse_JsonResult);
                    InternalAzureSpeechResponse detailedResponse = JsonConvert.DeserializeObject<InternalAzureSpeechResponse>(rawResponse);

                    switch (detailedResponse.RecognitionStatus)
                    {
                        case "Success":
                            returnVal.RecognitionStatus = Durandal.API.SpeechRecognitionStatus.Success;
                            break;
                        default:
                            returnVal.RecognitionStatus = Durandal.API.SpeechRecognitionStatus.Error;
                            break;
                    }

                    if (detailedResponse.NBest != null)
                    {
                        foreach (var inputPhrase in detailedResponse.NBest)
                        {
                            Durandal.API.SpeechRecognizedPhrase newPhrase = new Durandal.API.SpeechRecognizedPhrase();
                            newPhrase.AudioTimeLength = TimeSpan.FromTicks(detailedResponse.Duration.GetValueOrDefault(0));
                            newPhrase.AudioTimeOffset = TimeSpan.FromTicks(detailedResponse.Offset.GetValueOrDefault(0));
                            newPhrase.DisplayText = inputPhrase.Display;
                            newPhrase.InverseTextNormalizationResults = new List<string>() { inputPhrase.ITN };
                            newPhrase.IPASyllables = inputPhrase.Lexical;
                            newPhrase.Locale = _locale.ToBcp47Alpha2String();
                            newPhrase.MaskedInverseTextNormalizationResults = new List<string>() { inputPhrase.MaskedITN };
                            newPhrase.ProfanityTags = null;
                            newPhrase.SREngineConfidence = inputPhrase.Confidence;
                            returnVal.RecognizedPhrases.Add(newPhrase);
                        }

                        returnVal.RecognizedPhrases.Sort((a, b) => b.SREngineConfidence.CompareTo(a.SREngineConfidence));
                    }
                }
            }
            else
            {
                returnVal.RecognitionStatus = SpeechRecognitionStatus.Cancelled;
            }

            return returnVal;
        }

        private class InternalAzureSpeechResponse
        {
            public string RecognitionStatus { get; set; }
            public long? Offset { get; set; }
            public long? Duration { get; set; }
            public IList<InternalAzureSpeechNBestResult> NBest { get; set; }
        }

        private class InternalAzureSpeechNBestResult
        {
            public float Confidence { get; set; }
            public string Lexical { get; set; }
            public string ITN { get; set; }
            public string MaskedITN { get; set; }
            public string Display { get; set; }
        }

        private void OnPartialResponseReceivedHandler(object sender, SpeechRecognitionEventArgs e)
        {
            if (e.Result != null && !string.IsNullOrEmpty(e.Result.Text))
            {
                IntermediateResultEvent.FireInBackground(sender, new TextEventArgs(e.Result.Text), _logger, DefaultRealTimeProvider.Singleton);
            }
        }

        private void OnDataShortPhraseResponseReceivedHandler(object sender, SpeechRecognitionEventArgs e)
        {
            _finalResponse = e.Result;
            _finalResponseEvent.Set();
        }

        private void OnConversationErrorHandler(object sender, SpeechRecognitionCanceledEventArgs e)
        {
            _logger.Log(string.Format("An error occurred in the azure speech recognizer: {0} {1} {2}", e.ErrorCode.ToString(), e.Reason.ToString(), e.ErrorDetails), LogLevel.Err);
            if (e.ErrorCode == CancellationErrorCode.ConnectionFailure)
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
                    _finalResponseEvent.Dispose();
                    _speechClient.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}
