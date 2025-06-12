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
    /// Constructs customizable comment parsers.
    /// </summary>
    public class CommentParser : IComment
    {
        ///<summary>
        ///Single-line comment header.
        ///</summary>
        public string Single { get; set; }

        ///<summary>
        ///Newline character preference.
        ///</summary>
        public string NewLine { get; set; }

        ///<summary>
        ///Multi-line comment opener.
        ///</summary>
        public string MultiOpen { get; set; }

        ///<summary>
        ///Multi-line comment closer.
        ///</summary>
        public string MultiClose { get; set; }

        /// <summary>
        /// Initializes a Comment with C-style headers and Windows newlines.
        /// </summary>
        public CommentParser()
        {
            Single = "//";
            MultiOpen = "/*";
            MultiClose = "*/";
            NewLine = "\n";
        }

        /// <summary>
        /// Initializes a Comment with custom multi-line headers and newline characters.
        /// Single-line headers are made null, it is assumed they would not be used.
        /// </summary>
        /// <param name="multiOpen"></param>
        /// <param name="multiClose"></param>
        /// <param name="newLine"></param>
        public CommentParser(string multiOpen, string multiClose, string newLine = "\n")
        {
            Single = null;
            MultiOpen = multiOpen;
            MultiClose = multiClose;
            NewLine = newLine;
        }

        /// <summary>
        /// Initializes a Comment with custom headers and newline characters.
        /// </summary>
        /// <param name="single"></param>
        /// <param name="multiOpen"></param>
        /// <param name="multiClose"></param>
        /// <param name="newLine"></param>
        public CommentParser(string single, string multiOpen, string multiClose, string newLine = "\n")
        {
            Single = single;
            MultiOpen = multiOpen;
            MultiClose = multiClose;
            NewLine = newLine;
        }

        ///<summary>
        ///Parse a single-line comment.
        ///</summary>
        public Parser<string> SingleLineComment
        {
            get
            {
                if (Single == null)
                    throw new ParseException("Field 'Single' is null; single-line comments not allowed.");

                return from first in Parse.String(Single)
                       from rest in Parse.CharExcept(NewLine).Many().Text()
                       select rest;
            }
            private set { }
        }

        ///<summary>
        ///Parse a multi-line comment.
        ///</summary>
        public Parser<string> MultiLineComment
        {
            get
            {
                if (MultiOpen == null)
                    throw new ParseException("Field 'MultiOpen' is null; multi-line comments not allowed.");
                else if (MultiClose == null)
                    throw new ParseException("Field 'MultiClose' is null; multi-line comments not allowed.");

                return from first in Parse.String(MultiOpen)
                       from rest in Parse.AnyChar
                                    .Until(Parse.String(MultiClose)).Text()
                       select rest;
            }
            private set { }
        }

        ///<summary>
        ///Parse a comment.
        ///</summary>
        public Parser<string> AnyComment
        {
            get
            {
                if (Single != null && MultiOpen != null && MultiClose != null)
                    return SingleLineComment.Or(MultiLineComment);
                else if (Single != null && (MultiOpen == null || MultiClose == null))
                    return SingleLineComment;
                else if (Single == null && (MultiOpen != null && MultiClose != null))
                    return MultiLineComment;
                else throw new ParseException("Unable to parse comment; check values of fields 'MultiOpen' and 'MultiClose'.");
            }
            private set { }
        }
    }
}
