using Durandal.Common.MathExt;

namespace BVTTestDriver
{
    using Durandal;
    using Durandal.API;
    using Durandal.Extensions.BondProtocol;
    using Durandal.Common.Config;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Web;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.Logger;
    using Durandal.Common.LU;
    using Durandal.Common.Net;
    using Durandal.Common.Net.Http;
    using Durandal.Common.NLP.Train;
    using Durandal.Common.Utils;
    using Durandal.Common.File;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using Durandal.Common.Tasks;
    using System.Threading.Tasks;
        using Durandal.Common.Dialog.Runtime;
    using Durandal.Common.Time;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.Net.Http2;
    using Durandal.Common.ServiceMgmt;

    public class Program
    {
        public static void Main(string[] args)
        {
            AsyncMain(args).Await();
        }

        public static async Task AsyncMain(string[] args)
        {
            // Initialize and validate config
            ILogger testDriverLogger = new ConsoleLogger();
            IFileSystem localFileSystem = new RealFileSystem(testDriverLogger);
            ILogger errorOnlyLogger = new AggregateLogger(
                "BVTDriver",
                new TaskThreadPool(),
                new ConsoleLogger("BVTDriver", maxLevels: LogLevel.Err),
                new FileLogger(localFileSystem, "BVTDriver", maxLogLevels: LogLevel.Err));
            VirtualPath configFile = new VirtualPath("bvt_config");
            IConfiguration programConfig = await IniFileConfiguration.Create(testDriverLogger, configFile, localFileSystem, DefaultRealTimeProvider.Singleton, true);

            if (!programConfig.ContainsKey("dialogEngineDirectory"))
            {
                testDriverLogger.Log("Invalid config: dialogEngineDirectory not specified");
                return;
            }

            if (!programConfig.ContainsKey("luDirectory"))
            {
                testDriverLogger.Log("Invalid config: luDirectory not specified");
                return;
            }

            if (!programConfig.ContainsKey("bvtDirectory"))
            {
                testDriverLogger.Log("Invalid config: BVTDirectory not specified");
                return;
            }

            LanguageCode locale = LanguageCode.Parse(programConfig.GetString("locale", "en-US"));
            int threads = programConfig.GetInt32("testThreads", 8);
            ThreadPool.SetMaxThreads(threads, threads);

            DirectoryInfo dialogRuntimeFolder = new DirectoryInfo(programConfig.GetString("dialogEngineDirectory"));
           
            IFileSystem dialogResourceManager = new RealFileSystem(testDriverLogger, dialogRuntimeFolder.FullName);
            IConfiguration baseConfig = await IniFileConfiguration.Create(testDriverLogger, new VirtualPath("dialogEngine_config.ini"), dialogResourceManager, DefaultRealTimeProvider.Singleton, true);
            DialogConfiguration dialogConfig = new DialogConfiguration(new WeakPointer<IConfiguration>(baseConfig));
            DialogWebConfiguration dialogCoreConfig = new DialogWebConfiguration(new WeakPointer<IConfiguration>(baseConfig));

            // Create the test answer provider
            IDurandalPluginLoader pluginLoader = new ResidentDllPluginLoader(
                new BasicDialogExecutor(false),
                errorOnlyLogger,
                dialogResourceManager,
                new VirtualPath(RuntimeDirectoryName.PLUGIN_DIR),
                dialogResourceManager,
                PluginFrameworkLevel.NetFull);
            IDurandalPluginProvider pluginAnswerProvider = new MachineLocalPluginProvider(
                testDriverLogger,
                pluginLoader,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null);

            IDurandalPluginProvider wrapperAnswerProvider = new BvtWrapperPluginProvider(pluginAnswerProvider);

            VirtualPath bvtDirectory = new VirtualPath(programConfig.GetString("bvtDirectory"));

            InMemoryConversationStateCache mockConversationCache = new InMemoryConversationStateCache();

            DialogEngineParameters dialogParameters = new DialogEngineParameters(dialogConfig, new WeakPointer<IDurandalPluginProvider>(wrapperAnswerProvider))
            {
                Logger = errorOnlyLogger,
                ConversationStateCache = new WeakPointer<IConversationStateCache>(mockConversationCache),
            };

            DialogProcessingEngine dialogCore = new DialogProcessingEngine(dialogParameters);
            await dialogCore.LoadPlugins(dialogCoreConfig.PluginIdsToLoad, DefaultRealTimeProvider.Singleton);

            ILUTransportProtocol luProtocol = new LUBondTransportProtocol();

            TcpConnectionConfiguration tcpConfig = new TcpConnectionConfiguration()
            {
                DnsHostname = programConfig.GetString("luServerHost", "localhost"),
                Port = programConfig.GetInt32("luServerPort", 62291)
            };

            ILUClient luClient = new LUHttpClient(
                new SocketHttpClient(
                    new WeakPointer<ISocketFactory>(new TcpClientSocketFactory(errorOnlyLogger.Clone("SocketFactory"))),
                    tcpConfig,
                    errorOnlyLogger,
                    NullMetricCollector.WeakSingleton,
                    DimensionSet.Empty,
                    Http2SessionManager.Default,
                    new Http2SessionPreferences()),
                errorOnlyLogger,
                luProtocol);

            DirectoryInfo luFolder = new DirectoryInfo(programConfig.GetString("luDirectory"));
            IFileSystem luResourceManager = new RealFileSystem(testDriverLogger, luFolder.FullName);

            /////////////

            RunManualBVT(localFileSystem, bvtDirectory, dialogCore, luClient, mockConversationCache, locale, threads > 1);
            RunAutoBVT(testDriverLogger, luResourceManager, wrapperAnswerProvider, dialogCore, luClient, mockConversationCache, locale, threads > 1);

            /////////////

            Console.WriteLine("All tests completed.");
            Console.ReadKey();
        }

        private static void RunManualBVT(
            IFileSystem localResourceManager,
            VirtualPath bvtDirectory,
            DialogProcessingEngine dialogCore,
            ILUClient luClient,
            InMemoryConversationStateCache mockConversationCache,
            LanguageCode locale,
            bool threaded)
        {
            TestInputSet manualTestInputSet = new TestInputSet();

            foreach (VirtualPath bvtFile in localResourceManager.ListDirectories(bvtDirectory))
            {
                manualTestInputSet.AddBvtFile(bvtFile, localResourceManager);
            }
            
            Console.WriteLine("Running Manual BVT tests...");

            Stopwatch throughputTimer = new Stopwatch();
            throughputTimer.Start();
            IList<SingleTestResult> allTestResults = TestRunner.RunAllTests(dialogCore, luClient, manualTestInputSet, mockConversationCache, locale, threaded);
            throughputTimer.Stop();

            Console.WriteLine("Finished!");

            IDictionary<string, DomainTestResults> domainTestResults = ConvertToDomainTestResults(allTestResults);

            PrintTestResults(manualTestInputSet, allTestResults, domainTestResults, throughputTimer.ElapsedMilliseconds);
            PrintFailures(allTestResults);
        }

        private static void RunAutoBVT(
            ILogger logger,
            IFileSystem luResourceManager,
            IDurandalPluginProvider wrapperAnswerProvider,
            DialogProcessingEngine dialogCore,
            ILUClient luClient,
            InMemoryConversationStateCache mockConversationCache,
            LanguageCode locale,
            bool threaded)
        {
            TestInputSet autoTestInputSet = new TestInputSet();

            IList<TrainingDataTemplate> allTrainingData = AutoBvtGenerator.ParseTrainingFiles(luResourceManager, locale, logger);

            foreach (PluginStrongName pluginName in dialogCore.GetLoadedPlugins())
            {
                DurandalPlugin plugin = null;// wrapperAnswerProvider.GetPlugin(pluginName);
                if (plugin == null)
                {
                    throw new NotImplementedException("BVTs are out of maintenance");
                }

                AutoBvtGenerator generator = new AutoBvtGenerator(logger, plugin, allTrainingData);
                IList<IList<TestUtterance>> thisDomainInput = generator.GenerateConversations(500);
                if (thisDomainInput.Count > 0)
                {
                    autoTestInputSet.AddData(thisDomainInput);
                }
            }

            Console.WriteLine("Running Auto BVT tests...");

            Stopwatch throughputTimer = new Stopwatch();
            throughputTimer.Start();
            IList<SingleTestResult> allTestResults = TestRunner.RunAllTests(dialogCore, luClient, autoTestInputSet, mockConversationCache, locale, threaded);
            throughputTimer.Stop();

            Console.WriteLine("Finished!");

            IDictionary<string, DomainTestResults> domainTestResults = ConvertToDomainTestResults(allTestResults);

            PrintTestResults(autoTestInputSet, allTestResults, domainTestResults, throughputTimer.ElapsedMilliseconds);
            //PrintFailures(allTestResults);
        }

        private static IDictionary<string, DomainTestResults> ConvertToDomainTestResults(IList<SingleTestResult> individualResults)
        {
            IDictionary<string, DomainTestResults> returnVal = new Dictionary<string, DomainTestResults>();
            
            foreach (SingleTestResult result in individualResults)
            {
                if (!returnVal.ContainsKey(result.Domain))
                {
                    returnVal[result.Domain] = new DomainTestResults()
                    {
                        Domain = result.Domain
                    };
                }

                DomainTestResults thisDomainResults = returnVal[result.Domain];
                thisDomainResults.TestsRun++;

                switch (result.ResultCode)
                {
                    case TestResultCode.Success:
                        thisDomainResults.TestsSucceeded++;
                        if (result.ContainsTags)
                        {
                            thisDomainResults.AddPrecisionRecall(result.TaggerPrecision, result.TaggerRecall);
                        }
                        break;
                    case TestResultCode.DialogError:
                        thisDomainResults.FailedByDialogError++;
                        thisDomainResults.TestsFailed++;
                        break;
                    case TestResultCode.NoQasResult:
                        thisDomainResults.FailedByNoQasResults++;
                        thisDomainResults.TestsFailed++;
                        break;
                    case TestResultCode.QasTimeout:
                        thisDomainResults.FailedByQasTimeout++;
                        thisDomainResults.TestsFailed++;
                        break;
                    case TestResultCode.QuerySkipped:
                        thisDomainResults.FailedByQuerySkipped++;
                        thisDomainResults.TestsFailed++;
                        break;
                    case TestResultCode.WrongDomain:
                        thisDomainResults.FailedByWrongDomainAndIntent++;
                        thisDomainResults.TestsFailed++;
                        break;
                    case TestResultCode.WrongIntent:
                        thisDomainResults.FailedByWrongIntent++;
                        thisDomainResults.TestsFailed++;
                        break;
                }
            }

            return returnVal;
        }

        private static void PrintFailures(IList<SingleTestResult> individualResults)
        {
            foreach (SingleTestResult result in individualResults)
            {
                if (result.ResultCode != TestResultCode.Success && result.FailedInput != null)
                {
                    if (!string.IsNullOrEmpty(result.ActualDomainIntent))
                    {
                        Console.WriteLine("Input \"{0}\" failed with result {1} (expected {2}/{3}, got {4})",
                            result.FailedInput.Input,
                            result.ResultCode.ToString(),
                            result.FailedInput.ExpectedDomain,
                            result.FailedInput.ExpectedIntent,
                            result.ActualDomainIntent);
                    }
                    else
                    {
                        Console.WriteLine("Input \"{0}\" failed with result {1} (expected {2}/{3})",
                            result.FailedInput.Input,
                            result.ResultCode.ToString(),
                            result.FailedInput.ExpectedDomain,
                            result.FailedInput.ExpectedIntent);
                    }
                }
            }
        }

        private static void PrintTestResults(TestInputSet testInputSet, IList<SingleTestResult> individualResults, IDictionary<string, DomainTestResults> domainResults, long elapsedMilliseconds)
        {
            if (elapsedMilliseconds <= 0)
                elapsedMilliseconds = 1;

            double numUtterances = testInputSet.NumUtterances;
            double testLatency = elapsedMilliseconds;
            double qps = 1000 * numUtterances / testLatency;

            Console.WriteLine("Total time was {0} ms", testLatency);

            Console.WriteLine("Total queries run: {0}", numUtterances);

            Console.WriteLine("Total throughput was {0} qps", qps);

            double totalLatency = 0;
            foreach (SingleTestResult result in individualResults)
            {
                totalLatency += result.Latency;
            }

            double averageMs = totalLatency / individualResults.Count;

            Console.WriteLine("Average query latency was " + averageMs + " ms");

            int longestDomain = GetLongestStringLength(domainResults.Keys);

            foreach (DomainTestResults results in domainResults.Values)
            {
                if (results.HasTags())
                {
                    Console.WriteLine(
                        "{0,-" + longestDomain + "}  Tests={1,3}  Success={2,3}  Fail={3,3}  Rate={4,6:F1}%  Prec={5:F3}  Rec={6:F3}  F1={7:F3}",
                        results.Domain,
                        results.TestsRun,
                        results.TestsSucceeded,
                        results.TestsFailed,
                        results.GetSuccessPercentage(),
                        results.GetPrecision(),
                        results.GetRecall(),
                        results.GetF1());
                }
                else
                {
                    Console.WriteLine(
                        "{0,-" + longestDomain + "}  Tests={1,3}  Success={2,3}  Fail={3,3}  Rate={4,6:F1}%",
                        results.Domain,
                        results.TestsRun,
                        results.TestsSucceeded,
                        results.TestsFailed,
                        results.GetSuccessPercentage());
                }
            }

            StaticAverage averagePrecision = new StaticAverage();
            StaticAverage averageF1 = new StaticAverage();
            float totalTests = 0;
            float totalSuccesses = 0;
            foreach (DomainTestResults results in domainResults.Values)
            {
                totalTests += results.TestsRun;
                totalSuccesses += results.TestsSucceeded;
                if (results.HasTags())
                {
                    averagePrecision.Add(results.GetPrecision());
                    averageF1.Add(results.GetF1());
                }
            }

            if (totalTests > 0)
            {
                Console.WriteLine("Overall success rate: {0:F2}%", (100f * totalSuccesses / totalTests));
                Console.WriteLine("Overall precision: {0:F2}%", (100f * averagePrecision.Average));
                Console.WriteLine("Overall F-1 score: {0:F2}%", (100f * averageF1.Average));
                Console.WriteLine("Total time was {0}ms. (Average time {1}ms)",
                    elapsedMilliseconds,
                    ((float)elapsedMilliseconds / totalTests));
            }
        }

        private static int GetLongestStringLength(IEnumerable<string> strings)
        {
            int longest = 0;
            foreach (string x in strings)
            {
                longest = Math.Max(longest, x.Length);
            }
            return longest;
        }
    }
}
