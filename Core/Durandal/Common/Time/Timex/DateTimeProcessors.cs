namespace Durandal.Common.Time.Timex
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Globalization;
    using Durandal.Common.Time.Timex.Constants;
    using Durandal.Common.Time.Timex.Enums;
    using System.Diagnostics;
    using System.Threading;

    public static class DateTimeProcessors
    {
        #region Statics
        
        /// <summary>
        /// Used when we want to specify a FlagWeight that should exceed all but the most granular time values
        /// </summary>
        private const float FLAG_WEIGHT_OVERRIDE = 10f;

        private const float MAX_MATCHES_FOR_RANGE = 999;

        /// <summary>
        /// Used when passing around pairs of TimexMatches
        /// </summary>
        private struct MatchPair
        {
            public TimexMatch a;
            public TimexMatch b;
        }

        private static Lazy<IList<Tuple<DateTimeParts, float>>> _flagWeights = new Lazy<IList<Tuple<DateTimeParts, float>>>(InitializeFlagWeights, LazyThreadSafetyMode.PublicationOnly);

        /// <summary>
        /// Initializes the lazy list of DateTimeParts weights, for measuring the specificity of various time expressions
        /// </summary>
        /// <returns>The instantiated value</returns>
        private static IList<Tuple<DateTimeParts, float>> InitializeFlagWeights()
        {
            IList<Tuple<DateTimeParts, float>> returnVal = new List<Tuple<DateTimeParts, float>>();
            returnVal.Add(new Tuple<DateTimeParts, float>(DateTimeParts.Second, 5.0f));
            returnVal.Add(new Tuple<DateTimeParts, float>(DateTimeParts.Minute, 3.0f));
            returnVal.Add(new Tuple<DateTimeParts, float>(DateTimeParts.Hour, 2.0f));
            returnVal.Add(new Tuple<DateTimeParts, float>(DateTimeParts.Day, 1.0f));
            returnVal.Add(new Tuple<DateTimeParts, float>(DateTimeParts.WeekDay, 1.5f));
            returnVal.Add(new Tuple<DateTimeParts, float>(DateTimeParts.Week, 1.0f));
            returnVal.Add(new Tuple<DateTimeParts, float>(DateTimeParts.Month, 3.0f));
            returnVal.Add(new Tuple<DateTimeParts, float>(DateTimeParts.Year, 2.0f));
            returnVal.Add(new Tuple<DateTimeParts, float>(DateTimeParts.Decade, 0.8f));
            returnVal.Add(new Tuple<DateTimeParts, float>(DateTimeParts.Century, 0.5f));
            returnVal.Add(new Tuple<DateTimeParts, float>(DateTimeParts.Millenium, 0.3f));
            returnVal.Add(new Tuple<DateTimeParts, float>(DateTimeParts.DecadeYear, 0.3f));
            returnVal.Add(new Tuple<DateTimeParts, float>(DateTimeParts.Month, 0.5f));
            returnVal.Add(new Tuple<DateTimeParts, float>(DateTimeParts.TimeZone, 4.0f));
            returnVal.Add(new Tuple<DateTimeParts, float>(DateTimeParts.Reference, 1.1f));
            returnVal.Add(new Tuple<DateTimeParts, float>(DateTimeParts.OffsetAnchor, 1.0f));
            returnVal.Add(new Tuple<DateTimeParts, float>(DateTimeParts.PartOfYear, 0.7f));
            returnVal.Add(new Tuple<DateTimeParts, float>(DateTimeParts.Season, 0.6f));
            returnVal.Add(new Tuple<DateTimeParts, float>(DateTimeParts.AmPmUnambiguous, 0.5f));
            returnVal.Add(new Tuple<DateTimeParts, float>(DateTimeParts.WeekOfExpression, 0.3f));

            return returnVal;
        }

        #endregion

        #region Public Methods
        /// <summary>
        /// Determines if two ExtendedDateTime objects can be safely merged into one. This method will test for invalid combinations
        /// like "The 7th" and "Tomorrow", or "next week" and "5:00 PM", and return true if it believes that the two given
        /// times can reasonably be construed to represent different parts of the same overall date/time reference.
        /// </summary>
        /// <param name="firstTime">The first time to compare</param>
        /// <param name="secondTime">The second time to compare</param>
        /// <returns>True if the two times can be merged safely</returns>
        public static bool CanBeMerged(ExtendedDateTime firstTime, ExtendedDateTime secondTime)
        {
            if (firstTime == null || secondTime == null)
                return false;

            // Cannot merge if they are not dates/times
            if (!(firstTime.TemporalType == TemporalType.Date || firstTime.TemporalType == TemporalType.Time) ||
                !(secondTime.TemporalType == TemporalType.Date || secondTime.TemporalType == TemporalType.Time))
            {
                return false;
            }

            // Cannot merge if SetParts intersect (meaning that the same field is specified in both timexes)
            if ((firstTime.ExplicitSetParts & secondTime.ExplicitSetParts) != 0)
            {
                return false;
            }

            DateTimeParts mergedParts = (firstTime.ExplicitSetParts | secondTime.ExplicitSetParts);

            // These flags are incompatible with merged times
            if (mergedParts.HasFlag(DateTimeParts.DecadeYear) ||
                mergedParts.HasFlag(DateTimeParts.Reference))
            {
                return false;
            }

            // We can merge Weekof as long as the result specifies a Day and has no time information
            if (mergedParts.HasFlag(DateTimeParts.WeekOfExpression) &&
                (!mergedParts.HasFlag(DateTimeParts.Day) ||
                mergedParts.HasFlag(DateTimeParts.Hour)))
            {
                return false;
            }

            // Can only merge offset values if at most one of them is an offset type
            if (firstTime.IsOffset() && secondTime.IsOffset())
            {
                return false;
            }

            // Now make sure that the resulting set parts are "valid"
            // meaning: disallowing illegal constructions like year + day of month, day + minute, week + day of month, etc.
            if (mergedParts.HasFlag(DateTimeParts.Year) && !mergedParts.HasFlag(DateTimeParts.Month) && mergedParts.HasFlag(DateTimeParts.Day)) return false;
            if (mergedParts.HasFlag(DateTimeParts.Year) && !mergedParts.HasFlag(DateTimeParts.Week) && mergedParts.HasFlag(DateTimeParts.WeekDay)) return false;
            if (mergedParts.HasFlag(DateTimeParts.Month) && !mergedParts.HasFlag(DateTimeParts.Day) && mergedParts.HasFlag(DateTimeParts.Hour)) return false;
            if (mergedParts.HasFlag(DateTimeParts.Month) && !mergedParts.HasFlag(DateTimeParts.Week) && mergedParts.HasFlag(DateTimeParts.WeekDay)) return false;
            if (mergedParts.HasFlag(DateTimeParts.Week) && !mergedParts.HasFlag(DateTimeParts.WeekDay) && mergedParts.HasFlag(DateTimeParts.Hour)) return false;
            if (mergedParts.HasFlag(DateTimeParts.Day) && !mergedParts.HasFlag(DateTimeParts.Hour) && mergedParts.HasFlag(DateTimeParts.Minute)) return false;
            if (mergedParts.HasFlag(DateTimeParts.WeekDay) && !mergedParts.HasFlag(DateTimeParts.Hour) && mergedParts.HasFlag(DateTimeParts.Minute)) return false;
            if (mergedParts.HasFlag(DateTimeParts.Hour) && !mergedParts.HasFlag(DateTimeParts.Minute) && mergedParts.HasFlag(DateTimeParts.Second)) return false;
            // special: only allow merging part of day if there is either a day or an hour also specified
            if (mergedParts.HasFlag(DateTimeParts.PartOfDay) && !mergedParts.HasFlag(DateTimeParts.Day) && !mergedParts.HasFlag(DateTimeParts.WeekDay) && !mergedParts.HasFlag(DateTimeParts.Hour)) return false;
            // special: don't merge things like "Saturday" and "the 5th"
            if (mergedParts.HasFlag(DateTimeParts.WeekDay) && mergedParts.HasFlag(DateTimeParts.Day)) return false;

            // Offset merging only works in very specific cases, canonically "5 PM" and "Tomorrow", as well as similarly-structured cases
            if (firstTime.OffsetUnit.HasValue || secondTime.OffsetUnit.HasValue)
            {
                ExtendedDateTime offsetTime = firstTime.IsOffset() ? firstTime : secondTime;
                ExtendedDateTime nonOffsetTime = firstTime.IsOffset() ? secondTime : firstTime;

                // Ensure that the offset unit is "one higher" than the next specified unit
                // example: allow "5 PM" to merge with "tomorrow" but not with "next month"
                if (offsetTime.OffsetUnit.Value == TemporalUnit.Year && !mergedParts.HasFlag(DateTimeParts.Month)) return false;
                if (offsetTime.OffsetUnit.Value == TemporalUnit.Year && !mergedParts.HasFlag(DateTimeParts.Week)) return false;
                if (offsetTime.OffsetUnit.Value == TemporalUnit.Month && !mergedParts.HasFlag(DateTimeParts.Day)) return false;
                if (offsetTime.OffsetUnit.Value == TemporalUnit.Day && !mergedParts.HasFlag(DateTimeParts.Hour)) return false;
                if (offsetTime.OffsetUnit.Value == TemporalUnit.Week && !mergedParts.HasFlag(DateTimeParts.WeekDay)) return false;
                if (offsetTime.OffsetUnit.IsWeekday() && !mergedParts.HasFlag(DateTimeParts.Hour)) return false;
                if (offsetTime.OffsetUnit.Value == TemporalUnit.Hour && !mergedParts.HasFlag(DateTimeParts.Minute)) return false;
                if (offsetTime.OffsetUnit.Value == TemporalUnit.Minute && !mergedParts.HasFlag(DateTimeParts.Second)) return false;
                if (offsetTime.OffsetUnit.Value == TemporalUnit.Second) return false;

                // Also ensure that there are no set parts that are "higher" than the offset unit
                // example: forbid merging "tomorrow" and "November"
                if (offsetTime.OffsetUnit.Value == TemporalUnit.Month && ((mergedParts & (DateTimeParts.Year)) != 0)) return false;
                if (offsetTime.OffsetUnit.Value == TemporalUnit.Day && ((mergedParts & (DateTimeParts.Year | DateTimeParts.Month)) != 0)) return false;
                if (offsetTime.OffsetUnit.Value == TemporalUnit.Week && ((mergedParts & (DateTimeParts.Year | DateTimeParts.Month)) != 0)) return false;
                if (offsetTime.OffsetUnit.IsWeekday() && ((mergedParts & (DateTimeParts.Year | DateTimeParts.Month | DateTimeParts.Week)) != 0)) return false;
                if (offsetTime.OffsetUnit.Value == TemporalUnit.Hour && ((mergedParts & (DateTimeParts.Year | DateTimeParts.Month | DateTimeParts.Week | DateTimeParts.Day | DateTimeParts.WeekDay)) != 0)) return false;
                if (offsetTime.OffsetUnit.Value == TemporalUnit.Minute && ((mergedParts & (DateTimeParts.Year | DateTimeParts.Month | DateTimeParts.Week | DateTimeParts.Day | DateTimeParts.WeekDay | DateTimeParts.Hour)) != 0)) return false;
            }

            return true;
        }

        /// <summary>
        /// Returns true if the given ExtendedDateTimes contain dates or times that could represent a range.
        /// For example, "4:00 Tuesday" and "6:00" can be a range, but "tomorrow" and "7:00" cannot.
        /// </summary>
        /// <param name="firstTime">The first time to compare</param>
        /// <param name="secondTime">The second time to compare</param>
        /// <returns>True if these times could be used to define a time range.</returns>
        public static bool CanBeARange(ExtendedDateTime firstTime, ExtendedDateTime secondTime)
        {
            if (firstTime == null || secondTime == null)
                return false;
            return PartsHaveOverlap(firstTime.ExplicitSetParts, secondTime.ExplicitSetParts);
        }

        /// <summary>
        /// Runs the date range inference engine to attempt to extract a valid date time range from an input string.
        /// </summary>
        /// <param name="timex">A Timex object for performing match rules against the input</param>
        /// <param name="inputText">The raw input string</param>
        /// <param name="inputContext">Any additional information that may aid the resolution should be placed here.
        ///     For now, this is limited to part of days hints such as "Morning"</param>
        /// <param name="context">The context to use for timex matching. The Normalization flag of this context
        ///     will be used to determine if the returned range should be in the past or the future</param>
        /// <returns>A DateTimeRange object containing the extracted portions of the date time range. If no valid times were found, this will contain null pointers.
        ///     If only one valid time was found, it will be placed in the startTime field, and endTime will be nullptr. If a complete range was found, startTime
        ///     and endTime will both contain valid ExtendedDateTimes for the match.</returns>
        public static DateTimeRange RunTimeRangeResolution(TimexMatcher timex, string inputText, string inputContext, TimexContext context)
        {
            if (timex == null)
                throw new ArgumentNullException("timex");
            try
            {
                IList<TimexMatch> timexMatches = timex.Matches(inputText, context);

                // Just stop here if no matches were found in the input
                if (timexMatches.Count == 0)
                {
                    return new DateTimeRange();
                }

                // Capture hints from the input context, appending them to the main set of matches if applicable
                IList<TimexMatch> contextMatches = timex.Matches(inputContext, context);

                return RunTimeRangeResolution(timexMatches, contextMatches, context);
            }
#if TIME_RANGES_DEBUG
            catch (TimexException e)
#else
            catch (TimexException)
#endif
            {
                // Catch errors at the top level of the API
#if TIME_RANGES_DEBUG
                Debug.WriteLine("Grammar exception caught while running range inference: " + e.Message);
#endif
                return new DateTimeRange();
            }
        }

        /// <summary>
        /// Runs the date range inference engine to attempt to extract a valid date time range from an input string.
        /// </summary>
        /// <param name="allTimes">A set of ExtendedDateTimes, ordered in their original lexical ordering, to extract a range from</param>
        /// <param name="context">The context to use for timex matching. The Normalization flag of this context
        ///     will be used to determine if the returned range should be in the past or the future</param>
        /// <returns>A DateTimeRange object containing the extracted portions of the date time range. If no valid times were found, this will contain null pointers.
        ///     If only one valid time was found, it will be placed in the startTime field, and endTime will be nullptr. If a complete range was found, startTime
        ///     and endTime will both contain valid ExtendedDateTimes for the match.</returns>
        public static DateTimeRange RunTimeRangeResolution(IList<ExtendedDateTime> allTimes, TimexContext context)
        {
            if (allTimes == null || allTimes.Count == 0)
            {
                return new DateTimeRange();
            }

            IList<TimexMatch> inputList = new List<TimexMatch>();
            foreach (ExtendedDateTime match in allTimes)
            {
                inputList.Add(new TimexMatch(match));
            }

            DateTimeRange range = RunTimeRangeResolution(inputList, context);

            return range;
        }

        /// <summary>
        /// Runs the date range inference engine to attempt to extract a valid date time range from an input string.
        /// </summary>
        /// <param name="allMatches">A set of TimexMatches, ordered in their original lexical ordering, to extract a range from</param>
        /// <param name="context">The context to use for timex matching. The Normalization flag of this context
        ///     will be used to determine if the returned range should be in the past or the future</param>
        /// <returns>A DateTimeRange object containing the extracted portions of the date time range. If no valid times were found, this will contain null pointers.
        ///     If only one valid time was found, it will be placed in the startTime field, and endTime will be nullptr. If a complete range was found, startTime
        ///     and endTime will both contain valid ExtendedDateTimes for the match.</returns>
        public static DateTimeRange RunTimeRangeResolution(IList<TimexMatch> allMatches, TimexContext context)
        {
            // Make an empty vector for the contextual matches
            IList<TimexMatch> contextMatches = new List<TimexMatch>();
            return RunTimeRangeResolution(allMatches, contextMatches, context);
        }

		/// <summary>
		/// Runs the date range inference engine to attempt to extract a valid date time range from an input string.
		/// </summary>
		/// <param name="primaryMatches">A set of TimexMatches, ordered in their original lexical ordering, to extract a range from</param>
		/// <param name="extraContextMatches">A set of TimexMatches that may be used as "extra context" in the inference</param>
		/// <param name="context">The context to use for timex matching. The Normalization flag of this context
		///     will be used to determine if the returned range should be in the past or the future</param>
		/// <returns>A DateTimeRange object containing the extracted portions of the date time range. If no valid times were found, this will contain null pointers.
		///     If only one valid time was found, it will be placed in the startTime field, and endTime will be nullptr. If a complete range was found, startTime
		///     and endTime will both contain valid ExtendedDateTimes for the match.</returns>
        public static DateTimeRange RunTimeRangeResolution(
            IList<TimexMatch> primaryMatches, 
			IList<TimexMatch> extraContextMatches,
			TimexContext context)
        {
            DateTimeRange returnVal = new DateTimeRange();

            if (primaryMatches == null || primaryMatches.Count == 0)
            {
                return returnVal;
            }

            // Present normalization context is not supported
            if (context == null || context.Normalization == Normalization.Present)
            {
                return returnVal;
            }

            try
            {
                // Check if we can just infer a value from duration
                if (UseInferenceOnDurations(primaryMatches, context, out returnVal))
                {
                    return returnVal;
                }

                IList<TimexMatch> timexMatchesVector = new List<TimexMatch>();
        
                // Process the raw results and put them into a vector for easier manipulation
                foreach (TimexMatch nextMatch in primaryMatches)
                {
#if TIME_RANGES_DEBUG
                    Debug.WriteLine("Raw match " + nextMatch.ToTimexTag().ToString() + " (" + nextMatch.RuleId + ")");
#endif
                    // Cull everything that is not salient in the context
                    if (!context.TemporalType.HasFlag(nextMatch.ExtendedDateTime.TemporalType) ||
                        nextMatch.ExtendedDateTime.TemporalType == TemporalType.Duration)
                    {
                        continue;
                    }

                    // Also cull all PAST_REF and FUTURE_REF
                    if (nextMatch.ExtendedDateTime.SetParts == DateTimeParts.Reference &&
                        nextMatch.ExtendedDateTime.Reference != DateTimeReference.Present)
                    {
                        continue;
                    }

                    // Convert offsets into absolute times before putting them in the processing queue
                    ConvertOffsetMatchToAbsolute(nextMatch, context);
                    timexMatchesVector.Add(nextMatch);
                }

                // Capture hints from the input context, appending them to the main set of matches
                ProcessContext(extraContextMatches, ref timexMatchesVector);

                // Only one match? This is simple enough
                if (timexMatchesVector.Count == 1)
                {
                    TimexMatch onlyMatch = timexMatchesVector[0];
                    returnVal.StartTime = onlyMatch;
                    return returnVal;
                }

                // Check for some invalid edge cases
                if (ContainsInvalidConstructions(timexMatchesVector))
                {
#if TIME_RANGES_DEBUG
                    Debug.WriteLine("Detected invalid construction. Aborting...");
#endif
                    return returnVal;
                }

                // Attempt to merge partial dates/times
                timexMatchesVector = MergePartialTimexMatches(timexMatchesVector);
                timexMatchesVector = MergePartialTimexMatches(timexMatchesVector);

#if TIME_RANGES_DEBUG
                    
                    if (timexMatchesVector.Count != primaryMatches.Count)
                    {
                        Debug.WriteLine("After slot merging:");
                        foreach (var match in timexMatchesVector)
                        {
                            Debug.WriteLine("Raw match " + match.ToTimexTag().ToString());
                            Debug.WriteLine(match.RuleId);
                        }
                    }
#endif

                if (timexMatchesVector.Count == 0)
                {
                    return returnVal;
                }
                else if (timexMatchesVector.Count == 1)
                {
                    returnVal.StartTime = timexMatchesVector[0];
                    return returnVal;
                }
                else
                {
                    // There were at least 2 matches. Try and extract a range.
                    MatchPair bestGuessRange = ExtractDateRangeValues(timexMatchesVector);
            
                    TimexMatch first = bestGuessRange.a;
                    TimexMatch second = bestGuessRange.b;

                    // Was no range detected? In this case, return a single value
                    if (second == null)
                    {
                        returnVal.StartTime = first;
                        return returnVal;
                    }
        
                    // Apply inference rules to fix inconsistencies in the range and return the normalized range
                    returnVal = ApplyRangeInferenceRules(first, second, context);
                    return returnVal;
                }
            }
#if TIME_RANGES_DEBUG
            catch (TimexException e)
#else
            catch (TimexException)
#endif
            {
                // Catch errors at the top level of the API
#if TIME_RANGES_DEBUG
                Debug.WriteLine("Grammar exception caught while running range inference: " + e.Message);
#endif
                return returnVal;
            }
        }

        /// <summary>
        /// Performs one pass of timex match merging, which will inspect adjacent matches, check them for compatability, and merge them into combined values.
        /// I.e. the matches "5:00 PM" and "Monday" will merge into "5:00 PM Monday".
        /// A newly created vector is returned to contain the resulting matches. Relative ordering will be preserved in the returned vector.
        /// </summary>
        /// <param name="times">A list of time objects to be merged, in order</param>
        /// <returns>A new vector of ExtendedDateTime objects, potentially containing merged and unmerged values</returns>
        public static IList<ExtendedDateTime> MergePartialTimexMatches(IList<ExtendedDateTime> times)
        {
            if (times == null || times.Count == 0)
            {
                return new List<ExtendedDateTime>();
            }

            IList<TimexMatch> inputList = new List<TimexMatch>();
            foreach (ExtendedDateTime match in times)
            {
                inputList.Add(new TimexMatch(match));
            }

            IList<TimexMatch> outputList = MergePartialTimexMatches(inputList);

            IList<ExtendedDateTime> returnVal = new List<ExtendedDateTime>();
            foreach (TimexMatch match in outputList)
            {
                returnVal.Add(match.ExtendedDateTime);
            }

            return returnVal;
        }

        /// <summary>
        /// Performs one pass of timex match merging, which will inspect adjacent matches, check them for compatability, and merge them into combined values.
        /// I.e. the matches "5:00 PM" and "Monday" will merge into "5:00 PM Monday".
        /// A newly created vector is returned to contain the resulting matches. Relative ordering will be preserved in the returned vector.
        /// Timex match attributes, such as ID and value, will be augmented to reflect the merge that took place
        /// </summary>
        /// <param name="matches">A list of timex matches to be merged, in order</param>
        /// <returns>A new vector of timexmatches, potentially containing merged and unmerged values</returns>
        public static IList<TimexMatch> MergePartialTimexMatches(IList<TimexMatch> matches)
        {
            if (matches == null || matches.Count == 0)
            {
                return new List<TimexMatch>();
            }

            try
            {
                // Determine if one of the matches specified a part of day or an hour
                // This will determine if we need to rely on part of day hints
                bool partOfDayIsSpecified = matches.Any(match =>
                    {
                        DateTimeParts parts = match.ExtendedDateTime.SetParts;
                        return (parts.HasFlag(DateTimeParts.PartOfDay) &&
                            parts != DateTimeParts.PartOfDay) ||
                            (parts.HasFlag(DateTimeParts.Hour) &&
                            parts.HasFlag(DateTimeParts.AmPmUnambiguous));
                    });

                IList<TimexMatch> returnVal = new List<TimexMatch>();

                int firstMatchIndex = 0;
                int newRuleIndex = 0;
                while (firstMatchIndex + 1 < matches.Count)
                {
                    // Compare each match with the one next to it
                    // If they do not overlap in SetParts at all, they are a candidate for merging
                    TimexMatch oneCloned = matches[firstMatchIndex].Clone();
                    TimexMatch twoCloned = matches[firstMatchIndex + 1].Clone();
                    ExtendedDateTime firstTime = oneCloned.ExtendedDateTime;
                    ExtendedDateTime secondTime = twoCloned.ExtendedDateTime;

                    // Is an extemporaneous PartOfDay hint inside the window? Skip it.
                    if (partOfDayIsSpecified && secondTime.IsPartOfDayOnly())
                    {
                        oneCloned.Id = newRuleIndex++;
                        returnVal.Add(oneCloned);
                        firstMatchIndex += 2;
                        continue;
                    }
                    if (partOfDayIsSpecified && firstTime.IsPartOfDayOnly())
                    {
#if TIME_RANGES_DEBUG
                        Debug.WriteLine("Eliminating redundant part of day hint " + one.ToTimexTag().ToString());
#endif
                        firstMatchIndex += 1;
                        continue;
                    }
        
                    // Test for merging compatability
                    if (CanBeMerged(firstTime, secondTime))
                    {
                        // If one of them is an offset, convert it to an absolute time first
                        TimexContext context = firstTime.Context;
					    if (firstTime.IsOffset())
                        {
                            firstTime = ConvertOffsetTimeToAbsolute(oneCloned.ExtendedDateTime, firstTime.Context);
                        }
                        if (secondTime.IsOffset())
                        {
                            secondTime = ConvertOffsetTimeToAbsolute(twoCloned.ExtendedDateTime, secondTime.Context);
                        }
                    
                        // Looks good! Perform the merge by augmenting the first match object with the data from the second
                        ExtendedDateTime mergedTime = ExtendedDateTime.Merge(
                            firstTime,
                            secondTime,
                            TemporalType.None,
                            context);
                        string newValue = oneCloned.Value;
                        newValue += " " + twoCloned.Value;
                        oneCloned.Value = newValue;
                        oneCloned.RuleId = oneCloned.RuleId + " and " + twoCloned.RuleId;
                        oneCloned.Id = newRuleIndex++;
                        oneCloned.ExtendedDateTime = mergedTime;
                        var idList = new HashSet<int>(twoCloned.MergedIds);
                        idList.Add(oneCloned.Id);
                        idList.Add(twoCloned.Id);
                        oneCloned.MergedIds = oneCloned.MergedIds.Union(idList).ToList();
                        // Push the merged match into the results list
                        returnVal.Add(oneCloned);
                        firstMatchIndex += 2;
                        continue;
                    }

                    // Compare this match's value with the next one. Are they identical?
                    // If so, it's probably redundant input. Keep the most specific one
                    // But only do this for dates because dates can more easily be redundant (i.e. "next saturday the 5th")
                    if (firstTime.FormatValue() == secondTime.FormatValue() &&
                        firstTime.SetParts != secondTime.SetParts &&
                        !firstTime.SetParts.HasFlag(DateTimeParts.Hour) &&
                        !secondTime.SetParts.HasFlag(DateTimeParts.Hour))
                    {
                        if (GetFlagWeight(firstTime.ExplicitSetParts) > GetFlagWeight(secondTime.ExplicitSetParts))
                        {
                            oneCloned.Id = newRuleIndex++;
                            returnVal.Add(oneCloned);
                        }
                        else
                        {
                            twoCloned.Id = newRuleIndex++;
                            returnVal.Add(twoCloned);
                        }
                        firstMatchIndex += 2;
                        continue;
                    }

                    oneCloned.Id = newRuleIndex++;
                    returnVal.Add(oneCloned);
                    firstMatchIndex++;
                }

                // Append the tail item to the returnVal list, since it wasn't covered in the for loop
                if (firstMatchIndex < matches.Count)
                {
                    // Remove extemporaneous part of day hints
                    if (!(partOfDayIsSpecified && matches[firstMatchIndex].ExtendedDateTime.IsPartOfDayOnly()))
                    {
                        TimexMatch mergedMatch = matches[firstMatchIndex].Clone();
                        mergedMatch.Id = newRuleIndex++;
                        returnVal.Add(mergedMatch);
                    }
                }

                return returnVal;
            }
#if TIME_RANGES_DEBUG
            catch (TimexException e)
#else
            catch (TimexException)
#endif
            {
                // Catch errors at the top level of the API - bounce back the input value
#if TIME_RANGES_DEBUG
                    Debug.WriteLine("Grammar exception caught while merging slots: " + e.Message);
#endif
                return matches;
            }
        }

#endregion

#region Internal Methods

        /// <summary>
        /// Returns the weighted average of the SetParts of an ExtendedDateTime. Used to determine which time expressions are more specific than others.
        /// </summary>
        /// <param name="flags">The DateTimeParts to inspect</param>
        /// <returns>A float value corresponding to how granular the given DateTimeParts are specified.</returns>
        private static float GetFlagWeight(DateTimeParts flags)
        {
            float returnVal = 0.0f;

            foreach (var flagAndWeight in _flagWeights.Value)
            {
                if (flags.HasFlag(flagAndWeight.Item1))
                    returnVal += flagAndWeight.Item2;
            }

            if (flags.HasFlag(DateTimeParts.PartOfDay) && !flags.HasFlag(DateTimeParts.AmPmUnambiguous))
            {
                returnVal += 1.5f;
            }
    
            return returnVal;
        }

        /// <summary>
        /// Determines if a special case needs to be triggered for dates that are defined in terms of an offset and a day of week,
        /// for example "next friday". Values of this type cannot be compared directly with plain weekday expressions like "Wednesday", so
        /// we need to differentiate between them.
        /// </summary>
        /// <param name="dateTime">The extendeddatetime to inspect</param>
        /// <returns>True if this datetime is given in terms of a weekday offset</returns>
        private static bool IsWeekdayOffsetException(ExtendedDateTime dateTime)
        {
            return string.IsNullOrEmpty(dateTime.OffsetAnchor) &&
                dateTime.OffsetUnit.IsWeekday();
        }

        /// <summary>
        /// Returns true if the given DateTimeParts represent dates or times that could represent the same type of thing
        /// This test is performed when inferring whether a time range can exist between two ExtendedDateTime objects
        /// </summary>
        /// <param name="a">The first DateTimeParts to compare</param>
        /// <param name="b">The second DateTimeParts to compare</param>
        /// <returns>True if these parts could be used to define a time range.</returns>
        private static bool PartsHaveOverlap(DateTimeParts a, DateTimeParts b)
        {
            // Special case: If one has a time and the other has a part of day, they are considered overlapping
            if (!(a.HasFlag(DateTimeParts.Hour) && b.HasFlag(DateTimeParts.Hour)) &&
                !(a.HasFlag(DateTimeParts.PartOfDay) && b.HasFlag(DateTimeParts.PartOfDay)) &&
                ((a.HasFlag(DateTimeParts.PartOfDay) && b.HasFlag(DateTimeParts.Hour)) ||
                (a.HasFlag(DateTimeParts.Hour) && b.HasFlag(DateTimeParts.PartOfDay))))
            {
                // But only if they don't also specify a day (so we don't merge "Tuesday morning" and "5:00") (TODO is that desirable?)
                return (!(a.HasFlag(DateTimeParts.Day) || a.HasFlag(DateTimeParts.WeekDay)) &&
                    !(b.HasFlag(DateTimeParts.Day) || b.HasFlag(DateTimeParts.WeekDay)));
            }

            // Special case: If one has a day and the other has a weekday, they are overlapping.
            if ((a.HasFlag(DateTimeParts.WeekDay) && b.HasFlag(DateTimeParts.Day)) ||
                (a.HasFlag(DateTimeParts.Day) && b.HasFlag(DateTimeParts.WeekDay)))
            {
                return true;
            }

            return (a & b) != 0;
        }

        /// <summary>
        /// Converts an ExtendedDateTime given as an offset (such as "tomorrow") into an absolute value, which will allow
        /// for simple comparison later on. This method will return a newly created ExtendedDateTime.
        /// </summary>
        /// <param name="toConvert">The ExtendedDateTime to convert</param>
        /// <param name="context">The context to use for the returned time</param>
        /// <returns>A new ExtendedDateTime that has resolved the offset value of the original match into a fixed day/month/year</returns>
        private static ExtendedDateTime ConvertOffsetTimeToAbsolute(ExtendedDateTime toConvert, TimexContext context)
        {
            if (!toConvert.IsOffset())
            {
                return toConvert;
            }

#if TIME_RANGES_DEBUG
                    Debug.WriteLine("Attempting to convert match \"" + toConvert.FormatValue() + "\" to absolute time...");
#endif

            ExtendedDateTime newTimeValue = DateTimeParsers.TryParseExtendedDateTime(toConvert.FormatType(), toConvert.FormatValue(), "", "", "", "", context);
            if (newTimeValue == null)
            {
#if TIME_RANGES_DEBUG
                    Debug.WriteLine("Offset conversion failed for " + toConvert.FormatValue());
#endif
                return toConvert;
            }

#if TIME_RANGES_DEBUG
                    Debug.WriteLine("Conversion succeeded");
#endif
            return newTimeValue;
        }

        /// <summary>
        /// Converts an ExtendedDateTime given as an offset (such as "today") into an absolute value, which will allow
        /// for simple comparison later on. This method works in-place on the passed-in TimexMatch value.
        /// After this method is called, no more inference can be performed on the affected ExtendedDateTime.
        /// </summary>
        /// <param name="toConvert">The TimexMatch to convert</param>
        /// <param name="context">The normalization context to use for conversion</param>
        private static void ConvertOffsetMatchToAbsolute(TimexMatch toConvert, TimexContext context)
        {
            toConvert.ExtendedDateTime = ConvertOffsetTimeToAbsolute(toConvert.ExtendedDateTime, context);
        }

        /// <summary>
        /// Reinterprets an ExtendedDateTime value in terms of a new normalization direction and reference date.
        /// This is typically used to reinterpret times such as "6:00" to different days or parts of day, to resolve ambiguity
        /// </summary>
        /// <param name="originalTime">The ExtendedDateTime to be reinterpreted</param>
        /// <param name="refTime">A new reference date time to use for inference</param>
        /// <param name="direction">The normalization direction to use</param>
        /// <returns>A new ExtendedDateTime that has been reinterpeted</returns>
        private static ExtendedDateTime MatchRelativeTo(ExtendedDateTime originalTime, DateTime refTime, Normalization direction)
        {
            TimexContext modContext = new TimexContext()
                {
                    UseInference = true,
                    ReferenceDateTime = refTime,
                    Normalization = direction,
                    TemporalType = TemporalType.All
                };

            ExtendedDateTime returnVal = originalTime.Reinterpret(modContext);

#if TIME_RANGES_DEBUG
                    
                if (returnVal.InputDateWasInvalid)
                {
                     Debug.WriteLine("Relative match resulted in an invalid date");
                }
                Debug.WriteLine("Matched to " + returnVal.FormatValue());
#endif
    
            return returnVal;
        }

        /// <summary>
        /// Reinterprets an ExtendedDateTime value in terms of a new TimexDictionary and reference date.
        /// This is typically used to reinterpret times such as "6:00" to different days or parts of day, to resolve ambiguity
        /// </summary>
        /// <param name="timexDictionary">A TimexDictionary that describes a single date time value</param>
        /// <param name="refTime">A new reference date time to use for inference</param>
        /// <param name="direction">The normalization direction to use</param>
        /// <returns>A new ExtendedDateTime that has been reinterpeted</returns>
        private static ExtendedDateTime MatchRelativeTo(IDictionary<string, string> timexDictionary, DateTime refTime, Normalization direction)
        {
            TimexContext modContext = new TimexContext()
                {
                    UseInference = true,
                    ReferenceDateTime = refTime,
                    Normalization = direction,
                    TemporalType = TemporalType.All
                };

            // Passing TemporalType_None will cause the EDT to infer its own type
            ExtendedDateTime returnVal = ExtendedDateTime.Create(TemporalType.None, timexDictionary, modContext);

#if TIME_RANGES_DEBUG
                if (returnVal.InputDateWasInvalid)
                {
                    Debug.WriteLine("Relative match resulted in an invalid date");
                }
                Debug.WriteLine("Matched to " + returnVal.FormatValue());
#endif

            return returnVal;
        }

        /// <summary>
        /// Given a set of timexmatches and an "anchor" match, determine the most likely candidate for the
        /// other end of the inferred date range. This basically boils down to "do I choose the match that comes
        /// to the left or the one to the right?". Returns the final value as a match pair.
        /// </summary>
        /// <param name="timexMatches">A list of timexmatches to operate on</param>
        /// <param name="mostSpecifiedRule">The index of the match that will anchor one end of the resulting range</param>
        /// <returns>A pair of TimexMatches. Both values within the pair shall be non-null</returns>
        private static MatchPair DetermineMostSpecificMatchPair(IList<TimexMatch> timexMatches, int mostSpecifiedRule)
        {
            MatchPair returnVal;

            // Starting from the most specified match, determine which of its neighbors is a more likely candidate for range interpretation
            bool chooseRight = true;

            // The time occurring before the most specific one
            ExtendedDateTime leftTime = mostSpecifiedRule > 0 ? timexMatches[mostSpecifiedRule - 1].ExtendedDateTime : null;
            // The most specific time expression
            ExtendedDateTime centerTime = timexMatches[mostSpecifiedRule].ExtendedDateTime;
            // The time occurring after the most specific one
            ExtendedDateTime rightTime = mostSpecifiedRule < timexMatches.Count - 1 ? timexMatches[mostSpecifiedRule + 1].ExtendedDateTime : null;

            if (leftTime == null)
            {
                chooseRight = true;
            }
            else if (rightTime == null)
            {
                chooseRight = false;
            }
            else if (!CanBeARange(leftTime, centerTime))
            {
                chooseRight = true;
            }
            else if (!CanBeARange(centerTime, rightTime))
            {
                chooseRight = false;
            }
            else
            {
                chooseRight = GetFlagWeight(leftTime.ExplicitSetParts) <
                    GetFlagWeight(rightTime.ExplicitSetParts);
            }

            if (chooseRight)
            {
#if TIME_RANGES_DEBUG
                    Debug.WriteLine("Guessing the range is between tokens " + mostSpecifiedRule + " and " + (mostSpecifiedRule + 1));
#endif
                returnVal.a = timexMatches[mostSpecifiedRule];
                returnVal.b = timexMatches[mostSpecifiedRule + 1];
            }
            else
            {
#if TIME_RANGES_DEBUG
                    Debug.WriteLine("Guessing the range is between tokens " + (mostSpecifiedRule - 1) + " and " + mostSpecifiedRule);
#endif
                returnVal.a = timexMatches[mostSpecifiedRule - 1];
                returnVal.b = timexMatches[mostSpecifiedRule];
            }

            return returnVal;
        }

        /// <summary>
        /// Accepts a list of timex matches, and attempts to infer which ones define the range.
        /// This pair will be returned inside of a DateTimeRange struct.
        /// As long as the input vector has size > 0, this method shall always return a value in its first slot.
        /// If no range can be inferred, this will return the most granular time as its startTime.
        /// </summary>
        /// <param name="timexMatches">A list of timexmatches that may or may not contain a date. Must be non-empty</param>
        /// <returns>A pair of TimexMatches containing what was inferred to be the beginning and end of a valid time range. The second value may be nullptr.</returns>
        private static MatchPair ExtractDateRangeValues(IList<TimexMatch> timexMatches)
        {
            MatchPair returnVal;
            returnVal.a = null;
            returnVal.b = null;

            if (timexMatches.Count < 2)
            {
#if TIME_RANGES_DEBUG
                    Debug.WriteLine("Warning: Less than 2 matches passed into ExtractDateRangeValues");
#endif
                return returnVal;
            }
    
            // We have at least two matches. Find out which ones are likely to be ranges
            // Do this by finding the one that is most specified, and then picking the one closest to it
            // We need to keep track of two sets of variables here - one is for the most specific rule in general,
            // and one for the most specific rule that is adjacent to a compatible neighbor.
            // This is so we have a fallback; if this method only returns 1 match object, it should be the most specific overall match, regardless of its neighbors
            int mostSpecifiedRule = 0;
            int mostSpecifiedValidRule = 0;
            float highestFlagCount = 0;
            float highestValidFlagCount = 0;

            // TODO: Make this loop honor a language's LTR reading order
#if TIME_RANGES_DEBUG
                    Debug.WriteLine("Feature weights:");
#endif
            for (int index = 0; index < timexMatches.Count; index++)
            {
                ExtendedDateTime thisDateTime = timexMatches[index].ExtendedDateTime;
                float thisCount = GetFlagWeight(thisDateTime.ExplicitSetParts);
            
                // If the most specific time expression occurred on the edge, but the edge does not overlap with its neighbor, exempt it
                // from range interpretation
                bool isExempt = (index == 0 &&
                    !CanBeARange(thisDateTime,
                    timexMatches[index + 1].ExtendedDateTime)) ||
                    (index == timexMatches.Count - 1 &&
                    !CanBeARange(timexMatches[index - 1].ExtendedDateTime, thisDateTime));

                if (thisCount > highestFlagCount)
                {
                    highestFlagCount = thisCount;
                    mostSpecifiedRule = index;
                }
                if (thisCount > highestValidFlagCount && !isExempt)
                {
                    highestValidFlagCount = thisCount;
                    mostSpecifiedValidRule = index;
                }
#if TIME_RANGES_DEBUG
                    Debug.WriteLine(thisCount);
#endif
            }

            // Are there way too many matches? If so, skip the range processing. Just return the most specific time.
            if (timexMatches.Count > MAX_MATCHES_FOR_RANGE)
            {
#if TIME_RANGES_DEBUG
                    Debug.WriteLine("Way too many matches, returning match number " + mostSpecifiedRule);
#endif
                returnVal.a = timexMatches[mostSpecifiedRule];
                return returnVal;
            }

            // Call the helper function to make the judgment of which 2 matches should compose the final range
            returnVal = DetermineMostSpecificMatchPair(timexMatches, mostSpecifiedValidRule);

            // This is needed so that "Next wednesday to the following Friday" will work
            if (returnVal.a.ExtendedDateTime.IsOffset() ||
                returnVal.b.ExtendedDateTime.IsOffset())
            {
                return returnVal;
            }

            // PRESENT_REF is considered a universal time qualifier, so make sure we catch that
            if (returnVal.a.ExtendedDateTime.Reference == DateTimeReference.Present ||
                returnVal.b.ExtendedDateTime.Reference == DateTimeReference.Present)
            {
                return returnVal;
            }

            // This will eliminate mismatched ranges like "from November 12th to midnight"
            if (!CanBeARange(returnVal.a.ExtendedDateTime, returnVal.b.ExtendedDateTime))
            {
#if TIME_RANGES_DEBUG
                    Debug.WriteLine("No range detected (SetParts have no overlap)");
#endif
                returnVal.a = timexMatches[mostSpecifiedRule];
                returnVal.b = null;
            }

            // TODO: Skip dates that are in week-of-year format?

            return returnVal;
        }

        /// <summary>
        /// Inspects a date range to determine if it is anchored to "Now" on either end. In such a case, the other end will be resolved
        /// and returnVal will be set to the resulting range. Otherwise, this method returns false with no side effects.
        /// </summary>
        /// <param name="rangeBegin">The match that defines the beginning of a time range</param>
        /// <param name="rangeEnd">The match that defines the end of a time range</param>
        /// <param name="context">The context to use for normalization</param>
        /// <param name="returnVal">(out) The inferred time range, anchored to NOW on one end</param>
        /// <returns>True if this range can be interpreted as a range between now and another time</returns>
        private static bool UseInferenceOnPresentReferences(TimexMatch rangeBegin, TimexMatch rangeEnd, TimexContext context, ref DateTimeRange returnVal)
        {
            // catch "Right now -> right now" case. Just return one "now"
            if (rangeBegin.ExtendedDateTime.Reference == DateTimeReference.Present &&
                rangeEnd.ExtendedDateTime.Reference == DateTimeReference.Present)
            {
                returnVal.StartTime = rangeBegin;
                return true;
            }
            else if (rangeBegin.ExtendedDateTime.Reference != DateTimeReference.Present &&
                rangeEnd.ExtendedDateTime.Reference != DateTimeReference.Present)
            {
                return false;
            }

            TimexMatch newRangeBegin = new TimexMatch()
                {
                    ExtendedDateTime = rangeBegin.ExtendedDateTime,
                    Id = rangeBegin.Id,
                    Value = rangeBegin.Value,
                    RuleId = rangeBegin.RuleId,
                    Index = rangeBegin.Index
                };

            if (rangeEnd.ExtendedDateTime.Reference == DateTimeReference.Present) // The range ENDS now. Flip things around
            {
                // The start time needs to be in the past. Reformat it relative to the current reference time
                ExtendedDateTime newAnchorTime = MatchRelativeTo(rangeBegin.ExtendedDateTime, context.ReferenceDateTime, Normalization.Past);
                newRangeBegin.ExtendedDateTime = newAnchorTime;
            }
    
            // Check if the end time is in the past for some reason, or if the start time is in the future. If so, flip the values
            if (rangeEnd.ExtendedDateTime.IncompleteCompareTo(context.ReferenceDateTime) < 0 ||
                newRangeBegin.ExtendedDateTime.IncompleteCompareTo(context.ReferenceDateTime) > 0)
            {
                returnVal.StartTime = rangeEnd;
                returnVal.EndTime = newRangeBegin;
            }
            else
            {
                returnVal.StartTime = newRangeBegin;
                returnVal.EndTime = rangeEnd;
            }

            return true;
        }

        /// <summary>
        /// Applies inference to resolve ambiguity in the given time range, with the assumption that the rangeEnd should be resolved in terms of rangeBegin.
        /// This function will modify RangeEnd in-place.
        /// </summary>
        /// <param name="rangeBegin">The start of the time range</param>
        /// <param name="rangeEnd">The end of the time range</param>
        /// <param name="context">The inference context</param>
        /// <returns>True if inference succeeded.</returns>
        private static bool ApplyInferenceLeftToRight(TimexMatch rangeBegin, TimexMatch rangeEnd, TimexContext context)
        {
            DateTime refTime = context.ReferenceDateTime;
            // Catch a very specific edge case: "let's meet tomorrow 6:00-8:00 in the morning" - 6:00 is stuck at an inferred PM value
            int hourSpan = rangeBegin.ExtendedDateTime.Hour.GetValueOrDefault(0) - rangeEnd.ExtendedDateTime.Hour.GetValueOrDefault(0);
            if (rangeBegin.ExtendedDateTime.SetParts.HasFlag(DateTimeParts.Day) &&
                !rangeEnd.ExtendedDateTime.SetParts.HasFlag(DateTimeParts.Day) &&
                !rangeBegin.ExtendedDateTime.SetParts.HasFlag(DateTimeParts.AmPmUnambiguous) &&
                rangeEnd.ExtendedDateTime.SetParts.HasFlag(DateTimeParts.AmPmUnambiguous) &&
                rangeBegin.ExtendedDateTime.Hour.GetValueOrDefault(0) > 12 &&
                hourSpan > 0 && hourSpan < 12)
            {
                rangeBegin.ExtendedDateTime.FlipAmPm();
            }
            
            // Resolve the second time in terms of the first
            DateTime parseResult;
            if (DateTimeParsers.TryParseISOIntoLocalDateTime(rangeBegin.ExtendedDateTime.FormatValue(), out parseResult))
            {
                refTime = parseResult;
            }
            else
            {
                refTime = context.ReferenceDateTime; // If parsing failed, use the current reference time
            }

            // Make the inference
            rangeEnd.ExtendedDateTime = MatchRelativeTo(rangeEnd.ExtendedDateTime, refTime, Normalization.Future);
                
            hourSpan = rangeBegin.ExtendedDateTime.Hour.GetValueOrDefault(0) - rangeEnd.ExtendedDateTime.Hour.GetValueOrDefault(0);
            // Test: Is it an ambiguous time range that is separated by more than 12 hours?
            // If so, flip the PM of the second time to AM
            if (rangeBegin.ExtendedDateTime.SetParts.HasFlag(DateTimeParts.Hour) &&
                !rangeEnd.ExtendedDateTime.SetParts.HasFlag(DateTimeParts.AmPmUnambiguous) &&
                hourSpan < 12 && hourSpan > 0)
            {
                // "Tonight from 10:00 to 1:00" -> flip 1:00 to AM
                rangeEnd.ExtendedDateTime.FlipAmPm();
            }
            else if (rangeBegin.ExtendedDateTime.SetParts.HasFlag(DateTimeParts.Hour) &&
                !rangeBegin.ExtendedDateTime.SetParts.HasFlag(DateTimeParts.AmPmUnambiguous) &&
                rangeEnd.ExtendedDateTime.SetParts.HasFlag(DateTimeParts.AmPmUnambiguous))
            {
                // "tomorrow from 9:00 to 1:00 AM" -> flip 9:00 to PM
                // "Tomorrow from 8:00 to 10:00 PM -> flip 8:00 to PM
                if (hourSpan < -12 || hourSpan > 0)
                {
                    rangeBegin.ExtendedDateTime.FlipAmPm();
                }
            }
            return true;
        }

        /// <summary>
        /// Applies inference to resolve ambiguity in the given time range, with the assumption that the rangeBegin should be resolved in terms of rangeEnd.
        /// This function will modify RangeBegin in-place.
        /// </summary>
        /// <param name="rangeBegin">The start of the time range</param>
        /// <param name="rangeEnd">The end of the time range</param>
        /// <param name="context">The inference context</param>
        /// <returns>True if inference succeeded.</returns>
        private static bool ApplyInferenceRightToLeft(TimexMatch rangeBegin, TimexMatch rangeEnd, TimexContext context)
        {
            DateTime refTime;
            if (!DateTimeParsers.TryParseISOIntoLocalDateTime(rangeEnd.ExtendedDateTime.FormatValue(), out refTime))
            {
                refTime = context.ReferenceDateTime;
            }

            // Handle AMPM partial carryover (happens for inputs like "8:00 PM to 9:00 Tuesday", the "PM" does not carry over properly)
            if (rangeBegin.ExtendedDateTime.SetParts.HasFlag(DateTimeParts.AmPmUnambiguous) &&
                !rangeEnd.ExtendedDateTime.SetParts.HasFlag(DateTimeParts.AmPmUnambiguous))
            {
                bool applyCarryover = false;
                
                // Test if the second time is AM but should be PM
                if (rangeBegin.ExtendedDateTime.Hour.GetValueOrDefault(0) > 12 && refTime.Hour <= 12)
                {
#if TIME_RANGES_DEBUG
                    Debug.WriteLine("Caught Am/Pm discrepancy; applying carryover rule...");
#endif
                    // Correct the 12 hour discrepancy in the refence time
                    refTime = refTime.AddHours(12);
                    applyCarryover= true;
                }
                // Test if the second time is PM but should be AM
                else if (rangeBegin.ExtendedDateTime.Hour.GetValueOrDefault(0) <= 12 && refTime.Hour > 12 &&
                    rangeBegin.ExtendedDateTime.Hour.GetValueOrDefault(0) < (refTime.Hour - 12))
                {
#if TIME_RANGES_DEBUG
                    Debug.WriteLine("Caught Am/Pm discrepancy; applying carryover rule...");
#endif
                    refTime = refTime.AddHours(-12);
                    applyCarryover = true;
                }

                if (applyCarryover)
                {
                    // "See-saw" the value back and forth until it settles on the intended value (this propagates the change to both start and end times)
                    rangeBegin.ExtendedDateTime = MatchRelativeTo(rangeBegin.ExtendedDateTime, refTime, Normalization.Past);
                    
                    if (!DateTimeParsers.TryParseISOIntoLocalDateTime(rangeBegin.ExtendedDateTime.FormatValue(), out refTime))
                    {
                        refTime = context.ReferenceDateTime; // If could not parse refTime, just use the standard reference time from the context.
                    }

                    // Only resolve the time fields, ignoring all others.
                    IDictionary<string, string> amPmDictionary = new Dictionary<string, string>();
                    IDictionary<string, string> originalDictionary = rangeEnd.ExtendedDateTime.OriginalTimexDictionary;
                    IList<string> attributesToCopy = new List<string>();
                    attributesToCopy.Add(Iso8601.Hour);
                    attributesToCopy.Add(Iso8601.Minute);
                    attributesToCopy.Add(Iso8601.Second);
                    attributesToCopy.Add(TimexAttributes.AmPm);
                    foreach (var attribute in attributesToCopy)
                    {
                        if (originalDictionary.ContainsKey(attribute))
                        {
                            amPmDictionary[attribute] = originalDictionary[attribute];
                        }
                    }

                    rangeEnd.ExtendedDateTime = MatchRelativeTo(amPmDictionary, refTime, Normalization.Future);
                }
                else
                {
                    rangeBegin.ExtendedDateTime = MatchRelativeTo(rangeBegin.ExtendedDateTime, refTime, Normalization.Past);

                    int hourSpan = rangeBegin.ExtendedDateTime.Hour.GetValueOrDefault(0) - rangeEnd.ExtendedDateTime.Hour.GetValueOrDefault(0);
                    // Test: Is it an ambiguous time range that is separated by more than 12 hours?
                    // If so, flip the PM of the second time to AM
                    if (rangeBegin.ExtendedDateTime.SetParts.HasFlag(DateTimeParts.Hour) &&
                        !rangeEnd.ExtendedDateTime.SetParts.HasFlag(DateTimeParts.AmPmUnambiguous) &&
                        hourSpan < 12 && hourSpan > 0)
                    {
                        // "Tonight from 10:00 to 1:00" -> flip 1:00 to AM
                        rangeEnd.ExtendedDateTime.FlipAmPm();
                    }
                    else if (rangeBegin.ExtendedDateTime.SetParts.HasFlag(DateTimeParts.Hour) &&
                        !rangeBegin.ExtendedDateTime.SetParts.HasFlag(DateTimeParts.AmPmUnambiguous) &&
                        rangeEnd.ExtendedDateTime.SetParts.HasFlag(DateTimeParts.AmPmUnambiguous))
                    {
                        // "tomorrow from 9:00 to 1:00 AM" -> flip 9:00 to PM
                        // "Tomorrow from 8:00 to 10:00 PM -> flip 8:00 to PM
                        if (hourSpan < -12 || hourSpan > 0)
                        {
                            rangeBegin.ExtendedDateTime.FlipAmPm();
                        }
                    }
                }
            }
            else if (!rangeBegin.ExtendedDateTime.SetParts.HasFlag(DateTimeParts.AmPmUnambiguous) &&
                !rangeEnd.ExtendedDateTime.SetParts.HasFlag(DateTimeParts.AmPmUnambiguous) &&
                rangeBegin.ExtendedDateTime.Hour.GetValueOrDefault(0) > rangeEnd.ExtendedDateTime.Hour.GetValueOrDefault(0))
            {
                // Catch the case where both fields are ambiguous, and they cross the inference threshold. Example: 1:00 to 8:00 on Tuesday - should resolve both times to PM
                int medianTime = ((rangeBegin.ExtendedDateTime.Hour.GetValueOrDefault(0) - 12) + rangeEnd.ExtendedDateTime.Hour.GetValueOrDefault(0)) / 2;
                if (medianTime <= context.AmPmInferenceCutoff)
                {
                    // Both fields should be PM (set second to be PM)
                    rangeEnd.ExtendedDateTime.FlipAmPm();
                    refTime = refTime.AddHours(12);
                }

                rangeBegin.ExtendedDateTime = MatchRelativeTo(rangeBegin.ExtendedDateTime, refTime, Normalization.Past);
            }
            else
            {
                // By default, resolve the first time in terms of the second using simple logic
                rangeBegin.ExtendedDateTime = MatchRelativeTo(rangeBegin.ExtendedDateTime, refTime, Normalization.Past);
            }
            return true;
        }

        /// <summary>
        /// Given a pair of TimexMatches, attempts to resolve all ambiguity and format the final range values as a pair of ExtendedDateTime values.
        /// </summary>
        /// <param name="rangeBegin">The beginning of the time range</param>
        /// <param name="rangeEnd">The end of the time range</param>
        /// <param name="context">The normalization context to use for the inference rules</param>
        /// <returns>A DateTimeRange containing 0, 1, or 2 ExtendedDateTime objects specifying a range, depending on how the inference went</returns>
        private static DateTimeRange ApplyRangeInferenceRules(TimexMatch rangeBegin, TimexMatch rangeEnd, TimexContext context)
        {
            DateTimeRange returnVal = new DateTimeRange();

            // Is one of them flagged as inappropriate for time ranges (by the timex RANGE_HINT attribute)?
            if (!rangeBegin.ExtendedDateTime.ValidForRanges &&
                !rangeEnd.ExtendedDateTime.ValidForRanges)
            {
#if TIME_RANGES_DEBUG
                    Debug.WriteLine("Both times are marked as range invalid");
#endif
                return returnVal;
            }
            else if (!rangeBegin.ExtendedDateTime.ValidForRanges)
            {
#if TIME_RANGES_DEBUG
                    Debug.WriteLine("Start time is marked as range invalid");
#endif
                returnVal.StartTime = rangeEnd;
                return returnVal;
            }
            else if (!rangeEnd.ExtendedDateTime.ValidForRanges)
            {
#if TIME_RANGES_DEBUG
                    Debug.WriteLine("End time is marked as range invalid");
#endif
                returnVal.StartTime = rangeBegin;
                return returnVal;
            }

            // Is the range anchored to at least one PRESENT_REF? If so, apply special inference rules
            if (UseInferenceOnPresentReferences(rangeBegin, rangeEnd, context, ref returnVal))
            {
                return returnVal;
            }

            // Both fields appear to be of the same type of reference. Is one more defined than the other?
            float firstCount = GetFlagWeight(rangeBegin.ExtendedDateTime.ExplicitSetParts);
            float secondCount = GetFlagWeight(rangeEnd.ExtendedDateTime.ExplicitSetParts);

            // Catch comparisons that occur between offsets and weekdays (like "wednesday to next friday"). Change the anchor date in this case.
            if (IsWeekdayOffsetException(rangeBegin.ExtendedDateTime) &&
                rangeEnd.ExtendedDateTime.SetParts.HasFlag(DateTimeParts.WeekDay))
            {
                firstCount = FLAG_WEIGHT_OVERRIDE;
            }

            if (IsWeekdayOffsetException(rangeEnd.ExtendedDateTime) &&
                rangeBegin.ExtendedDateTime.SetParts.HasFlag(DateTimeParts.WeekDay))
            {
                secondCount = FLAG_WEIGHT_OVERRIDE;
            }
        
            // Set the default return values
            if (firstCount >= secondCount)
            {
                returnVal.StartTime = rangeBegin;
            }
            else
            {
                returnVal.StartTime = rangeEnd;
            }

            // TODO: Abort when mod=approx?
            bool success = false;
            if (firstCount >= secondCount)
            {
                success = ApplyInferenceLeftToRight(rangeBegin, rangeEnd, context);
            }
            else
            {
                success = ApplyInferenceRightToLeft(rangeBegin, rangeEnd, context);
            }

            // If inference failed, return the default return value now.
            if (!success)
            {
                return returnVal;
            }

            // Make sure that the start time really comes before the end time.
            // If not, then some kind of mismatch occurred. In this case, don't return a range, just settle on one time. (or just switch them?)
            // For complete comparison, convert their values into absolute times beforehand
            ExtendedDateTime startTime = DateTimeParsers.TryParseISOIntoExtendedDateTime(rangeBegin.ExtendedDateTime.FormatValue(), context);
            ExtendedDateTime endTime = DateTimeParsers.TryParseISOIntoExtendedDateTime(rangeEnd.ExtendedDateTime.FormatValue(), context);
            if (startTime != null && endTime != null &&
                startTime.CompareTo(endTime) < 0)
            {
#if TIME_RANGES_DEBUG
                    Debug.WriteLine("Start time does not come before end time. Discontinuing range interpretation.");
#endif
                // The most specific value will be returned as start time in this case
            }
            else
            {
                returnVal.StartTime = rangeBegin;
                returnVal.EndTime = rangeEnd;
            }
    
            return returnVal;
        }

        /// <summary>
        /// Processes a context string, and uses the given timex object to extract useful information that may be used in time range resolution.
        /// If any information is found, it will be added to the passed-in set of TimexMatches.
        /// </summary>
        /// <param name="contextMatches">A set of TimexMatches extracted from the context</param>
        /// <param name="finalMatches">All the other matches. This list may be modified in-place</param>
        private static void ProcessContext(IList<TimexMatch> contextMatches, ref IList<TimexMatch> finalMatches)
        {
            // For now, all we care about are part of day hints. Grab the first one we see.
            TimexMatch contextMatch = contextMatches.FirstOrDefault(item => item.ExtendedDateTime.IsPartOfDayOnly());

            // Halt if nothing found
            if (contextMatch == null)
                return;

            // If we found something, append it to the main list of matches (TODO: Should they be prepended in RTL-ordered languages?)
            // Ensure that the context string was not already matched inside the main input
            bool alreadyMatched = finalMatches.Any(item => item.Value == contextMatch.Value);

            if (!alreadyMatched)
            {
                finalMatches.Add(contextMatch);
            }
        }

        /// <summary>
        /// Inspects a set of TimexMatches. If there is exactly one match of type Duration, and all of the
        /// others indicate nothing more specific than a time of day, returns the duration match.
        /// Otherwise return nullptr
        /// </summary>
        /// <param name="matchSet">A set of TimexMatches</param>
        /// <returns>A single duration match from the given set, or nullptr</returns>
        private static TimexMatch ExtractOnlyDurationMatch(IList<TimexMatch> matchSet)
        {
            TimexMatch returnVal = null;

            foreach (TimexMatch iter in matchSet)
            {
                if (iter.ExtendedDateTime.TemporalType == TemporalType.Duration)
                {
                    if (returnVal == null)
                    {
                        returnVal = iter;
                    }
                    else
                    {
                        return null;
                    }
                }
                else if (!iter.ExtendedDateTime.IsPartOfDayOnly())
                {
                    return null;
                }
            }
            return returnVal;
        }

        /// <summary>
        /// Attempts to handle cases such as "the next 10 minutes". If the only significant time expression in matchSet is a duration, then
        /// convert it into a time range spanning from now to the time offset specified (i.e. ten minutes from now).
        /// If this is successful, the range will be stored in returnVal and this returns true. Otherwise, this returns false.
        /// </summary>
        /// <param name="matchSet">A set of TimexMatches</param>
        /// <param name="context">The normalization context to use. This will determine whether to interpret the duration as the last X minutes or the next X minutes</param>
        /// <param name="returnVal">(out) The inferred date range value, if processing was succesful</param>
        /// <returns>True if duration inference rules can be successfully applied</returns>
        private static bool UseInferenceOnDurations(IList<TimexMatch> matchSet, TimexContext context, out DateTimeRange returnVal)
        {
            // Special case: If there is only duration one match that is not a part-of-day hint, convert it into an offset and return it.
            TimexMatch durationMatch = ExtractOnlyDurationMatch(matchSet);
            returnVal = new DateTimeRange();

            // Honor the RANGE_HINT attribute to make sure this type of duration can actually be used to express a time range
            if (durationMatch != null && durationMatch.ExtendedDateTime.ValidForRanges)
            {
#if TIME_RANGES_DEBUG
                    Debug.WriteLine("Triggered duration interpretation");
#endif
                ExtendedDateTime absoluteTime = durationMatch.ExtendedDateTime.ConvertDurationIntoOffset(context);
                if (absoluteTime != null)
                {
                    TimexMatch newMatch = new TimexMatch()
                        {
                            Id = durationMatch.Id,
                            Index = durationMatch.Index,
                            RuleId = durationMatch.RuleId,
                            Value = durationMatch.Value,
                            ExtendedDateTime = absoluteTime
                        };

                    // Construct a PRESENT_REF time out of thin air (it was never said in the input, only implied)
                    IDictionary<string, string> newTimexDictionary = new Dictionary<string, string>();
                    newTimexDictionary[Iso8601.Reference] = EnumExtensions.ToString(DateTimeReference.Present);
                    ExtendedDateTime presentTime = ExtendedDateTime.Create(
                        newMatch.ExtendedDateTime.TemporalType,
                        newTimexDictionary,
                        context);

                    if (context.Normalization == Normalization.Past)
                    {
                        returnVal.StartTime = newMatch;
                        returnVal.EndTime = new TimexMatch(){
                            ExtendedDateTime = presentTime,
                            Id = 1,
                            RuleId = "Internal inference",
                            Index = 0,
                            Value = string.Empty
                        };
                    }
                    else
                    {
                        returnVal.StartTime = new TimexMatch()
                        {
                            ExtendedDateTime = presentTime,
                            Id = 0,
                            RuleId = "Internal inference",
                            Index = 0,
                            Value = string.Empty
                        };
                        returnVal.EndTime = newMatch;
                    }

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Inspects a list of TimexMatches and returns true if the given list is not likely to contain a valid time
        /// range. For example, the combination of "Tomorrow" and "The week of September 30th" is invalid.
        /// </summary>
        /// <param name="timexMatchesVector">A vector of TimexMatch objects that were matched from an input string</param>
        /// <returns>True if invalid constructions are found</returns>
        private static bool ContainsInvalidConstructions(IList<TimexMatch> timexMatchesVector)
        {
            // Check for invalid combinations of flags
            bool containsPresentRef = false;
            bool containsDuration = false;
            bool containsFixedDate = false;
            bool containsPartOfDay = false;
            bool containsWeek = false;
            bool containsPartOfYear = false;
            bool containsDecadeYear = false;

            foreach (TimexMatch iter in timexMatchesVector)
            {
                ExtendedDateTime time = iter.ExtendedDateTime;
                if (time.IsPartOfDayOnly())
                {
                    containsPartOfDay = true;
                }
                if (time.TemporalType == TemporalType.Duration)
                {
                    containsDuration = true;
                }
                if (time.Reference == DateTimeReference.Present)
                {
                    containsPresentRef = true;
                }
                if (time.SetParts.HasFlag(DateTimeParts.Week) || time.SetParts.HasFlag(DateTimeParts.WeekOfExpression))
                {
                    containsWeek = true;
                }
                if (time.SetParts.HasFlag(DateTimeParts.Day) || time.SetParts.HasFlag(DateTimeParts.WeekDay) || time.SetParts.HasFlag(DateTimeParts.Month))
                {
                    containsFixedDate = true;
                }
                if (time.PartOfYear != PartOfYear.None)
                {
                    containsPartOfYear = true;
                }
                if (time.SetParts.HasFlag(DateTimeParts.Decade) ||
                    time.SetParts.HasFlag(DateTimeParts.DecadeYear) ||
                    time.SetParts.HasFlag(DateTimeParts.Century) || 
                    time.SetParts.HasFlag(DateTimeParts.Millenium))
                {
                    containsDecadeYear = true;
                }
            }

            // Example: "6 months old now"
            if (containsPresentRef && containsDuration && containsFixedDate)
            {
                return true;
            }

            // Example: "between now and lunch"
            if (containsPresentRef && containsPartOfDay && !containsFixedDate)
            {
                return true;
            }

            // Example: "Between tomorrow and next week"
            if (containsFixedDate && containsWeek)
            {
                return true;
            }

            // Example: "Since the mid nineties"
            if (containsDecadeYear)
            {
                return true;
            }

            // Example: "4th quarter 2011"
            if (containsPartOfYear)
            {
                return true;
            }

            return false;
        }

#endregion
    }
}
