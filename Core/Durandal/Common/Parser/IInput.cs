#region License
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
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Durandal.Common.Parsers
{
    /// <summary>
    /// Represents an input for parsing.
    /// </summary>
    public interface IInput : IEquatable<IInput>
    {
        /// <summary>
        /// Advances the input.
        /// </summary>
        /// <returns>A new <see cref="IInput" /> that is advanced.</returns>
        /// <exception cref="System.InvalidOperationException">The input is already at the end of the source.</exception>
        IInput Advance();

        /// <summary>
        /// Gets the whole source.
        /// </summary>
        string Source { get; }

        /// <summary>
        /// Gets the current <see cref="System.Char" />.
        /// </summary>
        char Current { get; }

        /// <summary>
        /// Gets a value indicating whether the end of the source is reached.
        /// </summary>
        bool AtEnd { get; }

        /// <summary>
        /// Gets the current positon.
        /// </summary>
        int Position { get; }

        /// <summary>
        /// Gets the current line number.
        /// </summary>
        int Line { get; }

        /// <summary>
        /// Gets the current column.
        /// </summary>
        int Column { get; }

        /// <summary>
        /// Memos used by this input
        /// </summary>
        IDictionary<object, object> Memos { get; }
    }
}
