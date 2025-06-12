using Durandal.Common.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.MathExt;
using System.IO;
using Durandal.Common.Logger;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Durandal.Common.IO;
using Durandal.Common.Time;
using System.Threading;
using System.Runtime.InteropServices;
using Durandal.Common.IO.Hashing;
using System.Numerics;

namespace Durandal.Common.Utils
{
    public static class StringUtils
    {
        private static Encoding _utf8EncodingWithoutBom;
        private static Encoding _asciiEncoding;

        private static Vector<short> _vecCarriageReturn;
        private static Vector<short> _vecLineFeed;

        static StringUtils()
        {
            try
            {
                _asciiEncoding = Encoding.GetEncoding("ASCII");
            }
            catch (Exception)
            {
                _asciiEncoding = Encoding.UTF8;
            }

            _utf8EncodingWithoutBom = new UTF8Encoding(false);

            if (Vector.IsHardwareAccelerated)
            {
                _vecCarriageReturn = new Vector<short>((short)'\r');
                _vecLineFeed = new Vector<short>((short)'\n');
            }
        }

        /// <summary>
        /// Gets the encoding instance for 7-bit ASCII
        /// </summary>
        public static Encoding ASCII_ENCODING => _asciiEncoding;
        
        /// <summary>
        /// A static instance of <see cref="Encoding.UTF8"/> with byte-order marks disabled on write.
        /// This should be used whenever you have a <see cref="System.IO.StreamWriter"/> outputting a UTF8 stream.
        /// </summary>
        public static Encoding UTF8_WITHOUT_BOM => _utf8EncodingWithoutBom;

        /// <summary>
        /// Converts an arbitrary string into a deterministic guid value; used to ensure that client IDs are unique and fit within constraints
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static Guid HashToGuid(string input)
        {
            Span<byte> byteArr = stackalloc byte[16];
            Span<int> intArr = MemoryMarshal.Cast<byte, int>(byteArr);
            Span<short> shortArr = MemoryMarshal.Cast<byte, short>(byteArr);
            ulong baseHash = GetFNV1AHash(input);
            FastRandom rand = new FastRandom(baseHash);
            rand.NextBytes(byteArr);
            return new Guid(
                intArr[0],
                shortArr[2],
                (short)(0x4000 | (shortArr[3] & 0x0FFF)), // set UUID type 4 (random)
                (byte)(0x80 | (byteArr[8] & 0x3F)), byteArr[9], byteArr[10], byteArr[11], // set variant 1
                byteArr[12], byteArr[13], byteArr[14], byteArr[15]);
        }

        /// <summary>
        /// Uses a regular expression to extract a subexpression from a string.
        /// </summary>
        /// <param name="expression">The regex to use for matching</param>
        /// <param name="input">The input text to match against</param>
        /// <param name="groupNum">The regex capture group to return. By default, the entire matched expression will be returned</param>
        /// <param name="queryLogger">A logger for the operation</param>
        /// <returns>The extracted subexpression, or an empty string if no match was found</returns>
        public static string RegexRip(Regex expression, string input, int groupNum = 0, ILogger queryLogger = null)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            Match match = expression.Match(input);
            if (!match.Success)
            {
                return string.Empty;
            }

            if (groupNum >= match.Groups.Count)
            {
                if (queryLogger != null)
                {
                    queryLogger.Log("Capture group " + groupNum + " doesn't exist for regex " + expression, LogLevel.Wrn);
                }

                groupNum = match.Groups.Count - 1;
            }

            return match.Groups[groupNum].ToString();
        }

        /// <summary>
        /// Removes all matches of the given regex against the given input string
        /// </summary>
        /// <param name="expression"></param>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string RegexRemove(Regex expression, string input)
        {
            return RegexReplace(expression, input, string.Empty);
        }

        /// <summary>
        /// Replaces parts of a string, using a regex for matching.
        /// </summary>
        /// <param name="expression">The expression to use as the matcher</param>
        /// <param name="input">The input string to operate on</param>
        /// <param name="replacement">The string to replace the matches with</param>
        /// <param name="maxReplacements">The max replacements to do, or -1 for infinite.</param>
        /// <returns>The modified string</returns>
        public static string RegexReplace(Regex expression, string input, string replacement, int maxReplacements = -1)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            MatchCollection matches = expression.Matches(input);

            string returnVal = string.Empty;
            int lastIndex = 0;
            int replacements = 0;

            foreach (Match match in matches)
            {
                returnVal += input.Substring(lastIndex, match.Index - lastIndex);
                lastIndex = match.Index + match.Length;
                returnVal += replacement;
                replacements++;
                if (maxReplacements > 0 && replacements >= maxReplacements)
                    break;
            }

            returnVal += input.Substring(lastIndex);

            return returnVal;
        }

        /// <summary>
        /// Removes invalid characters from a string so that the result will be a legal filename.
        /// Characters such as "/", "~", "%", will be replaced with underscores
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static string SanitizeFileName(string fileName)
        {
            string returnVal = fileName;
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                returnVal = returnVal.Replace(c, '_');
            }
            return returnVal;
        }

        /// <summary>
        /// Normalized edit distance (Levenshtein) algorithm for computing divergence of two strings
        /// </summary>
        /// <param name="one"></param>
        /// <param name="two"></param>
        /// <returns>A value between 0 (no divergence) and 1 (maximum divergence)</returns>
        public static float NormalizedEditDistance(string one, string two)
        {
            string compareOne = one.ToLowerInvariant();
            string compareTwo = two.ToLowerInvariant();
            const int insertWeight = 4;
            const int offsetWeight = 3;
            const int editWeight = 2;

            // The old magic box
            int[] gridA = new int[one.Length + 1];
            int[] gridB = new int[one.Length + 1];
            int[] distA = new int[one.Length + 1];
            int[] distB = new int[one.Length + 1];
            int[] temp;

            // Initialize the horizontal grid values
            for (int x = 0; x <= one.Length; x++)
            {
                gridA[x] = x * insertWeight;
                distA[x] = x;
            }

            for (int y = 1; y <= two.Length; y++)
            {
                // Initialize the vertical grid value
                gridB[0] = y * insertWeight;
                distB[0] = y;

                // Iterate through the DP table
                for (int x = 1; x <= one.Length; x++)
                {
                    int diagWeight = gridA[x - 1];
                    if (compareOne[x - 1] != compareTwo[y - 1])
                    {
                        diagWeight += editWeight;
                    }
                    int leftWeight = gridB[x - 1];
                    if (compareOne[x - 1] != compareTwo[y - 1])
                    {
                        leftWeight += offsetWeight;
                    }
                    else
                    {
                        leftWeight += insertWeight;
                    }
                    int upWeight = gridA[x];
                    if (compareOne[x - 1] != compareTwo[y - 1])
                    {
                        upWeight += offsetWeight;
                    }
                    else
                    {
                        upWeight += insertWeight;
                    }

                    if (diagWeight < leftWeight && diagWeight < upWeight)
                    {
                        gridB[x] = diagWeight;
                        distB[x] = distA[x - 1] + 1;
                    }
                    else if (leftWeight < upWeight)
                    {
                        gridB[x] = leftWeight;
                        distB[x] = distB[x - 1] + 1;
                    }
                    else
                    {
                        gridB[x] = upWeight;
                        distB[x] = distA[x] + 1;
                    }
                }

                // Swap the buffers
                temp = gridA;
                gridA = gridB;
                gridB = temp;

                temp = distA;
                distA = distB;
                distB = temp;
            }

            // Extract the return value from the corner of the DP table
            float minWeight = gridA[one.Length];
            // Normalize it based on the length of the path that was taken
            float pathLength = distA[one.Length];
            if (pathLength == 0)
                return 0;

            return minWeight / pathLength / insertWeight;
        }

        /// <summary>
        /// Escapes all XML characters in the input text, specifically '&lt;', '$gt;', '&amp;', and apostrophe
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string EscapeXml(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            return new XText(input).ToString(SaveOptions.DisableFormatting);
        }

        /// <summary>
        /// Hashes a string using the FNV1a 64-bit algorithm
        /// </summary>
        /// <param name="rawValue">The string to hash.</param>
        /// <returns>The 64-bit hash value.</returns>
        public static ulong GetFNV1AHash(string rawValue)
        {
            return GetFNV1AHash(rawValue.AsSpan());
        }

        /// <summary>
        /// Hashes a string using the FNV1a 64-bit algorithm
        /// </summary>
        /// <param name="rawValue">The string to hash.</param>
        /// <returns>The 64-bit hash value.</returns>
        public static ulong GetFNV1AHash(ReadOnlySpan<char> rawValue)
        {
            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
 
            ulong hash = offsetBasis;
 
            foreach (char chr in rawValue)
            {
                hash ^= chr;
                hash *= prime;
            }
 
            return hash;
        }

        /// <summary>
        /// Hashes a string builder's contents using the FNV1a 64-bit algorithm
        /// </summary>
        /// <param name="stringBuilder">The builder whose contents to hash.</param>
        /// <returns>The 64-bit hash value.</returns>
        public static ulong GetFNV1AHash(StringBuilder stringBuilder)
        {
            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;

            ulong hash = offsetBasis;

            for (int c = 0; c < stringBuilder.Length; c++)
            {
                hash ^= stringBuilder[c];
                hash *= prime;
            }

            return hash;
        }

        /// <summary>
        /// Checks whether the given string is found inside of a larger string, using ordinal comparison.
        /// This is a specialized method which aims to reduce allocation when the alternative would be
        /// to do string.Substring().Equals and incur a lot of potential allocation.
        /// </summary>
        /// <param name="searchFor">The small string you are looking for.</param>
        /// <param name="searchWithin">The larger string you are searching within.</param>
        /// <param name="charOffset">The index of the first char in the larger string to attempt the match.</param>
        /// <param name="stringComparer">The string comparer to use</param>
        /// <returns>Whether the substring matched.</returns>
        public static bool SubstringEquals(string searchFor, string searchWithin, int charOffset, StringComparison stringComparer)
        {
            searchFor.AssertNonNull(nameof(searchFor));
            searchWithin.AssertNonNull(nameof(searchWithin));

            if (charOffset < 0)
            {
                throw new IndexOutOfRangeException("charOffset must be 0 or higher");
            }

            int count = searchFor.Length;
            if (charOffset + count > searchWithin.Length)
            {
                throw new IndexOutOfRangeException("String length is out of range");
            }

            return searchFor.AsSpan().Equals(searchWithin.AsSpan(charOffset, count), stringComparer);
        }

        /// <summary>
        /// Reads a stream fully into a <see cref="PooledStringBuilder"/> and returns the builder.
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="streamEncoding">The encoding of the stream</param>
        /// <returns></returns>
        public static async Task<PooledStringBuilder> ConvertStreamIntoString(Stream stream, Encoding streamEncoding)
        {
            stream.AssertNonNull(nameof(stream));
            streamEncoding.AssertNonNull(nameof(streamEncoding));

            PooledStringBuilder localBuffer = StringBuilderPool.Rent();
            try
            {
                using (PooledBuffer<byte> byteBuffer = BufferPool<byte>.Rent())
                using (PooledBuffer<char> charBuffer = BufferPool<char>.Rent())
                {
                    Decoder decoder = streamEncoding.GetDecoder();
                    int byteBufferCapacity = Math.Min(byteBuffer.Buffer.Length / 4, charBuffer.Buffer.Length);
                    int thisReadSize = 1;
                    while (thisReadSize > 0)
                    {
                        thisReadSize = await stream.ReadAsync(byteBuffer.Buffer, 0, byteBufferCapacity);
                        if (thisReadSize > 0)
                        {
                            int charsInCharBuffer = decoder.GetChars(byteBuffer.Buffer, 0, thisReadSize, charBuffer.Buffer, 0);
                            localBuffer.Builder.Append(charBuffer.Buffer, 0, charsInCharBuffer);
                        }
                    }

                    PooledStringBuilder returnVal = localBuffer;
                    // If we succeeded thus far, disable disposal of the return value so we transfer ownership to the caller properly
                    localBuffer = null;
                    return returnVal;
                }
            }
            finally
            {
                localBuffer?.Dispose();
            }
        }

        /// <summary>
        /// Reads a stream fully into a <see cref="PooledStringBuilder"/> and returns the builder.
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="streamEncoding">The encoding of the stream</param>
        /// <param name="cancelToken">A cancel token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns></returns>
        public static async Task<PooledStringBuilder> ConvertStreamIntoString(
            NonRealTimeStream stream,
            Encoding streamEncoding,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            stream.AssertNonNull(nameof(stream));
            streamEncoding.AssertNonNull(nameof(streamEncoding));
            realTime.AssertNonNull(nameof(realTime));

            PooledStringBuilder localBuffer = StringBuilderPool.Rent();
            try
            {
                using (PooledBuffer<byte> byteBuffer = BufferPool<byte>.Rent())
                using (PooledBuffer<char> charBuffer = BufferPool<char>.Rent())
                {
                    Decoder decoder = streamEncoding.GetDecoder();
                    int byteBufferCapacity = Math.Min(byteBuffer.Buffer.Length / 4, charBuffer.Buffer.Length); // assumes no single char will exceed 4 bytes in length
                    int thisReadSize = 1;
                    while (thisReadSize > 0)
                    {
                        thisReadSize = await stream.ReadAsync(byteBuffer.Buffer, 0, byteBufferCapacity, cancelToken, realTime);
                        if (thisReadSize > 0)
                        {
                            int charsInCharBuffer = decoder.GetChars(byteBuffer.Buffer, 0, thisReadSize, charBuffer.Buffer, 0);
                            localBuffer.Builder.Append(charBuffer.Buffer, 0, charsInCharBuffer);
                        }
                    }

                    PooledStringBuilder returnVal = localBuffer;
                    // If we succeeded thus far, disable disposal of the return value so we transfer ownership to the caller properly
                    localBuffer = null;
                    return returnVal;
                }
            }
            finally
            {
                localBuffer?.Dispose();
            }
        }

        /// <summary>
        /// Copies the contents of the given StringBuilder to the given TextWriter without modification.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="output"></param>
        public static void CopyStringBuilderToTextWriter(StringBuilder input, TextWriter output)
        {
            using (PooledBuffer<char> pooledChar = BufferPool<char>.Rent())
            {
                CopyStringBuilderToTextWriter(input, output, pooledChar.Buffer);
            }
        }

        /// <summary>
        /// Copies the contents of the given StringBuilder to the given TextWriter without modification.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="output"></param>
        /// <param name="scratchBuf">A temporary scratch buffer; all of it will be used if possible</param>
        public static void CopyStringBuilderToTextWriter(StringBuilder input, TextWriter output, char[] scratchBuf)
        {
            int charsCopied = 0;
            while (charsCopied < input.Length)
            {
                int charsCanCopy = Math.Min(input.Length - charsCopied, scratchBuf.Length);
                input.CopyTo(charsCopied, scratchBuf, 0, charsCanCopy);
                output.Write(scratchBuf, 0, charsCanCopy);
                charsCopied += charsCanCopy;
            }
        }

        /// <summary>
        /// Formats the given time value to the given string builder using the ISO 8601 format, with
        /// This corresponds with the standard format string "HH:mm:ss".
        /// All formatting assumes CultureInfo.InvariantCulture (which shouldn't be a problem as this is ISO standard).
        /// </summary>
        /// <param name="datetime">The datetime to format</param>
        /// <param name="stringBuilder">The string builder to write the formatted string to.</param>
        public static void FormatTime_ISO8601(DateTimeOffset datetime, StringBuilder stringBuilder)
        {
            FormatIntZeroPadded_2(datetime.Hour, stringBuilder);
            stringBuilder.Append(':');
            FormatIntZeroPadded_2(datetime.Minute, stringBuilder);
            stringBuilder.Append(':');
            FormatIntZeroPadded_2(datetime.Second, stringBuilder);
        }

        /// <summary>
        /// Formats the given datetime value to the given string builder using the ISO 8601 format, with
        /// no fractional part. This corresponds with the standard format string "yyyy-MM-ddTHH:mm:ss".
        /// All formatting assumes CultureInfo.InvariantCulture (which shouldn't be a problem as this is ISO standard).
        /// Also, the <see cref="DateTimeKind"/> is ignored, so all inputs are assumed to be a "local time" of some sort.
        /// </summary>
        /// <param name="datetime">The datetime to format</param>
        /// <param name="stringBuilder">The string builder to write the formatted string to.</param>
        public static void FormatDateTime_ISO8601(DateTimeOffset datetime, StringBuilder stringBuilder)
        {
            FormatIntZeroPadded_4(datetime.Year, stringBuilder);
            stringBuilder.Append('-');
            FormatIntZeroPadded_2(datetime.Month, stringBuilder);
            stringBuilder.Append('-');
            FormatIntZeroPadded_2(datetime.Day, stringBuilder);
            stringBuilder.Append('T');
            FormatIntZeroPadded_2(datetime.Hour, stringBuilder);
            stringBuilder.Append(':');
            FormatIntZeroPadded_2(datetime.Minute, stringBuilder);
            stringBuilder.Append(':');
            FormatIntZeroPadded_2(datetime.Second, stringBuilder);
        }

        /// <summary>
        /// Formats the given datetime value to the given string builder using the ISO 8601 format, with
        /// a 3-digit fractional part at the end representing milliseconds. This corresponds with the standard
        /// format string "yyyy-MM-ddTHH:mm:ss.fff".
        /// All formatting assumes CultureInfo.InvariantCulture (which shouldn't be a problem as this is ISO standard).
        /// </summary>
        /// <param name="datetime">The datetime to format</param>
        /// <param name="stringBuilder">The string builder to write the formatted string to.</param>
        public static void FormatDateTime_ISO8601WithMilliseconds(DateTimeOffset datetime, StringBuilder stringBuilder)
        {
            FormatIntZeroPadded_4(datetime.Year, stringBuilder);
            stringBuilder.Append('-');
            FormatIntZeroPadded_2(datetime.Month, stringBuilder);
            stringBuilder.Append('-');
            FormatIntZeroPadded_2(datetime.Day, stringBuilder);
            stringBuilder.Append('T');
            FormatIntZeroPadded_2(datetime.Hour, stringBuilder);
            stringBuilder.Append(':');
            FormatIntZeroPadded_2(datetime.Minute, stringBuilder);
            stringBuilder.Append(':');
            FormatIntZeroPadded_2(datetime.Second, stringBuilder);
            stringBuilder.Append('.');
            FormatIntZeroPadded_3(datetime.Millisecond, stringBuilder);
        }

        /// <summary>
        /// Formats the given datetime value to the given string builder using the ISO 8601 format, with
        /// a 6-digit fractional part at the end representing microseconds. This corresponds with the standard
        /// format string "yyyy-MM-ddTHH:mm:ss.ffffff".
        /// All formatting assumes CultureInfo.InvariantCulture (which shouldn't be a problem as this is ISO standard).
        /// </summary>
        /// <param name="datetime">The datetime to format</param>
        /// <param name="stringBuilder">The string builder to write the formatted string to.</param>
        public static void FormatDateTime_ISO8601WithMicroseconds(DateTimeOffset datetime, StringBuilder stringBuilder)
        {
            FormatIntZeroPadded_4(datetime.Year, stringBuilder);
            stringBuilder.Append('-');
            FormatIntZeroPadded_2(datetime.Month, stringBuilder);
            stringBuilder.Append('-');
            FormatIntZeroPadded_2(datetime.Day, stringBuilder);
            stringBuilder.Append('T');
            FormatIntZeroPadded_2(datetime.Hour, stringBuilder);
            stringBuilder.Append(':');
            FormatIntZeroPadded_2(datetime.Minute, stringBuilder);
            stringBuilder.Append(':');
            FormatIntZeroPadded_2(datetime.Second, stringBuilder);
            stringBuilder.Append('.');
            long convertedTicks = ((datetime.Ticks / 10) % 1000000);
            FormatIntZeroPadded_6(convertedTicks, stringBuilder);
        }

        /// <summary>
        /// Formats the given datetime value to the given string builder using the ISO 8601 format, with
        /// a 6-digit fractional part at the end representing microseconds. This corresponds with the standard
        /// format string "yyyy-MM-ddTHH:mm:ss.ffffff".
        /// All formatting assumes CultureInfo.InvariantCulture (which shouldn't be a problem as this is ISO standard).
        /// </summary>
        /// <param name="datetime">The datetime to format</param>
        /// <param name="writer">The text writer to write the formatted string to.</param>
        public static void FormatDateTime_ISO8601WithMicroseconds(DateTimeOffset datetime, TextWriter writer)
        {
            FormatIntZeroPadded_4(datetime.Year, writer);
            writer.Write('-');
            FormatIntZeroPadded_2(datetime.Month, writer);
            writer.Write('-');
            FormatIntZeroPadded_2(datetime.Day, writer);
            writer.Write('T');
            FormatIntZeroPadded_2(datetime.Hour, writer);
            writer.Write(':');
            FormatIntZeroPadded_2(datetime.Minute, writer);
            writer.Write(':');
            FormatIntZeroPadded_2(datetime.Second, writer);
            writer.Write('.');
            long convertedTicks = ((datetime.Ticks / 10) % 1000000);
            FormatIntZeroPadded_6(convertedTicks, writer);
        }

        /// <summary>
        /// Formats the given integer left-padded with zeroes at 2 columns to the given string builder.
        /// </summary>
        /// <param name="value">The input integer.</param>
        /// <param name="stringBuilder">The string builder to append to.</param>
        private static void FormatIntZeroPadded_2(int value, StringBuilder stringBuilder)
        {
            if (value < 10)
            {
                stringBuilder.Append('0');
            }

            stringBuilder.Append(value);
        }

        /// <summary>
        /// Formats the given integer left-padded with zeroes at 2 columns to the given string builder.
        /// </summary>
        /// <param name="value">The input integer.</param>
        /// <param name="writer">The text writer to append to.</param>
        private static void FormatIntZeroPadded_2(int value, TextWriter writer)
        {
            if (value < 10)
            {
                writer.Write('0');
            }

            writer.Write(value);
        }


        /// <summary>
        /// Formats the given integer left-padded with zeroes at 3 columns to the given string builder.
        /// </summary>
        /// <param name="value">The input integer.</param>
        /// <param name="stringBuilder">The string builder to append to.</param>
        private static void FormatIntZeroPadded_3(int value, StringBuilder stringBuilder)
        {
            if (value < 10)
            {
                stringBuilder.Append("00");
            }
            else if (value < 100)
            {
                stringBuilder.Append("0");
            }

            stringBuilder.Append(value);
        }

        /// <summary>
        /// Formats the given integer left-padded with zeroes at 4 columns to the given string builder.
        /// </summary>
        /// <param name="value">The input integer.</param>
        /// <param name="stringBuilder">The string builder to append to.</param>
        private static void FormatIntZeroPadded_4(int value, StringBuilder stringBuilder)
        {
            if (value < 10)
            {
                stringBuilder.Append("000");
            }
            else if (value < 100)
            {
                stringBuilder.Append("00");
            }
            else if (value < 1000)
            {
                stringBuilder.Append("0");
            }

            stringBuilder.Append(value);
        }

        /// <summary>
        /// Formats the given integer left-padded with zeroes at 4 columns to the given string builder.
        /// </summary>
        /// <param name="value">The input integer.</param>
        /// <param name="writer">The text writer to append to.</param>
        private static void FormatIntZeroPadded_4(int value, TextWriter writer)
        {
            if (value < 10)
            {
                writer.Write("000");
            }
            else if (value < 100)
            {
                writer.Write("00");
            }
            else if (value < 1000)
            {
                writer.Write("0");
            }

            writer.Write(value);
        }

        /// <summary>
        /// Formats the given integer left-padded with zeroes at 6 columns to the given string builder.
        /// </summary>
        /// <param name="value">The input integer.</param>
        /// <param name="stringBuilder">The string builder to append to.</param>
        private static void FormatIntZeroPadded_6(long value, StringBuilder stringBuilder)
        {
            if (value < 10)
            {
                stringBuilder.Append("00000");
            }
            else if (value < 100)
            {
                stringBuilder.Append("0000");
            }
            else if (value < 1000)
            {
                stringBuilder.Append("000");
            }
            else if (value < 10000)
            {
                stringBuilder.Append("00");
            }
            else if (value < 100000)
            {
                stringBuilder.Append("0");
            }

            stringBuilder.Append(value);
        }

        /// <summary>
        /// Formats the given integer left-padded with zeroes at 6 columns to the given string builder.
        /// </summary>
        /// <param name="value">The input integer.</param>
        /// <param name="writer">The text writer to append to.</param>
        private static void FormatIntZeroPadded_6(long value, TextWriter writer)
        {
            if (value < 10)
            {
                writer.Write("00000");
            }
            else if (value < 100)
            {
                writer.Write("0000");
            }
            else if (value < 1000)
            {
                writer.Write("000");
            }
            else if (value < 10000)
            {
                writer.Write("00");
            }
            else if (value < 100000)
            {
                writer.Write("0");
            }

            writer.Write(value);
        }

        /// <summary>
        /// Replaces all \r and \n characters in the given char buffer with spaces.
        /// This implementation uses vectors for matching, which is better at scanning
        /// large buffers for rare match instances. This means it performs worse if
        /// there are the whooole lot of newline characters to replace.
        /// </summary>
        /// <param name="chars"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public static void ReplaceNewlinesWithSpace(char[] chars, int offset, int count)
        {
#if !NET7_0_OR_GREATER
#if DEBUG
            if (Vector.IsHardwareAccelerated && count > BufferPool<short>.DEFAULT_BUFFER_SIZE && FastRandom.Shared.NextBool())
#else
            if (Vector.IsHardwareAccelerated && count > BufferPool<short>.DEFAULT_BUFFER_SIZE)
#endif
            {
                // Very rare case that we're converting a huge amount of data AND we can't use spans. Break it up even further to avoid LOH allocation
                int overallIdx = offset;
                using (PooledBuffer<short> scratch = BufferPool<short>.Rent(BufferPool<short>.DEFAULT_BUFFER_SIZE))
                {
                    while (overallIdx < count + offset)
                    {
                        int thisBlockSize = FastMath.Min(BufferPool<short>.DEFAULT_BUFFER_SIZE, count - overallIdx + offset);
                        int end = thisBlockSize - (thisBlockSize % Vector<short>.Count);
                        Buffer.BlockCopy(chars, overallIdx * sizeof(char), scratch.Buffer, 0, end * sizeof(char));

                        int localIdx = 0;
                        while (localIdx < end)
                        {
                            Vector<short> cmp = new Vector<short>(scratch.Buffer, localIdx);

                            if (Vector.EqualsAny(cmp, _vecCarriageReturn) ||
                                Vector.EqualsAny(cmp, _vecLineFeed))
                            {
                                int cap = overallIdx + localIdx + Vector<short>.Count;
                                for (int idx = overallIdx + localIdx; idx < cap; idx++)
                                {
                                    if (chars[idx] == '\r' || chars[idx] == '\n')
                                    {
                                        chars[idx] = ' ';
                                    }
                                }
                            }

                            localIdx += Vector<short>.Count;
                        }

                        while (localIdx < thisBlockSize)
                        {
                            // there's probably a slightly more optimal way to do this but this is a legacy path so we don't really care too much
                            int idx = overallIdx + localIdx;
                            if (chars[idx] == '\r' || chars[idx] == '\n')
                            {
                                chars[idx] = ' ';
                            }

                            localIdx++;
                        }

                        overallIdx += thisBlockSize;
                    }
                }
            }
            else
#endif
#if DEBUG
            if (Vector.IsHardwareAccelerated && count > 512 && FastRandom.Shared.NextBool())
#else
            if (Vector.IsHardwareAccelerated && count > 512)
#endif
            {
                // Medium-length vector accelerated case
#if NET7_0_OR_GREATER
                // Using spans
                int idx = offset;
                int end = offset + count;
                int vectorEnd = offset + count - (count % Vector<short>.Count);
                Span<short> inputAsInt16 = MemoryMarshal.Cast<char, short>(chars.AsSpan());

                while (idx < vectorEnd)
                {
                    Vector<short> cmp = new Vector<short>(inputAsInt16.Slice(idx, Vector<short>.Count));

                    // might be nice if we could use SSE string comparison opcodes directly rather than just comparing scalar numbers
                    if (Vector.EqualsAny(cmp, _vecCarriageReturn) ||
                        Vector.EqualsAny(cmp, _vecLineFeed))
                    {
                        int cap = idx + Vector<short>.Count;
                        for (; idx < cap; idx++)
                        {
                            if (chars[idx] == '\r' || chars[idx] == '\n')
                            {
                                chars[idx] = ' ';
                            }
                        }
                    }
                    else
                    {
                        idx += Vector<short>.Count;
                    }
                }

                while (idx < end)
                {
                    if (chars[idx] == '\r' || chars[idx] == '\n')
                    {
                        chars[idx] = ' ';
                    }

                    idx++;
                }
#else
                // Legacy codepath
                using (PooledBuffer<short> scratch = BufferPool<short>.Rent(count))
                {
                    int idx = offset;
                    int end = count - (count % Vector<short>.Count);

                    // Can't make vectors out of spans in legacy code so we have to use a scratch buffer copy, even though the data is the same
                    // Potentially we could do type fudging using an explicit layout struct to cast the char[] to a short[], but that's not really worth it.
                    Buffer.BlockCopy(chars, offset * sizeof(char), scratch.Buffer, 0, end * sizeof(char));

                    while (idx < end + offset)
                    {
                        Vector<short> cmp = new Vector<short>(scratch.Buffer, idx - offset);

                        if (Vector.EqualsAny(cmp, _vecCarriageReturn) ||
                            Vector.EqualsAny(cmp, _vecLineFeed))
                        {
                            for (int c = 0; c < Vector<short>.Count; c++)
                            {
                                if (chars[idx] == '\r' || chars[idx] == '\n')
                                {
                                    chars[idx] = ' ';
                                }

                                idx++;
                            }
                        }
                        else
                        {
                            idx += Vector<short>.Count;
                        }
                    }

                    while (idx < count + offset)
                    {
                        if (chars[idx] == '\r' || chars[idx] == '\n')
                        {
                            chars[idx] = ' ';
                        }

                        idx++;
                    }
                }
#endif // !NET7_0_OR_GREATER
            }
            else
            {
                for (int idx = offset; idx < offset + count; idx++)
                {
                    if (chars[idx] == '\r' || chars[idx] == '\n')
                    {
                        chars[idx] = ' ';
                    }
                }
            }
        }

        /// <summary>
        /// Takes the full contents of one string builder and appends it to another one.
        /// </summary>
        /// <param name="source">The string builder to copy from</param>
        /// <param name="dest">The string builder to append to</param>
        public static void CopyAcross(StringBuilder source, StringBuilder dest)
        {
            source.AssertNonNull(nameof(source));
            dest.AssertNonNull(nameof(dest));
            using (PooledBuffer<char> buf = BufferPool<char>.Rent())
            {
                int inIdx = 0;
                dest.EnsureCapacity(dest.Length + source.Length);

                while (inIdx < source.Length)
                {
                    int canCopy = FastMath.Min(buf.Buffer.Length, source.Length - inIdx);
                    source.CopyTo(inIdx, buf.Buffer, 0, canCopy);
                    inIdx += canCopy;
                    dest.Append(buf.Buffer, 0, canCopy);
                }
            }
        }

        /// <summary>
        /// Compares whether the contents of two seing builders are equal.
        /// Null inputs follow the same semantics as <see cref="String.Equals(string, string, StringComparison)"/> - two null StringBuilders are considered equal to each other.
        /// </summary>
        /// <param name="a">The first stringbuilder</param>
        /// <param name="b">The second stringbuilder</param>
        /// <param name="comparison">The type of string comparison logic to apply</param>
        /// <returns>True if the string builders' contents are the same according to the given comparison logic.</returns>
        public static bool StringBuildersEqual(StringBuilder a, StringBuilder b, StringComparison comparison = StringComparison.Ordinal)
        {
            if (a == null)
            {
                return b == null;
            }
            else if (b == null)
            {
                return false;
            }

            // Optimistically check if the two builders are literally the same object
            if (object.ReferenceEquals(a, b))
            {
                return true;
            }

            if (a.Length != b.Length)
            {
                return false;
            }

            if (a.Length == 0)
            {
                // Early escape for zero-length builders, saves us more logic later
                return true;
            }

#if NET5_0_OR_GREATER
            // Use span comparison for speed when possible
            StringBuilder.ChunkEnumerator enumA = a.GetChunks();
            StringBuilder.ChunkEnumerator enumB = b.GetChunks();
            int idxA = 0;
            int idxB = 0;
            enumA.MoveNext();
            enumB.MoveNext(); // Should never return false because we already know both builders are non-zero length
            while (true)
            {
                // We have to ensure we're not coupled to implementation details, so be paranoid about making
                // assumptions of the lengths of any individual chunks. They could be arbitrary lengths between the
                // two builders. Thus, only compare the smallest possible subsequences we can on each loop.
                // The one assumption we do make is that the chunk enumerator will never return char buffer data
                // that is beyond the length of the current stringbuilder.length - so we never have to actually
                // track how many chars are remaining to compare
                int toCompare = Math.Min(enumA.Current.Length - idxA, enumB.Current.Length - idxB);
                if (!enumA.Current.Span.Slice(idxA, toCompare).Equals(enumB.Current.Span.Slice(idxB, toCompare), comparison))
                {
                    return false;
                }

                // increment indexes and check for wraparound at the same time
                idxA = (idxA + toCompare) % enumA.Current.Length;
                idxB = (idxB + toCompare) % enumB.Current.Length;
                if ((idxA == 0 && !enumA.MoveNext()) ||
                    (idxB == 0 && !enumB.MoveNext()))
                {
                    // One of the enumerators is exhausted. We're done.
                    return true;
                }
            }
#else
            if (a.Length <= 32)
            {
                // For short lengths, naive char iteration is fine
                switch (comparison)
                {
                    case StringComparison.Ordinal:
                        for (int c = 0; c < a.Length; c++)
                        {
                            if (a[c] != b[c])
                            {
                                return false;
                            }
                        }

                        return true;
                    case StringComparison.OrdinalIgnoreCase:
                        for (int c = 0; c < a.Length; c++)
                        {
                            if (char.ToUpperInvariant(a[c]) != char.ToUpperInvariant(b[c]))
                            {
                                return false;
                            }
                        }

                        return true;
                    default:
                        throw new NotImplementedException("Only Ordinal and OrdinalIgnoreCase comparisons are implemented");
                }
            }
            else
            {
                // For larger lengths, we copy from string builder to a temp buffer so we can use Span.SequenceEqual()
                using (PooledBuffer<char> sourceA = BufferPool<char>.Rent(BufferPool<char>.DEFAULT_BUFFER_SIZE))
                using (PooledBuffer<char> sourceB = BufferPool<char>.Rent(BufferPool<char>.DEFAULT_BUFFER_SIZE))
                {
                    int index = 0;
                    switch (comparison)
                    {
                        case StringComparison.Ordinal:
                            while (index < a.Length)
                            {
                                int copySize = FastMath.Min(a.Length - index, sourceA.Length);
                                a.CopyTo(index, sourceA.Buffer, 0, copySize);
                                b.CopyTo(index, sourceB.Buffer, 0, copySize);
                                if (!sourceA.Buffer.AsSpan(0, copySize).SequenceEqual(sourceB.Buffer.AsSpan(0, copySize)))
                                {
                                    return false;
                                }

                                index += copySize;
                            }

                            return true;
                        case StringComparison.OrdinalIgnoreCase:
                            while (index < a.Length)
                            {
                                int copySize = FastMath.Min(a.Length - index, sourceA.Length);
                                a.CopyTo(index, sourceA.Buffer, 0, copySize);
                                b.CopyTo(index, sourceB.Buffer, 0, copySize);
                                for (int z = 0; z < copySize; z++)
                                {
                                    // This is really slow but we don't have access to Span<char>.Equals in this code path
                                    if (char.ToUpperInvariant(sourceA.Buffer[z]) != char.ToUpperInvariant(sourceB.Buffer[z]))
                                    {
                                        return false;
                                    }
                                }

                                index += copySize;
                            }

                            return true;
                        default:
                            throw new NotImplementedException("Only Ordinal and OrdinalIgnoreCase comparisons are implemented");
                    }
                }
            }
#endif
        }

        /// <summary>
        /// An implementation of IndexOfAny() which operates on StringBuilders.
        /// </summary>
        /// <param name="builder">The builder to search within.</param>
        /// <param name="startIdx">The index to start the search within</param>
        /// <param name="charsToFind">The list of chars to search for</param>
        /// <returns>The first index on or after startIdx containing any of the search characters, or -1 of none of the characters were found.</returns>
        public static int IndexOfAnyInStringBuilder(StringBuilder builder, int startIdx, ReadOnlySpan<char> charsToFind)
        {
            builder.AssertNonNull(nameof(builder));
            startIdx.AssertNonNegative(nameof(startIdx));
            if (startIdx > builder.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(startIdx));
            }

            if (charsToFind.Length == 0)
            {
                return -1;
            }

#if NET6_0_OR_GREATER
            // we can use some fast span magic in netcore if available
            int idx;
            int globalChunkStart = 0;
            StringBuilder.ChunkEnumerator chunkEnum = builder.GetChunks();

            // seek to chunk containing startidx
            while (true)
            {
                if (!chunkEnum.MoveNext())
                {
                    return -1;
                }

                if (globalChunkStart + chunkEnum.Current.Length > startIdx)
                {
                    break;
                }

                globalChunkStart += chunkEnum.Current.Length;
            }

            // This specific chunk should contain startIdx
            idx = chunkEnum.Current.Slice(startIdx - globalChunkStart).Span.IndexOfAny(charsToFind);

            if (idx >= 0)
            {
                return startIdx + idx;
            }

            globalChunkStart += chunkEnum.Current.Length;

            // Successive chunks we can scan entirely without slicing
            while (chunkEnum.MoveNext())
            {
                idx = chunkEnum.Current.Span.IndexOfAny(charsToFind);

                if (idx >= 0)
                {
                    return globalChunkStart + idx;
                }

                globalChunkStart += chunkEnum.Current.Length;
            }
#else // !NET6_0_OR_GREATER
            // slower but more compatible loop
            while (startIdx < builder.Length)
            {
                char c = builder[startIdx];
                for (int test = 0; test < charsToFind.Length; test++)
                {
                    if (charsToFind[test] == c)
                    {
                        return startIdx;
                    }
                }

                startIdx++;
            }
#endif
            return -1;
        }
    }
}
