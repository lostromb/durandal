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
    interface IComment
    {
        ///<summary>
        ///Single-line comment header.
        ///</summary>
        string Single { get; set; }

        ///<summary>
        ///Newline character preference.
        ///</summary>
        string NewLine { get; set; }

        ///<summary>
        ///Multi-line comment opener.
        ///</summary>
        string MultiOpen { get; set; }

        ///<summary>
        ///Multi-line comment closer.
        ///</summary>
        string MultiClose { get; set; }

        ///<summary>
        ///Parse a single-line comment.
        ///</summary>
        Parser<string> SingleLineComment { get; }

        ///<summary>
        ///Parse a multi-line comment.
        ///</summary>
        Parser<string> MultiLineComment { get; }

        ///<summary>
        ///Parse a comment.
        ///</summary>
        Parser<string> AnyComment { get; }
    }
}
