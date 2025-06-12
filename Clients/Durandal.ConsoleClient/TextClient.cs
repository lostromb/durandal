
namespace Durandal.ConsoleClient
{
    using Durandal;
    using Durandal.API;
    using Durandal.Common.Client;
    using Durandal.Common.Client.Actions;
    using Durandal.Common.Compression;
    using Durandal.Common.Config;
    using Durandal.Common.Config.Annotation;
    using Durandal.Common.Dialog.Web;
    using Durandal.Common.Events;
    using Durandal.Common.File;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.Logger;
    using Durandal.Common.MathExt;
    using Durandal.Common.Net;
    using Durandal.Common.Net.Http;
    using Durandal.Common.Net.Http2;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.Security.Client;
    using Durandal.Common.Security.Login;
    using Durandal.Common.Security.Login.Providers;
    using Durandal.Common.ServiceMgmt;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using Durandal.Common.Utils;
    using Durandal.Extensions.BondProtocol;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    public class TextClient : IDisposable
    {
        private readonly string _clientName;
        private readonly string _userName;
        private readonly ClientCore _clientCore;
        private readonly ILogger _logger;
        private readonly ClientConfiguration _clientConfig;
        private readonly IFileSystem _fileSystem;
        private readonly IDialogTransportProtocol _dialogProtocol;
        private readonly IRealTimeProvider _realTime;
        private readonly ISocketFactory _socketFactory;
        private readonly IHttpClientFactory _httpClientFactory;
        private JsonClientActionDispatcher _actionDispatcher;
        private ExecuteDelayedActionHandler _delayedActionHandler;
        private readonly IMetricCollector _metrics;
        private readonly DimensionSet _dimensions;
        private int _disposed = 0;

        public TextClient(string[] args)
        {
            string componentName = "ConsoleClient";
            string machineHostName = Dns.GetHostName();
            _realTime = DefaultRealTimeProvider.Singleton;
            LogLevel level;
#if DEBUG
            level = LogLevel.All;
#else
            level = LogLevel.Std | LogLevel.Wrn | LogLevel.Err | LogLevel.Ins;
#endif
            LogoUtil.PrintLogo("Text Client", Console.Out);
            ServicePointManager.Expect100Continue = false;

            ILogger bootstrapLogger = new ConsoleLogger(componentName, level, null);
            bootstrapLogger.Log(string.Format("Durandal client {0} built on {1}",
                SVNVersionInfo.VersionString,
                SVNVersionInfo.BuildDate));

            _metrics = new MetricCollector(bootstrapLogger, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));
            _dimensions = new DimensionSet(new MetricDimension[]
                {
                    new MetricDimension(CommonInstrumentation.Key_Dimension_ServiceName, "Durandal.ConsoleClient"),
                    new MetricDimension(CommonInstrumentation.Key_Dimension_ServiceVersion, SVNVersionInfo.AssemblyVersion),
                    new MetricDimension(CommonInstrumentation.Key_Dimension_HostName, machineHostName)
                });

            string rootRuntimeDirectory = ServiceCommon.GetDurandalEnvironmentDirectory(args, bootstrapLogger);
            ServiceCommon.UnpackBundleFile(bootstrapLogger, "bundle_client.zip", rootRuntimeDirectory);

            _fileSystem = new RealFileSystem(bootstrapLogger, rootRuntimeDirectory);
            VirtualPath configFileName = new VirtualPath("Durandal.ConsoleClient_config.ini");
            IConfiguration configFile = IniFileConfiguration.Create(bootstrapLogger, configFileName, _fileSystem, DefaultRealTimeProvider.Singleton, true, true).Await();
            _clientConfig = new ClientConfiguration(configFile);

            string remoteLoggingEndpoint = _clientConfig.GetBase().GetString("remoteLoggingEndpoint", "durandal-ai.net");
            int remoteLoggingPort = _clientConfig.GetBase().GetInt32("remoteLoggingPort", 62295);
            string remoteLoggingStream = _clientConfig.GetBase().GetString("remoteLoggingStream", "Dev");

            _socketFactory = new PooledTcpClientSocketFactory(bootstrapLogger.Clone("SocketFactory"), _metrics, _dimensions);
            _httpClientFactory = new SocketHttpClientFactory(
                new WeakPointer<ISocketFactory>(_socketFactory),
                new WeakPointer<IMetricCollector>(_metrics),
                _dimensions,
                Http2SessionManager.Default);

            //_httpClientFactory = new PortableHttpClientFactory(new WeakPointer<IMetricCollector>(_metrics), _dimensions);

            _logger = new AggregateLogger(componentName,
                null,
                bootstrapLogger,
                new FileLogger(
                    new RealFileSystem(bootstrapLogger.Clone("FileLogger"), rootRuntimeDirectory),
                    componentName,
                    logFilePrefix: Process.GetCurrentProcess().ProcessName,
                    backgroundLogThreadPool: LoggerBase.DEFAULT_BACKGROUND_LOGGING_THREAD_POOL,
                    bootstrapLogger: bootstrapLogger,
                    validLogLevels: LogLevel.All,
                    maxLogLevels: LogLevel.All,
                    maxFileSizeBytes: 10 * 1024 * 1024,
                    logDirectory: new VirtualPath("logs")),
                new RemoteInstrumentationLogger(
                    _httpClientFactory.CreateHttpClient(remoteLoggingEndpoint, remoteLoggingPort, false, bootstrapLogger),
                    new InstrumentationBlobSerializer(),
                    _realTime,
                    remoteLoggingStream,
                    tracesOnly: true,
                    bootstrapLogger: bootstrapLogger,
                    metrics: _metrics,
                    dimensions: _dimensions,
                    componentName: componentName,
                    //validLogLevels: LogLevel.All,
                    //maxLogLevels: LogLevel.All,
                    //maxPrivacyClasses: DataPrivacyClassification.All,
                    //defaultPrivacyClass: DataPrivacyClassification.SystemMetadata,
                    backgroundLogThreadPool: LoggerBase.DEFAULT_BACKGROUND_LOGGING_THREAD_POOL));

            if (string.IsNullOrEmpty(_clientConfig.ClientId))
            {
                _logger.Log("No client ID found, generating a new value...");
                RawConfigValue newClientId = new RawConfigValue("clientId", StringUtils.HashToGuid(Environment.MachineName).ToString("N"), ConfigValueType.String);
                newClientId.Annotations.Add(new DescriptionAnnotation("The unique ID to identify this client. If this value is removed, a new one will be generated."));
                _clientConfig.GetBase().Set(newClientId);
            }

            if (string.IsNullOrEmpty(_clientConfig.UserId))
            {
                _logger.Log("No user ID found, generating a new value...");
                RawConfigValue newUserId = new RawConfigValue("userId", StringUtils.HashToGuid(Environment.UserName).ToString("N"), ConfigValueType.String);
                newUserId.Annotations.Add(new DescriptionAnnotation("The unique ID to identify this user. If this value is removed, a new one will be generated."));
                _clientConfig.GetBase().Set(newUserId);
            }

            if (string.IsNullOrEmpty(_clientConfig.ClientName))
            {
                _clientName = Dns.GetHostName();
                _logger.LogFormat(LogLevel.Std, DataPrivacyClassification.EndUserIdentifiableInformation, "No client name found, using machine name \"{0}\" instead...", _clientName);
                RawConfigValue newClientName = new RawConfigValue("clientName", _clientName, ConfigValueType.String);
                newClientName.Annotations.Add(new DescriptionAnnotation("A human-readable name for this client. By default, this is the local machine name."));
                newClientName.Annotations.Add(new GUIAnnotation());
                _clientConfig.GetBase().Set(newClientName);
            }
            else
            {
                _clientName = _clientConfig.ClientName;
            }

            if (string.IsNullOrEmpty(_clientConfig.UserName))
            {
                _userName = Environment.UserName;
                _logger.LogFormat(LogLevel.Std, DataPrivacyClassification.EndUserIdentifiableInformation, "No user name found, using logged-in name \"{0}\" instead...", _userName);
                RawConfigValue newUserName = new RawConfigValue("userName", _userName, ConfigValueType.String);
                newUserName.Annotations.Add(new DescriptionAnnotation("A human-readable name for this user. By default, this is the local logged-in user name."));
                newUserName.Annotations.Add(new GUIAnnotation());
                _clientConfig.GetBase().Set(newUserName);
            }
            else
            {
                _userName = _clientConfig.UserName;
            }

            _dialogProtocol = new DialogBondTransportProtocol();

            _logger.LogFormat(LogLevel.Std, DataPrivacyClassification.EndUserPseudonymousIdentifiers, "Client ID = {0} User ID = {1}", _clientConfig.ClientId, _clientConfig.UserId);
            _clientCore = new ClientCore();

            _clientCore.ShowTextOutput.Subscribe(ShowText);
            _clientCore.ShowErrorOutput.Subscribe(ShowError);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~TextClient()
        {
            Dispose(false);
        }
#endif

        public async Task Run()
        {
            ILogger httpClientLogger = _logger.Clone("DialogHttpClient");

            IHttpClient dialogHttp = _httpClientFactory.CreateHttpClient(_clientConfig.RemoteDialogServerAddress, httpClientLogger);
            dialogHttp.SetReadTimeout(TimeSpan.FromMilliseconds(30000));

            IHttpClient dialogFloodHttp = _httpClientFactory.CreateHttpClient(_clientConfig.RemoteDialogServerAddress, httpClientLogger);
            dialogFloodHttp.SetReadTimeout(TimeSpan.FromMilliseconds(10000));

            _actionDispatcher = new JsonClientActionDispatcher();

            _delayedActionHandler = new ExecuteDelayedActionHandler();
            _delayedActionHandler.ExecuteActionEvent.Subscribe(ExecuteDelayedDialogAction);
            _actionDispatcher.AddHandler(_delayedActionHandler);
            _actionDispatcher.AddHandler(new BasicOAuthActionHandler());

            List<ILoginProvider> loginProviders = new List<ILoginProvider>();
            if (_clientConfig.AuthenticationEndpoint != null)
            {
                loginProviders.Add(AdhocLoginProvider.BuildForClient(_httpClientFactory, _logger.Clone("AdhocAuthenticator"), _clientConfig.AuthenticationEndpoint));
                MSAPortableLoginProvider msaProvider = MSAPortableLoginProvider.BuildForClient(_httpClientFactory, _logger.Clone("MSALogin"), _clientConfig.AuthenticationEndpoint);
                loginProviders.Add(msaProvider);
                _actionDispatcher.AddHandler(new MsaPortableLoginActionHandler(msaProvider, TimeSpan.FromMinutes(10)));
            }

            ClientCoreParameters coreParams = new ClientCoreParameters(_clientConfig, BuildClientContext)
            {
                Logger = _logger,
                EnableRSA = true,
                DialogConnection = new DialogHttpClient(dialogHttp, httpClientLogger, _dialogProtocol),
                LoginProviders = loginProviders,
                PrivateKeyStore = new FileBasedClientKeyStore(_fileSystem, _logger),
                ClientActionDispatcher = _actionDispatcher
            };

            await _clientCore.Initialize(coreParams);
            //ClientIdentifier authId = new ClientIdentifier(_userId, _userName, _clientId, _clientName);
            //if (await _clientCore.DoCredentialsExist(authId, ClientAuthenticationScope.UserClient))
            //{
            //    await _clientCore.LoadPrivateKeyFromLocalStore(authId, ClientAuthenticationScope.UserClient);
            //}
            //else
            //{
            //    await _clientCore.LoadPrivateKeyFromAuthProvider(authId, ClientAuthenticationScope.UserClient);
            //}

            //await _clientCore.AuthenticateAsUserClient(_userId, _userName, _clientId, _clientName, _logger);

            Console.WriteLine("Durandal console client started. Type quit to exit");
            bool running = true;
            while (running)
            {
                string nextQuery = Console.ReadLine();
                if ("quit".Equals(nextQuery) || "q".Equals(nextQuery) || "exit".Equals(nextQuery))
                {
                    running = false;
                }
                else if ("flood".Equals(nextQuery))
                {
                    Flood(dialogFloodHttp);
                }
                else
                {
                    await MakeRequest(nextQuery);
                }
            }
        }

        private ClientContext BuildClientContext()
        {
            ClientContext context = new ClientContext();
            context.SetCapabilities(
                ClientCapabilities.HasInternetConnection |
                ClientCapabilities.DisplayUnlimitedText |
                ClientCapabilities.RsaEnabled |
                ClientCapabilities.ClientActions);
            context.ClientId = _clientConfig.ClientId;
            context.UserId = _clientConfig.UserId;

            // The locale code for the communication.
            context.Locale = LanguageCode.EN_US;

            // Local client time
            context.ReferenceDateTime = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            context.UTCOffset = -420;

            // The common name of the client (to be used in dialog-side configuration, prettyprinting, etc.)
            context.ClientName = _clientName;

            // Client coordinates
            // context.Latitude = clientLatitude;
            // context.Longitude = clientLongitude;

            // Form factor code
            context.ExtraClientContext[ClientContextField.FormFactor] = FormFactor.Integrated.ToString();
            context.ExtraClientContext[ClientContextField.ClientType] = "TEXT_CONSOLE";
            context.ExtraClientContext[ClientContextField.ClientVersion] = SVNVersionInfo.VersionString;
            return context;
        }

        private async Task MakeRequest(string query)
        {
            if (await _clientCore.TryMakeTextRequest(query, BuildClientContext(), realTime: _realTime))
            {
                _logger.LogFormat(LogLevel.Std, DataPrivacyClassification.PrivateContent, ">>> \"{0}\"", query);
            }
            else
            {
                _logger.Log("Request not sent; another request is still pending", LogLevel.Wrn);
            }
        }

        private async Task ShowText(object source, TextEventArgs args, IRealTimeProvider realTime)
        {
            _logger.LogFormat(LogLevel.Std, DataPrivacyClassification.PrivateContent, "Got response text: {0}", args.Text);
            await DurandalTaskExtensions.NoOpTask;
        }

        private async Task ShowError(object source, TextEventArgs args, IRealTimeProvider realTime)
        {
            _logger.Log("Got error message: " + args.Text, LogLevel.Err);
            await DurandalTaskExtensions.NoOpTask;
        }

        public async Task ExecuteDelayedDialogAction(object sender, DialogActionEventArgs args, IRealTimeProvider realTime)
        {
            Console.WriteLine("I am executing a delayed action and the ID is " + args.ActionId);
            await DurandalTaskExtensions.NoOpTask;
        }

        /// <inheritdoc/>
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
                _actionDispatcher?.Dispose();
                _delayedActionHandler?.Dispose();
                _metrics?.Dispose();
                _clientCore?.Dispose();
                _socketFactory?.Dispose();
            }
        }

        // For measuring throughput of dialog server

        private void Flood(IHttpClient dialogHttp)
        {
            Stopwatch throughputTimer = new Stopwatch();
            throughputTimer.Start();
            TestRunResults results = RunFloodQueries(dialogHttp, _logger, _dialogProtocol);
            throughputTimer.Stop();
            long elapsedTime = throughputTimer.ElapsedMilliseconds;
            Console.WriteLine("Total time was " + elapsedTime + " ms");
            Console.WriteLine("Avg latency was " + results.AvgLatency + " ms");
            double throughput = (double)results.NumQueries * 1000 / elapsedTime;
            Console.WriteLine("Throughput was " + throughput + " RPS");
            double failurePercent = 100 * (results.NumFailures / (double)results.NumQueries);
            Console.WriteLine("Failure rate was " + failurePercent + "%");
        }

        private static TestRunResults RunFloodQueries(IHttpClient dialogHttp, ILogger logger, IDialogTransportProtocol protocol)
        {
            using (IThreadPool pool = new FixedCapacityThreadPool(
                new SystemThreadPool(logger.Clone("Flood"), NullMetricCollector.Singleton, DimensionSet.Empty),
                NullLogger.Singleton,
                NullMetricCollector.Singleton,
                DimensionSet.Empty,
                "FloodQueryFixed",
                100,
                ThreadPoolOverschedulingBehavior.ShedExcessWorkItems))
            {
                TimeSpan testLength = TimeSpan.FromMinutes(10);
                double initialRate = 1;
                double finalRate = 100;
                int queryIdx = 0;
                IList<string> queries = new List<string>();
                queries.Add("what time is it");
                queries.Add("what day is it");
                queries.Add("tell me a joke");
                queries.Add("what is your name");
                queries.Add("what does the fox say");

                TestRunResults results = new TestRunResults();
                results.NumQueries = queries.Count;
                results.NumFailures = 0;
                StaticAverage latency = new StaticAverage();
                RateLimiter limiter = new RateLimiter(initialRate, 20);
                RateCounter rps = new RateCounter(TimeSpan.FromSeconds(5));
                RateCounter eps = new RateCounter(TimeSpan.FromSeconds(5));
                Stopwatch testTimer = Stopwatch.StartNew();
                Console.WriteLine();

                while (testTimer.Elapsed < testLength)
                {
                    double percentComplete = (double)testTimer.ElapsedTicks / (double)testLength.Ticks;
                    limiter.TargetFrequency = ((finalRate - initialRate) * percentComplete) + initialRate;
                    string query = queries[queryIdx];
                    queryIdx = (queryIdx + 1) % queries.Count;
                    ThreadedExecutor executor = new ThreadedExecutor(query, dialogHttp, NullLogger.Singleton, protocol);
                    results.NumQueries++;
                    pool.EnqueueUserAsyncWorkItem(async () =>
                    {
                        rps.Increment();

                        try
                        {
                            await executor.Run();

                            if (!executor.Success)
                            {
                                results.NumFailures++;
                                eps.Increment();
                            }
                        }
                        catch (Exception)
                        {
                            results.NumFailures++;
                            eps.Increment();
                        }
                        finally
                        {
                            latency.Add(executor.Latency);
                        }
                    });

                    limiter.Limit(DefaultRealTimeProvider.Singleton, CancellationToken.None);
                    
                    Console.Write("\rTarget RPS {0:F1}\tActual RPS {1:F1}\tEPS {2:F1}", limiter.TargetFrequency, rps.Rate, eps.Rate);
                }

                results.AvgLatency = latency.Average;

                pool.Dispose();

                return results;
            }
        }

        private struct TestRunResults
        {
            public double AvgLatency;
            public int NumQueries;
            public int NumFailures;
        }

        private class ThreadedExecutor : IDisposable
        {
            public ILogger Logger;
            public string Query;
            public double Latency;
            public bool Success = true;
            private EventWaitHandle _finished;
            private IHttpClient _httpTransport;
            private IDialogTransportProtocol _protocol;
            private int _disposed = 0;

            public ThreadedExecutor(string query, IHttpClient dialogHttpTransport, ILogger logger, IDialogTransportProtocol protocol)
            {
                Query = query;
                Logger = logger;
                _httpTransport = dialogHttpTransport;
                _finished = new EventWaitHandle(false, EventResetMode.ManualReset);
                _protocol = protocol;
                DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
            }

#if TRACK_IDISPOSABLE_LEAKS
            ~ThreadedExecutor()
            {
                Dispose(false);
            }
#endif

            public async Task Run()
            {
                try
                {
                    DialogHttpClient client = new DialogHttpClient(_httpTransport, Logger, _protocol);
                    DialogRequest request = new DialogRequest();
                    request.InteractionType = InputMethod.Typed;
                    request.ClientContext.ClientId = Guid.NewGuid().ToString("N");
                    request.ClientContext.UserId = Guid.NewGuid().ToString("N");
                    request.ClientContext.Locale = LanguageCode.EN_US;
                    request.ClientContext.ReferenceDateTime = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ss");
                    request.ClientContext.ClientName = "floodclient" + Guid.NewGuid();
                    request.ClientContext.ExtraClientContext[ClientContextField.FormFactor] = FormFactor.Integrated.ToString();
                    
                    request.ClientContext.SetCapabilities(ClientCapabilities.DisplayUnlimitedText);
                    //request.ClientContext.SetCapabilities(
                    //    ClientCapabilities.HasInternetConnection |
                    //    ClientCapabilities.HasSpeakers |
                    //    ClientCapabilities.HasMicrophone |
                    //    ClientCapabilities.RsaEnabled |
                    //    ClientCapabilities.SupportsCompressedAudio |
                    //    ClientCapabilities.SupportsStreamingAudio);

                    request.RequestFlags = (uint)(QueryFlags.None);
                    request.TextInput = Query;
                    request.PreferredAudioCodec = "opus";

                    NetworkResponseInstrumented<DialogResponse> response = await client.MakeQueryRequest(request, Logger, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    if (response == null || response.Response == null)
                    {
                        Success = false;
                        Logger.Log("Null response");
                    }
                    else if (response.Response.ExecutionResult != Result.Success)
                    {
                        Success = false;
                        Logger.Log("Non-success response");
                    }
                    if (response != null)
                    {
                        Latency = response.EndToEndLatency;
                    }
                }
                catch (Exception e)
                {
                    Logger.Log(e, LogLevel.Err);
                }
                finally
                {
                    _finished.Set();
                }
            }

            public void Join()
            {
                _finished.WaitOne();
            }

            /// <inheritdoc/>
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
                    _finished?.Dispose();
                }
            }
        }
    }
}
