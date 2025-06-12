using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Durandal.API;
using Durandal.Common.NLP;

namespace Durandal.Common.NLP.Tagging
{
    using Durandal.Common.Dialog;
    using System.Text;
    using Utils;

    public static class TaggedDataSplitter
    {
        private static readonly Regex tagMatcher = new Regex("\\[([a-zA-Z0-9_\\.-]+?)\\]([\\w\\W]*?)\\[(/\\1)\\]");
        private static readonly HashSet<string> emptySet = new HashSet<string>();

        public static string StripTags(string taggedString)
        {
            string returnVal = taggedString;
            bool anyMatches = true;
            while (anyMatches)
            {
                anyMatches = false;
                returnVal = tagMatcher.Replace(returnVal, (m) =>
                {
                    if (m.Success && m.Groups[2].Success)
                    {
                        anyMatches = true;
                        return m.Groups[2].Value;
                    }
                    return "ERROR";
                });
            }

            return returnVal;
        }

        public static HashSet<string> ExtractTagNames(string taggedString)
        {
            if (!taggedString.Contains("["))
            {
                return emptySet;
            }

            HashSet<string> returnVal = new HashSet<string>();
            int index = 0;
            Match match = tagMatcher.Match(taggedString, index);
            while (match.Success)
            {
                if (!returnVal.Contains(match.Groups[1].Value))
                {
                    returnVal.Add(match.Groups[1].Value);
                }

                index = match.Index + 1;
                match = tagMatcher.Match(taggedString, index);
            } 

            return returnVal;
        }

        public static TaggedData ParseSlots(string taggedString, IWordBreaker wordBreaker)
        {
            TaggedData returnVal = new TaggedData();
            int realStringIndex = 0;

            // Build an index of tag names and boundaries
            List<Tuple<int, string>> tagBounds = new List<Tuple<int, string>>();
            Match match = tagMatcher.Match(taggedString, realStringIndex);
            while (match.Success)
            {
                tagBounds.Add(new Tuple<int, string>(match.Index, match.Groups[1].Value));
                tagBounds.Add(new Tuple<int, string>(match.Groups[3].Index - 1, match.Groups[1].Value));
                realStringIndex = match.Index + 1;
                match = tagMatcher.Match(taggedString, realStringIndex);
            }
            tagBounds.Sort((a, b) => a.Item1 - b.Item1);

            // Now extract the tags and expose the underlying utterance
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                StringBuilder utterance = pooledSb.Builder;
                IDictionary<string, int> openTags = new Dictionary<string, int>();
                realStringIndex = 0;
                int virtualStringIndex = 0;
                foreach (Tuple<int, string> tagBound in tagBounds)
                {
                    string tagName = tagBound.Item2;
                    int tagStartIndex = tagBound.Item1;

                    if (realStringIndex < tagStartIndex)
                    {
                        utterance.Append(taggedString.Substring(realStringIndex, tagStartIndex - realStringIndex));
                        virtualStringIndex += tagStartIndex - realStringIndex;
                    }

                    if (openTags.ContainsKey(tagName))
                    {
                        // Close a tag
                        int tagContentLength = virtualStringIndex - openTags[tagName];
                        char[] tmp = new char[tagContentLength];
                        utterance.CopyTo(openTags[tagName], tmp, 0, tagContentLength);
                        SlotValue newSlot = new SlotValue(tagName, new string(tmp), SlotValueFormat.Unknown);
                        newSlot.SetProperty(SlotPropertyName.StartIndex, openTags[tagName].ToString());
                        newSlot.SetProperty(SlotPropertyName.StringLength, tagContentLength.ToString());
                        returnVal.Slots.Add(newSlot);
                        openTags.Remove(tagName);
                    }
                    else
                    {
                        // Open a tag
                        openTags.Add(tagName, virtualStringIndex);
                    }

                    realStringIndex = tagStartIndex + tagName.Length + 2;

                    // technically we can't calculate the length of closing tags, so just nudge it if we're on the tag close boundary
                    if (realStringIndex < taggedString.Length && taggedString[realStringIndex] == ']')
                        realStringIndex++;
                }

                if (realStringIndex < taggedString.Length)
                {
                    utterance.Append(taggedString.Substring(realStringIndex));
                }

                returnVal.Utterance = utterance.ToString();
                returnVal.Confidence = 1.0f;
                return returnVal;
            }
        }

        public static TaggedSentence ParseTags(string taggedString, IWordBreaker wordBreaker, bool allowEmptyTags = false)
        {
            // Build an index of tag names and boundaries
            // "Original" index means the index in the original, tagged string
            int realStringIndex = 0;
            List<Tuple<int, string>> tagBoundsOriginalIndex = new List<Tuple<int, string>>();
            Match match = tagMatcher.Match(taggedString, realStringIndex);
            while (match.Success)
            {
                tagBoundsOriginalIndex.Add(new Tuple<int, string>(match.Index, match.Groups[1].Value));
                tagBoundsOriginalIndex.Add(new Tuple<int, string>(match.Groups[3].Index - 1, match.Groups[3].Value));
                realStringIndex = match.Index + 1;
                match = tagMatcher.Match(taggedString, realStringIndex);
            }

            tagBoundsOriginalIndex.Sort((a, b) =>
            {
                return a.Item1 - b.Item1;
            });

            // "Virtual" index means the index in the modified, untagged string
            List<Tuple<int, string>> tagBoundsVirtualIndex = new List<Tuple<int, string>>();
            int curOffset = 0;
            foreach (Tuple<int, string> boundOriginalIndex in tagBoundsOriginalIndex)
            {
                tagBoundsVirtualIndex.Add(new Tuple<int, string>(boundOriginalIndex.Item1 - curOffset, boundOriginalIndex.Item2));
                curOffset += boundOriginalIndex.Item2.Length + 2;
            }

            string untaggedString = StripTags(taggedString);
            Sentence wordBrokenSentence = wordBreaker.Break(untaggedString);
            List<TaggedWord> taggedWords = new List<TaggedWord>();
            foreach (string word in wordBrokenSentence.Words)
            {
                taggedWords.Add(new TaggedWord()
                {
                    Word = word,
                    Tags = new List<string>()
                });
            }

            // Detect tags that span length-0 tokens. These will be inserted explicitly as zero-length words in the sentence.
            HashSet<Tuple<int, string>> emptyTags = new HashSet<Tuple<int, string>>();
            if (allowEmptyTags)
            {
                for (int tagBoundIdx = 0; tagBoundIdx < tagBoundsVirtualIndex.Count - 1; tagBoundIdx++)
                {
                    Tuple<int, string> tag1 = tagBoundsVirtualIndex[tagBoundIdx];
                    for (int tagBoundIdx2 = tagBoundIdx + 1; tagBoundIdx2 < tagBoundsVirtualIndex.Count; tagBoundIdx2++)
                    {
                        Tuple<int, string> tag2 = tagBoundsVirtualIndex[tagBoundIdx2];
                        string tagName1 = tag1.Item2.TrimStart('/');
                        string tagName2 = tag2.Item2.TrimStart('/');
                        if (string.Equals(tagName1, tagName2) && tag1.Item1 == tag2.Item1)
                        {
                            Tuple<int, string> testVal = new Tuple<int, string>(tagBoundsVirtualIndex[tagBoundIdx].Item1, tagName1);
                            if (!emptyTags.Contains(testVal))
                            {
                                emptyTags.Add(testVal);
                            }
                        }
                    }
                }
            }

            // Find out if any tags split any tokens apart. If so, we need to rearrange the sentence a bit
            for (int wordIdx = 0; wordIdx < wordBrokenSentence.Length; wordIdx++)
            {
                foreach (Tuple<int, string> tagBound in tagBoundsVirtualIndex)
                {
                    string tokenVal = wordBrokenSentence.Words[wordIdx];
                    int tokenStart = wordBrokenSentence.Indices[wordIdx];
                    int tokenEnd = tokenStart + tokenVal.Length;

                    if (tagBound.Item1 > tokenStart && tagBound.Item1 < tokenEnd)
                    {
                        // Split the sentence token in-place along the tag bound
                        string splitTokenLeft = tokenVal.Substring(0, tagBound.Item1 - tokenStart);
                        string splitTokenRight = tokenVal.Substring(tagBound.Item1 - tokenStart);
                        wordBrokenSentence.Words[wordIdx] = splitTokenLeft;
                        wordBrokenSentence.Words.Insert(wordIdx + 1, splitTokenRight);
                        taggedWords[wordIdx].Word = splitTokenLeft;
                        taggedWords.Insert(wordIdx + 1, new TaggedWord()
                            {
                                Word = splitTokenRight,
                                Tags = new List<string>(),
                            });
                        wordBrokenSentence.Indices.Insert(wordIdx + 1, tagBound.Item1);
                        wordBrokenSentence.NonTokens.Insert(wordIdx + 1, string.Empty);
                        wordIdx++;
                    }
                }
            }

            // Insert empty tokens wherever an empty string is explicitly tagged
            foreach (Tuple<int, string> emptyTag in emptyTags)
            {
                int emptyTagBound = emptyTag.Item1;
                bool foundInToken = false;
                
                // Find out where this empty tag falls. Either it is within a token or within a non-token
                for (int wordIdx = 0; wordIdx < wordBrokenSentence.Length; wordIdx++)
                {
                    string tokenVal = wordBrokenSentence.Words[wordIdx];
                    int tokenStart = wordBrokenSentence.Indices[wordIdx];
                    int tokenEnd = tokenStart + tokenVal.Length;
                    if (emptyTagBound > tokenStart && emptyTagBound < tokenEnd)
                    {
                        // It's within a token
                        string splitTokenLeft = tokenVal.Substring(0, emptyTagBound - tokenStart);
                        string splitTokenRight = tokenVal.Substring(emptyTagBound - tokenStart);
                        wordBrokenSentence.Words[wordIdx] = splitTokenLeft;
                        wordBrokenSentence.Words.Insert(wordIdx + 1, splitTokenRight);
                        taggedWords[wordIdx].Word = splitTokenLeft;
                        taggedWords.Insert(wordIdx + 1, new TaggedWord()
                            {
                                Word = splitTokenRight,
                                Tags = new List<string>(new string[] { emptyTag.Item2 }), // Set the tag right here,
                            });
                        wordBrokenSentence.Indices.Insert(wordIdx + 1, emptyTagBound);
                        wordBrokenSentence.NonTokens.Insert(wordIdx + 1, string.Empty);
                        foundInToken = true;
                        break;
                    }
                }

                if (!foundInToken)
                {
                    for (int nonTokenIdx = wordBrokenSentence.Length; nonTokenIdx >= 0; nonTokenIdx--) // iterate in reverse so that multiple insertions "stack" in the proper direction
                    {
                        string tokenVal = wordBrokenSentence.NonTokens[nonTokenIdx];
                        int tokenStart = nonTokenIdx == 0 ? 0 : wordBrokenSentence.Indices[nonTokenIdx - 1] + wordBrokenSentence.Words[nonTokenIdx - 1].Length;
                        int tokenEnd = tokenStart + tokenVal.Length;
                        if (emptyTagBound >= tokenStart && emptyTagBound <= tokenEnd)
                        {
                            // It's within a nontoken
                            string splitTokenLeft = tokenVal.Substring(0, emptyTagBound - tokenStart);
                            string splitTokenRight = tokenVal.Substring(emptyTagBound - tokenStart);
                            wordBrokenSentence.NonTokens[nonTokenIdx] = splitTokenLeft;
                            wordBrokenSentence.NonTokens.Insert(nonTokenIdx + 1, splitTokenRight);
                            wordBrokenSentence.Indices.Insert(nonTokenIdx, emptyTagBound);
                            wordBrokenSentence.Words.Insert(nonTokenIdx, string.Empty);
                            taggedWords.Insert(nonTokenIdx, new TaggedWord()
                                {
                                    Word = string.Empty,
                                    Tags = new List<string>(new string[] { emptyTag.Item2 }), // Set the tag right here
                                });
                            break;
                        }
                    }
                }
            }

            TaggedSentence returnVal = new TaggedSentence();
            returnVal.Utterance = wordBrokenSentence;
            returnVal.Words = taggedWords;

            // Now, finally assign tags to each word
            IDictionary<string, int> openTags = new Dictionary<string, int>();
            
            realStringIndex = 0; // "Real" string index is the index in the sentence _including_ tags. So "[tag]test[/tag]" is 16 characters long
            int virtualStringIndex = 0;  // "Virtual" string index is the index in the sentence _excluding_ tags. So "[tag]test[/tag]" is 4 characters long
            foreach (Tuple<int, string> tagBound in tagBoundsOriginalIndex)
            {
                string tagName = tagBound.Item2;
                int tagStartIndex = tagBound.Item1;

                if (realStringIndex < tagStartIndex)
                {
                    virtualStringIndex += tagStartIndex - realStringIndex;
                }

                bool isCloseTag = tagName.StartsWith("/");
                string actualTagName = tagName;
                if (isCloseTag)
                {
                    actualTagName = tagName.Substring(1);
                }

                if (isCloseTag)
                {
                    if (openTags.ContainsKey(actualTagName))
                    {
                        int tagBoundStart = openTags[actualTagName];
                        int tagBoundEnd = virtualStringIndex;
                        if (tagBoundStart == tagBoundEnd)
                        {
                            openTags.Remove(actualTagName);
                            realStringIndex = tagStartIndex + tagName.Length + 2;
                            continue; // If it's a zero-length span then the tag has already been set
                        }

                        // Find the words spanned by this tag
                        bool foundFirstWord = false;
                        for (int wordNum = 0; wordNum < returnVal.Utterance.Words.Count; wordNum++)
                        {
                            int idx = returnVal.Utterance.Indices[wordNum];
                            int wordLength = returnVal.Utterance.Words[wordNum].Length;
                            if (wordLength > 0 && idx >= tagBoundStart && idx + wordLength <= tagBoundEnd)
                            {
                                returnVal.Words[wordNum].Tags.Add(actualTagName);
                                if (!foundFirstWord)
                                {
                                    returnVal.Words[wordNum].StartTags.Add(actualTagName);
                                    foundFirstWord = true;
                                }
                            }
                        }

                        openTags.Remove(actualTagName);
                    }
                    else
                    {
                        throw new FormatException("Opening and closing tags do not match for tag \"" + actualTagName + "\" in sentence " + taggedString);
                    }
                }
                else
                {
                    if (openTags.ContainsKey(actualTagName))
                    {
                        // Same tag opened twice. How do we handle this?
                        int startRegion = Math.Max(realStringIndex - 10, 0);
                        int endRegion = Math.Min(realStringIndex + 50, taggedString.Length);
                        string region = taggedString.Substring(startRegion, endRegion - startRegion);
                        throw new FormatException("Tag cannot be nested with itself. Tag: " + actualTagName + ", Index: " + realStringIndex + ", Context: \"" + region + "\"");
                    }
                    else
                    {
                        openTags.Add(actualTagName, virtualStringIndex);
                    }
                }

                realStringIndex = tagStartIndex + tagName.Length + 2;

                if (realStringIndex < taggedString.Length && taggedString[realStringIndex] == ']')
                    realStringIndex++;
            }

            // Any word that has no tag after all of this gets "O"
            foreach (TaggedWord taggedWord in returnVal.Words)
            {
                if (taggedWord.Tags.Count == 0)
                {
                    taggedWord.Tags.Add("O");
                }
            }

            return returnVal;
        }
    }
}
