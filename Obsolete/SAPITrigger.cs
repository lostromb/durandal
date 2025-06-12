using System;
using System.Speech.AudioFormat;
using System.Speech.Recognition;
using Durandal.API;
using Durandal.Common.Audio;

namespace Durandal.Common.Speech.Triggers
{
    using Stromberg.Config;
    using Durandal.API.Utils;
    using Stromberg.Logger;
    using Durandal.Common.Client;
    using Stromberg.Utils;
    using System.Threading;
    using System.Collections.Generic;
    using Stromberg.Utils.IO;

    public class SAPITrigger : IAudioTrigger
    {
        private const int SAMPLE_RATE = AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE;
        private const int ASYNC_RENDEVOUS_TIME = 5;
        private const int TIMEOUT_BETWEEN_TRIGGERS = 2000;
        private const double REQUIRED_VOLUME = 300;
        private const int OVERDRIVE_SILENCE_AMOUNT = 2000;
        private const int OVERDRIVE_BUFFER_SIZE = SAMPLE_RATE;
        private const int CHUNK_SIZE = SAMPLE_RATE / 10;
        
        private SpeechStreamer _rawAudioStream;
        private SpeechRecognitionEngine _engine;
        private volatile bool _triggered = false;
        private volatile int _msSinceLastReset;
        private readonly ILogger _logger;
        private readonly float _requiredConfidence;
        private BasicBufferShort _inputBuffer = new BasicBufferShort(SAMPLE_RATE * 10);
        private int _overdriveBufferIndex = 0;
        private short[] _overdriveBuffer = new short[OVERDRIVE_BUFFER_SIZE];
        
        public SAPITrigger(string triggerWord,
            ILogger logger,
            float sensitivity = 0.0f)
        {
            _logger = logger;
            // Set the sensitivity. From experimentation:
            // 0.84 will catch all triggers
            // 0.90 will catch most
            // 0.99 will catch none
            _requiredConfidence = 0.92f - (sensitivity * 0.6f);
            _msSinceLastReset = TIMEOUT_BETWEEN_TRIGGERS;
            
            // Build a grammar that only contains the trigger word
            GrammarBuilder builder = new GrammarBuilder(triggerWord);
            Grammar speechGrammar = new Grammar(builder);

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

            _engine = new SpeechRecognitionEngine(info);

            _engine.LoadGrammar(speechGrammar);

            _engine.InitialSilenceTimeout = TimeSpan.FromSeconds(1);
            _engine.BabbleTimeout = TimeSpan.FromSeconds(1);
            _engine.UpdateRecognizerSetting("CFGConfidenceRejectionThreshold", 0);
            _engine.EndSilenceTimeout = TimeSpan.FromSeconds(0);
            _engine.EndSilenceTimeoutAmbiguous = TimeSpan.FromSeconds(0);
            _rawAudioStream = new SpeechStreamer(10000 + OVERDRIVE_BUFFER_SIZE + (SAMPLE_RATE * OVERDRIVE_SILENCE_AMOUNT / 1000));
            _engine.SpeechRecognized += SpeechRecognized;
            _engine.SetInputToAudioStream(_rawAudioStream, 
                new SpeechAudioFormatInfo(
                    SAMPLE_RATE,
                    AudioBitsPerSample.Sixteen,
                    AudioChannel.Mono));

            _logger.Log("SAPI continuous listener OK");

            _engine.RecognizeAsync(RecognizeMode.Multiple);
        }

        private void SpeechRecognized(object sender, SpeechRecognizedEventArgs args)
        {
            var result = args.Result;

            if (result == null || result.Words.Count == 0)
            {
                return;
            }

            curMeasure = Math.Max(curMeasure, result.Confidence);
            
            // Negative samples: mean = 0.916 dev = 0.151
            // Positive samples: mean = 0.969 dev = 0.123
            //curMeasure = Math.Max(args.Result.Confidence, curMeasure);
            if (_msSinceLastReset > TIMEOUT_BETWEEN_TRIGGERS &&
                result.Confidence > _requiredConfidence)
            {
                _triggered = true;
                // _logger.Log("SAPI trigger with confidence " + args.Result.Confidence, LogLevel.Std);
            }
        }

        public void Reset()
        {
            _msSinceLastReset = 0;
        }

        private IList<float> measures = new List<float>();
        private float curMeasure = 0.0f;
        private StaticAverage mean = new StaticAverage();

        public void Measure()
        {
            // Gather statistics
            measures.Add(curMeasure);
            mean.Add(curMeasure);
            // Calculate mean and deviation
            float u = 0;
            float m = (float)mean.Average;
            foreach (float x in measures)
            {
                u += ((m - x) * (m - x)) / measures.Count;
            }
            u = (float)Math.Sqrt(u);
            Console.WriteLine("Mean: " + m + " StDev: " + u);
            curMeasure = 0.0f;
        }

        public void Dispose()
        {
            lock (this)
            {
                if (_engine != null)
                {
                    _rawAudioStream.Close();
                    _engine.RecognizeAsyncStop();
                    _rawAudioStream.Dispose();
                    _engine.Dispose();
                    _engine = null;
                    _logger.Log("Sapi resources disposed successfully", LogLevel.Vrb);
                }
            }
        }

        private void WriteToOverdrive(AudioChunk newAudioData)
        {
            int leftSize = Math.Min(newAudioData.DataLength, OVERDRIVE_BUFFER_SIZE - _overdriveBufferIndex);
            Array.Copy(newAudioData.Data, 0, _overdriveBuffer, _overdriveBufferIndex, leftSize);
            int rightSize = newAudioData.DataLength - leftSize;
            if (rightSize > 0)
            {
                Array.Copy(newAudioData.Data, 0, _overdriveBuffer, 0, rightSize);
                _overdriveBufferIndex = rightSize;
            }
            else
            {
                _overdriveBufferIndex += leftSize;
            }
        }

        private AudioChunk ReadOverdriveBuffer()
        {
            short[] returnData = new short[OVERDRIVE_BUFFER_SIZE];
            int leftSize = OVERDRIVE_BUFFER_SIZE - _overdriveBufferIndex;
            int rightSize = OVERDRIVE_BUFFER_SIZE - leftSize;
            Array.Copy(_overdriveBuffer, _overdriveBufferIndex, returnData, 0, leftSize);
            Array.Copy(_overdriveBuffer, 0, returnData, leftSize, rightSize);
            return new AudioChunk(returnData, SAMPLE_RATE);
        }

        private AudioChunk GetSilence(int ms)
        {
            return new AudioChunk(new short[SAMPLE_RATE * ms / 1000], SAMPLE_RATE);
        }

        public void NoOp(AudioChunk audioData)
        {
            if (audioData.SampleRate != SAMPLE_RATE)
            {
                _logger.Log("Input audio was not at the expected sample rate " + SAMPLE_RATE + ", resampling...", LogLevel.Wrn);
                //audioData = audioData.ResampleTo(SAMPLE_RATE);
            }
            
            lock (this)
            {
                _inputBuffer.Write(audioData.Data);
                _msSinceLastReset += (int)audioData.Length.TotalMilliseconds;
            }
        }

        public bool Try(AudioChunk newAudioData)
        {
            NoOp(newAudioData);
            
            lock (this)
            {
                if (_engine == null)
                {
                    return false;
                }

                while (_inputBuffer.Available() > CHUNK_SIZE)
                {
                    AudioChunk nextChunk = new AudioChunk(_inputBuffer.Read(CHUNK_SIZE), SAMPLE_RATE);
                    WriteToOverdrive(nextChunk);

                    // Is the volume high enough to consider?
                    if (nextChunk.Volume() > REQUIRED_VOLUME)
                    {
                        // Send the overdrive buffer through the pipe.
                        // This will force the recognizer to process the last 1 second or so of speech, followed by 2 seconds of silence.
                        // This overrides its natural "speech hypothesis finished" logic and forces it to produce a hypothesis immediately.
                        AudioChunk entireBuffer = ReadOverdriveBuffer();
                        AudioChunk silence = GetSilence(OVERDRIVE_SILENCE_AMOUNT);
                        _rawAudioStream.WriteAudio(entireBuffer);
                        _rawAudioStream.WriteAudio(silence);
                    }
                }
            }

            Thread.Sleep(ASYNC_RENDEVOUS_TIME);

            // Check if the recognizer was triggered (asynchronously)
            if (_triggered)
            {
                _triggered = false;
                _msSinceLastReset = 0;
                return true;
            }

            return false;
        }
    }
}
