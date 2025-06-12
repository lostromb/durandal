namespace Durandal.Tests.Common.MathExt
{
    using Durandal.Common.MathExt;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    [TestClass]
    public class StatisticalSetTests
    {
        [TestMethod]
        public void TestStatisticalSetConstructor()
        {
            StatisticalSet set = new StatisticalSet();
            Assert.IsNotNull(set);
            set = new StatisticalSet(1);
            Assert.IsNotNull(set);
            set = new StatisticalSet(1000);
            Assert.IsNotNull(set);

            try
            {
                set = new StatisticalSet(0);
                Assert.Fail("Should have thrown an ArgumentOutOfRangeException");
            }
            catch (ArgumentOutOfRangeException) { }

            try
            {
                set = new StatisticalSet(-4);
                Assert.Fail("Should have thrown an ArgumentOutOfRangeException");
            }
            catch (ArgumentOutOfRangeException) { }
        }

        [TestMethod]
        public void TestStatisticalSetEmptySet()
        {
            StatisticalSet set = new StatisticalSet();
            Assert.AreEqual(0, set.Mean);
            Assert.AreEqual(0, set.Variance);
            Assert.AreEqual(0, set.StandardDeviation);
            Assert.AreEqual(0, set.SampleCount);
            Assert.AreEqual(0, set.Samples.Count);
        }

        [TestMethod]
        public void TestStatisticalSetClear()
        {
            StatisticalSet set = new StatisticalSet();
            set.Add(5);
            set.Add(1);
            set.Add(7);
            set.Add(6);
            Assert.AreNotEqual(0, set.Mean);
            Assert.AreNotEqual(0, set.Variance);
            Assert.AreNotEqual(0, set.StandardDeviation);
            Assert.AreNotEqual(0, set.SampleCount);
            Assert.AreNotEqual(0, set.Samples.Count);
            set.Clear();
            Assert.AreEqual(0, set.Mean);
            Assert.AreEqual(0, set.Variance);
            Assert.AreEqual(0, set.StandardDeviation);
            Assert.AreEqual(0, set.SampleCount);
            Assert.AreEqual(0, set.Samples.Count);
        }

        [TestMethod]
        public void TestStatisticalSetAddSingleInvalidArgs()
        {
            StatisticalSet set = new StatisticalSet();

            try
            {
                set.Add(double.NaN);
                Assert.Fail("Should have thrown an ArgumentException");
            }
            catch (ArgumentException) { }

            try
            {
                set.Add(double.PositiveInfinity);
                Assert.Fail("Should have thrown an ArgumentException");
            }
            catch (ArgumentException) { }

            try
            {
                set.Add(double.NegativeInfinity);
                Assert.Fail("Should have thrown an ArgumentException");
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void TestStatisticalSetAddSingle()
        {
            StatisticalSet set = new StatisticalSet();
            IRandom rand = new FastRandom(8983411);
            for (int c = 0; c < 1000; c++)
            {
                set.Add(rand.NextDouble(-10, 1000) * rand.NextDouble(-10, 1000));
                Assert.AreEqual(c + 1, set.SampleCount);
                Assert.AreEqual(CalculateMeanNaive(set.Samples), set.Mean, 0.00001);
                Assert.AreEqual(CalculateVarianceNaive(set.Samples), set.Variance, 0.01);
                Assert.AreEqual(CalculateStdDevNaive(set.Samples), set.StandardDeviation, 0.00001);
                Assert.IsTrue(set.Mean <= 1000000);
                Assert.IsTrue(set.StandardDeviation <= 1000000);
            }
        }

        [TestMethod]
        public void TestStatisticalSetAddRangeInvalidArgs()
        {
            StatisticalSet set = new StatisticalSet();

            try
            {
                set.AddRange(null);
                Assert.Fail("Should have thrown an ArgumentNullException");
            }
            catch (ArgumentNullException) { }

            List<double> samples = new List<double>();
            samples.Add(1);
            samples.Add(2);
            samples.Add(3);
            samples.Add(4);
            samples.Add(double.NaN);

            try
            {
                set.AddRange(samples);
                Assert.Fail("Should have thrown an ArgumentException");
            }
            catch (ArgumentException) { }

            samples.RemoveAt(samples.Count - 1);
            samples.Add(double.NegativeInfinity);

            try
            {
                set.AddRange(samples);
                Assert.Fail("Should have thrown an ArgumentException");
            }
            catch (ArgumentException) { }

            samples.RemoveAt(samples.Count - 1);
            samples.Add(double.PositiveInfinity);

            try
            {
                set.AddRange(samples);
                Assert.Fail("Should have thrown an ArgumentException");
            }
            catch (ArgumentException) { }

            samples.RemoveAt(samples.Count - 1);
            set.AddRange(samples);
            set.AddRange(new List<double>());
        }

        [TestMethod]
        public void TestStatisticalSetAddRangeReadOnlyInvalidArgs()
        {
            StatisticalSet set = new StatisticalSet();

            try
            {
                set.AddRange(null);
                Assert.Fail("Should have thrown an ArgumentNullException");
            }
            catch (ArgumentNullException) { }

            List<double> samples = new List<double>();
            samples.Add(1);
            samples.Add(2);
            samples.Add(3);
            samples.Add(4);
            samples.Add(double.NaN);

            try
            {
                set.AddRangeReadOnly(samples);
                Assert.Fail("Should have thrown an ArgumentException");
            }
            catch (ArgumentException) { }

            samples.RemoveAt(samples.Count - 1);
            samples.Add(double.NegativeInfinity);

            try
            {
                set.AddRangeReadOnly(samples);
                Assert.Fail("Should have thrown an ArgumentException");
            }
            catch (ArgumentException) { }

            samples.RemoveAt(samples.Count - 1);
            samples.Add(double.PositiveInfinity);

            try
            {
                set.AddRangeReadOnly(samples);
                Assert.Fail("Should have thrown an ArgumentException");
            }
            catch (ArgumentException) { }

            samples.RemoveAt(samples.Count - 1);
            set.AddRangeReadOnly(samples);
            set.AddRangeReadOnly(new List<double>());
        }

        [TestMethod]
        public void TestStatisticalSetAddRange()
        {
            StatisticalSet set = new StatisticalSet();
            IRandom rand = new FastRandom(887123);
            List<double> inputList = new List<double>();
            int expectedListLength = 0;

            for (int c = 0; c < 100; c++)
            {
                inputList.Clear();
                int inputListLength = rand.NextInt(0, 1000);
                for (int i = 0; i < inputListLength; i++)
                {
                    inputList.Add(rand.NextDouble(-10, 1000) * rand.NextDouble(-10, 1000));
                }

                set.AddRange(inputList);
                expectedListLength += inputListLength;
                Assert.AreEqual(expectedListLength, set.SampleCount);
                Assert.AreEqual(CalculateMeanNaive(set.Samples), set.Mean, 0.00001);
                Assert.AreEqual(CalculateVarianceNaive(set.Samples), set.Variance, 0.01);
                Assert.AreEqual(CalculateStdDevNaive(set.Samples), set.StandardDeviation, 0.00001);
                Assert.IsTrue(set.Mean <= 1000000);
                Assert.IsTrue(set.StandardDeviation <= 1000000);
            }
        }

        [TestMethod]
        public void TestStatisticalSetAddRangeReadOnly()
        {
            StatisticalSet set = new StatisticalSet();
            IRandom rand = new FastRandom(887123);
            List<double> inputList = new List<double>();
            int expectedListLength = 0;

            for (int c = 0; c < 100; c++)
            {
                inputList.Clear();
                int inputListLength = rand.NextInt(0, 1000);
                for (int i = 0; i < inputListLength; i++)
                {
                    inputList.Add(rand.NextDouble(-10, 1000) * rand.NextDouble(-10, 1000));
                }

                set.AddRangeReadOnly(inputList);
                expectedListLength += inputListLength;
                Assert.AreEqual(expectedListLength, set.SampleCount);
                Assert.AreEqual(CalculateMeanNaive(set.Samples), set.Mean, 0.00001);
                Assert.AreEqual(CalculateVarianceNaive(set.Samples), set.Variance, 0.01);
                Assert.AreEqual(CalculateStdDevNaive(set.Samples), set.StandardDeviation, 0.00001);
                Assert.IsTrue(set.Mean <= 1000000);
                Assert.IsTrue(set.StandardDeviation <= 1000000);
            }
        }

        [TestMethod]
        public void TestStatisticalSetAddArrayInvalidArgs()
        {
            StatisticalSet set = new StatisticalSet();

            try
            {
                set.AddRange(null, 0, 10);
                Assert.Fail("Should have thrown an ArgumentNullException");
            }
            catch (ArgumentNullException) { }

            double[] inputArray = new double[10];
            try
            {
                set.AddRange(inputArray, -1, 5);
                Assert.Fail("Should have thrown an IndexOutOfRangeException");
            }
            catch (IndexOutOfRangeException) { }

            try
            {
                set.AddRange(inputArray, 10, 5);
                Assert.Fail("Should have thrown an IndexOutOfRangeException");
            }
            catch (IndexOutOfRangeException) { }

            try
            {
                set.AddRange(inputArray, 0, -1);
                Assert.Fail("Should have thrown an ArgumentOutOfRangeException");
            }
            catch (ArgumentOutOfRangeException) { }

            try
            {
                set.AddRange(inputArray, 5, 6);
                Assert.Fail("Should have thrown an IndexOutOfRangeException");
            }
            catch (IndexOutOfRangeException) { }

            inputArray[0] = 1;
            inputArray[1] = 2;
            inputArray[2] = 3;
            inputArray[3] = 4;
            inputArray[4] = double.NaN;

            try
            {
                set.AddRange(inputArray, 0, 5);
                Assert.Fail("Should have thrown an ArgumentException");
            }
            catch (ArgumentException) { }

            inputArray[4] = double.NegativeInfinity;

            try
            {
                set.AddRange(inputArray, 0, 5);
                Assert.Fail("Should have thrown an ArgumentException");
            }
            catch (ArgumentException) { }

            inputArray[4] = double.PositiveInfinity;

            try
            {
                set.AddRange(inputArray, 0, 5);
                Assert.Fail("Should have thrown an ArgumentException");
            }
            catch (ArgumentException) { }

            set.AddRange(inputArray, 0, 4);
            set.AddRange(inputArray, 0, 0);
        }

        [TestMethod]
        public void TestStatisticalSetAddArray()
        {
            StatisticalSet set = new StatisticalSet();
            IRandom rand = new FastRandom(3112);
            double[] inputArray = new double[1000];
            int expectedListLength = 0;

            for (int c = 0; c < 100; c++)
            {
                int inputArrayStart = rand.NextInt(0, 100);
                int inputArrayLength = rand.NextInt(0, inputArray.Length - inputArrayStart);
                for (int i = 0; i < inputArrayLength; i++)
                {
                    inputArray[i + inputArrayStart] = rand.NextDouble(-10, 1000) * rand.NextDouble(-10, 1000);
                }

                set.AddRange(inputArray, inputArrayStart, inputArrayLength);
                expectedListLength += inputArrayLength;
                Assert.AreEqual(expectedListLength, set.SampleCount);
                Assert.AreEqual(CalculateMeanNaive(set.Samples), set.Mean, 0.00001);
                Assert.AreEqual(CalculateVarianceNaive(set.Samples), set.Variance, 0.01);
                Assert.AreEqual(CalculateStdDevNaive(set.Samples), set.StandardDeviation, 0.00001);
                Assert.IsTrue(set.Mean <= 1000000);
                Assert.IsTrue(set.StandardDeviation <= 1000000);
            }
        }

        [TestMethod]
        public void TestStatisticalSetMedian()
        {
            StatisticalSet set = new StatisticalSet();

            // No input
            Assert.AreEqual(0, set.Median);

            // A single value
            set.Add(-500);
            Assert.AreEqual(-500, set.Median);

            // Two values                            
            double[] samples = new double[] { 6, 10 };
            set.Clear();
            set.AddRange(samples, 0, samples.Length);
            Assert.AreEqual(6, set.Median);

            // Two identical values                            
            samples = new double[] { 6, 6 };
            set.Clear();
            set.AddRange(samples, 0, samples.Length);
            Assert.AreEqual(6, set.Median);

            // Three values                            
            samples = new double[] { 6, 10, 20 };
            set.Clear();
            set.AddRange(samples, 0, samples.Length);
            Assert.AreEqual(10, set.Median);

            // Sorted input set - odd N
            //                                   \/                                 
            samples = new double[] { 1, 3, 6, 7, 14, 17, 20, 30, 56 };
            set.Clear();
            set.AddRange(samples, 0, samples.Length);
            Assert.AreEqual(14, set.Median);
            Assert.AreEqual(14, set.Median);

            // Unsorted input set - odd N                         
            samples = new double[] { 20, 17, 30, 56, 1, 7, 3, 14, 6 };
            set.Clear();
            set.AddRange(samples, 0, samples.Length);
            Assert.AreEqual(14, set.Median);
            Assert.AreEqual(14, set.Median);

            // Sorted input set - even N
            //                                 \/                                 
            samples = new double[] { 1, 3, 6, 7, 14, 17, 20, 30 };
            set.Clear();
            set.AddRange(samples, 0, samples.Length);
            Assert.AreEqual(7, set.Median);

            // All values are identical, odd length
            samples = new double[] { 7, 7, 7, 7, 7, 7, 7, 7, 7 };
            set.Clear();
            set.AddRange(samples, 0, samples.Length);
            Assert.AreEqual(7, set.Median);

            // All values are identical, even length                     
            samples = new double[] { 7, 7, 7, 7, 7, 7, 7, 7 };
            set.Clear();
            set.AddRange(samples, 0, samples.Length);
            Assert.AreEqual(7, set.Median);

            // Median updates correctly when values are added to the set
            //                            \/
            samples = new double[] { 0, 1, 2, 3, 4 };
            set.Clear();
            set.AddRange(samples, 0, samples.Length);
            Assert.AreEqual(2, set.Median);
            set.Add(5);
            Assert.AreEqual(2, set.Median);
            set.Add(6);
            Assert.AreEqual(3, set.Median);
        }

        [TestMethod]
        public void TestStatisticalSetBisectionMedian()
        {
            StatisticalSet set = new StatisticalSet();

            // No input
            Assert.AreEqual(0, set.BisectionMedian);

            // A single value
            set.Add(-500);
            Assert.AreEqual(-500, set.BisectionMedian, 0.0001);

            // Two values                            
            double[] samples = new double[] { 6, 10 };
            set.Clear();
            set.AddRange(samples, 0, samples.Length);
            Assert.AreEqual(8, set.BisectionMedian, 0.0001);

            // Two identical values                            
            samples = new double[] { 6, 6 };
            set.Clear();
            set.AddRange(samples, 0, samples.Length);
            Assert.AreEqual(6, set.BisectionMedian, 0.0001);

            // Three values                            
            samples = new double[] { 6, 10, 20 };
            set.Clear();
            set.AddRange(samples, 0, samples.Length);
            Assert.AreEqual(10, set.BisectionMedian, 0.0001);

            // Sorted input set - odd N
            //                                   \/                                 
            samples = new double[] { 1, 3, 6, 7, 14, 17, 20, 30, 56 };
            set.Clear();
            set.AddRange(samples, 0, samples.Length);
            Assert.AreEqual(14, set.BisectionMedian, 0.0001);
            Assert.AreEqual(14, set.BisectionMedian, 0.0001);

            // Unsorted input set - odd N                         
            samples = new double[] { 20, 17, 30, 56, 1, 7, 3, 14, 6 };
            set.Clear();
            set.AddRange(samples, 0, samples.Length);
            Assert.AreEqual(14, set.BisectionMedian, 0.0001);
            Assert.AreEqual(14, set.BisectionMedian, 0.0001);

            // Sorted input set - even N
            //                                 \/                                 
            samples = new double[] { 1, 3, 6, 8, 12, 17, 20, 30 };
            set.Clear();
            set.AddRange(samples, 0, samples.Length);
            Assert.AreEqual(10, set.BisectionMedian, 0.0001);

            // All values are identical, odd length
            samples = new double[] { 7, 7, 7, 7, 7, 7, 7, 7, 7 };
            set.Clear();
            set.AddRange(samples, 0, samples.Length);
            Assert.AreEqual(7, set.BisectionMedian);

            // All values are identical, even length
            samples = new double[] { 7, 7, 7, 7, 7, 7, 7, 7 };
            set.Clear();
            set.AddRange(samples, 0, samples.Length);
            Assert.AreEqual(7, set.BisectionMedian);

            // Median updates correctly when values are added to the set
            //                            \/
            samples = new double[] { 0, 1, 2, 3, 4 };
            set.Clear();
            set.AddRange(samples, 0, samples.Length);
            Assert.AreEqual(2, set.BisectionMedian, 0.0001);
            set.Add(5);
            Assert.AreEqual(2.5, set.BisectionMedian, 0.0001);
            set.Add(6);
            Assert.AreEqual(3, set.BisectionMedian, 0.0001);

            // Moderate skew to the left, odd length
            samples = new double[] { 1, 1, 7, 7, 7, 7, 7, 7, 7 };
            set.Clear();
            set.AddRange(samples, 0, samples.Length);
            Assert.AreEqual(4, set.BisectionMedian);

            // Moderate skew to the left, even length
            samples = new double[] { 1, 1, 7, 7, 7, 7, 7, 7 };
            set.Clear();
            set.AddRange(samples, 0, samples.Length);
            Assert.AreEqual(4, set.BisectionMedian);

            // Moderate skew to the right, odd length
            samples = new double[] { 7, 7, 7, 7, 7, 7, 7, 13, 13 };
            set.Clear();
            set.AddRange(samples, 0, samples.Length);
            Assert.AreEqual(10, set.BisectionMedian);

            // Moderate skew to the right, even length
            samples = new double[] { 7, 7, 7, 7, 7, 7, 13, 13 };
            set.Clear();
            set.AddRange(samples, 0, samples.Length);
            Assert.AreEqual(10, set.BisectionMedian);

            // Heavy skew to the left, odd length
            samples = new double[] { 1, 7, 7, 7, 7, 7, 7, 7, 7 };
            set.Clear();
            set.AddRange(samples, 0, samples.Length);
            Assert.AreEqual(4, set.BisectionMedian);

            // Heavy skew to the left, even length
            samples = new double[] { 1, 7, 7, 7, 7, 7, 7, 7 };
            set.Clear();
            set.AddRange(samples, 0, samples.Length);
            Assert.AreEqual(4, set.BisectionMedian);

            // Heavy skew to the right, odd length
            samples = new double[] { 7, 7, 7, 7, 7, 7, 7, 7, 13 };
            set.Clear();
            set.AddRange(samples, 0, samples.Length);
            Assert.AreEqual(10, set.BisectionMedian);

            // Heavy skew to the right, even length
            samples = new double[] { 7, 7, 7, 7, 7, 7, 7, 13 };
            set.Clear();
            set.AddRange(samples, 0, samples.Length);
            Assert.AreEqual(10, set.BisectionMedian);
        }

        // thread safety on all methods
        // accuracy of the math
        // accuracy of the caching after each operation
        // error handling when NaNs are encountered in large input sets

        private static double CalculateMeanNaive(IReadOnlyCollection<double> samples)
        {
            if (samples.Count == 0)
            {
                return 0;
            }

            double sum = 0;
            foreach (double sample in samples)
            {
                sum += sample;
            }

            return sum / (double)samples.Count;
        }

        private static double CalculateVarianceNaive(IReadOnlyCollection<double> samples)
        {
            if (samples.Count == 0)
            {
                return 0;
            }

            double mean = CalculateMeanNaive(samples);
            double sumVariance = 0;
            foreach (double sample in samples)
            {
                double delta = sample - mean;
                sumVariance += (delta * delta);
            }

            return sumVariance / (double)samples.Count;
        }

        private static double CalculateStdDevNaive(IReadOnlyCollection<double> samples)
        {
            return Math.Sqrt(CalculateVarianceNaive(samples));
        }
    }
}