using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Durandal.Common.IO;
using Durandal.Common.MathExt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Tests.Perf.TestCases
{
    [TestClass]
    [DoNotParallelize]
    [TestCategory("PerfRegression")]
    public class Base64EncodingPerfTests
    {
        private static Summary? _benchResults;

        private readonly int DataLength = 100000;
        private byte[]? _inputData;
        private char[]? _outputChars;
        private byte[]? _outputData;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _benchResults = BenchmarkRunner.Run<Base64EncodingPerfTests>(new RegressionTestCommon.PerfRegressionInProcessConfig());
        }

        [GlobalSetup]
        public void BenchmarkSetup()
        {
            _inputData = new byte[DataLength];
            _outputChars = new char[DataLength * 2];
            _outputData = new byte[DataLength * 2];
            FastRandom.Shared.NextBytes(_inputData);
        }

        [Benchmark]
        public void DurandalBase64Write()
        {
            using (Base64AsciiEncodingStream encoder = new Base64AsciiEncodingStream(EmptyStream.Singleton, StreamDirection.Write, false))
            {
                encoder.Write(_inputData.AsSpan());
                encoder.Finish(CancellationToken.None, DefaultRealTimeProvider.Singleton);
            }
        }

        [Benchmark]
        public void DurandalBase64Read()
        {
            _inputData.AssertNonNull(nameof(_inputData));
            using (MemoryStream source = new MemoryStream(_inputData))
            using (Base64AsciiEncodingStream encoder = new Base64AsciiEncodingStream(source, StreamDirection.Read, false))
            {
                int bytesRead;
                do
                {
                    bytesRead = encoder.Read(_outputData.AsSpan());
                } while (bytesRead > 0);
            }
        }

        [Benchmark]
        public void SystemBase64()
        {
            _inputData.AssertNonNull(nameof(_inputData));
            _outputChars.AssertNonNull(nameof(_inputData));
            _outputData.AssertNonNull(nameof(_inputData));
            int charsWritten = Convert.ToBase64CharArray(_inputData, 0, DataLength, _outputChars, 0);
            Encoding.ASCII.GetBytes(_outputChars, 0, charsWritten, _outputData, 0);
        }

        [TestMethod]
        public void TestBase64EncodeStreamRead_Regression()
        {
            _benchResults.AssertNonNull(nameof(_benchResults));
            RegressionTestCommon.AssertTestPerformedAboveThreshold(_benchResults, nameof(DurandalBase64Read), 3.0975);
            RegressionTestCommon.AssertMemoryAllocationBelowThreshold(_benchResults, nameof(DurandalBase64Read), 232);
        }

        [TestMethod]
        public void TestBase64EncodeStreamWrite_Regression()
        {
            _benchResults.AssertNonNull(nameof(_benchResults));
            RegressionTestCommon.AssertTestPerformedAboveThreshold(_benchResults, nameof(DurandalBase64Write), 2.7379);
            RegressionTestCommon.AssertMemoryAllocationBelowThreshold(_benchResults, nameof(DurandalBase64Write), 128);
        }

        [TestMethod]
        public void TestBase64EncodeStream_PerformsOnParWithRuntime()
        {
            _benchResults.AssertNonNull(nameof(_benchResults));
            TimeSpan timeForSystemBase64 = _benchResults.ElapsedTimeForTestCase(nameof(SystemBase64));
            TimeSpan timeForDurandalBase64Read = _benchResults.ElapsedTimeForTestCase(nameof(DurandalBase64Read));
            double slowerFactor = (timeForDurandalBase64Read / timeForSystemBase64);
            Assert.IsTrue(slowerFactor < 1.1,
                "Base64 encoder stream should have been no more than 1.1x slower than the runtime's baseline, instead it was " + slowerFactor + "x slower");
        }
    }
}
