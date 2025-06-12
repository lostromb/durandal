namespace Durandal.Common.NLP
{
    using System.Text;
    using Utils;

    /// <summary>
    /// Represents a single unit of pronunciation, composed of one or more phonemes
    /// </summary>
    public class Syllable
    {
        public string Spelling { get; set; }
        public string[] Phonemes { get; set; }
        public string PrevPhoneme { get; set; }

        private string _cachedPhonemeString;

        public string ContextSpelling
        {
            get
            {
                return PrevPhoneme + Spelling;
            }
        }

        public string PhonemeString
        {
            get
            {
                if (string.IsNullOrEmpty(_cachedPhonemeString))
                {
                    using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
                    {
                        foreach (string phon in Phonemes)
                        {
                            pooledSb.Builder.Append(phon);
                        }

                        _cachedPhonemeString = pooledSb.Builder.ToString();
                    }
                }

                return _cachedPhonemeString;
            }
        }

        public string PhonemeStringSeparated
        {
            get
            {
                using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
                {
                    StringBuilder returnVal = pooledSb.Builder;
                    for (int c = 0; c < Phonemes.Length; c++)
                    {
                        returnVal.Append(Phonemes[c]);
                        if (c != Phonemes.Length - 1)
                        {
                            returnVal.Append(" ");
                        }
                    }

                    return returnVal.ToString();
                }
            }
        }

        public int PhonemeCount
        {
            get
            {
                return Phonemes.Length;
            }
        }

        public override string ToString()
        {
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                StringBuilder returnVal = pooledSb.Builder;
                returnVal.Append(PrevPhoneme + " - ");
                foreach (string phon in Phonemes)
                {
                    returnVal.Append(phon);
                }

                return returnVal.ToString();
            }
        }

        public long GetMemoryUse()
        {
            long returnVal = 0;
            if (Spelling != null)
                returnVal += Encoding.UTF8.GetByteCount(Spelling);
            if (PrevPhoneme != null)
                returnVal += Encoding.UTF8.GetByteCount(PrevPhoneme);
            foreach (string Phoneme in Phonemes)
            {
                returnVal += Encoding.UTF8.GetByteCount(Phoneme);
            }

            return returnVal;
        }
    }
}
