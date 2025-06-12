using Durandal.Common.MathExt;
using Durandal.Common.Time;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.Time
{
    [TestClass]
    public class StopwatchTests
    {
        [TestMethod]
        public async Task TestValueStopwatchParity()
        {
            using (CancellationTokenSource testCancel = new CancellationTokenSource(TimeSpan.FromSeconds(4)))
            {
                CancellationToken testFinished = testCancel.Token;
                Stopwatch a = new Stopwatch();
                ValueStopwatch b = new ValueStopwatch();
                IRandom rand = new DefaultRandom();

                while (!testFinished.IsCancellationRequested)
                {
                    int next = rand.NextInt(0, 10);
                    if (next < 3)
                    {
                        a.Restart();
                        b.Restart();
                    }
                    else if (next < 6)
                    {
                        a.Start();
                        b.Start();
                    }
                    else
                    {
                        a.Stop();
                        b.Stop();
                    }

                    await Task.Delay(rand.NextInt(1, 60));

                    // Stopwatches should be within 5ms of each other
                    Assert.AreEqual(a.Elapsed.Ticks, b.Elapsed.Ticks, 50000);
                    Assert.AreEqual(a.ElapsedTicks, b.ElapsedTicks, 50000);
                    Assert.AreEqual(a.ElapsedMilliseconds, b.ElapsedMilliseconds, 5);
                    Assert.AreEqual(a.ElapsedMillisecondsPrecise(), b.ElapsedMillisecondsPrecise(), 5.0);
                }
            }
        }
    }
}
