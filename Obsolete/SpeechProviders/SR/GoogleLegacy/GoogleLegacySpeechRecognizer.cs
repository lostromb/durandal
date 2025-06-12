

namespace Durandal.Common.Speech.SR.GoogleLegacy
{
    using Durandal.Common.Audio;
    using Durandal.Common.AudioV2;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.Logger;
    using Durandal.Common.Audio.Codecs;
    using Durandal.Common.Utils;
    using Durandal.Common.File;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Threading;
    using Durandal.Common.IO;
    using Durandal.API;

    public class GoogleLegacySpeechRecognizer : ISpeechRecognizer
    {
        private const string API_KEY = "AIzaSyBOti4mM-6x9WDnZIjIeyEU21OpBXqWBgw"; // don't know whose key this is
        
        private readonly ILogger _logger;
        private readonly IAudioCodec _flacEncoder;

        // Variables used for streaming uploads to the sr service
        private HttpWebRequest _uploadClient;
        private Stream _uploadStream;
        private IAudioCompressionStream _flacEncodeStream;
        private int _disposed = 0;
        
        public GoogleLegacySpeechRecognizer(ILogger logger, IAudioCodec flacCodec, bool enableIntermediateResults)
        {
            _logger = logger;
            _flacEncoder = flacCodec;
        }

        ~GoogleLegacySpeechRecognizer()
        {
            Dispose(false);
        }

        public void StartUnderstandSpeech(string locale)
        {
            try
            {
                _flacEncodeStream = _flacEncoder.CreateCompressionStream(AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE);

                // Open an HTTP stream to the reco service
                string url = "https://www.google.com/speech-api/v2/recognize?xjerr=1&client=chromium&lang=en-US&maxresults=8&key=" + API_KEY;
                _uploadClient = (HttpWebRequest)HttpWebRequest.Create(url);
                _uploadClient.Method = "POST";
                _uploadClient.ReadWriteTimeout = 1000;
                _uploadClient.ContentType = "audio/x-flac; rate=" + AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE;
                _uploadClient.UserAgent = "Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/31.0.1650.63 Safari/537.36";
                _uploadClient.SendChunked = true;
                _uploadStream = _uploadClient.GetRequestStream();
            }
            catch (Exception e)
            {
                _logger.Log("Caught exception in SR while starting speech reco: " + e.Message, LogLevel.Err);
            }
        }

        public async Task<string> ContinueUnderstandSpeech(AudioChunk continualData)
        {
            try
            {
                if (continualData != null)
                {
                    byte[] encodedFlac = _flacEncodeStream.Compress(continualData);
                    if (encodedFlac.Length > 0)
                    {
                        await _uploadStream.WriteAsync(encodedFlac, 0, encodedFlac.Length);
                    }
                }

                return string.Empty;
            }
            catch (Exception e)
            {
                _logger.Log("Caught exception in SR while starting speech reco: " + e.Message, LogLevel.Err);
                return string.Empty;
            }
        }

        public async Task<SpeechRecognitionResult> FinishUnderstandSpeech(AudioChunk finalData = null)
        {
            // Write the last audio chunk to the flac process
            await ContinueUnderstandSpeech(finalData);
            SpeechRecognitionResult fallbackResults = new SpeechRecognitionResult();
            
            try
            {
                // Get the flac data footer and write it
                byte[] finalChunk = _flacEncodeStream.Close();
                if (finalChunk != null)
                {
                    _uploadStream.Write(finalChunk, 0, finalChunk.Length);
                }
                _uploadStream.Close();

                // Finalize the reco and get the results
                Stopwatch timer = new Stopwatch();
                timer.Start();
                byte[] rawResult;
                using (WebResponse webResponse = _uploadClient.GetResponse())
                {
                    using (Stream responseStream = webResponse.GetResponseStream())
                    {
                        using (RecyclableMemoryStream finalResponse = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
                        {
                            SimplePipe pipe = new SimplePipe(responseStream, finalResponse);
                            pipe.Drain();
                            responseStream.Close();
                            rawResult = finalResponse.ToArray();
                        }
                    }
                    webResponse.Close();
                }

                string googleJson = Encoding.UTF8.GetString(rawResult, 0, rawResult.Length);
                timer.Stop();
                _logger.Log(CommonInstrumentation.GenerateLatencyEntry("GoogleSpeechReco", timer), LogLevel.Ins);

                _logger.Log(googleJson, LogLevel.Vrb);
                // Take all the n-best utterances from the result
                SpeechRecognitionResult returnVal = new SpeechRecognitionResult();
                returnVal.RecognitionStatus = SpeechRecognitionStatus.NoMatch;
                Regex utteranceMatcher = new Regex("\"transcript\": ?\"(.+?)\"");
                Regex confidenceMatcher = new Regex("\"confidence\": ?([0-9\\.]+)");
                float topConfidence = 0.95f;
                const float confidenceDecay = 0.95f;
                Match confidenceMatch = confidenceMatcher.Match(googleJson);
                if (confidenceMatch.Success)
                {
                    topConfidence = float.Parse(confidenceMatch.Groups[1].Value);
                }
                MatchCollection matches = utteranceMatcher.Matches(googleJson);
                foreach (Match m in matches)
                {
                    if (m.Success)
                    {
                        returnVal.RecognizedPhrases.Add(new SpeechRecognizedPhrase()
                        {
                            DisplayText = m.Groups[1].Value,
                            SREngineConfidence = topConfidence,
                            IPASyllables = m.Groups[1].Value
                        });
                        returnVal.RecognitionStatus = SpeechRecognitionStatus.Success;
                    }
                    topConfidence *= confidenceDecay;
                }

                // If the service returned nothing, fall back on the SAPI results as a last resort
                if (returnVal == null || returnVal.RecognizedPhrases == null || returnVal.RecognizedPhrases.Count == 0)
                {
                    _logger.Log("Google SR service provided no results! Falling back to on-device reco", LogLevel.Wrn);
                    returnVal = fallbackResults;
                }
                return returnVal;
            }
            catch (Exception e)
            {
                _logger.Log("Caught exception in SR while starting speech reco: " + e.Message, LogLevel.Err);
                return fallbackResults;
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
                _uploadStream.Dispose();
                _flacEncodeStream.Dispose();
            }
        }
    }
}
