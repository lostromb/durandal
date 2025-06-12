using System;
using System.Runtime.CompilerServices;
using System.IO;
using System.Globalization;
using System.Diagnostics;
using System.Numerics;
using Newtonsoft.Json.Utilities;
using Newtonsoft.Json;
using Durandal.Common.Utils;
using Durandal.Common.Collections;
using Durandal.Common.Cache;
using System.Buffers;
using Durandal.Common.Ontology;

namespace Durandal.Common.IO.Json
{
    internal enum ReadType
    {
        Read,
        ReadAsInt32,
        ReadAsInt64,
        ReadAsBytes,
        ReadAsString,
        ReadAsDecimal,
        ReadAsDateTime,
        ReadAsDateTimeOffset,
        ReadAsDouble,
        ReadAsBoolean
    }

    /// <summary>
    /// Represents a reader that provides fast, non-cached, forward-only access to JSON text data, customized for the Durandal project.
    /// This has approximate parity with the built-in Newtonsoft JsonTextReader, though it uses <see cref="BufferPool{T}"/> internally,
    /// has extensions for handling buffered data without extra allocations, and does not support some of the more complex scenarios
    /// such as constructors.
    /// </summary>
    public class JsonCustomTextReader : JsonReader, IJsonLineInfo
    {
        private const int DESIRED_BUFFER_FULLNESS = 1024;
        private const int MAX_NUMBER_CHARACTER_LENGTH = 380;

        private readonly TextReader _reader;
        //private readonly MFUStringCache _stringCache;
        private PooledSegmentedBuffer<char> _charBuffer = new PooledSegmentedBuffer<char>();
        private bool _isEndOfFile = false;

        // The actual number of chars the "next char" is in the buffer. May be longer than valuelength.
        // This is to cover cases like "we have just read a string as the value, and then eaten some whitespace afterwards"
        private int _bufferCharsUsed;
        private int _valueOffset;
        private int _valueLength;
        private bool _valueParsed;
        private object _value;
        private Type _valueType;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonTextReader"/> class with the specified <see cref="TextReader"/>.
        /// </summary>
        /// <param name="reader">The <see cref="TextReader"/> containing the JSON data to read.</param>
        public JsonCustomTextReader(TextReader reader)
        {
            _reader = reader.AssertNonNull(nameof(reader));
            LineNumber = 1;
            LinePosition = 1;
        }

        public override object Value
        {
            get
            {
                if (_valueLength == 0)
                {
                    return null;
                }

                // Has the value been generated yet?
                if (!_valueParsed)
                {
                    // If not, generate it
                    _value = ProduceValueInternal();
                    _valueParsed = !Debugger.IsAttached; // Prevent the debugger from messing up the cache when it touches this property
                }

                // Then return the cached value
                return _value;
            }
        }

        public override Type ValueType => _valueType;

        public int LineNumber { get; private set; }

        public int LinePosition { get; private set; }

        public bool HasLineInfo()
        {
            return true;
        }

        public override bool Read()
        {
            while (true)
            {
                switch (CurrentState)
                {
                    case State.Start:
                    case State.Property:
                    case State.Array:
                    case State.ArrayStart:
                        return ParseValue();
                    case State.Object:
                    case State.ObjectStart:
                        return ParseObject();
                    case State.PostValue:
                        // returns true if it hits
                        // end of object or array
                        if (ParsePostValue(false))
                        {
                            return true;
                        }

                        break;
                    case State.Finished:
                        if (EnsureInputBuffer(1)) // Any more data left to read?
                        {
                            EatWhitespaceAndDiscard();
                            if (_isEndOfFile)
                            {
                                SetToken(JsonToken.None);
                                _valueType = null;
                                return false;
                            }
                            else if (NextUnreadChar == '/')
                            {
                                ParseComment(true);
                                return true;
                            }

                            // FIXME fill in debug info
                            throw CreateReaderException(string.Format(CultureInfo.InvariantCulture, "Additional text encountered after finished reading JSON content."));
                        }

                        SetToken(JsonToken.None);
                        _valueType = null;
                        return false;
                    case State.Constructor:
                    case State.ConstructorStart:
                        throw CreateReaderException(string.Format(CultureInfo.InvariantCulture, "Unimplemented state: {0}.", CurrentState));
                    default:
                        throw CreateReaderException(string.Format(CultureInfo.InvariantCulture, "Unexpected state: {0}.", CurrentState));
                }
            }
        }

        private void DiscardAndAdvanceBuffer(int amount = 1)
        {
            _charBuffer.DiscardFromFront(amount + _bufferCharsUsed);
            _valueOffset = 0;
            _valueLength = 0;
            _bufferCharsUsed = 0;
            _valueParsed = false;
            LinePosition += amount;
        }

        private void AdvanceBufferAsValue(int amount = 1)
        {
            if (_valueLength == 0)
            {
                // Starting a new value
                _valueOffset = _bufferCharsUsed;
                _valueLength = amount;
            }
#if DEBUG
            else if (_bufferCharsUsed > _valueOffset + _valueLength)
            {
                throw new InvalidOperationException("Can't read multiple distinct values into the char buffer");
            }
#endif
            else
            {
                // Extending an existing value
                _valueLength += amount;
            }

            _bufferCharsUsed += amount;
            _valueParsed = false;
            LinePosition += amount;
        }

        private void AdvanceBufferAsNonValue(int amount = 1)
        {
            _bufferCharsUsed += amount;
            LinePosition += amount;
        }

        private char NextUnreadChar
        {
            get
            {
                return _charBuffer[_bufferCharsUsed];
            }
        }

        private char UnreadChar(int index)
        {
            return _charBuffer[_bufferCharsUsed + index];
        }

        private char ValueChar(int index)
        {
            return _charBuffer[_valueOffset + index];
        }

        private string CurrentValueAsString
        {
            get
            {
                if (_valueLength == 0)
                {
                    return string.Empty;
                }

                char[] buf = new char[_valueLength];
                _charBuffer.Read(_valueOffset, buf, 0, _valueLength);
                return new string(buf, 0, _valueLength);
            }
        }

        /// <summary>
        /// Loads data into the input char buffer until there are at least <paramref name="desiredChars"/> in the buffer, in
        /// addition to any currently held value.
        /// Returns true if we can satisfy the request.
        /// </summary>
        /// <param name="desiredChars">The number of characters we desire in the buffer</param>
        /// <returns>True if the buffer is filled to the amount desired.</returns>
        private bool EnsureInputBuffer(int desiredChars)
        {
            if (_isEndOfFile)
            {
                return _charBuffer.Count >= desiredChars + _bufferCharsUsed;
            }

            int minRequiredChars = desiredChars - _charBuffer.Count + _bufferCharsUsed;
            while (minRequiredChars > 0)
            {
                int actualReadSize = _charBuffer.Load(_reader, minRequiredChars + DESIRED_BUFFER_FULLNESS);
                if (actualReadSize == 0)
                {
                    // End of stream
                    _isEndOfFile = true;
                    return _charBuffer.Count >= desiredChars;
                }

                minRequiredChars = desiredChars - _charBuffer.Count + _bufferCharsUsed;
            }

            return _charBuffer.Count >= desiredChars + _bufferCharsUsed;
        }

        private void EatWhitespaceAndDiscard()
        {
            while (true)
            {
                if (!EnsureInputBuffer(1))
                {
                    // No more input data to eat.
                    return;
                }

                char currentChar = NextUnreadChar;

                switch (currentChar)
                {
                    case '\0':
                        DiscardAndAdvanceBuffer();
                        break;
                    case '\r':
                        DiscardAndAdvanceBuffer();
                        break;
                    case '\n':
                        DiscardAndAdvanceBuffer();
                        break;
                    default:
                        if (currentChar == ' ' || char.IsWhiteSpace(currentChar))
                        {
                            DiscardAndAdvanceBuffer();
                        }
                        else
                        {
                            return;
                        }
                        break;
                }
            }
        }

        private void EatWhitespaceAndPreserveValue()
        {
            while (true)
            {
                if (!EnsureInputBuffer(1))
                {
                    // No more input data to eat.
                    return;
                }

                char currentChar = NextUnreadChar;

                switch (currentChar)
                {
                    case '\0':
                        AdvanceBufferAsNonValue();
                        break;
                    case '\r':
                        AdvanceBufferAsNonValue();
                        break;
                    case '\n':
                        AdvanceBufferAsNonValue();
                        break;
                    default:
                        if (currentChar == ' ' || char.IsWhiteSpace(currentChar))
                        {
                            AdvanceBufferAsNonValue();
                        }
                        else
                        {
                            return;
                        }
                        break;
                }
            }
        }

        private void ProcessLineFeed()
        {
            DiscardAndAdvanceBuffer();
        }

        private void ProcessCarriageReturn(bool isReadingValue)
        {
            LineNumber++;
            LinePosition = 1;

            // If the next char exists and is a newline then discard it without touching the line position
            // This is to handle Mac-style line feed ordering
            if (EnsureInputBuffer(1) && NextUnreadChar == '\n')
            {
                if (isReadingValue)
                {
                    AdvanceBufferAsValue();
                }
                else
                {
                    DiscardAndAdvanceBuffer();
                }
            }
        }

        private object ProduceValueInternal()
        {
            if (_valueType is null)
            {
                return null;
            }
            else if (_valueType == typeof(bool) ||
                _valueType == typeof(int) ||
                _valueType == typeof(long) ||
                _valueType == typeof(float) ||
                _valueType == typeof(double) ||
                _valueType == typeof(decimal) ||
                _valueType == typeof(BigInteger))
            {
                return base.Value; // Primitive values are parsed already
            }
            else if (_valueType == typeof(string))
            {
                char[] newStringData = new char[_valueLength];
                _charBuffer.Read(_valueOffset, newStringData, 0, _valueLength);
                return new string(newStringData, 0, _valueLength);
            }

            return null;
        }

        private bool ParseValue()
        {
            while (true)
            {
                if (!EnsureInputBuffer(1))
                {
                    return false;
                }

                char currentChar = NextUnreadChar;
                switch (currentChar)
                {
                    case '\0':
                        throw new NotImplementedException("The JSON parser currently can't handle null characters");
                    case '"':
                    case '\'':
                        ParseString(currentChar, ReadType.Read);
                        return true;
                    case 't':
                        ParseTrue();
                        return true;
                    case 'f':
                        ParseFalse();
                        return true;
                    case 'n':
                        if (EnsureInputBuffer(2))
                        {
                            char next = UnreadChar(1);

                            if (next == 'u')
                            {
                                ParseNull();
                            }
                            else
                            {
                                throw CreateReaderException(string.Format(CultureInfo.InvariantCulture, "Unexpected character {0}", next));
                            }
                        }
                        else
                        {
                            DiscardAndAdvanceBuffer();
                            throw new EndOfStreamException();
                        }
                        return true;
                    case 'N':
                        ParseNumberNaN(ReadType.Read);
                        return true;
                    //case 'I':
                    //    ParseNumberPositiveInfinity(ReadType.Read);
                    //    return true;
                    //case '-':
                    //    if (EnsureChars(1, true) && _chars[_charPos + 1] == 'I')
                    //    {
                    //        ParseNumberNegativeInfinity(ReadType.Read);
                    //    }
                    //    else
                    //    {
                    //        ParseNumber(ReadType.Read);
                    //    }
                    //    return true;
                    case '/':
                        ParseComment(true);
                        return true;
                    case 'u':
                        ParseUndefined();
                        return true;
                    case '{':
                        DiscardAndAdvanceBuffer();
                        SetToken(JsonToken.StartObject);
                        _valueType = null;
                        return true;
                    case '[':
                        DiscardAndAdvanceBuffer();
                        SetToken(JsonToken.StartArray);
                        _valueType = null;
                        return true;
                    case ']':
                        DiscardAndAdvanceBuffer();
                        SetToken(JsonToken.EndArray);
                        _valueType = null;
                        return true;
                    case ',':
                        // don't increment position, the next call to read will handle comma
                        // this is done to handle multiple empty comma values
                        SetToken(JsonToken.Undefined);
                        _valueType = null;
                        return true;
                    case ')':
                        DiscardAndAdvanceBuffer();
                        SetToken(JsonToken.EndConstructor);
                        _valueType = null;
                        return true;
                    case '\r':
                        DiscardAndAdvanceBuffer();
                        break;
                    case '\n':
                        DiscardAndAdvanceBuffer();
                        break;
                    case ' ':
                    case '\t':
                        // eat
                        DiscardAndAdvanceBuffer();
                        break;
                    default:
                        if (char.IsWhiteSpace(currentChar))
                        {
                            // eat
                            DiscardAndAdvanceBuffer();
                            break;
                        }
                        if (char.IsNumber(currentChar) || currentChar == '-' || currentChar == '.')
                        {
                            ParseNumber(ReadType.Read);
                            return true;
                        }

                        throw CreateReaderException(string.Format(CultureInfo.InvariantCulture, "Unexpected character: {0}.", currentChar));
                }
            }
        }

        private bool ParseObject()
        {
            while (true)
            {
                if (!EnsureInputBuffer(1))
                {
                    return false;
                }

                char currentChar = NextUnreadChar;

                switch (currentChar)
                {
                    case '\0':
                        DiscardAndAdvanceBuffer();
                        break;
                    case '}':
                        SetToken(JsonToken.EndObject);
                        _valueType = null;
                        DiscardAndAdvanceBuffer();
                        return true;
                    case '/':
                        ParseComment(true);
                        return true;
                    case '\r':
                        DiscardAndAdvanceBuffer();
                        break;
                    case '\n':
                        DiscardAndAdvanceBuffer();
                        break;
                    case ' ':
                    case '\t':
                        // eat
                        DiscardAndAdvanceBuffer();
                        break;
                    default:
                        if (char.IsWhiteSpace(currentChar))
                        {
                            // eat
                            DiscardAndAdvanceBuffer();
                        }
                        else
                        {
                            return ParseProperty();
                        }
                        break;
                }
            }
        }

        private bool ParseComment()
        {
            return false;
        }

        public bool ParseProperty()
        {
            char firstChar = NextUnreadChar;
            char quoteChar;

            if (firstChar == '\"' || firstChar == '\'')
            {
                quoteChar = firstChar;
                DiscardAndAdvanceBuffer();
                ReadStringIntoBuffer(quoteChar);
            }
            else if (ValidIdentifierChar(firstChar))
            {
                throw new NotImplementedException("The JSON parser currently can't handle identifiers");
                //quoteChar = '\0';
                //ShiftBufferIfNeeded();
                //ParseUnquotedProperty();
            }
            else
            {
                throw CreateReaderException(string.Format(CultureInfo.InvariantCulture, "Invalid property identifier character: {0}.", NextUnreadChar));
            }

            EatWhitespaceAndPreserveValue();

            if (NextUnreadChar != ':')
            {
                throw CreateReaderException(string.Format(CultureInfo.InvariantCulture, "Invalid character after parsing property name. Expected ':' but got: {0}.", NextUnreadChar));
            }

            AdvanceBufferAsNonValue();

            // The property name string isn't parsed here because it will be lazily read afterwards.
            SetToken(JsonToken.PropertyName, null);
            _valueType = typeof(string);
            QuoteChar = quoteChar;
            return true;
        }

        /// <summary>
        /// Advances the buffer and updates the internal value ranges so that
        /// they refer to a span of chars representing the contents of a string.
        /// This method should be called when the buffer is on the first character of the string value.
        /// After this method returns, the buffer will be set to one char after the closing " mark of the string.
        /// </summary>
        /// <param name="quote">The quote character which will terminate this string.</param>
        private void ReadStringIntoBuffer(char quote)
        {
            while (true)
            {
                switch (NextUnreadChar)
                {
                    case '\0':
                        throw new NotImplementedException("The JSON parser currently can't handle null characters");
                    //    if (_charsUsed == charPos - 1)
                    //    {
                    //        charPos--;

                    //        if (ReadData(true) == 0)
                    //        {
                    //            _charPos = charPos;
                    //            throw new Exception(); // throw JsonReaderException.Create(this, "Unterminated string. Expected delimiter: {0}.".FormatWith(CultureInfo.InvariantCulture, quote));
                    //        }
                    //    }
                    //    break;
                    case '\\':
                        throw new NotImplementedException("The JSON parser currently can't handle escape characters in strings");
                    //    _charPos = charPos;
                    //    if (!EnsureChars(0, true))
                    //    {
                    //        throw new Exception(); // throw JsonReaderException.Create(this, "Unterminated string. Expected delimiter: {0}.".FormatWith(CultureInfo.InvariantCulture, quote));
                    //    }

                    //    // start of escape sequence
                    //    int escapeStartPos = charPos - 1;

                    //    char currentChar = _chars[charPos];
                    //    charPos++;

                    //    char writeChar;

                    //    switch (currentChar)
                    //    {
                    //        case 'b':
                    //            writeChar = '\b';
                    //            break;
                    //        case 't':
                    //            writeChar = '\t';
                    //            break;
                    //        case 'n':
                    //            writeChar = '\n';
                    //            break;
                    //        case 'f':
                    //            writeChar = '\f';
                    //            break;
                    //        case 'r':
                    //            writeChar = '\r';
                    //            break;
                    //        case '\\':
                    //            writeChar = '\\';
                    //            break;
                    //        case '"':
                    //        case '\'':
                    //        case '/':
                    //            writeChar = currentChar;
                    //            break;
                    //        case 'u':
                    //            _charPos = charPos;
                    //            writeChar = ParseUnicode();

                    //            if (StringUtils.IsLowSurrogate(writeChar))
                    //            {
                    //                // low surrogate with no preceding high surrogate; this char is replaced
                    //                writeChar = UnicodeReplacementChar;
                    //            }
                    //            else if (StringUtils.IsHighSurrogate(writeChar))
                    //            {
                    //                bool anotherHighSurrogate;

                    //                // loop for handling situations where there are multiple consecutive high surrogates
                    //                do
                    //                {
                    //                    anotherHighSurrogate = false;

                    //                    // potential start of a surrogate pair
                    //                    if (EnsureChars(2, true) && _chars[_charPos] == '\\' && _chars[_charPos + 1] == 'u')
                    //                    {
                    //                        char highSurrogate = writeChar;

                    //                        _charPos += 2;
                    //                        writeChar = ParseUnicode();

                    //                        if (StringUtils.IsLowSurrogate(writeChar))
                    //                        {
                    //                            // a valid surrogate pair!
                    //                        }
                    //                        else if (StringUtils.IsHighSurrogate(writeChar))
                    //                        {
                    //                            // another high surrogate; replace current and start check over
                    //                            highSurrogate = UnicodeReplacementChar;
                    //                            anotherHighSurrogate = true;
                    //                        }
                    //                        else
                    //                        {
                    //                            // high surrogate not followed by low surrogate; original char is replaced
                    //                            highSurrogate = UnicodeReplacementChar;
                    //                        }

                    //                        EnsureBufferNotEmpty();

                    //                        WriteCharToBuffer(highSurrogate, lastWritePosition, escapeStartPos);
                    //                        lastWritePosition = _charPos;
                    //                    }
                    //                    else
                    //                    {
                    //                        // there are not enough remaining chars for the low surrogate or is not follow by unicode sequence
                    //                        // replace high surrogate and continue on as usual
                    //                        writeChar = UnicodeReplacementChar;
                    //                    }
                    //                } while (anotherHighSurrogate);
                    //            }

                    //            charPos = _charPos;
                    //            break;
                    //        default:
                    //            _charPos = charPos;
                    //            throw new Exception(); // throw JsonReaderException.Create(this, "Bad JSON escape sequence: {0}.".FormatWith(CultureInfo.InvariantCulture, @"\" + currentChar));
                    //    }

                    //    EnsureBufferNotEmpty();
                    //    WriteCharToBuffer(writeChar, lastWritePosition, escapeStartPos);

                    //    lastWritePosition = charPos;
                    //    break;
                    case '\r':
                        AdvanceBufferAsValue();
                        ProcessCarriageReturn(true);
                        break;
                    case '\n':
                        AdvanceBufferAsValue();
                        ProcessLineFeed();
                        break;
                    case '"':
                    case '\'':
                        if (NextUnreadChar == quote)
                        {
                            // The quote matches the one that started the string. So it must be the terminator
                            AdvanceBufferAsNonValue();
                            return;
                        }

                        // Treat non-matching quotes as literal chars within the string
                        AdvanceBufferAsValue();
                        break;
                    default:
                        // This case covers all normal characters
                        AdvanceBufferAsValue();
                        break;
                }
            }
        }

        private bool ParseComment(bool treatCommentAsValue)
        {
            return false;
        }

        private void ParseNumber(ReadType readType)
        {
            ReadNumberIntoBuffer();
            ParseReadNumber(readType);
        }

        private void ReadNumberIntoBuffer()
        {
            while (true)
            {
                char currentChar = NextUnreadChar;
                switch (currentChar)
                {
                    case '\0':
                        throw new NotImplementedException("The JSON parser currently can't handle null characters");
                    case '-':
                    case '+':
                    case 'a':
                    case 'A':
                    case 'b':
                    case 'B':
                    case 'c':
                    case 'C':
                    case 'd':
                    case 'D':
                    case 'e':
                    case 'E':
                    case 'f':
                    case 'F':
                    case 'x':
                    case 'X':
                    case '.':
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        AdvanceBufferAsValue();
                        break;
                    default:
                        if (char.IsWhiteSpace(currentChar) || currentChar == ',' || currentChar == '}' || currentChar == ']' || currentChar == ')' || currentChar == '/')
                        {
                            return;
                        }

                        throw CreateReaderException(string.Format(CultureInfo.InvariantCulture,
                            "Unexpected character encountered while parsing number: {0}.", currentChar));
                }
            }
        }

        private void ParseReadNumber(ReadType readType)
        {
            // set state to PostValue now so that if there is an error parsing the number then the reader can continue
            // SetPostValueState(true);

            object numberValue;
            JsonToken numberType;
            Type newValueType;

            bool singleDigit = _valueLength == 1 && char.IsDigit(ValueChar(0));
            bool nonBase10 = ValueChar(0) == '0' &&
                _valueLength > 1 &&
                ValueChar(1) != '.' &&
                ValueChar(1) != 'e' &&
                ValueChar(1) != 'E';

            switch (readType)
            {
                case ReadType.ReadAsString:
                    {
                        string number = CurrentValueAsString;

                        // validate that the string is a valid number
                        if (nonBase10)
                        {
                            try
                            {
                                if (number.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                                {
                                    Convert.ToInt64(number, 16);
                                }
                                else
                                {
                                    Convert.ToInt64(number, 8);
                                }
                            }
                            catch (Exception ex)
                            {
                                throw CreateReaderException(string.Format(CultureInfo.InvariantCulture,
                                    "Input string '{0}' is not a valid number.", number), ex);
                            }
                        }
                        else
                        {
                            if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                            {
                                throw CreateReaderException(string.Format(CultureInfo.InvariantCulture,
                                    "Input string '{0}' is not a valid number.", number));
                            }
                        }

                        numberType = JsonToken.String;
                        numberValue = number;
                        newValueType = typeof(string);
                    }
                    break;
                case ReadType.ReadAsInt32:
                    {
                        if (singleDigit)
                        {
                            // digit char values start at 48 in ASCII
                            numberValue = (int)(ValueChar(0) - 48); // BoxedPrimitives.Get(firstChar - 48); OPT original code would cache boxed primitives
                            newValueType = typeof(int);
                        }
                        else if (nonBase10)
                        {
                            string number = CurrentValueAsString;

                            try
                            {
                                int integer = number.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? Convert.ToInt32(number, 16) : Convert.ToInt32(number, 8);
                                numberValue = integer; //BoxedPrimitives.Get(integer);
                                newValueType = typeof(int);
                            }
                            catch (Exception ex)
                            {
                                throw CreateReaderException(string.Format(CultureInfo.InvariantCulture,
                                    "Input string '{0}' is not a valid integer.", number), ex);
                            }
                        }
                        else
                        {
                            // OPT there were a bunch of optimizations in the original code here, mostly to avoid the ToString() and boxing.
                            string number = CurrentValueAsString;
                            int int32Value;
                            long int64Value;
                            if (int.TryParse(number, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int32Value))
                            {
                                numberValue = int32Value;
                                newValueType = typeof(int);
                            }
                            else if (long.TryParse(number, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int64Value))
                            {
                                throw CreateReaderException(string.Format(CultureInfo.InvariantCulture,
                                    "JSON integer {0} is too large or small for an Int32.", number));
                            }
                            else
                            {
                                throw CreateReaderException(string.Format(CultureInfo.InvariantCulture,
                                    "Input string '{0}' is not a valid integer.", number));
                            }
                        }

                        numberType = JsonToken.Integer;
                    }
                    break;
                case ReadType.ReadAsDecimal:
                    {
                        if (singleDigit)
                        {
                            // digit char values start at 48
                            numberValue = (decimal)(ValueChar(0) - 48); // BoxedPrimitives.Get((decimal)firstChar - 48); OPT original code would cache boxed primitives
                            newValueType = typeof(decimal);
                        }
                        else if (nonBase10)
                        {
                            string number = CurrentValueAsString;

                            try
                            {
                                // decimal.Parse doesn't support parsing hexadecimal values
                                long integer = number.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? Convert.ToInt64(number, 16) : Convert.ToInt64(number, 8);
                                numberValue = Convert.ToDecimal(integer);
                                newValueType = typeof(decimal);
                            }
                            catch (Exception ex)
                            {
                                throw CreateReaderException(string.Format(CultureInfo.InvariantCulture, 
                                    "Input string '{0}' is not a valid decimal.", number), ex);
                            }
                        }
                        else
                        {
                            // OPT there were a bunch of optimizations here that I removed
                            string number = CurrentValueAsString;
                            decimal decimalValue;
                            if (decimal.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out decimalValue))
                            {
                                numberValue = decimalValue; // BoxedPrimitives.Get(value);
                                newValueType = typeof(decimal);
                            }
                            else
                            {
                                throw CreateReaderException(string.Format(CultureInfo.InvariantCulture,
                                    "Input string '{0}' is not a valid decimal.", number));
                            }
                        }

                        numberType = JsonToken.Float;
                    }
                    break;
                case ReadType.ReadAsDouble:
                    {
                        if (singleDigit)
                        {
                            // digit char values start at 48
                            numberValue = ((double)ValueChar(0) - 48); // BoxedPrimitives.Get();
                            newValueType = typeof(double);
                        }
                        else if (nonBase10)
                        {
                            string number = CurrentValueAsString;

                            try
                            {
                                // double.Parse doesn't support parsing hexadecimal values
                                long integer = number.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? Convert.ToInt64(number, 16) : Convert.ToInt64(number, 8);
                                numberValue = Convert.ToDouble(integer); //BoxedPrimitives.Get()
                                newValueType = typeof(long);
                            }
                            catch (Exception ex)
                            {
                                throw CreateReaderException(string.Format(CultureInfo.InvariantCulture,
                                    "Input string '{0}' is not a valid double.", number), ex);
                            }
                        }
                        else
                        {
                            string number = CurrentValueAsString;

                            if (double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                            {
                                numberValue = value; //BoxedPrimitives.Get(
                                newValueType = typeof(double);
                            }
                            else
                            {
                                throw CreateReaderException(string.Format(CultureInfo.InvariantCulture,
                                    "Input string '{0}' is not a valid double.", number));
                            }
                        }

                        numberType = JsonToken.Float;
                    }
                    break;
                case ReadType.Read:
                case ReadType.ReadAsInt64:
                    {
                        if (singleDigit)
                        {
                            // digit char values start at 48
                            numberValue = (long)ValueChar(0) - 48; //BoxedPrimitives.Get();
                            numberType = JsonToken.Integer;
                            newValueType = typeof(long);
                        }
                        else if (nonBase10)
                        {
                            string number = CurrentValueAsString;

                            try
                            {
                                // BoxedPrimitives.Get(
                                numberValue = number.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? Convert.ToInt64(number, 16) : Convert.ToInt64(number, 8);
                                newValueType = typeof(long);
                            }
                            catch (Exception ex)
                            {
                                throw CreateReaderException(string.Format(CultureInfo.InvariantCulture,
                                    "Input string '{0}' is not a valid number.", number), ex);
                            }

                            numberType = JsonToken.Integer;
                        }
                        else
                        {
                            // OPT there used to be a custom int64 parser to avoid the string allocation here
                            string number = CurrentValueAsString;
                            long int64value;
                            BigInteger bigIntValue;
                            decimal decimalValue;
                            double doubleValue;
                            if (number.Length > MAX_NUMBER_CHARACTER_LENGTH)
                            {
                                throw CreateReaderException(string.Format(CultureInfo.InvariantCulture,
                                    "JSON number '{0}' is too long to parse.", number));
                            }
                            else if (number.IndexOf('.') >= 0)
                            {
                                // It's actually a decimal
                                if (FloatParseHandling == FloatParseHandling.Decimal)
                                {
                                    // OPT there used to be custom parsers here
                                    if (decimal.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out decimalValue))
                                    {
                                        numberValue = decimalValue; // BoxedPrimitives.Get(d);
                                        newValueType = typeof(decimal);
                                    }
                                    else
                                    {
                                        throw CreateReaderException(string.Format(CultureInfo.InvariantCulture,
                                            "Input string '{0}' is not a valid number.", number));
                                    }
                                }
                                else
                                {
                                    if (double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out doubleValue))
                                    {
                                        numberValue = doubleValue; // BoxedPrimitives.Get(d);
                                        newValueType = typeof(double);
                                    }
                                    else
                                    {
                                        throw CreateReaderException(string.Format(CultureInfo.InvariantCulture,
                                            "Input string '{0}' is not a valid number.", number));
                                    }
                                }

                                numberType = JsonToken.Float;
                            }
                            else
                            {
                                // It's an integer; try int64 first and then fall back to BigInteger
                                if (long.TryParse(number, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int64value))
                                {
                                    numberValue = int64value; // BoxedPrimitives.Get(value);
                                    numberType = JsonToken.Integer;
                                    newValueType = typeof(long);
                                }
                                else if (BigInteger.TryParse(number, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out bigIntValue))
                                {
                                    numberValue = bigIntValue; // BoxedPrimitives.Get(value);
                                    numberType = JsonToken.Integer;
                                    newValueType = typeof(BigInteger);
                                }
                                else
                                {
                                    throw CreateReaderException(string.Format(CultureInfo.InvariantCulture,
                                        "Input string '{0}' is not a valid number.", number));
                                }
                            }
                        }
                    }
                    break;
                default:
                    throw CreateReaderException(string.Format(CultureInfo.InvariantCulture,
                         "Cannot read number value '{0}' as type '{1}'.", CurrentValueAsString, readType));
            }

            // FIXME there used to be some complexity with SetPostValueState here and 
            // passing false to SetToken. That gets triggered if number parsing fails
            // but we still want to continue reading
            SetToken(numberType, numberValue);
            _valueType = newValueType;
            _valueParsed = false;
        }

        private object ParseNumberNaN(ReadType readType)
        {
            return ParseNumberNaN(readType, MatchValueWithTrailingSeparator(JsonConvert.NaN));
        }

        private object ParseNumberNaN(ReadType readType, bool matched)
        {
            if (matched)
            {
                switch (readType)
                {
                    case ReadType.Read:
                    case ReadType.ReadAsDouble:
                        if (FloatParseHandling == FloatParseHandling.Double)
                        {
                            SetToken(JsonToken.Float, double.NaN); //BoxedPrimitives.DoubleNaN
                            _valueType = typeof(double);
                            return double.NaN;
                        }
                        break;
                    case ReadType.ReadAsString:
                        SetToken(JsonToken.String, JsonConvert.NaN);
                        _valueType = typeof(string);
                        return JsonConvert.NaN;
                }

                throw CreateReaderException("Cannot read NaN value.");
            }

            throw CreateReaderException("Cannot read NaN value.");
        }

        // ReadType gets passed by high-level calls to ReadAsBoolean, ReadAsString, etc.
        // We need a way to cache that until the deferred parsing operation actually happens
        private void ParseString(char quote, ReadType readType)
        {
            DiscardAndAdvanceBuffer();
            ReadStringIntoBuffer(quote);

            // The string value isn't parsed here because it will be lazily read afterwards.
            SetToken(JsonToken.String, null);
            _valueType = typeof(string);
        }

        private void ParseTrue()
        {
            // check characters equal 'true'
            // and that it is followed by either a separator character
            // or the text ends
            if (MatchValueWithTrailingSeparator(JsonConvert.True))
            {
                SetToken(JsonToken.Boolean, BoxedPrimitives.BooleanTrue);
                _valueType = typeof(bool);
            }
            else
            {
                throw CreateReaderException("Error parsing boolean value");
            }
        }

        private void ParseFalse()
        {
            // check characters equal 'false'
            // and that it is followed by either a separator character
            // or the text ends
            if (MatchValueWithTrailingSeparator(JsonConvert.False))
            {
                SetToken(JsonToken.Boolean, BoxedPrimitives.BooleanFalse);
                _valueType = typeof(bool);
            }
            else
            {
                throw CreateReaderException("Error parsing boolean value");
            }
        }

        private void ParseNull()
        {
            if (MatchValueWithTrailingSeparator(JsonConvert.Null))
            {
                SetToken(JsonToken.Null);
                _valueType = null;
            }
            else
            {
                throw CreateReaderException("Error parsing null value");
            }
        }

        private void ParseUndefined()
        {
            if (MatchValueWithTrailingSeparator(JsonConvert.Undefined))
            {
                SetToken(JsonToken.Undefined);
                _value = null;
                _valueType = null;
            }
            else
            {
                throw CreateReaderException("Error parsing undefined value.");
            }
        }

        private bool ParsePostValue(bool ignoreComments)
        {
            while (true)
            {
                char currentChar = NextUnreadChar;

                switch (currentChar)
                {
                    case '\0':
                        // FIXME what is this for?
                        //if (_charsUsed == _charPos)
                        //{
                        //    if (ReadData(false) == 0)
                        //    {
                        //        CurrentState = State.Finished;
                        //        return false;
                        //    }
                        //}
                        //else
                        {
                            DiscardAndAdvanceBuffer();
                        }
                        break;
                    case '}':
                        DiscardAndAdvanceBuffer();
                        SetToken(JsonToken.EndObject);
                        _valueType = null;
                        return true;
                    case ']':
                        DiscardAndAdvanceBuffer();
                        SetToken(JsonToken.EndArray);
                        _valueType = null;
                        return true;
                    case ')':
                        DiscardAndAdvanceBuffer();
                        SetToken(JsonToken.EndConstructor);
                        _valueType = null;
                        return true;
                    case '/':
                        ParseComment(!ignoreComments);
                        if (!ignoreComments)
                        {
                            return true;
                        }
                        break;
                    case ',':
                        DiscardAndAdvanceBuffer();

                        // finished parsing
                        SetStateBasedOnCurrent();
                        return false;
                    case ' ':
                    case '\t':
                        // eat
                        DiscardAndAdvanceBuffer();
                        break;
                    case '\r':
                        ProcessCarriageReturn(false);
                        break;
                    case '\n':
                        ProcessLineFeed();
                        break;
                    default:
                        if (char.IsWhiteSpace(currentChar))
                        {
                            // eat
                            DiscardAndAdvanceBuffer();
                        }
                        else
                        {
                            // handle multiple content without comma delimiter
                            if (SupportMultipleContent && Depth == 0)
                            {
                                SetStateBasedOnCurrent();
                                return false;
                            }

                            throw CreateReaderException(string.Format(CultureInfo.InvariantCulture, "After parsing a value an unexpected character was encountered: {0}.", currentChar));
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Returns true if the head of the character buffer contains the specified string.
        /// If there is a match, capture the value as a literal and advance the buffer.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private bool MatchValue(string value)
        {
            if (!EnsureInputBuffer(value.Length))
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                if (UnreadChar(i) != value[i])
                {
                    return false;
                }
            }

            AdvanceBufferAsValue(value.Length);
            return true;
        }

        // will match value and then move to the next character, checking that it is a separator character
        private bool MatchValueWithTrailingSeparator(string value)
        {
            if (!MatchValue(value))
            {
                return false;
            }

            if (!EnsureInputBuffer(value.Length + 1))
            {
                // No more chars after this value.
                return true;
            }

            return IsSeparator(NextUnreadChar) || NextUnreadChar == '\0';
        }

        private bool IsSeparator(char c)
        {
            switch (c)
            {
                case '}':
                case ']':
                case ',':
                    return true;
                case '/':
                    // check next character to see if start of a comment
                    if (!EnsureInputBuffer(2))
                    {
                        return false;
                    }

                    char nextChar = UnreadChar(1);

                    return (nextChar == '*' || nextChar == '/');
                case ')':
                    if (CurrentState == State.Constructor || CurrentState == State.ConstructorStart)
                    {
                        return true;
                    }
                    break;
                case ' ':
                case '\t':
                case '\r':
                case '\n':
                    return true;
                default:
                    if (char.IsWhiteSpace(c))
                    {
                        return true;
                    }
                    break;
            }

            return false;
        }

        private JsonReaderException CreateReaderException(string message, Exception innerException = null)
        {
            return new JsonReaderException(message, string.Empty, LineNumber, LinePosition, innerException);
        }

        private bool ValidIdentifierChar(char value)
        {
            return (char.IsLetterOrDigit(value) || value == '_' || value == '$');
        }

        private class BoxedPrimitives
        {
            public static readonly object BooleanTrue = true;
            public static readonly object BooleanFalse = false;
        }
    }
}
