using Durandal.API;
using Durandal.Common.Audio;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.Client;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Web;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Security;
using Durandal.Common.Security.Client;
using Durandal.Common.Test.FVT.Inputs;
using Durandal.Common.Test.FVT.Validators;
using Durandal.Common.IO;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Client.Actions;
using Newtonsoft.Json.Linq;
using Durandal.Common.Utils;
using Durandal.Common.NLP.Language;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Audio.Codecs.Opus;

namespace Durandal.Common.Test.FVT
{
    /// <summary>
    /// Test driver for running functional validation tests against a dialog service (represented by an <see cref="IDialogClient"/>.
    /// </summary>
    public class FunctionalTestDriver : IDisposable
    {
        private readonly ValidatorFactory _validatorFactory;
        private readonly ILogger _logger;
        private readonly ConjunctionValidator.Parser _conjunctionValidatorParser;
        private readonly TimeSpan? _defaultPreDelay;
        private readonly IDialogClient _dialogClient;
        private readonly ClientAuthenticator _clientAuthenticator;
        private readonly InMemoryClientKeyStore _clientKeyStore;
        private readonly IFunctionalTestIdentityStore _testIdentityStore;
        private readonly IAudioCodecFactory _codecs;
        private int _disposed = 0;

        /// <summary>
        /// Creates a functional test driver.
        /// </summary>
        /// <param name="logger">A global logger.</param>
        /// <param name="dialogHttpClient"></param>
        /// <param name="dialogProtocol"></param>
        /// <param name="testIdentityStore"></param>
        /// <param name="defaultPreDelay">If specfied, this is the amount of time to wait between dialog turns to account for race conditions in databases, etc.</param>
        public FunctionalTestDriver(
            ILogger logger,
            IHttpClient dialogHttpClient,
            IDialogTransportProtocol dialogProtocol,
            IFunctionalTestIdentityStore testIdentityStore,
            TimeSpan? defaultPreDelay = null)
        {
            _logger = logger;
            _dialogClient = new DialogHttpClient(dialogHttpClient, _logger.Clone("DialogClient"), dialogProtocol);
            _defaultPreDelay = defaultPreDelay;
            _testIdentityStore = testIdentityStore;
            _conjunctionValidatorParser = new ConjunctionValidator.Parser();

            _validatorFactory = new ValidatorFactory();
            _validatorFactory.AddParser(_conjunctionValidatorParser);
            _validatorFactory.AddParser(new DisjunctionValidator.Parser());
            _validatorFactory.AddParser(new ResponseTextRegexValidator.Parser());
            _validatorFactory.AddParser(new ResponseHtmlRegexValidator.Parser());
            _validatorFactory.AddParser(new TriggeredDomainIntentValidator.Parser());
            _validatorFactory.AddParser(new NoErrorMessageValidator.Parser());

            _clientKeyStore = new InMemoryClientKeyStore();
            _clientAuthenticator = ClientAuthenticator.Create(_logger.Clone("FVTRequestAuthenticator"), new StandardRSADelegates(), new InMemoryClientKeyStore()).Await();
            _codecs = new AggregateCodecFactory(
                new RawPcmCodecFactory(),
                new OpusRawCodecFactory(_logger.Clone("OpusCodec")));

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~FunctionalTestDriver()
        {
            Dispose(false);
        }
#endif

        /// <summary>
        /// Runs a single test case end-to-end.
        /// </summary>
        /// <param name="toRun"></param>
        /// <param name="cancelToken"></param>
        /// <param name="realTime"></param>
        /// <param name="firstTurnTraceId"></param>
        /// <returns></returns>
        public async Task<FunctionalTestResult> RunTest(
            FunctionalTest toRun,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            Guid? firstTurnTraceId = null)
        {
            if (toRun == null)
            {
                throw new ArgumentNullException("Test to run is null!");
            }

            if (toRun.Turns == null ||
                toRun.Turns.Count == 0)
            {
                throw new ArgumentException("Test has no turns to run!");
            }

            if (toRun.Metadata == null)
            {
                throw new ArgumentException("Test has null metadata!");
            }

            _logger.Log("Starting to run validation test " + toRun.Metadata.TestName);

            // Parse inputs and validators
            List<AbstractFunctionalTestInput> turnInputs = new List<AbstractFunctionalTestInput>();
            List<AbstractFunctionalTestValidator> turnValidators = new List<AbstractFunctionalTestValidator>();

            Stopwatch wallclockTimer = new Stopwatch();
            long nonRealTimeTimer = 0;
            long nonRealTimeStartTime;
            FunctionalTestResult finalTestResult = new FunctionalTestResult()
            {
                TurnResults = new List<FunctionalTestTurnResult>()
            };

            foreach (FunctionalTestTurn inputTurn in toRun.Turns)
            {
                string inputType = inputTurn.Input.Value<string>("Type");

                if (string.Equals(BasicTextInput.INPUT_TYPE, inputType))
                {
                    turnInputs.Add(inputTurn.Input.ToObject<BasicTextInput>());
                }
                else if (string.Equals(BasicSpeechInput.INPUT_TYPE, inputType))
                {
                    turnInputs.Add(inputTurn.Input.ToObject<BasicSpeechInput>());
                }
                else if (string.Equals(ComplexSpeechInput.INPUT_TYPE, inputType))
                {
                    turnInputs.Add(inputTurn.Input.ToObject<ComplexSpeechInput>());
                }
                else if (string.Equals(ClientDialogActionInput.INPUT_TYPE, inputType))
                {
                    turnInputs.Add(inputTurn.Input.ToObject<ClientDialogActionInput>());
                }
                else
                {
                    throw new Exception("Unknown input type " + inputType);
                }

                // Manually interpret the list of turn validators using the conjunction validator parser; this makes the syntax easier at test design time
                turnValidators.Add(_conjunctionValidatorParser.CreateFromValidatorList(inputTurn.Validations, _validatorFactory));
            }

            // Acquire handles to the test users / clients that we will need
            FunctionalTestIdentityPair identities = await _testIdentityStore.GetIdentities(
                FunctionalTestFeatureConstraints.EmptyConstraints,
                FunctionalTestFeatureConstraints.EmptyConstraints,
                _logger.Clone("TestIdentityStore")).ConfigureAwait(false);
            if (identities == null)
            {
                // Couldn't fetch any identities. Report this as a test framework issue since it probably means we need a larger identity pool
                // FIXME need a better error handler for these kinds of cases
                throw new NullReferenceException("No functional test identity was fetched. This could indicate that you do not have enough users in the test pool.");
            }

            try
            {
                // Initialize conversation state in case this identity was just barely used for something else
                await _dialogClient.ResetConversationState(identities.UserIdentity.UserId, identities.ClientIdentity.ClientId, _logger, cancelToken, realTime).ConfigureAwait(false);
                await realTime.WaitAsync(TimeSpan.FromSeconds(1), cancelToken).ConfigureAwait(false);

                // Now execute the test
                for (int turnNum = 0; turnNum < toRun.Turns.Count; turnNum++)
                {
                    Guid traceId;
                    if (turnNum == 0 && firstTurnTraceId.HasValue)
                    {
                        traceId = firstTurnTraceId.Value;
                    }
                    else
                    {
                        traceId = Guid.NewGuid();
                    }

                    ILogger traceLogger = _logger.CreateTraceLogger(traceId, "FVTTestDriver");
                    FunctionalTestTurn turn = toRun.Turns[turnNum];
                    AbstractFunctionalTestInput input = turnInputs[turnNum];
                    AbstractFunctionalTestValidator validator = turnValidators[turnNum];

                    traceLogger.Log("Turn " + (turnNum + 1) + ": trace ID " + CommonInstrumentation.FormatTraceId(traceId));

                    if (turn.PreDelay.HasValue)
                    {
                        traceLogger.Log("Waiting for predelay of " + turn.PreDelay.Value.PrintTimeSpan());
                        await realTime.WaitAsync(turn.PreDelay.Value, cancelToken).ConfigureAwait(false);
                        traceLogger.Log("Predelay finished");
                    }
                    else if (turnNum != 0 && _defaultPreDelay.HasValue)
                    {
                        traceLogger.Log("Waiting for default predelay of " + _defaultPreDelay.Value.PrintTimeSpan());
                        await realTime.WaitAsync(_defaultPreDelay.Value, cancelToken).ConfigureAwait(false);
                        traceLogger.Log("Predelay finished");
                    }

                    nonRealTimeStartTime = realTime.TimestampMilliseconds;
                    wallclockTimer.Start();
                    FunctionalTestTurnResult turnResponse = await ExecuteInput(
                        input,
                        traceLogger,
                        traceId,
                        identities.UserIdentity,
                        identities.ClientIdentity,
                        finalTestResult.TurnResults,
                        cancelToken,
                        realTime).ConfigureAwait(false);
                    wallclockTimer.Stop();
                    nonRealTimeTimer += (realTime.TimestampMilliseconds - nonRealTimeStartTime);

                    ValidationResponse validationResp = validator.Validate(turnResponse);
                    turnResponse.ValidationResult = validationResp;

                    finalTestResult.TurnResults.Add(turnResponse);

                    if (validationResp.ValidationPassed)
                    {
                        traceLogger.Log("Validation test passed");
                    }
                    else
                    {
                        traceLogger.Log("Validation test failed: " + validationResp.FailureReason, LogLevel.Err);
                        break;
                    }
                }
            }
            finally
            {
                await _testIdentityStore.ReleaseIdentities(identities, _logger.Clone("TestIdentityStore")).ConfigureAwait(false);
                wallclockTimer.Stop();
            }

            // FIXME have this calculation account for non-realtime scenarios rather than relying on the wall clock time
            if (realTime.IsForDebug)
            {
                finalTestResult.ActualTimeSpentInTests = TimeSpan.FromMilliseconds(nonRealTimeTimer);
            }
            else
            {
                finalTestResult.ActualTimeSpentInTests = TimeSpan.FromTicks(wallclockTimer.ElapsedTicks);
            }

            return finalTestResult;
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
                _dialogClient?.Dispose();
            }
        }

        /// <summary>
        /// Takes a single abstract input, interprets it, and executes it against the test dialog client.
        /// This usually means dispatching a query to the client, but could also involve other actions like HTTP GET or PUT, or direct dialog actions.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="traceLogger"></param>
        /// <param name="traceId"></param>
        /// <param name="userIdentity"></param>
        /// <param name="clientIdentity"></param>
        /// <param name="previousTurnResults"></param>
        /// <param name="cancelToken"></param>
        /// <param name="realTime"></param>
        /// <returns></returns>
        private async Task<FunctionalTestTurnResult> ExecuteInput(
            AbstractFunctionalTestInput input,
            ILogger traceLogger,
            Guid traceId,
            FunctionalTestIdentity userIdentity,
            FunctionalTestIdentity clientIdentity,
            List<FunctionalTestTurnResult> previousTurnResults,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            FunctionalTestTurnResult turnResult = new FunctionalTestTurnResult()
            {
                TraceId = traceId,
                SPARequestData = null,
                DialogResponse = null,
                TurnStartTime = realTime.Time,
                TurnEndTime = null,
                ValidationResult = null,
                DialogRequest = null
            };

            ClientContext context = new ClientContext();
            context.SetCapabilities(
                ClientCapabilities.ClientActions |
                ClientCapabilities.ServeHtml |
                ClientCapabilities.DisplayHtml5 |
                ClientCapabilities.RsaEnabled |
                ClientCapabilities.HasInternetConnection |
                ClientCapabilities.DisplayUnlimitedText);
            context.ClientId = clientIdentity.ClientId;
            context.UserId = userIdentity.UserId;
            context.ReferenceDateTime = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss");
            context.UTCOffset = 0;
            context.ClientName = "FVT test client";
            // context.Latitude = clientLatitude;
            // context.Longitude = clientLongitude;
            context.ExtraClientContext[ClientContextField.FormFactor] = FormFactor.Integrated.ToString();
            context.ExtraClientContext[ClientContextField.ClientType] = "FVT";
            context.ExtraClientContext[ClientContextField.ClientVersion] = SVNVersionInfo.VersionString;

            if (input is BasicTextInput)
            {
                BasicTextInput castInput = input as BasicTextInput;
                DialogRequest request = new DialogRequest();
                request.InteractionType = InputMethod.Typed;
                request.ClientContext = context;
                request.RequestFlags = QueryFlags.Monitoring;
                request.TextInput = castInput.Text;
                request.TraceId = CommonInstrumentation.FormatTraceId(traceId);
                request.ClientContext.Locale = castInput.Locale;
                ApplyClientContextFromInput(context, castInput.ClientContext);
                return await DispatchDialogRequest(turnResult, request, traceLogger, cancelToken, realTime).ConfigureAwait(false);
            }
            else if (input is BasicSpeechInput)
            {
                BasicSpeechInput castInput = input as BasicSpeechInput;
                DialogRequest request = new DialogRequest();
                request.InteractionType = InputMethod.Spoken;
                context.AddCapabilities(
                    ClientCapabilities.HasSpeakers |
                    ClientCapabilities.HasMicrophone |
                    ClientCapabilities.SupportsStreamingAudio |
                    ClientCapabilities.SupportsCompressedAudio);
                request.PreferredAudioCodec = OpusRawCodecFactory.CODEC_NAME;
                request.ClientContext = context;
                request.RequestFlags = QueryFlags.Monitoring;
                request.SpeechInput = new SpeechRecognitionResult()
                {
                    ConfusionNetworkData = new ConfusionNetwork(),
                    RecognitionStatus = SpeechRecognitionStatus.Success,
                    RecognizedPhrases = new List<SpeechRecognizedPhrase>()
                    {
                        new SpeechRecognizedPhrase()
                        {
                            DisplayText = castInput.DisplayText,
                            LexicalForm = castInput.DisplayText,
                            IPASyllables = string.Empty,
                            Locale = castInput.Locale.ToBcp47Alpha2String(),
                            SREngineConfidence = 0.9f,
                            AudioTimeLength = TimeSpan.FromSeconds(3),
                            AudioTimeOffset = TimeSpan.FromMilliseconds(100),
                            InverseTextNormalizationResults = new List<string>(),
                            MaskedInverseTextNormalizationResults = new List<string>(),
                            PhraseElements = new List<SpeechPhraseElement>(),
                            ProfanityTags = new List<Tag>()
                        }
                    }
                };

                request.TraceId = CommonInstrumentation.FormatTraceId(traceId);
                request.ClientContext.Locale = castInput.Locale;
                ApplyClientContextFromInput(context, castInput.ClientContext);
                return await DispatchDialogRequest(turnResult, request, traceLogger, cancelToken, realTime).ConfigureAwait(false);
            }
            else if (input is ComplexSpeechInput)
            {
                ComplexSpeechInput castInput = input as ComplexSpeechInput;
                DialogRequest request = new DialogRequest();
                request.InteractionType = InputMethod.Spoken;
                context.AddCapabilities(
                    ClientCapabilities.HasSpeakers |
                    ClientCapabilities.HasMicrophone |
                    ClientCapabilities.SupportsStreamingAudio |
                    ClientCapabilities.SupportsCompressedAudio);
                request.PreferredAudioCodec = OpusRawCodecFactory.CODEC_NAME;
                request.ClientContext = context;
                request.RequestFlags = QueryFlags.Monitoring;
                request.SpeechInput = castInput.SpeechRecognitionResult;
                request.TraceId = CommonInstrumentation.FormatTraceId(traceId);
                request.ClientContext.Locale = LanguageCode.TryParse(castInput.SpeechRecognitionResult?.RecognizedPhrases?[0]?.Locale);
                ApplyClientContextFromInput(context, castInput.ClientContext);
                return await DispatchDialogRequest(turnResult, request, traceLogger, cancelToken, realTime).ConfigureAwait(false);
            }

            else if (input is ClientDialogActionInput)
            {
                ClientDialogActionInput castInput = input as ClientDialogActionInput;

                // Try and find the desired action in previous turn client actions
                int sourceTurn = castInput.SourceTurn.GetValueOrDefault(previousTurnResults.Count - 1);
                string clientActionString = previousTurnResults[sourceTurn].DialogResponse.ResponseAction;
                DialogActionInterceptingHandler interceptor = new DialogActionInterceptingHandler();
                using (JsonClientActionDispatcher fakeClientActionDispatcher = new JsonClientActionDispatcher())
                {
                    fakeClientActionDispatcher.AddHandler(interceptor);
                    await fakeClientActionDispatcher.InterpretClientAction(clientActionString, null, traceLogger, CancellationToken.None, realTime).ConfigureAwait(false);
                    ExecuteDelayedAction targetAction = interceptor.InterceptedAction;

                    DialogRequest request = new DialogRequest();
                    InputMethod interactionMethod;
                    if (!Enum.TryParse(targetAction.InteractionMethod, out interactionMethod))
                    {
                        return null;
                    }

                    request.InteractionType = interactionMethod;
                    context.AddCapabilities(
                        ClientCapabilities.HasSpeakers |
                        ClientCapabilities.HasMicrophone |
                        ClientCapabilities.SupportsStreamingAudio |
                        ClientCapabilities.SupportsCompressedAudio);
                    request.PreferredAudioCodec = OpusRawCodecFactory.CODEC_NAME;
                    request.ClientContext = context;
                    request.RequestFlags = QueryFlags.Monitoring;
                    request.TraceId = CommonInstrumentation.FormatTraceId(traceId);
                    request.ClientContext.Locale = castInput.Locale;
                    ApplyClientContextFromInput(context, castInput.ClientContext);
                    return await DispatchDialogRequest(turnResult, request, traceLogger, cancelToken, realTime, targetAction.ActionId).ConfigureAwait(false);
                }
            }

            turnResult.TurnEndTime = realTime.Time;
            return turnResult;
        }

        private static void ApplyClientContextFromInput(ClientContext targetContext, InputClientContext inputContextFromTest)
        {
            if (targetContext== null ||
                inputContextFromTest == null)
            {
                return;
            }

            targetContext.Latitude = inputContextFromTest.Latitude;
            targetContext.Longitude = inputContextFromTest.Longitude;
            targetContext.LocationAccuracy = inputContextFromTest.LocationAccuracy;
            targetContext.UserTimeZone = inputContextFromTest.TimeZone;
            if (inputContextFromTest.SupportedClientActions != null)
            {
                if (targetContext.SupportedClientActions == null)
                {
                    targetContext.SupportedClientActions = new HashSet<string>();
                }

                targetContext.SupportedClientActions.UnionWith(inputContextFromTest.SupportedClientActions);
            }
        }

        private async Task<FunctionalTestTurnResult> DispatchDialogRequest(
            FunctionalTestTurnResult turnResult,
            DialogRequest request,
            ILogger traceLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            string dialogActionId = null)
        {
            DateTimeOffset turnStartTime = realTime.Time;
            turnResult.DialogRequest = request;

            try
            {
                // Authenticate the request
                // TODO need to stash the private key of the test user/client into the local store if available
                Stopwatch timer = Stopwatch.StartNew();
                await _clientAuthenticator.AuthenticateClientRequest(request, traceLogger, realTime).ConfigureAwait(false);
                timer.Stop();
                traceLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Client_GenerateRequestToken, timer), LogLevel.Ins);

                NetworkResponseInstrumented<DialogResponse> netResp;
                if (!string.IsNullOrEmpty(dialogActionId))
                {
                    netResp = await _dialogClient.MakeDialogActionRequest(request, dialogActionId, traceLogger, cancelToken, realTime).ConfigureAwait(false);
                }
                else
                {
                    netResp = await _dialogClient.MakeQueryRequest(request, traceLogger, cancelToken, realTime).ConfigureAwait(false);
                }

                try
                {
                    if (netResp != null && netResp.Success)
                    {
                        DialogResponse response = netResp.Response;
                        turnResult.DialogResponse = response;
                        traceLogger.Log(CommonInstrumentation.GenerateSizeEntry(CommonInstrumentation.Key_Size_Client_Request, netResp.RequestSize), LogLevel.Ins);
                        traceLogger.Log(CommonInstrumentation.GenerateSizeEntry(CommonInstrumentation.Key_Size_Client_Response, netResp.ResponseSize), LogLevel.Ins);
                        traceLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Client_E2E, netResp.EndToEndLatency), LogLevel.Ins);

                        // Download streaming audio if available
                        if (!string.IsNullOrEmpty(response.StreamingAudioUrl))
                        {
                            using (RecyclableMemoryStream memStream = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
                            {
                                DateTimeOffset streamAudioStartTime = realTime.Time;
                                DateTimeOffset streamFirstByteTime = realTime.Time;
                                using (IAudioDataSource result = await _dialogClient.GetStreamingAudioResponse(
                                    response.StreamingAudioUrl, traceLogger, cancelToken, realTime).ConfigureAwait(false))
                                {
                                    bool firstByte = true;
                                    Stream responseStream = result.AudioDataReadStream;
                                    if (responseStream != null)
                                    {
                                        using (PooledBuffer<byte> pooledBuffer = BufferPool<byte>.Rent())
                                        {
                                            byte[] scratch = pooledBuffer.Buffer;
                                            int readSize = await responseStream.ReadAsync(scratch, 0, scratch.Length).ConfigureAwait(false);

                                            while (readSize > 0)
                                            {
                                                if (firstByte)
                                                {
                                                    streamFirstByteTime = realTime.Time;
                                                    traceLogger.Log(CommonInstrumentation.GenerateLatencyEntry(
                                                        CommonInstrumentation.Key_Latency_Client_StreamingAudioBeginRead, streamFirstByteTime - streamAudioStartTime), LogLevel.Ins);
                                                    firstByte = false;
                                                }

                                                memStream.Write(scratch, 0, readSize);
                                                readSize = await responseStream.ReadAsync(scratch, 0, scratch.Length).ConfigureAwait(false);
                                            }
                                        }
                                    }

                                    DateTimeOffset streamLastByteTime = realTime.Time;
                                    traceLogger.Log(CommonInstrumentation.GenerateSizeEntry(CommonInstrumentation.Key_Size_Client_StreamingAudioResponse, memStream.Length), LogLevel.Ins);
                                    traceLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Client_StreamingAudioRead, streamLastByteTime - streamFirstByteTime), LogLevel.Ins);
                                    traceLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Client_AudioUPL, streamFirstByteTime - turnStartTime), LogLevel.Ins);

                                    // Decode the audio stream
                                    if (_codecs.CanDecode(result.Codec))
                                    {
                                        turnResult.StreamingAudioResponsePcm = await AudioHelpers.DecodeAudioDataUsingCodec(result, _codecs, traceLogger).ConfigureAwait(false);
                                    }
                                    else
                                    {
                                        traceLogger.Log("Cannot decode output audio: Unexpected codec \"" + result.Codec + "\"", LogLevel.Err);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // TODO make an error case?
                    }
                }
                finally
                {
                    netResp?.Dispose();
                }
            }
            finally
            {
                turnResult.TurnEndTime = realTime.Time;
            }

            return turnResult;
        }

        private class DialogActionInterceptingHandler : IJsonClientActionHandler
        {
            public ISet<string> GetSupportedClientActions()
            {
                return new HashSet<string>() { ExecuteDelayedAction.ActionName };
            }

            public Task HandleAction(string actionName, JObject action, ILogger queryLogger, ClientCore source, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                InterceptedAction = action.ToObject<ExecuteDelayedAction>();
                return DurandalTaskExtensions.NoOpTask;
            }

            public ExecuteDelayedAction InterceptedAction { get; set; }
        }
    }
}
