

namespace Durandal.Common.Dialog.Web
{
    using Durandal.API;
    using Durandal.Common.Audio;
    using Durandal.Common.Audio.Codecs;
    using Durandal.Common.Audio.Components;
    using Durandal.Common.Cache;
    using Durandal.Common.Collections;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.File;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.IO;
    using Durandal.Common.Logger;
    using Durandal.Common.LU;
    using Durandal.Common.MathExt;
    using Durandal.Common.Net;
    using Durandal.Common.NLP;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.Ontology;
    using Durandal.Common.Security;
    using Durandal.Common.Security.Server;
    using Durandal.Common.ServiceMgmt;
    using Durandal.Common.Speech.SR;
    using Durandal.Common.Speech.TTS;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using Durandal.Common.Time.TimeZone;
    using Durandal.Common.Utils;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Runtime.ExceptionServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public class DialogWebService : IDisposable
    {
        /// <summary>
        /// Specifies the amount (in percent from 0 to 100) that the current CPU load metric needs to exceed in
        /// order for monitoring traffic to be ignored as a way of shedding system load.
        /// FIXME make this configurable
        /// </summary>
        private const double CPU_OVERLOAD_THRESHOLD = 80.0;

        private WeakPointer<DialogProcessingEngine> _dialogEngine;

        private DialogWebConfiguration _serverConfig;
        private ILogger _logger;
        private IFileSystem _fileSystem;
        private TimeZoneResolver _timeZoneResolver;

        private readonly ReaderWriterLockAsync _asynchronousLoadMutex = new ReaderWriterLockAsync(64);

        private readonly DialogHttpServer _webServer;
        private readonly ServerAuthenticator _authenticator;
        private readonly IAudioCodecFactory _codecFactory;
        private readonly ISpeechSynth _speechSynthesizer;
        private readonly ISpeechRecognizerFactory _speechRecognitionEngine;
        private readonly IConversationStateCache _conversationStateCache;
        private readonly WeakPointer<ICache<DialogAction>> _cachedDialogActions;
        private readonly WeakPointer<ICache<CachedWebData>> _cachedWebData;
        private readonly WeakPointer<ICache<ClientContext>> _cachedClientContext;
        private readonly IStreamingAudioCache _streamingAudioCache;
        private readonly ILUClient _luInterface;
        private readonly IAudioCodecFactory _pcmCodec;
        private readonly WeakPointer<IMetricCollector> _metrics;
        private readonly DimensionSet _dimensions;
        private readonly AudioProcessingQuality _audioQuality;
        private int _disposed = 0;

        private DialogWebService(DialogWebParameters parameters)
        {
            _logger = parameters.Logger;
            _serverConfig = parameters.ServerConfig;
            _fileSystem = parameters.FileSystem;
            _conversationStateCache = parameters.ConversationStateCache;
            _cachedDialogActions = parameters.DialogActionStore;
            _cachedWebData = parameters.WebDataCache;
            _cachedClientContext = parameters.ClientContextCache;
            _streamingAudioCache = parameters.StreamingAudioCache;
            _dialogEngine = parameters.CoreEngine;
            _speechRecognitionEngine = parameters.SpeechReco;
            _speechSynthesizer = parameters.SpeechSynth;
            _codecFactory = parameters.CodecFactory;
            _metrics = parameters.Metrics.DefaultIfNull(() => NullMetricCollector.Singleton);
            _dimensions = parameters.MetricDimensions ?? DimensionSet.Empty;
            _audioQuality = AudioHelpers.GetAudioQualityBasedOnMachinePerformance();

            _luInterface = parameters.LuConnection;
            _pcmCodec = new RawPcmCodecFactory();

            if (parameters.HttpServer != null)
            {
                WeakPointer<IThreadPool> httpThreadPool = parameters.ProcessingThreadPool
                    .DefaultIfNull(() => new TaskThreadPool(_metrics, _dimensions, "DialogWeb"));

                _webServer = new DialogHttpServer(
                    this,
                    httpThreadPool,
                    parameters.HttpServer,
                    _logger.Clone("DialogHttpServer"),
                    _fileSystem,
                    _cachedWebData,
                    _cachedClientContext,
                    parameters.StreamingAudioCache,
                    parameters.TransportProtocols,
                   _metrics.Value,
                   parameters.MetricDimensions,
                   parameters.MachineHostName);
            }

            _authenticator = new ServerAuthenticator(
                _logger.Clone("ServerAuthentication"),
                parameters.PublicKeyStorage,
                new StandardRSADelegates());

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~DialogWebService()
        {
            Dispose(false);
        }
#endif

        public static async Task<DialogWebService> Create(DialogWebParameters parameters, CancellationToken cancelToken)
        {
            DialogWebService returnVal = new DialogWebService(parameters);
            await returnVal.Initialize(parameters, cancelToken).ConfigureAwait(false);
            return returnVal;
        }

        private async Task Initialize(DialogWebParameters parameters, CancellationToken cancelToken)
        {
            _logger.Log("Initializing dialog server");
            
            // Initialize global helpers and such

            if (_luInterface == null)
            {
                _logger.Log("Dialog is initializing without an LU interface! Only programmatic dialog actions will be possible", LogLevel.Wrn);
            }
            else
            {
                _logger.Log("Testing: is LU available?");
                IDictionary<string, string> luStatus = await _luInterface.GetStatus(_logger, cancelToken, parameters.RealTimeProvider).ConfigureAwait(false);
                if (luStatus == null)
                {
                    _logger.Log("LU did not respond to ping", LogLevel.Wrn);
                }
                else
                {
                    _logger.Log("LU reports the following status:");
                    foreach (KeyValuePair<string, string> kvp in luStatus)
                    {
                        _logger.Log("LU: " + kvp.Key + " = " + kvp.Value);
                        if (kvp.Key.Equals("ProtocolVersion"))
                        {
                            string expectedProtocolVersion = new LURequest().ProtocolVersion.ToString();
                            if (!kvp.Value.Equals(expectedProtocolVersion))
                            {
                                _logger.Log("The LU service reports a different protocol version (" + kvp.Value + ") than expected (" + expectedProtocolVersion + "). Queries will probably not work", LogLevel.Err);
                            }
                        }
                    }
                }
            }

            if (_dialogEngine.Value != null)
            {
                _dialogEngine.Value.PluginRegistered += PluginRegistered;
            }

            if (_serverConfig.SupportedAudioCodecs.Count > 0)
            {
                foreach (string desiredCodec in _serverConfig.SupportedAudioCodecs)
                {
                    if (_codecFactory.CanEncode(desiredCodec))
                    {
                        _logger.Log("Registered audio codec \"" + desiredCodec + "\"");
                    }
                    else
                    {
                        _logger.Log("Audio codec \"" + desiredCodec + "\" specified in configuration, but no codec implementation was found!", LogLevel.Wrn);
                    }
                }
            }
            else
            {
                _logger.Log("Dialog configuration does not specify any supportedAudioCodecs; only uncompressed pcm will be enabled", LogLevel.Wrn);
            }

            // Load timezone database for normalizing time info sent by clients
            VirtualPath ianaDirectory = new VirtualPath(RuntimeDirectoryName.MISCDATA_DIR).Combine("IANA");
            if (_fileSystem.Exists(ianaDirectory))
            {
                _timeZoneResolver = new TimeZoneResolver(_logger.Clone("TimeZoneResolver"));
                if (!(await _timeZoneResolver.Initialize(_fileSystem, ianaDirectory).ConfigureAwait(false)))
                {
                    _logger.Log("Failed to initialize time zone resolver. This probably means the files in /data/IANA are not correct.", LogLevel.Err);
                    _timeZoneResolver = null;
                }
            }
            else
            {
                _logger.Log("Cannot initialize time zone resolver: Required data files in /data/IANA are not present.", LogLevel.Wrn);
            }

            if (_webServer != null)
            {
                // And start the frontend server
                await _webServer.StartServer("Dialog Service/Presentation HTTP endpoint", cancelToken, parameters.RealTimeProvider).ConfigureAwait(false);
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

            if (_webServer != null)
            {
                _webServer.StopServer(CancellationToken.None, DefaultRealTimeProvider.Singleton);
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                _webServer?.Dispose();
                _speechSynthesizer?.Dispose();
                _speechRecognitionEngine?.Dispose();
                _asynchronousLoadMutex?.Dispose();
            }

            OnEngineStopped();
        }

        public bool IsStopped
        {
            get
            {
                return _disposed != 0;
            }
        }

        public IDictionary<string, string> GetStatus()
        {
            IDictionary<string, string> returnVal = new Dictionary<string, string>();
            returnVal["Version"] = SVNVersionInfo.VersionString;
            DialogRequest protoVer = new DialogRequest();
            returnVal["ProtocolVersion"] = protoVer.ProtocolVersion.ToString();
            return returnVal;
        }

        public async Task<IAudioDataSource> FetchStreamingAudio(
            string cacheKey,
            ILogger traceLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            RetrieveResult<IAudioDataSource> rr = await _streamingAudioCache.TryGetAudioReadStream(cacheKey, traceLogger, cancelToken, realTime, TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            if (rr.Success)
            {
                return rr.Result;
            }

            return null;
        }

        public Task<CachedWebData> FetchPluginViewData(string pluginId, string path, DateTimeOffset? ifModifiedSince, ILogger traceLogger, IRealTimeProvider realTime)
        {
            return _dialogEngine.Value.FetchPluginViewData(pluginId, path, ifModifiedSince, traceLogger, realTime);
        }

        /// <summary>
        /// Returns true if the incoming request has the Monitoring query flag and the current metrics report that machine CPU usage is above 80%
        /// </summary>
        /// <param name="clientInput"></param>
        /// <returns></returns>
        private bool ShouldDeprioritizeMonitoringTraffic(DialogRequest clientInput)
        {
            if (clientInput.RequestFlags.HasFlag(QueryFlags.Monitoring))
            {
                // Try and access the counter for the current machine CPU load
                double? currentMachineCpu = _metrics.Value.GetCurrentMetric(CommonInstrumentation.Key_Counter_MachineCpuUsage);
                return currentMachineCpu.GetValueOrDefault(0) > CPU_OVERLOAD_THRESHOLD;
            }
            else
            {
                return false;
            }
        }

        private ILogger CreateTraceLogger(DialogRequest clientInput)
        {
            ILogger queryLogger;
            Guid traceIdGuid;
            if (string.IsNullOrEmpty(clientInput.TraceId))
            {
                // Add a traceId if the client did not
                traceIdGuid = Guid.NewGuid();
                clientInput.TraceId = CommonInstrumentation.FormatTraceId(traceIdGuid);
                _logger.Log("Generating a new traceid for this query as one was not provided", LogLevel.Std, traceIdGuid);
            }
            else
            {
                if (Guid.TryParse(clientInput.TraceId, out traceIdGuid))
                {
                    string oldTraceId = clientInput.TraceId;
                    // Normalize the guid to make tracing consistent
                    clientInput.TraceId = CommonInstrumentation.FormatTraceId(traceIdGuid);
                    if (!string.Equals(oldTraceId, clientInput.TraceId))
                    {
                        _logger.Log("The input traceID does not conform to the standard format, which is 40-char lowercase hexadecimal with no dashes", LogLevel.Wrn, traceIdGuid);
                    }
                }
                else
                {
                    traceIdGuid = Guid.NewGuid();
                    clientInput.TraceId = CommonInstrumentation.FormatTraceId(traceIdGuid);
                    _logger.Log("Generating a new traceid for this query as the one provided (\"" + clientInput.TraceId + "\") could not be parsed", LogLevel.Wrn, traceIdGuid);
                }
            }

            // Create the master query logger for this query based on query flags that we receive.
            // First, handle special cases for monitoring
            if (clientInput.RequestFlags.HasFlag(QueryFlags.LogNothing))
            {
                if (clientInput.RequestFlags.HasFlag(QueryFlags.Trace))
                {
                    // For the special case of log nothing + tracing, create an eventonly logger that will simply buffer the generated messages internally without emitting them anywhere
                    queryLogger = new EventOnlyLogger(_logger.ComponentName,
                            validLogLevels: LogLevel.All,
                            maxLogLevels: LogLevel.All,
                            maxPrivacyClasses: DataPrivacyClassification.All,
                            defaultPrivacyClass: DataPrivacyClassification.SystemMetadata,
                            backgroundLogThreadPool: LoggerBase.DEFAULT_BACKGROUND_LOGGING_THREAD_POOL)
                                .CreateTraceLogger(traceIdGuid);
                }
                else
                {
                    queryLogger = NullLogger.Singleton;
                }
            }
            else
            {
                queryLogger = _logger.CreateTraceLogger(traceIdGuid);
            }

            // Then augment the logger's valid log & PII levels
            if (clientInput.RequestFlags.HasFlag(QueryFlags.Debug))
            {
                queryLogger = queryLogger.Clone(allowedLogLevels: LogLevel.All);
                queryLogger.Log("This event is flagged as debug; all verbose logs will be written", LogLevel.Vrb);
            }
            else
            {
                queryLogger = queryLogger.Clone(allowedLogLevels: LogLevel.Err | LogLevel.Wrn | LogLevel.Std | LogLevel.Ins);
            }

            if (clientInput.RequestFlags.HasFlag(QueryFlags.NoPII))
            {
                queryLogger = queryLogger.Clone(allowedPrivacyClasses: DataPrivacyClassification.SystemMetadata | DataPrivacyClassification.PublicNonPersonalData);
                queryLogger.Log("Incoming request has NoPII flag set - altering logger to forbid logging any personal identifiers");
            }

            return queryLogger;
        }

        private async Task<bool> ValidateClientInput(DialogRequest clientInput, ILogger queryLogger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            // Verify that the protocol version is supported
            int expectedProtocolVersion = new DialogRequest().ProtocolVersion;
            if (clientInput.ProtocolVersion != expectedProtocolVersion)
            {
                queryLogger.Log("Client is using an outdated or unsupported protocol version \"" + clientInput.ProtocolVersion + "\"", LogLevel.Wrn);
            }

            if (clientInput.ClientContext == null)
            {
                queryLogger.Log("Input client context is null! This should never happen!", LogLevel.Err);
                return false;
            }

            // Determine the client's locale
            if (clientInput.ClientContext.Locale == null)
            {
                queryLogger.Log("Client did not specify a locale in the client context!", LogLevel.Err);
                return false;
            }

            // Ensure that clientid and userid are both present and valid
            if (string.IsNullOrWhiteSpace(clientInput.ClientContext.ClientId))
            {
                queryLogger.Log("Client did not specify a client ID in the client context!", LogLevel.Err);
                return false;
            }

            if (clientInput.ClientContext.ClientId.Length > DialogConstants.MAX_CLIENT_ID_LENGTH)
            {
                queryLogger.Log("The given client ID \"" + clientInput.ClientContext.ClientId + "\" is longer than " + DialogConstants.MAX_CLIENT_ID_LENGTH +
                    " chars and will be hashed", LogLevel.Wrn, privacyClass: DataPrivacyClassification.EndUserPseudonymousIdentifiers);
                clientInput.ClientContext.ClientId = StringUtils.HashToGuid(clientInput.ClientContext.ClientId).ToString("N");
                queryLogger.Log("The hashed client ID is \"" + clientInput.ClientContext.ClientId + "\"", LogLevel.Wrn);
            }

            if (string.IsNullOrWhiteSpace(clientInput.ClientContext.UserId))
            {
                queryLogger.Log("Client did not specify a user ID in the client context!", LogLevel.Err);
                return false;
            }

            if (clientInput.ClientContext.UserId.Length > DialogConstants.MAX_USER_ID_LENGTH)
            {
                queryLogger.Log("The given user ID \"" + clientInput.ClientContext.UserId + "\" is longer than " + DialogConstants.MAX_USER_ID_LENGTH +
                    " chars and will be hashed", LogLevel.Wrn, privacyClass: DataPrivacyClassification.EndUserPseudonymousIdentifiers);
                clientInput.ClientContext.UserId = StringUtils.HashToGuid(clientInput.ClientContext.UserId).ToString("N");
                queryLogger.Log("The hashed user ID is \"" + clientInput.ClientContext.UserId + "\"", LogLevel.Wrn);
            }

            // Validate input time
            if (!string.IsNullOrEmpty(clientInput.ClientContext.ReferenceDateTime))
            {
                DateTimeOffset tempTime;
                if (DateTimeOffset.TryParseExact(
                    clientInput.ClientContext.ReferenceDateTime,
                    "yyyy-MM-ddTHH:mm:ss",
                    CultureInfo.InvariantCulture.DateTimeFormat,
                    DateTimeStyles.AssumeLocal,
                    out tempTime))
                {
                    // And if UTCOffset is unspecified, attempt to estimate it to the nearest 15 minutes
                    if (!clientInput.ClientContext.UTCOffset.HasValue)
                    {
                        double differenceFromUtc = (tempTime - realTime.Time).TotalMinutes;
                        clientInput.ClientContext.UTCOffset = (int)Math.Round(differenceFromUtc / 15) * 15;
                    }
                }
                else
                {
                    // If the time field is in the wrong format, ignore it
                    queryLogger.Log("The input time \"" + clientInput.ClientContext.ReferenceDateTime + "\" can't be parsed and will be ignored", LogLevel.Wrn);
                    clientInput.ClientContext.ReferenceDateTime = string.Empty;
                }
            }
            else if (clientInput.ClientContext.UTCOffset.HasValue)
            {
                // UTC offset is present but no reference time. Calculate reference time as an offset of current UTC
                clientInput.ClientContext.ReferenceDateTime = realTime.Time.AddMinutes(clientInput.ClientContext.UTCOffset.Value).ToString("yyyy-MM-ddTHH:mm:ss");
                queryLogger.Log("Exact client time was not specified; estimating it in terms of UTC: " + clientInput.ClientContext.ReferenceDateTime, LogLevel.Vrb);
            }

            if (_timeZoneResolver != null)
            {
                GeoCoordinate? userCoord = null;
                if (clientInput.ClientContext.Latitude.HasValue)
                {
                    userCoord = new GeoCoordinate(clientInput.ClientContext.Latitude.Value, clientInput.ClientContext.Longitude.Value);
                }

                int? utcOffset = clientInput.ClientContext.UTCOffset;
                string userTimeZone = clientInput.ClientContext.UserTimeZone;
                DateTimeOffset? localTime = null;

                _timeZoneResolver.PopulateMissingTimeInformation(
                    queryLogger.Clone("TimeZoneResolver"),
                    realTime.Time,
                    ref localTime,
                    ref userTimeZone,
                    ref utcOffset,
                    userCoord);

                if (!clientInput.ClientContext.UTCOffset.HasValue && utcOffset.HasValue)
                {
                    queryLogger.LogFormat(LogLevel.Std, DataPrivacyClassification.PublicNonPersonalData, "Inferring user UTC offset of {0}", utcOffset);
                    clientInput.ClientContext.UTCOffset = utcOffset;
                }

                if (string.IsNullOrEmpty(clientInput.ClientContext.UserTimeZone) &&
                    !string.IsNullOrEmpty(userTimeZone))
                {
                    queryLogger.LogFormat(LogLevel.Std, DataPrivacyClassification.PublicNonPersonalData, "Inferring user time zone of {0}", userTimeZone);
                    clientInput.ClientContext.UserTimeZone = userTimeZone;
                }

                if (string.IsNullOrEmpty(clientInput.ClientContext.ReferenceDateTime) &&
                    localTime.HasValue)
                {
                    string localTimeString = localTime.Value.ToString("yyyy-MM-ddTHH:mm:ss");
                    queryLogger.LogFormat(LogLevel.Std, DataPrivacyClassification.PublicNonPersonalData, "Inferring user local time of {0}", localTimeString);
                    clientInput.ClientContext.ReferenceDateTime = localTimeString;
                }
            }

            // Validate barge-in time
            if (clientInput.ClientAudioPlaybackTimeMs.HasValue && clientInput.ClientAudioPlaybackTimeMs.Value < 0)
            {
                queryLogger.Log("The client claims that it played a negative amount of audio before this input; ignoring that value", LogLevel.Wrn);
                clientInput.ClientAudioPlaybackTimeMs = null;
            }

            // Ensure that text queries actually have a query present
            if (clientInput.InteractionType == InputMethod.Typed &&
                string.IsNullOrWhiteSpace(clientInput.TextInput) &&
                (clientInput.LanguageUnderstanding == null || clientInput.LanguageUnderstanding.Count == 0))
            {
                queryLogger.Log("Request specifies that it is text input, but there are no text queries present", LogLevel.Err);
                return false;
            }

            bool hasAudio = clientInput.AudioInput != null && clientInput.AudioInput.Data != null && clientInput.AudioInput.Data.Array != null;

            if (hasAudio)
            {
                queryLogger.Log("Audio from client is encoded with \"" + clientInput.AudioInput.Codec + "\"", LogLevel.Std);
                if (_codecFactory.CanDecode(clientInput.AudioInput.Codec))
                {
                    clientInput.AudioInput = await TranscodeAudioToPcm(clientInput.AudioInput, queryLogger, cancelToken, realTime).ConfigureAwait(false);
                }
                else
                {
                    queryLogger.Log("Client sent compressed audio which uses an unsupported codec \"" + clientInput.AudioInput.Codec + "\"", LogLevel.Err);
                    return false;
                }
            }

            // Perform speech recognition if the client did not
            if (clientInput.InteractionType == InputMethod.Spoken &&
                (clientInput.SpeechInput == null || clientInput.SpeechInput.RecognizedPhrases == null || clientInput.SpeechInput.RecognizedPhrases.Count == 0))
            {
                // If client sent no audio and no queries, assume it is a noreco turn
                if (clientInput.AudioInput == null || clientInput.AudioInput.Data == null || clientInput.AudioInput.Data.Count == 0)
                {
                    queryLogger.Log("Speech input with no query text provided and no audio data present, tagging as noreco...", LogLevel.Err);
                    return false;
                }

                queryLogger.Log("Client did not provide speech reco results; running on server side...");

                // Obtain a speech reco engine and run SR
                using (NonRealTimeCancellationTokenSource srCancelizer = new NonRealTimeCancellationTokenSource(realTime, TimeSpan.FromSeconds(2)))
                {
                    clientInput.SpeechInput = await RunSpeechRecognition(clientInput.AudioInput, clientInput.ClientContext.Locale, queryLogger, srCancelizer.Token, realTime).ConfigureAwait(false);
                }

                // Check that speech reco worked
                if (clientInput.SpeechInput.RecognitionStatus != SpeechRecognitionStatus.Success)
                {
                    queryLogger.Log("Speech recognition returned non-success status code " + clientInput.SpeechInput.RecognitionStatus.ToString(), LogLevel.Err);
                    return false;
                }

                if (clientInput.SpeechInput.RecognizedPhrases.Count == 0)
                {
                    queryLogger.Log("Speech recognition returned empty result", LogLevel.Err);
                    return false;
                }
            }

            return true;
        }

        private async Task<ClientAuthenticationLevel> GetClientAuthentication(DialogRequest request, ILogger queryLogger, IRealTimeProvider realTime)
        {
            ClientAuthenticationLevel returnVal = ClientAuthenticationLevel.None;
            
            if (request.AuthTokens == null)
            {
                return returnVal;
            }

            queryLogger.Log("Client sent " + request.AuthTokens.Count + " auth token(s)");
            foreach (var token in request.AuthTokens)
            {
                queryLogger.Log("Validating token with scope " + Enum.GetName(typeof(ClientAuthenticationScope), token.Scope));
                AuthLevel internalAuthLevel = AuthLevel.Unknown;
                try
                {
                    RequestToken convertedToken = new RequestToken(token.Red, token.Blue);
                    ClientKeyIdentifier keyId = new ClientKeyIdentifier(token.Scope, request.ClientContext.UserId, request.ClientContext.ClientId);
                    internalAuthLevel = await _authenticator.VerifyRequestToken(keyId, convertedToken, queryLogger, realTime).ConfigureAwait(false);
                }
                catch (ArithmeticException)
                {
                    queryLogger.Log("Could not parse auth token; ignoring...", LogLevel.Wrn);
                    queryLogger.Log("Red=" + token.Red + " Blue=" + token.Blue, LogLevel.Wrn);
                }

                queryLogger.Log("Token validation level: " + Enum.GetName(typeof(AuthLevel), internalAuthLevel));

                // TODO: If the client passes a bad token, don't honor the request at all
                switch (internalAuthLevel)
                {
                    case AuthLevel.Unknown:
                        if (token.Scope.HasFlag(ClientAuthenticationScope.User))
                            returnVal |= ClientAuthenticationLevel.UserUnknown;
                        if (token.Scope.HasFlag(ClientAuthenticationScope.Client))
                            returnVal |= ClientAuthenticationLevel.ClientUnknown;
                        break;
                    case AuthLevel.Unauthorized:
                    case AuthLevel.RequestExpired:
                        if (token.Scope.HasFlag(ClientAuthenticationScope.User))
                            returnVal |= ClientAuthenticationLevel.UserUnauthorized;
                        if (token.Scope.HasFlag(ClientAuthenticationScope.Client))
                            returnVal |= ClientAuthenticationLevel.ClientUnauthorized;
                        break;
                    case AuthLevel.Unverified:
                        if (token.Scope.HasFlag(ClientAuthenticationScope.User))
                            returnVal |= ClientAuthenticationLevel.UserUnverified;
                        if (token.Scope.HasFlag(ClientAuthenticationScope.Client))
                            returnVal |= ClientAuthenticationLevel.ClientUnverified;
                        break;
                    case AuthLevel.Authorized:
                        if (token.Scope.HasFlag(ClientAuthenticationScope.User))
                            returnVal |= ClientAuthenticationLevel.UserAuthorized;
                        if (token.Scope.HasFlag(ClientAuthenticationScope.Client))
                            returnVal |= ClientAuthenticationLevel.ClientAuthorized;
                        break;
                    default:
                        if (token.Scope.HasFlag(ClientAuthenticationScope.User))
                            returnVal |= ClientAuthenticationLevel.UserUnknown;
                        if (token.Scope.HasFlag(ClientAuthenticationScope.Client))
                            returnVal |= ClientAuthenticationLevel.ClientUnknown;
                        break;
                }
            }

            return returnVal;
        }
        
        private static void LogClientRequest(DialogRequest clientInput, ILogger logger)
        {
            logger.DispatchAsync(
                (delegateLogger, timestamp) =>
                {
                    JObject clientRequestToLog = CommonInstrumentation.ToJObject(clientInput);
                    // There's no point in logging the audio data so we trim it from the object here.
                    CommonInstrumentation.NullifyField(clientRequestToLog, "$.AudioInput.Data");

                    IDictionary<DataPrivacyClassification, JToken> impressionsDividedByPrivacyClass =
                        CommonInstrumentation.SplitObjectByPrivacyClass(
                            CommonInstrumentation.PrependPath(clientRequestToLog, "$.Dialog.ClientRequest"),
                            delegateLogger.DefaultPrivacyClass,
                            CommonInstrumentation.GetPrivacyMappingsDialogRequest(),
                            delegateLogger);

                    using (PooledStringBuilder builder = StringBuilderPool.Rent())
                    {
                        foreach (var classifiedLogMsg in impressionsDividedByPrivacyClass)
                        {
                            delegateLogger.Log(
                                CommonInstrumentation.FromJObject(classifiedLogMsg.Value, builder.Builder),
                                LogLevel.Ins,
                                delegateLogger.TraceId,
                                classifiedLogMsg.Key,
                                timestamp);
                            builder.Builder.Length = 0;
                        }
                    }
                 });
                
            if (clientInput.ClientContext != null)
            {
                logger.LogFormat(LogLevel.Std, DataPrivacyClassification.EndUserPseudonymousIdentifiers, "ClientID is {0} / UserID is {1}", clientInput.ClientContext.ClientId, clientInput.ClientContext.UserId);
                logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "Client flags are {0}", clientInput.ClientContext.GetCapabilities());
                logger.LogFormat(LogLevel.Std, DataPrivacyClassification.PublicNonPersonalData, "Client locale is {0}", clientInput.ClientContext.Locale);
                if (clientInput.ClientContext.ExtraClientContext.Count > 0)
                {
                    logger.Log("Client context data follows:", LogLevel.Vrb, privacyClass: DataPrivacyClassification.EndUserPseudonymousIdentifiers);
                    foreach (var x in clientInput.ClientContext.ExtraClientContext)
                    {
                        logger.LogFormat(LogLevel.Vrb, privacyClass: DataPrivacyClassification.EndUserPseudonymousIdentifiers, "\"{0}\" = \"{1}\"", x.Key, x.Value);
                    }
                }
                else
                {
                    logger.Log("No other context data was given.", LogLevel.Vrb);
                }
            }
            if (clientInput.DomainScope != null && clientInput.DomainScope.Count > 0)
            {
                logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "Client is scoped to these domains: {{{0}}}", string.Join(",", clientInput.DomainScope));
            }
        }

        /// <summary>
        /// The main entry point function to the entire core workflow. Accepts a client
        /// request and returns a server response, after running language models
        /// and potentially executing stateful dialog actions.
        /// </summary>
        /// <param name="clientInput"></param>
        /// <param name="cancelToken"></param>
        /// <param name="realTime"></param>
        /// <returns></returns>
        public async Task<DialogWebServiceResponse> ProcessRegularQuery(DialogRequest clientInput, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            ILogger queryLogger;

            if (ShouldDeprioritizeMonitoringTraffic(clientInput))
            {
                _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_QueriesDeprioritized, _dimensions);
                return new DialogWebServiceResponse(
                    new DialogResponse()
                    {
                        ExecutionResult = Result.Failure,
                        ErrorMessage = "Server is overloaded",
                    });
            }

            bool skipLU = (clientInput.LanguageUnderstanding != null &&
                          clientInput.LanguageUnderstanding.Count > 0);
            queryLogger = CreateTraceLogger(clientInput);
            bool clientInputValid = await ValidateClientInput(clientInput, queryLogger, cancelToken, realTime).ConfigureAwait(false);
            bool successfulRecognition = clientInputValid || skipLU;

            LogClientRequest(clientInput, queryLogger);

            ClientAuthenticationLevel clientAuthLevel = ClientAuthenticationLevel.None;
            if (clientInput.AuthTokens != null)
            {
                clientAuthLevel = await GetClientAuthentication(clientInput, queryLogger, realTime).ConfigureAwait(false);
            }

            if (!skipLU)
            {
                queryLogger.Log("{ \"DialogEventType\": \"Query\" }", LogLevel.Ins);
            }
            else
            {
                queryLogger.Log("{ \"DialogEventType\": \"DirectAction\" }", LogLevel.Ins);
            }

            List<InstrumentationEvent> luInstrumentation = null;
            KnowledgeContext luEntityContext = null;
            Stack<ConversationState> prefetchedConversationState = null;

            // enter thread-safe block
            int mutexToken = await _asynchronousLoadMutex.EnterReadLockAsync().ConfigureAwait(false);
            try
            {
                // Put all hypotheses into LU and pick the one that results in the highest confidence
                List<RankedHypothesis> bestLUResult = CreateDefaultResult(clientInput.TextInput, clientInput.SpeechInput);
                int alternate = 0;
                int alternateUsed = 0;

                if (successfulRecognition && !skipLU)
                {
                    /////////////////////////////
                    ////////   Run LU!   ////////
                    /////////////////////////////
                    LURequest request = new LURequest()
                        {
                            DoFullAnnotation = true,
                            Context = clientInput.ClientContext,
                            TraceId = clientInput.TraceId,
                            RequestFlags = clientInput.RequestFlags,
                            TextInput = clientInput.TextInput,
                            SpeechInput = clientInput.SpeechInput,
                        };

                    if (clientInput.DomainScope != null && clientInput.DomainScope.Count > 0)
                    {
                        // Pass the domain scope through dialog to LU, and filter it based on what answers
                        // are actually loaded, so we don't trigger categorizers we don't need
                        ISet<string> loadedDomains = _dialogEngine.Value.GetLoadedPluginDomains();
                        request.DomainScope = new List<string>();

                        // Don't forget to add common, since it's not explicitly a "loaded" domain
                        request.DomainScope.Add(DialogConstants.COMMON_DOMAIN);
                        request.DomainScope.Add(DialogConstants.SIDE_SPEECH_DOMAIN);
                        foreach (string scopedDomain in clientInput.DomainScope)
                        {
                            if (loadedDomains.Contains(scopedDomain))
                            {
                                request.DomainScope.Add(scopedDomain);
                            }
                        }
                    }
                    
                    RetrieveResult<Stack<ConversationState>> sessionRetrieveResult =
                        await _conversationStateCache.TryRetrieveState(
                            clientInput.ClientContext.UserId,
                            clientInput.ClientContext.ClientId,
                            queryLogger.Clone("SessionStore"),
                            realTime).ConfigureAwait(false);
                    if (sessionRetrieveResult.Success)
                    {
                        prefetchedConversationState = sessionRetrieveResult.Result;
                        request.ContextualDomains = new List<string>();
                        foreach (ConversationState state in prefetchedConversationState)
                        {
                            if (!request.ContextualDomains.Contains(state.CurrentPluginDomain))
                            {
                                request.ContextualDomains.Add(state.CurrentPluginDomain);
                            }
                        }

                        queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Store_SessionRead, sessionRetrieveResult.LatencyMs), LogLevel.Ins);
                        queryLogger.Log("Found a conversation state, setting contextual domains = { " + string.Join(",", request.ContextualDomains) + " }", LogLevel.Vrb);
                    }

                    IDictionary<string, float> srConfidences = new Dictionary<string, float>();
                    if (clientInput.SpeechInput != null &&
                        clientInput.SpeechInput.RecognizedPhrases != null)
                    {
                        foreach (SpeechRecognizedPhrase query in clientInput.SpeechInput.RecognizedPhrases)
                        {
                            string srResultText = query.DisplayText;
                            // This will deduplicate the input queries and also give us a mapping between input and output confidences
                            if (!srConfidences.ContainsKey(srResultText))
                            {
                                srConfidences[srResultText] = query.SREngineConfidence;
                            }
                        }
                    }

                    LUResponse allResults = null;
                    if (_luInterface != null)
                    {
                        NetworkResponseInstrumented<LUResponse> networkLuResponse = await _luInterface.MakeQueryRequest(request, queryLogger, cancelToken, realTime).ConfigureAwait(true);
                        if (networkLuResponse != null)
                        {
                            queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Dialog_LUCall, networkLuResponse.EndToEndLatency), LogLevel.Ins);
                            allResults = networkLuResponse.UnboxAndDispose();
                        }
                    }
                    else
                    {
                        queryLogger.Log("LU was never configured, so I am returning null!", LogLevel.Err);
                    }

                    if (allResults == null)
                    {
                        // Null response (Could not connect)
                        // interpret it as noreco
                        queryLogger.Log("Null response from LU (could not connect)", LogLevel.Err);
                        return new DialogWebServiceResponse(new DialogResponse()
                            {
                                ErrorMessage = "Null response from LU (could not connect)",
                                ExecutionResult = Result.Failure,
                                TraceId = queryLogger.TraceId.HasValue ? CommonInstrumentation.FormatTraceId(queryLogger.TraceId.Value) : null
                            });
                    }
                    else if (allResults.Results.Count == 0)
                    {
                        // Empty response (No models fired)
                        // again, interpret it as noreco
                        queryLogger.Log("Empty results from LU (no models loaded/triggered)", LogLevel.Err);
                        return new DialogWebServiceResponse(new DialogResponse()
                            {
                                ErrorMessage = "Empty results from LU (no models loaded/triggered)",
                                ExecutionResult = Result.Failure,
                                TraceId = queryLogger.TraceId.HasValue ? CommonInstrumentation.FormatTraceId(queryLogger.TraceId.Value) : null
                        });
                    }
                    else
                    {
                        double bestConfidence = 0;
                        queryLogger.Log("LU analyzed " + allResults.Results.Count + " query hypotheses");
                        luInstrumentation = allResults.TraceInfo;
                        foreach (RecognizedPhrase luResult in allResults.Results)
                        {
                            List<RecoResult> recoResults = luResult.Recognition;
                            if (recoResults == null)
                            {
                                queryLogger.Log("Reco results list is null! This should never happen", LogLevel.Err);
                                continue;
                            }

                            if (queryLogger.ValidPrivacyClasses.HasFlag(DataPrivacyClassification.PrivateContent))
                            {
                                queryLogger.Log("Query hypothesis for \"" + luResult.Utterance + "\" has " + recoResults.Count + " reco results", privacyClass: DataPrivacyClassification.PrivateContent);
                            }
                            else
                            {
                                queryLogger.Log("Query hypothesis has " + recoResults.Count + " reco results");
                            }

                            float srConfidence = 0.5f;
                            if (srConfidences.ContainsKey(luResult.Utterance))
                            {
                                srConfidence = srConfidences[luResult.Utterance];
                            }

                            // Determine if an utterance with a lower SR score has a much higher LU score,
                            // in which case it supplants the top result.
                            // TODO: This really needs data-driven balancing somehow
                            if (recoResults.Count > 0 &&
                                recoResults[0].Confidence * srConfidence > bestConfidence * 1.01f) // Alternates must be at least 1% better than the next highest to be considered. This is to sidestep a bug where regex matches would choose the most verbose SR output possible.
                            {
                                if (recoResults[0].Domain.Equals(DialogConstants.COMMON_DOMAIN) ||
                                    recoResults[0].Domain.Equals(DialogConstants.SIDE_SPEECH_DOMAIN))
                                {
                                    // Local domain results should be favored over common domain
                                    bestConfidence = recoResults[0].Confidence * srConfidence;
                                }
                                else
                                {
                                    bestConfidence = recoResults[0].Confidence * srConfidence * 1.15f;
                                }

                                bestLUResult = new List<RankedHypothesis>();
                                foreach (var recoResult in recoResults)
                                {
                                    bestLUResult.Add(new RankedHypothesis(recoResult));
                                }

                                // Deserialize entity context from the best LU result
                                luEntityContext = KnowledgeContextSerializer.TryDeserializeKnowledgeContext(luResult.EntityContext);

                                alternateUsed = alternate;
                            }
                            alternate++;
                        }

                        if (alternateUsed != 0)
                        {
                            queryLogger.Log("Reranker decided on alternate reco result " + alternateUsed + ": " +
                                            clientInput.SpeechInput.RecognizedPhrases[alternateUsed].DisplayText, privacyClass: DataPrivacyClassification.PrivateContent);
                        }
                    }
                }
                else if (skipLU)
                {
                    // Client passed a DirectDialogAction
                    queryLogger.Log("Client provided its own understanding data; skipping LU");
                    
                    RecognizedPhrase recoPhrase = clientInput.LanguageUnderstanding[0];
                    List<RecoResult> clientRecoResults = recoPhrase.Recognition;
                    luEntityContext = KnowledgeContextSerializer.TryDeserializeKnowledgeContext(recoPhrase.EntityContext);

                    // Fill in missing information in case the client only specified domain/intent
                    foreach (RecoResult action in clientRecoResults)
                    {
                        if (action.Confidence == 0)
                        {
                            action.Confidence = 1.0f;
                        }
                        if (action.TagHyps.Count == 0)
                        {
                            action.TagHyps.Add(new TaggedData()
                            {
                                Annotations = new Dictionary<string, string>(),
                                Slots = new List<SlotValue>(),
                                Utterance = string.Empty
                            });
                        }
                    }

                    bestLUResult = new List<RankedHypothesis>();
                    foreach (var recoResult in clientRecoResults)
                    {
                        bestLUResult.Add(new RankedHypothesis(recoResult));
                    }
                }

                // TODO: DialogProcessingEngine now does this on its own, so the only real benefit is that this logs the more accurate
                // input hyps in the debugging statements below
                foreach (RankedHypothesis recoResult in bestLUResult)
                {
                    // Prevent side-speech from overriding valid results by capping its confidence score
                    if (recoResult.Result.Domain.Equals(DialogConstants.COMMON_DOMAIN) &&
                        recoResult.Result.Intent.Equals(DialogConstants.SIDE_SPEECH_INTENT))
                    {
                        recoResult.CapConfidence(0.75f);
                    }
                }

                RecoResult topResult = bestLUResult[0].Result;

                queryLogger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata, "Highest domain: {0}/{1} {2}", topResult.Domain, topResult.Intent, topResult.Confidence);
                if (topResult.MostLikelyTags != null)
                {
                    foreach (SlotValue slot in topResult.MostLikelyTags.Slots)
                    {
                        if (queryLogger.ValidPrivacyClasses.HasFlag(DataPrivacyClassification.PrivateContent))
                        {
                            queryLogger.LogFormat(LogLevel.Std, DataPrivacyClassification.PrivateContent, "  Slot \"{0}\" has value \"{1}\"", slot.Name, slot.Value);
                            if (slot.PropertyNames != null)
                            {
                                foreach (string prop in slot.PropertyNames)
                                {
                                    queryLogger.LogFormat(LogLevel.Std, DataPrivacyClassification.PrivateContent, "    {0}\" = \"{1}\"", prop, slot.GetProperty(prop));
                                }
                            }
                            if (slot.Alternates != null && slot.Alternates.Count > 0)
                            {
                                queryLogger.LogFormat(LogLevel.Std, DataPrivacyClassification.PrivateContent, "    Homophones: {0}", string.Join(",", slot.Alternates));
                            }
                        }
                        else
                        {
                            queryLogger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata, "  Slot \"{0}\" is present", slot.Name);
                        }
                    }
                }

                DialogWebServiceResponse finalDialogResponse = await ProcessRecoResults(
                    bestLUResult,
                    clientInput,
                    clientAuthLevel,
                    queryLogger,
                    luEntityContext,
                    prefetchedConversationState,
                    realTime).ConfigureAwait(true);
                
                // Add LU instant tracing info to dialog info, if any exists
                if (luInstrumentation != null && luInstrumentation.Count > 0)
                {
                    if (finalDialogResponse.ClientResponse.TraceInfo == null)
                    {
                        finalDialogResponse.ClientResponse.TraceInfo = luInstrumentation;
                    }
                    else
                    {
                        finalDialogResponse.ClientResponse.TraceInfo.FastAddRangeList(luInstrumentation);
                        finalDialogResponse.ClientResponse.TraceInfo.Sort((a, b) => { return (int)(a.Timestamp - b.Timestamp); });
                    }
                }

                finalDialogResponse.ClientResponse.TraceId = queryLogger.TraceId.HasValue ? CommonInstrumentation.FormatTraceId(queryLogger.TraceId.Value) : null;
                return finalDialogResponse;
            }
            finally
            {
                queryLogger.Log("All dialog processing finished", LogLevel.Vrb);
                _asynchronousLoadMutex.ExitReadLock(mutexToken);
            }
        }

        /// <summary>
        /// Creates either a side_speech or noreco result based on the input utterance, to use as a fallback if LU returns an empty set.
        /// </summary>
        /// <param name="textInput"></param>
        /// <param name="speechInput"></param>
        /// <returns></returns>
        private static List<RankedHypothesis> CreateDefaultResult(string textInput, SpeechRecognitionResult speechInput)
        {
            // See if there is a valid utterance
            string utterance = string.Empty;
            if (!string.IsNullOrEmpty(textInput))
            {
                utterance = textInput;
            }
            else if (speechInput != null && speechInput.RecognizedPhrases != null && speechInput.RecognizedPhrases.Count > 0)
            {
                if (speechInput.RecognizedPhrases[0].InverseTextNormalizationResults != null && speechInput.RecognizedPhrases[0].InverseTextNormalizationResults.Count > 0)
                {
                    utterance = speechInput.RecognizedPhrases[0].InverseTextNormalizationResults[0];
                }
                else
                {
                    utterance = speechInput.RecognizedPhrases[0].DisplayText;
                }
            }

            List<RankedHypothesis> returnVal = new List<RankedHypothesis>();
            TaggedData emptyTaggedData = new TaggedData()
                {
                    Annotations = new Dictionary<string, string>(),
                    Slots = new List<SlotValue>(),
                    Utterance = utterance,
                    Confidence = 0.0f
                };
            RecoResult reco = new RecoResult()
                {
                    Confidence = 1.0f,
                    Domain = DialogConstants.COMMON_DOMAIN,
                    Intent = string.IsNullOrEmpty(utterance) ? DialogConstants.NORECO_INTENT : DialogConstants.SIDE_SPEECH_INTENT,
                    Utterance = new Sentence(utterance)
                };
            reco.TagHyps.Add(emptyTaggedData);
            RankedHypothesis rankedHyp = new RankedHypothesis(reco);
            rankedHyp.DialogPriority = 0;
            returnVal.Add(rankedHyp);
            return returnVal;
        }

        public void ResetClientState(string userId, string clientId, ILogger queryLogger)
        {
            queryLogger.Log("Client has requested a reset of client state");

            if (queryLogger.ValidPrivacyClasses.HasFlag(DataPrivacyClassification.EndUserPseudonymousIdentifiers))
            {
                queryLogger.Log("UserId=" + userId + " / ClientId=" + clientId + " has requested a new context", privacyClass: DataPrivacyClassification.EndUserPseudonymousIdentifiers);
            }

            _conversationStateCache.ClearClientSpecificState(userId, clientId, queryLogger, true);
        }

        public async Task<DialogWebServiceResponse> ProcessDialogAction(DialogRequest request, string dialogActionKey, IRealTimeProvider realTime)
        {
            int mutexToken = await _asynchronousLoadMutex.EnterReadLockAsync().ConfigureAwait(false);
            try
            {
                Guid traceIdGuid;
                if (string.IsNullOrEmpty(request.TraceId) || !Guid.TryParse(request.TraceId, out traceIdGuid))
                {
                    traceIdGuid = Guid.NewGuid();
                    request.TraceId = CommonInstrumentation.FormatTraceId(traceIdGuid);
                }

                ILogger queryLogger = _logger.CreateTraceLogger(traceIdGuid);

                //LogClientRequest(request, queryLogger);

                return await ProcessDialogActionWithoutLocking(request,
                    dialogActionKey,
                    queryLogger,
                    realTime).ConfigureAwait(false);
            }
            finally
            {
                _asynchronousLoadMutex.ExitReadLock(mutexToken);
            }
        }

        /// <summary>
        /// Used internally to invoke a dialog action. This method does not use the mutex, so it allows recursion
        /// </summary>
        /// <param name="request"></param>
        /// <param name="dialogActionKey"></param>
        /// <param name="queryLogger"></param>
        /// <param name="realTime"></param>
        /// <returns></returns>
        private async Task<DialogWebServiceResponse> ProcessDialogActionWithoutLocking(
            DialogRequest request,
            string dialogActionKey,
            ILogger queryLogger,
            IRealTimeProvider realTime)
        {
            // Fetch the dialog action from the cache
            RetrieveResult<DialogAction> cacheResult = await _cachedDialogActions.Value.TryRetrieve(
                dialogActionKey,
                queryLogger,
                realTime,
                TimeSpan.FromSeconds(10)).ConfigureAwait(false);

            if (!cacheResult.Success)
            {
                string errorMessage = "No dialog action has been cached with key " + dialogActionKey;
                queryLogger.Log(errorMessage, LogLevel.Err);
                return new DialogWebServiceResponse(new DialogResponse()
                    {
                        ExecutionResult = Result.Failure,
                        ErrorMessage = errorMessage
                    });
            }

            DialogAction action = cacheResult.Result;
            request.InteractionType = action.InteractionMethod;
            
            // We defer logging the client input until this point so that the input type and other properties that are derived from the cached
            // action are properly filled in and we don't have to backfill things later
            LogClientRequest(request, queryLogger);

            // we don't need to log the whole action because it will become the selected reco result later
            queryLogger.Log("{\"Dialog\":{\"TriggeredActionId\":\"" + dialogActionKey + "\"}}", LogLevel.Ins);

            queryLogger.Log(string.Format("Triggering custom dialog action: Domain={0} Intent={1} Method={2}",
                action.Domain, action.Intent, action.InteractionMethod), LogLevel.Std);
            ClientAuthenticationLevel clientAuthLevel = ClientAuthenticationLevel.None;
            if (request.AuthTokens != null)
            {
                clientAuthLevel =  await GetClientAuthentication(request, queryLogger, realTime).ConfigureAwait(false);
            }

            List<RankedHypothesis> rankedHyps = new List<RankedHypothesis>();
            TaggedData taggedSlots = new TaggedData
                {
                    Utterance = string.Empty
                };
            taggedSlots.Slots.FastAddRangeList(action.Slots);
            RecoResult result = new RecoResult
                {
                    Confidence = 1.0f,
                    Domain = action.Domain,
                    Intent = action.Intent
                };
            result.TagHyps.Add(taggedSlots);
            rankedHyps.Add(new RankedHypothesis(result));

            return await ProcessRecoResults(
                recoResults: rankedHyps,
                clientInput: request,
                clientAuthLevel: clientAuthLevel,
                queryLogger: queryLogger,
                inputEntityContext: null,
                prefetchedSession: null,
                realTime: realTime).ConfigureAwait(false);
        }

        public Task<DialogWebServiceResponse> ProcessRecoResults(List<RecoResult> recoResults,
            DialogRequest clientInput,
            ClientAuthenticationLevel clientAuthLevel,
            ILogger queryLogger,
            KnowledgeContext luEntityContext = null,
            Stack<ConversationState> prefetchedSession = null,
            IRealTimeProvider realTime = null)
        {
            return ProcessRecoResults(
                RankedHypothesis.ConvertRecoResultList(recoResults),
                clientInput,
                clientAuthLevel,
                queryLogger,
                luEntityContext,
                prefetchedSession,
                realTime ?? DefaultRealTimeProvider.Singleton);
        }

        /// <summary>
        /// Sends a list of recognition results through the dialog engine for processing. This method invokes
        /// the DialogProcessingEngine.Process method which runs all the dialog logic, and then converts all of the output
        /// from a DialogEngineResponse into a ClientResponse. This conversion includes rendering HTML, processing
        /// audio, handling special URLs, and so forth.
        /// </summary>
        /// <param name="recoResults"></param>
        /// <param name="clientInput"></param>
        /// <param name="clientAuthLevel"></param>
        /// <param name="queryLogger"></param>
        /// <param name="inputEntityContext"></param>
        /// <param name="prefetchedSession">If you already have a session, you can reuse it here</param>
        /// <param name="realTime"></param>
        /// <returns></returns>
        public async Task<DialogWebServiceResponse> ProcessRecoResults(
            List<RankedHypothesis> recoResults,
            DialogRequest clientInput,
            ClientAuthenticationLevel clientAuthLevel,
            ILogger queryLogger,
            KnowledgeContext inputEntityContext,
            Stack<ConversationState> prefetchedSession,
            IRealTimeProvider realTime)
        {
            DialogResponse finalResponse = new DialogResponse()
            {
                ExecutionResult = Result.Failure,
                ResponseText = string.Empty,
                ResponseUrl = string.Empty,
                UrlScope = UrlScope.Local,
                SuggestedRetryDelay = 0,
                ContinueImmediately = false,
                ErrorMessage = string.Empty,
                ResponseAudio = new AudioData()
            };

            AudioEncoder outputAudioStream = null;
             
            try
            {
                Stopwatch dialogTimer = new Stopwatch();
                dialogTimer.Start();

                // Regarding the isNewConversation flag: If the client initiated a dialog action directly
                // (using programmatic recoresults), we should clear that client's context. This is because
                // we assume that such a request can only ever be used to start a new conversation (the canonical
                // use case here is like a proactive canvas that synthesizes reco results). Tactile actions
                // such as pressing buttons on a UI will have passed its results into recoResults and shouldn't
                // trigger isNewConversation here
                bool isNewConversation =
                    clientInput.InteractionType == InputMethod.Programmatic &&
                    clientInput.LanguageUnderstanding != null &&
                    clientInput.LanguageUnderstanding.Count > 0;
                AudioData queryAudio = clientInput.InteractionType == InputMethod.Spoken
                            ? clientInput.AudioInput
                            : null;

                IList<ContextualEntity> contextualEntities = new List<ContextualEntity>();
                
                // Deserialize client-provided entity context, if any
                if (clientInput.EntityInput != null && clientInput.EntityInput.Count > 0 &&
                    clientInput.EntityContext.Count > 0)
                {
                    queryLogger.Log("Deserializing client-provided entities...");

                    if (inputEntityContext == null)
                    {
                        // Create a new input entity context if we didn't get one from LU
                        inputEntityContext = new KnowledgeContext();
                    }

                    KnowledgeContext clientEntityContext = KnowledgeContextSerializer.TryDeserializeKnowledgeContext(clientInput.EntityContext);
                    foreach (var entityRef in clientInput.EntityInput)
                    {
                        Entity handle = clientEntityContext.GetEntityInMemory(entityRef.EntityId);
                        if (handle == null)
                        {
                            queryLogger.Log("Entity " + entityRef.EntityId + " was declared in client input but was not found in entity context!", LogLevel.Wrn);
                        }
                        else
                        {
                            handle.CopyTo(inputEntityContext);
                            ContextualEntity clientProvidedEntity = new ContextualEntity(handle, ContextualEntitySource.ClientInput, entityRef.Relevance);
                            contextualEntities.Add(clientProvidedEntity);
                        }
                    }

                    queryLogger.Log("Processed " + contextualEntities.Count + " client-provided entities");
                }

                //////////////////////////
                // Run dialog engine!!! //
                //////////////////////////
                DialogEngineResponse dialogEngineResponse = await _dialogEngine.Value.Process(
                    results: recoResults,
                    clientContext: clientInput.ClientContext,
                    authLevel: clientAuthLevel,
                    inputSource: clientInput.InteractionType,
                    isNewConversation: isNewConversation,
                    useTriggers: true,
                    traceId: CommonInstrumentation.TryParseTraceIdGuid(clientInput.TraceId),
                    queryLogger: queryLogger.Clone("DialogCore"),
                    inputAudio: queryAudio,
                    textInput: clientInput.TextInput,
                    speechInput: clientInput.SpeechInput,
                    conversationStack: prefetchedSession,
                    triggerSideEffects: null,
                    bargeInTime: clientInput.ClientAudioPlaybackTimeMs.GetValueOrDefault(-1),
                    requestFlags: clientInput.RequestFlags,
                    inputEntityContext: inputEntityContext,
                    contextualEntities: contextualEntities,
                    requestData: clientInput.RequestData,
                    realTime: realTime).ConfigureAwait(false);

                dialogTimer.Stop();

                //if (dialogTimer.Elapsed > TimeSpan.FromMilliseconds(500))
                //{
                //    for (int c = 0; c < 20; c++) queryLogger.Log("Stutter detected!", LogLevel.Err);
                //}

                queryLogger.DispatchAsync(
                    (delegateLogger, timestamp) =>
                    {
                        // Scrub out huge audio response data if present
                        JObject dialogResponseToLog = CommonInstrumentation.ToJObject(dialogEngineResponse);
                        CommonInstrumentation.NullifyField(dialogResponseToLog, "$.ResponseAudio.Data");

                        IDictionary<DataPrivacyClassification, JToken> impressionsDividedByPrivacyClass = 
                            CommonInstrumentation.SplitObjectByPrivacyClass(
                                CommonInstrumentation.PrependPath(dialogResponseToLog, "$.Dialog.DialogProcessorResponse"),
                                delegateLogger.DefaultPrivacyClass,
                                CommonInstrumentation.GetPrivacyMappingsDialogResponse(dialogEngineResponse.PluginResponsePrivacyClass),
                                delegateLogger);

                        using (PooledStringBuilder builder = StringBuilderPool.Rent())
                        {
                            foreach (var classifiedLogMsg in impressionsDividedByPrivacyClass)
                            {
                                delegateLogger.Log(
                                    CommonInstrumentation.FromJObject(classifiedLogMsg.Value, builder.Builder),
                                    LogLevel.Ins,
                                    delegateLogger.TraceId,
                                    classifiedLogMsg.Key,
                                    timestamp);
                                builder.Builder.Length = 0;
                            }
                        }
                    });
                
                queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Dialog_Core, dialogTimer), LogLevel.Ins);
                _metrics.Value.ReportPercentile(CommonInstrumentation.Key_Counter_Dialog_CoreLatency, _dimensions, dialogTimer.ElapsedMillisecondsPrecise());

                finalResponse.ExecutionResult = dialogEngineResponse.ResponseCode;
                finalResponse.ExecutedPlugin = dialogEngineResponse.ExecutedPlugin;
                ClientCapabilities clientCaps = clientInput.ClientContext.GetCapabilities();

                if (dialogEngineResponse.ResponseCode == Result.Success)
                {
                    finalResponse.IsRetrying = dialogEngineResponse.IsRetrying;

                    // Did the client return a URL?
                    if (!finalResponse.IsRetrying && !string.IsNullOrWhiteSpace(dialogEngineResponse.ActionURL))
                    {
                        // Is it a custom dialog action? (If so, we need to return the results of that action)
                        // This behavior is deprecated since the plugin can just return a dialogaction directly now, and it will be handled inside of DialogProcessingEngine
                        //if (dialogEngineResponse.ActionURL.StartsWith("/action"))
                        //{
                        //    queryLogger.Log("Dialog plugin triggered a redirection! Reprocessing query...");
                        //    string actionKey = StringUtils.RegexRip(new Regex("key=([a-fA-F0-9]+)"),
                        //                                              dialogEngineResponse.ActionURL, 1);
                        //    return this.ProcessDialogActionWithoutLocking(clientInput, actionKey, queryLogger, out outputAudioStream);
                        //}

                        finalResponse.ResponseUrl = dialogEngineResponse.ActionURL;
                        finalResponse.UrlScope = dialogEngineResponse.UrlScope;
                    }

                    // Did the plugin not return a link, and can the client view HTML?
                    // If so, either echo the plugin's HTML response, or create our own out of the response text
                    if ((clientCaps.HasFlag(ClientCapabilities.DisplayBasicHtml) ||
                         clientCaps.HasFlag(ClientCapabilities.DisplayHtml5)) &&
                        (!string.IsNullOrWhiteSpace(dialogEngineResponse.PresentationHtml) ||
                         !string.IsNullOrWhiteSpace(dialogEngineResponse.DisplayedText)))
                    {
                        string htmlToDisplay = dialogEngineResponse.PresentationHtml;
                        if (!finalResponse.IsRetrying &&
                            !string.IsNullOrWhiteSpace(dialogEngineResponse.DisplayedText) &&
                            string.IsNullOrEmpty(finalResponse.ResponseUrl) &&
                            string.IsNullOrEmpty(htmlToDisplay) &&
                            !clientCaps.HasFlag(ClientCapabilities.DoNotRenderTextAsHtml))
                        {
                            // note that if the plugin returns a URL and text, but no HTML, then the text rendering should
                            // not override the display URL, because that's probably more important.
                            // FIXME DEFAULT HTML PAGE HERE SHOULD PROBABLY CHANGE
                            queryLogger.Log("Generating default HTML page from text result to display on client screen");
                            htmlToDisplay = "<html><body bgcolor=\"black\"><font color=\"white\">" + dialogEngineResponse.DisplayedText +
                                  "</font></body></html>";
                        }

                        // Is the client able to render it themselves?
                        if (clientCaps.HasFlag(ClientCapabilities.ServeHtml))
                        {
                            finalResponse.ResponseHtml = htmlToDisplay;
                        }
                        else if (!string.IsNullOrEmpty(htmlToDisplay))
                        {
                            // We must serve the HTML ourselves (if it's not empty) and return a redirection link
                            string pageKey = Guid.NewGuid().ToString("N");
                            CachedWebData htmlCacheItem = new CachedWebData(Encoding.UTF8.GetBytes(htmlToDisplay), "text/html", queryLogger.TraceId);
                            await _cachedWebData.Value.Store(pageKey, htmlCacheItem, null, TimeSpan.FromMinutes(10), true, queryLogger, realTime).ConfigureAwait(false);
                            queryLogger.Log("We have HTML and it is being served from dialog with pagekey " + pageKey, LogLevel.Vrb);
                            if (!queryLogger.TraceId.HasValue)
                            {
                                finalResponse.ResponseUrl = string.Format("/cache?page={0}", pageKey);
                            }
                            else
                            {
                                finalResponse.ResponseUrl = string.Format("/cache?page={0}&trace={1}", pageKey, CommonInstrumentation.FormatTraceId(queryLogger.TraceId.Value));
                            }

                            finalResponse.UrlScope = UrlScope.Local;
                        }
                    }

                    // Only allow audio if the client reports that it has speakers,
                    // and the action is not a "silent" tactile action
                    if (clientCaps.HasFlag(ClientCapabilities.HasSpeakers) &&
                        clientInput.InteractionType != InputMethod.Tactile)
                    {
                        bool synthesizeSpeech = !string.IsNullOrEmpty(dialogEngineResponse.SpokenSsml) &&
                            !clientCaps.HasFlag(ClientCapabilities.CanSynthesizeSpeech);

                        // Parse the client audio format from request, if available
                        AudioSampleFormat clientPreferredAudioFormat;
                        if (string.IsNullOrEmpty(clientInput.PreferredAudioFormat) ||
                            !CommonCodecParamHelper.TryParseCodecParams(clientInput.PreferredAudioFormat, out clientPreferredAudioFormat))
                        {
                            clientPreferredAudioFormat = AudioSampleFormat.Mono(16000);
                        }

                        IAudioGraph audioGraph = new AudioGraph(AudioGraphCapabilities.None);
                        WeakPointer<IAudioGraph> graph = new WeakPointer<IAudioGraph>(audioGraph);
                        IAudioSampleSource finalAudioGenerator = null;
                        try
                        {
                            finalAudioGenerator = await ClientCore.GenerateFinalResponseAudio(
                                dialogEngineResponse.ResponseAudio == null ? null : dialogEngineResponse.ResponseAudio.Data,
                                dialogEngineResponse.SpokenSsml,
                                dialogEngineResponse.ResponseAudio == null ? AudioOrdering.Unspecified : dialogEngineResponse.ResponseAudio.Ordering,
                                queryLogger,
                                _codecFactory,
                                clientPreferredAudioFormat,
                                graph,
                                synthesizeSpeech ? _speechSynthesizer : null,
                                clientInput.ClientContext.Locale,
                                _audioQuality,
                                CancellationToken.None,
                                realTime).ConfigureAwait(false);

                            outputAudioStream = CreateOutputAudioEncoder(
                                queryLogger,
                                clientInput,
                                clientCaps,
                                clientPreferredAudioFormat,
                                _codecFactory,
                                graph);
                            outputAudioStream.ConnectInput(finalAudioGenerator);
                            outputAudioStream.TakeOwnershipOfDisposable(finalAudioGenerator);
                            finalAudioGenerator = null;
                            outputAudioStream.TakeOwnershipOfDisposable(audioGraph);
                            audioGraph = null;

                            // Do we return a single block of audio (synchronous processing) or do we return a stream?
                            if (!clientCaps.HasFlag(ClientCapabilities.SupportsStreamingAudio))
                            {
                                using (RecyclableMemoryStream audioDataDump = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
                                {
                                    // Client does not support streaming audio.
                                    // Run processing synchronously and dump all stream data into a buffer
                                    Stopwatch audioTimer = new Stopwatch();
                                    audioTimer.Start();
                                    AudioInitializationResult initResult = await outputAudioStream.Initialize(
                                        audioDataDump,
                                        false,
                                        CancellationToken.None,
                                        realTime).ConfigureAwait(false);
                                    if (initResult != AudioInitializationResult.Success)
                                    {
                                        finalResponse.ResponseAudio = null;
                                        throw new Exception("Couldn't initialize audio decoder");
                                    }

                                    await outputAudioStream.ReadFully(CancellationToken.None, realTime, TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
                                    if (audioDataDump.Length > 0)
                                    {
                                        finalResponse.ResponseAudio = new AudioData()
                                        {
                                            Codec = outputAudioStream.Codec,
                                            CodecParams = outputAudioStream.CodecParams,
                                            Data = new ArraySegment<byte>(audioDataDump.ToArray())
                                        };
                                    }
                                    else
                                    {
                                        finalResponse.ResponseAudio = null;
                                    }

                                    outputAudioStream.Dispose();
                                    outputAudioStream = null;
                                    audioTimer.Stop();
                                    queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Dialog_ProcessSyncAudio, audioTimer), LogLevel.Ins);
                                }
                            }
                        }
                        finally
                        {
                            finalAudioGenerator?.Dispose(); // error case
                            audioGraph?.Dispose();
                        }
                    }

                    if ((clientCaps.HasFlag(ClientCapabilities.DisplayBasicText) ||
                        clientCaps.HasFlag(ClientCapabilities.DisplayUnlimitedText))
                        && !string.IsNullOrWhiteSpace(dialogEngineResponse.DisplayedText))
                    {
                        finalResponse.ResponseText = dialogEngineResponse.DisplayedText;
                        queryLogger.Log("DisplayText = \"" + finalResponse.ResponseText + "\"", privacyClass: dialogEngineResponse.PluginResponsePrivacyClass);
                    }

                    finalResponse.ContinueImmediately = dialogEngineResponse.NextTurnBehavior.Continues &&
                                                        dialogEngineResponse.NextTurnBehavior.IsImmediate;
                    if (finalResponse.ContinueImmediately)
                    {
                        queryLogger.Log("Dialog is signaling client to continue this conversation");
                    }

                    if (dialogEngineResponse.ResponseData != null && dialogEngineResponse.ResponseData.Count > 0)
                    {
                        finalResponse.ResponseData = new Dictionary<string, string>();
                        foreach (KeyValuePair<string, string> responseDataItem in dialogEngineResponse.ResponseData)
                        {
                            finalResponse.ResponseData.Add(responseDataItem.Key, responseDataItem.Value);
                        }
                    }

                    // Pass over the ssml if the client can synthesize it
                    if (!string.IsNullOrEmpty(dialogEngineResponse.SpokenSsml) &&
                        clientCaps.HasFlag(ClientCapabilities.CanSynthesizeSpeech))
                    {
                        finalResponse.ResponseSsml = dialogEngineResponse.SpokenSsml;
                    }

                    // If DE returned an augmented query, use that too
                    if (!string.IsNullOrEmpty(dialogEngineResponse.AugmentedQuery))
                    {
                        finalResponse.AugmentedFinalQuery = dialogEngineResponse.AugmentedQuery;
                    }

                    if (clientCaps.HasFlag(ClientCapabilities.ClientActions) && 
                        !string.IsNullOrEmpty(dialogEngineResponse.ClientAction))
                    {
                        finalResponse.ResponseAction = dialogEngineResponse.ClientAction;
                    }

                    if (clientCaps.HasFlag(ClientCapabilities.KeywordSpotter) &&
                            dialogEngineResponse.TriggerKeywords != null &&
                            dialogEngineResponse.TriggerKeywords.Count > 0)
                    {
                        finalResponse.TriggerKeywords = dialogEngineResponse.TriggerKeywords;
                        queryLogger.Log("This response includes " + finalResponse.TriggerKeywords.Count + " instant keywords.");
                    }

                    // Pass the top reco result
                    finalResponse.SelectedRecoResult = dialogEngineResponse.SelectedRecoResult;
                    finalResponse.ConversationLifetimeSeconds = dialogEngineResponse.NextTurnBehavior.ConversationTimeoutSeconds;
                }
                else if (dialogEngineResponse.ResponseCode == Result.Failure)
                {
                    if (!string.IsNullOrWhiteSpace(dialogEngineResponse.ErrorMessage))
                    {
                        finalResponse.ErrorMessage = dialogEngineResponse.ErrorMessage;
                    }
                    else
                    {
                        finalResponse.ErrorMessage = "The dialog workflow encountered an unspecified error. Sorry about that!";
                    }
                }
                else if (dialogEngineResponse.ResponseCode == Result.Skip)
                {
                    finalResponse.ErrorMessage = "No recognition triggered";
                }
            }
            catch (DialogException dialogError)
            {
                if (_serverConfig.FailFastPlugins)
                {
                    ExceptionDispatchInfo.Capture(dialogError).Throw();
                }
                else
                {
                    queryLogger.Log("Caught a dialog exception from the dialog engine: " + dialogError.Message, LogLevel.Err);
                    finalResponse.ExecutionResult = Result.Failure;
                    finalResponse.ErrorMessage = dialogError.Message;
                }
            }
            catch (Exception e)
            {
                if (_serverConfig.FailFastPlugins)
                {
                    throw;
                }
                else
                {
                    _logger.Log("An exception occurred during dialog processing", LogLevel.Err);
                    _logger.Log(e, LogLevel.Err);
                }
            }

            // If the query enables instant tracing, do the trace now
            if (clientInput.RequestFlags.HasFlag(QueryFlags.Trace))
            {
                await queryLogger.Flush(CancellationToken.None, realTime, true).ConfigureAwait(false);
                EventOnlyLogger eventLogger = EventOnlyLogger.TryExtractFromAggregate(queryLogger);
                if (eventLogger != null)
                {
                    ILoggingHistory history = eventLogger.History;
                    finalResponse.TraceInfo = new List<InstrumentationEvent>();
                    FilterCriteria filter = new FilterCriteria()
                    {
                        TraceId = queryLogger.TraceId,
                        //Level = LogLevel.Std | LogLevel.Err | LogLevel.Wrn | LogLevel.Vrb
                    };

                    foreach (LogEvent e in history.FilterByCriteria(filter))
                    {
                        // TODO decrypt messages?
                        finalResponse.TraceInfo.Add(InstrumentationEvent.FromLogEvent(e));
                    }
                }
                else
                {
                    queryLogger.Log("Request specified the {Trace} flag, but no insta-trace data is available!", LogLevel.Wrn);
                }
            }

            return new DialogWebServiceResponse(finalResponse, outputAudioStream);
        }

        private async Task<AudioData> TranscodeAudioToPcm(
            AudioData inputData,
            ILogger queryLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            if (string.Equals(inputData.Codec, RawPcmCodecFactory.CODEC_NAME_PCM_S16LE))
            {
                // No transcoding needed
                return inputData;
            }

            using (MemoryStream inputStream = new MemoryStream(
                        inputData.Data.Array,
                        inputData.Data.Offset,
                        inputData.Data.Count,
                        false))
            using (IAudioGraph dummyGraph = new AudioGraph(AudioGraphCapabilities.None))
            using (RecyclableMemoryStream outputStream = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
            {
                WeakPointer<IAudioGraph> graph = new WeakPointer<IAudioGraph>(dummyGraph);
                Stopwatch timer = new Stopwatch();
                timer.Start();
                AudioDecoder decoder = null;
                AudioEncoder pcmEncoder = null;
                AudioConformer conformer = null;
                try
                {
                    decoder = _codecFactory.CreateDecoder(inputData.Codec, inputData.CodecParams, graph, queryLogger.Clone("PluginAudioDecoder"), "PluginAudioDecoder");
                    AudioInitializationResult initResult = await decoder.Initialize(
#pragma warning disable CA2000 // Dispose objects before losing scope (ownership of stream is transferred)
                        new NonRealTimeStreamWrapper(inputStream, false),
#pragma warning restore CA2000 // Dispose objects before losing scope
                        true,
                        cancelToken,
                        realTime).ConfigureAwait(false);
                    if (initResult != AudioInitializationResult.Success)
                    {
                        throw new Exception("Could not initialize codec to decode client audio \"" + inputData.Codec + "\" (corrupted stream?)");
                    }

                    pcmEncoder = _codecFactory.CreateEncoder(RawPcmCodecFactory.CODEC_NAME_PCM_S16LE, graph, decoder.OutputFormat, queryLogger.Clone("PcmTranscoder"), "PluginAudioPcmEncoder");
                    initResult = await pcmEncoder.Initialize(
#pragma warning disable CA2000 // Dispose objects before losing scope (ownership of stream is transferred)
                        new NonRealTimeStreamWrapper(outputStream, false),
#pragma warning restore CA2000 // Dispose objects before losing scope
                        true,
                        cancelToken,
                        realTime).ConfigureAwait(false);
                    if (initResult != AudioInitializationResult.Success)
                    {
                        throw new Exception("Could not initialize PCM codec; this should never happen");
                    }


                    conformer = new AudioConformer(
                        graph,
                        decoder.OutputFormat,
                        pcmEncoder.InputFormat,
                        "PluginOutputConformer",
                        queryLogger.Clone("PluginOutputConformer"),
                        resamplerQuality: _audioQuality);
                    decoder.ConnectOutput(conformer);
                    conformer.ConnectOutput(pcmEncoder);

                    await pcmEncoder.ReadFully(cancelToken, realTime, TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
                    timer.Stop();
                    queryLogger.Log("Audio decoding took " + timer.ElapsedMilliseconds + " ms.", LogLevel.Std);

                    return new AudioData()
                    {
                        Codec = pcmEncoder.Codec,
                        CodecParams = pcmEncoder.CodecParams,
                        Data = new ArraySegment<byte>(outputStream.ToArray())
                    };
                }
                finally
                {
                    decoder?.Dispose();
                    pcmEncoder?.Dispose();
                    conformer?.Dispose();
                }
            }
        }

        private async Task<SpeechRecognitionResult> RunSpeechRecognition(
            AudioData inputData,
            LanguageCode locale,
            ILogger queryLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            using (IAudioGraph dummyGraph = new AudioGraph(AudioGraphCapabilities.None))
            using (MemoryStream inputStream = new MemoryStream(
                        inputData.Data.Array,
                        inputData.Data.Offset,
                        inputData.Data.Count,
                        false))
            {
                WeakPointer<IAudioGraph> graph = new WeakPointer<IAudioGraph>(dummyGraph);
                Stopwatch srTimer = new Stopwatch();
                srTimer.Start();
                AudioDecoder decoder = null;
                ISpeechRecognizer recognizer = null;
                AudioConformer conformer = null;
                try
                {
                    recognizer = await _speechRecognitionEngine.CreateRecognitionStream(
                        graph,
                        "ClientAudioSpeechRecognizer",
                        locale,
                        queryLogger,
                        cancelToken,
                        realTime).ConfigureAwait(false);

                    decoder = _codecFactory.CreateDecoder(inputData.Codec, inputData.CodecParams, graph, queryLogger.Clone("ClientAudioDecoder"), "ClientAudioDecoder");
#pragma warning disable CA2000 // Dispose objects before losing scope
                    AudioInitializationResult initResult = await decoder.Initialize(new NonRealTimeStreamWrapper(inputStream, false), false, cancelToken, realTime).ConfigureAwait(false);
#pragma warning restore CA2000 // Dispose objects before losing scope
                    if (initResult != AudioInitializationResult.Success)
                    {
                        throw new Exception("Could not initialize codec to decode client audio \"" + inputData.Codec + "\" (corrupted stream?)");
                    }

                    conformer = new AudioConformer(
                        graph,
                        decoder.OutputFormat,
                        recognizer.InputFormat,
                        "ClientAudioToSRConformer",
                        queryLogger.Clone("ClientAudioToSRConformer"),
                        resamplerQuality: _audioQuality);
                    decoder.ConnectOutput(conformer);
                    conformer.ConnectOutput(recognizer);

                    await decoder.ReadFully(cancelToken, realTime).ConfigureAwait(false);
                    SpeechRecognitionResult returnVal = await recognizer.FinishUnderstandSpeech(cancelToken, realTime).ConfigureAwait(false);
                    srTimer.Stop();
                    queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry("DialogSR", srTimer), LogLevel.Ins);
                    return returnVal;
                }
                finally
                {
                    decoder?.Dispose();
                    recognizer?.Dispose();
                    conformer?.Dispose();
                }
            }
        }

        private static AudioEncoder CreateOutputAudioEncoder(
            ILogger queryLogger,
            DialogRequest clientInput,
            ClientCapabilities clientCaps,
            AudioSampleFormat preferredAudioFormat,
            IAudioCodecFactory audioCodecFactory,
            WeakPointer<IAudioGraph> graph)
        {
            // Determine which audio codec to use for the client response
            // Use the PreferredAudioCodec first, and if that's not available,
            // just use the same format as the input audio
            string preferredAudioCodec = null;
            if (!clientCaps.HasFlag(ClientCapabilities.SupportsCompressedAudio))
            {
                // Compression not supported
                preferredAudioCodec = null;
            }
            else if (!string.IsNullOrWhiteSpace(clientInput.PreferredAudioCodec))
            {
                // Compress using preferred audio codec
                preferredAudioCodec = clientInput.PreferredAudioCodec;
            }
            else if (clientInput.AudioInput != null &&
                     !string.IsNullOrEmpty(clientInput.AudioInput.Codec))
            {
                // Compress using the codec of the input audio
                preferredAudioCodec = clientInput.AudioInput.Codec;
            }

            if (preferredAudioCodec != null)
            {
                queryLogger.Log("Client's preferred audio encoding detected as " + preferredAudioCodec);
            }
            else
            {
                preferredAudioCodec = RawPcmCodecFactory.CODEC_NAME_PCM_S16LE;
            }

            // Make sure we can actually encode in that format
            if (preferredAudioCodec != null && !audioCodecFactory.CanEncode(preferredAudioCodec))
            {
                queryLogger.Log(
                    "Client requested an audio response using the \"" + preferredAudioCodec +
                    "\" codec, but no encoder is available! Will use PCM instead", LogLevel.Wrn);
                preferredAudioCodec = RawPcmCodecFactory.CODEC_NAME_PCM_S16LE;
            }

            queryLogger.Log("Encoding response audio using codec \"" + preferredAudioCodec + "\"");
            return audioCodecFactory.CreateEncoder(preferredAudioCodec, graph, preferredAudioFormat, queryLogger.Clone("OutputAudioEncoder"), "OutputAudioEncoder");
        }

#region Events

        // Passthrough from DialogProcessingEngine
        public event EventHandler<PluginRegisteredEventArgs> PluginRegistered;

        // Generated from this class
        public event EventHandler EngineStopped;
        
        private void OnEngineStopped()
        {
            if (EngineStopped != null)
            {
                EngineStopped(this, null);
            }
        }

#endregion
    }
}
