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

namespace Durandal.Common.Parsers
{
    partial class Parse
    {
        /// <summary>
        /// \n or \r\n
        /// </summary>
        public static readonly Parser<string> LineEnd =
            (from r in Char('\r').Optional()
            from n in Char('\n')
            select r.IsDefined ? r.Get().ToString() + n : n.ToString())
            .Named("LineEnd");

        /// <summary>
        /// line ending or end of input
        /// </summary>
        public static readonly Parser<string> LineTerminator =
            Return("").End()
                .Or(LineEnd.End())
                .Or(LineEnd)
                .Named("LineTerminator");

        /// <summary>
        /// Parser for identifier starting with <paramref name="firstLetterParser"/> and continuing with <paramref name="tailLetterParser"/>
        /// </summary>
        public static Parser<string> Identifier(Parser<char> firstLetterParser, Parser<char> tailLetterParser)
        {
            return
                from firstLetter in firstLetterParser
                from tail in tailLetterParser.Many().Text()
                select firstLetter + tail;
        }
    }
}