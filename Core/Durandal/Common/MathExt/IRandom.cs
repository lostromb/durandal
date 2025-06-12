using System;

namespace Durandal.Common.MathExt
{
    public interface IRandom
    {
        /// <summary>
        /// Generates a random positive integer between 0 and Int32.MaxValue
        /// </summary>
        /// <returns>A randomly generated integer</returns>
        int NextInt();

        /// <summary>
        /// Generates a random positive integer between 0 and maxValue (exclusive)
        /// </summary>
        /// <param name="maxValue"></param>
        /// <returns>A randomly generated integer within the desired range</returns>
        int NextInt(int maxValue);

        /// <summary>
        /// Generates a random positive integer between minValue (inclusive) and maxValue (exclusive)
        /// </summary>
        /// <param name="minValue"></param>
        /// <param name="maxValue"></param>
        /// <returns>A randomly generated integer within the desired range</returns>
        int NextInt(int minValue, int maxValue);

        /// <summary>
        /// Generates a random positive integer between 0 and Int64.MaxValue
        /// </summary>
        /// <returns>A randomly generated long integer</returns>
        long NextInt64();

        /// <summary>
        /// Generates a random positive integer between 0 and maxValue (exclusive)
        /// </summary>
        /// <returns>A randomly generated long integer within the desired range</returns>
        long NextInt64(long maxValue);

        /// <summary>
        /// Generates a random positive integer between minValue (inclusive) and maxValue (exclusive)
        /// </summary>
        /// <returns>A randomly generated long integer within the desired range</returns>
        long NextInt64(long minValue, long maxValue);

        /// <summary>
        /// Fills the entire target buffer with random bytes
        /// </summary>
        /// <param name="buffer"></param>
        void NextBytes(byte[] buffer);

        /// <summary>
        /// Fills the given range of the target buffer with random bytes
        /// </summary>
        /// <param name="buffer">The buffer to fill.</param>
        /// <param name="offset">The offset to start writing</param>
        /// <param name="count">The number of bytes to generate</param>
        void NextBytes(byte[] buffer, int offset, int count);

        /// <summary>
        /// Fills the given range of the target span with random bytes
        /// </summary>
        /// <param name="buffer">The buffer to fill.</param>
        void NextBytes(Span<byte> buffer);

        /// <summary>
        /// Returns a random 64-bit float value between 0.0 (inclusive) and 1.0 (exclusive)
        /// </summary>
        /// <returns></returns>
        double NextDouble();

        /// <summary>
        /// Returns a random 64-bit float value between min (inclusive) and max (exclusive)
        /// </summary>
        /// <returns>A randomly generated float64 value in the specified range.</returns>
        double NextDouble(double min, double max);

        /// <summary>
        /// Returns a random 32-bit float value between 0.0 (inclusive) and 1.0 (exclusive)
        /// </summary>
        float NextFloat();

        /// <summary>
        /// Returns a random 32-bit float value between min (inclusive) and max (exclusive)
        /// </summary>
        /// <returns>A randomly generated float32 value in the specified range.</returns>
        float NextFloat(float min, float max);

        /// <summary>
        /// Returns a random true or false value.
        /// </summary>
        /// <returns>The result of a random coin flip.</returns>
        bool NextBool();
    }
}