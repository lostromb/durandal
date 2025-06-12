using Durandal.Common.Audio;
using Durandal.Common.Logger;
using Durandal.Common.Speech.Triggers;
using Durandal.Common.MathExt;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Utils;
using Durandal.Common.Tasks;
using Durandal.Common.Events;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Speech.Triggers.Sphinx
{
    public class SphinxAudioTrigger : AbstractAudioSampleTarget, IAudioTrigger
    {
        private readonly IPocketSphinx _backend;
        private readonly string _acousticModelDir;
        private readonly string _dictionaryFile;
        private readonly bool _verbose;
        private readonly KeywordSpottingConfiguration _defaultConfig;
        private readonly ILogger _logger;
        private readonly object _mutex = new object();
        private int _disposed = 0;

        /// <summary>
        /// Tracks speech detection rate over the last 2 seconds
        /// </summary>
        private readonly MovingAverage _avgSpeechPercentage;

        /// <summary>
        /// Ms elapsed since the last time we measured instantaneous speech detection
        /// </summary>
        private int _avgSpeechMsConsumed;

        /// <summary>
        /// number of milliseconds in between measurements of the average speech percentage
        /// </summary>
        private const int AVG_SPEECH_MS_PER_SAMPLE = 20;
        private const double CHATTER_THRESHOLD_HIGH = 0.67;

        private bool _healthy;
        private KeywordSpottingConfiguration _currentConfig;

        public AsyncEvent<AudioTriggerEventArgs> TriggeredEvent { get; private set; }

        /// <summary>
        /// Constructs a new keyword trigger backed by Pocketsphinx
        /// </summary>
        /// <param name="audioGraph">The audio graph this trigger will participate in</param>
        /// <param name="inputFormat">The audio format that Sphinx expects. Typically this will be mono-16khz or mono-8khz</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        /// <param name="libraryImpl">The implementation of Pocketsphinx to use</param>
        /// <param name="logger">A logger</param>
        /// <param name="acousticModelDir">The path name to the acoustic model directory</param>
        /// <param name="dictionaryFile">The path name to the dictionary file</param>
        /// <param name="defaultConfig">The default keyword spotting config to use</param>
        /// <param name="verboseLogging">If true, enable verbose logging</param>
        public SphinxAudioTrigger(
            WeakPointer<IAudioGraph> audioGraph,
            AudioSampleFormat inputFormat,
            string nodeCustomName,
            IPocketSphinx libraryImpl,
            ILogger logger,
            string acousticModelDir,
            string dictionaryFile,
            KeywordSpottingConfiguration defaultConfig,
            bool verboseLogging) : base(audioGraph, nameof(SphinxAudioTrigger), nodeCustomName)
        {
            _acousticModelDir = acousticModelDir;
            _dictionaryFile = dictionaryFile;
            _verbose = verboseLogging;
            _defaultConfig = defaultConfig;
            _currentConfig = defaultConfig;
            _logger = logger;
            _healthy = false;
            _backend = libraryImpl;
            _avgSpeechPercentage = new MovingAverage(100, 0);
            InputFormat = inputFormat;
            TriggeredEvent = new AsyncEvent<AudioTriggerEventArgs>();
        }

        // Finalizer is present in base class
        //~SphinxAudioTrigger()
        //{
        //    Dispose(false);
        //}

        public void Initialize()
        {
            lock (_mutex)
            {
                _healthy = _backend.Create(
                    _acousticModelDir,
                    _dictionaryFile,
                    _verbose);

                if (_healthy)
                {
                    _logger.Log("Initializing trigger: primary kw " + _currentConfig.PrimaryKeyword, LogLevel.Std);
                    if (_currentConfig.SecondaryKeywords != null)
                    {
                        foreach (string secondaryKw in _currentConfig.SecondaryKeywords)
                        {
                            _logger.Log("Secondary kw " + secondaryKw, LogLevel.Std);
                        }
                    }

                    _healthy = _backend.Reconfigure(_currentConfig);
                    if (!_healthy)
                    {
                        _logger.Log("FAILED to initialize SphinxTrigger with default keyword config", LogLevel.Err);
                    }
                }
                else
                {
                    _logger.Log("FAILED to create SphinxTrigger", LogLevel.Err);
                }

                if (_healthy)
                {
                    _healthy = _backend.Start();
                    if (_healthy)
                    {
                        _logger.Log("Successfully initialized sphinx trigger");
                    }
                }

                if (!_healthy)
                {
                    _logger.Log("Something bad happened while initializing the sphinx trigger", LogLevel.Err);
                }
            }
        }

        public void Configure(KeywordSpottingConfiguration config)
        {
            lock (_mutex)
            {
                if (config == null)
                {
                    config = _defaultConfig;
                }

                _currentConfig = config;
                _logger.Log("Reconfiguring trigger: primary kw " + _currentConfig.PrimaryKeyword, LogLevel.Std);
                if (_currentConfig.SecondaryKeywords != null)
                {
                    foreach (string secondaryKw in _currentConfig.SecondaryKeywords)
                    {
                        _logger.Log("Secondary kw " + secondaryKw, LogLevel.Std);
                    }
                }

                if (_healthy)
                {
                    _backend.Stop();
                }

                if (!_backend.Reconfigure(_currentConfig))
                {
                    _logger.Log("Reconfiguring trigger failed. The key phrases may contain words not in the dictionary. Falling back to default config...", LogLevel.Wrn);
                    _currentConfig = _defaultConfig;
                    _healthy = _backend.Reconfigure(_currentConfig);
                }

                if (_healthy)
                {
                    _healthy = _backend.Start();
                }
            }
        }

        public void Reset()
        {
            if (_healthy)
            {
                lock (_mutex)
                {
                    _logger.Log("Reset trigger");
                    _backend.Stop();
                    _backend.Start();
                }
            }
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
                    lock (_mutex)
                    {
                        _logger.Log("Disposing of native resources...");
                        if (_healthy)
                        {
                            _backend.Stop();
                        }

                        _backend.Dispose();
                    }
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        protected override ValueTask WriteAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            AudioTriggerResult returnVal = new AudioTriggerResult()
            {
                Triggered = false,
                WasPrimaryKeyword = false,
                TriggeredKeyword = string.Empty
            };

            //if (noOp)
            //{
            //    return returnVal;
            //}

            lock (_mutex)
            {
                if (_healthy)
                {
                    short[] buf = new short[numSamplesPerChannel * InputFormat.NumChannels];
                    AudioMath.ConvertSamples_FloatToInt16(buffer, bufferOffset, buf, 0, buf.Length);
                    string lastTrigger = _backend.ProcessForKws(buf, buf.Length);

                    // Filter triggers out if recent speech detection is high
                    if (!string.IsNullOrEmpty(lastTrigger)/* && _avgSpeechPercentage.Average < CHATTER_THRESHOLD_HIGH*/)
                    {
                        returnVal.Triggered = true;
                        returnVal.TriggeredKeyword = lastTrigger;
                        if (lastTrigger.Equals(_currentConfig.PrimaryKeyword, StringComparison.OrdinalIgnoreCase))
                        {
                            returnVal.WasPrimaryKeyword = true;
                        }

                        _logger.Log("Audio trigger: " + returnVal.TriggeredKeyword + (returnVal.WasPrimaryKeyword ? " (Primary)" : ""));
                    }

                    bool wasSpeech = _backend.IsSpeechDetected();
                    _avgSpeechMsConsumed += (int)AudioMath.ConvertSamplesPerChannelToTimeSpan(InputFormat.SampleRateHz, numSamplesPerChannel).TotalMilliseconds;
                    while (_avgSpeechMsConsumed >= AVG_SPEECH_MS_PER_SAMPLE)
                    {
                        _avgSpeechPercentage.Add(wasSpeech ? 1 : 0);
                        _avgSpeechMsConsumed -= AVG_SPEECH_MS_PER_SAMPLE;
                    }
                }
            }

            if (returnVal.Triggered)
            {
                TriggeredEvent.FireInBackground(this, new AudioTriggerEventArgs(returnVal, realTime), _logger, realTime);
            }

            return new ValueTask();
        }
    }
}
