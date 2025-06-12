


namespace Durandal
{
    using Durandal.Common.Compression;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.Net.Http;
    using Durandal.Common.NLP.Annotation;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.Packages;
    using Durandal.Common.NLP.Train;
    using Durandal.Common.Tasks;
    using Durandal.API;
    using Durandal.Extensions.Azure.AppInsights;
    using Durandal.Extensions.BondProtocol;
    using Durandal.Common;
    using Durandal.Common.Config;
    using Durandal.Common.File;
    using Durandal.Common.Logger;
    using Durandal.Common.LU;
    using Durandal.Common.Net;
    using Durandal.Common.Utils;
    using Durandal.Common.MathExt;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;
    using Durandal.Common.Security;
    using Durandal.Common.Time;
    using System.Net;
    using Durandal.Common.Collections;
    using Durandal.Common.IO;
    using Durandal.Common.Net.Http2;
    using Durandal.Common.ServiceMgmt;
    using Durandal.Common.Utils.NativePlatform;
    using Durandal.Extensions.Compression.Crc;
    using Durandal.Extensions.NativeAudio;

    public class LanguageUnderstandingMain
    {
        private static ILogger APP_DOMAIN_LOGGER = NullLogger.Singleton;

        // This starts as en-US and can be changed via the console
        public static LanguageCode _consoleLocale = LanguageCode.EN_US;

        public static async Task RunLUService(string[] args)
        {
            string defaultComponentName = "LUMain";
            string machineHostName = Dns.GetHostName();
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            DimensionSet coreMetricDimensions = new DimensionSet(new MetricDimension[]
                {
                    new MetricDimension(CommonInstrumentation.Key_Dimension_ServiceName, "Durandal.LanguageUnderstanding"),
                    new MetricDimension(CommonInstrumentation.Key_Dimension_ServiceVersion, SVNVersionInfo.AssemblyVersion),
                    new MetricDimension(CommonInstrumentation.Key_Dimension_HostName, machineHostName)
                });

            AppDomain.CurrentDomain.UnhandledException += PrintUnhandledException;

            ILogger bootstrapLogger = new ConsoleLogger(defaultComponentName, LogLevel.All, null);
            APP_DOMAIN_LOGGER = bootstrapLogger;
            NativePlatformUtils.SetGlobalResolver(new NativeLibraryResolverImpl());
            MetricCollector metrics = new MetricCollector(bootstrapLogger, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(60));
#if NETFRAMEWORK
            metrics.AddMetricSource(new WindowsPerfCounterReporter(
                bootstrapLogger,
                coreMetricDimensions,
                WindowsPerfCounterSet.BasicLocalMachine |
                WindowsPerfCounterSet.BasicCurrentProcess |
                WindowsPerfCounterSet.DotNetClrCurrentProcess));
#elif NETCOREAPP
            metrics.AddMetricSource(new WindowsPerfCounterReporter(
                bootstrapLogger,
                coreMetricDimensions,
                WindowsPerfCounterSet.BasicLocalMachine |
                WindowsPerfCounterSet.BasicCurrentProcess));
             metrics.AddMetricSource(new NetCorePerfCounterReporter(coreMetricDimensions));
#endif
            GarbageCollectionObserver gcObserver = new GarbageCollectionObserver(metrics, coreMetricDimensions);
            SystemThreadPoolObserver threadPoolObserver = new SystemThreadPoolObserver(metrics, coreMetricDimensions, bootstrapLogger.Clone("ThreadPoolObserver"));

            BufferPool<byte>.Metrics = metrics;
            BufferPool<byte>.MetricDimensions = coreMetricDimensions.Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_BufferPoolName, "byte"));
            BufferPool<float>.Metrics = metrics;
            BufferPool<float>.MetricDimensions = coreMetricDimensions.Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_BufferPoolName, "float"));
            BufferPool<string>.Metrics = metrics;
            BufferPool<string>.MetricDimensions = coreMetricDimensions.Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_BufferPoolName, "string"));

            string rootRuntimeDirectory = ServiceCommon.GetDurandalEnvironmentDirectory(args, bootstrapLogger);

            IFileSystem configFileSystem = new RealFileSystem(bootstrapLogger, rootRuntimeDirectory);
            bootstrapLogger.Log(string.Format("Durandal LanguageUnderstanding Engine v{0} built on {1}", SVNVersionInfo.VersionString, SVNVersionInfo.BuildDate));

            // Unpack bundle data if present
            ServiceCommon.UnpackBundleFile(bootstrapLogger, "bundle_lu.zip", rootRuntimeDirectory);

            bootstrapLogger.Log("Bootstrapping config...");
            IConfiguration mainConfig = await IniFileConfiguration.Create(bootstrapLogger.Clone("PrimaryConfig"), new VirtualPath("Durandal.LanguageUnderstanding_Config.ini"), configFileSystem, realTime, true, true);

            // Now see what loggers should actually be used, based on the config
            IThreadPool asyncLoggingThreadPool = new FixedCapacityThreadPool(
                new TaskThreadPool(new WeakPointer<IMetricCollector>(metrics), coreMetricDimensions, "AsyncLogging"),
                //new CustomThreadPool(bootstrapLogger, metrics, coreMetricDimensions, ThreadPriority.Normal, "AsyncLogThreadPool", 8),
                bootstrapLogger,
                metrics,
                coreMetricDimensions,
                "AsyncLogThreadPool",
                8,
                ThreadPoolOverschedulingBehavior.BlockUntilThreadsAvailable);

            IRandom cryptoRandom = new CryptographicRandom();
            IAESDelegates aesImpl = new SystemAESDelegates();
            IRSADelegates rsaImpl = new StandardRSADelegates(cryptoRandom);
            IStringEncrypterPii piiEncrypter = new NullStringEncrypter();

            if (mainConfig.ContainsKey("piiEncryptionKey"))
            {
                PublicKey rsaKey = PublicKey.ReadFromXml(mainConfig.GetString("piiEncryptionKey"));
                DataPrivacyClassification privacyClassesToEncrypt = (DataPrivacyClassification)mainConfig.GetInt32(
                    "privacyClassesToEncrypt",
                    (int)(DataPrivacyClassification.EndUserIdentifiableInformation | DataPrivacyClassification.PrivateContent | DataPrivacyClassification.PublicPersonalData));

                if (privacyClassesToEncrypt != DataPrivacyClassification.Unknown)
                {
                    bootstrapLogger.Log("RSA PII encrypter is enabled");
                    bootstrapLogger.Log("Privacy classes to encrypt: " + privacyClassesToEncrypt.ToString());
                    piiEncrypter = new RsaStringEncrypterPii(
                        rsaImpl,
                        aesImpl,
                        cryptoRandom,
                        realTime,
                        privacyClassesToEncrypt,
                        rsaKey);
                }
            }
            else
            {
                bootstrapLogger.Log("PII encryption is disabled (no RSA public key specified)");
            }

            ILogger coreLogger = ServiceCommon.CreateAggregateLogger(
                defaultComponentName,
                mainConfig,
                bootstrapLogger,
                asyncLoggingThreadPool,
                metrics,
                coreMetricDimensions,
                piiEncrypter,
                realTime,
                rootRuntimeDirectory);

            if (coreLogger == null)
            {
                bootstrapLogger.Log("It looks like all loggers are turned off! I'll just be quiet then...");
                coreLogger = NullLogger.Singleton;
            }
            else
            {
                APP_DOMAIN_LOGGER = coreLogger;
            }

            IFileSystem fileSystem = new RealFileSystem(coreLogger, rootRuntimeDirectory);

            coreLogger.Log("Configuring runtime environment...");
            System.Runtime.GCSettings.LatencyMode = System.Runtime.GCLatencyMode.SustainedLowLatency;
            ServicePointManager.Expect100Continue = false;
            DefaultRealTimeProvider.HighPrecisionWaitProvider = new Win32HighPrecisionWaitProvider();
            AssemblyReflector.ApplyAccelerators(typeof(CRC32CAccelerator).Assembly, coreLogger); // Extensions.Compression

            metrics.AddMetricOutput(new FileMetricOutput(coreLogger, Process.GetCurrentProcess().ProcessName, Path.Combine(rootRuntimeDirectory, "logs"), 10485760));
            if (!string.IsNullOrEmpty(mainConfig.GetString("appInsightsConnectionString")))
            {
                metrics.AddMetricOutput(new AppInsightsMetricOutput(coreLogger, mainConfig.GetString("appInsightsConnectionString")));
            }

            coreLogger.Log("Starting language understanding service...");
            //IThreadPool luThreadPool = new CustomThreadPool(coreLogger.Clone("LUCoreThreadPool"), metrics, coreMetricDimensions, ThreadPriority.Normal, "LUCore");
            IThreadPool luThreadPool = new TaskThreadPool(new WeakPointer<IMetricCollector>(metrics), coreMetricDimensions, "LUCore");
            IHttpClientFactory annotatorHttpClientFactory = new PortableHttpClientFactory(new WeakPointer<IMetricCollector>(metrics), coreMetricDimensions);
            IAnnotatorProvider annotatorProvider = new HardcodedAnnotatorProvider(
                annotatorHttpClientFactory,
                fileSystem,
                bingMapsApiKey: mainConfig.GetString("bingMapsApiKey"),
                bingLocalApiKey: mainConfig.GetString("bingLocalApiKey"),
                bingSpellerApiKey: mainConfig.GetString("bingSpellerApiKey"));
            LanguageUnderstandingEngine core = new LanguageUnderstandingEngine(
                new LUConfiguration(new WeakPointer<IConfiguration>(mainConfig), coreLogger.Clone("LUConfig")),
                coreLogger.Clone("LUCore"),
                fileSystem,
                annotatorProvider,
                luThreadPool);

            if (!mainConfig.ContainsKey("answerDomains"))
            {
                coreLogger.Log("No answer domains are specified in the config file! Nothing will be loaded.");
            }

            IPackageLoader packageLoader = new PortableZipPackageFileLoader(coreLogger.Clone("PackageLoader"));
            PackageInstaller packageInstaller = await PackageInstaller.Create(fileSystem, packageLoader, coreLogger.Clone("PackageInstaller"), realTime, PackageComponent.LU);
            await packageInstaller.InitializePackages();

            ICultureInfoFactory cultureInfoFactory = new WindowsCultureInfoFactory();
            IList<string> rawEnabledLocales = mainConfig.GetStringList("answerLocales");
            IList<LanguageCode> enabledLocales = new List<LanguageCode>();
            foreach (string locale in rawEnabledLocales)
            {
                enabledLocales.Add(LanguageCode.Parse(locale));
            }

            core.Initialize(enabledLocales, cultureInfoFactory, realTime);

            //ThreadPool httpThreadPool = new CustomThreadPool(coreLogger.Clone("ThreadPool"), metrics, coreMetricDimensions, ThreadPriority.Normal, "LUHTTP");
            IThreadPool httpThreadPool = new TaskThreadPool(new WeakPointer<IMetricCollector>(metrics), coreMetricDimensions, "LUHTTP");
            List<ILUTransportProtocol> transportProtocols = new List<ILUTransportProtocol>()
            {
                new LUJsonTransportProtocol(),
                new LUBondTransportProtocol()
            };
            
            // Build the http server
            IHttpServer serverTransportBase;
            IList<string> rawEndpoints = mainConfig.GetStringList("luServerEndpoints");
            IList<ServerBindingInfo> parsedEndpoints = ServerBindingInfo.ParseBindingList(rawEndpoints);
            ILogger httpLogger = coreLogger.Clone("DialogHttpServerBase");
            
            if (string.Equals(mainConfig.GetString("httpServerImpl"), "listener", StringComparison.OrdinalIgnoreCase))
            {
#if NETFRAMEWORK
                coreLogger.Log("Initializing HTTP listener server");
                serverTransportBase = new ListenerHttpServer(parsedEndpoints, coreLogger.Clone("HttpServerBase"), new WeakPointer<IThreadPool>(httpThreadPool));
#else
                throw new PlatformNotSupportedException(".Net Core service does not support HTTP listener server. Use Kestrel instead");
#endif
            }
            else if (string.Equals(mainConfig.GetString("httpServerImpl"), "kestrel", StringComparison.OrdinalIgnoreCase))
            {
#if NETCOREAPP
                coreLogger.Log("Initializing HTTP kestrel server");
                serverTransportBase = new KestrelHttpServer(parsedEndpoints, httpLogger);
#else
                throw new PlatformNotSupportedException(".Net Framework service does not support HTTP kestrel server. Use Listener instead");
#endif
            }
            else
            {
                coreLogger.Log("Initializing HTTP socket server");
                serverTransportBase = new SocketHttpServer(
                    new RawTcpSocketServer(
                        parsedEndpoints,
                        coreLogger.Clone("SocketServerBase"),
                        realTime,
                        new WeakPointer<IMetricCollector>(metrics),
                        coreMetricDimensions,
                        new WeakPointer<IThreadPool>(httpThreadPool)),
                    coreLogger.Clone("HttpServerBase"),
                    new CryptographicRandom(),
                    new WeakPointer<IMetricCollector>(metrics),
                    coreMetricDimensions);
            }

            LUHttpServer serverTransport = new LUHttpServer(
                core,
                mainConfig,
                serverTransportBase,
                coreLogger.Clone("LUHttpServer"),
                transportProtocols,
                fileSystem,
                packageLoader,
                metrics,
                coreMetricDimensions,
                machineHostName);

            await serverTransport.StartServer("LUHTTP", CancellationToken.None, DefaultRealTimeProvider.Singleton);

            foreach (LanguageCode locale in enabledLocales)
            {
                ISet<string> domains = new HashSet<string>();
                
                // If there is no locale-specific domain config this will fall back to the default
                foreach (string enabledDomain in mainConfig.GetStringList("answerDomains", new SmallDictionary<string, string>() { { "locale", locale.ToBcp47Alpha2String() } }))
                {
                    if (!domains.Contains(enabledDomain))
                    {
                        domains.Add(enabledDomain);
                    }
                }

                // Check if the enabled domain list is empty or "*".
                // If so, tell the model to load everything
                if (domains.Count == 0 || domains.Count == 1 && domains.First().Equals("*"))
                {
                    core.LoadModels(locale, null);
                }
                else
                {
                    core.LoadModels(locale, domains);
                }
            }

            coreLogger.Log("LU server is started and ready to process queries on " + string.Join("|", serverTransport.Endpoints));
            Task backgroundJitTask = Jitter.JitAssembly(coreLogger.Clone("JIT"), Assembly.GetEntryAssembly(), TimeSpan.FromSeconds(60));

            if (Environment.UserInteractive)
            {
                // Wait for initial model load to finish
                while (!core.AnyModelLoaded)
                {
                    await Task.Delay(500);
                }

                string clientId = StringUtils.HashToGuid(Environment.MachineName).ToString("N");

                ILogger queryLogger = coreLogger.Clone("TestConsole");
                
                queryLogger.Log("Test console enabled");
                queryLogger.Log("Test console client ID is " + clientId);
                queryLogger.Log(" \"/quit\" - exit");
                queryLogger.Log(" \"/nuke\" - delete all cached models");
                queryLogger.Log(" \"/perf\" - run performance test");
                queryLogger.Log(" \"/validate {locale-id}\" - run validation against models");
                queryLogger.Log(" \"/locale {locale-id}\" - change query locale");
                queryLogger.Log(" (any other input) - Run a specific text query");
                //ILUClient client = new NativeLuClient(core);
                ILUClient client = new LUHttpClient(
                    new SocketHttpClient(
                        new WeakPointer<ISocketFactory>(new TcpClientSocketFactory(queryLogger.Clone("LUSocketFactory"))),
                        serverTransport.LocalAccessUri,
                        queryLogger.Clone("LUHttp"),
                        new WeakPointer<IMetricCollector>(metrics),
                        coreMetricDimensions,
                        Http2SessionManager.Default,
                        new Http2SessionPreferences()),
                    queryLogger.Clone("TestConsoleClient"),
                    new LUBondTransportProtocol());
                bool running = true;
                while (running)
                {
                    string input = Console.ReadLine();
                    if (input.Equals("/perf", StringComparison.OrdinalIgnoreCase))
                    {
                        Flood(coreLogger, client, clientId);
                    }
                    else if (input.StartsWith("validate", StringComparison.OrdinalIgnoreCase))
                    {
                        core.ValidateModels(_consoleLocale);
                    }
                    else if (input.Equals("/quit", StringComparison.OrdinalIgnoreCase) ||
                        input.Equals("/exit", StringComparison.OrdinalIgnoreCase) ||
                        input.Equals("/q", StringComparison.OrdinalIgnoreCase))
                    {
                        running = false;
                    }
                    else if (input.StartsWith("/locale ", StringComparison.OrdinalIgnoreCase))
                    {
                        LanguageCode nextLocale = LanguageCode.TryParse(input.Substring(input.IndexOf(' ') + 1));
                        if (nextLocale != null)
                        {
                            _consoleLocale = nextLocale;
                            queryLogger.Log("Console locale changed to " + nextLocale);
                        }
                        else
                        {
                            queryLogger.Log("New locales must follow the pattern xx-xx", LogLevel.Err);
                        }
                    }
                    else if (input.Equals("/reload", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (LanguageCode locale in enabledLocales)
                        {
                            ISet<string> domains = new HashSet<string>();
                            foreach (string enabledDomain in mainConfig.GetStringList("answerDomains", new SmallDictionary<string, string>() { { "locale", locale.ToBcp47Alpha2String() } }))
                            {
                                domains.Add(enabledDomain);
                            }

                            core.LoadModels(locale, domains);
                        }
                    }
                    else if (input.Equals("/nuke", StringComparison.OrdinalIgnoreCase))
                    {
                        queryLogger.Log("Nuking all cache and model directories...");
                        try
                        {
                            DirectoryInfo modelDir = new DirectoryInfo(RuntimeDirectoryName.MODEL_DIR);
                            if (modelDir.Exists)
                            {
                                Console.Write(modelDir.FullName + "...");
                                modelDir.Delete(true);
                                Console.WriteLine("Deleted");
                            }

                            DirectoryInfo cacheDir = new DirectoryInfo(RuntimeDirectoryName.CACHE_DIR);
                            if (cacheDir.Exists)
                            {
                                Console.Write(cacheDir.FullName + "...");
                                cacheDir.Delete(true);
                                Console.WriteLine("Deleted");
                            }
                        }
                        catch (Exception e)
                        {
                            queryLogger.Log(e, LogLevel.Err);
                        }
                    }
                    else
                    {
                        ClientContext fakeContext = new ClientContext()
                            {
                                ClientId = clientId,
                                UserId = clientId,
                                Locale = _consoleLocale,
                                ReferenceDateTime = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ss")
                            };
                        
                        // Run a test query
                        LURequest request = new LURequest()
                            {
                                DoFullAnnotation = true,
                                Context = fakeContext,
                                TraceId = CommonInstrumentation.FormatTraceId(Guid.NewGuid()),
                                RequestFlags = (QueryFlags.Debug),
                                TextInput = input,
                                ContextualDomains = new List<string>(core.LoadedDomains),
                            };

                        using (NetworkResponseInstrumented<LUResponse> response = client.MakeQueryRequest(request, queryLogger).Await())
                        {
                            if (response == null || response.Response == null)
                            {
                                queryLogger.Log("Null response!");
                            }
                            else if (response.Response.Results.Count == 0)
                            {
                                queryLogger.Log("No recognized phrases!");
                            }
                            else if (response.Response.Results[0].Recognition.Count == 0)
                            {
                                queryLogger.Log("No reco results!");
                            }
                            else
                            {
                                /*foreach (RecognizedPhrase result in response.Response.Results)
                                {
                                    queryLogger.Log("Classification results for \"" + result.Utterance + "\"");
                                    if (result.Results.Count > 0)
                                    {
                                        foreach (RecoResult reco in result.Results)
                                        {
                                            LogSemanticFrame(reco, queryLogger, LogLevel.Std);
                                        }
                                    }
                                }*/

                                queryLogger.Log("Latency = " + response.EndToEndLatency, LogLevel.Std);
                            }
                        }
                    }
                }
            }
            else
            {
                while (serverTransport.Running)
                {
                    // The http server will handle everything from here.
                    Thread.Sleep(1000);
                }
            }
        }

        private static void Flood(ILogger logger, ILUClient client, string clientId)
        {
            Stopwatch throughputTimer = new Stopwatch();
            throughputTimer.Start();
            TestRunResults results = RunFloodQueries(logger, client, clientId);
            throughputTimer.Stop();
            long elapsedTime = throughputTimer.ElapsedMilliseconds;
            Console.WriteLine("Total time was " + elapsedTime);
            Console.WriteLine("Avg latency was " + results.AvgLatency);
            double throughput = results.NumQueries * 1000 / elapsedTime;
            Console.WriteLine("Throughput was " + throughput);
            double failurePercent = results.NumFailures / (double)results.NumQueries;
            Console.WriteLine("Failure rate was " + failurePercent);
        }

        private static IThreadPool FLOOD_THREAD_POOL = null;

        private static TestRunResults RunFloodQueries(ILogger logger, ILUClient client, string clientId)
        {
            if (FLOOD_THREAD_POOL == null)
            {
                FLOOD_THREAD_POOL = new SystemThreadPool(logger.Clone("FloodThreadPool"), NullMetricCollector.Singleton, DimensionSet.Empty);
            }

            IList<ThreadedExecutor> executions = new List<ThreadedExecutor>();

            IList<string> queries = new List<string>();
            for (int c = 0; c < 1000; c++)
            {
                queries.Add("what time is it in Honolulu");
                queries.Add("the second movie");
                queries.Add("tell me a joke");
                queries.Add("turn off the front porch light");
                queries.Add("what color is violet");
            }

            foreach (string query in queries)
            {
                ThreadedExecutor executor = new ThreadedExecutor(query, NullLogger.Singleton, client, clientId);
                executions.Add(executor);
                FLOOD_THREAD_POOL.EnqueueUserAsyncWorkItem(executor.Run);
            }

            TestRunResults results = new TestRunResults();
            results.NumQueries = queries.Count;
            results.NumFailures = 0;
            StaticAverage latency = new StaticAverage();

            foreach (var executor in executions)
            {
                executor.Join();
                latency.Add(executor.Latency);
                if (!executor.Success)
                {
                    results.NumFailures++;
                }
            }

            results.AvgLatency = latency.Average;

            return results;
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
            public ILUClient Client;
            public string Query;
            public double Latency;
            public string ClientId;
            public bool Success = true;
            private EventWaitHandle _finished;
            private int _disposed = 0;

            public ThreadedExecutor(string query, ILogger logger, ILUClient client, string clientId)
            {
                Query = query;
                Logger = logger;
                Client = client;
                ClientId = clientId;
                _finished = new EventWaitHandle(false, EventResetMode.ManualReset);
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
                    ClientContext fakeContext = new ClientContext()
                    {
                        ClientId = ClientId,
                        UserId = ClientId,
                        Locale = _consoleLocale,
                        ReferenceDateTime = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ss")
                    };

                    LURequest request = new LURequest()
                    {
                        DoFullAnnotation = false,
                        Context = fakeContext,
                        RequestFlags = (QueryFlags.LogNothing),
                        TextInput = Query
                    };

                    using (NetworkResponseInstrumented<LUResponse> response = await Client.MakeQueryRequest(request))
                    {
                        if (response == null || response.Response == null || response.Response.Results == null || response.Response.Results.Count == 0)
                        {
                            Success = false;
                        }

                        Latency = response.EndToEndLatency;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
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

        private static void LogSemanticFrame(RecoResult result, ILogger logger, LogLevel level)
        {
            logger.Log(result.Domain + "/" + result.Intent + " : " + result.Confidence, level);
            if (result.TagHyps.Count > 0)
            {
                TaggedData data = result.MostLikelyTags;
                if (data.Slots.Count > 0)
                {
                    logger.Log("  Tag confidence " + data.Confidence, level);
                }
                foreach (KeyValuePair<string, string> note in data.Annotations)
                {
                    logger.Log("  Annotation: \"" + note.Key + "\" = \"" + note.Value + "\"", level);
                }
                foreach (SlotValue tag in data.Slots)
                {
                    logger.Log("  Slot Value: \"" + tag.Name + "\" = \"" + tag.Value + "\"", level);
                    foreach (KeyValuePair<string, string> note in tag.Annotations)
                    {
                        logger.Log("    Slot Annotation: \"" + note.Key + "\" = \"" + note.Value + "\"", level);
                    }
                }
            }
            for (int c = 1; c < result.TagHyps.Count; c++)
            {
                TaggedData data = result.TagHyps[c];
                logger.Log("  Alternate tag hypothesis " + c + ", confidence " + data.Confidence, level);
                foreach (SlotValue tag in data.Slots)
                {
                    logger.Log("    Slot Value: \"" + tag.Name + "\" = \"" + tag.Value + "\"", level);
                }
            }
        }

        private static void PrintUnhandledException(object source, UnhandledExceptionEventArgs args)
        {
            try
            {
                if (APP_DOMAIN_LOGGER != null)
                {
                    object exceptionObject = args.ExceptionObject;
                    if (exceptionObject is Exception)
                    {
                        Exception ex = exceptionObject as Exception;
                        APP_DOMAIN_LOGGER.Log(ex, LogLevel.Crt);
                    }

                    APP_DOMAIN_LOGGER.Log("A potentially fatal unhandled exception was raised in the AppDomain", LogLevel.Crt);
                }
            }
            catch (Exception) { }
        }
    }
}
