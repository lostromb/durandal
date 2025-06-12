using Durandal.Common.MathExt;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Audio
{
    public static class HermitianInterpolation
    {
        /// <summary>
        /// Fast lookup table for a Hermitian curve between 0.0 and 1.0
        /// </summary>
        private static readonly float[] HERMITIAN_CURVE_LUT = new float[100];

        static HermitianInterpolation()
        {
            // Precalculate hermitian curve
            for (int cacheIndex = 0; cacheIndex < 100; cacheIndex++)
            {
                float x = (float)cacheIndex / 100.0f;
                HERMITIAN_CURVE_LUT[cacheIndex] = 0 - ((2 * x * x * x) - (3 * x * x));
            }
        }

        /// <summary>
        /// Performs Hermitian resampling on an interleaved audio buffer
        /// </summary>
        /// <param name="inputBuffer">The input buffer to read samples from</param>
        /// <param name="inputOffset">The absolute offset when reading from input buffer</param>
        /// <param name="inputSamplesPerChannel">The number of samples per channel you want to read from the input.
        /// After processing, this value will be set to the number of samples per channel actually processed.</param>
        /// <param name="outputBuffer">The output buffer to write samples to</param>
        /// <param name="outputOffset">The absolute offset when writing to output buffer</param>
        /// <param name="outputSamplesPerChannel">The maximum number of samples per channel you want to write to the output (buffer size).
        /// After processing, this value will be set to the number of samples per channel actually processed.</param>
        /// <param name="rate">The rate of stretching to apply. Rates above 1.0 will upsample, below 1.0 will downsample</param>
        /// <param name="numChannels">The number of interleaved channels in the signal</param>
        public static void ResampleInterleavedByRate(
            float[] inputBuffer,
            int inputOffset,
            ref int inputSamplesPerChannel,
            float[] outputBuffer,
            int outputOffset,
            ref int outputSamplesPerChannel,
            double rate,
            int numChannels)
        {
            if (rate <= 0)
            {
                throw new ArgumentOutOfRangeException("Rate cannot be zero");
            }
            if (inputOffset < 0)
            {
                throw new ArgumentOutOfRangeException("Input offset cannot be negative");
            }
            if (outputOffset < 0)
            {
                throw new ArgumentOutOfRangeException("Output offset cannot be negative");
            }
            if (inputSamplesPerChannel < 0)
            {
                throw new ArgumentOutOfRangeException("Input samples cannot be negative");
            }
            if (outputSamplesPerChannel < 0)
            {
                throw new ArgumentOutOfRangeException("Output samples cannot be negative");
            }
            if (numChannels < 1)
            {
                throw new ArgumentOutOfRangeException("numChannels must be a positive integer");
            }

            outputSamplesPerChannel = FastMath.Min(outputSamplesPerChannel, (int)((double)inputSamplesPerChannel / rate));
            inputSamplesPerChannel = FastMath.Max(0, (int)((double)outputSamplesPerChannel * rate));

            if (outputSamplesPerChannel == 0)
            {
                inputSamplesPerChannel = 0;
                return;
            }
            else if (inputSamplesPerChannel == 0)
            {
                outputSamplesPerChannel = 0;
                return;
            }

            // floating index of the current sample we are on logically WITHIN the current channel
            double readIndex = 0;
            int writeArrayIndex = outputOffset;
            if (numChannels == 1)
            {
                for (int c = 0; c < outputSamplesPerChannel; c++)
                {
                    float lb = (float)Math.Floor(readIndex);
                    float mix = HERMITIAN_CURVE_LUT[(int)((readIndex - lb) * 100)];
                    int readArrayIndex = ((int)lb) + inputOffset;
                    outputBuffer[writeArrayIndex] =
                        (inputBuffer[readArrayIndex] * (1 - mix)) +
                        (inputBuffer[readArrayIndex + 1] * mix);
                    readIndex += rate;
                    writeArrayIndex++;
                }
            }
            else if (numChannels == 2)
            {
                for (int c = 0; c < outputSamplesPerChannel; c++)
                {
                    float lb = (float)Math.Floor(readIndex);
                    float mix = HERMITIAN_CURVE_LUT[(int)((readIndex - lb) * 100)];
                    int readArrayIndex = (((int)lb) * numChannels) + inputOffset;
                    // Unrolled loop to reduce branching in the 2 channel case
                    outputBuffer[writeArrayIndex] =
                        (inputBuffer[readArrayIndex] * (1 - mix)) +
                        (inputBuffer[readArrayIndex + numChannels] * mix);
                    outputBuffer[writeArrayIndex + 1] =
                            (inputBuffer[readArrayIndex + 1] * (1 - mix)) +
                            (inputBuffer[readArrayIndex + numChannels + 1] * mix);
                    writeArrayIndex += 2;
                    readIndex += rate;
                }
            }
            else
            {
                for (int c = 0; c < outputSamplesPerChannel; c++)
                {
                    float lb = (float)Math.Floor(readIndex);
                    float mix = HERMITIAN_CURVE_LUT[(int)((readIndex - lb) * 100)];
                    int readArrayIndex = (((int)lb) * numChannels) + inputOffset;
                    for (int channel = 0; channel < numChannels; channel++)
                    {
                        outputBuffer[writeArrayIndex] =
                            (inputBuffer[readArrayIndex] * (1 - mix)) +
                            (inputBuffer[readArrayIndex + numChannels] * mix);
                        readArrayIndex++;
                        writeArrayIndex++;
                    }

                    readIndex += rate;
                }
            }
        }
    }
}
