using Durandal.Common.Audio;
using Durandal.Common.MathExt;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.Audio
{
    [TestClass]
    public class VolumeMeterTests
    {
        [TestMethod]
        public void TestRmsVolumeHighestVolume()
        {
            const int setLength = 100;
            const int inputSize = 10000;
            MovingAverageRmsVolume vol = new MovingAverageRmsVolume(setLength, 0.0f);
            float[] inputSet = new float[inputSize];
            IRandom rand = new FastRandom(33801);
            for (int c = 0; c < inputSet.Length; c++)
            {
                inputSet[c] = (rand.NextFloat() * 2.0f) - 1.0f;
            }

            for (int setEndIdx = 0; setEndIdx < inputSize; setEndIdx++)
            {
                float expectedVal = 0.0f;
                for (int c = Math.Max(0, setEndIdx - setLength); c < setEndIdx; c++)
                {
                    expectedVal = Math.Max(expectedVal, Math.Abs(inputSet[c]));
                }

                Assert.AreEqual(expectedVal, vol.PeakVolume, 0.0001f);
                vol.Add(inputSet[setEndIdx]);
            }
        }
    }
}
