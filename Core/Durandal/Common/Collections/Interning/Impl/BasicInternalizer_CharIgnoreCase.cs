using Durandal.Common.IO.Crc;
using Durandal.Common.IO.Hashing;
using Durandal.Common.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Durandal.Common.Collections.Interning.Impl
{
    internal class BasicInternalizer_CharIgnoreCase : IPrimitiveInternalizer<char>
    {
        private readonly List<KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>> _values;
        private readonly IInternedKeySource<ReadOnlyMemory<char>> _valueSource;
        private readonly Dictionary<uint, int[]> _hashCodeToValueIndexesMapping;
        private readonly object _mutex;

        public BasicInternalizer_CharIgnoreCase(IInternedKeySource<ReadOnlyMemory<char>> keySource)
        {
            _hashCodeToValueIndexesMapping = new Dictionary<uint, int[]>();
            _values = new List<KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>>();
            _valueSource = keySource.AssertNonNull(nameof(keySource));
            _mutex = new object();
        }

        public InternalizerFeature Features => InternalizerFeature.CaseInsensitive;

        public int Count => _values.Count;

        /// <inheritdoc />
        public InternedKey<ReadOnlyMemory<char>> InternalizeValue(ReadOnlySpan<char> input, out ReadOnlySpan<char> internalizedValue)
        {
            uint hashCode = HashCharArrayCaseInsensitive(input);

            lock (_mutex)
            {
                InternedKey<ReadOnlyMemory<char>> newOrdinal;
                int[] possibleValueIndexes;
                if (!_hashCodeToValueIndexesMapping.TryGetValue(hashCode, out possibleValueIndexes))
                {
                    // Value and hash of value has never been seen before. Internalize.
                    // Create a new hash code -> index mapping
                    newOrdinal = _valueSource.GenerateNewUniqueValue();
                    newOrdinal.Key.AssertNonNegative(nameof(newOrdinal));

                    // Capture the index of the value and use that in the dict lookup
                    _hashCodeToValueIndexesMapping[hashCode] = new int[1] { _values.Count };

                    // And copy the entire value to our internal value set
                    _values.Add(new KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>(
                        new InternedKey<ReadOnlyMemory<char>>(newOrdinal.Key),
                        input.ToArray()));

                    internalizedValue = input;
                    return newOrdinal;
                }
                else
                {
                    // Hash code has been seen before. Does it match any known value?
                    foreach (int valueTableIndex in possibleValueIndexes)
                    {
                        var existingValue = _values[valueTableIndex];
                        if (existingValue.Value.Span.Equals(input, StringComparison.OrdinalIgnoreCase))
                        {
                            // Entry already exists
                            internalizedValue = existingValue.Value.Span;
                            return existingValue.Key;
                        }
                    }

                    // Need to update existing hash code -> ordinal mapping
                    // This code path is extremely rare because it only happens when there's a 32-bit hash collision
                    // with a value that hasn't been seen before.
                    newOrdinal = _valueSource.GenerateNewUniqueValue();
                    newOrdinal.Key.AssertNonNegative(nameof(newOrdinal));
                    int[] newValueIndexMapping = new int[possibleValueIndexes.Length + 1];
                    newValueIndexMapping[0] = _values.Count;
                    possibleValueIndexes.AsSpan().CopyTo(newValueIndexMapping.AsSpan(1));

                    _values.Add(new KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>(
                        new InternedKey<ReadOnlyMemory<char>>(newOrdinal.Key),
                        input.ToArray()));

                    _hashCodeToValueIndexesMapping[hashCode] = newValueIndexMapping;
                    internalizedValue = input;
                    return newOrdinal;
                }
            }
        }

        /// <inheritdoc />
        public bool TryGetInternalizedKey(ReadOnlySpan<char> input, out InternedKey<ReadOnlyMemory<char>> internalizedId)
        {
            uint hashCode = HashCharArrayCaseInsensitive(input);

            lock (_mutex)
            {
                int[] possibleValueIndexes;
                if (!_hashCodeToValueIndexesMapping.TryGetValue(hashCode, out possibleValueIndexes))
                {
                    internalizedId = default(InternedKey<ReadOnlyMemory<char>>);
                    return false;
                }

                foreach (int valueIndex in possibleValueIndexes)
                {
                    var existingValue = _values[valueIndex];
                    if (existingValue.Value.Span.Equals(input, StringComparison.OrdinalIgnoreCase))
                    {
                        internalizedId = existingValue.Key;
                        return true;
                    }
                }

                internalizedId = default(InternedKey<ReadOnlyMemory<char>>);
                return false;
            }
        }

        /// <inheritdoc />
        public bool TryGetInternalizedValue(
            ReadOnlySpan<char> input,
            out ReadOnlySpan<char> internalizedValue,
            out InternedKey<ReadOnlyMemory<char>> internalizedId)
        {
            uint hashCode = HashCharArrayCaseInsensitive(input);

            lock (_mutex)
            {
                int[] possibleValueIndexes;
                if (!_hashCodeToValueIndexesMapping.TryGetValue(hashCode, out possibleValueIndexes))
                {
                    internalizedValue = ReadOnlySpan<char>.Empty;
                    internalizedId = default(InternedKey<ReadOnlyMemory<char>>);
                    return false;
                }

                foreach (int valueIndex in possibleValueIndexes)
                {
                    var existingValue = _values[valueIndex];
                    if (existingValue.Value.Span.Equals(input, StringComparison.OrdinalIgnoreCase))
                    {
                        internalizedValue = existingValue.Value.Span;
                        internalizedId = existingValue.Key;
                        return true;
                    }
                }

                internalizedValue = ReadOnlySpan<char>.Empty;
                internalizedId = default(InternedKey<ReadOnlyMemory<char>>);
                return false;
            }
        }

        public IEnumerator<KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>> GetEnumerator()
        {
            return _values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _values.GetEnumerator();
        }

        private uint HashCharArrayCaseInsensitive(ReadOnlySpan<char> input)
        {
            // we need the case-insensitive hash code of the input
            // to do this, copy chars to a stack buffer, make them upper case, and then slurp that
            ICRC32C crc = CRC32CFactory.Create();
            CRC32CState crcState = new CRC32CState();
            const int CHAR_VECTOR_SIZE = 64;
            Span<char> charScratch = stackalloc char[CHAR_VECTOR_SIZE];
            Span<byte> byteScratch = MemoryMarshal.AsBytes(charScratch);
            int charsSlurped = 0;
            while (charsSlurped < input.Length)
            {
                int thisCharSize = Math.Min(CHAR_VECTOR_SIZE, input.Length - charsSlurped);
                ReadOnlySpan<char> source = input.Slice(charsSlurped, thisCharSize);
                for (int c = 0; c < thisCharSize; c++)
                {
                    charScratch[c] = char.ToUpperInvariant(source[c]);
                }

                crc.Slurp(ref crcState, byteScratch.Slice(0, thisCharSize * sizeof(char)));
                charsSlurped += thisCharSize;
            }

            return crcState.Checksum;
        }
    }
}
