using Durandal.Common.Collections;
using Durandal.Common.Compression.BZip2;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Security;
using Durandal.Common.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.MathExt
{
    [TestClass]
    public class RandomTests
    {
        public enum RandomImplementation
        {
            Default,
            Fast,
            Cryptographic
        }

        [TestMethod]
        [DataRow(RandomImplementation.Default)]
        [DataRow(RandomImplementation.Fast)]
        [DataRow(RandomImplementation.Cryptographic)]
        public void TestRandomNextInt32BaseRange(RandomImplementation impl)
        {
            IRandom rand = GetImplementation(impl);
            using (rand as IDisposable)
            {
                Assert.AreEqual(42, rand.NextInt(42, 42));
            }
        }

        [TestMethod]
        [DataRow(RandomImplementation.Default)]
        [DataRow(RandomImplementation.Fast)]
        [DataRow(RandomImplementation.Cryptographic)]
        public void TestRandomNextInt64BaseRange(RandomImplementation impl)
        {
            IRandom rand = GetImplementation(impl);
            using (rand as IDisposable)
            {
                Assert.AreEqual(42, rand.NextInt64(42, 42));
            }
        }

        [TestMethod]
        [DataRow(RandomImplementation.Default)]
        [DataRow(RandomImplementation.Fast)]
        [DataRow(RandomImplementation.Cryptographic)]
        public void TestRandomNextInt32Invalid(RandomImplementation impl)
        {
            IRandom rand = GetImplementation(impl);
            using (rand as IDisposable)
            {
                try
                {
                    rand.NextInt(10, 9);
                    Assert.Fail("Expected an ArgumentOutOfRangeException");
                }
                catch (ArgumentOutOfRangeException) { }
            }
        }

        [TestMethod]
        [DataRow(RandomImplementation.Default)]
        [DataRow(RandomImplementation.Fast)]
        [DataRow(RandomImplementation.Cryptographic)]
        public void TestRandomNextInt64Invalid(RandomImplementation impl)
        {
            IRandom rand = GetImplementation(impl);
            using (rand as IDisposable)
            {
                try
                {
                    rand.NextInt64(10, 9);
                    Assert.Fail("Expected an ArgumentOutOfRangeException");
                }
                catch (ArgumentOutOfRangeException) { }
            }
        }

        [TestMethod]
        [DataRow(RandomImplementation.Default)]
        [DataRow(RandomImplementation.Fast)]
        [DataRow(RandomImplementation.Cryptographic)]
        public void TestRandomNextFloat32Invalid(RandomImplementation impl)
        {
            IRandom rand = GetImplementation(impl);
            using (rand as IDisposable)
            {
                try
                {
                    rand.NextFloat(10, 9);
                    Assert.Fail("Expected an ArgumentOutOfRangeException");
                }
                catch (ArgumentOutOfRangeException) { }
            }
        }

        [TestMethod]
        [DataRow(RandomImplementation.Default)]
        [DataRow(RandomImplementation.Fast)]
        [DataRow(RandomImplementation.Cryptographic)]
        public void TestRandomNextFloat64Invalid(RandomImplementation impl)
        {
            IRandom rand = GetImplementation(impl);
            using (rand as IDisposable)
            {
                try
                {
                    rand.NextDouble(10, 9);
                    Assert.Fail("Expected an ArgumentOutOfRangeException");
                }
                catch (ArgumentOutOfRangeException) { }
            }
        }

        /// <summary>
        /// Tests that the int32 output of random number generator has a standard deviation within the expected range.
        /// </summary>
        [TestMethod]
        [DataRow(RandomImplementation.Default)]
        [DataRow(RandomImplementation.Fast)]
        [DataRow(RandomImplementation.Cryptographic)]
        public void TestRandomInt32Distribution(RandomImplementation impl)
        {
            IRandom rand = GetImplementation(impl);
            using (rand as IDisposable)
            {
                double maxDeviation = impl == RandomImplementation.Cryptographic ? 0.03 : 0.01;

                // analysis: create a histogram of a fixed number of bins, increment random bins
                // iteratively by 1, and then assert that the histogram is mostly flat
                int numRunsPerBin = 200;
                double avgStdDev = 0;
                const int numPasses = 10;
                for (int pass = 0; pass < numPasses; pass++)
                {
                    int numBins = rand.NextInt(100, 10000);
                    int numRuns = numBins * numRunsPerBin;
                    int[] bins = new int[numBins];
                    for (int c = 0; c < numRuns; c++)
                    {
                        bins[rand.NextInt(0, numBins)] += 1;
                    }

                    double average = 0;
                    double stdDev = 0;
                    for (int c = 0; c < numBins; c++)
                    {
                        average += bins[c];
                    }

                    average = average / (double)numBins;
                    for (int c = 0; c < numBins; c++)
                    {
                        double deviation = average - (double)bins[c];
                        stdDev += (deviation * deviation);
                    }

                    stdDev = Math.Sqrt(stdDev / (double)numBins);
                    avgStdDev += stdDev;
                }

                avgStdDev = avgStdDev / (double)numPasses;
                double distribution = avgStdDev / Math.Sqrt((double)numRunsPerBin);
                Console.WriteLine(distribution);
                Assert.AreEqual(1.0, distribution, maxDeviation);
            }
        }

        /// <summary>
        /// Tests that the int32 output of random number generator has the expected spacing between numbers
        /// </summary>
        [TestMethod]
        [DataRow(RandomImplementation.Default)]
        [DataRow(RandomImplementation.Fast)]
        [DataRow(RandomImplementation.Cryptographic)]
        public void TestRandomInt32SpacingPositiveRange(RandomImplementation impl)
        {
            IRandom rand = GetImplementation(impl);
            using (rand as IDisposable)
            {
                double maxDeviation = impl == RandomImplementation.Cryptographic ? 0.03 : 0.01;
                RunSpacingTest(() => (double)rand.NextInt(), (double)int.MaxValue, maxDeviation);
            }
        }

        /// <summary>
        /// Tests that the int32 output of random number generator has the expected spacing between numbers
        /// </summary>
        [TestMethod]
        [DataRow(RandomImplementation.Default)]
        [DataRow(RandomImplementation.Fast)]
        [DataRow(RandomImplementation.Cryptographic)]
        public void TestRandomInt32SpacingLimitedRange(RandomImplementation impl)
        {
            IRandom rand = GetImplementation(impl);
            using (rand as IDisposable)
            {
                double maxDeviation = impl == RandomImplementation.Cryptographic ? 0.03 : 0.01;
                RunSpacingTest(() => (double)rand.NextInt(100), (double)100, maxDeviation);
            }
        }

        /// <summary>
        /// Tests that the int32 output of random number generator has the expected spacing between numbers
        /// </summary>
        [TestMethod]
        [DataRow(RandomImplementation.Default)]
        [DataRow(RandomImplementation.Fast)]
        [DataRow(RandomImplementation.Cryptographic)]
        public void TestRandomInt32SpacingFullRange(RandomImplementation impl)
        {
            IRandom rand = GetImplementation(impl);
            using (rand as IDisposable)
            {
                double maxDeviation = impl == RandomImplementation.Cryptographic ? 0.03 : 0.01;
                RunSpacingTest(
                    () => (double)rand.NextInt(int.MinValue, int.MaxValue),
                    (double)int.MaxValue - (double)int.MinValue,
                    maxDeviation);
            }
        }

        [TestMethod]
        [DataRow(RandomImplementation.Default)]
        [DataRow(RandomImplementation.Fast)]
        [DataRow(RandomImplementation.Cryptographic)]
        public void TestRandomInt32StaysWithinDefaultRange(RandomImplementation impl)
        {
            IRandom rand = GetImplementation(impl);
            using (rand as IDisposable)
            {
                for (int c = 0; c < 10000; c++)
                {
                    int val = rand.NextInt();
                    Assert.IsTrue(val >= 0);
                }
            }
        }

        [TestMethod]
        [DataRow(RandomImplementation.Default)]
        [DataRow(RandomImplementation.Fast)]
        [DataRow(RandomImplementation.Cryptographic)]
        public void TestRandomInt32StaysWithinRange(RandomImplementation impl)
        {
            IRandom rand = GetImplementation(impl);
            using (rand as IDisposable)
            {
                for (int c = 0; c < 10000; c++)
                {
                    int val = rand.NextInt(-10, 10);
                    Assert.IsTrue(val >= -10);
                    Assert.IsTrue(val < 10);
                }
            }
        }

        /// <summary>
        /// Tests that the int64 output of random number generator has the expected spacing between numbers
        /// </summary>
        [TestMethod]
        [DataRow(RandomImplementation.Default)]
        [DataRow(RandomImplementation.Fast)]
        [DataRow(RandomImplementation.Cryptographic)]
        public void TestRandomInt64SpacingPositiveRange(RandomImplementation impl)
        {
            IRandom rand = GetImplementation(impl);
            using (rand as IDisposable)
            {
                double maxDeviation = impl == RandomImplementation.Cryptographic ? 0.03 : 0.01;
                RunSpacingTest(() => (double)rand.NextInt64(), (double)long.MaxValue, maxDeviation);
            }
        }

        /// <summary>
        /// Tests that the int64 output of random number generator has the expected spacing between numbers
        /// </summary>
        [TestMethod]
        [DataRow(RandomImplementation.Default)]
        [DataRow(RandomImplementation.Fast)]
        [DataRow(RandomImplementation.Cryptographic)]
        public void TestRandomInt64SpacingLimitedRange(RandomImplementation impl)
        {
            IRandom rand = GetImplementation(impl);
            using (rand as IDisposable)
            {
                double maxDeviation = impl == RandomImplementation.Cryptographic ? 0.03 : 0.01;
                RunSpacingTest(() => (double)rand.NextInt64(100), (double)100, maxDeviation);
            }
        }

        /// <summary>
        /// Tests that the int32 output of random number generator has the expected spacing between numbers
        /// </summary>
        [TestMethod]
        [DataRow(RandomImplementation.Default)]
        [DataRow(RandomImplementation.Fast)]
        [DataRow(RandomImplementation.Cryptographic)]
        public void TestRandomInt64SpacingFullRange(RandomImplementation impl)
        {
            IRandom rand = GetImplementation(impl);
            using (rand as IDisposable)
            {
                double maxDeviation = impl == RandomImplementation.Cryptographic ? 0.03 : 0.01;
                RunSpacingTest(
                    () => (double)rand.NextInt64(long.MinValue, long.MaxValue),
                    (double)long.MaxValue - (double)long.MinValue,
                    maxDeviation);
            }
        }

        [TestMethod]
        [DataRow(RandomImplementation.Default)]
        [DataRow(RandomImplementation.Fast)]
        [DataRow(RandomImplementation.Cryptographic)]
        public void TestRandomInt64StaysWithinDefaultRange(RandomImplementation impl)
        {
            IRandom rand = GetImplementation(impl);
            using (rand as IDisposable)
            {
                for (int c = 0; c < 10000; c++)
                {
                    long val = rand.NextInt64();
                    Assert.IsTrue(val >= 0);
                }
            }
        }

        [TestMethod]
        [DataRow(RandomImplementation.Default)]
        [DataRow(RandomImplementation.Fast)]
        [DataRow(RandomImplementation.Cryptographic)]
        public void TestRandomInt64StaysWithinRange(RandomImplementation impl)
        {
            IRandom rand = GetImplementation(impl);
            using (rand as IDisposable)
            {
                for (int c = 0; c < 10000; c++)
                {
                    long val = rand.NextInt64(-10, 10);
                    Assert.IsTrue(val >= -10);
                    Assert.IsTrue(val < 10);
                }
            }
        }

        /// <summary>
        /// Tests that the float32 output of random number generator has the expected spacing between numbers
        /// </summary>
        [TestMethod]
        [DataRow(RandomImplementation.Default)]
        [DataRow(RandomImplementation.Fast)]
        [DataRow(RandomImplementation.Cryptographic)]
        public void TestRandomFloat32SpacingDefaultRange(RandomImplementation impl)
        {
            IRandom rand = GetImplementation(impl);
            using (rand as IDisposable)
            {
                double maxDeviation = impl == RandomImplementation.Cryptographic ? 0.03 : 0.01;
                RunSpacingTest(() => (double)rand.NextFloat(), 1.0, maxDeviation);
            }
        }

        /// <summary>
        /// Tests that the float32 output of random number generator outputs values between 0.0 and 1.0
        /// </summary>
        [TestMethod]
        [DataRow(RandomImplementation.Default)]
        [DataRow(RandomImplementation.Fast)]
        [DataRow(RandomImplementation.Cryptographic)]
        public void TestRandomFloat32BoundaryDefaultRange(RandomImplementation impl)
        {
            IRandom rand = GetImplementation(impl);
            using (rand as IDisposable)
            {
                for (int c = 0; c < 10000; c++)
                {
                    float val = rand.NextFloat();
                    Assert.IsTrue(val >= 0.0f, "Value must be greater than or equal to zero");
                    Assert.IsTrue(val < 1.0f, "Value must be less than one");
                }
            }
        }

        /// <summary>
        /// Tests that the float32 output of random number generator has the expected spacing between numbers
        /// </summary>
        [TestMethod]
        [DataRow(RandomImplementation.Default)]
        [DataRow(RandomImplementation.Fast)]
        [DataRow(RandomImplementation.Cryptographic)]
        public void TestRandomFloat32SpacingLimitedRange(RandomImplementation impl)
        {
            IRandom rand = GetImplementation(impl);
            using (rand as IDisposable)
            {
                double maxDeviation = impl == RandomImplementation.Cryptographic ? 0.03 : 0.01;
                RunSpacingTest(() => (double)rand.NextFloat(-2.0f, 2.0f), (double)4, maxDeviation);
            }
        }

        /// <summary>
        /// Tests that the float32 output of random number generator has the expected spacing between numbers
        /// </summary>
        [TestMethod]
        [DataRow(RandomImplementation.Default)]
        [DataRow(RandomImplementation.Fast)]
        [DataRow(RandomImplementation.Cryptographic)]
        public void TestRandomFloat32SpacingBroadRange(RandomImplementation impl)
        {
            IRandom rand = GetImplementation(impl);
            using (rand as IDisposable)
            {
                double maxDeviation = impl == RandomImplementation.Cryptographic ? 0.03 : 0.01;
                RunSpacingTest(
                    () => (double)rand.NextFloat(int.MinValue, int.MaxValue),
                    (double)int.MaxValue - (double)int.MinValue,
                    maxDeviation);
            }
        }

        [TestMethod]
        [DataRow(RandomImplementation.Default)]
        [DataRow(RandomImplementation.Fast)]
        [DataRow(RandomImplementation.Cryptographic)]
        public void TestRandomFloat32StaysWithinRange(RandomImplementation impl)
        {
            IRandom rand = GetImplementation(impl);
            using (rand as IDisposable)
            {
                for (int c = 0; c < 10000; c++)
                {
                    float val = rand.NextFloat(-10.0f, 10.0f);
                    Assert.IsTrue(val >= -10.0f);
                    Assert.IsTrue(val < 10.0f);
                }
            }
        }

        [TestMethod]
        [DataRow(RandomImplementation.Default)]
        [DataRow(RandomImplementation.Fast)]
        [DataRow(RandomImplementation.Cryptographic)]
        public void TestRandomFloat32StaysWithinDefaultRange(RandomImplementation impl)
        {
            IRandom rand = GetImplementation(impl);
            using (rand as IDisposable)
            {
                for (int c = 0; c < 10000; c++)
                {
                    float val = rand.NextFloat();
                    Assert.IsTrue(val >= 0.0f);
                    Assert.IsTrue(val < 1.0f);
                }
            }
        }

        /// <summary>
        /// Tests that the float32 output of random number generator has the expected spacing between numbers
        /// </summary>
        [TestMethod]
        [DataRow(RandomImplementation.Default)]
        [DataRow(RandomImplementation.Fast)]
        [DataRow(RandomImplementation.Cryptographic)]
        public void TestRandomFloat64SpacingDefaultRange(RandomImplementation impl)
        {
            IRandom rand = GetImplementation(impl);
            using (rand as IDisposable)
            {
                double maxDeviation = impl == RandomImplementation.Cryptographic ? 0.03 : 0.01;
                RunSpacingTest(() => rand.NextDouble(), 1.0, maxDeviation);
            }
        }

        /// <summary>
        /// Tests that the float64 output of random number generator outputs values between 0.0 and 1.0
        /// </summary>
        [TestMethod]
        [DataRow(RandomImplementation.Default)]
        [DataRow(RandomImplementation.Fast)]
        [DataRow(RandomImplementation.Cryptographic)]
        public void TestRandomFloat64BoundaryDefaultRange(RandomImplementation impl)
        {
            IRandom rand = GetImplementation(impl);
            using (rand as IDisposable)
            {
                for (int c = 0; c < 10000; c++)
                {
                    double val = rand.NextDouble();
                    Assert.IsTrue(val >= 0.0f, "Value must be greater than or equal to zero");
                    Assert.IsTrue(val < 1.0f, "Value must be less than one");
                }
            }
        }

        /// <summary>
        /// Tests that the float32 output of random number generator has the expected spacing between numbers
        /// </summary>
        [TestMethod]
        [DataRow(RandomImplementation.Default)]
        [DataRow(RandomImplementation.Fast)]
        [DataRow(RandomImplementation.Cryptographic)]
        public void TestRandomFloat64SpacingLimitedRange(RandomImplementation impl)
        {
            IRandom rand = GetImplementation(impl);
            double maxDeviation = impl == RandomImplementation.Cryptographic ? 0.03 : 0.01;
            RunSpacingTest(() => rand.NextDouble(-2.0, 2.0), (double)4, maxDeviation);
        }

        /// <summary>
        /// Tests that the float32 output of random number generator has the expected spacing between numbers
        /// </summary>
        [TestMethod]
        [DataRow(RandomImplementation.Default)]
        [DataRow(RandomImplementation.Fast)]
        [DataRow(RandomImplementation.Cryptographic)]
        public void TestRandomFloat64SpacingBroadRange(RandomImplementation impl)
        {
            IRandom rand = GetImplementation(impl);
            using (rand as IDisposable)
            {
                double maxDeviation = impl == RandomImplementation.Cryptographic ? 0.03 : 0.01;
                RunSpacingTest(
                    () => rand.NextDouble(int.MinValue, int.MaxValue),
                    (double)int.MaxValue - (double)int.MinValue,
                    maxDeviation);
            }
        }

        [TestMethod]
        [DataRow(RandomImplementation.Default)]
        [DataRow(RandomImplementation.Fast)]
        [DataRow(RandomImplementation.Cryptographic)]
        public void TestRandomFloat64StaysWithinRange(RandomImplementation impl)
        {
            IRandom rand = GetImplementation(impl);
            using (rand as IDisposable)
            {
                for (int c = 0; c < 10000; c++)
                {
                    double val = rand.NextDouble(-10.0, 10.0);
                    Assert.IsTrue(val >= -10.0);
                    Assert.IsTrue(val < 10.0);
                }
            }
        }

        [TestMethod]
        [DataRow(RandomImplementation.Default)]
        [DataRow(RandomImplementation.Fast)]
        [DataRow(RandomImplementation.Cryptographic)]
        public void TestRandomFloat64StaysWithinDefaultRange(RandomImplementation impl)
        {
            IRandom rand = GetImplementation(impl);
            using (rand as IDisposable)
            {
                for (int c = 0; c < 10000; c++)
                {
                    double val = rand.NextDouble();
                    Assert.IsTrue(val >= 0.0);
                    Assert.IsTrue(val < 1.0);
                }
            }
        }

        private static void RunSpacingTest(Func<double> numberProducer, double range, double maxDeviation)
        {
            // analysis: generate a bunch of random numbers, calculate the average distance between them,
            // and ensure that it's close to the uniform distribution
            double averageDistance = 0;
            const int numPasses = 100;
            for (int pass = 0; pass < numPasses; pass++)
            {
                const int batchSize = 1000;
                double[] generatedValues = new double[batchSize];
                for (int c = 0; c < batchSize; c++)
                {
                    generatedValues[c] = numberProducer();
                }

                averageDistance += CalculateSpacingVariation(generatedValues, range) / (double)numPasses;
            }

            Console.WriteLine(averageDistance);
            Assert.AreEqual(1.0, averageDistance, maxDeviation);
        }

        private static double CalculateSpacingVariation(double[] values, double range)
        {
            Array.Sort(values);
            double denominator = (double)values.Length;
            double averageSpacing = 0;
            for (int c = 1; c < values.Length; c++)
            {
                double diff = values[c] - values[c - 1];
                averageSpacing += (diff / denominator);
            }

            double expectedSpacing = range / (double)values.Length;
            return averageSpacing / expectedSpacing;
        }

        /// <summary>
        /// Tests that the int64 output of random number generator has a standard deviation within the expected range.
        /// </summary>
        [TestMethod]
        [DataRow(RandomImplementation.Default)]
        [DataRow(RandomImplementation.Fast)]
        [DataRow(RandomImplementation.Cryptographic)]
        public void TestRandomInt64Distribution(RandomImplementation impl)
        {
            IRandom rand = GetImplementation(impl);
            using (rand as IDisposable)
            {
                double maxDeviation = impl == RandomImplementation.Cryptographic ? 0.03 : 0.01;

                // analysis: create a histogram of a fixed number of bins, increment random bins
                // iteratively by 1, and then assert that the histogram is mostly flat
                int numRunsPerBin = 100;
                double avgStdDev = 0;
                const int numPasses = 10;
                for (int pass = 0; pass < numPasses; pass++)
                {
                    int numBins = rand.NextInt(100, 10000);
                    int numRuns = numBins * numRunsPerBin;
                    int[] bins = new int[numBins];
                    for (int c = 0; c < numRuns; c++)
                    {
                        bins[rand.NextInt64(0, numBins)] += 1;
                    }

                    double average = 0;
                    double stdDev = 0;
                    for (int c = 0; c < numBins; c++)
                    {
                        average += bins[c];
                    }

                    average = average / (double)numBins;
                    for (int c = 0; c < numBins; c++)
                    {
                        double deviation = average - (double)bins[c];
                        stdDev += (deviation * deviation);
                    }

                    stdDev = Math.Sqrt(stdDev / (double)numBins);
                    avgStdDev += stdDev;
                }

                avgStdDev = avgStdDev / (double)numPasses;
                double distribution = avgStdDev / Math.Sqrt((double)numRunsPerBin);
                Console.WriteLine(distribution);
                Assert.AreEqual(1.0, distribution, maxDeviation);
            }
        }

        /// <summary>
        /// Tests that the byte output of random number generator has a standard deviation within the expected range.
        /// </summary>
        [TestMethod]
        [DataRow(RandomImplementation.Default)]
        [DataRow(RandomImplementation.Fast)]
        [DataRow(RandomImplementation.Cryptographic)]
        public void TestRandomBytesDistribution(RandomImplementation impl)
        {
            IRandom rand = GetImplementation(impl);
            using (rand as IDisposable)
            {
                double maxDeviation = impl == RandomImplementation.Cryptographic ? 0.03 : 0.01;

                // analysis: create a histogram of 256 bins, increment random bins
                // iteratively by 1, and then assert that the histogram is mostly flat
                int numRunsPerBin = 100;
                double avgStdDev = 0;
                const int numPasses = 100;
                const int numBins = 256;
                int numRuns = numBins * numRunsPerBin;
                byte[] values = new byte[numRuns];
                int[] bins = new int[numBins];

                for (int pass = 0; pass < numPasses; pass++)
                {
                    ArrayExtensions.WriteZeroes(bins, 0, numBins);
                    rand.NextBytes(values);
                    for (int c = 0; c < numRuns; c++)
                    {
                        bins[values[c]] += 1;
                    }

                    double average = 0;
                    double stdDev = 0;
                    for (int c = 0; c < numBins; c++)
                    {
                        average += bins[c];
                    }

                    average = average / (double)numBins;
                    for (int c = 0; c < numBins; c++)
                    {
                        double deviation = average - (double)bins[c];
                        stdDev += (deviation * deviation);
                    }

                    stdDev = Math.Sqrt(stdDev / (double)numBins);
                    avgStdDev += stdDev;
                }

                avgStdDev = avgStdDev / (double)numPasses;
                double distribution = avgStdDev / Math.Sqrt((double)numRunsPerBin);

                Console.WriteLine(distribution);
                Assert.AreEqual(1.0, distribution, maxDeviation);
            }
        }

        /// <summary>
        /// Tests that the output of random number generator covers the full int32 range
        /// </summary>
        [TestMethod]
        [DataRow(RandomImplementation.Default)]
        [DataRow(RandomImplementation.Fast)]
        [DataRow(RandomImplementation.Cryptographic)]
        public void TestRandomInt32Range(RandomImplementation impl)
        {
            IRandom rand = GetImplementation(impl);
            using (rand as IDisposable)
            {
                int iter = 0;
                const int maxIter = 1000000;

                const int lowThreshold = int.MinValue + 100000;
                const int highThreshold = int.MaxValue - 100000;
                const int midThreshold = 100000;

                // Ensure that it generates low numbers
                iter = 0;
                while (true)
                {
                    if (rand.NextInt(int.MinValue, int.MaxValue) < lowThreshold)
                    {
                        break;
                    }

                    if (iter++ > maxIter)
                    {
                        Assert.Fail("Took too long to generate a number in the low range");
                    }
                }

                // Ensure that it generates high numbers
                iter = 0;
                while (true)
                {
                    if (rand.NextInt(int.MinValue, int.MaxValue) > highThreshold)
                    {
                        break;
                    }

                    if (iter++ > maxIter)
                    {
                        Assert.Fail("Took too long to generate a number in the high range");
                    }
                }

                // Ensure that it generates numbers in the mid range
                iter = 0;
                while (true)
                {
                    if (Math.Abs(rand.NextInt(int.MinValue, int.MaxValue)) < midThreshold)
                    {
                        break;
                    }

                    if (iter++ > maxIter)
                    {
                        Assert.Fail("Took too long to generate a number in the mid range");
                    }
                }
            }
        }

        /// <summary>
        /// Tests that the output of random number generator covers the full int64 range
        /// </summary>
        [TestMethod]
        [DataRow(RandomImplementation.Default)]
        [DataRow(RandomImplementation.Fast)]
        [DataRow(RandomImplementation.Cryptographic)]
        public void TestRandomInt64Range(RandomImplementation impl)
        {
            IRandom rand = GetImplementation(impl);
            using (rand as IDisposable)
            {
                int iter = 0;
                const int maxIter = 1000000;

                const long lowThreshold = long.MinValue + 10000000000000000L;
                const long highThreshold = long.MaxValue - 10000000000000000L;
                const long midThreshold = 10000000000000000L;

                // Ensure that it generates low numbers
                iter = 0;
                while (true)
                {
                    if (rand.NextInt64(long.MinValue, long.MaxValue) < lowThreshold)
                    {
                        break;
                    }

                    if (iter++ > maxIter)
                    {
                        Assert.Fail("Took too long to generate a number in the low range");
                    }
                }

                // Ensure that it generates high numbers
                iter = 0;
                while (true)
                {
                    if (rand.NextInt64(long.MinValue, long.MaxValue) > highThreshold)
                    {
                        break;
                    }

                    if (iter++ > maxIter)
                    {
                        Assert.Fail("Took too long to generate a number in the high range");
                    }
                }

                // Ensure that it generates numbers in the mid range
                iter = 0;
                while (true)
                {
                    if (Math.Abs(rand.NextInt64(long.MinValue, long.MaxValue)) < midThreshold)
                    {
                        break;
                    }

                    if (iter++ > maxIter)
                    {
                        Assert.Fail("Took too long to generate a number in the mid range");
                    }
                }
            }
        }

        /// <summary>
        /// Tests that random booleans will tend to cancel each other out 50/50
        /// </summary>
        [TestMethod]
        [DataRow(RandomImplementation.Default)]
        [DataRow(RandomImplementation.Fast)]
        [DataRow(RandomImplementation.Cryptographic)]
        public void TestRandomBoolDistribution(RandomImplementation impl)
        {
            IRandom rand = GetImplementation(impl);
            using (rand as IDisposable)
            {
                double maxDeviation = impl == RandomImplementation.Cryptographic ? 0.03 : 0.01;

                int numRuns = 100000;
                double val = 0;
                for (int c = 0; c < numRuns; c++)
                {
                    if (rand.NextBool())
                    {
                        val += 1;
                    }
                    else
                    {
                        val -= 1;
                    }
                }

                double distribution = val / (double)numRuns;
                Console.WriteLine(distribution);
                Assert.AreEqual(0.0, distribution, maxDeviation);
            }
        }
        
        /// <summary>
        /// Specifically tests that the output of FastRandom.NextBytes is deterministic whether or not SIMD is used
        /// </summary>
        [TestMethod]
        public void TestFastRandomBytesArrayDeterminism()
        {
            const int seed = 99999;
            int[] bufSizesToCheck = new int[] { 1, 4, 8, 10, 50, 100, 1000, 5321, 7777, 27777 };
            foreach (int bufSizeA in bufSizesToCheck)
            {
                byte[] bufferA = new byte[bufSizeA];
                new FastRandom(seed).NextBytes(bufferA, 0, bufSizeA);

                foreach (int bufSizeB in bufSizesToCheck)
                {
                    byte[] bufferB = new byte[bufSizeB];
                    new FastRandom(seed).NextBytes(bufferB);
                    Console.WriteLine($"Testing buffer sizes {bufSizeA} and {bufSizeB}");
                    Assert.IsTrue(ArrayExtensions.ArrayEquals(bufferA, 0, bufferB, 0, Math.Min(bufSizeA, bufSizeB)));
                }
            }
        }

        /// <summary>
        /// Specifically tests that the output of FastRandom.NextBytes is deterministic whether or not SIMD is used
        /// </summary>
        [TestMethod]
        [DataRow(1)]
        [DataRow(4)]
        [DataRow(7)]
        [DataRow(8)]
        [DataRow(10)]
        [DataRow(33)]
        [DataRow(50)]
        [DataRow(100)]
        [DataRow(1000)]
        [DataRow(5321)]
        [DataRow(7777)]
        [DataRow(27777)]
        public void TestFastRandomBytesSequentialSpanDeterminism(int writeSize)
        {
            const int seed = 99999;
            const int totalBufferSize = 65536;
            byte[] bufferA = new byte[totalBufferSize];
            byte[] bufferB = new byte[totalBufferSize];
            Console.WriteLine($"Testing write size {writeSize}");
            FastRandom truthGenerator = new FastRandom(seed);
            FastRandom uncertainGenerator = new FastRandom(seed);
            truthGenerator.NextBytes(bufferA.AsSpan());

            int bytesGenerated = 0;
            while (bytesGenerated < totalBufferSize)
            {
                int toGenerate = Math.Min(totalBufferSize - bytesGenerated, writeSize);
                uncertainGenerator.NextBytes(bufferB.AsSpan(bytesGenerated, toGenerate));
                bytesGenerated += toGenerate;
            }

            Assert.IsTrue(ArrayExtensions.ArrayEquals(bufferA, 0, bufferB, 0, totalBufferSize));
        }

        [TestMethod]
        public void TestFastRandomBytesSequentialSpanDeterminismRandomWriteSize()
        {
            const int seed = 1234;
            const int totalBufferSize = 65536 * 4;
            byte[] bufferA = new byte[totalBufferSize];
            byte[] bufferB = new byte[totalBufferSize];
            FastRandom truthGenerator = new FastRandom(seed);
            FastRandom uncertainGenerator = new FastRandom(seed);
            truthGenerator.NextBytes(bufferA.AsSpan());

            int bytesGenerated = 0;
            while (bytesGenerated < totalBufferSize)
            {
                int toGenerate = Math.Min(totalBufferSize - bytesGenerated, truthGenerator.NextInt(1, 1000));
                uncertainGenerator.NextBytes(bufferB.AsSpan(bytesGenerated, toGenerate));
                bytesGenerated += toGenerate;
            }

            Assert.IsTrue(ArrayExtensions.ArrayEquals(bufferA, 0, bufferB, 0, totalBufferSize));
        }

        [TestMethod]
        public void TestFastRandomBytesSequentialArrayDeterminismRandomWriteSize()
        {
            const int seed = 1234;
            const int totalBufferSize = 65536 * 4;
            byte[] bufferA = new byte[totalBufferSize];
            byte[] bufferB = new byte[totalBufferSize];
            FastRandom truthGenerator = new FastRandom(seed);
            FastRandom uncertainGenerator = new FastRandom(seed);
            truthGenerator.NextBytes(bufferA.AsSpan());

            int bytesGenerated = 0;
            while (bytesGenerated < totalBufferSize)
            {
                int toGenerate = Math.Min(totalBufferSize - bytesGenerated, truthGenerator.NextInt(1, 1000));
                uncertainGenerator.NextBytes(bufferB, bytesGenerated, toGenerate);
                bytesGenerated += toGenerate;
            }

            Assert.IsTrue(ArrayExtensions.ArrayEquals(bufferA, 0, bufferB, 0, totalBufferSize));
        }

        /// <summary>
        /// Specifically tests that the output of FastRandom.NextBytes is deterministic whether or not SIMD is used
        /// </summary>
        [TestMethod]
        [DataRow(1)]
        [DataRow(4)]
        [DataRow(7)]
        [DataRow(8)]
        [DataRow(10)]
        [DataRow(33)]
        [DataRow(50)]
        [DataRow(100)]
        [DataRow(1000)]
        [DataRow(5321)]
        [DataRow(7777)]
        [DataRow(27777)]
        public void TestFastRandomBytesSequentialArrayDeterminism(int writeSize)
        {
            const int seed = 99999;
            const int totalBufferSize = 65536;
            byte[] bufferA = new byte[totalBufferSize];
            byte[] bufferB = new byte[totalBufferSize];
            Console.WriteLine($"Testing write size {writeSize}");
            FastRandom truthGenerator = new FastRandom(seed);
            FastRandom uncertainGenerator = new FastRandom(seed);
            truthGenerator.NextBytes(bufferA, 0, totalBufferSize);

            int bytesGenerated = 0;
            while (bytesGenerated < totalBufferSize)
            {
                int toGenerate = Math.Min(totalBufferSize - bytesGenerated, writeSize);
                uncertainGenerator.NextBytes(bufferB, bytesGenerated, toGenerate);
                bytesGenerated += toGenerate;
            }

            Assert.IsTrue(ArrayExtensions.ArrayEquals(bufferA, 0, bufferB, 0, totalBufferSize));
        }

        /// <summary>
        /// Specifically tests that the output of FastRandom.NextBytes is deterministic whether or not SIMD is used
        /// </summary>
        [TestMethod]
        public void TestFastRandomBytesSpanDeterminism()
        {
            const int seed = 99999;
            int[] bufSizesToCheck = new int[] { 1, 4, 8, 10, 50, 100, 1000, 5321, 7777, 27777 };
            foreach (int bufSizeA in bufSizesToCheck)
            {
                byte[] bufferA = new byte[bufSizeA];
                new FastRandom(seed).NextBytes(bufferA.AsSpan());

                foreach (int bufSizeB in bufSizesToCheck)
                {
                    byte[] bufferB = new byte[bufSizeB];
                    new FastRandom(seed).NextBytes(bufferB.AsSpan());
                    Console.WriteLine($"Testing buffer sizes {bufSizeA} and {bufSizeB}");
                    Assert.IsTrue(ArrayExtensions.ArrayEquals(bufferA, 0, bufferB, 0, Math.Min(bufSizeA, bufSizeB)));
                }
            }
        }

        /// <summary>
        /// Tries to test the edge cases of where there are remainder bytes to generate after the main vector loop of FastRandom.NextBytes
        /// </summary>
        [TestMethod]
        public void TestFastRandomBytesRemainder()
        {
            const int seed = 99999;
            int bufSizeBase = 256;// 256 * 16;
            byte[] expectedBuffer = new byte[bufSizeBase + 512];
            new FastRandom(seed).NextBytesSerial(expectedBuffer.AsSpan(0, expectedBuffer.Length));

            byte[] actualBuffer = new byte[bufSizeBase + 32];
            for (int bufSizeRemainder = 1; bufSizeRemainder < 32; bufSizeRemainder++)
            {
                Console.WriteLine($"Testing remainder {bufSizeRemainder}");
                new FastRandom(seed).NextBytes(actualBuffer, 0, bufSizeBase + bufSizeRemainder);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedBuffer, 0, actualBuffer, 0, bufSizeBase + bufSizeRemainder));
            }
        }

        /// <summary>
        /// Check the span method signature as well since it could have different logic
        /// </summary>
        [TestMethod]
        public void TestFastRandomBytesRemainderSpan()
        {
            const int seed = 99999;
            int bufSizeBase = 256;// 256 * 16;
            byte[] expectedBuffer = new byte[bufSizeBase + 512];
            new FastRandom(seed).NextBytesSerial(expectedBuffer.AsSpan(0, expectedBuffer.Length));

            byte[] actualBuffer = new byte[bufSizeBase + 32];
            for (int bufSizeRemainder = 1; bufSizeRemainder < 32; bufSizeRemainder++)
            {
                Console.WriteLine($"Testing remainder {bufSizeRemainder}");
                new FastRandom(seed).NextBytes(actualBuffer.AsSpan(0, bufSizeBase + bufSizeRemainder));
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedBuffer, 0, actualBuffer, 0, bufSizeBase + bufSizeRemainder));
            }
        }

        /// <summary>
        /// Checks that calls to NextBytes produces the same output when filling arrays
        /// </summary>
        [TestMethod]
        public void TestFastRandomBytesArrayParity()
        {
            const int seed = 99999;
            int[] bufSizesToCheck = new int[] { 1, 4, 8, 10, 50, 100, 129, 1000, 5321, 7777, 27777 };

            foreach (int bufSize in bufSizesToCheck)
            {
                byte[] truthBuf = new byte[bufSize];
                byte[] arrayBuf = new byte[bufSize];
                new FastRandom(seed).NextBytesSerial(truthBuf.AsSpan());
                new FastRandom(seed).NextBytes(arrayBuf, 0, bufSize);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(truthBuf, 0, arrayBuf, 0, bufSize), $"Failed with array length {bufSize}");
            }
        }

        /// <summary>
        /// Checks that calls to NextBytes produces the same output when filling spans
        /// </summary>
        [TestMethod]
        public void TestFastRandomBytesSpanParity()
        {
            const int seed = 99999;
            int[] bufSizesToCheck = new int[] { /*1, 4, */8, 10, 50, 100, 129, 1000, 5321, 7777, 27777 };

            foreach (int bufSize in bufSizesToCheck)
            {
                byte[] truthBuf = new byte[bufSize];
                byte[] spanBuf = new byte[bufSize];
                new FastRandom(seed).NextBytesSerial(truthBuf.AsSpan());
                new FastRandom(seed).NextBytes(spanBuf.AsSpan(0, bufSize));
                Assert.IsTrue(ArrayExtensions.ArrayEquals(truthBuf, 0, spanBuf, 0, bufSize), $"Failed with span length {bufSize}");
            }
        }

        /// <summary>
        /// Checks that calls to NextBytes respect array boundaries
        /// </summary>
        [TestMethod]
        public void TestFastRandomBytesBoundaryChecks()
        {
            FastRandom rand = new FastRandom(11111);
            for (int bufSize = 0; bufSize < 1024; bufSize++)
            {
                byte[] buf = new byte[bufSize];
                rand.NextBytes(buf, 0, bufSize);
                rand.NextBytes(buf.AsSpan());
            }
        }

        [TestMethod]
        public void TestFastRandomThreadSafety()
        {
            FastRandom rand = new FastRandom();
            ILogger logger = new ConsoleLogger();
            const int threadCount = 8;
            const int loopCount = 1000;
            int erroredThreads = 0;
            using (Barrier barrier = new Barrier(threadCount + 1))
            using (CustomThreadPool threadPool = new CustomThreadPool(logger.Clone("ThreadPool"), NullMetricCollector.Singleton, DimensionSet.Empty, threadCount: threadCount))
            {
                for (int thread = 0; thread < threadCount; thread++)
                {
                    threadPool.EnqueueUserWorkItem(() =>
                    {
                        try
                        {
                            for (int loop = 0; loop < loopCount; loop++)
                            {
                                int bufSize = rand.NextInt(1, 1024);
                                byte[] buf = new byte[bufSize];
                                barrier.SignalAndWait();
                                rand.NextBytes(buf, 0, bufSize);
                                rand.NextBytes(buf.AsSpan());
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.Log(ex);
                            Interlocked.Increment(ref erroredThreads);
                        }
                        finally
                        {
                            barrier.RemoveParticipant();
                        }
                    });
                }

                for (int loop = 0; loop < loopCount; loop++)
                {
                    barrier.SignalAndWait();
                }

                Assert.AreEqual(0, erroredThreads);
            }
        }

        private static IRandom GetImplementation(RandomImplementation impl)
        {
            switch (impl)
            {
                case RandomImplementation.Default:
                    return new DefaultRandom(99);
                case RandomImplementation.Fast:
                    return new FastRandom(742);
                case RandomImplementation.Cryptographic:
                    return new CryptographicRandom();
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
