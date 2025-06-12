using Durandal.API;
using Durandal.Common.Cache;
using Durandal.Common.Collections;
using Durandal.Common.File;
using Durandal.Common.Logger;
using Durandal.Common.NLP;
using Durandal.Common.NLP.Alignment;
using Durandal.Common.NLP.ApproxString;
using Durandal.Common.NLP.Language;
using Durandal.Common.NLP.Language.English;
using Durandal.Common.Statistics;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Durandal.Common.Dialog.Services
{
    /// <summary>
    /// v2 implementation of entity resolver using lexical (spoken or written) signals
    /// </summary>
    public class GenericEntityResolver
    {
        private static readonly VirtualPath VIRTUAL_CACHE_FILE_NAME = new VirtualPath("cache.dat");
        private static readonly Hypothesis<int>.DescendingComparator HYP_SORTER = new Hypothesis<int>.DescendingComparator();

        // Used to compare hyps that are "close" to each other
        private const float ARBITRARY_CONF = 0.15f;

        private readonly ICache<byte[]> _featureIndexCache;
        private readonly INLPToolsCollection _nlTools;
        
        public GenericEntityResolver(INLPToolsCollection nlTools, ICache<byte[]> featureIndexCache = null)
        {
            _nlTools = nlTools;
            _featureIndexCache = featureIndexCache;
        }

        /// <summary>
        /// Attempts to match a user's input to a set of possible named entity candidates. The input is generally
        /// assumed to be speech. The actual mechanism for the resolution is decided by the runtime.
        /// </summary>
        /// <param name="input">The user's input</param>
        /// <param name="possibleValues">A list of all possible values to be selected against</param>
        /// <param name="locale">The current locale</param>
        /// <param name="queryLogger">A logger</param>
        /// <returns>A set of selection hypotheses</returns>
        public async Task<IList<Hypothesis<int>>> ResolveEntity(LexicalString input, IList<LexicalNamedEntity> possibleValues, LanguageCode locale, ILogger queryLogger)
        {
            queryLogger.Log("Attempting to resolve the input \"" + input.ToString() + "\" against " + possibleValues.Count + " possibilities", privacyClass: DataPrivacyClassification.PrivateContent);
            if (possibleValues.Count == 0)
            {
                return new List<Hypothesis<int>>();
            }
            else if (possibleValues.Count < 20)
            {
                // If there are relatively few values, use naive comparison
                NLPTools nlTools;
                if (!string.IsNullOrEmpty(input.SpokenForm) &&
                    _nlTools != null &&
                    _nlTools.TryGetNLPTools(locale, out nlTools) &&
                    nlTools.Pronouncer != null &&
                    nlTools.WordBreaker != null)
                {
                    // Use pronouncer if available, and if the input was spoken
                    queryLogger.Log("Using non-indexed pronunciation resolver path", LogLevel.Vrb);
                    return ResolveEntityByPronunciation(input, possibleValues, nlTools.Pronouncer, nlTools.WordBreaker, locale, queryLogger);
                }
                else
                {
                    // Or just fall back to raw edit distance
                    queryLogger.Log("Using non-indexed edit distance resolver path", LogLevel.Vrb);
                    return ResolveEntityByEditDistance(input, possibleValues);
                }
            }
            else
            {
                // Build a string index and do feature-based matching for large sets
                queryLogger.Log("Using indexed resolver path", LogLevel.Vrb);
                //NLPTools.EditDistanceComparer comparer = null;
                //if (_nlTools != null &&
                //    _nlTools.ContainsKey(locale) &&
                //    _nlTools[locale].Pronouncer != null &&
                //    _nlTools[locale].WordBreaker != null)
                //{
                //    queryLogger.Log("Using indexed pronunciation resolver path", LogLevel.Vrb);
                //    comparer = new EditDistancePronunciation(_nlTools[locale].Pronouncer, _nlTools[locale].WordBreaker, locale).Calculate;
                //}
                //else
                //{
                //    queryLogger.Log("Using indexed edit distance resolver path", LogLevel.Vrb);
                //}

                return await ResolveEntityByStringIndex(input, possibleValues, queryLogger, locale).ConfigureAwait(false);
            }
        }

        private IList<Hypothesis<int>> ResolveEntityByPronunciation(
            LexicalString input,
            IList<LexicalNamedEntity> possibleValues,
            IPronouncer pronouncer,
            IWordBreaker wordBreaker,
            LanguageCode locale,
            ILogger queryLogger)
        {
            string pronOne = string.IsNullOrEmpty(input.SpokenForm) ? pronouncer.PronouncePhraseAsString(wordBreaker.Break(input.WrittenForm).Words) : input.SpokenForm;

            // Create a fast counter of the best confidence for each hypothesis
            FastConcurrentDictionary<int, float> bestConfidences = new FastConcurrentDictionary<int, float>(possibleValues.Count);

            LexicalString bestMatchString = input;
            float bestEditDist = 100000f;
            foreach (LexicalNamedEntity possible in possibleValues)
            {
                foreach (LexicalString knownAs in possible.KnownAs)
                {
                    string pronTwo = string.IsNullOrEmpty(knownAs.SpokenForm) ? pronouncer.PronouncePhraseAsString(wordBreaker.Break(knownAs.WrittenForm).Words) : knownAs.SpokenForm;
                    float combinedDist = InternationalPhoneticAlphabet.EditDistance(pronOne, pronTwo, locale);
                    if (combinedDist > bestEditDist + ARBITRARY_CONF)
                    {
                        // Distance is far below best edit dist. Don't even consider this hyp.
                    }
                    else
                    {
                        float newValue;
                        if (combinedDist == bestEditDist)
                        {
                            // If pronunciations are the same, fall back to actual spelling edit distance
                            float gold = StringUtils.NormalizedEditDistance(input.WrittenForm, bestMatchString.WrittenForm);
                            float test = StringUtils.NormalizedEditDistance(input.WrittenForm, knownAs.WrittenForm);
                            if (test < gold)
                            {
                                bestEditDist = combinedDist;
                                bestMatchString = knownAs;
                            }

                            newValue = Math.Max(0, 1f - ((test + combinedDist) / 2));
                        }
                        else if (combinedDist < bestEditDist)
                        {
                            bestEditDist = combinedDist;
                            bestMatchString = knownAs;
                            newValue = Math.Max(0, 1f - combinedDist);
                        }
                        else
                        {
                            newValue = Math.Max(0, 1f - combinedDist); 
                        }

                        if (newValue > 0.001f)
                        {
                            float existingValue;
                            if (bestConfidences.TryGetValue(possible.Ordinal, out existingValue))
                            {
                                if (newValue > existingValue)
                                {
                                    bestConfidences[possible.Ordinal] = newValue;
                                }
                            }
                            else
                            {
                                bestConfidences[possible.Ordinal] = newValue;
                            }
                        }
                    }
                }
            }

            // Put the deduplicated hypotheses into a list and then sort them in order
            List<Hypothesis<int>> returnVal = new List<Hypothesis<int>>();
            foreach (var kvp in bestConfidences)
            {
                returnVal.Add(new Hypothesis<int>(kvp.Key, kvp.Value));
            }

            returnVal.Sort(HYP_SORTER);
            return returnVal;
        }

        private static IList<Hypothesis<int>> ResolveEntityByEditDistance(LexicalString input, IList<LexicalNamedEntity> possibleValues)
        {
            // Create a fast counter of the best confidence for each hypothesis
            FastConcurrentDictionary<int, float> bestConfidences = new FastConcurrentDictionary<int, float>(possibleValues.Count);

            float bestEditDist = 100000f;
            foreach (LexicalNamedEntity possible in possibleValues)
            {
                foreach (LexicalString knownAs in possible.KnownAs)
                {
                    float combinedDist = StringUtils.NormalizedEditDistance(input.WrittenForm, knownAs.WrittenForm);
                    if (combinedDist > bestEditDist + ARBITRARY_CONF)
                    {
                        // Distance is far below best edit dist. Don't even consider this hyp.
                    }
                    else
                    {
                        if (combinedDist < bestEditDist)
                        {
                            bestEditDist = combinedDist;
                        }

                        float newValue = Math.Max(0, 1f - combinedDist);
                        if (newValue > 0.001f)
                        {
                            float existingValue;
                            if (bestConfidences.TryGetValue(possible.Ordinal, out existingValue))
                            {
                                if (newValue > existingValue)
                                {
                                    bestConfidences[possible.Ordinal] = newValue;
                                }
                            }
                            else
                            {
                                bestConfidences[possible.Ordinal] = newValue;
                            }
                        }
                    }
                }
            }

            // Put the deduplicated hypotheses into a list and then sort them in order
            List<Hypothesis<int>> returnVal = new List<Hypothesis<int>>();
            foreach (var kvp in bestConfidences)
            {
                returnVal.Add(new Hypothesis<int>(kvp.Key, kvp.Value));
            }

            returnVal.Sort(HYP_SORTER);
            return returnVal;
        }

        private async Task<IList<Hypothesis<int>>> ResolveEntityByStringIndex(
            LexicalString input,
            IList<LexicalNamedEntity> possibleValues,
            ILogger queryLogger,
            LanguageCode locale)
        {
            ValueStopwatch timer = ValueStopwatch.StartNew();
            IApproxStringFeatureExtractor featureExtractor = new EnglishNgramApproxStringFeatureExtractor();
            ILexicalMatcher index = null;
            Dictionary<LexicalString, HashSet<int>> backMapping = new Dictionary<LexicalString, HashSet<int>>();

            int featureHash = 0;

            foreach (LexicalNamedEntity entity in possibleValues)
            {
                foreach (LexicalString knownAs in entity.KnownAs)
                {
                    featureHash ^= knownAs.GetHashCode();

                    HashSet<int> set;

                    if (!backMapping.ContainsKey(knownAs))
                    {
                        set = new HashSet<int>();
                        backMapping[knownAs] = set;
                    }
                    else
                    {
                        set = backMapping[knownAs];
                    }

                    if (!set.Contains(entity.Ordinal))
                    {
                        set.Add(entity.Ordinal);
                    }
                }
            }
            timer.Stop();
            queryLogger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                "Time to prepare inputs was {0}", timer.ElapsedMillisecondsPrecise());
            timer.Restart();

            // Check the local cache for cached indexes
            RetrieveResult<byte[]> existingCache;
            if (_featureIndexCache != null)
            {
                existingCache = await _featureIndexCache.TryRetrieve(featureHash.ToString(), queryLogger, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
            }
            else
            {
                existingCache = new RetrieveResult<byte[]>();
            }

            timer.Stop();
            queryLogger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                "Time to fetch cache was {0}", timer.ElapsedMillisecondsPrecise());
            timer.Restart();

            if (existingCache.Success)
            {
                // Use a precached index if available
                InMemoryFileSystem fakeResources = new InMemoryFileSystem();
                fakeResources.AddFile(VIRTUAL_CACHE_FILE_NAME, existingCache.Result);
                index = await ApproxStringMatchingIndex.Deserialize(fakeResources, VIRTUAL_CACHE_FILE_NAME, featureExtractor, queryLogger, locale).ConfigureAwait(false);
                queryLogger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                    "Index cache hit; cache size is  {0}", existingCache.Result.Length);
            }
            else
            {
                // Or create one on the fly (much slower, obviously)
                index = new ApproxStringMatchingIndex(featureExtractor, locale, queryLogger);

                foreach (LexicalNamedEntity entity in possibleValues)
                {
                    index.Index(entity.KnownAs);
                }

                if (_featureIndexCache != null)
                {
                    // Write the index back to cache
                    InMemoryFileSystem fakeFilesystem = new InMemoryFileSystem();
                    await index.Serialize(fakeFilesystem, VIRTUAL_CACHE_FILE_NAME);
                    byte[] newCache = fakeFilesystem.GetFile(VIRTUAL_CACHE_FILE_NAME).ToArray(); // OPT it would be nice to remove this ToArray allocation...
                    await _featureIndexCache.Store(featureHash.ToString(), newCache, null, TimeSpan.FromSeconds(15), true, queryLogger, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    queryLogger.Log("Index cache miss", LogLevel.Vrb);
                }
            }

            timer.Stop();
            queryLogger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                "Time to prepare cache was {0}", timer.ElapsedMillisecondsPrecise());

            timer.Restart();
            // Now match the knownas string
            IList<Hypothesis<LexicalString>> hyps = index.Match(input);
            timer.Stop();
            queryLogger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                "Time to match index was {0}", timer.ElapsedMillisecondsPrecise());

            // And map that back to an object (or multiple objects, if several objects are associated with the matched string)
            List<Hypothesis<int>> returnVal = new List<Hypothesis<int>>();
            ISet<int> objectsAlreadyResolved = new HashSet<int>(); // used to deduplicate results
            foreach (Hypothesis<LexicalString> hyp in hyps)
            {
                if (backMapping.ContainsKey(hyp.Value))
                {
                    foreach (int entity in backMapping[hyp.Value])
                    {
                        if (!objectsAlreadyResolved.Contains(entity))
                        {
                            returnVal.Add(new Hypothesis<int>(entity, hyp.Conf));

                            // mark this specific entity as having a hypothesis, to deduplicate results
                            // this assumes that the list of incoming hyps is in sorted descending order so that the most confident hypothesis for a given entity is always kept
                            objectsAlreadyResolved.Add(entity);
                        }
                    }
                }
            }

            return returnVal;
        }
    }
}
