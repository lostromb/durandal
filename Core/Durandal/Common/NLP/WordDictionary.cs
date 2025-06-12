using System.Collections.Generic;
using System.IO;
using Durandal.API;

namespace Durandal.Common.NLP
{
    using Durandal.Common.Logger;
    using Durandal.Common.File;

    public class WordDictionary
    {
        // Don't store words; only store hashes of words. This reduces memory usage by about 90%
        private ISet<int> knownWords = new HashSet<int>();
        private bool caseSensitive = false;
        private bool useNormalization = false;

        public WordDictionary(VirtualPath dictFileName, IFileSystem fileSystem, bool isCaseSensitive, bool usesNormalization, ILogger logger)
        {
            caseSensitive = isCaseSensitive;
            useNormalization = usesNormalization;
            logger.Log("Loading dictionary from " + dictFileName + "... ");
            if (!fileSystem.Exists(dictFileName))
            {
                logger.Log("Dictionary file " + dictFileName + " not found!", LogLevel.Wrn);
                return;
            }

            using (StreamReader fileIn = new StreamReader(fileSystem.OpenStream(dictFileName, FileOpenMode.Open, FileAccessMode.Read)))
            {
                while (!fileIn.EndOfStream)
                {
                    string nextWord = fileIn.ReadLine();
                    if (string.IsNullOrWhiteSpace(nextWord))
                        continue;
                    if (caseSensitive)
                        knownWords.Add(nextWord.GetHashCode());
                    else
                        knownWords.Add(nextWord.ToLowerInvariant().GetHashCode());
                }

                //fileIn.Close();
            }

            logger.Log(knownWords.Count + " words loaded into dictionary");
        }

        public long GetMemoryUse()
        {
            return knownWords.Count * 4L;
        }

        public bool IsKnown(string word)
        {
            if (!caseSensitive)
                word = word.ToLowerInvariant();

            if (knownWords.Contains(word.GetHashCode()))
                return true;

            string testVal = word;
            
            // TODO: Yeah, make this language-independent
            if (useNormalization)
            {
                // "Fairies" => "Fairy"
                if (word.EndsWith("ies"))
                {
                    testVal = word.Substring(0, word.Length - 3) + "y";
                    if (knownWords.Contains(testVal.GetHashCode()))
                        return true;
                }
                // "Running" => "Run"
                if (word.EndsWith("ning"))
                {
                    testVal = word.Substring(0, word.Length - 4);
                    if (knownWords.Contains(testVal.GetHashCode()))
                        return true;
                }
                // "Rowing" => "Row"
                if (word.EndsWith("ing"))
                {
                    testVal = word.Substring(0, word.Length - 3);
                    if (knownWords.Contains(testVal.GetHashCode()))
                        return true;
                }
                // "Beaches" => "Beach"
                if (word.EndsWith("es"))
                {
                    testVal = word.Substring(0, word.Length - 2);
                    if (knownWords.Contains(testVal.GetHashCode()))
                        return true;
                }
                // "Lizards" => "Lizard"
                // "Used" => "Use"
                // "Liberian" => "Liberia"
                if (word.EndsWith("s") || word.EndsWith("ed") || word.EndsWith("an"))
                {
                    testVal = word.Substring(0, word.Length - 1);
                    if (knownWords.Contains(testVal.GetHashCode()))
                        return true;
                }
            }
            return false;
        }
    }
}
