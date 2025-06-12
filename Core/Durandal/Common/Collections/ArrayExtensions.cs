using Durandal.Common.IO;
using Durandal.Common.MathExt;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Durandal.Common.Collections
{
    /// <summary>
    /// Extensions and helpers for manipulating data within arrays.
    /// </summary>
    public static class ArrayExtensions
    {
        // Keep a statically allocated buffer of zeroes so we can zero out arrays using span copies which potentially have SIMD acceleration
        // In latest .Net, Array.Clear runs pretty dang fast which makes this a little obsolete. But I guess it's relevant for legacy .Net Framework
        private const int ZERO_BUF_LENGTH = 4096;
        private static readonly byte[] ZEROES = new byte[ZERO_BUF_LENGTH];

        public static readonly string[] EMPTY_STRING_ARRAY = new string[0];

        /// <summary>
        /// Writes zeroes to the given array with the given bounds.
        /// </summary>
        /// <param name="array">The array to write to</param>
        /// <param name="offset">The array index to start writing from</param>
        /// <param name="count">The number of values to write</param>
        public static void WriteZeroes(int[] array, int offset, int count)
        {
#if NET5_0_OR_GREATER
            array.AsSpan(offset, count).Clear();
#else
            WriteZeroesInternal(array, offset, count, sizeof(int));
#endif
        }

        /// <summary>
        /// Writes zeroes to the entire given array segment
        /// </summary>
        /// <param name="array">The array segment to write to</param>
        public static void WriteZeroes(ArraySegment<int> array)
        {
#if NET5_0_OR_GREATER
            array.AsSpan().Clear();
#else
            WriteZeroesInternal(array.Array, array.Offset, array.Count, sizeof(int));
#endif
        }

        /// <summary>
        /// Writes zeroes to the given array segment with the given bounds.
        /// </summary>
        /// <param name="array">The array segment to write to</param>
        /// <param name="extraOffset">The array index to start writing from (in addition to the segment's offset)</param>
        /// <param name="count">The number of values to write</param>
        public static void WriteZeroes(ArraySegment<int> array, int extraOffset, int count)
        {
#if NET5_0_OR_GREATER
            array.AsSpan(extraOffset, count).Clear();
#else
            WriteZeroesInternal(array.Array, array.Offset + extraOffset, count, sizeof(int));
#endif
        }

        /// <summary>
        /// Writes zeroes to the given array with the given bounds.
        /// </summary>
        /// <param name="array">The array to write to</param>
        /// <param name="offset">The array index to start writing from</param>
        /// <param name="count">The number of values to write</param>
        public static void WriteZeroes(uint[] array, int offset, int count)
        {
#if NET5_0_OR_GREATER
            array.AsSpan(offset, count).Clear();
#else
            WriteZeroesInternal(array, offset, count, sizeof(uint));
#endif
        }

        /// <summary>
        /// Writes zeroes to the entire given array segment
        /// </summary>
        /// <param name="array">The array segment to write to</param>
        public static void WriteZeroes(ArraySegment<uint> array)
        {
#if NET5_0_OR_GREATER
            array.AsSpan().Clear();
#else
            WriteZeroesInternal(array.Array, array.Offset, array.Count, sizeof(uint));
#endif
        }

        /// <summary>
        /// Writes zeroes to the given array segment with the given bounds.
        /// </summary>
        /// <param name="array">The array segment to write to</param>
        /// <param name="extraOffset">The array index to start writing from (in addition to the segment's offset)</param>
        /// <param name="count">The number of values to write</param>
        public static void WriteZeroes(ArraySegment<uint> array, int extraOffset, int count)
        {
#if NET5_0_OR_GREATER
            array.AsSpan(extraOffset, count).Clear();
#else
            WriteZeroesInternal(array.Array, array.Offset + extraOffset, count, sizeof(uint));
#endif
        }

        /// <summary>
        /// Writes zeroes to the given array with the given bounds.
        /// </summary>
        /// <param name="array">The array to write to</param>
        /// <param name="offset">The array index to start writing from</param>
        /// <param name="count">The number of values to write</param>
        public static void WriteZeroes(long[] array, int offset, int count)
        {
#if NET5_0_OR_GREATER
            array.AsSpan(offset, count).Clear();
#else
            WriteZeroesInternal(array, offset, count, sizeof(long));
#endif
        }

        /// <summary>
        /// Writes zeroes to the entire given array segment
        /// </summary>
        /// <param name="array">The array segment to write to</param>
        public static void WriteZeroes(ArraySegment<long> array)
        {
#if NET5_0_OR_GREATER
            array.AsSpan().Clear();
#else
            WriteZeroesInternal(array.Array, array.Offset, array.Count, sizeof(long));
#endif
        }

        /// <summary>
        /// Writes zeroes to the given array segment with the given bounds.
        /// </summary>
        /// <param name="array">The array segment to write to</param>
        /// <param name="extraOffset">The array index to start writing from (in addition to the segment's offset)</param>
        /// <param name="count">The number of values to write</param>
        public static void WriteZeroes(ArraySegment<long> array, int extraOffset, int count)
        {
#if NET5_0_OR_GREATER
            array.AsSpan(extraOffset, count).Clear();
#else
            WriteZeroesInternal(array.Array, array.Offset + extraOffset, count, sizeof(long));
#endif
        }

        /// <summary>
        /// Writes zeroes to the given array with the given bounds.
        /// </summary>
        /// <param name="array">The array to write to</param>
        /// <param name="offset">The array index to start writing from</param>
        /// <param name="count">The number of values to write</param>
        public static void WriteZeroes(double[] array, int offset, int count)
        {
#if NET5_0_OR_GREATER
            array.AsSpan(offset, count).Clear();
#else
            WriteZeroesInternal(array, offset, count, sizeof(double));
#endif
        }

        /// <summary>
        /// Writes zeroes to the entire given array segment
        /// </summary>
        /// <param name="array">The array segment to write to</param>
        public static void WriteZeroes(ArraySegment<double> array)
        {
#if NET5_0_OR_GREATER
            array.AsSpan().Clear();
#else
            WriteZeroesInternal(array.Array, array.Offset, array.Count, sizeof(double));
#endif
        }

        /// <summary>
        /// Writes zeroes to the given array segment with the given bounds.
        /// </summary>
        /// <param name="array">The array segment to write to</param>
        /// <param name="extraOffset">The array index to start writing from (in addition to the segment's offset)</param>
        /// <param name="count">The number of values to write</param>
        public static void WriteZeroes(ArraySegment<double> array, int extraOffset, int count)
        {
#if NET5_0_OR_GREATER
            array.AsSpan(extraOffset, count).Clear();
#else
            WriteZeroesInternal(array.Array, array.Offset + extraOffset, count, sizeof(double));
#endif
        }

        /// <summary>
        /// Writes zeroes to the given array with the given bounds.
        /// </summary>
        /// <param name="array">The array to write to</param>
        /// <param name="offset">The array index to start writing from</param>
        /// <param name="count">The number of values to write</param>
        public static void WriteZeroes(float[] array, int offset, int count)
        {
#if NET5_0_OR_GREATER
            array.AsSpan(offset, count).Clear();
#else
            WriteZeroesInternal(array, offset, count, sizeof(float));
#endif
        }

        /// <summary>
        /// Writes zeroes to the entire given array segment
        /// </summary>
        /// <param name="array">The array segment to write to</param>
        public static void WriteZeroes(ArraySegment<float> array)
        {
#if NET5_0_OR_GREATER
            array.AsSpan().Clear();
#else
            WriteZeroesInternal(array.Array, array.Offset, array.Count, sizeof(float));
#endif
        }

        /// <summary>
        /// Writes zeroes to the given array segment with the given bounds.
        /// </summary>
        /// <param name="array">The array segment to write to</param>
        /// <param name="extraOffset">The array index to start writing from (in addition to the segment's offset)</param>
        /// <param name="count">The number of values to write</param>
        public static void WriteZeroes(ArraySegment<float> array, int extraOffset, int count)
        {
#if NET5_0_OR_GREATER
            array.AsSpan(extraOffset, count).Clear();
#else
            WriteZeroesInternal(array.Array, array.Offset + extraOffset, count, sizeof(float));
#endif
        }

        /// <summary>
        /// Writes zeroes to the given array with the given bounds.
        /// </summary>
        /// <param name="array">The array to write to</param>
        /// <param name="offset">The array index to start writing from</param>
        /// <param name="count">The number of values to write</param>
        public static void WriteZeroes(byte[] array, int offset, int count)
        {
#if NET5_0_OR_GREATER
            array.AsSpan(offset, count).Clear();
#else
            WriteZeroesInternal(array, offset, count, 1);
#endif
        }

        /// <summary>
        /// Writes zeroes to the entire given array segment
        /// </summary>
        /// <param name="array">The array segment to write to</param>
        public static void WriteZeroes(ArraySegment<byte> array)
        {
#if NET5_0_OR_GREATER
            array.AsSpan().Clear();
#else
            WriteZeroesInternal(array.Array, array.Offset, array.Count, 1);
#endif
        }

        /// <summary>
        /// Writes zeroes to the given array segment with the given bounds.
        /// </summary>
        /// <param name="array">The array segment to write to</param>
        /// <param name="extraOffset">The array index to start writing from (in addition to the segment's offset)</param>
        /// <param name="count">The number of values to write</param>
        public static void WriteZeroes(ArraySegment<byte> array, int extraOffset, int count)
        {
#if NET5_0_OR_GREATER
            array.AsSpan(extraOffset, count).Clear();
#else
            WriteZeroesInternal(array.Array, array.Offset + extraOffset, count, 1);
#endif
        }

        /// <summary>
        /// Writes zeroes to the given array with the given bounds.
        /// </summary>
        /// <param name="array">The array to write to</param>
        /// <param name="offset">The array index to start writing from</param>
        /// <param name="count">The number of values to write</param>
        public static void WriteZeroes(short[] array, int offset, int count)
        {
#if NET5_0_OR_GREATER
            array.AsSpan(offset, count).Clear();
#else
            WriteZeroesInternal(array, offset, count, sizeof(short));
#endif
        }

        /// <summary>
        /// Writes zeroes to the entire given array segment
        /// </summary>
        /// <param name="array">The array segment to write to</param>
        public static void WriteZeroes(ArraySegment<short> array)
        {
#if NET5_0_OR_GREATER
            array.AsSpan().Clear();
#else
            WriteZeroesInternal(array.Array, array.Offset, array.Count, sizeof(short));
#endif
        }

        /// <summary>
        /// Writes zeroes to the given array segment with the given bounds.
        /// </summary>
        /// <param name="array">The array segment to write to</param>
        /// <param name="extraOffset">The array index to start writing from (in addition to the segment's offset)</param>
        /// <param name="count">The number of values to write</param>
        public static void WriteZeroes(ArraySegment<short> array, int extraOffset, int count)
        {
#if NET5_0_OR_GREATER
            array.AsSpan(extraOffset, count).Clear();
#else
            WriteZeroesInternal(array.Array, array.Offset + extraOffset, count, sizeof(short));
#endif
        }

        /// <summary>
        /// Low-level byte copy implementation of WriteZeroes
        /// </summary>
        /// <param name="array">The array to write to</param>
        /// <param name="startOffset">The start offset in elements</param>
        /// <param name="elementsToClear">The number of elements to clear</param>
        /// <param name="elementSize">The size of each element in bytes</param>
        private static void WriteZeroesInternal<T>(
            T[] array,
            int startOffset,
            int elementsToClear,
            int elementSize) where T : struct
        {
            Span<byte> targetSpanBytes = MemoryMarshal.AsBytes(array.AsSpan());
            ReadOnlySpan<byte> zeroes = ZEROES.AsSpan();
            int bytesCopied = 0;
            int bytesToCopy = elementsToClear * elementSize;
            int destOffsetBytes = startOffset * elementSize;
            while (bytesCopied < bytesToCopy)
            {
                int thisCopyLengthBytes = FastMath.Min(ZERO_BUF_LENGTH, bytesToCopy - bytesCopied);
                zeroes.Slice(0, thisCopyLengthBytes)
                    .CopyTo(targetSpanBytes.Slice(destOffsetBytes + bytesCopied, thisCopyLengthBytes));
                bytesCopied += thisCopyLengthBytes;
            }
        }

        /// <summary>
        /// Centralized buffer copy routine. Uses the fastest method available to the system to copy
        /// data between arrays. If the source and destinations arrays are the same, move semantics are guaranteed
        /// (that is, overlapping memory regions are guaranteed to not corrupt themselves by overwrites).
        /// </summary>
        /// <param name="source">The source array</param>
        /// <param name="source_idx">The start index on the source array</param>
        /// <param name="dest">The destination array. May be the same as source.</param>
        /// <param name="dest_idx">The start index on the destination array.</param>
        /// <param name="numElements">The number of bytes to copy.</param>
        public static void MemCopy(byte[] source, int source_idx, byte[] dest, int dest_idx, int numElements)
        {
            source.AsSpan().Slice(source_idx, numElements).CopyTo(dest.AsSpan().Slice(dest_idx, numElements));
        }

        /// <summary>
        /// Centralized buffer copy routine. Uses the fastest method available to the system to copy
        /// data between arrays. If the source and destinations arrays are the same, move semantics are guaranteed
        /// (that is, overlapping memory regions are guaranteed to not corrupt themselves by overwrites).
        /// </summary>
        /// <param name="source">The source array</param>
        /// <param name="source_idx">The start index on the source array</param>
        /// <param name="dest">The destination array. May be the same as source.</param>
        /// <param name="dest_idx">The start index on the destination array.</param>
        /// <param name="numElements">The number of elements to copy.</param>
        public static void MemCopy(int[] source, int source_idx, int[] dest, int dest_idx, int numElements)
        {
            source.AsSpan(source_idx, numElements).CopyTo(dest.AsSpan(dest_idx, numElements));
        }

        /// <summary>
        /// Centralized buffer copy routine. Uses the fastest method available to the system to copy
        /// data between arrays. If the source and destinations arrays are the same, move semantics are guaranteed
        /// (that is, overlapping memory regions are guaranteed to not corrupt themselves by overwrites).
        /// </summary>
        /// <param name="source">The source array</param>
        /// <param name="source_idx">The start index on the source array</param>
        /// <param name="dest">The destination array. May be the same as source.</param>
        /// <param name="dest_idx">The start index on the destination array.</param>
        /// <param name="numElements">The number of elements to copy.</param>
        public static void MemCopy(float[] source, int source_idx, float[] dest, int dest_idx, int numElements)
        {
            source.AsSpan(source_idx, numElements).CopyTo(dest.AsSpan(dest_idx, numElements));
        }

        /// <summary>
        /// Centralized buffer copy routine. Uses the fastest method available to the system to copy
        /// data between arrays. If the source and destinations arrays are the same, move semantics are guaranteed
        /// (that is, overlapping memory regions are guaranteed to not corrupt themselves by overwrites).
        /// </summary>
        /// <typeparam name="T">The type of primitive being copied</typeparam>
        /// <param name="source">The source array</param>
        /// <param name="source_idx">The start index on the source array</param>
        /// <param name="dest">The destination array. May be the same as source.</param>
        /// <param name="dest_idx">The start index on the destination array.</param>
        /// <param name="numElements">The number of elements (not bytes) to copy.</param>
        public static void MemCopy<T>(T[] source, int source_idx, T[] dest, int dest_idx, int numElements)
        {
            source.AsSpan(source_idx, numElements)
                .CopyTo(dest.AsSpan(dest_idx, numElements));
        }

        /// <summary>
        /// Buffer copy routine which adapts different primitive memory types. The method signature and the
        /// semantics are identical to <see cref="Buffer.BlockCopy"/>. Use this to, for example,
        /// copy float32 arrays to bytes and vice versa. Endianness of the copy is based on the processor
        /// running the operation, so be careful if the data you copy here ends up being transferred off-system.
        /// If the source and destinations arrays are the same, move semantics are guaranteed
        /// (that is, overlapping memory regions are guaranteed to not corrupt themselves by overwrites).
        /// </summary>
        /// <typeparam name="S">The source primitive type being copied</typeparam>
        /// <typeparam name="D">The target primitive type being copied</typeparam>
        /// <param name="source">The source array</param>
        /// <param name="sourceByteIdx">The start BYTE index on the source array</param>
        /// <param name="dest">The destination array. May be the same as source.</param>
        /// <param name="destByteIdx">The start BYTE index on the destination array.</param>
        /// <param name="bytesToCopy">The number of BYTES to copy.</param>
        public static void ReinterpretCastMemCopy<S, D>(S[] source, int sourceByteIdx, D[] dest, int destByteIdx, int bytesToCopy)
            where S : struct
            where D : struct
        {
            MemoryMarshal.AsBytes(source.AsSpan()).Slice(sourceByteIdx, bytesToCopy)
                .CopyTo(MemoryMarshal.AsBytes(dest.AsSpan()).Slice(destByteIdx, bytesToCopy));
        }

        /// <summary>
        /// Move bytes within a single array. This method guarantees safe moves if the source and destination spans overlap.
        /// </summary>
        /// <param name="source">The array to move data within</param>
        /// <param name="source_idx">The index of the point to copy from, indexed by # of bytes</param>
        /// <param name="dest_idx">The index of the point to copy to, indexed by # of bytes</param>
        /// <param name="numElements">The number of bytes to copy</param>
        public static void MemMove(byte[] source, int source_idx, int dest_idx, int numElements)
        {
            Span<byte> span = source.AsSpan();
            span.Slice(source_idx, numElements).CopyTo(span.Slice(dest_idx, numElements));
        }

        /// <summary>
        /// Move data within a single array. This method guarantees safe moves if the source and destination spans overlap.
        /// </summary>
        /// <param name="source">The array to move data within</param>
        /// <param name="source_idx">The index of the point to copy from, indexed by # of elements (as opposed to bytes)</param>
        /// <param name="dest_idx">The index of the point to copy to, indexed by # of elements (as opposed to bytes)</param>
        /// <param name="numElements">The number of elements elements to copy</param>
        public static void MemMove<T>(T[] source, int source_idx, int dest_idx, int numElements) where T : struct
        {
            Span<T> span = source.AsSpan();
            MemoryMarshal.AsBytes(span.Slice(source_idx, numElements))
                .CopyTo(MemoryMarshal.AsBytes(span.Slice(dest_idx, numElements)));
        }

        /// <summary>
        /// Attempts to retrieve a value by index from an array, returning a default value if the index is out of bounds
        /// </summary>
        /// <returns></returns>
        public static T TryGetArray<T>(T[] array, int index, T defaultValue)
        {
            if (index < 0 || index >= array.Length)
                return defaultValue;
            return array[index];
        }

        public static bool ArrayEquals(byte[] arrayOne, int oneOffset, byte[] arrayTwo, int twoOffset, int count)
        {
            if (oneOffset < 0 ||
                twoOffset < 0 ||
                oneOffset + count > arrayOne.Length ||
                twoOffset + count > arrayTwo.Length)
            {
                throw new IndexOutOfRangeException();
            }

            for (int c = 0; c < count; c++)
            {
                if (arrayOne[c + oneOffset] != (arrayTwo[c + twoOffset]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Returns true if every element in the given arrays are equal.
        /// </summary>
        /// <typeparam name="T">The type of array item</typeparam>
        /// <param name="a">The first array</param>
        /// <param name="b">The second array</param>
        /// <returns>True if they are equal</returns>
        public static bool ArrayEquals<T>(T[] a, T[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            for (int c = 0; c < a.Length; c++)
            {
                if (!a[c].Equals(b[c]))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Returns true if every element in the given buffers are equal.
        /// </summary>
        /// <typeparam name="T">The type of buffer item</typeparam>
        /// <param name="a">The first buffer</param>
        /// <param name="b">The second buffer</param>
        /// <returns>True if they are equal</returns>
        public static bool ArrayEquals<T>(PooledBuffer<T> a, PooledBuffer<T> b)
        {
            if ((a == null) ^ (b == null))
            {
                return false;
            }

            if (a.Length != b.Length)
            {
                return false;
            }

            return ArrayEquals(a.Buffer, 0, b.Buffer, 0, a.Length);
        }

        /// <summary>
        /// Returns true if every element in the given array segments are equal.
        /// </summary>
        /// <typeparam name="T">The type of array item</typeparam>
        /// <param name="a">The first array</param>
        /// <param name="aOffset">The initial offset of the first array</param>
        /// <param name="b">The second array</param>
        /// <param name="bOffset">The initial offset of the second array</param>
        /// <param name="count">The number of elements in each array</param>
        /// <returns>True if they are equal</returns>
        public static bool ArrayEquals<T>(T[] a, int aOffset, T[] b, int bOffset, int count)
        {
            for (int c = 0; c < count; c++)
            {
                if (!a[c + aOffset].Equals(b[c + bOffset]))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Returns true if every element in the given array segments are equal.
        /// </summary>
        /// <param name="a">The first array</param>
        /// <param name="aOffset">The initial offset of the first array</param>
        /// <param name="b">The second array</param>
        /// <param name="bOffset">The initial offset of the second array</param>
        /// <param name="count">The number of elements in each array</param>
        /// <param name="maxDelta">The maximum allowable difference between floating-point values in the array</param>
        /// <returns>True if they are equal</returns>
        public static bool ArrayEquals(float[] a, int aOffset, float[] b, int bOffset, int count, float maxDelta)
        {
            for (int c = 0; c < count; c++)
            {
                if (Math.Abs(a[c + aOffset] - b[c + bOffset]) > maxDelta)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Returns true if every element in the given array segments are equal.
        /// </summary>
        /// <typeparam name="T">The type of array item</typeparam>
        /// <param name="a">The first array</param>
        /// <param name="b">The second array</param>
        /// <returns>True if they are equal</returns>
        public static bool ArrayEquals<T>(ArraySegment<T> a, ArraySegment<T> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            for (int c = 0; c < a.Count; c++)
            {
                if (!a.Array[c + a.Offset].Equals(b.Array[c + b.Offset]))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// PCL implementation of the Array.Sort(Keys, Values) method.
        /// Sorts a pair of coupled arrays using a default comparer. Both the key
        /// and value arrays are sorted.
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="keys"></param>
        /// <param name="values"></param>
        public static void Sort<K, V>(K[] keys, V[] values) where K : IComparable
        {
            Sort(keys, values, Comparer<K>.Default);
        }

        /// <summary>
        /// PCL implementation of the Array.Sort(Keys, Values, Comparer) method.
        /// Sorts a pair of coupled arrays using a specified comparer. Both the key
        /// and value arrays are sorted.
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="keys"></param>
        /// <param name="values"></param>
        /// <param name="comparer">The comparer to use</param>
        public static void Sort<K, V>(K[] keys, V[] values, IComparer<K> comparer)
        {
            // Sanity checks
            if (keys == null)
                throw new ArgumentNullException("keys");
            if (values == null)
                throw new ArgumentNullException("values");
            if (comparer == null)
                throw new ArgumentNullException("comparer");
            if (keys.Length != values.Length)
                throw new ArgumentException("Key-value pair sorting requires two arrays of equal length");

            // Create scratch space
            K[] scratchK = new K[keys.Length];
            V[] scratchV = new V[values.Length];
            // Do a recursive in-place merge sort of the values
            RecursivePairedMergeSort(keys, values, scratchK, scratchV, 0, keys.Length, comparer);
        }

        private static void RecursivePairedMergeSort<K, V>(K[] keys, V[] values, K[] scratchK, V[] scratchV, int start, int end, IComparer<K> comparer)
        {
            int length = end - start;
            if (length < 2)
            {
                // Base case: no work to do
                return;
            }
            else if (length == 2)
            {
                // Base case: conditional swap
                if (comparer.Compare(keys[start], keys[start + 1]) > 0)
                {
                    K tmpK = keys[start];
                    keys[start] = keys[start + 1];
                    keys[start + 1] = tmpK;

                    V tmpV = values[start];
                    values[start] = values[start + 1];
                    values[start + 1] = tmpV;
                }

                return;
            }
            // OPT: If length is less than about 6, insertion sort is faster than doing another merge pass
            else
            {
                // Divide the array
                int mid = start + (length / 2);

                // Recurse
                RecursivePairedMergeSort(keys, values, scratchK, scratchV, start, mid, comparer);
                RecursivePairedMergeSort(keys, values, scratchK, scratchV, mid, end, comparer);

                // And merge
                int iterA = start; // left source iter
                int iterB = mid; // right source iter
                int iterC = start; // scratch (target) iter
                while (iterC < end)
                {
                    if (iterA == mid)
                    {
                        // Drain right
                        scratchK[iterC] = keys[iterB];
                        scratchV[iterC] = values[iterB];
                        iterB++;
                        iterC++;
                    }
                    else if (iterB == end)
                    {
                        // Drain left
                        scratchK[iterC] = keys[iterA];
                        scratchV[iterC] = values[iterA];
                        iterA++;
                        iterC++;
                    }
                    else
                    {
                        // Compare
                        if (comparer.Compare(keys[iterA], keys[iterB]) > 0)
                        {
                            scratchK[iterC] = keys[iterB];
                            scratchV[iterC] = values[iterB];
                            iterB++;
                        }
                        else
                        {
                            scratchK[iterC] = keys[iterA];
                            scratchV[iterC] = values[iterA];
                            iterA++;
                        }

                        iterC++;
                    }
                }

                // Copy from scratch back into our main array
                ArrayExtensions.MemCopy(scratchK, start, keys, start, length);
                ArrayExtensions.MemCopy(scratchV, start, values, start, length);
            }
        }
    }
}
