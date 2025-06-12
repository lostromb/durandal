using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers
{
    public class HaarWavelet
    {
        /// <summary>
        /// Transforms an arbitrary-sized matrix using the 2D Haar wavelet transform. NPOT matrices will be padded with zero.
        /// The result is the convoluted matrix with the highest weighted coeffecient in the [0][0] position.
        /// </summary>
        /// <param name="matrix"></param>
        /// <returns></returns>
        public static WaveletDecomposition Transform(float[][] matrix)
        {
            if (matrix == null || matrix.Length == 0)
            {
                return null;
            }

            int real_w = matrix.Length;
            int real_h = matrix[0].Length;
            int virtual_w = NPOT(real_w);
            int virtual_h = NPOT(real_h);

            float[][] returnVal = new float[real_w][];
            for (int x = 0; x < real_w; x++)
            {
                returnVal[x] = new float[real_h];

                // Copy the input to output
                for (int y = 0; y < real_h; y++)
                {
                    returnVal[x][y] = matrix[x][y];
                }
            }

            // Recurse over the rows
            int window = virtual_w / 2;
            const float SQRT2 = 1.414213f;
            float[] scratch = new float[real_w];
            while (window > 0)
            {
                for (int y = 0; y < real_h; y++)
                {
                    for (int x = 0; x < real_w; x++)
                    {
                        scratch[x] = returnVal[x][y];
                    }

                    for (int x = 0; x < window; x++)
                    {
                        int left = x * 2;
                        float a = 0;
                        float b = 0;

                        if (left < real_w)
                        {
                            a = returnVal[left][y];
                        }

                        if (left + 1 < real_w)
                        {
                            b = returnVal[left + 1][y];
                        }

                        scratch[x] = (a + b) / SQRT2;

                        if (x + window < real_w)
                        {
                            scratch[x + window] = (a - b) / SQRT2;
                        }
                    }

                    for (int x = 0; x < real_w; x++)
                    {
                        returnVal[x][y] = scratch[x];
                    }
                }

                window = window / 2;
            }

            // Now recurse over the columns
            window = virtual_h / 2;
            scratch = new float[real_h];
            while (window > 0)
            {
                for (int x = 0; x < real_w; x++)
                {
                    for (int y = 0; y < real_h; y++)
                    {
                        scratch[y] = returnVal[x][y];
                    }

                    for (int y = 0; y < window; y++)
                    {
                        int left = y * 2;
                        float a = 0;
                        float b = 0;

                        if (left < real_h)
                        {
                            a = returnVal[x][left];
                        }

                        if (left + 1 < real_h)
                        {
                            b = returnVal[x][left + 1];
                        }

                        scratch[y] = (a + b) / SQRT2;

                        if (y + window < real_h)
                        {
                            scratch[y + window] = (a - b) / SQRT2;
                        }
                    }

                    for (int y = 0; y < real_h; y++)
                    {
                        returnVal[x][y] = scratch[y];
                    }
                }

                window = window / 2;
            }

            // Now normalize the output
            float mass = 0;
            float z;
            for (int x = 0; x < real_w; x++)
            {
                for (int y = 0; y < real_h; y++)
                {
                    // mass += Abs(returnVal[x][y]) optimized
                    z = returnVal[x][y];
                    if (z > 0)
                        mass += z;
                    else
                        mass -= z;
                }
            }

            for (int x = 0; x < real_w; x++)
            {
                for (int y = 0; y < real_h; y++)
                {
                    returnVal[x][y] /= mass;
                }
            }

            return new WaveletDecomposition(returnVal, real_w, real_h);
        }

        private static void PrintCoeffecients(float[][] matrix, int cutoff = 4)
        {
            for (int y = 0; y < cutoff; y++)
            {
                Console.Write("[\t");
                for (int x = 0; x < cutoff; x++)
                {
                    Console.Write(matrix[x][y] + ",\t");
                }
                Console.WriteLine("]");
            }
        }

        /// <summary>
        /// Returns the nearest power-of-two value that is larger than or equal to the given value.
        /// ex: "100" returns "128", "4100" returns "8192", etc.
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        private static int NPOT(int val)
        {
            double logBase = Math.Log((double)val, 2);
            double upperBound = Math.Ceiling(logBase);
            int nearestPowerOfTwo = (int)Math.Pow(2, upperBound);
            return nearestPowerOfTwo;
        }
    }
}
