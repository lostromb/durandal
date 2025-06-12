using Durandal.Common.MathExt;
using Durandal.Common.Time;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.Time
{
    [TestClass]
    public class HighPrecisionTimerTests
    {
        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            // Warmup
            for (int c = 0; c < 100; c++)
            {
                HighPrecisionTimer.GetCurrentTicks();
            }
        }

        [TestMethod]
        public void TestHighPrecisionTimerCurrentTicks()
        {
            StatisticalSet stats = new StatisticalSet(1000000);
            Stopwatch executionTimer = Stopwatch.StartNew();
            while (executionTimer.ElapsedMilliseconds < 100)
            {
                long actualTicks = DateTimeOffset.UtcNow.Ticks; // relying on NetCore's improved UtcNow precision - this won't work in NetFX
                long testTicks = HighPrecisionTimer.GetCurrentTicks();
                TimeSpan delta = TimeSpan.FromTicks(Math.Abs(actualTicks - testTicks));
                stats.Add(delta.TotalMilliseconds);
            }

            Console.WriteLine(stats.ToString());
            Assert.IsTrue(stats.Mean < 0.01, "Expected a time delta of less than 0.01ms from wallclock, instead got " + stats.Mean + "ms");
        }

        [TestMethod]
        public void TestHighPrecisionTimerCurrentTime()
        {
            StatisticalSet stats = new StatisticalSet(1000000);
            Stopwatch executionTimer = Stopwatch.StartNew();
            while (executionTimer.ElapsedMilliseconds < 100)
            {
                DateTimeOffset actualTime = DateTimeOffset.UtcNow; // relying on NetCore's improved UtcNow precision - this won't work in NetFX
                DateTimeOffset testTime = HighPrecisionTimer.GetCurrentUTCTime();
                TimeSpan delta = TimeSpan.FromTicks(Math.Abs(actualTime.Ticks - testTime.Ticks));
                stats.Add(delta.TotalMilliseconds);
            }

            Console.WriteLine(stats.ToString());
            Assert.IsTrue(stats.Mean < 0.01, "Expected a time delta of less than 0.01ms from wallclock, instead got " + stats.Mean + "ms");
        }
    }
}
