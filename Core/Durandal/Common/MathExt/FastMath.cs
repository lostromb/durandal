using Durandal.Common.Utils;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Durandal.Common.MathExt
{
    /// <summary>
    /// LUT-based functions to perform logarithms, exponents, sigmoids, etc. about 3-4 times faster than the system base Math functions
    /// FIXME this class uses like 2 Mb for its cache tables. Am I sure that's really necessary?
    /// </summary>
    public static class FastMath
    {
        private const float _logGranularity = 100000f;
        private const float _logRange = 2f;
        private static float[] _logCache;

        private const float _expGranularity = 100000f;
        private const float _expRange = 2f;
        private static float[] _expCache;

        private const float _sigGranularity = 50000f;
        private const float _sigRange = 2f; // POSITIVE AND NEGATIVE
        private static float[] _sigCache;

        public const float PI = (float)Math.PI;
        private const float PI_SQUARED = (float)(Math.PI * Math.PI);
        private const float TWO_PI = (float)(Math.PI * 2.0f);
        private const float HALF_PI = (float)(Math.PI / 2.0f);

        /// <summary>
        /// Initialize the tables
        /// </summary>
        static FastMath()
        {
            _logCache = new float[(int)(_logGranularity * _logRange)];
            for (int c = 0; c < _logCache.Length; c++)
            {
                float input = c / _logGranularity;
                _logCache[c] = (float)System.Math.Log(input);
            }

            _expCache = new float[(int)(_expGranularity * _expRange)];
            for (int c = 0; c < _expCache.Length; c++)
            {
                float input = c / _expGranularity;
                _expCache[c] = (float)System.Math.Exp(input);
            }

            _sigCache = new float[(int)(_sigGranularity * _sigRange * 2)];
            for (int c = 0; c < _sigCache.Length; c++)
            {
                float input = (c / _sigGranularity) - _sigRange;
                _sigCache[c] = (float)(1 / (1 + System.Math.Exp(0 - input)));
            }
        }
        
        /// <summary>
        /// Calculates the single-precision logarithm of a number. This method is optimized for calculating values between 0 and 2
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Log(float value)
        {
            int index = (int)(value * _logGranularity);
            if (index < 0 || index >= _logCache.Length)
            {
                return (float)System.Math.Log(value);
            }

            return _logCache[index];
        }

        /// <summary>
        /// Calculates the single-precision exp (e^x) of a number. This method is optimized for calculating values between 0 and 2
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Exp(float value)
        {
            int index = (int)(value * _expGranularity);
            if (index < 0 || index >= _expCache.Length)
            {
                return (float)System.Math.Exp(value);
            }

            return _expCache[index];
        }

        /// <summary>
        /// Calculates a sigmoid curve, where domain is (-inf, inf) and range is (0, 1)
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sigmoid(float value)
        {
            int index = (int)((value + _sigRange) * _sigGranularity);
            if (index < 0 || index >= _sigCache.Length)
            {
                return (1 / (1 + Exp(0 - value)));
            }

            return _sigCache[index];
        }

        /// <summary>
        /// Branchless implementation of int32 Max(). There is no downside to using this method.
        /// </summary>
        /// <param name="a">The first integer</param>
        /// <param name="b">The second integer</param>
        /// <returns>The maximum of the two inputs</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Max(int a, int b)
        {
#if NET8_0_OR_GREATER
            return a > b ? a : b;
#else
            // The algorithm here is:
            // 1. Find the sign of the difference between the numbers
            // 2. Turn that sign into a bitfield, either 0 or FFFFFFFF
            // 3. Based on that bitfield, either return A, or (A - Diff) which equals B.
            // Use 64-bit registers to properly handle int.Min/MaxValue and other edge cases.
            // This is only more performant on .Net 7 and below, prior to better runtime CMOV intrinsics
            long diff = (long)a - b;
            long sign = (diff >> 63);
            return (int)(a - (diff & sign));
#endif
        }

        /// <summary>
        /// Branchless implementation of int32 Min(). There is no downside to using this method.
        /// </summary>
        /// <param name="a">The first integer</param>
        /// <param name="b">The second integer</param>
        /// <returns>The minimum of the two inputs</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Min(int a, int b)
        {
#if NET8_0_OR_GREATER
            return a < b ? a : b;
#else
            long diff = (long)a - b;
            long sign = (diff >> 63);
            return (int)(b + (diff & sign));
#endif
        }

        /// <summary>
        /// Returns the value that is the next largest power of two for the given integer.
        /// For example, 100 will return 128, 64 returns 64, 129 returns 256.
        /// If overflow occurs, this returns zero.
        /// </summary>
        /// <param name="value">The value to round up.</param>
        /// <returns>The rounded value.</returns>
        public static uint RoundUpToPowerOf2(uint value)
        {
#if NET6_0_OR_GREATER
            return System.Numerics.BitOperations.RoundUpToPowerOf2(value);
#else
            uint field = 0x1;
            while (field != 0)
            {
                if (field >= value)
                {
                    return field;
                }

                field <<= 1;
            }

            return 0;
#endif
        }

        /// <summary>
        /// Returns the value that is the next largest power of two for the given integer.
        /// For example, 100 will return 128, 64 returns 64, 129 returns 256.
        /// If overflow occurs, this returns zero.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static int RoundUpToPowerOf2(int value)
        {
#if NET6_0_OR_GREATER
            int returnVal = (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)value);
            if (returnVal < 0)
            {
                return 0;
            }

            return returnVal;
#else
            int field = 0x1;
            while (field != int.MinValue) // overflow happened => 0x80000000 => int.MinValue
            {
                if (field >= value)
                {
                    return field;
                }

                field <<= 1;
            }

            return 0;
#endif
        }

        /// <summary>
        /// Converts degrees to radians.
        /// </summary>
        /// <param name="degrees"></param>
        /// <returns></returns>
        public static float DegreesToRads(float degrees)
        {
            return degrees * (PI / 180);
        }

        /// <summary>
        /// Converts radians to degrees.
        /// </summary>
        /// <param name="radians"></param>
        /// <returns></returns>
        public static float RadsToDegrees(float radians)
        {
            return radians * (180 / PI);
        }

        /// <summary>
        /// Fast approximate sine function.
        /// The error from true sine is typically less than 0.001.
        /// </summary>
        /// <param name="input">The input angle, in radians.</param>
        /// <returns>The approximate sine of the angle.</returns>
        public static float Sin(float input)
        {
#if NET7_0_OR_GREATER
            return MathF.Sin(input);
#else
            return Cos(input - HALF_PI);
#endif
        }

        /// <summary>
        /// Fast approximate cosine function.
        /// The error from true cosine is typically less than 0.001.
        /// </summary>
        /// <param name="input">The input angle, in radians.</param>
        /// <returns>The approximate cosine of the angle.</returns>
        public static float Cos(float input)
        {
#if NET7_0_OR_GREATER
            return MathF.Cos(input);
#else
            // Somewhat faster implementation of Cos(radians) using Bhaskara's formula.
            float fraction = input / TWO_PI;
            fraction = fraction - (float)Math.Floor(fraction);
            if (fraction < 0.25f)
            {
                input = fraction * TWO_PI;
                float inputSquared = input * input;
                return (PI_SQUARED - inputSquared - inputSquared - inputSquared - inputSquared) / (PI_SQUARED + inputSquared);
            }
            else if (fraction > 0.75f)
            {
                input = (1.0f - fraction) * TWO_PI;
                float inputSquared = input * input;
                return (PI_SQUARED - inputSquared - inputSquared - inputSquared - inputSquared) / (PI_SQUARED + inputSquared);
            }
            else
            {
                input = (0.5f - fraction) * TWO_PI;
                float inputSquared = input * input;
                return 0 - (PI_SQUARED - inputSquared - inputSquared - inputSquared - inputSquared) / (PI_SQUARED + inputSquared);
            }
#endif
        }
    }
}
