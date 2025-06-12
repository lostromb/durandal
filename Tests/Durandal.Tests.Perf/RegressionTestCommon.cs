using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using Durandal.Common.Utils;

namespace Durandal.Tests.Perf
{
    /// <summary>
    /// Common code for creating automatic performance regression tests using BenchmarkDotNet
    /// </summary>
    public static class RegressionTestCommon
    {
        private static readonly object _baselineLock = new object();
        private static TimeSpan _baselineTime = TimeSpan.Zero;

        public static TimeSpan NanosecondsToTimeSpan(double ns)
        {
            return TimeSpan.FromTicks((long)(ns / 100));
        }

        public static long MemoryAllocationForTestCase(this Summary summary, string methodName)
        {
            return (long)GetReportForTestCase(summary, methodName).Metrics["Allocated Memory"].Value;
        }

        public static TimeSpan ElapsedTimeForTestCase(this Summary summary, string methodName)
        {
            var report = GetReportForTestCase(summary, methodName);
            report.ResultStatistics.AssertNonNull("ResultStatistics");
            return NanosecondsToTimeSpan(report.ResultStatistics.Mean);
        }

        public static bool WasTestRunWithoutOptimizations(this Summary summary)
        {
            return summary.ValidationErrors.Any((err) => err.Message.Contains("references non-optimized", StringComparison.Ordinal));
        }

        public static void AssertTestPerformedAboveThreshold(this Summary summary, string methodName, double baselineMagnitude)
        {
            bool noOptimizations = WasTestRunWithoutOptimizations(summary);
            if (noOptimizations)
            {
                Console.WriteLine("WARNING: Benchmarks are being run on non-optimized assemblies!");
            }

            TimeSpan absoluteTimeInTest = ElapsedTimeForTestCase(summary, methodName);
            TimeSpan baselineUnit = GetBaselineTime(1);
            double normalizedTimeInTest = absoluteTimeInTest / baselineUnit;
            TimeSpan absoluteTestThreshold = baselineUnit * baselineMagnitude;
            Console.WriteLine("Evaluating test runtime for method {0}:", methodName);
            Console.WriteLine("   Absolute test time: {0}", absoluteTimeInTest);
            Console.WriteLine("   Threshold time: {0}", absoluteTestThreshold);
            Console.WriteLine("   Normalized test time: {0:F4}", normalizedTimeInTest);
            Console.WriteLine("   Normalized threshold: {0:F4}", baselineMagnitude);
            Console.WriteLine("   Safe threshold (10% leeway): {0:F4}", normalizedTimeInTest * 1.1);
            if (absoluteTimeInTest > absoluteTestThreshold)
            {
                string message = string.Format(
                    "Performance test case {0} was too slow! Runtime should have been less than {1:F4} units, but was actually {2:F4} units.",
                    methodName,
                    baselineMagnitude,
                    normalizedTimeInTest);
                if (noOptimizations)
                {
                    message += " Please run tests again in RELEASE mode for accurate numbers.";
                    Assert.Inconclusive(message);
                }
                else
                {
                    Assert.Fail(message);
                }
            }
        }

        public static void AssertMemoryAllocationBelowThreshold(this Summary summary, string methodName, long maxAllocatedBytes)
        {
            bool noOptimizations = WasTestRunWithoutOptimizations(summary);
            if (noOptimizations)
            {
                Console.WriteLine("WARNING: Benchmarks are being run on non-optimized assemblies!");
            }

            long actualAlloc = MemoryAllocationForTestCase(summary, methodName);
            Console.WriteLine("Evaluating test memory allocation for method {0}:", methodName);
            Console.WriteLine("   Absolute allocated bytes: {0}", actualAlloc);
            Console.WriteLine("   Threshold allocated bytes: {0}", maxAllocatedBytes);
            if (actualAlloc > maxAllocatedBytes)
            {
                string message = string.Format(
                    "Performance test case {0} allocated too much! Should have been less than {1} bytes, but was actually {2} bytes.",
                    methodName,
                    maxAllocatedBytes,
                    actualAlloc);
                if (noOptimizations)
                {
                    message += " Please run tests again in RELEASE mode for accurate numbers.";
                    Assert.Inconclusive(message);
                }
                else
                {
                    Assert.Fail(message);
                }
            }
        }

        public static BenchmarkReport GetReportForTestCase(this Summary summary, string methodName)
        {
            return summary.Reports
                .Where((r) => string.Equals(methodName, r.BenchmarkCase.Descriptor.WorkloadMethodDisplayInfo, StringComparison.Ordinal))
                .Single();
        }

        public class PerfRegressionInProcessConfig : ManualConfig
        {
            public PerfRegressionInProcessConfig()
            {
                AddColumnProvider(DefaultColumnProviders.Instance);
                AddDiagnoser(MemoryDiagnoser.Default);
                AddLogger(BenchmarkDotNet.Loggers.ConsoleLogger.Default);
                AddJob(Job.ShortRun
                    .WithLaunchCount(1)
                    .WithMinWarmupCount(5)
                    .WithMaxWarmupCount(10)
                    .WithMinIterationCount(10)
                    .WithMaxIterationCount(50)
                    .WithToolchain(new InProcessEmitToolchain(
                        timeout: TimeSpan.FromMinutes(10),
                        logOutput: true)));
            }

#if DEBUG
            public new ConfigOptions Options => ConfigOptions.DisableOptimizationsValidator;
#endif
        }

        public static TimeSpan GetBaselineTime(double magnitude)
        {
            lock (_baselineLock)
            {
                if (_baselineTime == TimeSpan.Zero)
                {
                    var baselineResults = BenchmarkRunner.Run<BaselineGenerator>(new PerfRegressionInProcessConfig());
                    _baselineTime = baselineResults.ElapsedTimeForTestCase(nameof(BaselineGenerator.CalculateBaseline));
                }
            }

            return _baselineTime * magnitude;
        }

        public class BaselineGenerator
        {
            private static Random _random = new Random();
            private System.Numerics.BigInteger a;
            private System.Numerics.BigInteger b;
            private System.Numerics.BigInteger c;


            [GlobalSetup]
            public void BenchmarkSetup()
            {
                byte[] a_bytes = new byte[8];
                byte[] b_bytes = new byte[8];
                byte[] c_bytes = new byte[8];
                _random.NextBytes(a_bytes);
                _random.NextBytes(b_bytes);
                _random.NextBytes(c_bytes);
                a_bytes[7] &= 0x7F; // positive integers only
                b_bytes[7] &= 0x7F;
                c_bytes[7] &= 0x7F;
                a = new System.Numerics.BigInteger(a_bytes);
                b = new System.Numerics.BigInteger(b_bytes);
                c = new System.Numerics.BigInteger(c_bytes);
            }

            [Benchmark]
            public void CalculateBaseline()
            {
                System.Numerics.BigInteger.ModPow(a, b, c);
            }
        }
    }
}
