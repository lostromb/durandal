using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Utils;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Audio;
using Durandal.Common.Collections;
using Durandal.Common.Speech.SR;
using Durandal.Common.NLP.Language;
using Durandal.Common.ServiceMgmt;
using Durandal.Extensions.Vosk.Adapter;
using Durandal.Common.Utils.NativePlatform;
using Durandal.Common.Cache;
using Durandal.Common.MathExt;
using Durandal.Common.IO;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.Audio.Codecs.Opus;
using System.IO;
using Durandal.Common.Audio.Components;
using Durandal.Common.NLP;

namespace Durandal.Extensions.Vosk
{
    /// <summary>
    /// <see cref="ISpeechRecognizerFactory"/> implementation backed by Vosk recognition library.
    /// </summary>
    public class VoskSpeechRecognizerFactory : ISpeechRecognizerFactory
    {
        private const double MODEL_INMEMORY_MULTIPLIER = 1.90; // estimated ratio between on-disk and in-memory loaded model.

        private readonly int _maxRecognizersPerModel;
        private readonly ILogger _logger;
        private readonly int _inputSampleRate;
        private readonly INLPToolsCollection _nlTools;
        private readonly IDictionary<LanguageCode, Model> _loadedModels = new Dictionary<LanguageCode, Model>();
        private readonly IDictionary<Model, LockFreeCache<VoskRecognizer>> _loadedRecognizers = new Dictionary<Model, LockFreeCache<VoskRecognizer>>();
        private readonly object _lock = new object();
        private long _estimatedLoadedModelSizeBytes = 0;
        private int _initialized = 0;
        private int _disposed = 0;

        /// <summary>
        /// Creates a new speech recognizer factory backed by Vosk models. You must call <see cref="LoadLanguageModel(string, LanguageCode[])"/> to manually load each model.
        /// </summary>
        /// <param name="logger">A default logger</param>
        /// <param name="nlTools">A collection of NL tools for enriching the returned data.</param>
        /// <param name="inputSampleRate">The sample rate to set for model input. Higher rates may be more accurate at the cost of speed. Minimum 16000 recommended.</param>
        /// <param name="maxRecognizersPerModel">The maximum number of cached recognizers to instantiate for each model. Caching a recognizer will lower initial recognition latency, but use memory.</param>
        /// <param name="enableGpu">Whether to try and initialize GPU accelerated decoding.</param>
        public VoskSpeechRecognizerFactory(ILogger logger, INLPToolsCollection nlTools, int inputSampleRate, int maxRecognizersPerModel = 1, bool enableGpu = true)
        {
            _logger = logger.AssertNonNull(nameof(logger));
            _inputSampleRate = inputSampleRate.AssertNonNegative(nameof(inputSampleRate));
            _maxRecognizersPerModel = FastMath.RoundUpToPowerOf2(maxRecognizersPerModel.AssertPositive(nameof(maxRecognizersPerModel)));
            _nlTools = nlTools.AssertNonNull(nameof(nlTools));

            if (AtomicOperations.ExecuteOnce(ref _initialized))
            {
                OSAndArchitecture platform = NativePlatformUtils.GetCurrentPlatform(logger);
                NativeLibraryStatus status = NativePlatformUtils.PrepareNativeLibrary("vosk", logger);
                if (status != NativeLibraryStatus.Available)
                {
                    throw new PlatformNotSupportedException($"Could not initialize Vosk native bindings. It is possible this platform ({platform}) is not supported");
                }

                if (platform.OS == PlatformOperatingSystem.Windows)
                {
                    // have to load other dependency libraries on windows only
                    NativePlatformUtils.PrepareNativeLibrary("libgcc_s_seh-1", logger);
                    NativePlatformUtils.PrepareNativeLibrary("libstdc++-6", logger);
                    NativePlatformUtils.PrepareNativeLibrary("libwinpthread-1", logger);
                }

#if DEBUG
                VoskGlobal.SetLogLevel(0);
#else
                VoskGlobal.SetLogLevel(-1);
#endif

                if (enableGpu)
                {
                    VoskGlobal.GpuInit();
                }
            }

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~VoskSpeechRecognizerFactory()
        {
            Dispose(false);
        }
#endif

        public void LoadLanguageModel(string modelPath, params LanguageCode[] mappedLanguageCodes)
        {
            lock (_lock)
            {
                if (_disposed != 0)
                {
                    throw new ObjectDisposedException(nameof(VoskSpeechRecognizerFactory));
                }

                if (mappedLanguageCodes == null || mappedLanguageCodes.Length == 0)
                {
                    throw new ArgumentException("Vosk model must be mapped to at least one recognition locale");
                }

                // Load the model and associate it with locales
                DirectoryInfo modelDir = new DirectoryInfo(modelPath.AssertNonNullOrEmpty(nameof(modelPath)));
                if (!modelDir.Exists)
                {
                    throw new DirectoryNotFoundException($"Vosk model directory {modelDir} was not found!");
                }

                foreach (LanguageCode exactLanguageCode in mappedLanguageCodes)
                {
                    if (_loadedModels.ContainsKey(exactLanguageCode))
                    {
                        throw new ArgumentException($"A Vosk model for language code {exactLanguageCode} is already loaded!", nameof(mappedLanguageCodes));
                    }
                }

                _logger.Log($"Loading Vosk model from {modelDir.FullName} for locales {string.Join(",", mappedLanguageCodes.Select((s) => s.ToBcp47Alpha2String()))}");

                // Estimate unmanaged memory pressure based on model directory size first, so GC can handle all the allocation that's about to happen
                long thisModelSizeOnDisk = modelDir.EnumerateFiles("*", SearchOption.AllDirectories).Select((file) => file.Length).Sum();
                long modelEstimatedSizeBytes = (long)((double)thisModelSizeOnDisk * MODEL_INMEMORY_MULTIPLIER);
                _logger.Log($"Estimated in-memory model size is {(double)(modelEstimatedSizeBytes / 1024) / 1024} MB");
                GC.AddMemoryPressure(modelEstimatedSizeBytes);
                _estimatedLoadedModelSizeBytes += modelEstimatedSizeBytes;

                Model model = new Model(modelDir.FullName); 
                
                foreach (LanguageCode exactLanguageCode in mappedLanguageCodes)
                {
                    _loadedModels.Add(exactLanguageCode, model);
                }

                // Create initial recognizers for this model too
                LockFreeCache<VoskRecognizer> recoCache = new LockFreeCache<VoskRecognizer>(_maxRecognizersPerModel);
                _loadedRecognizers.Add(model, recoCache);
            }
        }

        /// <inheritdoc />
        public Task<ISpeechRecognizer> CreateRecognitionStream(
            WeakPointer<IAudioGraph> audioGraph,
            string graphNodeName,
            LanguageCode locale,
            ILogger queryLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            if (queryLogger == null)
            {
                queryLogger = _logger;
            }

            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(VoskSpeechRecognizerFactory));
            }

            lock (_lock)
            {
                Model recognitionModel;
                if (!_loadedModels.TryGetValue(locale, out recognitionModel))
                {
                    queryLogger.Log($"The locale {locale} does not correspond to a loaded Vosk model", LogLevel.Err);
                    return null;
                }

                LockFreeCache<VoskRecognizer> recoCache = _loadedRecognizers[recognitionModel];
                VoskRecognizer recognizer = recoCache.TryDequeue();
                if (recognizer == null)
                {
                    queryLogger.Log("Creating a new Vosk recognizer for this recognition stream, this may be slow!", LogLevel.Wrn);
                    recognizer = new VoskRecognizer(recognitionModel, _inputSampleRate);
                }
                else
                {
                    recognizer.Reset();
                }

                NLPTools nlTools;
                if (!_nlTools.TryGetNLPTools(locale, out nlTools))
                {
                    nlTools = null;
                }

                return Task.FromResult<ISpeechRecognizer>(
                    new VoskSpeechRecognizer(
                        audioGraph,
                        AudioSampleFormat.Mono(_inputSampleRate),
                        graphNodeName,
                        queryLogger,
                        locale,
                        recoCache,
                        recognizer,
                        nlTools));
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
                foreach (LockFreeCache<VoskRecognizer> recognizerCache in _loadedRecognizers.Values)
                {
                    VoskRecognizer reco = recognizerCache.TryDequeue();
                    while (reco != null)
                    {
                        reco.Dispose();
                        reco = recognizerCache.TryDequeue();
                    }
                }

                foreach (Model loadedModel in _loadedModels.Values)
                {
                    // could dispose of the same model multiple times; whatever, it can handle that
                    loadedModel?.Dispose();
                }

                GC.RemoveMemoryPressure(_estimatedLoadedModelSizeBytes);
            }
        }

        /// <inheritdoc />
        public bool IsLocaleSupported(LanguageCode locale)
        {
            if (locale == null)
            {
                return false;
            }

            lock (_lock)
            {
                return _loadedModels.ContainsKey(locale);
            }
        }
    }
}
