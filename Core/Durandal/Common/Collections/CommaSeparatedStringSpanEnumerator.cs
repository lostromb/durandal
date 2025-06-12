namespace Durandal.Common.Collections
{
    using Durandal.Common.Utils;
    using System;

    /// <summary>
    /// Takes an input char array and enumerates each comma-separated substring as its own span.
    /// This avoids substring allocation and is ideal for some high-performance scenarios.
    /// Currently this enumerator is very naive and does not handle escape sequences or anything fancy.
    /// Adjacent commas or commas at the beginning or end of strings are treated as delimiting zero-length strings in between them,
    /// so ",,," is a valid input that would enumerate 4 empty strings.
    /// </summary>
    public class CommaSeparatedStringSpanEnumerator : ISpanEnumerator<char>
    {
        private readonly ReadOnlyMemory<char> _sourceString;
        private int _startIdx;
        private int _endIdx;

        /// <summary>
        /// Constructs a new <see cref="CommaSeparatedStringSpanEnumerator"/> which enumerates over the given char memory.
        /// </summary>
        /// <param name="sourceString">The string to iterate over. May be null.</param>
        public CommaSeparatedStringSpanEnumerator(ReadOnlyMemory<char> sourceString)
        {
            _sourceString = sourceString;
            Reset();
        }

        /// <summary>
        /// Constructs a new <see cref="CommaSeparatedStringSpanEnumerator"/> which enumerates over the given string.
        /// </summary>
        /// <param name="sourceString">The string to iterate over. May be null.</param>
        public CommaSeparatedStringSpanEnumerator(string sourceString)
        {
            if (sourceString == null)
            {
                _sourceString = sourceString.AsMemory();
            }
            else
            {
                _sourceString = ReadOnlyMemory<char>.Empty;
            }

            Reset();
        }

        /// <inheritdoc />
        public bool MoveNext()
        {
            _startIdx = _endIdx + 1;

            if (_sourceString.Length == 0 || _startIdx > _sourceString.Length)
            {
                return false;
            }
            else if (_startIdx == _sourceString.Length)
            {
                // Special case for handling a comma at the end of the string
                _endIdx = _startIdx;
                return true;
            }

            _endIdx = _sourceString.Span.Slice(_startIdx).IndexOf(',');
            if (_endIdx < 0)
            {
                // No comma, continue to end of string
                _endIdx = _sourceString.Length;
            }
            else
            {
                // Adjust the relative index that was returned by IndexOf
                _endIdx += _startIdx;
            }

            return true;
        }

        /// <inheritdoc />
        public bool Reset()
        {
            _startIdx = -1;
            _endIdx = -1;
            return true;
        }

        /// <inheritdoc />
        public ReadOnlySpan<char> Current
        {
            get
            {
                if (_startIdx < 0 || _startIdx >= _sourceString.Length)
                {
                    return ReadOnlySpan<char>.Empty;
                }
                else
                {
                    return _sourceString.Span.Slice(_startIdx, _endIdx - _startIdx);
                }
            }
        }
    }
}