// Numerizer.cs - modified from the nChronic code base
// (c) Robert Wilczyński
// 
//The MIT License

//Copyright(c) Tom Preston-Werner

//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in
//all copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
//THE SOFTWARE.

using System;
using System.Text.RegularExpressions;

namespace Durandal.Common.NLP.Language.English
{
    /// <summary>
    /// Parses English-language string expressions and extracts numbers
    /// </summary>
    public static class EnglishNumerizer
    {
        private static readonly string[][] DIRECT_NUMS = new string[][]
            {
                new string[] {"eleven", "11"},
                new string[] {"twelve", "12"},
                new string[] {"thirteen", "13"},
                new string[] {"fourteen", "14"},
                new string[] {"fifteen", "15"},
                new string[] {"sixteen", "16"},
                new string[] {"seventeen", "17"},
                new string[] {"eighteen", "18"},
                new string[] {"nineteen", "19"},
                new string[] {"ninteen", "19"}, // Common mis-spelling
                new string[] {"zero", "0"},
                new string[] {"one", "1"},
                new string[] {"two", "2"},
                new string[] {"three", "3"},
                new string[] {@"four(\W|$)", "4$1"},
                // The weird regex is so that it matches four but not fourty
                new string[] {"five", "5"},
                new string[] {@"six(\W|$)", "6$1"},
                new string[] {@"seven(\W|$)", "7$1"},
                new string[] {@"eight(\W|$)", "8$1"},
                new string[] {@"nine(\W|$)", "9$1"},
                new string[] {"ten", "10"},
                new string[] {@"\ba[\b^$]", "1"}
                // doesn"t make sense for an "a" at the end to be a 1
            };

        private static readonly string[][] ORDINALS = new string[][]
            {
                new string[] {"first", "1"},
                new string[] {"third", "3"},
                new string[] {"fourth", "4"},
                new string[] {"fifth", "5"},
                new string[] {"sixth", "6"},
                new string[] {"seventh", "7"},
                new string[] {"eighth", "8"},
                new string[] {"ninth", "9"},
                new string[] {"tenth", "10"}
            };

        private static readonly Tuple<string, decimal>[] TEN_PREFIXES = new Tuple<string, decimal>[]
            {
                new Tuple<string, decimal>("twenty", 20),
                new Tuple<string, decimal>("thirty", 30),
                new Tuple<string, decimal>("forty", 40),
                new Tuple<string, decimal>("fourty", 40), // Common mis-spelling
                new Tuple<string, decimal>("fifty", 50),
                new Tuple<string, decimal>("sixty", 60),
                new Tuple<string, decimal>("seventy", 70),
                new Tuple<string, decimal>("eighty", 80),
                new Tuple<string, decimal>("ninety", 90)
            };

        private static readonly Tuple<string, decimal>[] BIG_PREFIXES = new Tuple<string, decimal>[]
            {
                new Tuple<string, decimal>("hundred", 100),
                new Tuple<string, decimal>("thousand", 1000),
                new Tuple<string, decimal>("million", 1000000),
                new Tuple<string, decimal>("billion", 1000000000),
                new Tuple<string, decimal>("trillion", 1000000000000)
            };

        private static readonly Regex FIRST_REGEX = new Regex(@" +|([^\d])-([^\d])");

        public static string Numerize(string value)
        {
            string result = value;

            // preprocess
            result = FIRST_REGEX.Replace(result, "$1 $2");
            // will mutilate hyphenated-words but shouldn't matter for date extraction
            result = result.Replace("a half", "haAlf");
            // take the 'a' out so it doesn't turn into a 1, save the half for the end

            // easy/direct replacements

            foreach (var pr in DIRECT_NUMS)
            {
                result = Regex.Replace(
                    result,
                    pr[0],
                    "<num>" + pr[1]);
            }

            foreach (var pr in ORDINALS)
            {
                result = Regex.Replace(
                    result,
                    pr[0],
                    "<num>" + pr[1] + pr[0].Substring(pr[0].Length - 2));
            }

            // ten, twenty, etc.

            foreach (var pr in TEN_PREFIXES)
            {
                result = Regex.Replace(
                    result,
                    "(?:" + pr.Item1 + @") *<num>(\d(?=[^\d]|$))*",
                    match => "<num>" + (pr.Item2 + decimal.Parse(match.Groups[1].Value)));
            }

            foreach (var pr in TEN_PREFIXES)
            {
                result = Regex.Replace(result, pr.Item1, "<num>" + pr.Item2.ToString());
            }

            // hundreds, thousands, millions, etc.

            foreach (var pr in BIG_PREFIXES)
            {
                result = Regex.Replace(result, @"(?:<num>)?(\d*) *" + pr.Item1, match => "<num>" + (pr.Item2 * decimal.Parse(match.Groups[1].Value)).ToString());
                result = Andition(result);
            }

            // fractional addition
            // I'm not combining this with the previous block as using float addition complicates the strings
            // (with extraneous .0"s and such )
            result = Regex.Replace(result, @"(\d +)(?: |and | -)*haAlf", match => (decimal.Parse(match.Groups[1].Value) + 0.5M).ToString()); // FIXME nonoptimal replacement
            result = result.Replace("<num>", "");
            return result;
        }

        private static readonly Regex SECOND_REGEX = new Regex(@"<num>(\d+)( | and )<num>(\d+)(?=[^\w]|$)");

        private static string Andition(string value)
        {
            var result = value;
            while (true)
            {
                var match = SECOND_REGEX.Match(result);
                if (match.Success == false)
                    break;
                result = result.Substring(0, match.Index) +
                    "<num>" + ((int.Parse(match.Groups[1].Value) + int.Parse(match.Groups[3].Value)).ToString()) +
                    result.Substring(match.Index + match.Length);
            }
            return result;
        }
    }
}