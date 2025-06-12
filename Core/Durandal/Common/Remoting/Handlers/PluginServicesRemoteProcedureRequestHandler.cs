using Durandal.API;
using Durandal.Common.Audio;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.Audio.Components;
using Durandal.Common.Dialog.Services;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.NLP;
using Durandal.Common.NLP.Language;
using Durandal.Common.Remoting.Protocol;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Speech.SR;
using Durandal.Common.Statistics;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Remoting.Handlers
{
    /// <summary>
    /// Implements handlers for handling remoted dialog requests and translating them into calls to a local instance of IPluginServices
    /// </summary>
    public class PluginServicesRemoteProcedureRequestHandler : IRemoteProcedureRequestHandler
    {
        private readonly IPluginServices _targetServices;

        public PluginServicesRemoteProcedureRequestHandler(IPluginServices targetServices)
        {
            _targetServices = targetServices;
        }

        public bool CanHandleRequestType(Type requestType)
        {
            return requestType == typeof(RemoteSynthesizeSpeechRequest) ||
                requestType == typeof(RemoteRecognizeSpeechRequest) ||
                requestType == typeof(RemoteGetOAuthTokenRequest) ||
                requestType == typeof(RemoteCreateOAuthUriRequest) ||
                requestType == typeof(RemoteDeleteOAuthTokenRequest) ||
                requestType == typeof(RemoteResolveEntityRequest);
        }

        public Task HandleRequest(
            PostOffice postOffice,
            IRemoteDialogProtocol remoteProtocol,
            ILogger traceLogger,
            Tuple<object, Type> parsedMessage, 
            MailboxMessage originalMessage,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            TaskFactory taskFactory)
        {
            if (parsedMessage.Item2 == typeof(RemoteSynthesizeSpeechRequest))
            {
                // Synthesize speech request from plugin
                RemoteSynthesizeSpeechRequest parsedInterstitialRequest = parsedMessage.Item1 as RemoteSynthesizeSpeechRequest;
                traceLogger.Log("Execution guest is making a call to speech sythesis");
                ILogger speechSynthLogger = traceLogger.Clone("PluginSpeechSynth");
                IRealTimeProvider threadLocalTime = realTime.Fork("RemotedSpeechSynth");
                return taskFactory.StartNew(async () =>
                {
                    try
                    {
                        RemoteProcedureResponse<SynthesizedSpeech> speechResponse;
                        try
                        {
                            if (parsedInterstitialRequest.SynthRequest == null)
                            {
                                throw new NullReferenceException("Remoted synth request is null");
                            }

                            if (!_targetServices.TTSEngine.IsLocaleSupported(parsedInterstitialRequest.SynthRequest.Locale))
                            {
                                throw new NotSupportedException("Locale \"" + parsedInterstitialRequest.SynthRequest.Locale + "\" is not supported by this TTS engine");
                            }

                            SynthesizedSpeech speech = await _targetServices.TTSEngine.SynthesizeSpeechAsync(
                                parsedInterstitialRequest.SynthRequest,
                                cancelToken,
                                threadLocalTime,
                                speechSynthLogger).ConfigureAwait(false);
                            speechResponse = new RemoteProcedureResponse<SynthesizedSpeech>(parsedInterstitialRequest.MethodName, speech);
                        }
                        catch (Exception e)
                        {
                            speechResponse = new RemoteProcedureResponse<SynthesizedSpeech>(parsedInterstitialRequest.MethodName, e);
                        }

                        PooledBuffer<byte> serializedSynthSpeechResponse = remoteProtocol.Serialize(speechResponse, traceLogger);
                        MailboxMessage interstitialResponseMessage = new MailboxMessage(originalMessage.MailboxId, remoteProtocol.ProtocolId, serializedSynthSpeechResponse);
                        interstitialResponseMessage.MessageId = postOffice.GenerateMessageId();
                        interstitialResponseMessage.ReplyToId = originalMessage.MessageId;
                        await postOffice.SendMessage(interstitialResponseMessage, cancelToken, threadLocalTime).ConfigureAwait(false);
                    }
                    finally
                    {
                        threadLocalTime.Merge();
                    }
                });
            }
            else if (parsedMessage.Item2 == typeof(RemoteRecognizeSpeechRequest))
            {
                // Recognize speech request from plugin
                RemoteRecognizeSpeechRequest parsedInterstitialRequest = parsedMessage.Item1 as RemoteRecognizeSpeechRequest;
                traceLogger.Log("Execution guest is making a call to speech recognition");
                ILogger speechRecoLogger = traceLogger.Clone("PluginSpeechReco");

                IRealTimeProvider threadLocalTime = realTime.Fork("RemotedSpeechReco");
                return taskFactory.StartNew(async () =>
                {
                    try
                    {
                        RemoteProcedureResponse<SpeechRecognitionResult> speechResponse;

                        try
                        {
                            if (!_targetServices.SpeechRecoEngine.IsLocaleSupported(parsedInterstitialRequest.Locale))
                            {
                                throw new NotSupportedException("Locale \"" + parsedInterstitialRequest.Locale + "\" is not supported by this speech reco engine");
                            }
                            if (!string.Equals(parsedInterstitialRequest.Audio.Codec, RawPcmCodecFactory.CODEC_NAME_PCM_S16LE))
                            {
                                throw new NotSupportedException("Only PCM codec is supported for remote speech recognition right now");
                            }

                            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.None))
                            using (ISpeechRecognizer recognizer = await _targetServices.SpeechRecoEngine.CreateRecognitionStream(
                                new WeakPointer<IAudioGraph>(graph),
                                "RemotedSpeechRecognizer",
                                parsedInterstitialRequest.Locale,
                                speechRecoLogger,
                                cancelToken,
                                threadLocalTime).ConfigureAwait(false))
                            using (AudioDecoder decoder = new RawPcmDecoder(new WeakPointer<IAudioGraph>(graph), parsedInterstitialRequest.Audio.CodecParams, "RemotedSpeechRecoPcmDecoder"))
                            {
                                await decoder.Initialize(new NonRealTimeStreamWrapper(
                                    new MemoryStream(
                                        parsedInterstitialRequest.Audio.Data.Array,
                                        parsedInterstitialRequest.Audio.Data.Offset,
                                        parsedInterstitialRequest.Audio.Data.Count,
                                        false), true),
                                    true,
                                    cancelToken,
                                    threadLocalTime).ConfigureAwait(false);

                                AudioConformer conformer = new AudioConformer(
                                    new WeakPointer<IAudioGraph>(graph),
                                    decoder.OutputFormat,
                                    recognizer.InputFormat,
                                    "RemotedSpeechRecoConformer",
                                    speechRecoLogger,
                                    resamplerQuality: AudioProcessingQuality.Balanced);
                                decoder.TakeOwnershipOfDisposable(conformer);
                                decoder.ConnectOutput(conformer);
                                conformer.ConnectOutput(recognizer);

                                await decoder.ReadFully(cancelToken, threadLocalTime).ConfigureAwait(false);
                                SpeechRecognitionResult recoResult = await recognizer.FinishUnderstandSpeech(cancelToken, threadLocalTime).ConfigureAwait(false);
                                speechResponse = new RemoteProcedureResponse<SpeechRecognitionResult>(parsedInterstitialRequest.MethodName, recoResult);
                            }
                        }
                        catch (Exception e)
                        {
                            speechResponse = new RemoteProcedureResponse<SpeechRecognitionResult>(parsedInterstitialRequest.MethodName, e);
                        }

                        PooledBuffer<byte> serializedSynthSpeechResponse = remoteProtocol.Serialize(speechResponse, traceLogger);
                        MailboxMessage interstitialResponseMessage = new MailboxMessage(originalMessage.MailboxId, remoteProtocol.ProtocolId, serializedSynthSpeechResponse);
                        interstitialResponseMessage.MessageId = postOffice.GenerateMessageId();
                        interstitialResponseMessage.ReplyToId = originalMessage.MessageId;
                        await postOffice.SendMessage(interstitialResponseMessage, cancelToken, threadLocalTime).ConfigureAwait(false);
                    }
                    finally
                    {
                        threadLocalTime.Merge();
                    }
                });
            }
            else if (parsedMessage.Item2 == typeof(RemoteGetOAuthTokenRequest))
            {
                // Get oauth token request from plugin
                RemoteGetOAuthTokenRequest parsedInterstitialRequest = parsedMessage.Item1 as RemoteGetOAuthTokenRequest;
                traceLogger.Log("Execution guest is making a call to get oauth token");
                IRealTimeProvider threadLocalTime = realTime.Fork("RemotedOAuthGetTokenRequest");
                return taskFactory.StartNew(async () =>
                {
                    try
                    {
                        RemoteProcedureResponse<OAuthToken> tokenResponse;

                        try
                        {
                            OAuthToken responseToken = await _targetServices.TryGetOAuthToken(parsedInterstitialRequest.OAuthConfig, parsedInterstitialRequest.UserId, cancelToken, threadLocalTime).ConfigureAwait(false);
                            tokenResponse = new RemoteProcedureResponse<OAuthToken>(parsedInterstitialRequest.MethodName, responseToken);
                        }
                        catch (Exception e)
                        {
                            tokenResponse = new RemoteProcedureResponse<OAuthToken>(parsedInterstitialRequest.MethodName, e);
                        }

                        PooledBuffer<byte> serializedGetAuthTokenResponse = remoteProtocol.Serialize(tokenResponse, traceLogger);
                        MailboxMessage interstitialResponseMessage = new MailboxMessage(originalMessage.MailboxId, remoteProtocol.ProtocolId, serializedGetAuthTokenResponse);
                        interstitialResponseMessage.MessageId = postOffice.GenerateMessageId();
                        interstitialResponseMessage.ReplyToId = originalMessage.MessageId;
                        await postOffice.SendMessage(interstitialResponseMessage, cancelToken, threadLocalTime).ConfigureAwait(false);
                    }
                    finally
                    {
                        threadLocalTime.Merge();
                    }
                });
            }
            else if (parsedMessage.Item2 == typeof(RemoteCreateOAuthUriRequest))
            {
                // Create oauth URI request from plugin
                RemoteCreateOAuthUriRequest parsedInterstitialRequest = parsedMessage.Item1 as RemoteCreateOAuthUriRequest;
                traceLogger.Log("Execution guest is making a call to create oauth URI");
                IRealTimeProvider threadLocalTime = realTime.Fork("RemotedOAuthCreateUriRequest");
                return taskFactory.StartNew(async () =>
                {
                try
                {
                    RemoteProcedureResponse<string> uriResponse;

                    try
                    {
                        Uri createdUri = await _targetServices.CreateOAuthUri(parsedInterstitialRequest.OAuthConfig, parsedInterstitialRequest.UserId, cancelToken, threadLocalTime).ConfigureAwait(false);
                        uriResponse = new RemoteProcedureResponse<string>(parsedInterstitialRequest.MethodName, createdUri.AbsoluteUri);
                        }
                        catch (Exception e)
                        {
                            uriResponse = new RemoteProcedureResponse<string>(parsedInterstitialRequest.MethodName, e);
                        }

                        PooledBuffer<byte> serializedCreateUriResponse = remoteProtocol.Serialize(uriResponse, traceLogger);
                        MailboxMessage interstitialResponseMessage = new MailboxMessage(originalMessage.MailboxId, remoteProtocol.ProtocolId, serializedCreateUriResponse);
                        interstitialResponseMessage.MessageId = postOffice.GenerateMessageId();
                        interstitialResponseMessage.ReplyToId = originalMessage.MessageId;
                        await postOffice.SendMessage(interstitialResponseMessage, cancelToken, threadLocalTime).ConfigureAwait(false);
                    }
                    finally
                    {
                        threadLocalTime.Merge();
                    }
                });
            }
            else if (parsedMessage.Item2 == typeof(RemoteDeleteOAuthTokenRequest))
            {
                // Delete oauth token request from plugin
                RemoteDeleteOAuthTokenRequest parsedInterstitialRequest = parsedMessage.Item1 as RemoteDeleteOAuthTokenRequest;
                traceLogger.Log("Execution guest is making a call to delete oauth token");
                IRealTimeProvider threadLocalTime = realTime.Fork("RemotedOAuthDeleteTokenRequest");
                return taskFactory.StartNew(async () =>
                {
                    try
                    {
                        RemoteProcedureResponse<bool> successResponse;

                        try
                        {
                            await _targetServices.DeleteOAuthToken(parsedInterstitialRequest.OAuthConfig, parsedInterstitialRequest.UserId, cancelToken, threadLocalTime).ConfigureAwait(false);
                            successResponse = new RemoteProcedureResponse<bool>(parsedInterstitialRequest.MethodName, true);
                        }
                        catch (Exception e)
                        {
                            successResponse = new RemoteProcedureResponse<bool>(parsedInterstitialRequest.MethodName, e);
                        }

                        PooledBuffer<byte> serializedResponse = remoteProtocol.Serialize(successResponse, traceLogger);
                        MailboxMessage interstitialResponseMessage = new MailboxMessage(originalMessage.MailboxId, remoteProtocol.ProtocolId, serializedResponse);
                        interstitialResponseMessage.MessageId = postOffice.GenerateMessageId();
                        interstitialResponseMessage.ReplyToId = originalMessage.MessageId;
                        await postOffice.SendMessage(interstitialResponseMessage, cancelToken, threadLocalTime).ConfigureAwait(false);
                    }
                    finally
                    {
                        threadLocalTime.Merge();
                    }
                });
            }
            else if (parsedMessage.Item2 == typeof(RemoteResolveEntityRequest))
            {
                // Resolve entity request from plugin
                RemoteResolveEntityRequest parsedInterstitialRequest = parsedMessage.Item1 as RemoteResolveEntityRequest;
                traceLogger.Log("Execution guest is making a call to resolve entities");
                IRealTimeProvider threadLocalTime = realTime.Fork("RemotedEntityResolve");

                return taskFactory.StartNew(async () =>
                {
                    try
                    {
                        RemoteProcedureResponse<RemoteResolveEntityResponse> procedureResponse;

                        try
                        {
                            EventOnlyLogger resolutionLogger = new EventOnlyLogger("EntityResolution");
                            IList<NamedEntity<int>> convertedInput = new List<NamedEntity<int>>();
                            foreach (LexicalNamedEntity input in parsedInterstitialRequest.Possibilities)
                            {
                                convertedInput.Add(new NamedEntity<int>(input.Ordinal, input.KnownAs));
                            }

                            IList<Hypothesis<int>> resolutionResults = await _targetServices.EntityResolver.ResolveEntity<int>(
                                parsedInterstitialRequest.Input,
                                convertedInput,
                                LanguageCode.Parse(parsedInterstitialRequest.Locale),
                                resolutionLogger).ConfigureAwait(false);

                            InstrumentationEventList logEvents = new InstrumentationEventList();
                            foreach (LogEvent logEvent in resolutionLogger.History)
                            {
                                logEvents.Events.Add(InstrumentationEvent.FromLogEvent(logEvent));
                            }

                            RemoteResolveEntityResponse resolutionResponse = new RemoteResolveEntityResponse()
                            {
                                Hypotheses = (resolutionResults is List<Hypothesis<int>>) ? (resolutionResults as List<Hypothesis<int>>) : new List<Hypothesis<int>>(resolutionResults),
                                LogEvents = logEvents
                            };

                            procedureResponse = new RemoteProcedureResponse<RemoteResolveEntityResponse>(parsedInterstitialRequest.MethodName, resolutionResponse);
                        }
                        catch (Exception e)
                        {
                            procedureResponse = new RemoteProcedureResponse<RemoteResolveEntityResponse>(parsedInterstitialRequest.MethodName, e);
                        }

                        PooledBuffer<byte> serializedGetAuthTokenResponse = remoteProtocol.Serialize(procedureResponse, traceLogger);
                        MailboxMessage interstitialResponseMessage = new MailboxMessage(originalMessage.MailboxId, remoteProtocol.ProtocolId, serializedGetAuthTokenResponse);
                        interstitialResponseMessage.MessageId = postOffice.GenerateMessageId();
                        interstitialResponseMessage.ReplyToId = originalMessage.MessageId;
                        await postOffice.SendMessage(interstitialResponseMessage, cancelToken, threadLocalTime).ConfigureAwait(false);
                    }
                    finally
                    {
                        threadLocalTime.Merge();
                    }
                });
            }
            else
            {
                return DurandalTaskExtensions.NoOpTask;
            }
        }
    }
}
