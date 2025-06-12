using Durandal.Common.Audio;
using Durandal.Common.Collections;
using Durandal.Common.MathExt;
using Durandal.Common.Test;
using Durandal.Common.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.Audio
{
    [TestClass]
    public class AudioMathTests
    {
        [TestMethod]
        public void TestAudioMath_ConvertTimeSpanToSamplesPerChannel()
        {
            Assert.AreEqual(16, AudioMath.ConvertTimeSpanToSamplesPerChannel(16000, TimeSpan.FromMilliseconds(1)));
            Assert.AreEqual(160, AudioMath.ConvertTimeSpanToSamplesPerChannel(16000, TimeSpan.FromMilliseconds(10)));
            Assert.AreEqual(1600, AudioMath.ConvertTimeSpanToSamplesPerChannel(16000, TimeSpan.FromMilliseconds(100)));
            Assert.AreEqual(16000, AudioMath.ConvertTimeSpanToSamplesPerChannel(16000, TimeSpan.FromMilliseconds(1000)));
            Assert.AreEqual(4410, AudioMath.ConvertTimeSpanToSamplesPerChannel(44100, TimeSpan.FromMilliseconds(100)));
        }

        [TestMethod]
        public void TestAudioMath_ConvertSamplesPerChannelToTimeSpan()
        {
            Assert.AreEqual(TimeSpan.FromMilliseconds(1), AudioMath.ConvertSamplesPerChannelToTimeSpan(16000, 16));
            Assert.AreEqual(TimeSpan.FromMilliseconds(10), AudioMath.ConvertSamplesPerChannelToTimeSpan(16000, 160));
            Assert.AreEqual(TimeSpan.FromMilliseconds(100), AudioMath.ConvertSamplesPerChannelToTimeSpan(16000, 1600));
            Assert.AreEqual(TimeSpan.FromMilliseconds(1000), AudioMath.ConvertSamplesPerChannelToTimeSpan(16000, 16000));
            Assert.AreEqual(TimeSpan.FromMilliseconds(1000), AudioMath.ConvertSamplesPerChannelToTimeSpan(44100, 44100));
        }

        [TestMethod]
        public void TestAudioMath_ConvertSamplesPerChannelToTicks()
        {
            Assert.AreEqual(TimeSpan.FromMilliseconds(1).Ticks, AudioMath.ConvertSamplesPerChannelToTicks(16000, 16));
            Assert.AreEqual(TimeSpan.FromMilliseconds(10).Ticks, AudioMath.ConvertSamplesPerChannelToTicks(16000, 160));
            Assert.AreEqual(TimeSpan.FromMilliseconds(100).Ticks, AudioMath.ConvertSamplesPerChannelToTicks(16000, 1600));
            Assert.AreEqual(TimeSpan.FromMilliseconds(1000).Ticks, AudioMath.ConvertSamplesPerChannelToTicks(16000, 16000));
            Assert.AreEqual(TimeSpan.FromMilliseconds(1000).Ticks, AudioMath.ConvertSamplesPerChannelToTicks(44100, 44100));
        }

        [TestMethod]
        public void TestAudioMath_ConvertTicksToSamplesPerChannel()
        {
            Assert.AreEqual(16, AudioMath.ConvertTicksToSamplesPerChannel(16000, TimeSpan.FromMilliseconds(1).Ticks));
            Assert.AreEqual(160, AudioMath.ConvertTicksToSamplesPerChannel(16000, TimeSpan.FromMilliseconds(10).Ticks));
            Assert.AreEqual(1600, AudioMath.ConvertTicksToSamplesPerChannel(16000, TimeSpan.FromMilliseconds(100).Ticks));
            Assert.AreEqual(16000, AudioMath.ConvertTicksToSamplesPerChannel(16000, TimeSpan.FromMilliseconds(1000).Ticks));
            Assert.AreEqual(4410, AudioMath.ConvertTicksToSamplesPerChannel(44100, TimeSpan.FromMilliseconds(100).Ticks));
        }

        [TestMethod]
        [DataRow(float.NegativeInfinity, 0)]
        [DataRow(-40.0f, 0.01f)]
        [DataRow(-30.0f, 0.03162f)]
        [DataRow(-20.0f, 0.1f)]
        [DataRow(-10.0f, 0.316f)]
        [DataRow(-6.0f, 0.501f)]
        [DataRow(0.0f, 1.0f)]
        [DataRow(6.0f, 1.995f)]
        [DataRow(10.0f, 3.16f)]
        [DataRow(20.0f, 10.0f)]
        [DataRow(30.0f, 31.62f)]
        [DataRow(40.0f, 100.0f)]
        public void TestAudioMath_DecibelConversion(float decibels, float linear)
        {
            Assert.AreEqual(linear, AudioMath.DecibelsToLinear(decibels), 0.01f);
            Assert.AreEqual(decibels, AudioMath.LinearToDecibels(linear), 0.01f);
        }

        [TestMethod]
        [ExpectedException(typeof(ArithmeticException))]
        public void TestAudioMath_DecibelConversionNaN()
        {
            AudioMath.DecibelsToLinear(float.NaN);
        }

        [TestMethod]
        [ExpectedException(typeof(ArithmeticException))]
        public void TestAudioMath_DecibelConversionInfinity()
        {
            AudioMath.DecibelsToLinear(float.PositiveInfinity);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void TestAudioMath_DecibelConversionNegative()
        {
            AudioMath.LinearToDecibels(-0.1f);
        }

        [TestMethod]
        [DataRow(0, false)]
        [DataRow(1, false)]
        [DataRow(4, false)]
        [DataRow(100, false)]
        [DataRow(9999, false)]
        [DataRow(0, true)]
        [DataRow(1, true)]
        [DataRow(4, true)]
        [DataRow(100, true)]
        [DataRow(9999, true)]
        public void TestAudioMath_ConvertFloatTo2BytesInt16LittleEndian(int samples, bool clamp)
        {
            IRandom rand = new FastRandom(99999);

            for (int loop = 0; loop < 50; loop++)
            {
                int floatPadding = rand.NextInt(10, 50);
                int bytePadding = rand.NextInt(10, 50);
                float[] inputBuffer = new float[floatPadding + samples];
                byte[] outputBuffer = new byte[bytePadding + (sizeof(short) * samples)];
                byte[] expectedOutput = new byte[sizeof(short) * samples];

                for (int sample = 0; sample < samples; sample++)
                {
                    float floatVal;
                    short sampleVal;
                    if (clamp)
                    {
                        floatVal = (rand.NextFloat() - 0.5f) * 3.0f;
                        if (floatVal > 1.0f)
                        {
                            sampleVal = 32767;
                        }
                        else if (floatVal < -1.0f)
                        {
                            sampleVal = -32767;
                        }
                        else
                        {
                            sampleVal = (short)(floatVal * 32767.0f);
                        }
                    }
                    else
                    {
                        floatVal = (rand.NextFloat() - 0.5f);
                        sampleVal = (short)(floatVal * 32767.0f);
                    }

                    inputBuffer[floatPadding + sample] = floatVal;
                    expectedOutput[(sample * 2) + 0] = (byte)((sampleVal >> 0) & 0xFF);
                    expectedOutput[(sample * 2) + 1] = (byte)((sampleVal >> 8) & 0xFF);
                }

                AudioMath.ConvertSamples_FloatTo2BytesIntLittleEndian(
                    inputBuffer,
                    floatPadding,
                    outputBuffer,
                    bytePadding,
                    samples,
                    clamp);

                Assert.IsTrue(ArrayExtensions.ArrayEquals(outputBuffer, bytePadding, expectedOutput, 0, samples * sizeof(short)));
            }
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(4)]
        [DataRow(100)]
        [DataRow(9999)]
        public void TestAudioMath_Convert2BytesToFloat(int samples)
        {
            IRandom rand = new FastRandom(621);

            for (int loop = 0; loop < 50; loop++)
            {
                int floatPadding = rand.NextInt(10, 50);
                int bytePadding = rand.NextInt(10, 50);
                byte[] inputBuffer = new byte[bytePadding + (sizeof(short) * samples)];
                float[] outputBuffer = new float[floatPadding + samples];
                float[] expectedOutput = new float[samples];

                for (int sample = 0; sample < samples; sample++)
                {
                    expectedOutput[sample] = (rand.NextFloat() - 0.5f);
                }

                // prepare input float -> bytes
                AudioMath.ConvertSamples_FloatTo2BytesIntLittleEndian(
                    expectedOutput,
                    0,
                    inputBuffer,
                    bytePadding,
                    samples,
                    clamp: false);

                // Now convert bytes -> floats - this is what we're actually testing
                AudioMath.ConvertSamples_2BytesIntLittleEndianToFloat(
                    inputBuffer, bytePadding, outputBuffer, floatPadding, samples);

                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedOutput, 0, outputBuffer, floatPadding, samples, maxDelta: 0.001f));
            }
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(4)]
        [DataRow(100)]
        [DataRow(9999)]
        public void TestAudioMath_ConvertFloatToInt16_RoundTripUnclamped(int samples)
        {
            IRandom rand = new FastRandom(6422);

            for (int loop = 0; loop < 50; loop++)
            {
                int floatPadding = rand.NextInt(10, 50);
                int shortPadding = rand.NextInt(10, 50);
                float[] inputData = new float[floatPadding + samples];
                short[] shortData = new short[shortPadding + samples];
                float[] outputData = new float[floatPadding + samples];

                for (int sample = 0; sample < samples; sample++)
                {
                    inputData[floatPadding + sample] = (rand.NextFloat() - 0.5f) * 1.0f;  // range -0.5 to 0.5
                }

                // Convert input float -> int16
                AudioMath.ConvertSamples_FloatToInt16(inputData, floatPadding, shortData, shortPadding, samples, clamp: false);

                // Convert back int16 -> output float
                AudioMath.ConvertSamples_Int16ToFloat(shortData, shortPadding, outputData, floatPadding, samples);

                Assert.IsTrue(ArrayExtensions.ArrayEquals(inputData, floatPadding, outputData, floatPadding, samples, maxDelta: 0.001f));
            }
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(4)]
        [DataRow(100)]
        [DataRow(9999)]
        public void TestAudioMath_ConvertFloatToInt16_RoundTripClamped(int samples)
        {
            IRandom rand = new FastRandom(43);

            for (int loop = 0; loop < 50; loop++)
            {
                int floatPadding = rand.NextInt(10, 50);
                int shortPadding = rand.NextInt(10, 50);
                float[] inputData = new float[floatPadding + samples];
                short[] shortData = new short[shortPadding + samples];
                float[] outputData = new float[floatPadding + samples];
                float[] expectedOutput = new float[samples];

                for (int sample = 0; sample < samples; sample++)
                {
                    float thisValue = (rand.NextFloat() - 0.5f) * 3.0f; // range -1.5 to 1.5
                    inputData[floatPadding + sample] = thisValue;
                    if (thisValue > 1.0f)
                    {
                        expectedOutput[sample] = 1.0f;
                    }
                    else if (thisValue < -1.0f)
                    {
                        expectedOutput[sample] = -1.0f;
                    }
                    else
                    {
                        expectedOutput[sample] = thisValue;
                    }
                }

                // Convert input float -> int16
                AudioMath.ConvertSamples_FloatToInt16(inputData, floatPadding, shortData, shortPadding, samples, clamp: true);

                // Convert back int16 -> output float
                AudioMath.ConvertSamples_Int16ToFloat(shortData, shortPadding, outputData, floatPadding, samples);

                // Compare float sample similarity
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedOutput, 0, outputData, floatPadding, samples, maxDelta: 0.001f));
            }
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(4)]
        [DataRow(100)]
        [DataRow(9999)]
        public void TestAudioMath_ConvertFloatToInt16_ArrayAndSpanParity_Unclamped(int samples)
        {
            IRandom rand = new FastRandom(9123111);

            for (int loop = 0; loop < 50; loop++)
            {
                int floatPadding = rand.NextInt(10, 50);
                int shortPadding = rand.NextInt(10, 50);
                float[] inputData = new float[floatPadding + samples];
                short[] arrayOutputData = new short[shortPadding + samples];
                short[] spanOutputData = new short[shortPadding + samples];

                for (int sample = 0; sample < samples; sample++)
                {
                    inputData[floatPadding + sample] = (rand.NextFloat() - 0.5f) * 1.0f; // range -0.5 to 0.5
                }

                AudioMath.ConvertSamples_FloatToInt16(inputData, floatPadding, arrayOutputData, shortPadding, samples, clamp: false);
                AudioMath.ConvertSamples_FloatToInt16(inputData.AsSpan(floatPadding), spanOutputData.AsSpan(shortPadding), samples, clamp: false);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(arrayOutputData, shortPadding, spanOutputData, shortPadding, samples));
            }
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(4)]
        [DataRow(100)]
        [DataRow(9999)]
        public void TestAudioMath_ConvertFloatToInt16_ArrayAndSpanParity_Clamped(int samples)
        {
            IRandom rand = new FastRandom(98953);

            for (int loop = 0; loop < 50; loop++)
            {
                int floatPadding = rand.NextInt(10, 50);
                int shortPadding = rand.NextInt(10, 50);
                float[] inputData = new float[floatPadding + samples];
                short[] arrayOutputData = new short[shortPadding + samples];
                short[] spanOutputData = new short[shortPadding + samples];
                for (int sample = 0; sample < samples; sample++)
                {
                    inputData[floatPadding + sample] = (rand.NextFloat() - 0.5f) * 3.0f; // range -1.5 to 1.5
                }

                AudioMath.ConvertSamples_FloatToInt16(inputData, floatPadding, arrayOutputData, shortPadding, samples, clamp: true);
                AudioMath.ConvertSamples_FloatToInt16(inputData.AsSpan(floatPadding), spanOutputData.AsSpan(shortPadding), samples, clamp: true);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(arrayOutputData, shortPadding, spanOutputData, shortPadding, samples));
            }
        }

        [TestMethod]
        [DataRow(float.NaN, false, (short)0)]
        [DataRow(float.NaN, true, (short)-32767)]
        [DataRow(float.NegativeInfinity, false, (short)0)]
        [DataRow(float.NegativeInfinity, true, (short)-32767)]
        [DataRow(float.PositiveInfinity, false, (short)0)]
        [DataRow(float.PositiveInfinity, true, (short)-32767)]
        public void TestAudioMath_ConvertFloatToInt16Array_InvalidInputs(float inVal, bool clamp, short outVal)
        {
            const int samples = 128;
            for (int loop = 0; loop < 50; loop++)
            {
                float[] inputData = new float[samples];
                short[] outputData = new short[samples];
                short[] expectedOutput = new short[samples];
                inputData.AsSpan().Fill(inVal);
                expectedOutput.AsSpan().Fill(outVal);

                AudioMath.ConvertSamples_FloatToInt16(inputData, 0, outputData, 0, samples, clamp: clamp);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedOutput, 0, outputData, 0, samples));
            }
        }

        [TestMethod]
        [DataRow(float.NaN, false, (short)0)]
        [DataRow(float.NaN, true, (short)-32767)]
        [DataRow(float.NegativeInfinity, false, (short)0)]
        [DataRow(float.NegativeInfinity, true, (short)-32767)]
        [DataRow(float.PositiveInfinity, false, (short)0)]
        [DataRow(float.PositiveInfinity, true, (short)-32767)]
        public void TestAudioMath_ConvertFloatToInt16Span_InvalidInputs(float inVal, bool clamp, short outVal)
        {
            const int samples = 128;
            for (int loop = 0; loop < 50; loop++)
            {
                float[] inputData = new float[samples];
                short[] outputData = new short[samples];
                short[] expectedOutput = new short[samples];
                inputData.AsSpan().Fill(inVal);
                expectedOutput.AsSpan().Fill(outVal);

                AudioMath.ConvertSamples_FloatToInt16(inputData.AsSpan(), outputData.AsSpan(), samples, clamp: clamp);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedOutput, 0, outputData, 0, samples));
            }
        }

        [TestMethod]
        public void TestAudioMath_ScaleAndMoveSamples()
        {
            IRandom rand = new FastRandom(54123);

            for (int loop = 0; loop < 50; loop++)
            {
                float scale = rand.NextFloat() * 2.0f;
                int samples = rand.NextInt(1, 1000);
                int inputPadding = rand.NextInt(10, 50);
                int outputPadding = rand.NextInt(10, 50);
                float[] inputData = new float[inputPadding + samples];
                float[] outputData = new float[outputPadding + samples];

                for (int sample = 0; sample < samples; sample++)
                {
                    inputData[inputPadding + sample] = (rand.NextFloat() - 0.5f) * 2.0f;  // range -0.5 to 0.5
                }

                AudioMath.ScaleAndMoveSamples(inputData, inputPadding, outputData, outputPadding, samples, scale);
                for (int sample = 0; sample < samples; sample++)
                {
                    Assert.AreEqual(inputData[inputPadding + sample] * scale, outputData[outputPadding + sample], 0.001f);
                }
            }
        }

        [TestMethod]
        public void TestAudioMath_ScaleSamples()
        {
            IRandom rand = new FastRandom(54123);

            for (int loop = 0; loop < 50; loop++)
            {
                float scale = rand.NextFloat() * 2.0f;
                int samples = rand.NextInt(1, 1000);
                int inputPadding = rand.NextInt(10, 50);
                float[] inputData = new float[inputPadding + samples];
                float[] expectedOutputData = new float[samples];

                for (int sample = 0; sample < samples; sample++)
                {
                    inputData[inputPadding + sample] = (rand.NextFloat() - 0.5f) * 2.0f;
                    expectedOutputData[sample] = inputData[inputPadding + sample] * scale;
                }

                AudioMath.ScaleSamples(inputData, inputPadding, samples, scale);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedOutputData, 0, inputData, inputPadding, samples, maxDelta: 0.001f));
            }
        }
    }
}
