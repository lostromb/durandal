﻿#region License
//Copyright(c) 2017 Mike Hadlow

//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.

#endregion

using System;

namespace Durandal.Common.Parsers
{
    /// <summary>
    /// Represents an error that occurs during parsing.
    /// </summary>
    public class ParseException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ParseException" /> class.
        /// </summary>
        public ParseException() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ParseException" /> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public ParseException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ParseException" /> class with a specified error message 
        /// and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, 
        /// or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
        public ParseException(string message, Exception innerException) : base(message, innerException) { }
    }
}
