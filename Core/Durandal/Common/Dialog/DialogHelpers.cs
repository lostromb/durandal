
namespace Durandal.Common.Dialog
{
    using Common.Utils;
    using Durandal.API;
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Various utility methods used to process slot values or recognition results.
    /// </summary>
    public static class DialogHelpers
    {
        /// <summary>
        /// Looks in a conversation history and attempts to pull the most recent slot string value with the given slot name from a session.
        /// If the slot is not found, this returns string.Empty.
        /// If multiple slots exist with the same name in the same turn, this will return the value of the first one that is found.
        /// </summary>
        /// <param name="results"></param>
        /// <param name="tagName"></param>
        /// <returns></returns>
        public static string TryGetAnySlotValue(QueryWithContext results, string tagName)
        {
            // Look in the current turn
            string returnVal = TryGetSlotValue(results.Understanding, tagName);
            if (!string.IsNullOrEmpty(returnVal))
            {
                return returnVal;
            }

            // Look in past turns
            for (int index = results.PastTurns.Count - 1; index >= 0; index--)
            {
                returnVal = TryGetSlotValue(results.PastTurns[index].MostLikelyTags, tagName);
                if (!string.IsNullOrEmpty(returnVal))
                    return returnVal;
            }

            return returnVal;
        }

        /// <summary>
        /// Looks in a conversation history and attempts to pull the most recent slot string value with the given slot name from a session.
        /// If the slot is not found, this returns string.Empty.
        /// If multiple slots exist with the same name in the same turn, this will return the value of the first one that is found.
        /// </summary>
        /// <param name="results"></param>
        /// <param name="tagName"></param>
        /// <returns></returns>
        public static string TryGetAnySlotValue(IList<RecoResult> results, string tagName)
        {
            string returnVal = null;
            for (int index = results.Count - 1; index >= 0; index--)
            {
                returnVal = TryGetSlotValue(results[index].MostLikelyTags, tagName);
                if (!string.IsNullOrEmpty(returnVal))
                    return returnVal;
            }

            return returnVal;
        }

        /// <summary>
        /// Looks in a conversation history and attempts to pull the most recent structured slot with the given slot name from a session.
        /// If the slot is not found, this returns null.
        /// If multiple slots exist with the same name in the same turn, this will return the value of the first one that is found.
        /// </summary>
        /// <param name="results"></param>
        /// <param name="tagName"></param>
        /// <returns></returns>
        public static SlotValue TryGetAnySlot(QueryWithContext results, string tagName)
        {
            // Look in the current turn
            SlotValue returnVal = TryGetSlot(results.Understanding, tagName);
            if (returnVal != null)
            {
                return returnVal;
            }

            // Look in past turns
            for (int index = results.PastTurns.Count - 1; index >= 0; index--)
            {
                returnVal = TryGetSlot(results.PastTurns[index].MostLikelyTags, tagName);
                if (returnVal != null)
                    return returnVal;
            }

            return returnVal;
        }

        /// <summary>
        /// Looks in a conversation history and attempts to pull the most recent structured slot with the given slot name from a session.
        /// If the slot is not found, this returns null.
        /// If multiple slots exist with the same name in the same turn, this will return the value of the first one that is found.
        /// </summary>
        /// <param name="results"></param>
        /// <param name="tagName"></param>
        /// <returns></returns>
        public static SlotValue TryGetAnySlot(IList<RecoResult> results, string tagName)
        {
            SlotValue returnVal = null;
            for (int index = results.Count - 1; index >= 0; index--)
            {
                returnVal = TryGetSlot(results[index].MostLikelyTags, tagName);
                if (returnVal != null)
                    return returnVal;
            }

            return returnVal;
        }

        /// <summary>
        /// Looks in a single conversation turn reco results and attempts to pull the slot string value with the given slot name from the most likely tag hypothesis.
        /// If the slot is not found, this returns string.Empty.
        /// </summary>
        /// <param name="result"></param>
        /// <param name="tagName"></param>
        /// <returns></returns>
        public static string TryGetSlotValue(RecoResult result, string tagName)
        {
            return TryGetSlotValue(result.MostLikelyTags, tagName);
        }

        /// <summary>
        /// Looks in a single conversation turn reco results and attempts to pull the slot string value with the given slot name.
        /// If the slot is not found, this returns string.Empty.
        /// If multiple slots exist with the same name, this will return the value of the first one that is found.
        /// </summary>
        /// <param name="result"></param>
        /// <param name="tagName"></param>
        /// <returns></returns>
        public static string TryGetSlotValue(TaggedData result, string tagName)
        {
            SlotValue val = TryGetSlot(result, tagName);
            return (val == null) ? string.Empty : val.Value;
        }

        /// <summary>
        /// Looks in a single conversation turn reco results and attempts to pull the slot lexical value with the given slot name from the most likely tag hypothesis.
        /// If the slot is not found, this returns null.
        /// </summary>
        /// <param name="result"></param>
        /// <param name="tagName"></param>
        /// <returns></returns>
        public static LexicalString TryGetLexicalSlotValue(RecoResult result, string tagName)
        {
            SlotValue slot = TryGetSlot(result.MostLikelyTags, tagName);
            if (slot == null)
            {
                return null;
            }

            return new LexicalString(slot.Value, string.IsNullOrEmpty(slot.LexicalForm) ? null : slot.LexicalForm);
        }

        /// <summary>
        /// Looks in a single conversation turn reco results and attempts to pull a structured slot with the given slot name from the most likely tag hypothesis.
        /// If the slot is not found, this returns null.
        /// </summary>
        /// <param name="result"></param>
        /// <param name="tagName"></param>
        /// <returns></returns>
        public static SlotValue TryGetSlot(RecoResult result, string tagName)
        {
            return TryGetSlot(result.MostLikelyTags, tagName);
        }

        /// <summary>
        /// Looks in a single conversation turn reco results and attempts to pull all structured slots with the given slot name from the most likely tag hypothesis.
        /// </summary>
        /// <param name="result"></param>
        /// <param name="tagName"></param>
        /// <returns></returns>
        public static IEnumerable<SlotValue> TryGetSlots(RecoResult result, string tagName)
        {
            return TryGetSlots(result.MostLikelyTags, tagName);
        }

        /// <summary>
        /// Looks at a single tag hypothesis and attempts to pull the structured slot value with the given slot name.
        /// If the slot is not found, this returns null.
        /// If multiple slots exist with the same name, this will return the first one that is found.
        /// </summary>
        /// <param name="result"></param>
        /// <param name="tagName"></param>
        /// <returns></returns>
        public static SlotValue TryGetSlot(TaggedData result, string tagName)
        {
            return TryGetSlot(result.Slots, tagName);
        }

        /// <summary>
        /// Looks at a TaggedData object and enumerates all of the slots (zero or more) with the given name.
        /// </summary>
        /// <param name="result"></param>
        /// <param name="tagName"></param>
        /// <returns></returns>
        public static IEnumerable<SlotValue> TryGetSlots(TaggedData result, string tagName)
        {
            return TryGetSlots(result.Slots, tagName);
        }

        /// <summary>
        /// Looks at a list of slot values and attempts to pull the structured slot value with the given slot name.
        /// If the slot is not found, this returns null.
        /// If multiple slots exist with the same name, this will return the first one that is found.
        /// </summary>
        /// <param name="slots"></param>
        /// <param name="tagName"></param>
        /// <returns></returns>
        public static SlotValue TryGetSlot(IList<SlotValue> slots, string tagName)
        {
            foreach (SlotValue tag in slots)
            {
                if (string.Equals(tag.Name, tagName, StringComparison.Ordinal))
                {
                    return tag;
                }
            }

            return null;
        }

        /// <summary>
        /// Looks at a list of slot values and enumerates all of the slots (zero or more) with the given name.
        /// </summary>
        /// <param name="slots"></param>
        /// <param name="tagName"></param>
        /// <returns></returns>
        public static IEnumerable<SlotValue> TryGetSlots(IList<SlotValue> slots, string tagName)
        {
            foreach (SlotValue tag in slots)
            {
                if (string.Equals(tag.Name, tagName, StringComparison.Ordinal))
                {
                    yield return tag;
                }
            }
        }

        //<summary>
        //Helper function to coerce a phonetic slot value(like "on R") into an expected
        //value("Omar") from a list of possibilities.This can be used to disambiguate
        //contact names, places, song titles, or any other entity data for which the catalog
        //is based mostly on client-side context.
        //</summary>
        //<param name = "actual" ></ param >
        //< param name= "possibleValues" ></ param >
        //< returns > A list of possibilities</returns>
        //public static string RewriteSlotValue(string input, IEnumerable<string> possibleValues, IPronouncer pronouncer, out float confidence)
        //{
        //    string pronOne = pronouncer.PronouncePhraseAsString(input.Split(' '));
        //    string bestMatch = input;
        //    float bestEditDist = 3f;
        //    foreach (string possible in possibleValues)
        //    {
        //        string pronTwo = pronouncer.PronouncePhraseAsString(possible.Split(' '));
        //        float combinedDist = StringUtils.NormalizedEditDistance(pronOne, pronTwo);
        //        //+ NormalizedEditDistance(actual, possible)) / 2f;
        //        if (combinedDist == bestEditDist)
        //        {
        //            // If pronunciations are the same, fall back to actual spelling edit distance
        //            float gold = StringUtils.NormalizedEditDistance(input, bestMatch);
        //            float test = StringUtils.NormalizedEditDistance(input, possible);
        //            if (test < gold)
        //            {
        //                bestEditDist = combinedDist;
        //                bestMatch = possible;
        //            }
        //        }
        //        else if (combinedDist < bestEditDist)
        //        {
        //            bestEditDist = combinedDist;
        //            bestMatch = possible;
        //        }
        //    }
        //    confidence = 1f - bestEditDist;
        //    return bestMatch;
        //}

        //public static string RewriteSlotValue(string actual, IEnumerable<string> possibleValues, NLPTools.EditDistanceComparer comparisonMetric, out float confidence)
        //{
        //    string bestMatch = actual;
        //    float bestEditDist = 3f;
        //    foreach (string possible in possibleValues)
        //    {
        //        float combinedDist = comparisonMetric(actual, possible);
        //        if (combinedDist < bestEditDist)
        //        {
        //            bestEditDist = combinedDist;
        //            bestMatch = possible;
        //        }
        //    }
        //    confidence = 1f - bestEditDist;
        //    return bestMatch;
        //}

        //public static IList<Hypothesis<string>> RewriteSlotValueNBest(string actual, IEnumerable<string> possibleValues, int n, IPronouncer pronouncer, float minConfidence = 0)
        //{
        //    List<Hypothesis<string>> hypotheses = new List<Hypothesis<string>>();
        //    string pronOne = pronouncer.PronouncePhraseAsString(actual.Split(' '));
        //    foreach (string possible in possibleValues)
        //    {
        //        string pronTwo = pronouncer.PronouncePhraseAsString(possible.Split(' '));
        //        float confidence = 1 - StringUtils.NormalizedEditDistance(pronOne, pronTwo);
        //        if (confidence >= minConfidence)
        //        {
        //            Hypothesis<string> a = new Hypothesis<string>(possible, confidence);
        //            hypotheses.Add(a);
        //            // Cull some intermediate results to improve performance (mostly memory)
        //            if (hypotheses.Count > 50)
        //            {
        //                hypotheses.Sort(new Hypothesis<string>.DescendingComparator());
        //                hypotheses.RemoveRange(n, hypotheses.Count - n);
        //            }
        //        }
        //    }
        //    hypotheses.Sort(new Hypothesis<string>.DescendingComparator());
        //    hypotheses.RemoveRange(n, hypotheses.Count - n);
        //    return hypotheses;
        //}

        //public static IList<Hypothesis<string>> RewriteSlotValueNBest(string actual, IEnumerable<string> possibleValues, int n, NLPTools.EditDistanceComparer comparisonMetric, float minConfidence = 0)
        //{
        //    List<Hypothesis<string>> hypotheses = new List<Hypothesis<string>>();
        //    foreach (string possible in possibleValues)
        //    {
        //        float confidence = 1 - comparisonMetric(actual, possible);
        //        if (confidence >= minConfidence)
        //        {
        //            Hypothesis<string> a = new Hypothesis<string>(possible, confidence);
        //            hypotheses.Add(a);
        //            // Cull some intermediate results to improve performance (mostly memory)
        //            if (hypotheses.Count > 50)
        //            {
        //                hypotheses.Sort(new Hypothesis<string>.DescendingComparator());
        //                hypotheses.RemoveRange(n, hypotheses.Count - n);
        //            }
        //        }
        //    }
        //    hypotheses.Sort(new Hypothesis<string>.DescendingComparator());
        //    hypotheses.RemoveRange(n, hypotheses.Count - n);
        //    return hypotheses;
        //}

        /// <summary>
        /// Converts a TaggedData object back into a plain string, reversing canonicalization and applying slot augmentation as necessary
        /// </summary>
        /// <param name="input">The tagged sentence to be converted</param>
        /// <returns>A plain string representing the augmented utterance</returns>
        public static string ConvertTaggedDataToAugmentedQuery(TaggedData input)
        {
            if (input == null)
                return string.Empty;

            string originalUtterance = input.Utterance;

            // Look for slots that have an AugmentedValue field and sort them in sentence order
            List<SlotValue> sortedSlots = new List<SlotValue>();
            foreach (SlotValue slot in input.Slots)
            {
                if (!slot.Annotations.ContainsKey(SlotPropertyName.AugmentedValue))
                    continue;

                string originalValue = slot.Value;
                if (slot.Annotations.ContainsKey(SlotPropertyName.NonCanonicalValue))
                {
                    originalValue = slot.Annotations[SlotPropertyName.NonCanonicalValue];
                }
                
                // Recalculate start indices and length if necessary
                if (!slot.Annotations.ContainsKey(SlotPropertyName.StartIndex))
                {
                    slot.Annotations[SlotPropertyName.StartIndex] = Math.Max(0, originalUtterance.IndexOf(originalValue, StringComparison.Ordinal)).ToString();
                }

                if (!slot.Annotations.ContainsKey(SlotPropertyName.StringLength))
                {
                    slot.Annotations[SlotPropertyName.StringLength] = originalValue.Length.ToString();
                }

                sortedSlots.Add(slot);
            }

            // Sort
            sortedSlots.Sort((SlotValue a, SlotValue b) =>
                {
                    int aIdx = int.Parse(a.Annotations[SlotPropertyName.StartIndex]);
                    int bIdx = int.Parse(b.Annotations[SlotPropertyName.StartIndex]);
                    return Math.Sign(aIdx - bIdx);
                });

            // Now reconstruct the final augmented utterance
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                StringBuilder finalUtterance = pooledSb.Builder;
                int curStringIndex = 0;
                int slotIndex = 0;
                while (curStringIndex < originalUtterance.Length)
                {
                    // See if there's a slot coming up
                    if (slotIndex < sortedSlots.Count)
                    {
                        // Is there any more text to capture before then?
                        int slotStart = int.Parse(sortedSlots[slotIndex].Annotations[SlotPropertyName.StartIndex]);
                        int dist = slotStart - curStringIndex;
                        if (dist > 0)
                        {
                            // Capture that text preceeding the slot
                            finalUtterance.Append(originalUtterance.Substring(curStringIndex, dist));
                            curStringIndex += dist;
                        }
                        else
                        {
                            // Capture the slot's augmented value
                            int slotLength = int.Parse(sortedSlots[slotIndex].Annotations[SlotPropertyName.StringLength]);
                            finalUtterance.Append(sortedSlots[slotIndex].Annotations[SlotPropertyName.AugmentedValue]);
                            curStringIndex += slotLength;
                            // And move on to the next slot
                            slotIndex++;
                        }
                    }
                    else
                    {
                        // No more slots, capture the rest of the string
                        finalUtterance.Append(originalUtterance.Substring(curStringIndex));
                        curStringIndex = originalUtterance.Length;
                    }
                }

                return finalUtterance.ToString();
            }
        }

        /// <summary>
        /// If you want to return an augmented query that is just a plain string (i.e. you want to overwrite it entirely),
        /// then this method will build the proper return value for you.
        /// </summary>
        /// <param name="input">The raw augmented query to return</param>
        /// <returns>A TaggedData object representing this query</returns>
        public static TaggedData BuildTaggedDataFromPlainString(string input)
        {
            return new TaggedData()
            {
                Utterance = input,
                Annotations = new Dictionary<string, string>(),
                Confidence = 1.0f,
                Slots = new List<SlotValue>()
            };
        }
        
        /// <summary>
        /// Assuming that this (the current answer) was invoked by a crossdomain request from another answer, and the caller
        /// provided callback info, this function will build an invokable dialog action that will actually trigger that
        /// callback, and return to the caller a list of slots that are output from the current domain.
        /// Note that the slots you specify here will be changed to "{domain}.{slotname}" when they reach the caller,
        /// to prevent namespaces from clashing.
        /// </summary>
        /// <param name="queryWithContext"></param>
        /// <param name="returnValSlots"></param>
        /// <returns></returns>
        public static DialogAction BuildCallbackAction(QueryWithContext queryWithContext, List<SlotValue> returnValSlots)
        {
            string callbackDomain = TryGetAnySlotValue(queryWithContext, DialogConstants.CALLBACK_DOMAIN_SLOT_NAME);
            string callbackIntent = TryGetAnySlotValue(queryWithContext, DialogConstants.CALLBACK_INTENT_SLOT_NAME);

            return new DialogAction()
            {
                Domain = callbackDomain,
                Intent = callbackIntent,
                Slots = returnValSlots
            };
        }
    }
}
