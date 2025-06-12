using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Durandal.Common.Audio
{
    public static class AudioMath
    {
        private const short INT16_MIN_SHORT = 0 - 0x7FFF; // -32767
        private const short INT16_MAX_SHORT = 0x7FFF;     //  32767
        private const int INT24_MIN_INT = 0 - 0x7FFFFF;   // -8388607
        private const int INT24_MAX_INT = 0x7FFFFF;       //  8388607
        private const int INT32_MIN_INT = 0 - 0x7FFFFFFF; // -2147483647
        private const int INT32_MAX_INT = 0x7FFFFFFF;     //  2147483647

        private const float INT16_MIN_FLOAT = INT16_MIN_SHORT;
        private const float INT16_MAX_FLOAT = INT16_MAX_SHORT;
        private const float INT24_MIN_FLOAT = INT24_MIN_INT;
        private const float INT24_MAX_FLOAT = INT24_MAX_INT;
        private const float INT32_MIN_FLOAT = INT32_MIN_INT;
        private const float INT32_MAX_FLOAT = INT32_MAX_INT;

        private readonly static Vector<int> ClampVecInt16Max;
        private readonly static Vector<int> ClampVecInt16Min;

        static AudioMath()
        {
            if (Vector.IsHardwareAccelerated)
            {
                ClampVecInt16Max = new Vector<int>(INT16_MAX_SHORT);
                ClampVecInt16Min = new Vector<int>(INT16_MIN_SHORT);
            }
        }

        /// <summary>
        /// Convenience method for determining how many samples per channel are needed to fill the given time span in this format.
        /// </summary>
        /// <param name="sampleRate">Audio sample rate (per-channel)</param>
        /// <param name="span">The time span to be measured.</param>
        /// <returns>The number of samples per channel that a signal in this format should be to have length == timespan</returns>
        public static long ConvertTimeSpanToSamplesPerChannel(int sampleRate, TimeSpan span)
        {
            return (long)(span.TotalMilliseconds * (double)sampleRate / 1000.0);
        }

        public static TimeSpan ConvertSamplesPerChannelToTimeSpan(int sampleRate, long samplesPerChannel)
        {
            return TimeSpan.FromTicks(samplesPerChannel * TimeSpan.TicksPerSecond / sampleRate);
        }

        public static long ConvertSamplesPerChannelToTicks(int sampleRate, long samplesPerChannel)
        {
            return (samplesPerChannel * TimeSpan.TicksPerSecond) / sampleRate;
        }

        public static long ConvertTicksToSamplesPerChannel(int sampleRate, long ticks)
        {
            return (ticks * sampleRate) / TimeSpan.TicksPerSecond;
        }

        /// <summary>
        /// Converts a volume from decibels of amplification to a linear scalar value.
        /// Input of 0 will return 1
        /// -6 is approximately half volume.
        /// Input of -infinity will return 0.
        /// </summary>
        /// <param name="volumeDecibels">The volume in decibels</param>
        /// <returns>The linear conversion of this volume</returns>
        public static float DecibelsToLinear(float volumeDecibels)
        {
            if (float.IsNegativeInfinity(volumeDecibels))
            {
                return 0.0f;
            }

            if (float.IsNaN(volumeDecibels) || float.IsInfinity(volumeDecibels))
            {
                throw new ArithmeticException("Decibel value is NaN");
            }

            return (float)Math.Pow(10, volumeDecibels / 20.0f);
        }

        /// <summary>
        /// Converts a volume from linear scalar to decibels of amplification.
        /// Input of 0 will return -infinity.
        /// Input of 0.5 will return about -6.
        /// Input of 1 will return 0. And so on for positive inputs too
        /// </summary>
        /// <param name="volumeLinear">The volume as a linear scalar</param>
        /// <returns>The volume in decibels</returns>
        public static float LinearToDecibels(float volumeLinear)
        {
            if (volumeLinear < 0.0f)
            {
                throw new ArgumentOutOfRangeException("Linear volume cannot be less than zero");
            }

            if (volumeLinear == 0)
            {
                return float.NegativeInfinity;
            }

            return (float)(20 * Math.Log10(volumeLinear));
        }

        /// <summary>
        /// Calculates the distance in millimeters that a sound wave travels through air
        /// in the span of a single sample at the given sample rate.
        /// </summary>
        /// <param name="sampleRate">The sample rate</param>
        /// <returns>The distance that a sound wave travels in a single sample</returns>
        public static float SpeedOfSoundMillimetersPerSample(int sampleRate)
        {
            return 343_000f / (float)sampleRate;
        }

        /// <summary>
        /// Converts audio samples from signed int16 PCM format (in the range of +- 32767) to 
        /// single-precision floating point values in the range of (-1.0, 1.0).
        /// </summary>
        /// <param name="input">The input samples to convert</param>
        /// <param name="in_offset">Offset when reading from input samples</param>
        /// <param name="output">The buffer to write the output to. Must have room for the requested number of samples.</param>
        /// <param name="out_offset">Offset when writing to output.</param>
        /// <param name="samples">The number of total samples to convert. Channels are not considered.</param>
        public static void ConvertSamples_Int16ToFloat(short[] input, int in_offset, float[] output, int out_offset, int samples)
        {
#if DEBUG
            if (Vector.IsHardwareAccelerated && FastRandom.Shared.NextBool())
#else
            if (Vector.IsHardwareAccelerated)
#endif
            {
                int idx = 0;
                // pay attention to what type is used for Vector<>.Count because we're dealing with both int16 and float32
                int stop = samples - (samples % Vector<short>.Count);
                while (idx < stop)
                {
                    Vector<int> int32VecLeft;
                    Vector<int> int32VecRight;
                    Vector.Widen(new Vector<short>(input, idx + in_offset), out int32VecLeft, out int32VecRight);
                    Vector.Multiply(1.0f / INT16_MAX_FLOAT, Vector.ConvertToSingle(int32VecLeft)).CopyTo(output, idx + out_offset);
                    Vector.Multiply(1.0f / INT16_MAX_FLOAT, Vector.ConvertToSingle(int32VecRight)).CopyTo(output, idx + out_offset + Vector<float>.Count);
                    idx += Vector<short>.Count;
                }

                while (idx < samples)
                {
                    output[idx + out_offset] = (float)input[idx + in_offset] / INT16_MAX_FLOAT;
                    idx++;
                }
            }
            else
            {
                for (int c = 0; c < samples; c++)
                {
                    output[c + out_offset] = (float)input[c + in_offset] / INT16_MAX_FLOAT;
                }
            }
        }

        /// <summary>
        /// Converts audio samples from 32-bit float to 16-bit int.
        /// </summary>
        /// <param name="input">The input buffer</param>
        /// <param name="in_offset">The absolute offset when reading from input buffer</param>
        /// <param name="output">The output buffer</param>
        /// <param name="out_offset">The absolute offset when writing to output buffer</param>
        /// <param name="samples">The number of TOTAL samples to process (not per-channel)</param>
        /// <param name="clamp">If true, clamp high values to +-32767</param>
        public static void ConvertSamples_FloatToInt16(float[] input, int in_offset, short[] output, int out_offset, int samples, bool clamp = true)
        {
#if DEBUG
            if (Vector.IsHardwareAccelerated && FastRandom.Shared.NextBool())
#else
            if (Vector.IsHardwareAccelerated)
#endif
            {
                int blockSize = Vector<float>.Count * 2;
                if (clamp)
                {
                    int idx = 0;
                    int stop = samples - (samples % blockSize);
                    while (idx < stop)
                    {
                        // we have to do the processing of two vectors at once because at the end,
                        // we narrow the clamped int32 vector into int16 and there's not an easy way to
                        // extract only the first half of the vector
                        Vector.Narrow(
                            Vector.Max(
                                Vector.Min(
                                    Vector.ConvertToInt32(
                                        Vector.Multiply<float>(INT16_MAX_FLOAT, new Vector<float>(input, in_offset + idx))),
                                    ClampVecInt16Max),
                                ClampVecInt16Min),
                            Vector.Max(
                                Vector.Min(
                                    Vector.ConvertToInt32(
                                        Vector.Multiply<float>(INT16_MAX_FLOAT, new Vector<float>(input, in_offset + idx + Vector<float>.Count))),
                                    ClampVecInt16Max),
                                ClampVecInt16Min))
                            .CopyTo(output, idx + out_offset);
                        idx += blockSize;
                    }

                    while (idx < samples)
                    {
                        float num = input[idx + in_offset] * INT16_MAX_FLOAT;
                        if (num > INT16_MAX_FLOAT)
                        {
                            num = INT16_MAX_FLOAT;
                        }
                        else if (num < INT16_MIN_FLOAT)
                        {
                            num = INT16_MIN_FLOAT;
                        }

                        output[idx + out_offset] = (short)num;
                        idx++;
                    }
                }
                else
                {
                    int idx = 0;
                    int stop = samples - (samples % blockSize);
                    while (idx < stop)
                    {
                        Vector.Narrow(
                            Vector.ConvertToInt32(
                                Vector.Multiply<float>(INT16_MAX_FLOAT, new Vector<float>(input, in_offset + idx))),
                            Vector.ConvertToInt32(
                                Vector.Multiply<float>(INT16_MAX_FLOAT, new Vector<float>(input, in_offset + idx + Vector<float>.Count))))
                            .CopyTo(output, idx + out_offset);
                        idx += blockSize;
                    }

                    while (idx < samples)
                    {
                        output[idx + out_offset] = (short)(input[idx + in_offset] * INT16_MAX_FLOAT);
                        idx++;
                    }
                }
            }
            else
            {
                if (clamp)
                {
                    for (int c = 0; c < samples; c++)
                    {
                        float sample = input[c + in_offset] * INT16_MAX_FLOAT;
                        if (float.IsNaN(sample) || float.IsInfinity(sample))
                        {
                            output[c + out_offset] = INT16_MIN_SHORT; // 0 would make more sense but this is to keep parity with the vectorized behavior
                        }
                        else if (sample >= INT16_MAX_FLOAT)
                        {
                            output[c + out_offset] = INT16_MAX_SHORT;
                        }
                        else if (sample <= INT16_MIN_FLOAT)
                        {
                            output[c + out_offset] = INT16_MIN_SHORT;
                        }
                        else
                        {
                            output[c + out_offset] = (short)sample;
                        }
                    }
                }
                else
                {
                    for (int c = 0; c < samples; c++)
                    {
                        output[c + out_offset] = (short)(input[c + in_offset] * INT16_MAX_FLOAT);
                    }
                }
            }
        }

        /// <summary>
        /// Converts audio samples from 32-bit float to 16-bit int.
        /// This variant of the function uses spans, which support vector acceleration only
        /// on the .Net Core build of the code.
        /// </summary>
        /// <param name="input">The input buffer as a span</param>
        /// <param name="output">The output buffer as a span</param>
        /// <param name="samples">The number of TOTAL samples to process (not per-channel)</param>
        /// <param name="clamp">If true, clamp high values to +-32767</param>
        public static void ConvertSamples_FloatToInt16(ReadOnlySpan<float> input, Span<short> output, int samples, bool clamp = true)
        {
#if NET6_0_OR_GREATER
#if DEBUG
            if (Vector.IsHardwareAccelerated && FastRandom.Shared.NextBool())
#else
            if (Vector.IsHardwareAccelerated)
#endif
            {
                int blockSize = Vector<float>.Count * 2;
                if (clamp)
                {
                    int idx = 0;
                    int stop = samples - (samples % blockSize);
                    while (idx < stop)
                    {
                        // we have to do the processing of two vectors at once because at the end,
                        // we narrow the clamped int32 vector into int16 and there's not an easy way to
                        // extract only the first half of the vector
                        Vector.Narrow(
                            Vector.Max(
                                Vector.Min(
                                    Vector.ConvertToInt32(
                                        Vector.Multiply<float>(INT16_MAX_FLOAT, new Vector<float>(input.Slice(idx)))),
                                    ClampVecInt16Max),
                                ClampVecInt16Min),
                            Vector.Max(
                                Vector.Min(
                                    Vector.ConvertToInt32(
                                        Vector.Multiply<float>(INT16_MAX_FLOAT, new Vector<float>(input.Slice(idx + Vector<float>.Count)))),
                                    ClampVecInt16Max),
                                ClampVecInt16Min))
                            .CopyTo(output.Slice(idx));
                        idx += blockSize;
                    }

                    while (idx < samples)
                    {
                        float num = input[idx] * INT16_MAX_FLOAT;
                        if (num > INT16_MAX_FLOAT)
                        {
                            num = INT16_MAX_FLOAT;
                        }
                        else if (num < INT16_MIN_FLOAT)
                        {
                            num = INT16_MIN_FLOAT;
                        }

                        output[idx] = (short)num;
                        idx++;
                    }
                }
                else
                {
                    int idx = 0;
                    int stop = samples - (samples % blockSize);
                    while (idx < stop)
                    {
                        Vector.Narrow(
                            Vector.ConvertToInt32(
                                Vector.Multiply<float>(INT16_MAX_FLOAT, new Vector<float>(input.Slice(idx)))),
                            Vector.ConvertToInt32(
                                Vector.Multiply<float>(INT16_MAX_FLOAT, new Vector<float>(input.Slice(idx + Vector<float>.Count)))))
                            .CopyTo(output.Slice(idx));
                        idx += blockSize;
                    }

                    while (idx < samples)
                    {
                        output[idx] = (short)(input[idx] * INT16_MAX_FLOAT);
                        idx++;
                    }
                }
            }
            else
#endif // NET6_0_OR_GREATER
            {
                if (clamp)
                {
                    for (int c = 0; c < samples; c++)
                    {
                        float sample = input[c] * INT16_MAX_FLOAT;
                        if (float.IsNaN(sample) || float.IsInfinity(sample))
                        {
                            output[c] = INT16_MIN_SHORT; // 0 would make more sense but this is to keep parity with the vectorized behavior
                        }
                        else if (sample >= INT16_MAX_FLOAT)
                        {
                            output[c] = INT16_MAX_SHORT;
                        }
                        else if (sample <= INT16_MIN_FLOAT)
                        {
                            output[c] = INT16_MIN_SHORT;
                        }
                        else
                        {
                            output[c] = (short)sample;
                        }
                    }
                }
                else
                {
                    for (int c = 0; c < samples; c++)
                    {
                        output[c] = (short)(input[c] * INT16_MAX_FLOAT);
                    }
                }
            }
        }

        public static void ConvertSamples_Int32_16bit_ToFloat(int[] input, int in_offset, float[] output, int out_offset, int samples)
        {
            for (int c = 0; c < samples; c++)
            {
                output[c + out_offset] = (float)input[c + in_offset] / INT16_MAX_FLOAT;
            }
        }

        /// <summary>
        /// Converts audio samples from 32-bit float to 16-bit int stored within an int32 container
        /// </summary>
        /// <param name="input">The input buffer</param>
        /// <param name="in_offset">The absolute offset when reading from input buffer</param>
        /// <param name="output">The output buffer</param>
        /// <param name="out_offset">The absolute offset when writing to output buffer</param>
        /// <param name="samples">The number of TOTAL samples to process (not per-channel)</param>
        /// <param name="clamp">If true, clamp high values to +-32767</param>
        public static void ConvertSamples_FloatToInt32_16bit(float[] input, int in_offset, int[] output, int out_offset, int samples, bool clamp = true)
        {
            if (clamp)
            {
                for (int c = 0; c < samples; c++)
                {
                    float sample = input[c + in_offset] * INT16_MAX_FLOAT;
                    if (float.IsNaN(sample) || float.IsInfinity(sample))
                    {
                        output[c + out_offset] = 0;
                    }
                    else if (sample >= INT16_MAX_FLOAT)
                    {
                        output[c + out_offset] = INT16_MAX_SHORT;
                    }
                    else if (sample <= INT16_MIN_FLOAT)
                    {
                        output[c + out_offset] = INT16_MIN_SHORT;
                    }
                    else
                    {
                        output[c + out_offset] = (int)sample;
                    }
                }
            }
            else
            {
                for (int c = 0; c < samples; c++)
                {
                    output[c + out_offset] = (int)(input[c + in_offset] * INT16_MAX_FLOAT);
                }
            }
        }

        public static void ConvertSamples_Int32_24bit_ToFloat(int[] input, int in_offset, float[] output, int out_offset, int samples)
        {
            for (int c = 0; c < samples; c++)
            {
                output[c + out_offset] = (float)input[c + in_offset] / INT24_MAX_FLOAT;
            }
        }

        /// <summary>
        /// Converts audio samples from 32-bit float to 24-bit int stored within an int32 container
        /// </summary>
        /// <param name="input">The input buffer</param>
        /// <param name="in_offset">The absolute offset when reading from input buffer</param>
        /// <param name="output">The output buffer</param>
        /// <param name="out_offset">The absolute offset when writing to output buffer</param>
        /// <param name="samples">The number of TOTAL samples to process (not per-channel)</param>
        /// <param name="clamp">If true, clamp high values to +-3288607</param>
        public static void ConvertSamples_FloatToInt32_24bit(float[] input, int in_offset, int[] output, int out_offset, int samples, bool clamp = true)
        {
            if (clamp)
            {
                for (int c = 0; c < samples; c++)
                {
                    int sample = (int)(input[c + in_offset] * INT24_MAX_FLOAT);
                    if (float.IsNaN(sample) || float.IsInfinity(sample))
                    {
                        output[c + out_offset] = 0;
                    }
                    else if (sample > INT24_MAX_INT)
                    {
                        output[c + out_offset] = INT24_MAX_INT;
                    }
                    else if (sample < INT24_MIN_INT)
                    {
                        output[c + out_offset] = INT24_MIN_INT;
                    }
                    else
                    {
                        output[c + out_offset] = (int)sample;
                    }
                }
            }
            else
            {
                for (int c = 0; c < samples; c++)
                {
                    output[c + out_offset] = (int)(input[c + in_offset] * INT24_MAX_FLOAT);
                }
            }
        }

        /// <summary>
        /// Converts a byte array containing little-endian float32 samples and converts them into platform-native floats.
        /// </summary>
        /// <param name="input">The input buffer</param>
        /// <param name="in_offset">The absolute offset to read from input</param>
        /// <param name="output">The output buffer</param>
        /// <param name="out_offset">The absolute offset to write to output</param>
        /// <param name="samples">the number of TOTAL samples to convert - not samples per channel</param>
        public static void ConvertSamples_4BytesFloatLittleEndianToFloat(byte[] input, int in_offset, float[] output, int out_offset, int samples)
        {
            if (BitConverter.IsLittleEndian)
            {
                ReadOnlySpan<byte> rawBytes = input.AsSpan(in_offset, samples * sizeof(float));
                ReadOnlySpan<float> castFloats = MemoryMarshal.Cast<byte, float>(rawBytes);
                castFloats.CopyTo(output.AsSpan(out_offset, samples));
            }
            else
            {
                Span<uint> swapSpaceUint = stackalloc uint[1];
                Span<float> swapSpaceFloat = MemoryMarshal.Cast<uint, float>(swapSpaceUint);
                ReadOnlySpan<byte> inputBytes = input.AsSpan(in_offset, samples * sizeof(float));
                ReadOnlySpan<uint> inputUints = MemoryMarshal.Cast<byte, uint>(inputBytes);
                for (int sample = 0; sample < samples; sample++)
                {
                    swapSpaceUint[0] = BinaryHelpers.ReverseEndianness(inputUints[sample]);
                    output[out_offset + sample] = swapSpaceFloat[0];
                }
            }
        }

        public static void ConvertSamples_FloatTo4BytesFloatLittleEndian(float[] input, int in_offset, byte[] output, int out_offset, int samples)
        {
            if (BitConverter.IsLittleEndian)
            {
                ReadOnlySpan<float> rawFloats = input.AsSpan(in_offset, samples);
                ReadOnlySpan<byte> castFloats = MemoryMarshal.Cast<float, byte>(rawFloats);
                castFloats.CopyTo(output.AsSpan(out_offset, samples * sizeof(float)));
            }
            else
            {
                Span<uint> inputUints = MemoryMarshal.Cast<float, uint>(input.AsSpan(in_offset, samples));
                Span<byte> outputBytes = output.AsSpan(out_offset, samples * sizeof(float));
                Span<uint> outputUints = MemoryMarshal.Cast<byte, uint>(outputBytes);
                for (int sample = 0; sample < samples; sample++)
                {
                    outputUints[sample] = BinaryHelpers.ReverseEndianness(inputUints[sample]);
                }
            }
        }

        public static void ConvertSamples_2BytesIntLittleEndianToFloat(byte[] input, int in_offset, float[] output, int out_offset, int samples)
        {
            if (BitConverter.IsLittleEndian)
            {
                int end = out_offset + samples;
#if DEBUG
                if (Vector.IsHardwareAccelerated && FastRandom.Shared.NextBool())
#else
                if (Vector.IsHardwareAccelerated)
#endif
                {
                    int vectorEnd = end - (samples % (Vector<float>.Count * 2));
                    Vector<int> wideLeft;
                    Vector<int> wideRight;
                    while (out_offset < vectorEnd)
                    {
                        Vector.Widen(
                            Vector.AsVectorInt16(new Vector<byte>(input, in_offset)),
                            out wideLeft,
                            out wideRight);
                        Vector.Multiply(1.0f / INT16_MAX_FLOAT, Vector.ConvertToSingle(wideLeft)).CopyTo(output, out_offset);
                        out_offset += Vector<float>.Count;
                        Vector.Multiply(1.0f / INT16_MAX_FLOAT, Vector.ConvertToSingle(wideRight)).CopyTo(output, out_offset);
                        out_offset += Vector<float>.Count;
                        in_offset += Vector<byte>.Count;
                    }
                }

                // use machine-native byte ordering for speed
                Span<byte> bytePointer = input.AsSpan(in_offset);
                Span<short> castPointer = MemoryMarshal.Cast<byte, short>(bytePointer);
                Span<float> outputPointer = output.AsSpan();
                int castPtrOffset = 0;
                while (out_offset < end)
                {
                    outputPointer[out_offset++] = castPointer[castPtrOffset++] / INT16_MAX_FLOAT;
                }
            }
            else
            {
                short scratch;
                int inIdx = in_offset;
                int outIdx = out_offset;
                for (int c = 0; c < samples; c++)
                {
                    scratch = (short)(((int)input[inIdx++]) << 0);
                    scratch += (short)(((int)input[inIdx++]) << 8);
                    output[outIdx++] = (float)scratch / INT16_MAX_FLOAT;
                }
            }
        }

        public static void ConvertSamples_FloatTo2BytesIntLittleEndian(float[] input, int in_offset, byte[] output, int out_offset, int samples, bool clamp = true)
        {
            float scratch;
            int inIdx = in_offset;
            int outIdx = out_offset;
            int end = inIdx + samples;
            if (clamp)
            {
                if (BitConverter.IsLittleEndian)
                {
#if DEBUG
                    if (Vector.IsHardwareAccelerated && FastRandom.Shared.NextBool())
#else
                    if (Vector.IsHardwareAccelerated)
#endif
                    {
                        // clamped vector loop
                        int vecEnd = end - (samples % (Vector<float>.Count * 2));
                        while (inIdx < vecEnd)
                        {
                            Vector.AsVectorByte(Vector.Narrow(
                                Vector.Min(ClampVecInt16Max,
                                    Vector.Max(ClampVecInt16Min,
                                        Vector.ConvertToInt32(
                                            Vector.Multiply(INT16_MAX_FLOAT,
                                                new Vector<float>(input, inIdx))))),
                                Vector.Min(ClampVecInt16Max,
                                    Vector.Max(ClampVecInt16Min,
                                        Vector.ConvertToInt32(
                                            Vector.Multiply(INT16_MAX_FLOAT,
                                                new Vector<float>(input, inIdx + Vector<float>.Count)))))))
                                .CopyTo(output, outIdx);
                            outIdx += Vector<byte>.Count;
                            inIdx += Vector<float>.Count * 2;
                        }
                    }
                    else
                    {
                        // If we don't have vectors, we can at least use native machine word copies
                        // clamped "fake vector" loop
                        const int FAKE_VEC_LENGTH = 32;
                        Span<short> int16Vector = stackalloc short[FAKE_VEC_LENGTH];
                        Span<byte> byteVector = MemoryMarshal.Cast<short, byte>(int16Vector);
                        int vecEnd = end - (samples % FAKE_VEC_LENGTH);
                        while (inIdx < vecEnd)
                        {
                            for (int c = 0; c < FAKE_VEC_LENGTH; c++)
                            {
                                scratch = input[inIdx++] * INT16_MAX_FLOAT;
                                short sample;
                                if (float.IsNaN(scratch) || float.IsInfinity(scratch))
                                {
                                    sample = 0;
                                }
                                else if (scratch > INT16_MAX_FLOAT)
                                {
                                    sample = INT16_MAX_SHORT;
                                }
                                else if (scratch < INT16_MIN_FLOAT)
                                {
                                    sample = INT16_MIN_SHORT;
                                }
                                else
                                {
                                    sample = (short)scratch;
                                }

                                int16Vector[c] = sample;
                            }

                            byteVector.CopyTo(output.AsSpan(outIdx));
                            outIdx += byteVector.Length;
                            // don't need to increment inIdx here because we did a ++ at its last reference
                        }
                    }
                }

                // clamped residual loop
                while (inIdx < end)
                {
                    scratch = input[inIdx++] * INT16_MAX_FLOAT;
                    short sample;
                    if (float.IsNaN(scratch) || float.IsInfinity(scratch))
                    {
                        sample = 0;
                    }
                    else if (scratch > INT16_MAX_FLOAT)
                    {
                        sample = INT16_MAX_SHORT;
                    }
                    else if (scratch < INT16_MIN_FLOAT)
                    {
                        sample = INT16_MIN_SHORT;
                    }
                    else
                    {
                        sample = (short)scratch;
                    }

                    output[outIdx++] = (byte)((sample >> 0) & 0xFF);
                    output[outIdx++] = (byte)((sample >> 8) & 0xFF);
                }
            }
            else // if clamp = false
            {
                if (BitConverter.IsLittleEndian)
                {
#if DEBUG
                    if (Vector.IsHardwareAccelerated && FastRandom.Shared.NextBool())
#else
                    if (Vector.IsHardwareAccelerated)
#endif
                    {
                        // unclamped vector loop
                        int vecEnd = end - (samples % (Vector<float>.Count * 2));
                        while (inIdx < vecEnd)
                        {
                            Vector.AsVectorByte(Vector.Narrow(
                                Vector.ConvertToInt32(
                                    Vector.Multiply(INT16_MAX_FLOAT,
                                        new Vector<float>(input, inIdx))),
                                Vector.ConvertToInt32(
                                    Vector.Multiply(INT16_MAX_FLOAT,
                                        new Vector<float>(input, inIdx + Vector<float>.Count)))))
                                .CopyTo(output, outIdx);
                            outIdx += Vector<byte>.Count;
                            inIdx += Vector<float>.Count * 2;
                        }
                    }
                    else
                    {
                        // If we don't have vectors, we can at least use native machine word copies
                        // unclamped "fake vector" loop
                        const int FAKE_VEC_LENGTH = 32;
                        Span<short> int16Vector = stackalloc short[FAKE_VEC_LENGTH];
                        Span<byte> byteVector = MemoryMarshal.Cast<short, byte>(int16Vector);
                        int vecEnd = end - (samples % FAKE_VEC_LENGTH);
                        while (inIdx < vecEnd)
                        {
                            for (int c = 0; c < FAKE_VEC_LENGTH; c++)
                            {
                                int16Vector[c] = (short)(input[inIdx++] * INT16_MAX_FLOAT);
                            }

                            byteVector.CopyTo(output.AsSpan(outIdx));
                            outIdx += byteVector.Length;
                        }
                    }
                }

                // unclamped residual loop after vectors have finished, or if we are big endian
                while (inIdx < end)
                {
                    short sample = (short)(input[inIdx++] * INT16_MAX_FLOAT);
                    output[outIdx++] = (byte)((sample >> 0) & 0xFF);
                    output[outIdx++] = (byte)((sample >> 8) & 0xFF);
                }
            }
        }

        public static void ConvertSamples_FloatTo2BytesIntBigEndian(float[] input, int in_offset, byte[] output, int out_offset, int samples, bool clamp = true)
        {
            float scratch;
            int inIdx = in_offset;
            int outIdx = out_offset;
            if (clamp)
            {
                for (int c = 0; c < samples; c++)
                {
                    scratch = input[inIdx++] * INT16_MAX_FLOAT;
                    short sample;
                    if (float.IsNaN(scratch) || float.IsInfinity(scratch))
                    {
                        sample = 0;
                    }
                    else if (scratch > INT16_MAX_FLOAT)
                    {
                        sample = INT16_MAX_SHORT;
                    }
                    else if (scratch < INT16_MIN_FLOAT)
                    {
                        sample = INT16_MIN_SHORT;
                    }
                    else
                    {
                        sample = (short)scratch;
                    }

                    output[outIdx++] = (byte)((sample >> 8) & 0xFF);
                    output[outIdx++] = (byte)((sample >> 0) & 0xFF);
                }
            }
            else
            {
                for (int c = 0; c < samples; c++)
                {
                    short sample = (short)(input[inIdx++] * INT16_MAX_FLOAT);
                    output[outIdx++] = (byte)((sample >> 8) & 0xFF);
                    output[outIdx++] = (byte)((sample >> 0) & 0xFF);
                }
            }
        }

        public static void ConvertSamples_2BytesIntBigEndianToFloat(byte[] input, int in_offset, float[] output, int out_offset, int samples)
        {
            short scratch;
            int inIdx = in_offset;
            int outIdx = out_offset;
            for (int c = 0; c < samples; c++)
            {
                scratch = (short)(((int)input[inIdx++]) << 8);
                scratch += (short)(((int)input[inIdx++]) << 0);
                output[outIdx++] = (float)scratch / INT16_MAX_FLOAT;
            }
        }

        public static void ConvertSamples_3BytesIntLittleEndianToFloat(byte[] input, int in_offset, float[] output, int out_offset, int samples)
        {
            int scratch;
            int inIdx = in_offset;
            int outIdx = out_offset;
            for (int c = 0; c < samples; c++)
            {
                // Looks strange, I know
                // The reason we shift an extra <<8 is because the sign bit
                // for the int24 value is stored in bit 23 but we need it in bit 31,
                // otherwise the sample value gets interpreted as a uint.
                // So shift the whole value into bits 31-8, interpret that as a signed
                // 32-bit value, then scale it as though it were a regular 32-bit integer sample
                scratch = input[inIdx++] << 8;
                scratch += input[inIdx++] << 16;
                scratch += input[inIdx++] << 24;
                output[outIdx++] = (float)scratch / INT32_MAX_FLOAT;
            }
        }

        public static void ConvertSamples_FloatTo3BytesIntLittleEndian(float[] input, int in_offset, byte[] output, int out_offset, int samples, bool clamp = true)
        {
            float scratch;
            int inIdx = in_offset;
            int outIdx = out_offset;
            if (clamp)
            {
                for (int c = 0; c < samples; c++)
                {
                    scratch = input[inIdx++] * INT24_MAX_FLOAT;
                    int sample;
                    if (float.IsNaN(scratch) || float.IsInfinity(scratch))
                    {
                        sample = 0;
                    }
                    else if (scratch > INT24_MAX_FLOAT)
                    {
                        sample = INT24_MAX_INT;
                    }
                    else if (scratch < INT24_MIN_FLOAT)
                    {
                        sample = INT24_MIN_INT;
                    }
                    else
                    {
                        sample = (int)scratch;
                    }

                    output[outIdx++] = (byte)((sample >> 0) & 0xFF);
                    output[outIdx++] = (byte)((sample >> 8) & 0xFF);
                    output[outIdx++] = (byte)((sample >> 16) & 0xFF);
                }
            }
            else
            {
                for (int c = 0; c < samples; c++)
                {
                    int sample = (int)(input[inIdx++] * INT24_MAX_FLOAT);
                    output[outIdx++] = (byte)((sample >> 0) & 0xFF);
                    output[outIdx++] = (byte)((sample >> 8) & 0xFF);
                    output[outIdx++] = (byte)((sample >> 16) & 0xFF);
                }
            }
        }


        public static void ConvertSamples_4BytesIntLittleEndianToFloat(byte[] input, int in_offset, float[] output, int out_offset, int samples)
        {
            if (BitConverter.IsLittleEndian)
            {
                // use machine-native byte ordering for speed
                Span<byte> bytePointer = input.AsSpan(in_offset);
                Span<int> castPointer = MemoryMarshal.Cast<byte, int>(bytePointer);
                Span<float> outputPointer = output.AsSpan(out_offset);
                for (int c = 0; c < samples; c++)
                {
                    outputPointer[c] = castPointer[c] / INT32_MAX_FLOAT;
                }
            }
            else
            {
                int scratch;
                int inIdx = in_offset;
                int outIdx = out_offset;
                for (int c = 0; c < samples; c++)
                {
                    scratch = input[inIdx++] << 0;
                    scratch |= input[inIdx++] << 8;
                    scratch |= input[inIdx++] << 16;
                    scratch |= input[inIdx++] << 24;
                    output[outIdx++] = scratch / INT32_MAX_FLOAT;
                }
            }
        }

        public static void ConvertSamples_FloatTo4BytesIntLittleEndian(float[] input, int in_offset, byte[] output, int out_offset, int samples, bool clamp = true)
        {
            float scratch;
            int inIdx = in_offset;
            int outIdx = out_offset;
            if (clamp)
            {
                for (int c = 0; c < samples; c++)
                {
                    scratch = input[inIdx++] * INT32_MAX_FLOAT;
                    int sample;
                    if (float.IsNaN(scratch) || float.IsInfinity(scratch))
                    {
                        sample = 0;
                    }
                    else if (scratch > INT32_MAX_FLOAT)
                    {
                        sample = INT32_MAX_INT;
                    }
                    else if (scratch < INT32_MIN_FLOAT)
                    {
                        sample = INT32_MIN_INT;
                    }
                    else
                    {
                        sample = (int)scratch;
                    }

                    output[outIdx++] = (byte)((sample >> 0) & 0xFF);
                    output[outIdx++] = (byte)((sample >> 8) & 0xFF);
                    output[outIdx++] = (byte)((sample >> 16) & 0xFF);
                    output[outIdx++] = (byte)((sample >> 24) & 0xFF);
                }
            }
            else
            {
                for (int c = 0; c < samples; c++)
                {
                    int sample = (int)(input[inIdx++] * INT32_MAX_FLOAT);
                    output[outIdx++] = (byte)((sample >> 0) & 0xFF);
                    output[outIdx++] = (byte)((sample >> 8) & 0xFF);
                    output[outIdx++] = (byte)((sample >> 16) & 0xFF);
                    output[outIdx++] = (byte)((sample >> 24) & 0xFF);
                }
            }
        }

        /// <summary>
        /// Multiplies a buffer of audio samples in-place by a specified scalar.
        /// </summary>
        /// <param name="input">The buffer to modify</param>
        /// <param name="inOffset">The start offset</param>
        /// <param name="samples">The number of TOTAL SAMPLES to augment. Channels are not applicable here</param>
        /// <param name="scale">The scalar value</param>
        public static void ScaleSamples(float[] input, int inOffset, int samples, float scale)
        {
            int totalEndOffset = inOffset + samples;
#if DEBUG
            if (Vector.IsHardwareAccelerated && FastRandom.Shared.NextBool())
#else
            if (Vector.IsHardwareAccelerated)
#endif
            {
                int vectorEndOffset = inOffset + samples - (samples % Vector<float>.Count);
                while (inOffset < vectorEndOffset)
                {
                    Vector.Multiply<float>(new Vector<float>(input, inOffset), scale).CopyTo(input, inOffset);
                    inOffset += Vector<float>.Count;
                }
            }

            while (inOffset < totalEndOffset)
            {
                input[inOffset] = input[inOffset] * scale;
                inOffset++;
            }
        }

        /// <summary>
        /// Multiplies a buffer of audio samples by a specified scalar and copies them to an output.
        /// </summary>
        /// <param name="input">The input buffer</param>
        /// <param name="inOffset">The input start offset</param>
        /// <param name="output">The output buffer</param>
        /// <param name="outOffset">The output start offset</param>
        /// <param name="samples">The number of TOTAL SAMPLES to augment. Channels are not applicable here</param>
        /// <param name="scale">The scalar value</param>
        public static void ScaleAndMoveSamples(float[] input, int inOffset, float[] output, int outOffset, int samples, float scale)
        {
            int totalEndOffset = inOffset + samples;
#if DEBUG
            if (Vector.IsHardwareAccelerated && FastRandom.Shared.NextBool())
#else
            if (Vector.IsHardwareAccelerated)
#endif
            {
                int vectorEndOffset = inOffset + samples - (samples % Vector<float>.Count);
                while (inOffset < vectorEndOffset)
                {
                    Vector.Multiply<float>(new Vector<float>(input, inOffset), scale).CopyTo(output, outOffset);
                    inOffset += Vector<float>.Count;
                    outOffset += Vector<float>.Count;
                }
            }

            while (inOffset < totalEndOffset)
            {
                output[outOffset] = input[inOffset] * scale;
                inOffset++;
                outOffset++;
            }
        }
    }
}
