#if !PCL

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Durandal.Common.AudioV2;
using Durandal.Common.Audio;
using Durandal.Common.Logger;
using System.Speech.AudioFormat;
using System.Speech.Recognition;
using System.Threading;
using System.Threading.Tasks;
using Durandal.API;

namespace Durandal.Common.Speech.SR.SAPI
{
    /// <summary>
    /// Don't use this it is terrible
    /// </summary>
    public class SapiSpeechRecognizer : ISpeechRecognizer
    {
        private readonly ILogger _logger;
        private SpeechStreamer _rawAudioStream;
        private SpeechRecognitionEngine _engine = null;
        private volatile SpeechRecognitionResult _lastRecoResult = null;
        private volatile SpeechRecognitionResult _lastRecoHyp = null;
        private int _disposed = 0;

        public SapiSpeechRecognizer(int sampleRate, ILogger logger)
        {
            _logger = logger;

            // Find the English recognizer
            RecognizerInfo info = null;
            foreach (RecognizerInfo ri in SpeechRecognitionEngine.InstalledRecognizers())
            {
                if (ri.Culture.TwoLetterISOLanguageName.Equals("en"))
                {
                    _logger.Log("Using " + ri.Description);
                    info = ri;
                    break;
                }
            }
            if (info == null)
            {
                _logger.Log("No SAPI recognizer installed for English speech!", LogLevel.Err);
                return;
            }

            // Load the dictation grammar
            _engine = new SpeechRecognitionEngine(info);
            _engine.LoadGrammar(new DictationGrammar());
            _engine.InitialSilenceTimeout = TimeSpan.FromSeconds(5);
            _engine.BabbleTimeout = TimeSpan.FromSeconds(5);
            _engine.UpdateRecognizerSetting("CFGConfidenceRejectionThreshold", 0);
            _engine.EndSilenceTimeout = TimeSpan.FromSeconds(0);
            _engine.EndSilenceTimeoutAmbiguous = TimeSpan.FromSeconds(0);
            _rawAudioStream = new SpeechStreamer(50000);
            _engine.SpeechRecognized += SpeechRecognized;
            _engine.SpeechHypothesized += SpeechHypothesized;
            _engine.SetInputToAudioStream(_rawAudioStream,
                new SpeechAudioFormatInfo(
                    sampleRate,
                    AudioBitsPerSample.Sixteen,
                    AudioChannel.Mono));
        }

        ~SapiSpeechRecognizer()
        {
            Dispose(false);
        }

        public void StartUnderstandSpeech(string locale)
        {
            _engine.RecognizeAsync(RecognizeMode.Multiple);
            _lastRecoResult = null;
            _lastRecoHyp = null;
        }

        public async Task<string> ContinueUnderstandSpeech(AudioChunk continualData)
        {
            if (_engine == null)
            {
                return null;
            }

            if (continualData != null)
            {
                _rawAudioStream.WriteAudio(continualData);
            }

            // sapi is lame
            while (_rawAudioStream.Available > 1000)
            {
                await Task.Delay(1);
            }

            if (_lastRecoHyp != null && _lastRecoHyp.RecognizedPhrases != null && _lastRecoHyp.RecognizedPhrases.Count > 0)
            {
                return _lastRecoHyp.RecognizedPhrases[0].DisplayText;
            }

            return null;
        }

        public async Task<SpeechRecognitionResult> FinishUnderstandSpeech(AudioChunk finalData = null)
        {
            SpeechRecognitionResult returnVal = new SpeechRecognitionResult();
            returnVal.RecognitionStatus = SpeechRecognitionStatus.Error;

            if (_engine == null)
            {
                _logger.Log("SAPI recognizer is not installed or not supported on this platform!", LogLevel.Wrn);
                return returnVal;
            }

            _lastRecoResult = null;
            if (finalData != null)
            {
                _rawAudioStream.WriteAudio(finalData);
            }

            while (_rawAudioStream.Available > 1000)
            {
                await Task.Delay(1);
            }

            // SAPI is really lame and we have to do this crap to inject silence into the buffer
            byte[] silence = new byte[AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE * 2];
            for (int wait = 0; wait < 10; wait++)
            {
                _rawAudioStream.Write(silence, 0, AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE);
                while (_rawAudioStream.Available > 1000)
                {
                    Thread.Sleep(1);
                }
            }

            // See if any results actually came out
            if (_lastRecoResult != null)
            {
                returnVal = _lastRecoResult;
            }

            else if (_lastRecoHyp != null)
            {
                // If no final results, use the last known hypothesis
                returnVal = _lastRecoHyp;
            }

            _engine.RecognizeAsyncCancel();

            return returnVal;
        }

        private void SpeechRecognized(object sender, SpeechRecognizedEventArgs args)
        {
            if (args.Result != null)
            {
                SpeechRecognitionResult finalResult = new SpeechRecognitionResult();
                finalResult.RecognitionStatus = SpeechRecognitionStatus.Success;
                finalResult.RecognizedPhrases.Add(new SpeechRecognizedPhrase()
                {
                    SREngineConfidence = args.Result.Confidence,
                    DisplayText = args.Result.Text,
                    AudioTimeLength = args.Result.Audio.Duration,
                    AudioTimeOffset = args.Result.Audio.AudioPosition,
                    IPASyllables = string.Empty
                });

                foreach (var alternate in args.Result.Alternates)
                {
                    finalResult.RecognizedPhrases.Add(new SpeechRecognizedPhrase()
                    {
                        SREngineConfidence = alternate.Confidence,
                        DisplayText = alternate.Text,
                        AudioTimeLength = args.Result.Audio.Duration,
                        AudioTimeOffset = args.Result.Audio.AudioPosition,
                        IPASyllables = string.Empty
                    });
                }

                _lastRecoResult = finalResult;
            }
        }

        private void SpeechHypothesized(object sender, SpeechHypothesizedEventArgs args)
        {
            if (args.Result != null)
            {
                SpeechRecognitionResult intermediateResult = new SpeechRecognitionResult();
                intermediateResult.RecognitionStatus = SpeechRecognitionStatus.Success;
                intermediateResult.RecognizedPhrases.Add(new SpeechRecognizedPhrase()
                {
                    SREngineConfidence = args.Result.Confidence,
                    DisplayText = args.Result.Text,
                    AudioTimeLength = args.Result.Audio?.Duration,
                    AudioTimeOffset = args.Result.Audio?.AudioPosition,
                    IPASyllables = string.Empty
                });

                _lastRecoHyp = intermediateResult;
            }
        }

        public void Close() { }

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
                _engine.Dispose();
                _rawAudioStream.Dispose();
            }
        }
    }
}

#endif