using Durandal.API;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Runtime;
using Durandal.Common.Dialog.Services;
using Durandal.Common.Instrumentation;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Remoting.Handlers;
using Durandal.Common.Remoting.Protocol;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Remoting
{
    /// <summary>
    /// This is the client ("host") that talks to an instance of a remote dialog executor server ("guest") in order
    /// to execute plugins and invoke related methods (trigger, crossdomain, load, etc.) over a remoting channel.
    /// The channel talks on a specific protocol over a post office (multiplexer) that is provided by the caller.
    /// </summary>
    public class RemoteDialogExecutorClient : IDurandalPluginLoader
    {
        private readonly TimeSpan TIMEOUT_EXECUTE_PLUGIN = TimeSpan.FromSeconds(10);
        private readonly TimeSpan TIMEOUT_TRIGGER_PLUGIN = TimeSpan.FromSeconds(2);
        private readonly TimeSpan TIMEOUT_LOAD_PLUGIN = TimeSpan.FromMinutes(10);
        private readonly TimeSpan TIMEOUT_UNLOAD_PLUGIN = TimeSpan.FromSeconds(10);
        private readonly TimeSpan TIMEOUT_GET_ALL_PLUGINS = TimeSpan.FromSeconds(10);
        private readonly TimeSpan TIMEOUT_CROSSDOMAIN_REQUEST = TimeSpan.FromSeconds(10);
        private readonly TimeSpan TIMEOUT_FETCH_HTML_DATA = TimeSpan.FromSeconds(10);

        private readonly IRemoteDialogProtocol _protocol;
        private readonly WeakPointer<IMetricCollector> _metrics;
        private readonly DimensionSet _metricDimensions;
        private readonly WeakPointer<PostOffice> _postOffice;
        private readonly ILogger _logger;

        public RemoteDialogExecutorClient(
            IRemoteDialogProtocol protocol,
            WeakPointer<PostOffice> messageRouter,
            ILogger logger,
            WeakPointer<IMetricCollector> metrics,
            DimensionSet metricDimensions,
            bool useDebugTimeouts = false)
        {
            _protocol = protocol;
            _postOffice = messageRouter;
            _logger = logger;
            _metrics = metrics;
            _metricDimensions = metricDimensions;

            if (useDebugTimeouts)
            {
                TIMEOUT_EXECUTE_PLUGIN = TimeSpan.FromMinutes(10);
                TIMEOUT_TRIGGER_PLUGIN = TimeSpan.FromMinutes(10);
                TIMEOUT_LOAD_PLUGIN = TimeSpan.FromMinutes(10);
                TIMEOUT_UNLOAD_PLUGIN = TimeSpan.FromMinutes(10);
                TIMEOUT_GET_ALL_PLUGINS = TimeSpan.FromMinutes(10);
                TIMEOUT_CROSSDOMAIN_REQUEST = TimeSpan.FromMinutes(10);
                TIMEOUT_FETCH_HTML_DATA = TimeSpan.FromMinutes(10);
            }
        }

        public async Task<DialogProcessingResponse> LaunchPlugin(
            PluginStrongName toExecute,
            string entryPoint,
            bool isRetry,
            QueryWithContext query,
            IPluginServicesInternal services,
            ILogger queryLogger,
            IRealTimeProvider realTime)
        {
            using (CancellationTokenSource cancelToken = new NonRealTimeCancellationTokenSource(realTime, TIMEOUT_EXECUTE_PLUGIN))
            {
                MailboxId requestMailboxId = _postOffice.Value.CreateTransientMailbox(realTime);
                RemoteExecutePluginRequest executeRequest = new RemoteExecutePluginRequest()
                {
                    EntryPoint = entryPoint,
                    IsRetry = isRetry,
                    PluginId = toExecute,
                    Query = query,
                    SessionStore = services.SessionStore,
                    LocalUserProfile = services.LocalUserProfile,
                    GlobalUserProfile = services.GlobalUserProfile,
                    EntityContext = services.EntityContext,
                    EntityHistory = (InMemoryEntityHistory)services.EntityHistory,
                    ContextualEntities = services.ContextualEntities,
                    TraceId = queryLogger.TraceId.HasValue ? CommonInstrumentation.FormatTraceId(queryLogger.TraceId.Value) : null,
                    ValidLogLevels = (int)queryLogger.ValidLogLevels,
                    GlobalUserProfileIsWritable = !services.GlobalUserProfile.IsReadOnly
                };

                PooledBuffer<byte> serializedRequest = _protocol.Serialize(executeRequest, queryLogger);
                MailboxMessage message = new MailboxMessage(requestMailboxId, _protocol.ProtocolId, serializedRequest);
                message.MessageId = _postOffice.Value.GenerateMessageId();
                await _postOffice.Value.SendMessage(message, cancelToken.Token, realTime).ConfigureAwait(false);

                RemoteProcedureRequestOrchestrator interstitialHandler = new RemoteProcedureRequestOrchestrator(
                    _protocol,
                    _postOffice,
                    queryLogger,
                    new HttpRemoteProcedureRequestHandler(services.HttpClientFactory),
                    new LoggerRemoteProcedureRequestHandler(services.Logger),
                    new FileSystemRemoteProcedureRequestHandler(services.FileSystem, services.Logger.Clone("FileSystemRemoteHandler")),
                    new PluginServicesRemoteProcedureRequestHandler(services));

                try
                {
                    while (!cancelToken.IsCancellationRequested)
                    {
                        MailboxMessage receivedMessage = await _postOffice.Value.ReceiveMessage(requestMailboxId, cancelToken.Token, realTime).ConfigureAwait(false);

                        if (cancelToken.IsCancellationRequested)
                        {
                            // Timed out waiting for response
                            queryLogger.Log("Timed out waiting for remote procedure response", LogLevel.Err);
                            return new DialogProcessingResponse(new PluginResult(Result.Failure) { ErrorMessage = "Timed out waiting for remote procedure response" }, isRetry);
                        }

                        // Parse messages that are coming back and decide how to dispatch them
                        Tuple<object, Type> parsedMessage = _protocol.Parse(receivedMessage.Buffer, queryLogger);
                        if (parsedMessage == null)
                        {
                            // Unknown message received; treat it as an error
                            queryLogger.Log("Remote procedure response could not be parsed", LogLevel.Err);
                            return new DialogProcessingResponse(new PluginResult(Result.Failure) { ErrorMessage = "Remote procedure response could not be parsed" }, isRetry);
                        }
                        else if (parsedMessage.Item2 == typeof(RemoteProcedureResponse<DialogProcessingResponse>))
                        {
                            // Got the final response
                            RemoteProcedureResponse<DialogProcessingResponse> parsedResponse = parsedMessage.Item1 as RemoteProcedureResponse<DialogProcessingResponse>;
                            if (parsedResponse.Exception != null)
                            {
                                string errorMessage = parsedResponse.Exception.ExceptionType + ": " + parsedResponse.Exception.Message;
                                queryLogger.Log("Exception occurred on remote executor: " + errorMessage, LogLevel.Err);
                                queryLogger.Log(parsedResponse.Exception.StackTrace, LogLevel.Err);
                                await interstitialHandler.Flush().ConfigureAwait(false);
                                return new DialogProcessingResponse(new PluginResult(Result.Failure) { ErrorMessage = errorMessage }, isRetry);
                            }
                            else
                            {
                                // Await on all interstitial postback tasks. They should have all finished by now, but we want to expose errors if any of them failed
                                await interstitialHandler.Flush().ConfigureAwait(false);
                                return parsedResponse.ReturnVal;
                            }
                        }
                        else
                        {
                            await interstitialHandler.HandleIncomingMessage(parsedMessage, receivedMessage, cancelToken.Token, realTime).ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception e)
                {
                    queryLogger.Log(e, LogLevel.Err);
                    return new DialogProcessingResponse(new PluginResult(Result.Failure) { ErrorMessage = e.Message }, isRetry);
                }

                return new DialogProcessingResponse(new PluginResult(Result.Failure) { ErrorMessage = "Possible timeout in remote dialog executor" }, isRetry);
            }
        }

        public async Task<CrossDomainRequestData> CrossDomainRequest(
            PluginStrongName pluginId,
            string targetIntent,
            ILogger queryLogger,
            IRealTimeProvider realTime)
        {
            RemoteCrossDomainRequestRequest request = new RemoteCrossDomainRequestRequest()
            {
                PluginId = pluginId,
                TargetIntent = targetIntent
            };

            PooledBuffer<byte> serializedRequest = _protocol.Serialize(request, queryLogger);

            RetrieveResult<CrossDomainRequestData> result = await SendSimpleRequestOverMailbox<CrossDomainRequestData>(
                serializedRequest,
                queryLogger,
                CancellationToken.None,
                TIMEOUT_CROSSDOMAIN_REQUEST,
                realTime).ConfigureAwait(false);

            if (!result.Success)
            {
                return null;
            }

            return result.Result;
        }

        public async Task<CrossDomainResponseResponse> CrossDomainResponse(
            PluginStrongName pluginId,
            CrossDomainContext context,
            IPluginServicesInternal services,
            ILogger queryLogger,
            IRealTimeProvider realTime)
        {
            using (CancellationTokenSource cancelToken = new NonRealTimeCancellationTokenSource(realTime, TIMEOUT_TRIGGER_PLUGIN))
            {
                MailboxId requestMailboxId = _postOffice.Value.CreateTransientMailbox(realTime);
                RemoteCrossDomainResponseRequest crossDomainResponse = new RemoteCrossDomainResponseRequest()
                {
                    PluginId = pluginId,
                    Context = context,
                    TraceId = queryLogger.TraceId.HasValue ? CommonInstrumentation.FormatTraceId(queryLogger.TraceId.Value) : null,
                    ValidLogLevels = (int)services.Logger.ValidLogLevels,
                    SessionStore = services.SessionStore
                };

                PooledBuffer<byte> serializedRequest = _protocol.Serialize(crossDomainResponse, queryLogger);
                MailboxMessage message = new MailboxMessage(requestMailboxId, _protocol.ProtocolId, serializedRequest);
                message.MessageId = _postOffice.Value.GenerateMessageId();
                //queryLogger.Log("Client sending trigger request with message ID " + message.MessageId + " on mailbox " + requestMailboxId);
                await _postOffice.Value.SendMessage(message, cancelToken.Token, realTime).ConfigureAwait(false);

                RemoteProcedureRequestOrchestrator interstitialHandler = new RemoteProcedureRequestOrchestrator(
                    _protocol,
                    _postOffice,
                    queryLogger,
                    new HttpRemoteProcedureRequestHandler(services.HttpClientFactory),
                    new LoggerRemoteProcedureRequestHandler(services.Logger),
                    new FileSystemRemoteProcedureRequestHandler(services.FileSystem, services.Logger.Clone("FileSystemRemoteHandler")),
                    new PluginServicesRemoteProcedureRequestHandler(services));

                try
                {
                    while (!cancelToken.IsCancellationRequested)
                    {
                        MailboxMessage receivedMessage = await _postOffice.Value.ReceiveMessage(requestMailboxId, cancelToken.Token, realTime).ConfigureAwait(false);

                        if (cancelToken.IsCancellationRequested)
                        {
                            // Timed out waiting for response
                            queryLogger.Log("Timed out waiting for remote crossdomainresponse response on mailbox " + requestMailboxId, LogLevel.Err);
                            return null;
                        }

                        //queryLogger.Log("Client got a message with message ID " + receivedMessage.Result.MessageId + " and replyto ID " + receivedMessage.Result.ReplyToId + " on mailbox " + requestMailboxId);

                        // Parse messages that are coming back and decide how to dispatch them
                        Tuple<object, Type> parsedMessage = _protocol.Parse(receivedMessage.Buffer, queryLogger);

                        //if (parsedMessage != null &&
                        //    parsedMessage.Item2 != null)
                        //{
                        //    queryLogger.Log("Client got message with type " + parsedMessage.Item2.Name);
                        //}

                        if (parsedMessage == null)
                        {
                            // Unknown message received; treat it as an error
                            queryLogger.Log("Remote CDR response could not be parsed", LogLevel.Err);
                            return null;
                        }
                        else if (parsedMessage.Item2 == typeof(RemoteProcedureResponse<CrossDomainResponseResponse>))
                        {
                            // Got the final response
                            RemoteProcedureResponse<CrossDomainResponseResponse> parsedResponse = parsedMessage.Item1 as RemoteProcedureResponse<CrossDomainResponseResponse>;
                            if (parsedResponse.Exception != null)
                            {
                                string errorMessage = parsedResponse.Exception.ExceptionType + ": " + parsedResponse.Exception.Message;
                                queryLogger.Log("Exception occurred on remote executor: " + errorMessage, LogLevel.Err);
                                queryLogger.Log(parsedResponse.Exception.StackTrace, LogLevel.Err);
                                await interstitialHandler.Flush().ConfigureAwait(false);
                                return null;
                            }
                            else
                            {
                                await interstitialHandler.Flush().ConfigureAwait(false);
                                return parsedResponse.ReturnVal;
                            }
                        }
                        else
                        {
                            await interstitialHandler.HandleIncomingMessage(parsedMessage, receivedMessage, cancelToken.Token, realTime).ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception e)
                {
                    queryLogger.Log(e, LogLevel.Err);
                }

                return null;
            }
        }

        public async Task<TriggerProcessingResponse> TriggerPlugin(
            PluginStrongName pluginId,
            QueryWithContext query,
            IPluginServicesInternal services,
            ILogger queryLogger,
            IRealTimeProvider realTime)
        {
            using (CancellationTokenSource cancelToken = new NonRealTimeCancellationTokenSource(realTime, TIMEOUT_TRIGGER_PLUGIN))
            {
                MailboxId requestMailboxId = _postOffice.Value.CreateTransientMailbox(realTime);
                RemoteTriggerPluginRequest triggerRequest = new RemoteTriggerPluginRequest()
                {
                    PluginId = pluginId,
                    Query = query,
                    TraceId = queryLogger.TraceId.HasValue ? CommonInstrumentation.FormatTraceId(queryLogger.TraceId.Value) : null,
                    ValidLogLevels = (int)queryLogger.ValidLogLevels
                };

                PooledBuffer<byte> serializedRequest = _protocol.Serialize(triggerRequest, queryLogger);
                MailboxMessage message = new MailboxMessage(requestMailboxId, _protocol.ProtocolId, serializedRequest);
                message.MessageId = _postOffice.Value.GenerateMessageId();
                //queryLogger.Log("Client sending trigger request with message ID " + message.MessageId + " on mailbox " + requestMailboxId);
                await _postOffice.Value.SendMessage(message, cancelToken.Token, realTime).ConfigureAwait(false);

                RemoteProcedureRequestOrchestrator interstitialHandler = new RemoteProcedureRequestOrchestrator(
                    _protocol,
                    _postOffice,
                    queryLogger,
                    new HttpRemoteProcedureRequestHandler(services.HttpClientFactory),
                    new LoggerRemoteProcedureRequestHandler(services.Logger),
                    new FileSystemRemoteProcedureRequestHandler(services.FileSystem, services.Logger.Clone("FileSystemRemoteHandler")),
                    new PluginServicesRemoteProcedureRequestHandler(services));

                try
                {
                    while (!cancelToken.IsCancellationRequested)
                    {
                        MailboxMessage receivedMessage = await _postOffice.Value.ReceiveMessage(requestMailboxId, cancelToken.Token, realTime).ConfigureAwait(false);

                        if (cancelToken.IsCancellationRequested)
                        {
                            // Timed out waiting for response
                            queryLogger.Log("Timed out waiting for remote trigger response on mailbox " + requestMailboxId, LogLevel.Err);
                            return null;
                        }

                        //queryLogger.Log("Client got a message with message ID " + receivedMessage.Result.MessageId + " and replyto ID " + receivedMessage.Result.ReplyToId + " on mailbox " + requestMailboxId);

                        // Parse messages that are coming back and decide how to dispatch them
                        Tuple<object, Type> parsedMessage = _protocol.Parse(receivedMessage.Buffer, queryLogger);

                        //if (parsedMessage != null &&
                        //    parsedMessage.Item2 != null)
                        //{
                        //    queryLogger.Log("Client got message with type " + parsedMessage.Item2.Name);
                        //}

                        if (parsedMessage == null)
                        {
                            // Unknown message received; treat it as an error
                            queryLogger.Log("Remote trigger response could not be parsed", LogLevel.Err);
                            return null;
                        }
                        else if (parsedMessage.Item2 == typeof(RemoteProcedureResponse<TriggerProcessingResponse>))
                        {
                            // Got the final response
                            RemoteProcedureResponse<TriggerProcessingResponse> parsedResponse = parsedMessage.Item1 as RemoteProcedureResponse<TriggerProcessingResponse>;
                            if (parsedResponse.Exception != null)
                            {
                                string errorMessage = parsedResponse.Exception.ExceptionType + ": " + parsedResponse.Exception.Message;
                                queryLogger.Log("Exception occurred on remote executor: " + errorMessage, LogLevel.Err);
                                queryLogger.Log(parsedResponse.Exception.StackTrace, LogLevel.Err);
                                await interstitialHandler.Flush().ConfigureAwait(false);
                                return null;
                            }
                            else
                            {
                                await interstitialHandler.Flush().ConfigureAwait(false);

                                if (parsedResponse.ReturnVal != null &&
                                    parsedResponse.ReturnVal.UpdatedSessionStore != null)
                                {
                                    // need to recalculate the Touched flag on certain objects where it wasn't explicitly serialized
                                    parsedResponse.ReturnVal.UpdatedSessionStore.Touched = true;
                                }

                                return parsedResponse.ReturnVal;
                            }
                        }
                        else
                        {
                            await interstitialHandler.HandleIncomingMessage(parsedMessage, receivedMessage, cancelToken.Token, realTime).ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception e)
                {
                    queryLogger.Log(e, LogLevel.Err);
                }

                return null;
            }
        }

        public async Task<LoadedPluginInformation> LoadPlugin(PluginStrongName pluginId, IPluginServicesInternal localServices, ILogger logger, IRealTimeProvider realTime)
        {
            using (CancellationTokenSource cancelToken = new NonRealTimeCancellationTokenSource(realTime, TIMEOUT_LOAD_PLUGIN))
            {
                RemoteProcedureRequestOrchestrator interstitialHandler = new RemoteProcedureRequestOrchestrator(
                    _protocol,
                    _postOffice,
                    logger,
                    new LoggerRemoteProcedureRequestHandler(localServices.Logger),
                    new PluginServicesRemoteProcedureRequestHandler(localServices)
                    // FIXME: What services do we support over the remote channel during load?
                    //new HttpRemoteProcedureRequestHandler(localServices.HttpClientFactory),
                    //new FileSystemRemoteProcedureRequestHandler(localServices.FileSystem)
                    );

                RemoteLoadPluginRequest loadRequest = new RemoteLoadPluginRequest()
                {
                    PluginId = pluginId
                };

                PooledBuffer<byte> serializedRequest = _protocol.Serialize(loadRequest, logger);
                MailboxId requestMailboxId = _postOffice.Value.CreateTransientMailbox(realTime);
                MailboxMessage message = new MailboxMessage(requestMailboxId, _protocol.ProtocolId, serializedRequest);
                message.MessageId = _postOffice.Value.GenerateMessageId();
                await _postOffice.Value.SendMessage(message, cancelToken.Token, realTime).ConfigureAwait(false);

                try
                {
                    while (!cancelToken.IsCancellationRequested)
                    {
                        MailboxMessage receivedMessage = await _postOffice.Value.ReceiveMessage(requestMailboxId, cancelToken.Token, realTime).ConfigureAwait(false);

                        if (cancelToken.IsCancellationRequested)
                        {
                            // Timed out waiting for response
                            logger.Log("Timed out waiting for remote load plugin response on mailbox " + requestMailboxId, LogLevel.Err);
                            return null;
                        }

                        //queryLogger.Log("Client got a message with message ID " + receivedMessage.Result.MessageId + " and replyto ID " + receivedMessage.Result.ReplyToId + " on mailbox " + requestMailboxId);

                        // Parse messages that are coming back and decide how to dispatch them
                        Tuple<object, Type> parsedMessage = _protocol.Parse(receivedMessage.Buffer, logger);

                        //if (parsedMessage != null &&
                        //    parsedMessage.Item2 != null)
                        //{
                        //    queryLogger.Log("Client got message with type " + parsedMessage.Item2.Name);
                        //}

                        if (parsedMessage == null)
                        {
                            // Unknown message received; treat it as an error
                            logger.Log("Remote load plugin response could not be parsed", LogLevel.Err);
                            return null;
                        }
                        else if (parsedMessage.Item2 == typeof(RemoteProcedureResponse<LoadedPluginInformation>))
                        {
                            // Got the final response
                            RemoteProcedureResponse<LoadedPluginInformation> parsedResponse = parsedMessage.Item1 as RemoteProcedureResponse<LoadedPluginInformation>;
                            if (parsedResponse.Exception != null)
                            {
                                string errorMessage = parsedResponse.Exception.ExceptionType + ": " + parsedResponse.Exception.Message;
                                logger.Log("Exception occurred on remote executor: " + errorMessage, LogLevel.Err);
                                logger.Log(parsedResponse.Exception.StackTrace, LogLevel.Err);
                                await interstitialHandler.Flush().ConfigureAwait(false);
                                return null;
                            }
                            else
                            {
                                await interstitialHandler.Flush().ConfigureAwait(false);
                                return parsedResponse.ReturnVal;
                            }
                        }
                        else
                        {
                            await interstitialHandler.HandleIncomingMessage(parsedMessage, receivedMessage, cancelToken.Token, realTime).ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception e)
                {
                    logger.Log(e, LogLevel.Err);
                }

                return null;
            }
        }

        public async Task<bool> UnloadPlugin(PluginStrongName pluginId, IPluginServicesInternal localServices, ILogger logger, IRealTimeProvider realTime)
        {
            RemoteUnloadPluginRequest unloadRequest = new RemoteUnloadPluginRequest()
            {
                PluginId = pluginId
            };

            PooledBuffer<byte> serializedRequest = _protocol.Serialize(unloadRequest, logger);

            RetrieveResult<bool> result = await SendSimpleRequestOverMailbox<bool>(serializedRequest, logger, CancellationToken.None, TIMEOUT_UNLOAD_PLUGIN, realTime).ConfigureAwait(false);
            if (!result.Success)
            {
                return false;
            }

            return result.Result;
        }

        public async Task<IEnumerable<PluginStrongName>> GetAllAvailablePlugins(IRealTimeProvider realTime)
        {
            RemoteGetAvailablePluginsRequest getPluginsRequest = new RemoteGetAvailablePluginsRequest();
            PooledBuffer<byte> serializedRequest = _protocol.Serialize(getPluginsRequest, _logger);

            RetrieveResult<List<PluginStrongName>> result = await SendSimpleRequestOverMailbox<List<PluginStrongName>>(serializedRequest, _logger, CancellationToken.None, TIMEOUT_GET_ALL_PLUGINS, realTime).ConfigureAwait(false);
            if (!result.Success)
            {
                return new List<PluginStrongName>();
            }

            return result.Result;
        }

        public async Task<CachedWebData> FetchPluginViewData(PluginStrongName pluginId, string path, DateTimeOffset? ifModifiedSince, ILogger traceLogger, IRealTimeProvider realTime)
        {
            RemoteFetchPluginViewDataRequest fetchViewDataRequest = new RemoteFetchPluginViewDataRequest()
            {
                 PluginId = pluginId,
                 FilePath = path,
                 IfModifiedSince = ifModifiedSince
            };

            PooledBuffer<byte> serializedRequest = _protocol.Serialize(fetchViewDataRequest, _logger);

            RetrieveResult<CachedWebData> result = await SendSimpleRequestOverMailbox<CachedWebData>(serializedRequest, _logger, CancellationToken.None, TIMEOUT_FETCH_HTML_DATA, realTime).ConfigureAwait(false);
            if (!result.Success)
            {
                return null;
            }

            return result.Result;
        }

#if DEBUG
        public async Task _UnitTesting_CrashContainer(IRealTimeProvider realTime)
        {
            RemoteCrashContainerRequest request = new RemoteCrashContainerRequest();
            PooledBuffer<byte> serializedRequest = _protocol.Serialize(request, _logger);
            MailboxId requestMailboxId = _postOffice.Value.CreateTransientMailbox(realTime);
            MailboxMessage message = new MailboxMessage(requestMailboxId, _protocol.ProtocolId, serializedRequest);
            message.MessageId = _postOffice.Value.GenerateMessageId();
            await _postOffice.Value.SendMessage(message, CancellationToken.None, realTime).ConfigureAwait(false);
        }
#endif

        private async Task<RetrieveResult<T>> SendSimpleRequestOverMailbox<T>(PooledBuffer<byte> payload, ILogger logger, CancellationToken cancelToken, TimeSpan timeout, IRealTimeProvider realTime)
        {
            MailboxId requestMailboxId = _postOffice.Value.CreateTransientMailbox(realTime);
            MailboxMessage message = new MailboxMessage(requestMailboxId, _protocol.ProtocolId, payload);
            message.MessageId = _postOffice.Value.GenerateMessageId();
            await _postOffice.Value.SendMessage(message, CancellationToken.None, realTime).ConfigureAwait(false);

            try
            {
                while (!cancelToken.IsCancellationRequested)
                {
                    RetrieveResult<MailboxMessage> receivedMessage = await _postOffice.Value.TryReceiveMessage(requestMailboxId, cancelToken, timeout, realTime).ConfigureAwait(false);

                    if (!receivedMessage.Success)
                    {
                        // Timed out waiting for response
                        logger.Log("Timed out waiting for remote procedure response", LogLevel.Err);
                        return new RetrieveResult<T>();
                    }

                    // Parse messages that are coming back and decide how to dispatch them
                    Tuple<object, Type> parsedMessage = _protocol.Parse(receivedMessage.Result.Buffer, logger);
                    if (parsedMessage == null)
                    {
                        // Unknown message received; treat it as an error
                        logger.Log("Remote procedure response could not be parsed", LogLevel.Err);
                        return new RetrieveResult<T>();
                    }
                    else if (parsedMessage.Item2 == typeof(RemoteProcedureResponse<T>))
                    {
                        // Got the final response
                        RemoteProcedureResponse<T> parsedResponse = parsedMessage.Item1 as RemoteProcedureResponse<T>;
                        if (parsedResponse.Exception != null)
                        {
                            string errorMessage = parsedResponse.Exception.ExceptionType + ": " + parsedResponse.Exception.Message;
                            logger.Log("Exception occurred on remote executor: " + errorMessage, LogLevel.Err);
                            logger.Log(parsedResponse.Exception.StackTrace, LogLevel.Err);
                            return new RetrieveResult<T>();
                        }
                        else
                        {
                            return new RetrieveResult<T>(parsedResponse.ReturnVal);
                        }
                    }
                    else
                    {
                        // Got something else; ignore it for now
                        logger.Log("Unknown remoting message with type " + parsedMessage.Item2.Name, LogLevel.Wrn);
                    }
                }

                return new RetrieveResult<T>();
            }
            catch (Exception e)
            {
                logger.Log(e, LogLevel.Err);
                return new RetrieveResult<T>();
            }
        }
    }
}
