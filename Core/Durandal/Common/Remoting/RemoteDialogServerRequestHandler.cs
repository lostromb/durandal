using Durandal.API;
using Durandal.Common.Config;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Runtime;
using Durandal.Common.Dialog.Services;
using Durandal.Common.Dialog.Web;
using Durandal.Common.File;
using Durandal.Common.Instrumentation;
using Durandal.Common.LG;
using Durandal.Common.LG.Statistical;
using Durandal.Common.Logger;
using Durandal.Common.Net.Http;
using Durandal.Common.NLP;
using Durandal.Common.Ontology;
using Durandal.Common.Remoting.Protocol;
using Durandal.Common.Security.OAuth;
using Durandal.Common.Speech.SR;
using Durandal.Common.Speech.TTS;
using Durandal.Common.Test;
using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Utils;
using Durandal.Common.Remoting.Proxies;
using Durandal.Common.Instrumentation.Profiling;
using Durandal.Common.NLP.Language;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Remoting
{
    /// <summary>
    /// Helper class that handles a single socket connection to a remote desktop server or "guest". This object stays alive
    /// as long as a single mailbox is active, and dispatches the primary requests (ExecutePlugin, TriggerPlugin, CrossDomain, etc.) over
    /// that single mailbox. This represents the adapter layer between the dialog remoting layer, the remoting protocol, and the 
    /// actual PluginLoader which actually executes the actual plugin. Included in this work is constructing properly remoted PluginServices
    /// and providing methods for all of those services to communicate through the post office.
    /// </summary>
    public class RemoteDialogServerRequestHandler
    {
        private readonly WeakPointer<PostOffice> _postOffice;
        private readonly MailboxId _mailbox;
        private readonly ILogger _logger;
        private readonly IDictionary<uint, IRemoteDialogProtocol> _protocols;
        private readonly IDurandalPluginLoader _pluginLoader;
        private readonly WeakPointer<IThreadPool> _serverThreadPool;
        private readonly IFileSystem _localFileSystem;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILGScriptCompiler _lgScriptCompiler;
        private readonly VirtualPath _lgDataDirectory;
        private readonly VirtualPath _pluginConfigDirectory;
        private readonly INLPToolsCollection _pluginNlpTools;
        private readonly FastConcurrentDictionary<PluginStrongName, CachedRemotePluginServicesConstants> _cachedPluginServices;
        private readonly WeakPointer<IMetricCollector> _metrics;
        private readonly DimensionSet _metricDimensions;

        public RemoteDialogServerRequestHandler(
            ILogger logger,
            WeakPointer<PostOffice> postOffice,
            IDurandalPluginLoader pluginLoader,
            IDictionary<uint, IRemoteDialogProtocol> dialogProtocols,
            FastConcurrentDictionary<PluginStrongName, CachedRemotePluginServicesConstants> cachedPluginServices,
            MailboxId mailbox,
            IThreadPool serverThreadPool,
            IFileSystem localFileSystem,
            IHttpClientFactory httpClientFactory,
            ILGScriptCompiler lgScriptCompiler,
            INLPToolsCollection pluginNlpTools,
            WeakPointer<IMetricCollector> metrics,
            DimensionSet metricDimensions)
        {
            _logger = logger;
            _postOffice = postOffice;
            _pluginLoader = pluginLoader;
            _protocols = dialogProtocols;
            _mailbox = mailbox;
            _serverThreadPool = new WeakPointer<IThreadPool>(serverThreadPool);
            _localFileSystem = localFileSystem;
            _httpClientFactory = httpClientFactory;
            _lgScriptCompiler = lgScriptCompiler;
            _pluginNlpTools = pluginNlpTools;
            _cachedPluginServices = cachedPluginServices;
            _lgDataDirectory = new VirtualPath(RuntimeDirectoryName.LG_DIR);
            _pluginConfigDirectory = new VirtualPath(RuntimeDirectoryName.PLUGINCONFIG_DIR);
            _metrics = metrics;
            _metricDimensions = metricDimensions;
        }

        public async Task ProcessIncomingRequest(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            try
            {
                RetrieveResult<MailboxMessage> initialMessage = await _postOffice.Value.TryReceiveMessage(_mailbox, cancelToken, TimeSpan.FromSeconds(10), realTime).ConfigureAwait(false);
                if (!initialMessage.Success)
                {
                    _logger.Log("Incoming request failed because there is no initial message (should never happen)", LogLevel.Err);
                    return;
                }

                uint protocolId = initialMessage.Result.ProtocolId;
                IRemoteDialogProtocol protocol;
                if (!_protocols.TryGetValue(protocolId, out protocol))
                {
                    _logger.Log("Incoming request uses unsupported protocol " + protocolId, LogLevel.Err);
                    return;
                }

                Tuple<object, Type> parsedRequest = protocol.Parse(initialMessage.Result.Buffer, _logger);
                if (parsedRequest == null)
                {
                    _logger.Log("Incoming request couldn't be parsed", LogLevel.Err);
                    return;
                }

                //_logger.Log("Server got message with type " + parsedRequest.Item2.Name + " and messageId " + initialMessage.Result.MessageId + " on mailbox " + initialMessage.Result.MailboxId);

                if (parsedRequest.Item2 == typeof(RemoteExecutePluginRequest))
                {
                    using (RemoteDialogMethodDispatcher methodDispatcher = new RemoteDialogMethodDispatcher(_postOffice.Value, _mailbox, _logger, protocol))
                    {
                        RemoteExecutePluginRequest executionRequest = parsedRequest.Item1 as RemoteExecutePluginRequest;
                        RemoteProcedureResponse<DialogProcessingResponse> returnVal;

                        InMemoryDataStore globalUserProfile = executionRequest.GlobalUserProfile;
                        if (!executionRequest.GlobalUserProfileIsWritable)
                        {
                            globalUserProfile.IsReadOnly = true;
                        }

                        RemotedLogger remotedLogger = new RemotedLogger(
                            new WeakPointer<RemoteDialogMethodDispatcher>(methodDispatcher),
                            realTime,
                            _logger,
                            "RemoteDialogServer",
                            (LogLevel)executionRequest.ValidLogLevels);
                        using (RemotedPluginServices remotePluginServices = BuildRemotedPluginServices(
                            executionRequest.PluginId,
                            realTime,
                            remotedLogger.CreateTraceLogger(CommonInstrumentation.TryParseTraceIdGuid(executionRequest.TraceId), "RemoteDialogServer"),
                            methodDispatcher,
                            executionRequest.ValidLogLevels,
                            CommonInstrumentation.TryParseTraceIdGuid(executionRequest.TraceId),
                            overrideLogger: null,
                            inSessionStore: executionRequest.SessionStore,
                            inLocalUserProfile: executionRequest.LocalUserProfile,
                            inGlobalUserProfile: globalUserProfile,
                            inEntityHistory: executionRequest.EntityHistory,
                            inEntityContext: executionRequest.EntityContext,
                            inContextualEntities: executionRequest.ContextualEntities))
                        {
                            try
                            {
                                DialogProcessingResponse pluginResponse = await _pluginLoader.LaunchPlugin(
                                    executionRequest.PluginId,
                                    executionRequest.EntryPoint,
                                    executionRequest.IsRetry,
                                    executionRequest.Query,
                                    remotePluginServices,
                                    remotePluginServices.Logger,
                                    realTime).ConfigureAwait(false);

                                returnVal = new RemoteProcedureResponse<DialogProcessingResponse>(executionRequest.MethodName, pluginResponse);
                            }
                            catch (Exception e)
                            {
                                //_logger.Log(e, LogLevel.Err);
                                returnVal = new RemoteProcedureResponse<DialogProcessingResponse>(executionRequest.MethodName, e);
                            }
                            finally
                            {
                                // Due to the nature of how the remoted logger is set up, we _have_ to flush here.
                                // Otherwise it would just buffer all of the messages and never send them.
                                // Sending them as a single batch during flush saves about 10ms of latency
                                await remotedLogger.Flush(cancelToken, realTime, true).ConfigureAwait(false);
                                methodDispatcher.Stop();
                            }

                            PooledBuffer<byte> serializedResponse = protocol.Serialize(returnVal, _logger);
                            MailboxMessage responseMessage = new MailboxMessage(_mailbox, protocol.ProtocolId, serializedResponse, _postOffice.Value.GenerateMessageId(), initialMessage.Result.MessageId);
                            await _postOffice.Value.SendMessage(responseMessage, cancelToken, realTime).ConfigureAwait(false);
                            remotedLogger.DisposeOfCore();
                        }
                    }
                }
                else if (parsedRequest.Item2 == typeof(RemoteTriggerPluginRequest))
                {
                    using (RemoteDialogMethodDispatcher methodDispatcher = new RemoteDialogMethodDispatcher(_postOffice.Value, _mailbox, _logger, protocol))
                    {
                        RemoteTriggerPluginRequest triggerRequest = parsedRequest.Item1 as RemoteTriggerPluginRequest;
                        RemoteProcedureResponse<TriggerProcessingResponse> returnVal;

                        RemotedLogger remotedLogger = new RemotedLogger(
                            new WeakPointer<RemoteDialogMethodDispatcher>(methodDispatcher),
                            realTime,
                            _logger,
                            "RemoteDialogServer",
                            (LogLevel)triggerRequest.ValidLogLevels);
                        using (RemotedPluginServices remotePluginServices = BuildRemotedPluginServices(
                                triggerRequest.PluginId,
                                realTime,
                                remotedLogger.CreateTraceLogger(CommonInstrumentation.TryParseTraceIdGuid(triggerRequest.TraceId), "RemoteDialogServer"),
                                methodDispatcher,
                                triggerRequest.ValidLogLevels,
                                CommonInstrumentation.TryParseTraceIdGuid(triggerRequest.TraceId)))
                        {
                            try
                            {
                                TriggerProcessingResponse pluginResponse = await _pluginLoader.TriggerPlugin(
                                    triggerRequest.PluginId,
                                    triggerRequest.Query,
                                    remotePluginServices,
                                    remotePluginServices.Logger,
                                    realTime).ConfigureAwait(false);

                                returnVal = new RemoteProcedureResponse<TriggerProcessingResponse>(triggerRequest.MethodName, pluginResponse);
                            }
                            catch (Exception e)
                            {
                                //_logger.Log(e, LogLevel.Err);
                                returnVal = new RemoteProcedureResponse<TriggerProcessingResponse>(triggerRequest.MethodName, e);
                            }
                            finally
                            {
                                await remotedLogger.Flush(cancelToken, realTime, true).ConfigureAwait(false);
                                methodDispatcher.Stop();
                            }

                            PooledBuffer<byte> serializedResponse = protocol.Serialize(returnVal, _logger);
                            MailboxMessage responseMessage = new MailboxMessage(_mailbox, protocol.ProtocolId, serializedResponse, _postOffice.Value.GenerateMessageId(), initialMessage.Result.MessageId);
                            await _postOffice.Value.SendMessage(responseMessage, cancelToken, realTime).ConfigureAwait(false);
                            remotedLogger.DisposeOfCore();
                        }
                    }
                }
                else if (parsedRequest.Item2 == typeof(RemoteLoadPluginRequest))
                {
                    using (RemoteDialogMethodDispatcher methodDispatcher = new RemoteDialogMethodDispatcher(_postOffice.Value, _mailbox, _logger, protocol))
                    {
                        RemoteLoadPluginRequest loadRequest = parsedRequest.Item1 as RemoteLoadPluginRequest;

                        RemotedLogger remotedLogger = new RemotedLogger(
                            new WeakPointer<RemoteDialogMethodDispatcher>(methodDispatcher),
                            realTime,
                            _logger,
                            "RemoteDialogServer",
                            LogLevel.All);
                        using (RemotedPluginServices remotePluginServices = BuildRemotedPluginServices(loadRequest.PluginId, realTime, remotedLogger, overrideLogger: _logger))
                        {
                            RemoteProcedureResponse<LoadedPluginInformation> returnVal;
                            try
                            {
                                LoadedPluginInformation pluginResponse = await _pluginLoader.LoadPlugin(
                                    loadRequest.PluginId,
                                    remotePluginServices,
                                    remotePluginServices.Logger,
                                    realTime).ConfigureAwait(false);

                                returnVal = new RemoteProcedureResponse<LoadedPluginInformation>(loadRequest.MethodName, pluginResponse);
                            }
                            catch (Exception e)
                            {
                                //_logger.Log(e, LogLevel.Err);
                                returnVal = new RemoteProcedureResponse<LoadedPluginInformation>(loadRequest.MethodName, e);
                            }
                            finally
                            {
                                await remotedLogger.Flush(cancelToken, realTime, true).ConfigureAwait(false);
                                methodDispatcher.Stop();
                            }

                            PooledBuffer<byte> serializedResponse = protocol.Serialize(returnVal, _logger);
                            MailboxMessage responseMessage = new MailboxMessage(_mailbox, protocol.ProtocolId, serializedResponse, _postOffice.Value.GenerateMessageId(), initialMessage.Result.MessageId);
                            await _postOffice.Value.SendMessage(responseMessage, cancelToken, realTime).ConfigureAwait(false);
                            remotedLogger.DisposeOfCore();
                        }
                    }
                }
                else if (parsedRequest.Item2 == typeof(RemoteUnloadPluginRequest))
                {
                    RemoteUnloadPluginRequest unloadRequest = parsedRequest.Item1 as RemoteUnloadPluginRequest;
                    RemoteProcedureResponse<bool> returnVal;

                    using (RemotedPluginServices remotePluginServices = BuildRemotedPluginServices(unloadRequest.PluginId, realTime, _logger, overrideLogger: _logger))
                    {
                        try
                        {
                            bool pluginResponse = await _pluginLoader.UnloadPlugin(
                                unloadRequest.PluginId,
                                remotePluginServices,
                                _logger,
                                realTime).ConfigureAwait(false);

                            returnVal = new RemoteProcedureResponse<bool>(unloadRequest.MethodName, pluginResponse);
                        }
                        catch (Exception e)
                        {
                            //_logger.Log(e, LogLevel.Err);
                            returnVal = new RemoteProcedureResponse<bool>(unloadRequest.MethodName, e);
                        }

                        PooledBuffer<byte> serializedResponse = protocol.Serialize(returnVal, _logger);
                        MailboxMessage responseMessage = new MailboxMessage(_mailbox, protocol.ProtocolId, serializedResponse, _postOffice.Value.GenerateMessageId(), initialMessage.Result.MessageId);
                        await _postOffice.Value.SendMessage(responseMessage, cancelToken, realTime).ConfigureAwait(false);
                    }
                }
                else if (parsedRequest.Item2 == typeof(RemoteGetAvailablePluginsRequest))
                {
                    RemoteProcedureResponse<List<PluginStrongName>> returnVal;
                    try
                    {
                        List<PluginStrongName> pluginResponse = (await _pluginLoader.GetAllAvailablePlugins(realTime).ConfigureAwait(false)).ToList();
                        returnVal = new RemoteProcedureResponse<List<PluginStrongName>>(RemoteGetAvailablePluginsRequest.METHOD_NAME, pluginResponse);
                    }
                    catch (Exception e)
                    {
                        //_logger.Log(e, LogLevel.Err);
                        returnVal = new RemoteProcedureResponse<List<PluginStrongName>>(RemoteGetAvailablePluginsRequest.METHOD_NAME, e);
                    }

                    PooledBuffer<byte> serializedResponse = protocol.Serialize(returnVal, _logger);
                    MailboxMessage responseMessage = new MailboxMessage(_mailbox, protocol.ProtocolId, serializedResponse, _postOffice.Value.GenerateMessageId(), initialMessage.Result.MessageId);
                    await _postOffice.Value.SendMessage(responseMessage, cancelToken, realTime).ConfigureAwait(false);
                }
                else if (parsedRequest.Item2 == typeof(RemoteCrossDomainRequestRequest))
                {
                    RemoteCrossDomainRequestRequest cdrRequest = parsedRequest.Item1 as RemoteCrossDomainRequestRequest;
                    ILogger queryLogger = _logger.CreateTraceLogger(CommonInstrumentation.TryParseTraceIdGuid(cdrRequest.TraceId));
                    queryLogger = queryLogger.Clone(allowedLogLevels: (LogLevel)cdrRequest.ValidLogLevels);
                    RemoteProcedureResponse<CrossDomainRequestData> returnVal;
                    try
                    {
                        CrossDomainRequestData pluginResponse = await _pluginLoader.CrossDomainRequest(cdrRequest.PluginId, cdrRequest.TargetIntent, queryLogger, realTime).ConfigureAwait(false);
                        returnVal = new RemoteProcedureResponse<CrossDomainRequestData>(RemoteCrossDomainRequestRequest.METHOD_NAME, pluginResponse);
                    }
                    catch (Exception e)
                    {
                        //_logger.Log(e, LogLevel.Err);
                        returnVal = new RemoteProcedureResponse<CrossDomainRequestData>(RemoteCrossDomainRequestRequest.METHOD_NAME, e);
                    }

                    PooledBuffer<byte> serializedResponse = protocol.Serialize(returnVal, _logger);
                    MailboxMessage responseMessage = new MailboxMessage(_mailbox, protocol.ProtocolId, serializedResponse, _postOffice.Value.GenerateMessageId(), initialMessage.Result.MessageId);
                    await _postOffice.Value.SendMessage(responseMessage, cancelToken, realTime).ConfigureAwait(false);
                }
                else if (parsedRequest.Item2 == typeof(RemoteCrossDomainResponseRequest))
                {
                    using (RemoteDialogMethodDispatcher methodDispatcher = new RemoteDialogMethodDispatcher(_postOffice.Value, _mailbox, _logger, protocol))
                    {
                        RemoteCrossDomainResponseRequest cdrResponse = parsedRequest.Item1 as RemoteCrossDomainResponseRequest;
                        RemoteProcedureResponse<CrossDomainResponseResponse> returnVal;

                        RemotedLogger remotedLogger = new RemotedLogger(
                                new WeakPointer<RemoteDialogMethodDispatcher>(methodDispatcher),
                                realTime,
                                _logger,
                                "RemoteDialogServer",
                                (LogLevel)cdrResponse.ValidLogLevels);
                        using (RemotedPluginServices remotePluginServices = BuildRemotedPluginServices(
                                cdrResponse.PluginId,
                                realTime,
                                remotedLogger.CreateTraceLogger(CommonInstrumentation.TryParseTraceIdGuid(cdrResponse.TraceId), "RemoteDialogServer"),
                                methodDispatcher,
                                cdrResponse.ValidLogLevels,
                                CommonInstrumentation.TryParseTraceIdGuid(cdrResponse.TraceId),
                                inSessionStore: cdrResponse.SessionStore))
                        {
                            try
                            {
                                CrossDomainResponseResponse pluginResponse = await _pluginLoader.CrossDomainResponse(
                                    cdrResponse.PluginId,
                                    cdrResponse.Context,
                                    remotePluginServices,
                                    remotePluginServices.Logger,
                                    realTime).ConfigureAwait(false);

                                returnVal = new RemoteProcedureResponse<CrossDomainResponseResponse>(cdrResponse.MethodName, pluginResponse);
                            }
                            catch (Exception e)
                            {
                                //_logger.Log(e, LogLevel.Err);
                                returnVal = new RemoteProcedureResponse<CrossDomainResponseResponse>(cdrResponse.MethodName, e);
                            }
                            finally
                            {
                                await remotedLogger.Flush(cancelToken, realTime, true).ConfigureAwait(false);
                                methodDispatcher.Stop();
                            }

                            PooledBuffer<byte> serializedResponse = protocol.Serialize(returnVal, _logger);
                            MailboxMessage responseMessage = new MailboxMessage(_mailbox, protocol.ProtocolId, serializedResponse, _postOffice.Value.GenerateMessageId(), initialMessage.Result.MessageId);
                            await _postOffice.Value.SendMessage(responseMessage, cancelToken, realTime).ConfigureAwait(false);
                            remotedLogger.DisposeOfCore();
                        }
                    }
                }
                else if (parsedRequest.Item2 == typeof(RemoteFetchPluginViewDataRequest))
                {
                    RemoteFetchPluginViewDataRequest viewFetchRequest = parsedRequest.Item1 as RemoteFetchPluginViewDataRequest;

                    RemoteProcedureResponse<CachedWebData> returnVal;
                    try
                    {
                        CachedWebData pluginResponse = await _pluginLoader.FetchPluginViewData(
                            viewFetchRequest.PluginId,
                            viewFetchRequest.FilePath,
                            viewFetchRequest.IfModifiedSince,
                            _logger,
                            realTime).ConfigureAwait(false); // FIXME this is the wrong logger
                        returnVal = new RemoteProcedureResponse<CachedWebData>(RemoteFetchPluginViewDataRequest.METHOD_NAME, pluginResponse);
                    }
                    catch (Exception e)
                    {
                        //_logger.Log(e, LogLevel.Err);
                        returnVal = new RemoteProcedureResponse<CachedWebData>(RemoteFetchPluginViewDataRequest.METHOD_NAME, e);
                    }

                    PooledBuffer<byte> serializedResponse = protocol.Serialize(returnVal, _logger);
                    MailboxMessage responseMessage = new MailboxMessage(_mailbox, protocol.ProtocolId, serializedResponse, _postOffice.Value.GenerateMessageId(), initialMessage.Result.MessageId);
                    await _postOffice.Value.SendMessage(responseMessage, cancelToken, realTime).ConfigureAwait(false);
                }
                else if (parsedRequest.Item2 == typeof(KeepAliveRequest))
                {
                    uint operationId = MicroProfiler.GenerateOperationId();
                    MicroProfiler.Send(MicroProfilingEventType.KeepAlive_Ping_RecvRequestStart, operationId);
                    KeepAliveRequest keepAliveRequest = parsedRequest.Item1 as KeepAliveRequest;
                    MicroProfiler.Send(MicroProfilingEventType.KeepAlive_Ping_RecvRequestFinish, operationId);
                    RemoteProcedureResponse<long> returnVal = new RemoteProcedureResponse<long>(keepAliveRequest.MethodName, HighPrecisionTimer.GetCurrentTicks());
                    PooledBuffer<byte> serializedResponse = protocol.Serialize(returnVal, _logger);
                    MailboxMessage responseMessage = new MailboxMessage(_mailbox, protocol.ProtocolId, serializedResponse, _postOffice.Value.GenerateMessageId(), initialMessage.Result.MessageId);
                    MicroProfiler.Send(MicroProfilingEventType.KeepAlive_Ping_SendResponseStart, operationId);
                    await _postOffice.Value.SendMessage(responseMessage, cancelToken, realTime).ConfigureAwait(false);
                    MicroProfiler.Send(MicroProfilingEventType.KeepAlive_Ping_SendResponseFinish, operationId);
                }
                else if (parsedRequest.Item2 == typeof(RemoteCrashContainerRequest))
                {
#if DEBUG
                    throw new PlatformNotSupportedException();
#else
                    _logger.Log("Got a RemoteCrashContainerRequest, it will be ignored because this code was not built in debug mode", LogLevel.Wrn);
#endif
                }
            }
#if DEBUG
            catch (PlatformNotSupportedException)
            {
                // pass along our special "crash container" signal if allowed
                throw;
            }
#endif
            catch (Exception e)
            {
                _logger.Log(e, LogLevel.Err);
            }
        }

        private RemotedPluginServices BuildRemotedPluginServices(
            PluginStrongName pluginId,
            IRealTimeProvider realTime,
            ILogger remotedLogger,
            RemoteDialogMethodDispatcher methodDispatcher = null,
            int validLogLevels = 0,
            Guid? traceId = null,
            ILogger overrideLogger = null,
            InMemoryDataStore inSessionStore = null,
            InMemoryDataStore inLocalUserProfile = null,
            InMemoryDataStore inGlobalUserProfile = null,
            InMemoryEntityHistory inEntityHistory = null,
            KnowledgeContext inEntityContext = null,
            IList<ContextualEntity> inContextualEntities = null)
        {
            ILogger serviceLogger = overrideLogger ?? NullLogger.Singleton;

            ISpeechSynth speechSynth;
            if (methodDispatcher == null)
            {
                speechSynth = new NullSpeechSynth();
            }
            else
            {
                speechSynth = new RemotedSpeechSynth(methodDispatcher, realTime);
            }

            ISpeechRecognizerFactory speechReco;
            if (methodDispatcher == null)
            {
                speechReco = NullSpeechRecoFactory.Singleton;
            }
            else
            {
                speechReco = new RemotedSpeechRecognizerFactory(methodDispatcher);
            }

            IOAuthManager remotedOauth;
            FakeOAuthSecretStore fakeOauthStore = null;
            if (methodDispatcher == null)
            {
                fakeOauthStore = new FakeOAuthSecretStore();
                remotedOauth = new OAuthManager("https://null", fakeOauthStore, _metrics, _metricDimensions);
            }
            else
            {
                remotedOauth = new RemotedOAuthManager(methodDispatcher);
            }

            RemotedEntityResolver remotedEntityResolver = new RemotedEntityResolver(methodDispatcher);

            // Create all temporary objects
            InMemoryDataStore sessionStore = inSessionStore ?? new InMemoryDataStore();
            InMemoryDataStore localUserProfile = inLocalUserProfile ?? new InMemoryDataStore();
            InMemoryDataStore globalUserProfile = inGlobalUserProfile ?? new InMemoryDataStore();
            InMemoryEntityHistory entityHistory = inEntityHistory ?? new InMemoryEntityHistory();
            KnowledgeContext entityContext = inEntityContext ?? new KnowledgeContext();

            // Fetch (or create) all constant services, such as LG
            CachedRemotePluginServicesConstants cachedConstants;
            _cachedPluginServices.TryGetValueOrSet(pluginId, out cachedConstants, () =>
            {
                CachedRemotePluginServicesConstants returnVal = new CachedRemotePluginServicesConstants();

                returnVal.PluginConfiguration = IniFileConfiguration.Create(
                    serviceLogger.Clone("Plugin-" + pluginId.PluginId + "-Config"),
                    _pluginConfigDirectory.Combine("pluginconfig_" + pluginId.PluginId + " " + pluginId.MajorVersion + "." + pluginId.MinorVersion + ".ini"),
                    _localFileSystem,
                    realTime,
                    warnIfNotFound: false).Await();

                // FIXME This is a lot of crap. We are training a language generation engine while holding a bin lock on the concurrent dictionary.
                // But the alternative would be to potentially initialize LG multiple times in parallel, so this is a tradeoff.
                // Perhaps WorkSharingCache could help here?
                returnVal.LanguageGenerator = LGHelpers.BuildLGEngineForPlugin(_localFileSystem, _lgDataDirectory, pluginId, serviceLogger, _lgScriptCompiler, _pluginNlpTools).Await();
                return returnVal;
            });

            return new RemotedPluginServices(
                pluginId: pluginId,
                fileSystem: _localFileSystem,
                languageGenerator: cachedConstants.LanguageGenerator,
                pluginConfiguration: cachedConstants.PluginConfiguration,
                oauthManager: remotedOauth,
                sessionStore: sessionStore,
                localUserProfile: localUserProfile,
                globalUserProfile: globalUserProfile,
                httpClientFactory: _httpClientFactory,
                logger: remotedLogger,
                traceId: traceId,
                speechRecoEngine: speechReco,
                ttsEngine: speechSynth,
                contextualEntities: inContextualEntities,
                entityContext: entityContext,
                entityHistory: entityHistory,
                entityResolver: remotedEntityResolver,
                fakeOauthStore: fakeOauthStore);
        }
    }
}
