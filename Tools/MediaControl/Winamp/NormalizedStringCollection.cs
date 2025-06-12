namespace MediaControl.Winamp
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using Durandal.Common.Logger;
    using Durandal.Common.Utils;
    using Durandal.Common.File;
    using Durandal.Common.NLP;

    /// <summary>
    /// Given a collection of strings (for example, a list of music artists),
    /// this class attempts to find similar values and create mappings to normalize those values.
    /// For example, if a music library contained 20 entries for "Herb Alpert" and one entry
    /// for "herb albert", the similarity will be identified, and "herb albert" will be mapped
    /// into the normalized form "Herb Alpert".
    /// </summary>
    public class NormalizedStringCollection
    {
        private readonly IDictionary<string, string> _mappings;
        private readonly HashSet<string> _normalizedValues;
        private readonly ILogger _logger;
        private readonly IFileSystem _fileManager;

        /// <summary>
        /// Creates new normalization mappings from the given indexed data set.
        /// </summary>
        /// <param name="data">The raw data. These values should be valid keys in the index given index</param>
        /// <param name="index">The index containing the actual strings</param>
        /// <param name="comparison">The method of comparison to use (metaphone, etc.)</param>
        /// <param name="similarityThreshold">The sensitivity of the mapping. Higher numbers will mean that more different things will be conglomerated together into one normalization mapping.</param>
        public NormalizedStringCollection(IEnumerable<string> data,
            NLPTools.EditDistanceComparer comparison,
            float similarityThreshold,
            ILogger logger,
            IFileSystem fileManager,
            VirtualPath cacheFileName)
        {
            _logger = logger;
            _mappings = new Dictionary<string, string>();
            _normalizedValues = new HashSet<string>();
            _fileManager = fileManager;
            if (!ReadCache(cacheFileName))
            {
                Initialize(data, comparison, similarityThreshold);
                WriteCache(cacheFileName);
            }
        }

        private bool ReadCache(VirtualPath cacheFileName)
        {
            if (!_fileManager.Exists(cacheFileName))
            {
                return false;
            }

            // Read the cache data
            using (StreamReader cacheIn = new StreamReader(_fileManager.OpenStream(cacheFileName, FileOpenMode.Open, FileAccessMode.Read)))
            {
                IDictionary<string, string> cachedMappings = new Dictionary<string, string>();
                HashSet<string> cachedEntries = new HashSet<string>();
                int numMappings;
                string mappingCountString = cacheIn.ReadLine();
                if (mappingCountString == null || !int.TryParse(mappingCountString, out numMappings))
                {
                    return false;
                }
                for (int c = 0; c < numMappings; c++)
                {
                    string one = cacheIn.ReadLine();
                    string two = cacheIn.ReadLine();
                    if (one != null && two != null)
                    {
                        //if (!_index.Contains(one))
                        //{
                        //    _logger.Log("The mapping " + one + " was not found in the normalized string cache " + cacheFileName + ". Rebuilding cache...", LogLevel.Wrn);
                        //    return false;
                        //}
                        //if (!_index.Contains(two))
                        //{
                        //    _logger.Log("The mapping " + two + " was not found in the normalized string cache " + cacheFileName + ". Rebuilding cache...", LogLevel.Wrn);
                        //    return false;
                        //}
                        cachedMappings[one] = two;
                    }
                }
                int numEntries;
                string entryCountString = cacheIn.ReadLine();
                if (entryCountString == null || !int.TryParse(entryCountString, out numEntries))
                {
                    return false;
                }
                for (int c = 0; c < numEntries; c++)
                {
                    string one = cacheIn.ReadLine();
                    if (one != null)
                    {
                        cachedEntries.Add(one);
                        //if (!_index.Contains(one))
                        //{
                        //    _logger.Log("The entry " + one + " was not found in the normalized string cache " + cacheFileName + ". Rebuilding cache...", LogLevel.Wrn);
                        //    return false;
                        //}
                    }
                }
                cacheIn.Close();

                // The cache is good. Copy all the information over.
                foreach (KeyValuePair<string, string> mapping in cachedMappings)
                {
                    _mappings[mapping.Key] = mapping.Value;
                }
                foreach (string entry in cachedEntries)
                {
                    _normalizedValues.Add(entry);
                }
                _logger.Log("Loaded string normalizers from cache " + cacheFileName, LogLevel.Vrb);
            }
            
            return true;
        }

        private void WriteCache(VirtualPath cacheFileName)
        {
            using (StreamWriter fileOut = new StreamWriter(_fileManager.OpenStream(cacheFileName, FileOpenMode.Create, FileAccessMode.Write)))
            {
                fileOut.WriteLine(_mappings.Count);
                foreach (KeyValuePair<string, string> val in _mappings)
                {
                    fileOut.WriteLine(val.Key);
                    fileOut.WriteLine(val.Value);
                }
                fileOut.WriteLine(_normalizedValues.Count);
                foreach (string val in _normalizedValues)
                {
                    string value = val;
                    if (!string.IsNullOrEmpty(value))
                    {
                        fileOut.WriteLine(value);
                    }
                }
                fileOut.Close();
            }
        }

        private void Initialize(IEnumerable<string> data,
            NLPTools.EditDistanceComparer comparison,
            float similarityThreshold)
        {
            // Put all the strings into a set
            IDictionary<string, int> counts = new Dictionary<string, int>();
            foreach (string strKey in data)
            {
                if (!_normalizedValues.Contains(strKey))
                    _normalizedValues.Add(strKey);
                if (!counts.ContainsKey(strKey))
                    counts[strKey] = 1;
                else
                    counts[strKey] += 1;
            }
            HashSet<string> nonNormalizedValues = new HashSet<string>();
            // Find similar strings and add them to the mapping
            foreach (string one in _normalizedValues)
            {
                foreach (string two in _normalizedValues)
                {
                    if (counts[one] < counts[two])
                    {
                        string a = one;
                        string b = two;
                        // Only compare the strings if their lengths are similar
                        if (b.Length > 0 && Math.Abs(((float)a.Length / b.Length) - 1f) <= 0.5f)
                        {
                            float divergence = comparison(a, b);
                            if (divergence < similarityThreshold)
                            {
                                _mappings[one] = two;
                                nonNormalizedValues.Add(one);
                                //if (divergence > 0)
                                //    Console.WriteLine("Normalizing " + index.Retrieve(one) + " into " + index.Retrieve(two) + "(divergence=" + divergence + ")");
                            }
                        }
                    }
                }
            }
            foreach (string key in nonNormalizedValues)
            {
                _normalizedValues.Remove(key);
            }
        }

        /// <summary>
        /// Retrieves the normalized form of the given string
        /// </summary>
        /// <param name="key"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public string Get(string key)
        {
            if (_mappings.ContainsKey(key))
                return _mappings[key];
            // Use the identity mapping by default
            return key;
        }

        public IEnumerable<string> GetNormalizedValues()
        {
            return _normalizedValues;
        }
    }
}
