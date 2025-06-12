using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Utils
{
    /// <summary>
    /// Utility class for detecting a magic pattern of bytes within a stream.
    /// It could be used for detecting line breaks in text, or delimiters between headers in an HTTP request.
    /// If you are familiar with the Ogg container format then this implements the "OggS" matcher https://www.xiph.org/ogg/doc/framing.html.
    /// </summary>
    public class CapturePatternMatcher
    {
        private readonly byte[] _capturePattern;
        private readonly byte[] _consecutiveMatches;
        private readonly int _patternLength;
        private int _oldest;

        public CapturePatternMatcher(byte[] capturePattern)
        {
            _capturePattern = capturePattern;
            _patternLength = _capturePattern.Length;
            if (_patternLength > byte.MaxValue)
            {
                throw new ArgumentOutOfRangeException("Capture pattern cannot be longer than " + byte.MaxValue + " bytes");
            }

            _consecutiveMatches = new byte[_patternLength];
            _oldest = 0;
        }

        /// <summary>
        /// Matches the next byte of input, and returns true if this byte is the final byte of a capture pattern.
        /// </summary>
        /// <param name="input">A byte from the stream where a capture pattern may be found</param>
        /// <returns>True if the input byte is the final byte of a full pattern</returns>
        public bool Match(byte input)
        {
            for (int iter = 0; iter < _patternLength; iter++)
            {
                if (input == _capturePattern[_patternLength - iter - 1])
                {
                    _consecutiveMatches[(_oldest + iter) % _patternLength]++;
                }
                else
                {
                    _consecutiveMatches[(_oldest + iter) % _patternLength] = 0;
                }
            }

            bool returnVal = _consecutiveMatches[_oldest] >= _patternLength;
            if (returnVal)
            {
                _consecutiveMatches[_oldest] = 0;
            }

            _oldest = (_oldest + 1) % _patternLength;
            return returnVal;
        }

        /// <summary>
        /// Resets this matcher's state
        /// </summary>
        public void Reset()
        {
            _oldest = 0;
            for (int c = 0; c < _patternLength; c++)
            {
                _consecutiveMatches[c] = 0;
            }
        }

        /// <summary>
        /// Attempts to find the first instance of the pattern in the given byte array.
        /// Returns either the index of the first byte of the first match, or -1 for no match found.
        /// Matcher state is carried over from previous invocations, so it's
        /// possible you could, for example, match a multi-byte string on the first byte
        /// of input because it was mostly matched on a previous call to Find().
        /// </summary>
        /// <param name="input"></param>
        /// <param name="startOffset"></param>
        /// <param name="maxCount"></param>
        /// <returns></returns>
        public int Find(byte[] input, int startOffset, int maxCount)
        {
            for (int endIdx = startOffset + maxCount; startOffset < endIdx; startOffset++)
            {
                if (Match(input[startOffset]))
                {
                    return startOffset - _patternLength + 1;
                }
            }

            return -1;
        }
    }
}
