using Durandal.Common.MathExt;
using Durandal.Common.Time;
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
    public class MovingAverageTests
    {
        [TestMethod]
        public void TestTimeWindowMovingAverage()
        {
            ManualTimeProvider realTime = new ManualTimeProvider();
            TimeWindowMovingAverage avg = new TimeWindowMovingAverage(5, TimeSpan.FromMinutes(1), realTime);
            Assert.IsFalse(avg.Average.HasValue);
            avg.Add(2);
            Assert.AreEqual(2, avg.Average.GetValueOrDefault(-1000), 0.01);
            realTime.Time = realTime.Time.AddSeconds(10);

            avg.Add(4);
            Assert.AreEqual(3, avg.Average.GetValueOrDefault(-1000), 0.01);
            realTime.Time = realTime.Time.AddSeconds(10);

            avg.Add(6);
            Assert.AreEqual(4, avg.Average.GetValueOrDefault(-1000), 0.01);
            realTime.Time = realTime.Time.AddSeconds(10);

            avg.Add(8);
            Assert.AreEqual(5, avg.Average.GetValueOrDefault(-1000), 0.01);
            realTime.Time = realTime.Time.AddSeconds(10);

            avg.Add(10);
            Assert.AreEqual(6, avg.Average.GetValueOrDefault(-1000), 0.01);
            realTime.Time = realTime.Time.AddSeconds(10);

            avg.Add(12);
            Assert.AreEqual(8, avg.Average.GetValueOrDefault(-1000), 0.01);

            realTime.Time = realTime.Time.AddSeconds(25);
            Assert.AreEqual(9, avg.Average.GetValueOrDefault(-1000), 0.01);

            realTime.Time = realTime.Time.AddSeconds(10);
            Assert.AreEqual(10, avg.Average.GetValueOrDefault(-1000), 0.01);

            realTime.Time = realTime.Time.AddSeconds(10);
            Assert.AreEqual(11, avg.Average.GetValueOrDefault(-1000), 0.01);

            realTime.Time = realTime.Time.AddSeconds(10);
            Assert.AreEqual(12, avg.Average.GetValueOrDefault(-1000), 0.01);

            realTime.Time = realTime.Time.AddSeconds(10);
            Assert.IsFalse(avg.Average.HasValue);
        }

        [TestMethod]
        public void TestMovingPercentileBasic()
        {
            MovingPercentile percentile = new MovingPercentile(5, 0.70, 0.90, 0.50, 0.10, 0.30);
            IRandom rand = new FastRandom(534);
            for (int clear = 0; clear < 10; clear++)
            {
                for (int loop = 0; loop < 1000; loop++)
                {
                    percentile.Add(rand.NextInt(1, 6));
                }

                percentile.Clear();
            }

            percentile.Add(5);
            percentile.Add(2);
            percentile.Add(4);
            percentile.Add(3);
            percentile.Add(1);
            Assert.AreEqual(1, percentile.GetPercentile(0.10));
            Assert.AreEqual(2, percentile.GetPercentile(0.30));
            Assert.AreEqual(3, percentile.GetPercentile(0.50));
            Assert.AreEqual(4, percentile.GetPercentile(0.70));
            Assert.AreEqual(5, percentile.GetPercentile(0.90));

            Assert.AreEqual(1, percentile.GetPercentile(0.09));
            Assert.AreEqual(2, percentile.GetPercentile(0.29));
            Assert.AreEqual(3, percentile.GetPercentile(0.49));
            Assert.AreEqual(4, percentile.GetPercentile(0.69));
            Assert.AreEqual(5, percentile.GetPercentile(0.89));

            Assert.AreEqual(1, percentile.GetPercentile(0.11));
            Assert.AreEqual(2, percentile.GetPercentile(0.31));
            Assert.AreEqual(3, percentile.GetPercentile(0.51));
            Assert.AreEqual(4, percentile.GetPercentile(0.71));
            Assert.AreEqual(5, percentile.GetPercentile(0.91));
        }

        [TestMethod]
        public void TestMovingPercentileComplex()
        {
            MovingPercentile percentile = new MovingPercentile(1000, 0.75, 0.99, 0.01, 0.25, 0.50);
            IRandom rand = new FastRandom(5418);
            for (int loop = 0; loop < 100000; loop++)
            {
                percentile.Add(100 * Math.Pow(rand.NextDouble(), 3));
            }

            Assert.AreEqual(100 * Math.Pow(0.01, 3), percentile.GetPercentile(0.01), 0.05);
            Assert.AreEqual(100 * Math.Pow(0.25, 3), percentile.GetPercentile(0.25), 0.5);
            Assert.AreEqual(100 * Math.Pow(0.50, 3), percentile.GetPercentile(0.50), 1.5);
            Assert.AreEqual(100 * Math.Pow(0.75, 3), percentile.GetPercentile(0.75), 3);
            Assert.AreEqual(100 * Math.Pow(0.99, 3), percentile.GetPercentile(0.99), 5);
        }

        [TestMethod]
        public void TestMovingPercentileNanInput()
        {
            MovingPercentile percentile = new MovingPercentile(100, 0.25, 0.5, 0.75);
            percentile.Add(5);
            percentile.Add(double.NaN);
            percentile.Add(double.PositiveInfinity);
            percentile.Add(double.NegativeInfinity);
            Assert.AreEqual(5, percentile.GetPercentile(0.5));
        }

        [TestMethod]
        public void TestMovingPercentileRandom()
        {
            MovingPercentile percentile = new MovingPercentile(1000, 0.001, 0.25, 0.5, 0.75, 0.999);
            IRandom random = new FastRandom(542887);
            for (int loop = 0; loop < 100; loop++)
            {
                percentile.Clear();
                for (int c = 0; c < 2000; c++)
                {
                    percentile.Add((random.NextDouble() - 0.5) * 200000);
                }
            }
        }
    }
}
