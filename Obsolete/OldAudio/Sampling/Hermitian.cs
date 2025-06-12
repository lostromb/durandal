using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Sampling
{
    public static class Hermitian
    {
        private static float[] _cache = new float[100];

        static Hermitian()
        {
            for (int c = 0; c < _cache.Length; c++)
            {
                _cache[c] = float.NaN;
            }
        }
        
        /// <summary>
        /// A basic implementation of Hermitian (Catmull-Rom) interpolation in 1 dimension
        /// </summary>
        /// <param name="input"></param>
        /// <param name="inputSampleRate"></param>
        /// <param name="outputSampleRate"></param>
        /// <returns></returns>
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
                float index = ((float)c * (float)(input.Length - 1) / (float)requiredSize);
                float lb = (float)Math.Floor(index);
                float mix = HermitianCurve(index - lb);
                returnVal[c] = (short)(((float)input[(int)lb] * (1 - mix)) +
                                       ((float)input[(int)lb + 1] * mix));
            }

            return returnVal;
        }

        private static float HermitianCurve(float x)
        {
            if (x <= 0)
                return 0;
            if (x >= 1)
                return 1;
            // Lookup cached curve; calculate it if necessary
            int cacheIndex = (int)(x * 100);
            if (float.IsNaN(_cache[cacheIndex]))
                _cache[cacheIndex] = (2 * x * x * x) - (3 * x * x);
            return _cache[cacheIndex];
        }
    }
}
