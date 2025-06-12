using Durandal.Common.Collections.Interning.Impl;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Collections.Interning
{
    /// <summary>
    /// Static factory for creating internalizers, which are high-performance lookup tables to map spans of data
    /// to uniquely identifying keys which describe that data.
    /// </summary>
    public static class InternalizerFactory
    {
        /// <summary>
        /// Creates a sealed copy of the given internalizer, which will provide high-performance
        /// read-only access to the lookup table.
        /// </summary>
        /// <param name="internalizer">The internalizer to seal.</param>
        /// <returns>A newly created, sealed internalizer containing a copy of the given internalizer's data.</returns>
        public static ISealedPrimitiveInternalizer<byte> Seal(this IPrimitiveInternalizer<byte> internalizer)
        {
            return CreateSealedInternalizer(internalizer, internalizer.Features);
        }

        /// <summary>
        /// Creates a sealed copy of the given internalizer, which will provide high-performance
        /// read-only access to the lookup table.
        /// </summary>
        /// <param name="internalizer">The internalizer to seal.</param>
        /// <returns>A newly created, sealed internalizer containing a copy of the given internalizer's data.</returns>
        public static ISealedPrimitiveInternalizer<char> Seal(this IPrimitiveInternalizer<char> internalizer)
        {
            return CreateSealedInternalizer(internalizer, internalizer.Features);
        }

        /// <summary>
        /// Creates a mutable internalizer.
        /// </summary>
        /// <param name="keySource">The provider for ordinals used by this internalizer.
        /// Or in other words, it provides context for what ranges of numbers are valid in what scenario.</param>
        /// <param name="features">The features to define for the newly created internalizer, such as case insensitivity.</param>
        /// <returns>A newly created internalizer.</returns>
        public static IPrimitiveInternalizer<byte> CreateInternalizer(
            IInternedKeySource<ReadOnlyMemory<byte>> keySource,
            InternalizerFeature features = InternalizerFeature.None)
        {
            return new BasicInternalizer_Byte(keySource);
        }

        /// <summary>
        /// Creates a mutable internalizer.
        /// </summary>
        /// <param name="keySource">The provider for ordinals used by this internalizer.
        /// Or in other words, it provides context for what ranges of numbers are valid in what scenario.</param>
        /// <param name="features">The features to define for the newly created internalizer, such as case insensitivity.</param>
        /// <returns>A newly created internalizer.</returns>
        public static IPrimitiveInternalizer<char> CreateInternalizer(
            IInternedKeySource<ReadOnlyMemory<char>> keySource,
            InternalizerFeature features = InternalizerFeature.None)
        {
            if (features.HasFlag(InternalizerFeature.CaseInsensitive))
            {
                return new BasicInternalizer_CharIgnoreCase(keySource);
            }
            else
            {
                return new BasicInternalizer_Char(keySource);
            }
        }

        /// <summary>
        /// Creates a sealed internalizer.
        /// </summary>
        /// <param name="entries">The list of keys + values for this internalizer to contain. Keys do not need to be unique, 
        /// but values do, and keys must be non-negative.</param>
        /// <param name="features">The features to define for the newly created internalizer, such as case insensitivity.</param>
        /// <returns>A newly created sealed internalizer.</returns>
        public static ISealedPrimitiveInternalizer<byte> CreateSealedInternalizer(
            IReadOnlyCollection<KeyValuePair<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>>> entries,
            InternalizerFeature features = InternalizerFeature.None)
        {
            if (features.HasFlag(InternalizerFeature.OnlyMatchesWithinSet))
            {
                return new SealedInternalizer_Byte_PerfectHash_Unchecked(entries);
            }

            // Check the distribution of entry lengths to estimate if a linear or perfecthash would
            // be more memory efficient
            int linearTableLength = BinaryTreeCommon.GetLongestEntryLength(entries);
            int perfectHashLength = BinaryTreeCommon.CalculateMinimumLengthTableSizePowerOfTwo(entries);
            int linearTableOverheadBytes = linearTableLength * 12;
            int perfectHashOverheadBytes = perfectHashLength * (sizeof(int) + 12);

            if (linearTableOverheadBytes > perfectHashOverheadBytes)
            {
                return new SealedInternalizer_Byte_PerfectHash(entries);
            }
            else
            {
                return new SealedInternalizer_Byte_Linear(entries);
            }
        }

        /// <summary>
        /// Creates a sealed internalizer.
        /// </summary>
        /// <param name="entries">The list of keys + values for this internalizer to contain. Keys do not need to be unique, 
        /// but values do, and keys must be non-negative.</param>
        /// <param name="features">The features to define for the newly created internalizer, such as case insensitivity.</param>
        /// <returns>A newly created sealed internalizer.</returns>
        public static ISealedPrimitiveInternalizer<char> CreateSealedInternalizer(
            IReadOnlyCollection<KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>> entries,
            InternalizerFeature features = InternalizerFeature.None)
        {
            if (features.HasFlag(InternalizerFeature.OnlyMatchesWithinSet))
            {
                if (features.HasFlag(InternalizerFeature.CaseInsensitive))
                {
                    return new SealedInternalizer_CharIgnoreCase_PerfectHash_Unchecked(entries);
                }
                else
                {
                    return new SealedInternalizer_Char_PerfectHash_Unchecked(entries);
                }
            }

            // Check the distribution of entry lengths to estimate if a linear or perfecthash would
            // be more memory efficient
            int linearTableLength = BinaryTreeCommon.GetLongestEntryLength(entries);
            int perfectHashLength = BinaryTreeCommon.CalculateMinimumLengthTableSizePowerOfTwo(entries);
            int linearTableOverheadBytes = linearTableLength * 12;
            int perfectHashOverheadBytes = perfectHashLength * (sizeof(int) + 12);

            if (linearTableOverheadBytes > perfectHashOverheadBytes)
            {
                if (features.HasFlag(InternalizerFeature.CaseInsensitive))
                {
                    return new SealedInternalizer_CharIgnoreCase_PerfectHash(entries);
                }
                else
                {
                    return new SealedInternalizer_Char_PerfectHash(entries);
                }
            }
            else
            {
                if (features.HasFlag(InternalizerFeature.CaseInsensitive))
                {
                    return new SealedInternalizer_CharIgnoreCase_Linear(entries);
                }
                else
                {
                    return new SealedInternalizer_Char_Linear(entries);
                }
            }
        }
    }
}
