using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal
{
    internal static class StringCase
    {
        internal static byte UPPER_CASE(byte c)
        {
            return ((((c) >= (byte)'a') && ((c) <= (byte)'z')) ? (byte)(c - 32) : c);
        }

        internal static byte LOWER_CASE(byte c)
        {
            return ((((c) >= (byte)'A') && ((c) <= (byte)'Z')) ? (byte)(c + 32) : c);
        }

        /// <summary>
        /// (FIXME! The implementation is incorrect!) 
        /// Case insensitive string compare.Return the usual -1, 0, +1, depending on
        /// str1 &lt;, =, &gt; str2 (case insensitive, of course).
        /// </summary>
        /// <param name="str1">The first string</param>
        /// <param name="str2">The second string</param>
        /// <returns></returns>
        internal static int strcmp_nocase(Pointer<byte> str1, Pointer<byte> str2)
        {
            byte c1, c2;

            if (str1.Equals(str2))
                return 0;
            if (str1.IsNonNull && str2.IsNonNull)
            {
                while (true)
                {
                    str1 = str1.Iterate(out c1);
                    c1 = UPPER_CASE(c1);
                    str2 = str2.Iterate(out c2);
                    c2 = UPPER_CASE(c2);
                    if (c1 != c2)
                        return (c1 - c2);
                    if (c1 == '\0')
                        return 0;
                }
            }
            else
                return (str1.IsNull) ? -1 : 1;
        }
    }
}
