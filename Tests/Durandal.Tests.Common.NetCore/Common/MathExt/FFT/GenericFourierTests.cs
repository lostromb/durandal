using Durandal.Common.MathExt;
using Durandal.Common.MathExt.FFT;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.MathExt.FFT
{
    [TestClass]
    public class GenericFourierTests
    {
        [TestMethod]
        public void TestFourier1DTransform32Bit()
        {
            for (int inputSizeBase = 5; inputSizeBase < 13; inputSizeBase++)
            {
                int inputSize = 0x1 << inputSizeBase;
                Console.WriteLine("Testing 32-bit FFT with input size " + inputSize);
                float[] input = new float[inputSize];
                for (int c = 0; c < inputSize; c++)
                {
                    input[c] = (float)Math.Sin((double)c / 100.0);
                }

                ComplexF[] fourierSet = new ComplexF[inputSize];
                for (int c = 0; c < inputSize; c++)
                {
                    fourierSet[c].Re = input[c];
                }

                // Run FFT and then undo it
                Fourier.FFT(fourierSet, inputSize, FourierDirection.Forward);
                Fourier.FFT(fourierSet, inputSize, FourierDirection.Backward);

                // Scale back magnitude of the output
                for (int c = 0; c < inputSize; c++)
                {
                    fourierSet[c].Re /= (float)inputSize;
                }

                for (int c = 0; c < inputSize; c++)
                {
                    Assert.AreEqual(input[c], fourierSet[c].Re, 0.01f);
                    Assert.AreEqual(0, fourierSet[c].Im, 0.01f);
                }
            }
        }

        [TestMethod]
        public void TestFourier1DTransform64Bit()
        {
            for (int inputSizeBase = 5; inputSizeBase < 13; inputSizeBase++)
            {
                int inputSize = 0x1 << inputSizeBase;
                Console.WriteLine("Testing 64-bit FFT with input size " + inputSize);
                double[] input = new double[inputSize];
                for (int c = 0; c < inputSize; c++)
                {
                    input[c] = Math.Sin((double)c / 100.0);
                }

                Complex[] fourierSet = new Complex[inputSize];
                for (int c = 0; c < inputSize; c++)
                {
                    fourierSet[c].Re = input[c];
                }

                // Run FFT and then undo it
                Fourier.FFT(fourierSet, inputSize, FourierDirection.Forward);
                Fourier.FFT(fourierSet, inputSize, FourierDirection.Backward);

                // Scale back magnitude of the output
                for (int c = 0; c < inputSize; c++)
                {
                    fourierSet[c].Re /= (double)inputSize;
                }

                for (int c = 0; c < inputSize; c++)
                {
                    Assert.AreEqual(input[c], fourierSet[c].Re, 0.01);
                    Assert.AreEqual(0, fourierSet[c].Im, 0.01);
                }
            }
        }

    }
}
