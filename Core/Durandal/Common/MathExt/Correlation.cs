
namespace Durandal.Common.MathExt
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using System.Text;

    /// <summary>
    /// Static math functions for vector cross-correlation, autocorrelation, etc.
    /// </summary>
    public static class Correlation
    {
        public delegate float CrossCorrelationDelegate(
            float[] x,
            int xIdx,
            float[] y,
            int yIdx,
            int count);

        /// <summary>
        /// Calculates the normalized cross-correlation of two vectors, with the assumption that both vectors are always positive,
        /// or that abs() has been applied to each element. The intuition is mostly the same as with regular correlation,
        /// except that the range of outputs is from 0 to 1 since anticorrelation is not possible.
        /// </summary>
        /// <param name="x">The first vector</param>
        /// <param name="xIdx">Starting index in the first vector</param>
        /// <param name="y">The second vector (can be same as the first for autocorrelation)</param>
        /// <param name="yIdx">Starting index in the second vector</param>
        /// <param name="count">The length of each vector</param>
        /// <returns>The normalized cross-correlation coefficient, from 0 to 1.</returns>
        public static float NormalizedCrossCorrelationOfAbsoluteVector(
            float[] x,
            int xIdx,
            float[] y,
            int yIdx,
            int count)
        {
            // since both vectors are positive, we normalize using inverse square root of 2 because that's the max distance between orthogonal unit vectors
            // This is kind of wishy-washy math but it works well enough
            float start = 1.0f / 1.414213f;
            float end = 1.0f;
            float ncc = NormalizedCrossCorrelation(x, xIdx, y, yIdx, count);
            float returnVal = (ncc - start) / (end - start);

            // clamp to 0 because we can't have an anticorrelation between positive vectors
            return returnVal < 0 ? 0 : returnVal;
        }

        /// <summary>
        /// Calculates the normalized cross-correlation (sometimes called Pearson correlation coefficient) between
        /// two vectors, represented as float array segments. The result will range from -1 (perfect anticorrelation) to 1 (perfect correlation).
        /// </summary>
        /// <param name="x">The first vector</param>
        /// <param name="xIdx">Starting index in the first vector</param>
        /// <param name="y">The second vector (can be same as the first for autocorrelation)</param>
        /// <param name="yIdx">Starting index in the second vector</param>
        /// <param name="count">The length of each vector</param>
        /// <returns>The normalized cross-correlation coefficient, from -1 to 1.</returns>
        public static float NormalizedCrossCorrelation(
            float[] x,
            int xIdx,
            float[] y,
            int yIdx,
            int count)
        {
            float xSquaredMag = 0;
            float ySquaredMag = 0;
            float cross = 0;
            int idx = 0;

#if DEBUG
            if (Vector.IsHardwareAccelerated && FastRandom.Shared.NextBool())
#else
            if (Vector.IsHardwareAccelerated)
#endif
            {
                int stop = count - (count % Vector<float>.Count);
                while (idx < stop)
                {
                    // this is a trivial kernel to vectorize so make it happen
                    Vector<float> dx = new Vector<float>(x, xIdx + idx);
                    Vector<float> dy = new Vector<float>(y, yIdx + idx);
                    cross += Vector.Dot(dx, dy);
                    xSquaredMag += Vector.Dot(dx, dx);
                    ySquaredMag += Vector.Dot(dy, dy);
                    idx += Vector<float>.Count;
                }
            }

            while (idx < count)
            {
                float dx = x[xIdx + idx];
                float dy = y[yIdx + idx];
                cross += dx * dy;
                xSquaredMag += dx * dx;
                ySquaredMag += dy * dy;
                idx++;
            }

            float numerator = cross;
            float denominator = (float)Math.Sqrt(xSquaredMag * ySquaredMag);
            return (denominator == 0) ? 0.0f : numerator / denominator;
        }

        //public static float CrossCorrelation(
        //    ArraySegment<float> x,
        //    int xIdx,
        //    ArraySegment<float> y,
        //    int yIdx,
        //    int count)
        //{
        //    xIdx += x.Offset;
        //    yIdx += y.Offset;
        //    float cross = 0;
        //    for (int c = 0; c < count; c++)
        //    {
        //        float dx = x.Array[xIdx + c];
        //        float dy = y.Array[yIdx + c];
        //        cross += dx * dy;
        //    }

        //    return cross;
        //}

        //public static float NormalizedCrossCorrelation(
        //    ArraySegment<float> x,
        //    int xIdx,
        //    ArraySegment<float> y,
        //    int yIdx,
        //    int count)
        //{
        //    xIdx += x.Offset;
        //    yIdx += y.Offset;
        //    float xSquaredMag = 0;
        //    float ySquaredMag = 0;
        //    float cross = 0;
        //    for (int c = 0; c < count; c++)
        //    {
        //        float dx = x.Array[xIdx + c];
        //        float dy = y.Array[yIdx + c];
        //        cross += dx * dy;
        //        xSquaredMag += dx * dx;
        //        ySquaredMag += dy * dy;
        //    }

        //    float numerator = cross;
        //    float denominator = (float)Math.Sqrt(xSquaredMag * ySquaredMag);
        //    return (denominator == 0) ? 0.0f : numerator / denominator;
        //}

        /// <summary>
        /// Calculates cross correlation of two audio signals (ILBC implementation, not sure what exactly it's theoretically based on)
        /// </summary>
        /// <param name="target">The sample you are attempting to match</param>
        /// <param name="t_idx">The offset when reading from target</param>
        /// <param name="regressor">The field you are attempting to look for the sample in</param>
        /// <param name="r_idx">The offset when reading from regressor</param>
        /// <param name="subl">The number of samples to compare - typically the minimum of both array lengths</param>
        /// <returns>The correlation value, where ~1 indicates the signals are identical</returns>
        public static float XCorrKernel(
                float[] target,
                int t_idx,
                float[] regressor,
                int r_idx,
                int subl)
        {
            int i;
            float ftmp1, ftmp2;

            ftmp1 = 0.0f;
            ftmp2 = 0.0f;
            for (i = 0; i < subl; i++)
            {
                float t = target[t_idx + i];
                float r = regressor[r_idx + i];
                ftmp1 += t * r;
                ftmp2 += r * r;
            }

            if (ftmp1 > 0.0f)
            {
                return ftmp1 * ftmp1 / ftmp2;
            }
            else
            {
                return 0.0f;
            }
        }
    }
}
