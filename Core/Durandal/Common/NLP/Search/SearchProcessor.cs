using Durandal.Common.NLP.Language.English;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.NLP.Search
{
    public static class SearchProcessor
    {
        private const int MAX_QUERY_LENGTH = 20000;

        public static string Normalize(string query)
        {
            string txt = query.ToLower();
            if (txt.Length > MAX_QUERY_LENGTH) txt = txt.Substring(0, MAX_QUERY_LENGTH);

            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                StringBuilder sb = pooledSb.Builder;
                for (int i = 0; i < txt.Length; i++)// (char c in txt)
                {
                    if (txt[i] == '\'' || txt[i] == '$' || txt[i] == '\n' || txt[i] == '\t' || txt[i] == '\r') continue;
                    else if ((txt[i] == '+' && i == 0) || (txt[i] == '+' && i > 0 && txt[i - 1] == ' ')) continue;
                    else if (txt[i] == ' ') sb.Append(" ");
                    else if (txt[i] == '.' && i > 0 && txt[i - 1] == 'n') sb.Append(".");
                    else if (char.IsPunctuation(txt[i])) sb.Append(" ");
                    else if (txt[i] < 128) sb.Append(txt[i]);
                }

                txt = sb.ToString();
                while (txt.IndexOf("  ") >= 0)
                {
                    txt = txt.Replace("  ", " ");
                }

                return txt.Trim();
            }
        }

        internal static string NormalizeUnigram(bool stem, string query)
        {
            string txt = query.ToLower();
            if (txt.Length > MAX_QUERY_LENGTH) txt = txt.Substring(0, MAX_QUERY_LENGTH);
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                StringBuilder sb = pooledSb.Builder;
                for (int i = 0; i < txt.Length; i++)// (char c in txt)
                {
                    if (txt[i] == '\'' || txt[i] == '$' || txt[i] == '\n' || txt[i] == '\t' || txt[i] == '\r') continue;
                    else if ((txt[i] == '+' && i == 0) || (txt[i] == '+' && i > 0 && txt[i - 1] == ' ')) continue;
                    else if (txt[i] == ' ') sb.Append(" ");
                    else if (txt[i] == '.' && i > 0 && txt[i - 1] == 'n') sb.Append(".");
                    else if (char.IsPunctuation(txt[i])) sb.Append(" ");
                    else if (txt[i] < 128) sb.Append(txt[i]);
                }
                txt = sb.ToString();

                while (txt.IndexOf("  ") >= 0)
                {
                    txt = txt.Replace("  ", " ");
                }

                if (stem)
                {
                    txt = txt.Trim();
                    EnglishStemmer stemmer = new EnglishStemmer();
                    foreach (var c in txt) stemmer.add(c);
                    stemmer.stem();
                    txt = stemmer.ToString();
                    return txt;
                }
                else
                {
                    return txt.Trim();
                }
            }
        }

        internal static void AddNgrams(string input, int min, int max, Dictionary<string, float> words, float weight, bool stemWords)
        {
            string[] inputParts1 = input.ToLower().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> inputParts3 = new List<string>();

            //make the unigrams stemmed
            foreach (var s in inputParts1)
            {
                if (stemWords)
                {
                    EnglishStemmer stem = new EnglishStemmer();
                    foreach (var c in s) stem.add(c);
                    stem.stem();
                    string newword = stem.ToString();
                    inputParts3.Add(newword);
                }
                else inputParts3.Add(s);
            }
            string[] inputParts = inputParts3.ToArray();

            // Always add unigrams to list
            for (int i = 0; i < inputParts.Length; i++)
            {
                if (!words.ContainsKey(inputParts[i])) words.Add(inputParts[i], 0);
                words[inputParts[i]] += weight;
            }

            // Add N-Grams
            for (int prefixIdx = 0; prefixIdx < inputParts.Length; ++prefixIdx)
            {
                // Build N-Gram based on current prefix
                string ngram = inputParts[prefixIdx];
                for (int endIdx = prefixIdx + 1; endIdx < inputParts.Length && endIdx < prefixIdx + max; ++endIdx)
                {
                    // Add current N-Gram
                    ngram += " " + inputParts[endIdx];
                    if (!words.ContainsKey(ngram)) words.Add(ngram, 0);
                    words[ngram] += weight;
                }
            }
        }

        internal static HashSet<string> GenerateNgrams(string input, int min, int max, bool stemWords)
        {
            HashSet<string> res = new HashSet<string>();
            string[] inputParts1 = input.ToLower().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> inputParts3 = new List<string>();
            foreach (var s in inputParts1)
            {
                EnglishStemmer stem = new EnglishStemmer();
                foreach (var c in s) stem.add(c);
                stem.stem();
                string newword = stem.ToString();
                inputParts3.Add(newword);
            }

            string[] inputParts = inputParts3.ToArray();

            // Always add unigrams to list
            for (int i = 0; i < inputParts.Length; i++)
            {
                res.Add(inputParts[i]);
            }

            // Add N-Grams
            for (int prefixIdx = 0; prefixIdx < inputParts.Length; ++prefixIdx)
            {
                // Build N-Gram based on current prefix
                string ngram = inputParts[prefixIdx];
                for (int endIdx = prefixIdx + 1; endIdx < inputParts.Length && endIdx < prefixIdx + max; ++endIdx)
                {
                    // Add current N-Gram
                    ngram += " " + inputParts[endIdx];
                    res.Add(ngram);
                }
            }

            return res;
        }
    }
}
