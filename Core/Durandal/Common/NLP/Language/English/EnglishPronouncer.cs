namespace Durandal.Common.NLP.Language.English
{
    using Durandal.Common.File;
    using Durandal.Common.Logger;
    using Durandal.Common.Collections.Indexing;
    using Durandal.Common.Utils;
    using Durandal.Common.IO;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using System.Threading;
    using Durandal.Common.ServiceMgmt;
    using Durandal.Common.Collections;

    /// <summary>
    /// This class provides helper methods to pronounce arbitrary English words as IPA strings.
    /// </summary>
    public class EnglishPronouncer : IPronouncer
    {
        private ICompactIndex<string> _stringIndex = new BlockTransformCompactIndex<string>(new StringByteConverter(), new LZ4CompressedMemoryPageStorage(false, 10), 1024, 5);
        private IFileSystem _fileSystem;
        private VirtualPath _trainingFile;
        private VirtualPath _cacheFile;
        private Dictionary<Compact<string>, Compact<string>> _contextPronunciationDict;
        private Dictionary<Compact<string>, Compact<string>> _pronunciationDict;
        private Dictionary<Compact<string>, Syllable> _syllableDict;
        private Dictionary<string, string> _exactMatchDictionary; // TODO: This dictionary uses 15 megabytes to store 3.5 mb of data; why?
        private ILogger _logger;
        private int _disposed = 0;

        /// <summary>
        /// Async constructor-initializer pattern
        /// </summary>
        /// <param name="trainingFileName"></param>
        /// <param name="cacheFileName"></param>
        /// <param name="logger"></param>
        /// <param name="fileSystem"></param>
        private EnglishPronouncer(VirtualPath trainingFileName, VirtualPath cacheFileName, ILogger logger, IFileSystem fileSystem)
        {
            _logger = logger;
            _fileSystem = fileSystem;
            _trainingFile = trainingFileName;
            _cacheFile = cacheFileName;
            _contextPronunciationDict = new Dictionary<Compact<string>, Compact<string>>();
            _pronunciationDict = new Dictionary<Compact<string>, Compact<string>>();
            _syllableDict = new Dictionary<Compact<string>, Syllable>();
            _exactMatchDictionary = new Dictionary<string, string>();
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~EnglishPronouncer()
        {
            Dispose(false);
        }
#endif

        private async Task Initialize()
        {
            bool cacheOk = false;
            try
            {
                cacheOk = _cacheFile != null &&
                    (await _fileSystem.ExistsAsync(_cacheFile).ConfigureAwait(false)) && 
                    (await ReadCache(_cacheFile, _trainingFile).ConfigureAwait(false));
            }
            catch (Exception e)
            {
                _logger.Log("Some error occurred while loading the pronunciation cache! Invalidating and rebuilding...", LogLevel.Wrn);
                _logger.Log(e, LogLevel.Wrn);
            }

            if (!cacheOk)
            {
                if (!(await _fileSystem.ExistsAsync(_trainingFile).ConfigureAwait(false)))
                {
                    _logger.Log("Pronouncer training file " + _trainingFile + " not found!", LogLevel.Err);
                }
                else
                {
                    try
                    {
                        await TrainFromFile(_trainingFile).ConfigureAwait(false);
                        await WriteCache(_cacheFile).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        _logger.Log("Some error occurred while training the pronouncer!", LogLevel.Err);
                        _logger.Log(e, LogLevel.Err);
                    }
                }
            }
        }

        public static async Task<EnglishPronouncer> Create(VirtualPath trainingFileName, VirtualPath cacheFileName, ILogger logger, IFileSystem fileSystem)
        {
            EnglishPronouncer returnVal = new EnglishPronouncer(trainingFileName, cacheFileName, logger, fileSystem);
            await returnVal.Initialize().ConfigureAwait(false);
            return returnVal;
        }

        public long GetMemoryUse()
        {
            long returnVal = _stringIndex == null ? 0 : _stringIndex.MemoryUse;
            returnVal += _contextPronunciationDict.Count * 8L;
            returnVal += _pronunciationDict.Count * 8L;
            foreach (var syllable in _syllableDict)
            {
                returnVal += 4L;

                returnVal += syllable.Value.GetMemoryUse();
            }
            foreach (var match in _exactMatchDictionary)
            {
                returnVal += Encoding.UTF8.GetByteCount(match.Key);
                returnVal += Encoding.UTF8.GetByteCount(match.Value);
            }

            return returnVal;
        }

        
        public string PronouncePhraseAsString(IEnumerable<string> phrase)
        {
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                StringBuilder returnVal = pooledSb.Builder;
                foreach (string word in phrase)
                {
                    Syllable[] syllables = PronounceAsSyllables(word);
                    foreach (Syllable s in syllables)
                    {
                        returnVal.Append(s.PhonemeString);
                    }
                }

                return returnVal.ToString();
            }
        }

        public string PronounceAsString(string word)
        {
            Syllable[] syllables = PronounceAsSyllables(word);

            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                StringBuilder returnVal = pooledSb.Builder;
                foreach (Syllable s in syllables)
                {
                    returnVal.Append(s.PhonemeString);
                }

                return returnVal.ToString();
            }
        }

        public Syllable[] PronounceAsSyllables(string word)
        {
            List<Syllable> returnVal = new List<Syllable>();
            string normalizedInput = word.ToLowerInvariant();
            // Check for exact matches
            if (/*_stringIndex.Contains(normalizedInput) && */_exactMatchDictionary.ContainsKey(normalizedInput))
            {
                string pron = _exactMatchDictionary[normalizedInput];
                string[] parts = pron.Split(' ');
                List<string> phonemes = new List<string>();
                for (int c = 1; c < parts.Length; c++)
                {
                    if (!string.IsNullOrWhiteSpace(parts[c]))
                    {
                        phonemes.Add(parts[c]);
                    }
                }

                List<Syllable> syllables = BreakIntoSyllables(word, phonemes.ToArray());
                return syllables.ToArray();
            }

            int curIndex = 0;
            string lastPhoneme = "STKN";
            while (curIndex < normalizedInput.Length)
            {
                // Try all substrings, using both context and context-free spellings
                int nextIndex;
                int nextContextIndex;
                string nextSyllable = FindNextLargestSubstring(normalizedInput, curIndex, string.Empty, _pronunciationDict, out nextIndex);
                string nextContextSyllable = FindNextLargestSubstring(normalizedInput, curIndex, lastPhoneme, _contextPronunciationDict, out nextContextIndex);

                Compact<string> phonemeString = _stringIndex.GetIndex(string.Empty);
                string nextSub;

                if (nextSyllable.Length > nextContextSyllable.Length)
                {
                    nextSub = nextSyllable;
                    Compact<string> idx = _stringIndex.GetIndex(nextSub);
                    if (_pronunciationDict.ContainsKey(idx))
                        phonemeString = _pronunciationDict[idx];
                    curIndex = nextIndex;
                }
                else
                {
                    nextSub = nextContextSyllable;
                    Compact<string> idx = _stringIndex.GetIndex(lastPhoneme + nextSub);
                    if (_contextPronunciationDict.ContainsKey(idx))
                        phonemeString = _contextPronunciationDict[idx];
                    curIndex = nextContextIndex;
                }

                if (_syllableDict.ContainsKey(phonemeString))
                {
                    Syllable actualSyllable = _syllableDict[phonemeString];
                    returnVal.Add(new Syllable()
                        {
                            Spelling = nextSyllable,
                            Phonemes = actualSyllable.Phonemes,
                            PrevPhoneme = actualSyllable.PrevPhoneme
                        });
                    lastPhoneme = actualSyllable.Phonemes[actualSyllable.PhonemeCount - 1];
                }
                else
                {
                    curIndex += 1;
                }
            }
            return returnVal.ToArray();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                _stringIndex?.Dispose();
            }
        }

        private Compact<string>[] StoreMultiple(string value, ICompactIndex<string> index)
        {
            string[] parts = value.Split(' ');
            Compact<string>[] returnVal = new Compact<string>[parts.Length];
            for (int c = 0; c < parts.Length; c++)
            {
                returnVal[c] = index.Store(parts[c]);
            }

            return returnVal;
        }

        private string RetrieveMultiple(Compact<string>[] value, ICompactIndex<string> index)
        {
            string[] parts = new string[value.Length];
            for (int c = 0; c < parts.Length; c++)
            {
                parts[c] = index.Retrieve(value[c]);
            }

            return string.Join(" ", parts);
        }

        private async Task<bool> ReadCache(VirtualPath cacheFileName, VirtualPath trainingFileName)
        {
            using (StreamReader fileIn = new StreamReader(await _fileSystem.OpenStreamAsync(trainingFileName, FileOpenMode.Open, FileAccessMode.Read).ConfigureAwait(false)))
            {
                while (!fileIn.EndOfStream)
                {
                    string nextLine = fileIn.ReadLine();

                    if (nextLine == null)
                        break;
                    string[] parts = nextLine.Split(' ');
                    if (parts.Length == 1)
                        continue;
                    string word = parts[0].ToLowerInvariant();
                    if (!word.EndsWith(")")) // Don't store alternates in the exact match dictionary
                    {
                        _exactMatchDictionary[word] = nextLine.Substring(parts[0].Length + 1);
                    }
                }

                fileIn.Dispose();
            }

            using (StreamReader fileIn = new StreamReader(await _fileSystem.OpenStreamAsync(cacheFileName, FileOpenMode.Open, FileAccessMode.Read).ConfigureAwait(false)))
            {
                int dictCount = int.Parse(fileIn.ReadLine());
                for (int c = 0; c < dictCount; c++)
                {
                    string key = fileIn.ReadLine();
                    _pronunciationDict[_stringIndex.Store(key)] = _stringIndex.Store(fileIn.ReadLine());
                }

                int contextDictCount = int.Parse(fileIn.ReadLine());
                for (int c = 0; c < contextDictCount; c++)
                {
                    string key = fileIn.ReadLine();
                    _contextPronunciationDict[_stringIndex.Store(key)] = _stringIndex.Store(fileIn.ReadLine());
                }

                int syllableDictCount = int.Parse(fileIn.ReadLine());
                for (int c = 0; c < syllableDictCount; c++)
                {
                    string key = fileIn.ReadLine();
                    string spelling = fileIn.ReadLine();
                    string phonemes = fileIn.ReadLine();
                    if (string.IsNullOrEmpty(spelling) || string.IsNullOrEmpty(phonemes))
                    {
                        return false;
                    }

                    Syllable newSyllable = new Syllable();
                    newSyllable.Spelling = spelling;
                    newSyllable.Phonemes = phonemes.Split(' ');
                    _syllableDict[_stringIndex.Store(key)] = newSyllable;
                }

                fileIn.Dispose();
                _logger.Log("Loaded cached pronunciation data");
            }

            return true;
        }

        private async Task WriteCache(VirtualPath cacheFileName)
        {
            if (cacheFileName == null)
            {
                return;
            }

            if (await _fileSystem.ExistsAsync(cacheFileName).ConfigureAwait(false))
            {
                await _fileSystem.DeleteAsync(cacheFileName).ConfigureAwait(false);
            }

            using (StreamWriter fileOut = new StreamWriter(await _fileSystem.OpenStreamAsync(cacheFileName, FileOpenMode.Create, FileAccessMode.Write).ConfigureAwait(false)))
            {
                fileOut.WriteLine(_pronunciationDict.Count);
                foreach (Compact<string> key in _pronunciationDict.Keys)
                {
                    fileOut.WriteLine(_stringIndex.Retrieve(key));
                    fileOut.WriteLine(_stringIndex.Retrieve(_pronunciationDict[key]));
                }

                fileOut.WriteLine(_contextPronunciationDict.Count);
                foreach (Compact<string> key in _contextPronunciationDict.Keys)
                {
                    fileOut.WriteLine(_stringIndex.Retrieve(key));
                    fileOut.WriteLine(_stringIndex.Retrieve(_contextPronunciationDict[key]));
                }

                fileOut.WriteLine(_syllableDict.Count);
                foreach (Compact<string> key in _syllableDict.Keys)
                {
                    fileOut.WriteLine(_stringIndex.Retrieve(key));
                    fileOut.WriteLine(_syllableDict[key].Spelling);
                    fileOut.WriteLine(_syllableDict[key].PhonemeStringSeparated);
                }

                fileOut.Dispose();
            }
        }

        private async Task TrainFromFile(VirtualPath trainingFileName)
        {
            StatisticalMapping<Compact<string>> syllableSpellings = new StatisticalMapping<Compact<string>>();
            StatisticalMapping<Compact<string>> syllableSpellingsWithPhoneme = new StatisticalMapping<Compact<string>>();
            using (StreamReader fileIn = new StreamReader(await _fileSystem.OpenStreamAsync(trainingFileName, FileOpenMode.Open, FileAccessMode.Read).ConfigureAwait(false)))
            {
                while (!fileIn.EndOfStream)
                {
                    string nextLine = fileIn.ReadLine();
                    if (nextLine == null)
                    {
                        break;
                    }

                    string[] parts = nextLine.Split(' ');
                    if (parts.Length == 1)
                    {
                        continue;
                    }

                    List<string> phonemes = new List<string>();
                    string word = parts[0].ToLowerInvariant();
                    if (word.EndsWith(")")) // Trim "(1)" alternates
                    {
                        word = word.Substring(0, word.Length - 3);
                    }

                    for (int c = 1; c < parts.Length; c++)
                    {
                        if (!string.IsNullOrWhiteSpace(parts[c]))
                        {
                            phonemes.Add(parts[c]);
                        }
                    }

                    List<Syllable> syllables = BreakIntoSyllables(word, phonemes.ToArray());
                    if (!word.EndsWith(")") && !_exactMatchDictionary.ContainsKey(word)) // Don't store alternates in the exact match dictionary
                    {
                        _exactMatchDictionary[word] = nextLine.Substring(parts[0].Length + 1);
                    }

                    foreach (Syllable x in syllables)
                    {
                        syllableSpellings.Increment(_stringIndex.Store(x.Spelling), _stringIndex.Store(x.PhonemeString));
                        syllableSpellingsWithPhoneme.Increment(_stringIndex.Store(x.ContextSpelling), _stringIndex.Store(x.PhonemeString));
                        Compact<string> key = _stringIndex.Store(x.PhonemeString);
                        if (!_syllableDict.ContainsKey(key))
                        {
                            _syllableDict[key] = x;
                        }
                    }
                }

                fileIn.Dispose();
            }

            foreach (Compact<string> phrase in syllableSpellings.GetItems())
            {
                _pronunciationDict[phrase] = syllableSpellings.GetMostLikelyOutputFor(phrase);
            }

            foreach (Compact<string> phrase in syllableSpellingsWithPhoneme.GetItems())
            {
                _contextPronunciationDict[phrase] = syllableSpellingsWithPhoneme.GetMostLikelyOutputFor(phrase);
            }

            Compact<string> emptyIndex = _stringIndex.GetIndex(string.Empty);
            if (_pronunciationDict.ContainsKey(emptyIndex))
            {
                _pronunciationDict.Remove(emptyIndex);
            }
            if (_contextPronunciationDict.ContainsKey(emptyIndex))
            {
                _contextPronunciationDict.Remove(emptyIndex);
            }

            _logger.Log("Trained prounouncer using " + trainingFileName);
            _logger.Log(_exactMatchDictionary.Count + " known words", LogLevel.Vrb);
            _logger.Log(_syllableDict.Count + " syllables", LogLevel.Vrb);
            _logger.Log(_pronunciationDict.Count + " context-sensitive pronunciations", LogLevel.Vrb);
            _logger.Log(_contextPronunciationDict.Count + " context-free pronunciations", LogLevel.Vrb);
        }


        private string FindNextLargestSubstring(string input, int index, string prefix, Dictionary<Compact<string>, Compact<string>> dict, out int nextStartIndex)
        {
            int curIndex = index;
            nextStartIndex = curIndex;
            int length = input.Length - curIndex;
            while (curIndex < input.Length)
            {
                while (length > 0)
                {
                    nextStartIndex = curIndex + length;
                    string sub = input.Substring(curIndex, length);
                    //Console.WriteLine("Testing " + sub);
                    Compact<string> key = _stringIndex.GetIndex(prefix + sub);
                    if (key != _stringIndex.GetNullIndex() && dict.ContainsKey(key))
                        return sub;
                    length--;
                }

                curIndex += 1;
                nextStartIndex = curIndex;
                length = input.Length - curIndex;
            }

            return string.Empty;
        }

        private List<Syllable> BreakIntoSyllables(string word, string[] phonemes)
        {
            List<Syllable> returnVal = new List<Syllable>();
            int[] syllableIndex = new int[phonemes.Length];
            int numSyllables = 0;

            // Find the number of syllables in the sentence
            for (int c = 0; c < phonemes.Length; c++)
            {
                if (phonemes[c].EndsWith("0") ||
                    phonemes[c].EndsWith("1") ||
                    phonemes[c].EndsWith("2"))
                {
                    phonemes[c] = phonemes[c].TrimEnd('0', '1', '2');
                    syllableIndex[c] = numSyllables++;
                }
                else
                {
                    syllableIndex[c] = -1;
                }
            }

            // Catch utterances that don't specify a stress; assume there is only 1 syllable
            if (numSyllables == 0)
            {
                numSyllables = 1;
            }

            // Assign phonemes to syllables
            int[] syllableLengths = new int[numSyllables];
            int[] syllableStarts = new int[numSyllables];
            // Rule 1: The phoneme immediately after a stress goes to the previous syllable
            for (int c = phonemes.Length - 1; c > 0; c--)
            {
                if (syllableIndex[c] == -1)
                {
                    syllableIndex[c] = syllableIndex[c - 1];
                }
            }
            // Rule 2: The phoneme immediately before a stress, if unassigned, goes to the next syllable
            for (int c = 0; c < phonemes.Length - 1; c++)
            {
                if (syllableIndex[c] == -1)
                {
                    syllableIndex[c] = syllableIndex[c + 1];
                }
            }
            // Rule 3: All remaining syllables go to the nearest previous syllable
            int nextSyllable = 0;
            for (int c = 0; c < phonemes.Length; c++)
            {
                if (syllableIndex[c] == -1)
                {
                    syllableIndex[c] = nextSyllable;
                }
                else if (nextSyllable != syllableIndex[c])
                {
                    nextSyllable = syllableIndex[c];
                    syllableStarts[nextSyllable] = c;
                }

                syllableLengths[nextSyllable]++;
            }

            // Build syllables from phonemes
            for (int c = 0; c < numSyllables; c++ )
            {
                Syllable newSyllable = new Syllable();
                newSyllable.Phonemes = new string[syllableLengths[c]];
                ArrayExtensions.MemCopy(phonemes, syllableStarts[c], newSyllable.Phonemes, 0, syllableLengths[c]);
                newSyllable.PrevPhoneme = syllableStarts[c] > 0 ? phonemes[syllableStarts[c] - 1] : "STKN";

                // Attempt to derive the spelling of the syllable within the actual input text
                float stringLength = word.Length;
                float phonemeLength = (float)phonemes.Length;
                int startIndex = (int)((float)syllableStarts[c] * stringLength / phonemeLength);
                int endIndex = (int)((float)(syllableStarts[c] + syllableLengths[c]) * stringLength / phonemeLength);
                if (startIndex <= 0)
                {
                    startIndex = 0;
                }
                else if (endIndex == startIndex)
                {
                    endIndex = startIndex + 1;
                }

                if (endIndex > word.Length)
                {
                    endIndex = word.Length - 1;
                }
                else if (endIndex == startIndex)
                {
                    startIndex = endIndex - 1;
                }

                if (startIndex <= 0)
                {
                    startIndex = 0;
                }

                if (endIndex > word.Length)
                {
                    endIndex = word.Length;
                }

                newSyllable.Spelling = word.Substring(startIndex, endIndex - startIndex);

                returnVal.Add(newSyllable);
            }

            return returnVal;
        }
    }
}
