namespace Durandal.Common.NLP.Alignment
{
    using System.Text;
    using System;

    /// <summary>
    /// An implementation of the Metaphone algorithm
    /// </summary>
    public class Metaphone
    {
        public static string Encode(string text)
        {
            Metaphone m = new Metaphone();
            return m.EncodeInternal(text);
        }

        // Constants
        private const int MaxEncodedLength = 6;
        private const char NullChar = (char)0;
        private const string Vowels = "AEIOU";

        // For tracking position within current string
        private string _text;
        private int _pos;

        /// <summary>
        /// Encodes the given text using the Metaphone algorithm.
        /// </summary>
        /// <param name="text">Text to encode</param>
        /// <returns></returns>
        private string EncodeInternal(string text)
        {
            // Process normalized text
            this.InitializeText(this.Normalize(text));

            // Write encoded string to StringBuilder
            StringBuilder builder = new StringBuilder();

            // Special handling of some string prefixes:
            //     PN, KN, GN, AE, WR, WH and X
            switch (this.Peek())
            {
                case 'P':
                case 'K':
                case 'G':
                    if (this.Peek(1) == 'N')
                        this.MoveAhead();
                    break;

                case 'A':
                    if (this.Peek(1) == 'E')
                        this.MoveAhead();
                    break;

                case 'W':
                    if (this.Peek(1) == 'R')
                        this.MoveAhead();
                    else if (this.Peek(1) == 'H')
                    {
                        builder.Append('W');
                        this.MoveAhead(2);
                    }
                    break;

                case 'X':
                    builder.Append('S');
                    this.MoveAhead();
                    break;
            }

            //
            while (!this.EndOfText && builder.Length < MaxEncodedLength)
            {
                // Cache this character
                char c = this.Peek();

                // Ignore duplicates except CC
                if (c == this.Peek(-1) && c != 'C')
                {
                    this.MoveAhead();
                    continue;
                }

                // Don't change F, J, L, M, N, R or first-letter vowel
                if (this.IsOneOf(c, "FJLMNR") ||
                    (builder.Length == 0 && this.IsOneOf(c, Vowels)))
                {
                    builder.Append(c);
                    this.MoveAhead();
                }
                else
                {
                    int charsConsumed = 1;

                    switch (c)
                    {
                        case 'B':
                            // B = 'B' if not -MB
                            if (this.Peek(-1) != 'M' || this.Peek(1) != NullChar)
                                builder.Append('B');
                            break;

                        case 'C':
                            // C = 'X' if -CIA- or -CH-
                            // Else 'S' if -CE-, -CI- or -CY-
                            // Else 'K' if not -SCE-, -SCI- or -SCY-
                            if (this.Peek(-1) != 'S' || !this.IsOneOf(this.Peek(1), "EIY"))
                            {
                                if (this.Peek(1) == 'I' && this.Peek(2) == 'A')
                                    builder.Append('X');
                                else if (this.IsOneOf(this.Peek(1), "EIY"))
                                    builder.Append('S');
                                else if (this.Peek(1) == 'H')
                                {
                                    if ((this._pos == 0 && !this.IsOneOf(this.Peek(2), Vowels)) ||
                                        this.Peek(-1) == 'S')
                                        builder.Append('K');
                                    else
                                        builder.Append('X');
                                    charsConsumed++;    // Eat 'CH'
                                }
                                else builder.Append('K');
                            }
                            break;

                        case 'D':
                            // D = 'J' if DGE, DGI or DGY
                            // Else 'T'
                            if (this.Peek(1) == 'G' && this.IsOneOf(this.Peek(2), "EIY"))
                                builder.Append('J');
                            else
                                builder.Append('T');
                            break;

                        case 'G':
                            // G = 'F' if -GH and not B--GH, D--GH, -H--GH, -H---GH
                            // Else dropped if -GNED, -GN, -DGE-, -DGI-, -DGY-
                            // Else 'J' if -GE-, -GI-, -GY- and not GG
                            // Else K
                            if ((this.Peek(1) != 'H' || this.IsOneOf(this.Peek(2), Vowels)) &&
                                (this.Peek(1) != 'N' || (this.Peek(1) != NullChar &&
                                (this.Peek(2) != 'E' || this.Peek(3) != 'D'))) &&
                                (this.Peek(-1) != 'D' || !this.IsOneOf(this.Peek(1), "EIY")))
                            {
                                if (this.IsOneOf(this.Peek(1), "EIY") && this.Peek(2) != 'G')
                                    builder.Append('J');
                                else
                                    builder.Append('K');
                            }
                            // Eat GH
                            if (this.Peek(1) == 'H')
                                charsConsumed++;
                            break;

                        case 'H':
                            // H = 'H' if before or not after vowel
                            if (!this.IsOneOf(this.Peek(-1), Vowels) || this.IsOneOf(this.Peek(1), Vowels))
                                builder.Append('H');
                            break;

                        case 'K':
                            // K = 'C' if not CK
                            if (this.Peek(-1) != 'C')
                                builder.Append('K');
                            break;

                        case 'P':
                            // P = 'F' if PH
                            // Else 'P'
                            if (this.Peek(1) == 'H')
                            {
                                builder.Append('F');
                                charsConsumed++;    // Eat 'PH'
                            }
                            else
                                builder.Append('P');
                            break;

                        case 'Q':
                            // Q = 'K'
                            builder.Append('K');
                            break;

                        case 'S':
                            // S = 'X' if SH, SIO or SIA
                            // Else 'S'
                            if (this.Peek(1) == 'H')
                            {
                                builder.Append('X');
                                charsConsumed++;    // Eat 'SH'
                            }
                            else if (this.Peek(1) == 'I' && this.IsOneOf(this.Peek(2), "AO"))
                                builder.Append('X');
                            else
                                builder.Append('S');
                            break;

                        case 'T':
                            // T = 'X' if TIO or TIA
                            // Else '0' if TH
                            // Else 'T' if not TCH
                            if (this.Peek(1) == 'I' && this.IsOneOf(this.Peek(2), "AO"))
                                builder.Append('X');
                            else if (this.Peek(1) == 'H')
                            {
                                builder.Append('0');
                                charsConsumed++;    // Eat 'TH'
                            }
                            else if (this.Peek(1) != 'C' || this.Peek(2) != 'H')
                                builder.Append('T');
                            break;

                        case 'V':
                            // V = 'F'
                            builder.Append('F');
                            break;

                        case 'W':
                        case 'Y':
                            // W,Y = Keep if not followed by vowel
                            if (this.IsOneOf(this.Peek(1), Vowels))
                                builder.Append(c);
                            break;

                        case 'X':
                            // X = 'S' if first character (already done)
                            // Else 'KS'
                            builder.Append("KS");
                            break;

                        case 'Z':
                            // Z = 'S'
                            builder.Append('S');
                            break;
                    }
                    // Advance over consumed characters
                    this.MoveAhead(charsConsumed);
                }
            }
            // Return result
            return builder.ToString();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="text"></param>
        private void InitializeText(string text)
        {
            this._text = text;
            this._pos = 0;
        }

        /// <summary>
        /// Indicates if the current position is at the end of
        /// the text.
        /// </summary>
        private bool EndOfText
        {
            get { return this._pos >= this._text.Length; }
        }

        /// <summary>
        /// Moves the current position ahead one character.
        /// </summary>
        private void MoveAhead()
        {
            this.MoveAhead(1);
        }

        /// <summary>
        /// Moves the current position ahead the specified number.
        /// of characters.
        /// </summary>
        /// <param name="count">Number of characters to move
        /// ahead.</param>
        private void MoveAhead(int count)
        {
            this._pos = Math.Min(this._pos + count, this._text.Length);
        }

        /// <summary>
        /// Returns the character at the current position.
        /// </summary>
        /// <returns></returns>
        private char Peek()
        {
            return this.Peek(0);
        }

        /// <summary>
        /// Returns the character at the specified position.
        /// </summary>
        /// <param name="ahead">Position to read relative
        /// to the current position.</param>
        /// <returns></returns>
        private char Peek(int ahead)
        {
            int pos = (this._pos + ahead);
            if (pos < 0 || pos >= this._text.Length)
                return NullChar;
            return this._text[pos];
        }

        /// <summary>
        /// Indicates if the specified character occurs within
        /// the specified string.
        /// </summary>
        /// <param name="c">Character to find</param>
        /// <param name="chars">String to search</param>
        /// <returns></returns>
        private bool IsOneOf(char c, string chars)
        {
            return (chars.IndexOf(c) != -1);
        }

        /// <summary>
        /// Normalizes the given string by removing characters
        /// that are not letters and converting the result to
        /// upper case.
        /// </summary>
        /// <param name="text">Text to be normalized</param>
        /// <returns></returns>
        private string Normalize(string text)
        {
            StringBuilder builder = new StringBuilder();
            foreach (char c in text)
            {
                if (Char.IsLetter(c))
                    builder.Append(Char.ToUpper(c));
            }
            return builder.ToString();
        }
    }
}