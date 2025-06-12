using System;

namespace Durandal.Common.IO.Json
{
    internal readonly struct LocalStringReference
    {
        private readonly char[] _chars;
        private readonly int _startIndex;
        private readonly int _length;

        public char this[int i] => _chars[i];

        public char[] Chars => _chars;

        public int StartIndex => _startIndex;

        public int Length => _length;

        public LocalStringReference(char[] chars, int startIndex, int length)
        {
            _chars = chars;
            _startIndex = startIndex;
            _length = length;
        }

        public override string ToString()
        {
            return new string(_chars, _startIndex, _length);
        }
    }

    internal static class LocalStringReferenceExtensions
    {
        public static int IndexOf(this LocalStringReference s, char c, int startIndex, int length)
        {
            int index = Array.IndexOf(s.Chars, c, s.StartIndex + startIndex, length);
            if (index == -1)
            {
                return -1;
            }

            return index - s.StartIndex;
        }

        public static bool StartsWith(this LocalStringReference s, string text)
        {
            if (text.Length > s.Length)
            {
                return false;
            }

            char[] chars = s.Chars;

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != chars[i + s.StartIndex])
                {
                    return false;
                }
            }

            return true;
        }

        public static bool EndsWith(this LocalStringReference s, string text)
        {
            if (text.Length > s.Length)
            {
                return false;
            }

            char[] chars = s.Chars;

            int start = s.StartIndex + s.Length - text.Length;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != chars[i + start])
                {
                    return false;
                }
            }

            return true;
        }
    }
}