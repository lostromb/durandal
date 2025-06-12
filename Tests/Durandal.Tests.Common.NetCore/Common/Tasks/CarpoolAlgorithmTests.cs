using Durandal.Common.Collections;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Tests.Common.Tasks;
using Durandal.Common.ServiceMgmt;
using System.Runtime.InteropServices;

namespace Durandal.Tests.Common.Tasks
{
    [TestClass]
    [DoNotParallelize]
    public class CarpoolAlgorithmTests
    {
        private const int CARPOOL_TEST_PASSES = 1000;
        private static readonly TimeSpan TEST_LIFETIME = TimeSpan.FromSeconds(30);

        private static IHighPrecisionWaitProvider _waitProvider;

        [ClassInitialize]
        public static void InitializeClass(TestContext context)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _waitProvider = new Win32HighPrecisionWaitProvider();
                DefaultRealTimeProvider.HighPrecisionWaitProvider = _waitProvider;
            }
            else
            {
                _waitProvider = new LowPrecisionWaitProvider();
            }
        }

        [ClassCleanup]
        public static void CleanupClass()
        {
            DefaultRealTimeProvider.HighPrecisionWaitProvider = new LowPrecisionWaitProvider();
            Thread.Sleep(100);
            _waitProvider?.Dispose();
        }

        [TestMethod]
        public async Task TestCarpoolAlgorithmSingleThread()
        {
            CarpoolAlgorithmTester tester = new CarpoolAlgorithmTester();
            CarpoolAlgorithm<int> algorithm = new CarpoolAlgorithm<int>(tester.GenerateNumber);

            for (int c = 2; c < CARPOOL_TEST_PASSES; c += 2)
            {
                RetrieveResult<int> result = await algorithm.WorkOnePhase<StubType, int>(c, StubType.Empty, tester.CheckEvenResult, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(c * 3, result.Result);
            }
        }

        [TestMethod]
        public async Task TestCarpoolAlgorithmSingleThreadAsyncRead()
        {
            CarpoolAlgorithmTester tester = new CarpoolAlgorithmTester();
            CarpoolAlgorithm<int> algorithm = new CarpoolAlgorithm<int>(tester.GenerateNumber);

            for (int c = 2; c < CARPOOL_TEST_PASSES; c += 2)
            {
                RetrieveResult<int> result = await algorithm.WorkOnePhase<StubType, int>(c, StubType.Empty, tester.CheckEvenResultAsync, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(c * 3, result.Result);
            }
        }

        [TestMethod]
        public async Task TestCarpoolAlgorithmThrowExceptionFromDriverBeforeWait()
        {
            CarpoolAlgorithmTester tester = new CarpoolAlgorithmTester();
            CarpoolAlgorithm<int> algorithm = new CarpoolAlgorithm<int>(tester.GenerateNumber);
            try
            {
                await algorithm.WorkOnePhase<StubType, int>(0, StubType.Empty, tester.CheckEvenResult, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                Assert.Fail("Should have thrown an ArgumentException");
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public async Task TestCarpoolAlgorithmThrowExceptionFromDriverAfterWait()
        {
            CarpoolAlgorithmTester tester = new CarpoolAlgorithmTester();
            CarpoolAlgorithm<int> algorithm = new CarpoolAlgorithm<int>(tester.GenerateNumber);
            try
            {
                await algorithm.WorkOnePhase<StubType, int>(-1, StubType.Empty, tester.CheckEvenResult, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                Assert.Fail("Should have thrown an ArgumentException");
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public async Task TestCarpoolAlgorithmCancelledDriver()
        {
            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                cts.Cancel();
                CarpoolAlgorithmTester tester = new CarpoolAlgorithmTester();
                CarpoolAlgorithm<int> algorithm = new CarpoolAlgorithm<int>(tester.GenerateNumber);
                try
                {
                    await algorithm.WorkOnePhase<StubType, int>(-1, StubType.Empty, tester.CheckEvenResult, cts.Token, DefaultRealTimeProvider.Singleton);
                    Assert.Fail("Should have thrown an OperationCanceledException");
                }
                catch (OperationCanceledException) { }
            }
        }

        [TestMethod]
        public async Task TestCarpoolAlgorithmMultiThreadSingleOutputQueue()
        {
            ILogger logger = new ConsoleLogger();
            CancellationTokenSource testAbort = new CancellationTokenSource();
            testAbort.CancelAfter(TEST_LIFETIME);
            CancellationToken testAbortToken = testAbort.Token;
            CarpoolAlgorithmTester tester = new CarpoolAlgorithmTester();
            CarpoolAlgorithm<int> algorithm = new CarpoolAlgorithm<int>(tester.GenerateNumber);

            using (IThreadPool threadPool = new TaskThreadPool())
            using (FixedCapacityThreadPool fixedCapacityPool = new FixedCapacityThreadPool(threadPool, NullLogger.Singleton, NullMetricCollector.Singleton, DimensionSet.Empty, "FixedThreadPool", maxCapacity: 4))
            {
                List<CarpoolTestThread> testThreads = new List<CarpoolTestThread>();
                for (int c = 2; c < CARPOOL_TEST_PASSES; c += 2)
                {
                    CarpoolTestThread testThread = new CarpoolTestThread(c, algorithm, testAbortToken, DefaultRealTimeProvider.Singleton, tester.CheckEvenResult);
                    testThreads.Add(testThread);
                }

                foreach (CarpoolTestThread testThread in testThreads)
                {
                    //logger.Log("Queueing work with input " + testThread.Input);
                    fixedCapacityPool.EnqueueUserAsyncWorkItem(testThread.Run);
                }

                await fixedCapacityPool.WaitForCurrentTasksToFinish(testAbortToken, DefaultRealTimeProvider.Singleton);

                foreach (CarpoolTestThread testThread in testThreads)
                {
                    Assert.AreEqual(IsEven(testThread.Input * 3), IsEven(testThread.ReturnVal));
                }
            }
        }

        [TestMethod]
        public async Task TestCarpoolAlgorithmMultiThreadMultipleOutputQueue()
        {
            ILogger logger = new ConsoleLogger();
            CancellationTokenSource testAbort = new CancellationTokenSource();
            testAbort.CancelAfter(TEST_LIFETIME);
            CancellationToken testAbortToken = testAbort.Token;
            CarpoolAlgorithmTester tester = new CarpoolAlgorithmTester();
            CarpoolAlgorithm<int> algorithm = new CarpoolAlgorithm<int>(tester.GenerateNumber);

            using (IThreadPool threadPool = new TaskThreadPool())
            using (FixedCapacityThreadPool fixedCapacityPool = new FixedCapacityThreadPool(threadPool, NullLogger.Singleton, NullMetricCollector.Singleton, DimensionSet.Empty, "FixedThreadPool", maxCapacity: 4))
            {
                List<CarpoolTestThread> testThreads = new List<CarpoolTestThread>();
                for (int c = 1; c < CARPOOL_TEST_PASSES; c += 1)
                {
                    CarpoolAlgorithm<int>.CheckForCompletedWorkDelegate<StubType, int> workDelegate;
                    if (IsEven(c * 3))
                    {
                        workDelegate = tester.CheckEvenResultAsync;
                    }
                    else
                    {
                        workDelegate = tester.CheckOddResultAsync;
                    }

                    CarpoolTestThread testThread = new CarpoolTestThread(c, algorithm, testAbortToken, DefaultRealTimeProvider.Singleton, workDelegate);
                    testThreads.Add(testThread);
                }

                foreach (CarpoolTestThread testThread in testThreads)
                {
                    //logger.Log("Queueing work with input " + testThread.Input);
                    fixedCapacityPool.EnqueueUserAsyncWorkItem(testThread.Run);
                }

                await fixedCapacityPool.WaitForCurrentTasksToFinish(testAbortToken, DefaultRealTimeProvider.Singleton);

                foreach (CarpoolTestThread testThread in testThreads)
                {
                    Assert.AreEqual(IsEven(testThread.Input * 3), IsEven(testThread.ReturnVal));
                }
            }
        }

        [TestMethod]
        public async Task TestCarpoolAlgorithmReentrant()
        {
            ILogger logger = new ConsoleLogger();
            CancellationTokenSource testAbort = new CancellationTokenSource();
            testAbort.CancelAfter(TEST_LIFETIME);
            CancellationToken testAbortToken = testAbort.Token;
            CarpoolAlgorithmTester tester = new CarpoolAlgorithmTester();
            CarpoolAlgorithm<int> algorithm = new CarpoolAlgorithm<int>(tester.GenerateNumber);

            List<CarpoolTestThread> testThreads = new List<CarpoolTestThread>();
            for (int c = 1; c < CARPOOL_TEST_PASSES; c += 1)
            {
                CarpoolAlgorithm<int>.CheckForCompletedWorkDelegate<StubType, int> workDelegate;
                if (IsEven(c * 3))
                {
                    workDelegate = tester.CheckEvenResultAsync;
                }
                else
                {
                    workDelegate = tester.CheckOddResultAsync;
                }

                CarpoolTestThread testThread = new CarpoolTestThread(c, algorithm, testAbortToken, DefaultRealTimeProvider.Singleton, workDelegate);
                testThreads.Add(testThread);
            }

            List<Task> tasks = new List<Task>();

            foreach (CarpoolTestThread testThread in testThreads)
            {
                //logger.Log("Queueing work with input " + testThread.Input);
                tasks.Add(testThread.Run());
            }

            foreach (Task task in tasks)
            {
                await task;
            }

            foreach (CarpoolTestThread testThread in testThreads)
            {
                Assert.AreEqual(IsEven(testThread.Input * 3), IsEven(testThread.ReturnVal));
            }
        }

        private static bool IsEven(int i)
        {
            return (i % 2) == 0;
        }

        private class CarpoolTestThread
        {
            private readonly int _producerInput;
            private readonly CarpoolAlgorithm<int> _algorithm;
            private readonly CancellationToken _cancelToken;
            private readonly IRealTimeProvider _realTime;
            private readonly CarpoolAlgorithm<int>.CheckForCompletedWorkDelegate<StubType, int> _workDelegate;
            private int _output;

            public CarpoolTestThread(
                int input,
                CarpoolAlgorithm<int> algorithm,
                CancellationToken cancelToken,
                IRealTimeProvider realTime,
                CarpoolAlgorithm<int>.CheckForCompletedWorkDelegate<StubType, int> workDelegate)
            {
                _producerInput = input;
                _algorithm = algorithm;
                _cancelToken = cancelToken;
                _realTime = realTime;
                _workDelegate = workDelegate;
            }

            public async Task Run()
            {
                RetrieveResult<int> rr = new RetrieveResult<int>();
                while (!rr.Success)
                {
                    rr = await _algorithm.WorkOnePhase<StubType, int>(_producerInput, StubType.Empty, _workDelegate, _cancelToken, _realTime).ConfigureAwait(false);
                }

                _output = rr.Result;
            }

            public int Input => _producerInput;
            public int ReturnVal => _output;
        }

        private class CarpoolAlgorithmTester
        {
            private readonly ConcurrentQueue<int> _oddOutput = new ConcurrentQueue<int>();
            private readonly ConcurrentQueue<int> _evenOutput = new ConcurrentQueue<int>();
            private int _threadInCriticalArea = 0;

            /// <summary>
            /// Asynchronous method which takes the input, multiplies it by 3, and sorts the result into one of
            /// two queues depending on whether it is even or odd.
            /// </summary>
            /// <param name="input"></param>
            /// <param name="cancelToken"></param>
            /// <param name="realTime"></param>
            /// <returns></returns>
            public async ValueTask GenerateNumber(int input, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                if (Interlocked.CompareExchange(ref _threadInCriticalArea, 1, 0) != 0)
                {
                    throw new InvalidOperationException("Multiple threads are inside the critical area");
                }

                if (input == 0)
                {
                    throw new ArgumentException("Exception before await");
                }

                await realTime.WaitAsync(TimeSpan.FromMilliseconds(1), cancelToken).ConfigureAwait(false);

                if (input < 0)
                {
                    throw new ArgumentException("Exception after await");
                }

                cancelToken.ThrowIfCancellationRequested();

                // Generate result
                int result = input * 3;

                // Sort result
                if (IsEven(result))
                {
                    _evenOutput.Enqueue(result);
                }
                else
                {
                    _oddOutput.Enqueue(result);
                }

                if (Interlocked.CompareExchange(ref _threadInCriticalArea, 0, 1) != 1)
                {
                    throw new InvalidOperationException("Multiple threads are leaving the critical area");
                }
            }

            public ValueTask<RetrieveResult<int>> CheckEvenResult(StubType taskInput, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                int returnVal;
                if (_evenOutput.TryDequeue(out returnVal))
                {
                    return new ValueTask<RetrieveResult<int>>(new RetrieveResult<int>(returnVal));
                }

                return new ValueTask<RetrieveResult<int>>(new RetrieveResult<int>());
            }

            public async ValueTask<RetrieveResult<int>> CheckEvenResultAsync(StubType taskInput, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                await realTime.WaitAsync(TimeSpan.FromMilliseconds(1), cancelToken).ConfigureAwait(false);

                int returnVal;
                if (_evenOutput.TryDequeue(out returnVal))
                {
                    return new RetrieveResult<int>(returnVal);
                }

                return new RetrieveResult<int>();
            }

            public ValueTask<RetrieveResult<int>> CheckOddResult(StubType taskInput, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                int returnVal;
                if (_oddOutput.TryDequeue(out returnVal))
                {
                    return new ValueTask<RetrieveResult<int>>(new RetrieveResult<int>(returnVal));
                }

                return new ValueTask<RetrieveResult<int>>(new RetrieveResult<int>());
            }

            public async ValueTask<RetrieveResult<int>> CheckOddResultAsync(StubType taskInput, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                await realTime.WaitAsync(TimeSpan.FromMilliseconds(1), cancelToken).ConfigureAwait(false);

                int returnVal;
                if (_oddOutput.TryDequeue(out returnVal))
                {
                    return new RetrieveResult<int>(returnVal);
                }

                return new RetrieveResult<int>();
            }
        }
    }
}
