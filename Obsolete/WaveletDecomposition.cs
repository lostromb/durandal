using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers
{
    public class WaveletDecomposition
    {
        public readonly float[][] Matrix;
        public readonly int ImageWidth;
        public readonly int ImageHeight;
        
        public WaveletDecomposition(float[][] matrix, int width, int height)
        {
            Matrix = matrix;
            ImageWidth = width;
            ImageHeight = height;
        }

        public float CalculateDifference(WaveletDecomposition other, int cutoff = 4)
        {
            float[][] a = Matrix;
            float[][] b = other.Matrix;

            int realCutoff = 
                Math.Min(cutoff,
                Math.Min(ImageWidth,
                Math.Min(ImageHeight,
                Math.Min(other.ImageWidth, other.ImageHeight))));

            float diff = 0;
            for (int y = 0; y < realCutoff; y++)
            {
                for (int x = 0; x < realCutoff; x++)
                {
                    float left = a[x][y];
                    float right = b[x][y];
                    if (left > right)
                        diff += left - right;
                    else
                        diff += right - left;
                }
            }

            return diff;
        }

        public float CalculateVectorDifference(WaveletDecomposition other, int cutoff = 4)
        {
            float[][] a = Matrix;
            float[][] b = other.Matrix;

            int realCutoff =
                Math.Min(cutoff,
                Math.Min(ImageWidth,
                Math.Min(ImageHeight,
                Math.Min(other.ImageWidth, other.ImageHeight))));

            float diff = 0;
            for (int z = 0; z < realCutoff; z++)
            {
                float left = a[z][z];
                float right = b[z][z];
                diff += (left - right) * (left - right);
            }

            return (float)Math.Sqrt(diff);
        }
    }
}
