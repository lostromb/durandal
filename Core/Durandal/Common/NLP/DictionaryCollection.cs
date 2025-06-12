using System.Collections.Generic;

namespace Durandal.Common.NLP
{
    using System.Text;

    using Durandal.Common.Logger;
    using Durandal.Common.File;
    using Durandal.Common.NLP.Language;

    public class DictionaryCollection
    {
        private Dictionary<string, WordDictionary> dictionaries;
        private ILogger _logger;
        private readonly IFileSystem _fileSystem;

        public DictionaryCollection(ILogger logger, IFileSystem fileSystem)
        {
            _logger = logger;
            _fileSystem = fileSystem;
            dictionaries = new Dictionary<string, WordDictionary>();
        }

        public void Load(string dictName, LanguageCode locale, VirtualPath sourceFileName, bool caseSensitive, bool useNormalization)
        {
            WordDictionary newDict = new WordDictionary(sourceFileName, _fileSystem, caseSensitive, useNormalization, _logger);
            dictionaries[dictName] = newDict;
        }

        public long GetMemoryUse()
        {
            long returnVal = 0;
            foreach (var s in dictionaries)
            {
                returnVal += (Encoding.UTF8.GetByteCount(s.Key)) + s.Value.GetMemoryUse();
            }
            return returnVal;
        }

        /// <summary>
        /// TODO: Optimize this method
        /// </summary>
        /// <param name="input"></param>
        /// <param name="dictName"></param>
        /// <returns></returns>
        public bool IsA(string input, string dictName)
        {
            if (dictionaries.ContainsKey(dictName))
            {
                return dictionaries[dictName].IsKnown(input);
            }
            return false;
        }
    }
}
