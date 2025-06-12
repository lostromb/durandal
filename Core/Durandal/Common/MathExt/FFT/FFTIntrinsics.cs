using Durandal.Common.MathExt;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Durandal.Common.MathExt.FFT
{
    public static class FFTIntrinsics
    {
        /// <summary>
        /// Returns true if the spans refer to the same base address
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        internal static bool SpanRefEquals<T>(Span<T> a, Span<T> b)
        {
            if (a.Length == 0)
            {
                return b.Length == 0;
            }
            else if (b.Length == 0)
            {
                return a.Length == 0;
            }

            return Unsafe.AreSame(
                ref Unsafe.AsRef(in a[0]),
                ref Unsafe.AsRef(in b[0]));

            //return a.Slice(0, 1).Overlaps(b.Slice(0, 1));
        }

        internal static void ScaleSpanInPlace(Span<Complex> span, double scale)
        {
            ScaleSpanInPlace(MemoryMarshal.Cast<Complex, double>(span), scale);
        }

        internal static void ScaleSpan(Span<Complex> source, Span<Complex> dest, double scale)
        {
            ScaleSpan(
                MemoryMarshal.Cast<Complex, double>(source),
                MemoryMarshal.Cast<Complex, double>(dest),
                scale);
        }

        internal static void ScaleSpan(Span<double> source, Span<double> dest, double scale)
        {
            int idx = 0;
            int endIdx = source.Length;
#if NET6_0_OR_GREATER
            if (System.Numerics.Vector.IsHardwareAccelerated)
            {
                int vectorEndIdx = endIdx - (source.Length % System.Numerics.Vector<double>.Count);
                while (idx < vectorEndIdx)
                {
                    System.Numerics.Vector.Multiply(
                        new System.Numerics.Vector<double>(source.Slice(idx, System.Numerics.Vector<double>.Count)), scale)
                        .CopyTo(dest.Slice(idx, System.Numerics.Vector<double>.Count));
                    idx += System.Numerics.Vector<double>.Count;
                }
            }
#endif
            for (; idx < endIdx; idx++)
            {
                dest[idx] = source[idx] * scale;
            }
        }

        internal static void ScaleSpanInPlace(Span<double> span, double scale)
        {
            int idx = 0;
            int endIdx = span.Length;
#if NET6_0_OR_GREATER
            if (System.Numerics.Vector.IsHardwareAccelerated)
            {
                int vectorEndIdx = endIdx - (span.Length % System.Numerics.Vector<double>.Count);
                while (idx < vectorEndIdx)
                {
                    Span<double> slice = span.Slice(idx, System.Numerics.Vector<double>.Count);
                    System.Numerics.Vector.Multiply(new System.Numerics.Vector<double>(slice), scale).CopyTo(slice);
                    idx += System.Numerics.Vector<double>.Count;
                }
            }
#endif

            for (; idx < endIdx; idx++)
            {
                span[idx] *= scale;
            }
        }

        internal static void ScaleSpanInPlace(Span<ComplexF> span, float scale)
        {
            ScaleSpanInPlace(MemoryMarshal.Cast<ComplexF, float>(span), scale);
        }

        internal static void ScaleSpan(Span<ComplexF> source, Span<ComplexF> dest, float scale)
        {
            ScaleSpan(
                MemoryMarshal.Cast<ComplexF, float>(source),
                MemoryMarshal.Cast<ComplexF, float>(dest),
                scale);
        }

        internal static void ScaleSpan(Span<float> source, Span<float> dest, float scale)
        {
            int idx = 0;
            int endIdx = source.Length;
#if NET6_0_OR_GREATER
            if (System.Numerics.Vector.IsHardwareAccelerated)
            {
                int vectorEndIdx = endIdx - (source.Length % System.Numerics.Vector<float>.Count);
                while (idx < vectorEndIdx)
                {
                    System.Numerics.Vector.Multiply(
                        new System.Numerics.Vector<float>(source.Slice(idx, System.Numerics.Vector<float>.Count)), scale)
                        .CopyTo(dest.Slice(idx, System.Numerics.Vector<float>.Count));
                    idx += System.Numerics.Vector<float>.Count;
                }
            }
#endif
            for (; idx < endIdx; idx++)
            {
                dest[idx] = source[idx] * scale;
            }
        }

        internal static void ScaleSpanInPlace(Span<float> span, float scale)
        {
            int idx = 0;
            int endIdx = span.Length;
#if NET6_0_OR_GREATER
            if (System.Numerics.Vector.IsHardwareAccelerated)
            {
                int vectorEndIdx = endIdx - (span.Length % System.Numerics.Vector<float>.Count);
                while (idx < vectorEndIdx)
                {
                    Span<float> slice = span.Slice(idx, System.Numerics.Vector<float>.Count);
                    System.Numerics.Vector.Multiply(new System.Numerics.Vector<float>(slice), scale).CopyTo(slice);
                    idx += System.Numerics.Vector<float>.Count;
                }
            }
#endif

            for (; idx < endIdx; idx++)
            {
                span[idx] *= scale;
            }
        }

        public static void CastDoubleToSingle(Span<double> input, Span<float> output)
        {
            if (input.Length == 0)
            {
                return;
            }

            if (output.Length < input.Length)
            {
                throw new IndexOutOfRangeException("Output is not large enough for input");
            }

            int idx = 0;
            int endIdx = input.Length;
#if NET6_0_OR_GREATER
            if (System.Numerics.Vector.IsHardwareAccelerated)
            {
                int vectorEndIdx = endIdx - (input.Length % System.Numerics.Vector<float>.Count);
                while (idx < vectorEndIdx)
                {
                    System.Numerics.Vector.Narrow(
                        new System.Numerics.Vector<double>(input.Slice(idx, System.Numerics.Vector<double>.Count)),
                        new System.Numerics.Vector<double>(input.Slice(idx + System.Numerics.Vector<double>.Count, System.Numerics.Vector<double>.Count)))
                        .CopyTo(output.Slice(idx, System.Numerics.Vector<float>.Count));
                    idx += System.Numerics.Vector<float>.Count;
                }
            }
#endif
            for (; idx < endIdx; idx++)
            {
                output[idx] = (float)input[idx];
            }
        }

        public static void CastSingleToDouble(Span<float> input, Span<double> output)
        {
            if (input.Length == 0)
            {
                return;
            }

            if (output.Length < input.Length)
            {
                throw new IndexOutOfRangeException("Output is not large enough for input");
            }

            int idx = 0;
            int endIdx = input.Length;
#if NET6_0_OR_GREATER
            if (System.Numerics.Vector.IsHardwareAccelerated)
            {
                int vectorEndIdx = endIdx - (input.Length % System.Numerics.Vector<float>.Count);
                while (idx < vectorEndIdx)
                {
                    System.Numerics.Vector<double> low, high;
                    System.Numerics.Vector.Widen(new System.Numerics.Vector<float>(input.Slice(idx, System.Numerics.Vector<float>.Count)), out low, out high);
                    low.CopyTo(output.Slice(idx, System.Numerics.Vector<double>.Count));
                    high.CopyTo(output.Slice(idx + System.Numerics.Vector<double>.Count, System.Numerics.Vector<double>.Count));
                    idx += System.Numerics.Vector<float>.Count;
                }
            }
#endif
            for (; idx < endIdx; idx++)
            {
                output[idx] = (float)input[idx];
            }
        }

        //#define SWAP(a,b,type)
        //    do { type tmp_=(a); (a)=(b); (b)=tmp_; } while(0)
        internal static void Swap<T>(ref T a, ref T b)
        {
            T tmp = a;
            a = b;
            b = tmp;
        }

        internal static double fma(double a, double b, double c)
        {
#if NET8_0_OR_GREATER
            return Math.FusedMultiplyAdd(a, b, c);
#else
            return (a * b) + c;
#endif
        }

        internal static float fma(float a, float b, float c)
        {
#if NET8_0_OR_GREATER
            return (float)Math.FusedMultiplyAdd(a, b, c);
#else
            return (a * b) + c;
#endif
        }

        // adapted from https://stackoverflow.com/questions/42792939/
        // CAUTION: this function only works for arguments in the range [-0.25; 0.25]!
        internal static void my_sincosm1pi(double a, Span<double> res)
        {
            double s = a * a;
            /* Approximate cos(pi*x)-1 for x in [-0.25,0.25] */
            double r = -1.0369917389758117e-4;
            r = fma(r, s, 1.9294935641298806e-3);
            r = fma(r, s, -2.5806887942825395e-2);
            r = fma(r, s, 2.3533063028328211e-1);
            r = fma(r, s, -1.3352627688538006e+0);
            r = fma(r, s, 4.0587121264167623e+0);
            r = fma(r, s, -4.9348022005446790e+0);
            double c = r * s;
            /* Approximate sin(pi*x) for x in [-0.25,0.25] */
            r = 4.6151442520157035e-4;
            r = fma(r, s, -7.3700183130883555e-3);
            r = fma(r, s, 8.2145868949323936e-2);
            r = fma(r, s, -5.9926452893214921e-1);
            r = fma(r, s, 2.5501640398732688e+0);
            r = fma(r, s, -5.1677127800499516e+0);
            s = s * a;
            r = r * s;
            s = fma(a, 3.1415926535897931e+0, r);
            res[0] = c;
            res[1] = s;
        }

        // adapted from https://stackoverflow.com/questions/42792939/
        // CAUTION: this function only works for arguments in the range [-0.25; 0.25]!
        internal static void my_sincosm1pi(float a, Span<float> res)
        {
            float s = a * a;
            /* Approximate cos(pi*x)-1 for x in [-0.25,0.25] */
            float r = -1.0369917389758117e-4f;
            r = fma(r, s, 1.9294935641298806e-3f);
            r = fma(r, s, -2.5806887942825395e-2f);
            r = fma(r, s, 2.3533063028328211e-1f);
            r = fma(r, s, -1.3352627688538006e+0f);
            r = fma(r, s, 4.0587121264167623e+0f);
            r = fma(r, s, -4.9348022005446790e+0f);
            float c = r * s;
            /* Approximate sin(pi*x) for x in [-0.25,0.25] */
            r = 4.6151442520157035e-4f;
            r = fma(r, s, -7.3700183130883555e-3f);
            r = fma(r, s, 8.2145868949323936e-2f);
            r = fma(r, s, -5.9926452893214921e-1f);
            r = fma(r, s, 2.5501640398732688e+0f);
            r = fma(r, s, -5.1677127800499516e+0f);
            s = s * a;
            r = r * s;
            s = fma(a, 3.1415926535897931e+0f, r);
            res[0] = c;
            res[1] = s;
        }

        internal static void calc_first_octant(int den, Span<double> res)
        {
            Span<double> cs = stackalloc double[2];
            int n = (den + 4) >> 3;
            if (n == 0)
            {
                return;
            }

            res[0] = 1.0;
            res[1] = 0.0;
            if (n == 1)
            {
                return;
            }

            int l1 = (int)Math.Sqrt(n);
            for (int i = 1; i < l1; ++i)
            {
                my_sincosm1pi((2.0 * i) / den, res.Slice(2 * i));
            }

            int start = l1;
            while (start < n)
            {
                my_sincosm1pi((2.0 * start) / den, cs);
                res[2 * start] = cs[0] + 1.0;
                res[2 * start + 1] = cs[1];

                int end = l1;
                if (start + end > n)
                {
                    end = n - start;
                }

                for (int i = 1; i < end; ++i)
                {
                    double csx0 = res[2 * i];
                    double csx1 = res[2 * i + 1];
                    res[2 * (start + i)] = ((cs[0] * csx0 - cs[1] * csx1 + cs[0]) + csx0) + 1.0;
                    res[2 * (start + i) + 1] = (cs[0] * csx1 + cs[1] * csx0) + cs[1] + csx1;
                }

                start += l1;
            }

            for (int i = 1; i < l1; ++i)
            {
                res[2 * i] += 1.0;
            }
        }

        internal static void calc_first_octant(int den, Span<float> res)
        {
            Span<float> cs = stackalloc float[2];
            int n = (den + 4) >> 3;
            if (n == 0)
            {
                return;
            }

            res[0] = 1.0f;
            res[1] = 0.0f;
            if (n == 1)
            {
                return;
            }

            int l1 = (int)Math.Sqrt(n);
            for (int i = 1; i < l1; ++i)
            {
                my_sincosm1pi((2.0f * i) / den, res.Slice(2 * i));
            }

            int start = l1;
            while (start < n)
            {
                my_sincosm1pi((2.0f * start) / den, cs);
                res[2 * start] = cs[0] + 1.0f;
                res[2 * start + 1] = cs[1];

                int end = l1;
                if (start + end > n)
                {
                    end = n - start;
                }

                for (int i = 1; i < end; ++i)
                {
                    float csx0 = res[2 * i];
                    float csx1 = res[2 * i + 1];
                    res[2 * (start + i)] = ((cs[0] * csx0 - cs[1] * csx1 + cs[0]) + csx0) + 1.0f;
                    res[2 * (start + i) + 1] = (cs[0] * csx1 + cs[1] * csx0) + cs[1] + csx1;
                }

                start += l1;
            }

            for (int i = 1; i < l1; ++i)
            {
                res[2 * i] += 1.0f;
            }
        }

        internal static void calc_first_quadrant(int n, Span<double> res)
        {
            Span<double> p = res.Slice(n);
            calc_first_octant(n << 1, p);
            int ndone = (n + 2) >> 2;
            int i = 0, idx1 = 0, idx2 = 2 * ndone - 2;
            for (; i + 1 < ndone; i += 2, idx1 += 2, idx2 -= 2)
            {
                res[idx1] = p[2 * i];
                res[idx1 + 1] = p[2 * i + 1];
                res[idx2] = p[2 * i + 3];
                res[idx2 + 1] = p[2 * i + 2];
            }

            if (i != ndone)
            {
                res[idx1] = p[2 * i];
                res[idx1 + 1] = p[2 * i + 1];
            }
        }

        internal static void calc_first_quadrant(int n, Span<float> res)
        {
            Span<float> p = res.Slice(n);
            calc_first_octant(n << 1, p);
            int ndone = (n + 2) >> 2;
            int i = 0, idx1 = 0, idx2 = 2 * ndone - 2;
            for (; i + 1 < ndone; i += 2, idx1 += 2, idx2 -= 2)
            {
                res[idx1] = p[2 * i];
                res[idx1 + 1] = p[2 * i + 1];
                res[idx2] = p[2 * i + 3];
                res[idx2 + 1] = p[2 * i + 2];
            }

            if (i != ndone)
            {
                res[idx1] = p[2 * i];
                res[idx1 + 1] = p[2 * i + 1];
            }
        }

        internal static void calc_first_half(int n, Span<double> res)
        {
            int ndone = (n + 1) >> 1;
            Span<double> p = res.Slice(n - 1);
            calc_first_octant(n << 2, p);
            int i4 = 0, inp = n, i = 0;

            for (; i4 <= inp - i4; ++i, i4 += 4) // octant 0
            {
                res[2 * i] = p[2 * i4]; res[2 * i + 1] = p[2 * i4 + 1];
            }

            for (; i4 - inp <= 0; ++i, i4 += 4) // octant 1
            {
                int xm = inp - i4;
                res[2 * i] = p[2 * xm + 1]; res[2 * i + 1] = p[2 * xm];
            }

            for (; i4 <= 3 * inp - i4; ++i, i4 += 4) // octant 2
            {
                int xm = i4 - inp;
                res[2 * i] = -p[2 * xm + 1]; res[2 * i + 1] = p[2 * xm];
            }

            for (; i < ndone; ++i, i4 += 4) // octant 3
            {
                int xm = 2 * inp - i4;
                res[2 * i] = -p[2 * xm]; res[2 * i + 1] = p[2 * xm + 1];
            }
        }

        internal static void calc_first_half(int n, Span<float> res)
        {
            int ndone = (n + 1) >> 1;
            Span<float> p = res.Slice(n - 1);
            calc_first_octant(n << 2, p);
            int i4 = 0, inp = n, i = 0;

            for (; i4 <= inp - i4; ++i, i4 += 4) // octant 0
            {
                res[2 * i] = p[2 * i4]; res[2 * i + 1] = p[2 * i4 + 1];
            }

            for (; i4 - inp <= 0; ++i, i4 += 4) // octant 1
            {
                int xm = inp - i4;
                res[2 * i] = p[2 * xm + 1]; res[2 * i + 1] = p[2 * xm];
            }

            for (; i4 <= 3 * inp - i4; ++i, i4 += 4) // octant 2
            {
                int xm = i4 - inp;
                res[2 * i] = -p[2 * xm + 1]; res[2 * i + 1] = p[2 * xm];
            }

            for (; i < ndone; ++i, i4 += 4) // octant 3
            {
                int xm = 2 * inp - i4;
                res[2 * i] = -p[2 * xm]; res[2 * i + 1] = p[2 * xm + 1];
            }
        }

        internal static void fill_first_quadrant(int n, Span<double> res)
        {
            const double hsqt2 = 0.707106781186547524400844362104849;
            int quart = n >> 2;

            if ((n & 7) == 0)
            {
                res[quart] = res[quart + 1] = hsqt2;
            }

            for (int i = 2, j = 2 * quart - 2; i < quart; i += 2, j -= 2)
            {
                res[j] = res[i + 1];
                res[j + 1] = res[i];
            }
        }

        internal static void fill_first_quadrant(int n, Span<float> res)
        {
            const float hsqt2 = 0.707106781186547524400844362104849f;
            int quart = n >> 2;

            if ((n & 7) == 0)
            {
                res[quart] = res[quart + 1] = hsqt2;
            }

            for (int i = 2, j = 2 * quart - 2; i < quart; i += 2, j -= 2)
            {
                res[j] = res[i + 1];
                res[j + 1] = res[i];
            }
        }

        internal static void fill_first_half(int n, Span<double> res)
        {
            int half = n >> 1;
            if ((n & 3) == 0)
            {
                for (int i = 0; i < half; i += 2)
                {
                    res[i + half] = -res[i + 1];
                    res[i + half + 1] = res[i];
                }
            }
            else
            {
                for (int i = 2, j = 2 * half - 2; i < half; i += 2, j -= 2)
                {
                    res[j] = -res[i];
                    res[j + 1] = res[i + 1];
                }
            }
        }

        internal static void fill_first_half(int n, Span<float> res)
        {
            int half = n >> 1;
            if ((n & 3) == 0)
            {
                for (int i = 0; i < half; i += 2)
                {
                    res[i + half] = -res[i + 1];
                    res[i + half + 1] = res[i];
                }
            }
            else
            {
                for (int i = 2, j = 2 * half - 2; i < half; i += 2, j -= 2)
                {
                    res[j] = -res[i];
                    res[j + 1] = res[i + 1];
                }
            }
        }

        internal static void fill_second_half(int n, Span<double> res)
        {
            if ((n & 1) == 0)
            {
                for (int i = 0; i < n; ++i)
                {
                    res[i + n] = -res[i];
                }
            }
            else
            {
                for (int i = 2, j = 2 * n - 2; i < n; i += 2, j -= 2)
                {
                    res[j] = res[i];
                    res[j + 1] = -res[i + 1];
                }
            }
        }

        internal static void fill_second_half(int n, Span<float> res)
        {
            if ((n & 1) == 0)
            {
                for (int i = 0; i < n; ++i)
                {
                    res[i + n] = -res[i];
                }
            }
            else
            {
                for (int i = 2, j = 2 * n - 2; i < n; i += 2, j -= 2)
                {
                    res[j] = res[i];
                    res[j + 1] = -res[i + 1];
                }
            }
        }

        internal static void sincos_2pibyn_half(int n, Span<double> res)
        {
            if ((n & 3) == 0)
            {
                calc_first_octant(n, res);
                fill_first_quadrant(n, res);
                fill_first_half(n, res);
            }
            else if ((n & 1) == 0)
            {
                calc_first_quadrant(n, res);
                fill_first_half(n, res);
            }
            else
            {
                calc_first_half(n, res);
            }
        }

        internal static void sincos_2pibyn_half(int n, Span<float> res)
        {
            if ((n & 3) == 0)
            {
                calc_first_octant(n, res);
                fill_first_quadrant(n, res);
                fill_first_half(n, res);
            }
            else if ((n & 1) == 0)
            {
                calc_first_quadrant(n, res);
                fill_first_half(n, res);
            }
            else
            {
                calc_first_half(n, res);
            }
        }

        internal static void sincos_2pibyn(int n, Span<double> res)
        {
            sincos_2pibyn_half(n, res);
            fill_second_half(n, res);
        }

        internal static void sincos_2pibyn(int n, Span<float> res)
        {
            sincos_2pibyn_half(n, res);
            fill_second_half(n, res);
        }

        internal static int largest_prime_factor(int n)
        {
            int res = 1;
            int tmp;
            while (((tmp = (n >> 1)) << 1) == n)
            {
                res = 2;
                n = tmp;
            }

            int limit = (int)Math.Sqrt(n + 0.01);
            for (int x = 3; x <= limit; x += 2)
            {
                while (((tmp = (n / x)) * x) == n)
                {
                    res = x;
                    n = tmp;
                    limit = (int)Math.Sqrt(n + 0.01);
                }
            }

            if (n > 1)
            {
                res = n;
            }

            return res;
        }

        internal static double cost_guess(int n)
        {
            const double lfp = 1.1; // penalty for non-hardcoded larger factors
            int ni = n;
            double result = 0.0;
            int tmp;
            while (((tmp = (n >> 1)) << 1) == n)
            {
                result += 2;
                n = tmp;
            }

            int limit = (int)Math.Sqrt(n + 0.01);
            for (int x = 3; x <= limit; x += 2)
            {
                while ((tmp = (n / x)) * x == n)
                {
                    result += (x <= 5) ? x : lfp * x; // penalize larger prime factors
                    n = tmp;
                    limit = (int)Math.Sqrt(n + 0.01);
                }
            }

            if (n > 1)
            {
                result += (n <= 5) ? n : lfp * n;
            }

            return result * ni;
        }

        /* returns the smallest composite of 2, 3, 5, 7 and 11 which is >= n */
        internal static int good_size(int n)
        {
            if (n <= 6) return n;

            int bestfac = 2 * n;
            for (int f2 = 1; f2 < bestfac; f2 *= 2)
                for (int f23 = f2; f23 < bestfac; f23 *= 3)
                    for (int f235 = f23; f235 < bestfac; f235 *= 5)
                        for (int f2357 = f235; f2357 < bestfac; f2357 *= 7)
                            for (int f235711 = f2357; f235711 < bestfac; f235711 *= 11)
                                if (f235711 >= n) bestfac = f235711;
            return bestfac;
        }

        // #define PMC(a,b,c,d) { a.r=c.r+d.Re; a.i=c.i+d.Im; b.r=c.r-d.Re; b.i=c.i-d.Im; }
        internal static void PMC(ref Complex a, ref Complex b, ref Complex c, ref Complex d)
        {
            a.Re = c.Re + d.Re;
            a.Im = c.Im + d.Im;
            b.Re = c.Re - d.Re;
            b.Im = c.Im - d.Im;
        }

        internal static void PMC(ref ComplexF a, ref ComplexF b, ref ComplexF c, ref ComplexF d)
        {
            a.Re = c.Re + d.Re;
            a.Im = c.Im + d.Im;
            b.Re = c.Re - d.Re;
            b.Im = c.Im - d.Im;
        }

        //#define ADDC(a,b,c) { a.r=b.r+c.Re; a.i=b.i+c.Im; }
        internal static void ADDC(ref Complex a, ref Complex b, ref Complex c)
        {
            a.Re = b.Re + c.Re;
            a.Im = b.Im + c.Im;
        }

        internal static void ADDC(ref ComplexF a, ref ComplexF b, ref ComplexF c)
        {
            a.Re = b.Re + c.Re;
            a.Im = b.Im + c.Im;
        }

        internal static void PASSG1(ref Complex a, ref Complex b, ref Complex c, ref Complex d, ref Complex e, ref Complex f)
        {
            a.Re = b.Re + c.Re * d.Re + e.Re * f.Re;
            a.Im = b.Im + c.Re * d.Im + e.Re * f.Im;
        }

        internal static void PASSG1(ref ComplexF a, ref ComplexF b, ref ComplexF c, ref ComplexF d, ref ComplexF e, ref ComplexF f)
        {
            a.Re = b.Re + c.Re * d.Re + e.Re * f.Re;
            a.Im = b.Im + c.Re * d.Im + e.Re * f.Im;
        }

        internal static void PASSG2(ref Complex a, ref Complex b, ref Complex c, ref Complex d, ref Complex e)
        {
            a.Re = -b.Im * c.Im - d.Im * e.Im;
            a.Im = b.Im * c.Re + d.Im * e.Re;
        }

        internal static void PASSG2(ref ComplexF a, ref ComplexF b, ref ComplexF c, ref ComplexF d, ref ComplexF e)
        {
            a.Re = -b.Im * c.Im - d.Im * e.Im;
            a.Im = b.Im * c.Re + d.Im * e.Re;
        }

        internal static void PASSG3(ref Complex a, ref Complex b, ref Complex c, ref Complex d, ref Complex e)
        {
            a.Re += b.Re * c.Re + d.Re * e.Re;
            a.Im += b.Im * c.Re + d.Im * e.Re;
        }

        internal static void PASSG3(ref ComplexF a, ref ComplexF b, ref ComplexF c, ref ComplexF d, ref ComplexF e)
        {
            a.Re += b.Re * c.Re + d.Re * e.Re;
            a.Im += b.Im * c.Re + d.Im * e.Re;
        }

        internal static void PASSG4(ref Complex a, ref Complex b, ref Complex c, ref Complex d, ref Complex e)
        {
            a.Re -= b.Im * c.Im + d.Im * e.Im;
            a.Im += b.Re * c.Im + d.Re * e.Im;
        }

        internal static void PASSG4(ref ComplexF a, ref ComplexF b, ref ComplexF c, ref ComplexF d, ref ComplexF e)
        {
            a.Re -= b.Im * c.Im + d.Im * e.Im;
            a.Im += b.Re * c.Im + d.Re * e.Im;
        }

        internal static void PASSG5(ref Complex a, ref Complex b, ref Complex c)
        {
            a.Re += b.Re * c.Re;
            a.Im += b.Im * c.Re;
        }

        internal static void PASSG5(ref ComplexF a, ref ComplexF b, ref ComplexF c)
        {
            a.Re += b.Re * c.Re;
            a.Im += b.Im * c.Re;
        }

        internal static void PASSG6(ref Complex a, ref Complex b, ref Complex c)
        {
            a.Re -= b.Im * c.Im;
            a.Im += b.Re * c.Im;
        }

        internal static void PASSG6(ref ComplexF a, ref ComplexF b, ref ComplexF c)
        {
            a.Re -= b.Im * c.Im;
            a.Im += b.Re * c.Im;
        }

        internal static void SUBC(ref Complex a, ref Complex b, ref Complex c)
        {
            a.Re = b.Re - c.Re;
            a.Im = b.Im - c.Im;
        }

        internal static void SUBC(ref ComplexF a, ref ComplexF b, ref ComplexF c)
        {
            a.Re = b.Re - c.Re;
            a.Im = b.Im - c.Im;
        }

        internal static void ADDCSCALED(ref Complex a, ref Complex b, double scale, ref Complex c)
        {
            a.Re = b.Re + scale * c.Re;
            a.Im = b.Im + scale * c.Im;
        }

        internal static void ADDCSCALED(ref ComplexF a, ref ComplexF b, float scale, ref ComplexF c)
        {
            a.Re = b.Re + scale * c.Re;
            a.Im = b.Im + scale * c.Im;
        }

        internal static void CPROJECT(ref Complex a, double scale, ref Complex b)
        {
            a.Re = -(scale * b.Im);
            a.Im = scale * b.Re;
        }

        internal static void CPROJECT(ref ComplexF a, float scale, ref ComplexF b)
        {
            a.Re = -(scale * b.Im);
            a.Im = scale * b.Re;
        }

        //#define ROT90(a) { double tmp_=a.Re; a.r=-a.Im; a.i=tmp_; }
        internal static void ROT90(ref Complex a)
        {
            double tmp = a.Re;
            a.Re = -a.Im;
            a.Im = tmp;
        }

        internal static void ROT90(ref ComplexF a)
        {
            float tmp = a.Re;
            a.Re = -a.Im;
            a.Im = tmp;
        }

        //#define ROTM90(a) { double tmp_=-a.Re; a.r=a.Im; a.i=tmp_; }
        internal static void ROTM90(ref Complex a)
        {
            double tmp = -a.Re;
            a.Re = a.Im;
            a.Im = tmp;
        }

        internal static void ROTM90(ref ComplexF a)
        {
            float tmp = -a.Re;
            a.Re = a.Im;
            a.Im = tmp;
        }

        //#define A_EQ_B_MUL_C(a,b,c) { a.r=b.r*c.r-b.i*c.Im; a.i=b.r*c.i+b.i*c.Re; }
        internal static void A_EQ_B_MUL_C(ref Complex a, ref Complex b, ref Complex c)
        {
            /* a = b*c */
            a.Re = b.Re * c.Re - b.Im * c.Im;
            a.Im = b.Re * c.Im + b.Im * c.Re;
        }

        internal static void A_EQ_B_MUL_C(ref ComplexF a, ref ComplexF b, ref ComplexF c)
        {
            /* a = b*c */
            a.Re = b.Re * c.Re - b.Im * c.Im;
            a.Im = b.Re * c.Im + b.Im * c.Re;
        }

        //#define A_EQ_CB_MUL_C(a,b,c) { a.r=b.r*c.r+b.i*c.Im; a.i=b.r*c.i-b.i*c.Re; }
        internal static void A_EQ_CB_MUL_C(ref Complex a, ref Complex b, ref Complex c)
        {
            /* a = conj(b)*c*/
            a.Re = b.Re * c.Re + b.Im * c.Im;
            a.Im = b.Re * c.Im - b.Im * c.Re;
        }

        internal static void A_EQ_CB_MUL_C(ref ComplexF a, ref ComplexF b, ref ComplexF c)
        {
            /* a = conj(b)*c*/
            a.Re = b.Re * c.Re + b.Im * c.Im;
            a.Im = b.Re * c.Im - b.Im * c.Re;
        }

        //#define PMSIGNC(a,b,c,d) { a.r=c.r+sign*d.Re; a.i=c.i+sign*d.Im; b.r=c.r-sign*d.Re; b.i=c.i-sign*d.Im; }
        internal static void PMSIGNC(ref Complex a, ref Complex b, ref Complex c, ref Complex d, int sign)
        {
            a.Re = c.Re + sign * d.Re;
            a.Im = c.Im + sign * d.Im;
            b.Re = c.Re - sign * d.Re;
            b.Im = c.Im - sign * d.Im;
        }

        internal static void PMSIGNC(ref ComplexF a, ref ComplexF b, ref ComplexF c, ref ComplexF d, int sign)
        {
            a.Re = c.Re + sign * d.Re;
            a.Im = c.Im + sign * d.Im;
            b.Re = c.Re - sign * d.Re;
            b.Im = c.Im - sign * d.Im;
        }

        //#define MULPMSIGNC(a,b,c) { a.r=b.r*c.r-sign*b.i*c.Im; a.i=b.r*c.i+sign*b.i*c.Re; }
        internal static void MULPMSIGNC(ref Complex a, ref Complex b, ref Complex c, int sign)
        {
            /* a = b*c */
            a.Re = b.Re * c.Re - sign * b.Im * c.Im;
            a.Im = b.Re * c.Im + sign * b.Im * c.Re;
        }

        internal static void MULPMSIGNC(ref ComplexF a, ref ComplexF b, ref ComplexF c, int sign)
        {
            /* a = b*c */
            a.Re = b.Re * c.Re - sign * b.Im * c.Im;
            a.Im = b.Re * c.Im + sign * b.Im * c.Re;
        }

        internal static void BLUESTEINSTEP0(ref Complex a, ref Complex b)
        {
            a.Re = b.Re;
            a.Im = -b.Im;
        }

        internal static void BLUESTEINSTEP0(ref ComplexF a, ref ComplexF b)
        {
            a.Re = b.Re;
            a.Im = -b.Im;
        }

        internal static void BLUESTEINSTEP1A(ref Complex a, ref Complex b, ref Complex c)
        {
            a.Re = b.Re * c.Re - b.Im * c.Im;
            a.Im = b.Re * c.Im + b.Im * c.Re;
        }

        internal static void BLUESTEINSTEP1A(ref ComplexF a, ref ComplexF b, ref ComplexF c)
        {
            a.Re = b.Re * c.Re - b.Im * c.Im;
            a.Im = b.Re * c.Im + b.Im * c.Re;
        }

        internal static void BLUESTEINSTEP1B(ref Complex a, ref Complex b, ref Complex c)
        {
            a.Re = b.Re * c.Re + b.Im * c.Im;
            a.Im = -b.Re * c.Im + b.Im * c.Re;
        }

        internal static void BLUESTEINSTEP1B(ref ComplexF a, ref ComplexF b, ref ComplexF c)
        {
            a.Re = b.Re * c.Re + b.Im * c.Im;
            a.Im = -b.Re * c.Im + b.Im * c.Re;
        }

        internal static void BLUESTEINSTEP2A(ref Complex a, ref Complex b)
        {
            double im = -a.Re * b.Im + a.Im * b.Re;
            a.Re = a.Re * b.Re + a.Im * b.Im;
            a.Im = im;
        }

        internal static void BLUESTEINSTEP2A(ref ComplexF a, ref ComplexF b)
        {
            float im = -a.Re * b.Im + a.Im * b.Re;
            a.Re = a.Re * b.Re + a.Im * b.Im;
            a.Im = im;
        }

        internal static void BLUESTEINSTEP2B(ref Complex a, ref Complex b)
        {
            double im = a.Re * b.Im + a.Im * b.Re;
            a.Re = a.Re * b.Re - a.Im * b.Im;
            a.Im = im;
        }

        internal static void BLUESTEINSTEP2B(ref ComplexF a, ref ComplexF b)
        {
            float im = a.Re * b.Im + a.Im * b.Re;
            a.Re = a.Re * b.Re - a.Im * b.Im;
            a.Im = im;
        }

        internal static void BLUESTEINSTEP3A(ref Complex a, ref Complex b, ref Complex c)
        {
            a.Re = b.Re * c.Re - b.Im * c.Im;
            a.Im = b.Im * c.Re + b.Re * c.Im;
        }

        internal static void BLUESTEINSTEP3A(ref ComplexF a, ref ComplexF b, ref ComplexF c)
        {
            a.Re = b.Re * c.Re - b.Im * c.Im;
            a.Im = b.Im * c.Re + b.Re * c.Im;
        }

        internal static void BLUESTEINSTEP3B(ref Complex a, ref Complex b, ref Complex c)
        {
            a.Re = b.Re * c.Re + b.Im * c.Im;
            a.Im = -b.Im * c.Re + b.Re * c.Im;
        }

        internal static void BLUESTEINSTEP3B(ref ComplexF a, ref ComplexF b, ref ComplexF c)
        {
            a.Re = b.Re * c.Re + b.Im * c.Im;
            a.Im = -b.Im * c.Re + b.Re * c.Im;
        }
    }
}
