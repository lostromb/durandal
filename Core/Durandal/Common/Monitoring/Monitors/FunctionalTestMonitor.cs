using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Config;
using Durandal.Common.Dialog.Web;
using Durandal.Common.File;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Test.FVT;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Newtonsoft.Json;

namespace Durandal.Common.Monitoring.Monitors
{
    public class FunctionalTestMonitor : IServiceMonitor
    {
        private static readonly TimeSpan BETWEEN_TURNS_INTERVAL = TimeSpan.FromSeconds(1);

        private readonly IFunctionalTestIdentityStore _testIdentityStore;
        private FunctionalTest _testDefinition;
        private Uri _dialogServiceUrl;
        private IDialogTransportProtocol _dialogProtocol;
        private ILogger _queryInstrumentationLogger;
        private FunctionalTestDriver _testDriver;
        private IHttpClientFactory _httpClientFactory;

        private string _fvtFileName;
        private string _testName;
        private string _testSuiteName;
        private string _testDescription;
        private float? _passRateThreshold;
        private TimeSpan? _latencyPerTurnThreshold;
        private TimeSpan? _latencyTotalThreshold;
        private TimeSpan _testInterval;
        private int _disposed = 0;

        public FunctionalTestMonitor(
            string fvtFileName,
            Uri dialogServiceUrl,
            IDialogTransportProtocol dialogProtocol,
            ILogger queryInstrumentationLogger,
            IFunctionalTestIdentityStore testIdentityStore,
            string testSuiteName,
            float? passRateThreshold = null,
            TimeSpan? latencyPerTurnThreshold = null)
        {
            _fvtFileName = fvtFileName;
            _dialogProtocol = dialogProtocol;
            _dialogServiceUrl = dialogServiceUrl;
            _testIdentityStore = testIdentityStore;
            _queryInstrumentationLogger = queryInstrumentationLogger;
            _passRateThreshold = passRateThreshold;
            _testSuiteName = testSuiteName;
            _latencyPerTurnThreshold = latencyPerTurnThreshold;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~FunctionalTestMonitor()
        {
            Dispose(false);
        }
#endif

        public string TestName => _testName;

        public string TestSuiteName => _testSuiteName;

        public string TestDescription => _testDescription;

        public float? PassRateThreshold => _passRateThreshold;

        public TimeSpan? LatencyThreshold => _latencyTotalThreshold;

        public TimeSpan QueryInterval => _testInterval;

        public string ExclusivityKey => "fvt:" + _dialogServiceUrl.Host;

        public async Task<bool> Initialize(
            IConfiguration environmentConfig,
            Guid machineLocalGuid,
            IFileSystem localFileSystem,
            IHttpClientFactory httpClientFactory,
            WeakPointer<ISocketFactory> socketFactory,
            WeakPointer<IMetricCollector> metrics,
            DimensionSet metricDimensions)
        {

            // Load the FVT definition file
            if (!environmentConfig.ContainsKey("fvtDirectory"))
            {
                return false;
            }

            VirtualPath fvtDir = new VirtualPath(environmentConfig.GetString("fvtDirectory"));
            VirtualPath fvtFile = fvtDir.Combine(_fvtFileName);
            if (!localFileSystem.Exists(fvtFile))
            {
                return false;
            }

            using (Stream fileReadStream = await localFileSystem.OpenStreamAsync(fvtFile, FileOpenMode.Open, FileAccessMode.Read).ConfigureAwait(false))
            {
                using (StreamReader streamReader = new StreamReader(fileReadStream))
                {
                    using (JsonReader reader = new JsonTextReader(streamReader))
                    {
                        JsonSerializer deserializer = JsonSerializer.Create();
                        _testDefinition = deserializer.Deserialize<FunctionalTest>(reader);
                    }
                }
            }

            _testName = "Unknown FVT Test";
            if (_testDefinition.Metadata != null &&
                !string.IsNullOrEmpty(_testDefinition.Metadata.TestName))
            {
                _testName = _testDefinition.Metadata.TestName;
            }

            _testInterval = TimeSpan.FromSeconds(10);
            if (_testDefinition.Metadata != null &&
                _testDefinition.Metadata.SuggestedTestInterval.HasValue)
            {
                _testInterval = _testDefinition.Metadata.SuggestedTestInterval.Value;
            }

            // TODO: Fill out the test description better using the contents of the test definition
            _testDescription = "Runs a functional validation test";

            if (_latencyPerTurnThreshold.HasValue)
            {
                long thresholdTicks = _testDefinition.Turns.Count * _latencyPerTurnThreshold.Value.Ticks;

                // The reported test latency no longer factors in delay between turns, so we don't calculate it any more.
                //for (int turnNum = 1; turnNum < _testDefinition.Turns.Count; turnNum++)
                //{
                //    // Factor in default + specific predelay per-turn
                //    FunctionalTestTurn turn = _testDefinition.Turns[turnNum];
                //    thresholdTicks += turn.PreDelay.GetValueOrDefault(BETWEEN_TURNS_INTERVAL).Ticks;
                //}

                _latencyTotalThreshold = TimeSpan.FromTicks(thresholdTicks);
            }
            else
            {
                _latencyTotalThreshold = null;
            }

            //ISocketFactory socketFactory = new PooledTcpClientSocketFactory(logger.Clone("SocketFactory"));
            //ISocketFactory socketFactory = new RawTcpSocketFactory(logger.Clone("SocketFactory"));
            //IHttpClientFactory httpClientFactory = new SocketHttpClientFactory(socketFactory, DefaultRealTimeProvider.Singleton);
            _httpClientFactory = httpClientFactory;
            IHttpClient httpClient = _httpClientFactory.CreateHttpClient(_dialogServiceUrl, _queryInstrumentationLogger.Clone("HttpClient"));
            _testDriver = new FunctionalTestDriver(_queryInstrumentationLogger, httpClient, _dialogProtocol, _testIdentityStore, BETWEEN_TURNS_INTERVAL);

            return true;
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
                _testDriver?.Dispose();
            }
        }

        public async Task<SingleTestResult> Run(Guid traceId, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            FunctionalTestResult allTestResult = await _testDriver.RunTest(_testDefinition, cancelToken, realTime, traceId).ConfigureAwait(false);

            // Did the test fail?
            bool testPassed = true;
            string errorMessage = null;
            foreach (var turnResult in allTestResult.TurnResults)
            {
                if (!turnResult.ValidationResult.ValidationPassed)
                {
                    testPassed = false;
                    errorMessage = turnResult.ValidationResult.FailureReason;
                }
            }

            return new SingleTestResult()
            {
                Success = testPassed,
                ErrorMessage = errorMessage,
                OverrideTestExecutionTime = allTestResult.ActualTimeSpentInTests
            };
        }
    }
}
