using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Sampling
{
    public static class Linear
    {
        public static short[] Resample(short[] input, int inputSampleRate, int outputSampleRate)
        {
            // check input
            if (inputSampleRate == outputSampleRate)
                return input;
            if (outputSampleRate == 0 || inputSampleRate == 0) // Prevent divide by zero
                return input;

            int requiredSize = (int)((double)input.Length * (double)outputSampleRate / (double)inputSampleRate);
            short[] returnVal = new short[requiredSize];

            for (int c = 0; c < requiredSize; c++)
            {
                double index = ((double)c * (double)(input.Length - 1) / (double)requiredSize);
                double lb = Math.Floor(index);
                double mix = index - lb;
                returnVal[c] = (short)(((double)input[(int)lb] * (1 - mix)) +
                                       ((double)input[(int)lb + 1] * mix));
            }

            return returnVal;
        }
    }
}
