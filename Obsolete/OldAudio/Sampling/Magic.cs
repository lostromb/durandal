using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Sampling
{
    public static class Magic
    {
        private static double PISQ = Math.PI * Math.PI;
        private static double[] _lanczosCache = new double[600];
        private static double[] _hermCache = new double[100];
        private const double MIX = 0.50;

        static Magic()
        {
            for (int c = 0; c < _hermCache.Length; c++)
            {
                _hermCache[c] = double.NaN;
            }
            for (int c = 0; c < _lanczosCache.Length; c++)
            {
                _lanczosCache[c] = double.NaN;
            }
        }

        /// <summary>
        /// Returns an aggregate of the Hermitian and Lanczos interpolations. This allows us to get the smooth spline
        /// of the Lanczos kernel as well as some of the artificial highs of the Hermitian kernel
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

            int degree = 3;
            int requiredSize = (int)((double)input.Length * (double)outputSampleRate / (double)inputSampleRate);
            short[] returnVal = new short[requiredSize];

            for (int c = 0; c < requiredSize; c++)
            {
                double x = ((double)c * (double)(input.Length - 1) / (double)requiredSize);
                double lanczosComponent = 0;
                int lb = (int)Math.Floor(x);
                for (int i = lb - degree + 1; i < lb + degree; i++)
                {
                    // clamp i so we don't overstep the input array
                    int clampedI = i;
                    if (i < 0)
                        clampedI = 0;
                    if (i >= input.Length)
                        clampedI = input.Length - 1;

                    lanczosComponent += input[clampedI] * LanczosKernel(x - i, degree);
                }
            
                double mix = HermitianCurve(x - lb);
                double hermitianComponent = (((double)input[lb] * (1 - mix)) +
                                        ((double)input[lb + 1] * mix));
            
                //amount to weight towards lanczos
                returnVal[c] = (short)((MIX * lanczosComponent) + ((1.0 - MIX) * hermitianComponent));
            }

            return returnVal;
        }

        private static double LanczosKernel(double x, int a)
        {
            if (x == 0)
                return 1;
            if (x >= a || x <= (0 - a))
                return 0;
            int cacheIndex = (int)(x * 100) + 300;
            if (double.IsNaN(_lanczosCache[cacheIndex]))
                _lanczosCache[cacheIndex] = a * Math.Sin(Math.PI * x) * Math.Sin(Math.PI * x / a) / (PISQ * x * x);
            return _lanczosCache[cacheIndex];
        }
    
        private static double HermitianCurve(double x)
        {
            if (x <= 0)
                return 0;
            if (x >= 1)
                return 1;
            int cacheIndex = (int)(x * 100);
            if (double.IsNaN(_hermCache[cacheIndex]))
                _hermCache[cacheIndex] = (2 * x * x * x) - (3 * x * x);
            return _hermCache[cacheIndex];
        }
    }
}
