using Durandal.API;
using Durandal.Common.Collections;
using Durandal.Common.File;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.NLP;
using Durandal.Common.NLP.Language;
using Durandal.Common.Remoting.Protocol;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Statistics;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Remoting
{
    /// <summary>
    /// This is a helper class that lives on the remote dialog server ("guest") side, which provides an adapter between remoted plugin services
    /// such as remoted logger, remoted speech synthesizer, etc. and the actual socket / protocol / callback multiplexer that routes requests and
    /// responses to the remote dialog host. The primary work done here is making sure that function requests and responses get associated with
    /// each other and that return values go to their proper places.
    /// </summary>
    public class RemoteDialogMethodDispatcher : IDisposable
    {
        private readonly FastConcurrentDictionary<uint, MailboxMessage> _activeCalls; // stash for callbacks that we need to pass across threads
        private readonly WeakPointer<PostOffice> _postOffice;
        private readonly MailboxId _mailbox;
        private readonly ILogger _serviceLogger;
        private readonly IRemoteDialogProtocol _remotingProtocol;
        //private readonly Queue<Task> _outgoingMessageQueue;
        //private readonly SemaphoreSlim _outgoingMessageLock;
        private readonly CancellationTokenSource _cancelTokenSource;
        private readonly CancellationToken _dispatcherClosingDownToken;
        private CarpoolAlgorithm<StubType> _carpoolAlgorithm;
        private int _disposed = 0;

        public RemoteDialogMethodDispatcher(
            PostOffice postOffice,
            MailboxId mailbox,
            ILogger serviceLogger,
            IRemoteDialogProtocol remotingProtocol)
        {
            _postOffice = new WeakPointer<PostOffice>(postOffice);
            _mailbox = mailbox;
            _serviceLogger = serviceLogger;
            _remotingProtocol = remotingProtocol;
            _activeCalls = new FastConcurrentDictionary<uint, MailboxMessage>();
            //_outgoingMessageQueue = new Queue<Task>();
            //_outgoingMessageLock = new SemaphoreSlim(1);
            _cancelTokenSource = new CancellationTokenSource();
            _dispatcherClosingDownToken = _cancelTokenSource.Token;
            _carpoolAlgorithm = new CarpoolAlgorithm<StubType>(ReadAndSortSingleMessage);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~RemoteDialogMethodDispatcher()
        {
            Dispose(false);
        }
#endif

        public void Stop()
        {
            //await FlushOutgoingMessages().ConfigureAwait(false);
            _cancelTokenSource.Cancel();
        }

        public async Task Logger_Log(InstrumentationEventList value, IRealTimeProvider realTime)
        {
            RemoteLogMessageRequest remoteRequest = new RemoteLogMessageRequest()
            {
                LogEvents = value
            };

            PooledBuffer<byte> serializedRequest = _remotingProtocol.Serialize(remoteRequest, _serviceLogger);
            // Best-effort logging. We don't care about the callback, which limits error handling but improves performance
            await SendMessageIgnoreCallback(new MailboxMessage(_mailbox, _remotingProtocol.ProtocolId, serializedRequest), realTime).ConfigureAwait(false);

            // More reliable implementation
            //uint messageId;
            //using (BufferedChannel<MailboxMessage> responseChannel = QueueOutgoingMessageWithCallback(new MailboxMessage(_mailbox, _remotingProtocol.ProtocolId, serializedRequest), out messageId))
            //{
            //_serviceLogger.Log(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
            //    "Queued remoted {0} request {1} with message ID {2}",
            //    nameof(Logger_Log), remoteRequest.MethodName, messageId);
            //    MailboxMessage response = await responseChannel.ReceiveAsync(realTime, _cancelToken);

            //    Tuple<object, Type> parsedResponse = _remotingProtocol.Parse(response.Payload, _serviceLogger);
            //    if (parsedResponse.Item2 != typeof(RemoteProcedureResponse<bool>))
            //    {
            //        throw new Exception("Can't parse log message response");
            //    }

            //    RemoteProcedureResponse<bool> finalResponse = parsedResponse.Item1 as RemoteProcedureResponse<bool>;

            //    finalResponse.RaiseExceptionIfPresent();
            //}
        }

        public async Task Metric_Upload(SerializedMetricEventList value, IRealTimeProvider realTime, CancellationToken cancelToken)
        {
            using (CancellationTokenSource mergedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, _dispatcherClosingDownToken))
            {
                CancellationToken operationCancelToken = mergedCts.Token;
                RemoteUploadMetricsRequest remoteRequest = new RemoteUploadMetricsRequest()
                {
                    Metrics = value
                };

                PooledBuffer<byte> serializedRequest = _remotingProtocol.Serialize(remoteRequest, _serviceLogger);
                uint messageId = await SendMessageWithCallback(new MailboxMessage(_mailbox, _remotingProtocol.ProtocolId, serializedRequest), realTime).ConfigureAwait(false);
                _serviceLogger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                    "Queued remoted {0} request {1} with message ID {2}",
                    nameof(Metric_Upload), remoteRequest.MethodName, messageId);
                MailboxMessage response = await ReadReplyMessageFromPostOffice(messageId, operationCancelToken, realTime).ConfigureAwait(false);

                Tuple<object, Type> parsedResponse = _remotingProtocol.Parse(response.Buffer, _serviceLogger);
                if (parsedResponse.Item2 != typeof(RemoteProcedureResponse<bool>))
                {
                    throw new Exception("Can't parse metric upload response");
                }

                RemoteProcedureResponse<bool> finalResponse = parsedResponse.Item1 as RemoteProcedureResponse<bool>;

                finalResponse.RaiseExceptionIfPresent();
            }
        }

        public async Task<SynthesizedSpeech> SpeechSynth_Synthesize(SpeechSynthesisRequest request, IRealTimeProvider realTime, CancellationToken cancelToken)
        {
            using (CancellationTokenSource mergedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, _dispatcherClosingDownToken))
            {
                CancellationToken operationCancelToken = mergedCts.Token;
                RemoteSynthesizeSpeechRequest remoteRequest = new RemoteSynthesizeSpeechRequest()
                {
                    SynthRequest = request
                };

                PooledBuffer<byte> serializedRequest = _remotingProtocol.Serialize(remoteRequest, _serviceLogger);
                uint messageId = await SendMessageWithCallback(new MailboxMessage(_mailbox, _remotingProtocol.ProtocolId, serializedRequest), realTime).ConfigureAwait(false);
                _serviceLogger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                    "Queued remoted {0} request {1} with message ID {2}",
                    nameof(SpeechSynth_Synthesize), remoteRequest.MethodName, messageId);
                MailboxMessage response = await ReadReplyMessageFromPostOffice(messageId, operationCancelToken, realTime).ConfigureAwait(false);

                Tuple<object, Type> parsedResponse = _remotingProtocol.Parse(response.Buffer, _serviceLogger);
                if (parsedResponse.Item2 != typeof(RemoteProcedureResponse<SynthesizedSpeech>))
                {
                    throw new Exception("Can't parse speech synth response");
                }

                RemoteProcedureResponse<SynthesizedSpeech> finalResponse = parsedResponse.Item1 as RemoteProcedureResponse<SynthesizedSpeech>;

                finalResponse.RaiseExceptionIfPresent();
                return finalResponse.ReturnVal;
            }
        }

        public async Task<SpeechRecognitionResult> SpeechReco_Recognize(LanguageCode locale, AudioData audioData, IRealTimeProvider realTime, CancellationToken cancelToken)
        {
            using (CancellationTokenSource mergedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, _dispatcherClosingDownToken))
            {
                CancellationToken operationCancelToken = mergedCts.Token;
                RemoteRecognizeSpeechRequest remoteRequest = new RemoteRecognizeSpeechRequest()
                {
                    Locale = locale,
                    Audio = audioData
                };

                PooledBuffer<byte> serializedRequest = _remotingProtocol.Serialize(remoteRequest, _serviceLogger);
                uint messageId = await SendMessageWithCallback(new MailboxMessage(_mailbox, _remotingProtocol.ProtocolId, serializedRequest), realTime).ConfigureAwait(false);
                _serviceLogger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                    "Queued remoted {0} request {1} with message ID {2}",
                    nameof(SpeechReco_Recognize), remoteRequest.MethodName, messageId);
                MailboxMessage response = await ReadReplyMessageFromPostOffice(messageId, operationCancelToken, realTime).ConfigureAwait(false);

                Tuple<object, Type> parsedResponse = _remotingProtocol.Parse(response.Buffer, _serviceLogger);
                if (parsedResponse.Item2 != typeof(RemoteProcedureResponse<SpeechRecognitionResult>))
                {
                    throw new Exception("Can't parse speech reco response");
                }

                RemoteProcedureResponse<SpeechRecognitionResult> finalResponse = parsedResponse.Item1 as RemoteProcedureResponse<SpeechRecognitionResult>;

                finalResponse.RaiseExceptionIfPresent();
                return finalResponse.ReturnVal;
            }
        }

        public async Task<OAuthToken> OAuth_GetToken(string durandalUserId, PluginStrongName owningPlugin, OAuthConfig config, IRealTimeProvider realTime, CancellationToken cancelToken)
        {
            using (CancellationTokenSource mergedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, _dispatcherClosingDownToken))
            {
                CancellationToken operationCancelToken = mergedCts.Token;
                RemoteGetOAuthTokenRequest remoteRequest = new RemoteGetOAuthTokenRequest()
                {
                    OAuthConfig = config,
                    PluginId = owningPlugin,
                    UserId = durandalUserId
                };

                PooledBuffer<byte> serializedRequest = _remotingProtocol.Serialize(remoteRequest, _serviceLogger);
                uint messageId = await SendMessageWithCallback(new MailboxMessage(_mailbox, _remotingProtocol.ProtocolId, serializedRequest), realTime).ConfigureAwait(false);
                _serviceLogger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                    "Queued remoted {0} request {1} with message ID {2}",
                    nameof(OAuth_GetToken), remoteRequest.MethodName, messageId);
                MailboxMessage response = await ReadReplyMessageFromPostOffice(messageId, operationCancelToken, realTime).ConfigureAwait(false);

                Tuple<object, Type> parsedResponse = _remotingProtocol.Parse(response.Buffer, _serviceLogger);
                if (parsedResponse.Item2 != typeof(RemoteProcedureResponse<OAuthToken>))
                {
                    throw new Exception("Can't parse get oauth token response");
                }

                RemoteProcedureResponse<OAuthToken> finalResponse = parsedResponse.Item1 as RemoteProcedureResponse<OAuthToken>;

                finalResponse.RaiseExceptionIfPresent();
                return finalResponse.ReturnVal;
            }
        }

        public async Task<Uri> OAuth_CreateAuthUri(string durandalUserId, PluginStrongName owningPlugin, OAuthConfig config, IRealTimeProvider realTime, CancellationToken cancelToken)
        {
            using (CancellationTokenSource mergedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, _dispatcherClosingDownToken))
            {
                CancellationToken operationCancelToken = mergedCts.Token;
                RemoteCreateOAuthUriRequest remoteRequest = new RemoteCreateOAuthUriRequest()
                {
                    OAuthConfig = config,
                    PluginId = owningPlugin,
                    UserId = durandalUserId
                };

                PooledBuffer<byte> serializedRequest = _remotingProtocol.Serialize(remoteRequest, _serviceLogger);
                uint messageId = await SendMessageWithCallback(new MailboxMessage(_mailbox, _remotingProtocol.ProtocolId, serializedRequest), realTime).ConfigureAwait(false);
                _serviceLogger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                    "Queued remoted {0} request {1} with message ID {2}",
                    nameof(OAuth_CreateAuthUri), remoteRequest.MethodName, messageId);
                MailboxMessage response = await ReadReplyMessageFromPostOffice(messageId, operationCancelToken, realTime).ConfigureAwait(false);

                Tuple<object, Type> parsedResponse = _remotingProtocol.Parse(response.Buffer, _serviceLogger);
                if (parsedResponse.Item2 != typeof(RemoteProcedureResponse<string>))
                {
                    throw new Exception("Can't parse get oauth token response");
                }

                RemoteProcedureResponse<string> finalResponse = parsedResponse.Item1 as RemoteProcedureResponse<string>;

                finalResponse.RaiseExceptionIfPresent();
                return new Uri(finalResponse.ReturnVal);
            }
        }

        public async Task OAuth_DeleteToken(string durandalUserId, PluginStrongName owningPlugin, OAuthConfig config, IRealTimeProvider realTime, CancellationToken cancelToken)
        {
            using (CancellationTokenSource mergedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, _dispatcherClosingDownToken))
            {
                CancellationToken operationCancelToken = mergedCts.Token;
                RemoteDeleteOAuthTokenRequest remoteRequest = new RemoteDeleteOAuthTokenRequest()
                {
                    OAuthConfig = config,
                    PluginId = owningPlugin,
                    UserId = durandalUserId
                };

                PooledBuffer<byte> serializedRequest = _remotingProtocol.Serialize(remoteRequest, _serviceLogger);
                uint messageId = await SendMessageWithCallback(new MailboxMessage(_mailbox, _remotingProtocol.ProtocolId, serializedRequest), realTime).ConfigureAwait(false);
                _serviceLogger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                    "Queued remoted {0} request {1} with message ID {2}",
                    nameof(OAuth_DeleteToken), remoteRequest.MethodName, messageId);
                MailboxMessage response = await ReadReplyMessageFromPostOffice(messageId, operationCancelToken, realTime).ConfigureAwait(false);

                Tuple<object, Type> parsedResponse = _remotingProtocol.Parse(response.Buffer, _serviceLogger);
                if (parsedResponse.Item2 != typeof(RemoteProcedureResponse<bool>))
                {
                    throw new Exception("Can't parse delete oauth token response");
                }

                RemoteProcedureResponse<bool> finalResponse = parsedResponse.Item1 as RemoteProcedureResponse<bool>;
                finalResponse.RaiseExceptionIfPresent();
            }
        }

        public async Task<IList<Hypothesis<T>>> Utility_ResolveEntity<T>(
            LexicalString input,
            IList<NamedEntity<T>> possibleValues,
            LanguageCode locale,
            ILogger traceLogger,
            IRealTimeProvider realTime,
            CancellationToken cancelToken)
        {
            using (CancellationTokenSource mergedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, _dispatcherClosingDownToken))
            {
                CancellationToken operationCancelToken = mergedCts.Token;
                // Build a resolver request
                RemoteResolveEntityRequest remoteRequest = new RemoteResolveEntityRequest()
                {
                    Input = input,
                    Locale = locale.ToBcp47Alpha2String(),
                    Possibilities = new List<LexicalNamedEntity>()
                };

                // Convert typed input to ordinals
                for (int ordinal = 0; ordinal < possibleValues.Count; ordinal++)
                {
                    remoteRequest.Possibilities.Add(new LexicalNamedEntity(ordinal, possibleValues[ordinal].KnownAs));
                }

                PooledBuffer<byte> serializedRequest = _remotingProtocol.Serialize(remoteRequest, _serviceLogger);
                uint messageId = await SendMessageWithCallback(new MailboxMessage(_mailbox, _remotingProtocol.ProtocolId, serializedRequest), realTime).ConfigureAwait(false);
                _serviceLogger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                    "Queued remoted {0} request {1} with message ID {2}",
                    nameof(Utility_ResolveEntity), remoteRequest.MethodName, messageId);
                MailboxMessage response = await ReadReplyMessageFromPostOffice(messageId, operationCancelToken, realTime).ConfigureAwait(false);

                Tuple<object, Type> parsedResponse = _remotingProtocol.Parse(response.Buffer, _serviceLogger);
                if (parsedResponse.Item2 != typeof(RemoteProcedureResponse<RemoteResolveEntityResponse>))
                {
                    throw new Exception("Can't parse resolve entity response");
                }

                RemoteProcedureResponse<RemoteResolveEntityResponse> finalResponse = parsedResponse.Item1 as RemoteProcedureResponse<RemoteResolveEntityResponse>;

                finalResponse.RaiseExceptionIfPresent();
                IList<Hypothesis<T>> returnVal = new List<Hypothesis<T>>();
                if (finalResponse.ReturnVal != null)
                {
                    // Convert responses back
                    foreach (Hypothesis<int> ordinalHyp in finalResponse.ReturnVal.Hypotheses)
                    {
                        returnVal.Add(new Hypothesis<T>(possibleValues[ordinalHyp.Value].Handle, ordinalHyp.Conf));
                    }

                    // And log remote messages that came from the service
                    if (finalResponse.ReturnVal.LogEvents != null)
                    {
                        foreach (InstrumentationEvent logEvent in finalResponse.ReturnVal.LogEvents.Events)
                        {
                            LogEvent convertedEvent = logEvent.ToLogEvent();
                            convertedEvent.TraceId = traceLogger.TraceId;
                            traceLogger.Log(convertedEvent);
                        }
                    }
                }

                return returnVal;
            }
        }

        public async Task<ArraySegment<byte>> File_ReadContents(VirtualPath filePath, IRealTimeProvider realTime, CancellationToken cancelToken)
        {
            using (CancellationTokenSource mergedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, _dispatcherClosingDownToken))
            {
                CancellationToken operationCancelToken = mergedCts.Token;
                RemoteFileReadContentsRequest remoteRequest = new RemoteFileReadContentsRequest()
                {
                    FilePath = filePath.FullName
                };

                PooledBuffer<byte> serializedRequest = _remotingProtocol.Serialize(remoteRequest, _serviceLogger);
                uint messageId = await SendMessageWithCallback(new MailboxMessage(_mailbox, _remotingProtocol.ProtocolId, serializedRequest), realTime).ConfigureAwait(false);
                _serviceLogger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                    "Queued remoted {0} request {1} with message ID {2}",
                    nameof(File_ReadContents), remoteRequest.MethodName, messageId);
                MailboxMessage response = await ReadReplyMessageFromPostOffice(messageId, operationCancelToken, realTime).ConfigureAwait(false);

                Tuple<object, Type> parsedResponse = _remotingProtocol.Parse(response.Buffer, _serviceLogger);
                if (parsedResponse.Item2 != typeof(RemoteProcedureResponse<ArraySegment<byte>>))
                {
                    throw new Exception("Can't parse file read response");
                }

                RemoteProcedureResponse<ArraySegment<byte>> finalResponse = parsedResponse.Item1 as RemoteProcedureResponse<ArraySegment<byte>>;
                finalResponse.RaiseExceptionIfPresent();
                return finalResponse.ReturnVal;
            }
        }
        
        public async Task File_WriteContents(VirtualPath filePath, ArraySegment<byte> newContents, IRealTimeProvider realTime, CancellationToken cancelToken)
        {
            using (CancellationTokenSource mergedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, _dispatcherClosingDownToken))
            {
                CancellationToken operationCancelToken = mergedCts.Token;
                RemoteFileWriteContentsRequest remoteRequest = new RemoteFileWriteContentsRequest()
                {
                    FilePath = filePath.FullName,
                    NewContents = newContents
                };

                PooledBuffer<byte> serializedRequest = _remotingProtocol.Serialize(remoteRequest, _serviceLogger);
                uint messageId = await SendMessageWithCallback(new MailboxMessage(_mailbox, _remotingProtocol.ProtocolId, serializedRequest), realTime).ConfigureAwait(false);
                _serviceLogger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                    "Queued remoted {0} request {1} with message ID {2}",
                    nameof(File_WriteContents), remoteRequest.MethodName, messageId);
                MailboxMessage response = await ReadReplyMessageFromPostOffice(messageId, operationCancelToken, realTime).ConfigureAwait(false);

                Tuple<object, Type> parsedResponse = _remotingProtocol.Parse(response.Buffer, _serviceLogger);
                if (parsedResponse.Item2 != typeof(RemoteProcedureResponse<bool>))
                {
                    throw new Exception("Can't parse file write response");
                }

                RemoteProcedureResponse<bool> finalResponse = parsedResponse.Item1 as RemoteProcedureResponse<bool>;
                finalResponse.RaiseExceptionIfPresent();
            }
        }

        public async Task<IList<VirtualPath>> File_List(VirtualPath filePath, bool listDirectories, IRealTimeProvider realTime, CancellationToken cancelToken)
        {
            using (CancellationTokenSource mergedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, _dispatcherClosingDownToken))
            {
                CancellationToken operationCancelToken = mergedCts.Token;
                RemoteFileListRequest remoteRequest = new RemoteFileListRequest()
                {
                    SourcePath = filePath.FullName,
                    ListDirectories = listDirectories
                };

                PooledBuffer<byte> serializedRequest = _remotingProtocol.Serialize(remoteRequest, _serviceLogger);
                uint messageId = await SendMessageWithCallback(new MailboxMessage(_mailbox, _remotingProtocol.ProtocolId, serializedRequest), realTime).ConfigureAwait(false);
                _serviceLogger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                    "Queued remoted {0} request {1} with message ID {2}",
                    nameof(File_List), remoteRequest.MethodName, messageId);
                MailboxMessage response = await ReadReplyMessageFromPostOffice(messageId, operationCancelToken, realTime).ConfigureAwait(false);

                Tuple<object, Type> parsedResponse = _remotingProtocol.Parse(response.Buffer, _serviceLogger);
                if (parsedResponse.Item2 != typeof(RemoteProcedureResponse<List<string>>))
                {
                    throw new Exception("Can't parse file list response");
                }

                RemoteProcedureResponse<List<string>> finalResponse = parsedResponse.Item1 as RemoteProcedureResponse<List<string>>;

                finalResponse.RaiseExceptionIfPresent();
                List<VirtualPath> returnVal = new List<VirtualPath>();
                foreach (string path in finalResponse.ReturnVal)
                {
                    returnVal.Add(new VirtualPath(path));
                }

                return returnVal;
            }
        }

        public async Task<RemoteFileStat> File_Stat(VirtualPath filePath, IRealTimeProvider realTime, CancellationToken cancelToken)
        {
            using (CancellationTokenSource mergedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, _dispatcherClosingDownToken))
            {
                CancellationToken operationCancelToken = mergedCts.Token;
                RemoteFileStatRequest remoteRequest = new RemoteFileStatRequest()
                {
                    TargetPath = filePath.FullName
                };

                PooledBuffer<byte> serializedRequest = _remotingProtocol.Serialize(remoteRequest, _serviceLogger);
                uint messageId = await SendMessageWithCallback(new MailboxMessage(_mailbox, _remotingProtocol.ProtocolId, serializedRequest), realTime).ConfigureAwait(false);
                _serviceLogger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                    "Queued remoted {0} request {1} with message ID {2}",
                    nameof(File_Stat), remoteRequest.MethodName, messageId);
                MailboxMessage response = await ReadReplyMessageFromPostOffice(messageId, operationCancelToken, realTime).ConfigureAwait(false);

                Tuple<object, Type> parsedResponse = _remotingProtocol.Parse(response.Buffer, _serviceLogger);
                if (parsedResponse.Item2 != typeof(RemoteProcedureResponse<RemoteFileStat>))
                {
                    throw new Exception("Can't parse file stat response");
                }

                RemoteProcedureResponse<RemoteFileStat> finalResponse = parsedResponse.Item1 as RemoteProcedureResponse<RemoteFileStat>;

                finalResponse.RaiseExceptionIfPresent();
                return finalResponse.ReturnVal;
            }
        }

        public async Task File_WriteStat(VirtualPath filePath, IRealTimeProvider realTime, DateTimeOffset? newCreationTime, DateTimeOffset? newModificationTime, CancellationToken cancelToken)
        {
            using (CancellationTokenSource mergedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, _dispatcherClosingDownToken))
            {
                CancellationToken operationCancelToken = mergedCts.Token;
                RemoteFileWriteStatRequest remoteRequest = new RemoteFileWriteStatRequest()
                {
                    TargetPath = filePath.FullName,
                    NewCreationTime = newCreationTime,
                    NewModificationTime = newModificationTime,
                };

                PooledBuffer<byte> serializedRequest = _remotingProtocol.Serialize(remoteRequest, _serviceLogger);
                uint messageId = await SendMessageWithCallback(new MailboxMessage(_mailbox, _remotingProtocol.ProtocolId, serializedRequest), realTime).ConfigureAwait(false);
                _serviceLogger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                    "Queued remoted {0} request {1} with message ID {2}",
                    nameof(File_WriteStat), remoteRequest.MethodName, messageId);
                MailboxMessage response = await ReadReplyMessageFromPostOffice(messageId, operationCancelToken, realTime).ConfigureAwait(false);

                Tuple<object, Type> parsedResponse = _remotingProtocol.Parse(response.Buffer, _serviceLogger);
                if (parsedResponse.Item2 != typeof(RemoteProcedureResponse<bool>))
                {
                    throw new Exception("Can't parse file write stat response");
                }

                RemoteProcedureResponse<bool> finalResponse = parsedResponse.Item1 as RemoteProcedureResponse<bool>;

                finalResponse.RaiseExceptionIfPresent();
            }
        }

        public async Task File_Move(VirtualPath sourcePath, VirtualPath targetPath, IRealTimeProvider realTime, CancellationToken cancelToken)
        {
            using (CancellationTokenSource mergedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, _dispatcherClosingDownToken))
            {
                CancellationToken operationCancelToken = mergedCts.Token;
                RemoteFileMoveRequest remoteRequest = new RemoteFileMoveRequest()
                {
                    SourcePath = sourcePath.FullName,
                    TargetPath = targetPath.FullName
                };

                PooledBuffer<byte> serializedRequest = _remotingProtocol.Serialize(remoteRequest, _serviceLogger);
                uint messageId = await SendMessageWithCallback(new MailboxMessage(_mailbox, _remotingProtocol.ProtocolId, serializedRequest), realTime).ConfigureAwait(false);
                _serviceLogger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                    "Queued remoted {0} request {1} with message ID {2}",
                    nameof(File_Move), remoteRequest.MethodName, messageId);
                MailboxMessage response = await ReadReplyMessageFromPostOffice(messageId, operationCancelToken, realTime).ConfigureAwait(false);

                Tuple<object, Type> parsedResponse = _remotingProtocol.Parse(response.Buffer, _serviceLogger);
                if (parsedResponse.Item2 != typeof(RemoteProcedureResponse<bool>))
                {
                    throw new Exception("Can't parse file move response");
                }

                RemoteProcedureResponse<bool> finalResponse = parsedResponse.Item1 as RemoteProcedureResponse<bool>;

                finalResponse.RaiseExceptionIfPresent();
            }
        }
        
        public async Task<bool> File_Delete(VirtualPath path, IRealTimeProvider realTime, CancellationToken cancelToken)
        {
            using (CancellationTokenSource mergedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, _dispatcherClosingDownToken))
            {
                CancellationToken operationCancelToken = mergedCts.Token;
                RemoteFileDeleteRequest remoteRequest = new RemoteFileDeleteRequest()
                {
                    TargetPath = path.FullName
                };

                PooledBuffer<byte> serializedRequest = _remotingProtocol.Serialize(remoteRequest, _serviceLogger);
                uint messageId = await SendMessageWithCallback(new MailboxMessage(_mailbox, _remotingProtocol.ProtocolId, serializedRequest), realTime).ConfigureAwait(false);
                _serviceLogger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                    "Queued remoted {0} request {1} with message ID {2}",
                    nameof(File_Delete), remoteRequest.MethodName, messageId);
                MailboxMessage response = await ReadReplyMessageFromPostOffice(messageId, operationCancelToken, realTime).ConfigureAwait(false);

                Tuple<object, Type> parsedResponse = _remotingProtocol.Parse(response.Buffer, _serviceLogger);
                if (parsedResponse.Item2 != typeof(RemoteProcedureResponse<bool>))
                {
                    throw new Exception("Can't parse file delete response");
                }

                RemoteProcedureResponse<bool> finalResponse = parsedResponse.Item1 as RemoteProcedureResponse<bool>;

                finalResponse.RaiseExceptionIfPresent();
                return finalResponse.ReturnVal;
            }
        }

        public async Task File_CreateDirectory(VirtualPath path, IRealTimeProvider realTime, CancellationToken cancelToken)
        {
            using (CancellationTokenSource mergedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, _dispatcherClosingDownToken))
            {
                CancellationToken operationCancelToken = mergedCts.Token;
                RemoteFileCreateDirectoryRequest remoteRequest = new RemoteFileCreateDirectoryRequest()
                {
                    DirectoryPath = path.FullName
                };

                PooledBuffer<byte> serializedRequest = _remotingProtocol.Serialize(remoteRequest, _serviceLogger);
                uint messageId = await SendMessageWithCallback(new MailboxMessage(_mailbox, _remotingProtocol.ProtocolId, serializedRequest), realTime).ConfigureAwait(false);
                _serviceLogger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                    "Queued remoted {0} request {1} with message ID {2}",
                    nameof(File_CreateDirectory), remoteRequest.MethodName, messageId);
                MailboxMessage response = await ReadReplyMessageFromPostOffice(messageId, operationCancelToken, realTime).ConfigureAwait(false);

                Tuple<object, Type> parsedResponse = _remotingProtocol.Parse(response.Buffer, _serviceLogger);
                if (parsedResponse.Item2 != typeof(RemoteProcedureResponse<bool>))
                {
                    throw new Exception("Can't parse file create directory response");
                }

                RemoteProcedureResponse<bool> finalResponse = parsedResponse.Item1 as RemoteProcedureResponse<bool>;

                finalResponse.RaiseExceptionIfPresent();
            }
        }

        public async Task<RemoteFileStreamOpenResult> FileStream_Open(
            VirtualPath filePath,
            FileOpenMode openMode,
            FileAccessMode accessMode,
            FileShareMode shareMode,
            IRealTimeProvider realTime,
            CancellationToken cancelToken)
        {
            using (CancellationTokenSource mergedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, _dispatcherClosingDownToken))
            {
                CancellationToken operationCancelToken = mergedCts.Token;
                RemoteFileStreamOpenRequest remoteRequest = new RemoteFileStreamOpenRequest()
                {
                    FilePath = filePath.FullName,
                    OpenMode = Convert(openMode),
                    AccessMode = Convert(accessMode),
                    ShareMode = Convert(shareMode),
                };

                PooledBuffer<byte> serializedRequest = _remotingProtocol.Serialize(remoteRequest, _serviceLogger);
                uint messageId = await SendMessageWithCallback(new MailboxMessage(_mailbox, _remotingProtocol.ProtocolId, serializedRequest), realTime).ConfigureAwait(false);
                _serviceLogger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                    "Queued remoted {0} request {1} with message ID {2}",
                    nameof(FileStream_Open), remoteRequest.MethodName, messageId);
                MailboxMessage response = await ReadReplyMessageFromPostOffice(messageId, operationCancelToken, realTime).ConfigureAwait(false);

                Tuple<object, Type> parsedResponse = _remotingProtocol.Parse(response.Buffer, _serviceLogger);
                if (parsedResponse.Item2 != typeof(RemoteProcedureResponse<RemoteFileStreamOpenResult>))
                {
                    throw new Exception("Can't parse file stream open response");
                }

                RemoteProcedureResponse<RemoteFileStreamOpenResult> finalResponse = parsedResponse.Item1 as RemoteProcedureResponse<RemoteFileStreamOpenResult>;

                finalResponse.RaiseExceptionIfPresent();
                return finalResponse.ReturnVal;
            }
        }

        public async Task FileStream_Close(
            string streamId,
            IRealTimeProvider realTime,
            CancellationToken cancelToken)
        {
            using (CancellationTokenSource mergedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, _dispatcherClosingDownToken))
            {
                CancellationToken operationCancelToken = mergedCts.Token;
                RemoteFileStreamCloseRequest remoteRequest = new RemoteFileStreamCloseRequest()
                {
                    StreamId = streamId,
                };
                PooledBuffer<byte> serializedRequest = _remotingProtocol.Serialize(remoteRequest, _serviceLogger);
                uint messageId = await SendMessageWithCallback(new MailboxMessage(_mailbox, _remotingProtocol.ProtocolId, serializedRequest), realTime).ConfigureAwait(false);
                _serviceLogger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                    "Queued remoted {0} request {1} with message ID {2}",
                    nameof(FileStream_Close), remoteRequest.MethodName, messageId);

                MailboxMessage response = await ReadReplyMessageFromPostOffice(messageId, operationCancelToken, realTime).ConfigureAwait(false);

                Tuple<object, Type> parsedResponse = _remotingProtocol.Parse(response.Buffer, _serviceLogger);
                if (parsedResponse.Item2 != typeof(RemoteProcedureResponse<bool>))
                {
                    throw new Exception("Can't parse file stream close response");
                }

                RemoteProcedureResponse<bool> finalResponse = parsedResponse.Item1 as RemoteProcedureResponse<bool>;
                finalResponse.RaiseExceptionIfPresent();
            }
        }

        /// <summary>
        /// This method returned a nested Task by design. The first Task is for writing the message to the wire. The second Task is
        /// waiting for the response acknowledgment. It's nested this way so that the caller can await the first task to ensure that
        /// outgoing messages are in sequential order, but then it can wait for the reply asynchronously as a separate operation.
        /// </summary>
        /// <param name="streamId"></param>
        /// <param name="filePosition"></param>
        /// <param name="data"></param>
        /// <param name="realTime"></param>
        /// <returns></returns>
        public async Task<Task<bool>> FileStream_Write(
            string streamId,
            long filePosition,
            ArraySegment<byte> data,
            IRealTimeProvider realTime,
            CancellationToken cancelToken)
        {
            using (CancellationTokenSource mergedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, _dispatcherClosingDownToken))
            {
                CancellationToken operationCancelToken = mergedCts.Token;
                RemoteFileStreamWriteRequest remoteRequest = new RemoteFileStreamWriteRequest()
                {
                    StreamId = streamId.AssertNonNull(nameof(streamId)),
                    Position = filePosition.AssertNonNegative(nameof(filePosition)),
                    Data = data.AssertNonNull(nameof(data)),
                };

                PooledBuffer<byte> serializedRequest = _remotingProtocol.Serialize(remoteRequest, _serviceLogger);
                uint messageId = await SendMessageWithCallback(new MailboxMessage(_mailbox, _remotingProtocol.ProtocolId, serializedRequest), realTime).ConfigureAwait(false);
                _serviceLogger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                    "Queued remoted {0} request {1} with message ID {2}",
                    nameof(FileStream_Write), remoteRequest.MethodName, messageId);

                ReadAsyncResponseClosure<bool> returnTaskClosure = new ReadAsyncResponseClosure<bool>(
                    this,
                    _remotingProtocol,
                    _serviceLogger,
                    messageId,
                    operationCancelToken,
                    realTime.Fork("RemoteFileWrite"));
                return returnTaskClosure.Run();
            }
        }

        private class ReadAsyncResponseClosure<T>
        {
            private readonly RemoteDialogMethodDispatcher _parent;
            private readonly IRemoteDialogProtocol _remotingProtocol;
            private readonly ILogger _serviceLogger;
            private readonly uint _messageId;
            private readonly CancellationToken _cancelToken;
            private readonly IRealTimeProvider _threadLocalTime;

            public ReadAsyncResponseClosure(
                RemoteDialogMethodDispatcher parent,
                IRemoteDialogProtocol remotingProtocol,
                ILogger serviceLogger,
                uint messageId,
                CancellationToken cancelToken,
                IRealTimeProvider threadLocalTime)
            {
                _parent = parent;
                _remotingProtocol = remotingProtocol;
                _serviceLogger = serviceLogger;
                _messageId = messageId;
                _cancelToken = cancelToken;
                _threadLocalTime = threadLocalTime;
            }

            public async Task<T> Run()
            {
                try
                {
                    MailboxMessage response = await _parent.ReadReplyMessageFromPostOffice(_messageId, _cancelToken, _threadLocalTime).ConfigureAwait(false);

                    Tuple<object, Type> parsedResponse = _remotingProtocol.Parse(response.Buffer, _serviceLogger);
                    if (parsedResponse.Item2 != typeof(RemoteProcedureResponse<T>))
                    {
                        throw new Exception("Can't parse file stream read/write response");
                    }

                    RemoteProcedureResponse<T> finalResponse = parsedResponse.Item1 as RemoteProcedureResponse<T>;

                    finalResponse.RaiseExceptionIfPresent();
                    return finalResponse.ReturnVal;
                }
                finally
                {
                    _threadLocalTime.Merge();
                }
            }
        }

        private static RemoteFileStreamOpenMode Convert(FileOpenMode mode)
        {
            switch (mode)
            {
                case FileOpenMode.CreateNew:
                    return RemoteFileStreamOpenMode.CreateNew;
                case FileOpenMode.Create:
                    return RemoteFileStreamOpenMode.Create;
                case FileOpenMode.Open:
                    return RemoteFileStreamOpenMode.Open;
                case FileOpenMode.OpenOrCreate:
                    return RemoteFileStreamOpenMode.OpenOrCreate;
                default:
                    throw new Exception("Unknown FileOpenMode");
            }
        }

        private static RemoteFileStreamAccessMode Convert(FileAccessMode mode)
        {
            switch (mode)
            {
                case FileAccessMode.Read:
                    return RemoteFileStreamAccessMode.Read;
                case FileAccessMode.Write:
                    return RemoteFileStreamAccessMode.Write;
                case FileAccessMode.ReadWrite:
                    return RemoteFileStreamAccessMode.ReadWrite;
                default:
                    throw new Exception("Unknown FileAccessMode");
            }
        }

        private static RemoteFileStreamShareMode Convert(FileShareMode mode)
        {
            RemoteFileStreamShareMode returnVal = RemoteFileStreamShareMode.None;
            if (mode.HasFlag(FileShareMode.Read))
            {
                returnVal |= RemoteFileStreamShareMode.Read;
            }
            if (mode.HasFlag(FileShareMode.Write))
            {
                returnVal |= RemoteFileStreamShareMode.Write;
            }
            if (mode.HasFlag(FileShareMode.Delete))
            {
                returnVal |= RemoteFileStreamShareMode.Delete;
            }

            return returnVal;
        }

        public async Task<Task<ArraySegment<byte>>> FileStream_Read(
            string streamId,
            long filePosition,
            int length,
            IRealTimeProvider realTime,
            CancellationToken cancelToken)
        {
            using (CancellationTokenSource mergedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, _dispatcherClosingDownToken))
            {
                CancellationToken operationCancelToken = mergedCts.Token;
                RemoteFileStreamReadRequest remoteRequest = new RemoteFileStreamReadRequest()
                {
                    StreamId = streamId.AssertNonNull(nameof(streamId)),
                    Position = filePosition.AssertNonNegative(nameof(filePosition)),
                    Length = length.AssertNonNegative(nameof(length)),
                };

                PooledBuffer<byte> serializedRequest = _remotingProtocol.Serialize(remoteRequest, _serviceLogger);
                uint messageId = await SendMessageWithCallback(new MailboxMessage(_mailbox, _remotingProtocol.ProtocolId, serializedRequest), realTime).ConfigureAwait(false);
                _serviceLogger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                    "Queued remoted {0} request {1} with message ID {2}",
                    nameof(FileStream_Read), remoteRequest.MethodName, messageId);

                ReadAsyncResponseClosure<ArraySegment<byte>> returnTaskClosure = new ReadAsyncResponseClosure<ArraySegment<byte>>(
                    this,
                    _remotingProtocol,
                    _serviceLogger,
                    messageId,
                    operationCancelToken,
                    realTime.Fork("RemoteFileRead"));
                return returnTaskClosure.Run();
            }
        }

        //public async Task FileStream_WriteSegment(
        //    IRealTimeProvider realTime,
        //    long writeOffset,
        //    ArraySegment<byte> data)
        //{
        //    RemoteFileStreamWriteRequest remoteRequest = new RemoteFileStreamWriteRequest()
        //    {
        //        Position = writeOffset,
        //        Data = data
        //    };

        //    PooledBuffer<byte> serializedRequest = _remotingProtocol.Serialize(remoteRequest, _serviceLogger);
        //    uint messageId = await SendMessageWithCallback(new MailboxMessage(_mailbox, _remotingProtocol.ProtocolId, serializedRequest), realTime).ConfigureAwait(false);
        //    _serviceLogger.Log(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
        //        "Queued remoted {0} request {1} with message ID {2}",
        //        nameof(FileStream_WriteSegment), remoteRequest.MethodName, messageId);
        //    MailboxMessage response = await ReadReplyMessageFromPostOffice(messageId, _cancelToken, realTime).ConfigureAwait(false);

        //    Tuple<object, Type> parsedResponse = _remotingProtocol.Parse(response.Buffer, _serviceLogger);
        //    if (parsedResponse.Item2 != typeof(RemoteProcedureResponse<bool>))
        //    {
        //        throw new Exception("Can't parse file write segment response");
        //    }

        //    RemoteProcedureResponse<bool> finalResponse = parsedResponse.Item1 as RemoteProcedureResponse<bool>;

        //    finalResponse.RaiseExceptionIfPresent();
        //}

        public async Task<NetworkResponseInstrumented<HttpResponse>> Http_Request(
            HttpRequest request,
            Uri baseUri,
            ILogger traceLogger,
            IRealTimeProvider realTime,
            CancellationToken cancelToken)
        {
            using (CancellationTokenSource mergedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, _dispatcherClosingDownToken))
            {
                CancellationToken operationCancelToken = mergedCts.Token;
                Stopwatch timer = Stopwatch.StartNew();
                uint messageId;
                using (RecyclableMemoryStream wireBuffer = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
                using (StreamSocket requestSocketAdapter = new StreamSocket(wireBuffer))
                {
                    await HttpHelpers.WriteRequestToSocket(request, HttpVersion.HTTP_1_1, requestSocketAdapter, operationCancelToken, realTime, traceLogger, () => "Remoted HTTP proxy request").ConfigureAwait(false);
                    bool ssl = string.Equals("https", baseUri.Scheme, StringComparison.OrdinalIgnoreCase);
                    RemoteHttpRequest remoteRequest = new RemoteHttpRequest()
                    {
                        TargetHost = baseUri.Host,
                        TargetPort = baseUri.Port > 0 ? baseUri.Port : (ssl ? 443 : 80),
                        UseSSL = ssl,
                        WireRequest = new ArraySegment<byte>(wireBuffer.GetBuffer(), 0, (int)wireBuffer.Length),
                    };

                    PooledBuffer<byte> serializedRequest = _remotingProtocol.Serialize(remoteRequest, _serviceLogger);
                    messageId = await SendMessageWithCallback(new MailboxMessage(_mailbox, _remotingProtocol.ProtocolId, serializedRequest), realTime).ConfigureAwait(false);
                    _serviceLogger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                        "Queued remoted {0} request {1} with message ID {2}",
                        nameof(Http_Request), remoteRequest.MethodName, messageId);
                }

                // FIXME: Http request timeouts are not honored here
                MailboxMessage response = await ReadReplyMessageFromPostOffice(messageId, operationCancelToken, realTime).ConfigureAwait(false);

                Tuple<object, Type> parsedResponse = _remotingProtocol.Parse(response.Buffer, _serviceLogger);
                if (parsedResponse.Item2 != typeof(RemoteProcedureResponse<ArraySegment<byte>>))
                {
                    throw new Exception("Can't parse http request response");
                }

                RemoteProcedureResponse<ArraySegment<byte>> finalResponse = parsedResponse.Item1 as RemoteProcedureResponse<ArraySegment<byte>>;

                finalResponse.RaiseExceptionIfPresent();

                // The response was a byte array containing just the HTTP wire-format data. So write that
                // buffer to our virtual socket and then tell HTTP to read it as though it were coming from the net
                StreamSocket responseSocketAdapter = new StreamSocket(new MemoryStream(finalResponse.ReturnVal.Array, finalResponse.ReturnVal.Offset, finalResponse.ReturnVal.Count));

                // We associate the response with a socket context that's mocked up based on the fixed byte array of the response data.
                // FIXME this mock socket is disconnected but not disposed, I would need to implement my own socket client context to do that
                // If we wanted to get even crazier with the design, we could forego this entire "read/write the entire request at once" and actually implement
                // streaming requests over the post office protocol. But that would be just crazy.
                HttpResponse baseResponse = await HttpHelpers.ReadResponseFromSocket(responseSocketAdapter, HttpVersion.HTTP_1_1, traceLogger, operationCancelToken, realTime).ConfigureAwait(false);
                timer.Stop();
                return new NetworkResponseInstrumented<HttpResponse>(baseResponse,
                    finalResponse.ReturnVal.Count,
                    finalResponse.ReturnVal.Count,
                    0,
                    timer.ElapsedMillisecondsPrecise(),
                    0);
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

            try
            {
                if (!_dispatcherClosingDownToken.IsCancellationRequested)
                {
                    _cancelTokenSource.Cancel();
                }
            }
            catch (Exception)
            {
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                try
                {
                    //_outgoingMessageLock?.Dispose();
                    _cancelTokenSource?.Dispose();
                }
                catch (Exception)
                {
                }
            }
        }

        /// <summary>
        /// Threadless work-sharing algorithm. When you call this method, it will either start an async task to read from the post office,
        /// or wait for an existing task if one is already running. Then it will repeat until one work item reads a reply to the
        /// given message ID. That message will then be returned. Otherwise, the method will loop until cancellation.
        /// </summary>
        /// <param name="desiredReplyToId">The message which we are looking for a reply to.</param>
        /// <param name="cancelToken">The cancellation token.</param>
        /// <param name="realTime">A definition of real time.</param>
        /// <returns>An async task which will produce a mailbox reply message</returns>
        private async ValueTask<MailboxMessage> ReadReplyMessageFromPostOffice(uint desiredReplyToId, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            RetrieveResult<MailboxMessage> rr = new RetrieveResult<MailboxMessage>();
            while (!rr.Success)
            {
                rr = await _carpoolAlgorithm.WorkOnePhase<uint, MailboxMessage>(StubType.Empty, desiredReplyToId, CheckForCallbacks, cancelToken, realTime).ConfigureAwait(false);
            }

            return rr.Result;
        }

        /// <summary>
        /// Helper method for the threadless work-sharing algorithm.
        /// Reads a single message from the post office. If the message is a reply
        /// to the given replyToId, returns that message. Otherwise, stask the result into
        /// a channel destined for the correct thread which is looking for that message.
        /// </summary>
        /// <param name="dummyInput">Not needed here.</param>
        /// <param name="cancelToken">The cancellation token.</param>
        /// <param name="realTime">A definition of real time.</param>
        /// <returns>An async task which will produce a mailbox reply message IF it is the desired reply, otherwise returns null (but the message still gets sorted)</returns>
        private async ValueTask ReadAndSortSingleMessage(StubType dummyInput, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            //_serviceLogger.Log("ReadAndSortSingleMessage called looking for replies to " + desiredReplyToId);

            MailboxMessage retrieveResult = await _postOffice.Value.ReceiveMessage(_mailbox, cancelToken, realTime).ConfigureAwait(false);
            if (!cancelToken.IsCancellationRequested)
            {
                // See what the message is replying to
                MailboxMessage message = retrieveResult;
                uint replyToId = message.ReplyToId;
                if (_activeCalls.ContainsKey(replyToId))
                {
                    //_serviceLogger.Log("Got message on the socket replying to " + replyToId + " which we will send to channel", LogLevel.Vrb);
                    _activeCalls[replyToId] = message;
                }
                else
                {
                    //_serviceLogger.Log("Got a callback for a nonexistent request " + replyToId, LogLevel.Wrn);
                }
            }
        }

        private ValueTask<RetrieveResult<MailboxMessage>> CheckForCallbacks(uint desiredReplyToId, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            MailboxMessage replyMessage;
            if (_activeCalls.TryGetValue(desiredReplyToId, out replyMessage) && replyMessage != null)
            {
                //_serviceLogger.Log("Got a message on the channel replying to " + desiredReplyToId + " and we are the thread which wants that reply", LogLevel.Vrb);
                return new ValueTask<RetrieveResult<MailboxMessage>>(new RetrieveResult<MailboxMessage>(replyMessage));
            }

            return new ValueTask<RetrieveResult<MailboxMessage>>(new RetrieveResult<MailboxMessage>());
        }

        //private async Task FlushOutgoingMessages()
        //{
        //    await _outgoingMessageLock.WaitAsync().ConfigureAwait(false);
        //    try
        //    {
        //        while (_outgoingMessageQueue.Count > 0)
        //        {
        //            await _outgoingMessageQueue.Dequeue().ConfigureAwait(false);
        //        }
        //    }
        //    finally
        //    {
        //        _outgoingMessageLock.Release();
        //    }
        //}

        //private void SendMessageIgnoreCallback(MailboxMessage message)
        //{
        //    message.MessageId = _postOffice.Value.GenerateMessageId();

        //    _outgoingMessageLock.Wait();
        //    try
        //    {
        //        _outgoingMessageQueue.Enqueue(_postOffice.Value.SendMessage(message, _cancelToken));
        //    }
        //    finally
        //    {
        //        _outgoingMessageLock.Release();
        //    }
        //}

        //private uint SendMessageWithCallback(MailboxMessage message)
        //{
        //    uint messageId = _postOffice.Value.GenerateMessageId();
        //    message.MessageId = messageId;
        //    _activeCalls[messageId] = null;
        //    _outgoingMessageLock.Wait();
        //    try
        //    {
        //        _outgoingMessageQueue.Enqueue(_postOffice.Value.SendMessage(message, _cancelToken));
        //    }
        //    finally
        //    {
        //        _outgoingMessageLock.Release();
        //    }

        //    return messageId;
        //}

        private async Task SendMessageIgnoreCallback(MailboxMessage message, IRealTimeProvider realTime)
        {
            uint messageId = _postOffice.Value.GenerateMessageId();
            message.MessageId = messageId;
            _activeCalls[messageId] = null;
            await _postOffice.Value.SendMessage(message, _dispatcherClosingDownToken, realTime).ConfigureAwait(false);
        }

        private async Task<uint> SendMessageWithCallback(MailboxMessage message, IRealTimeProvider realTime)
        {
            uint messageId = _postOffice.Value.GenerateMessageId();
            message.MessageId = messageId;
            _activeCalls[messageId] = null;
            await _postOffice.Value.SendMessage(message, _dispatcherClosingDownToken, realTime).ConfigureAwait(false);
            return messageId;
        }
    }
}
