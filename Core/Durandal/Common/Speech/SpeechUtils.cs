namespace Durandal.Common.Speech
{
    using Durandal.API;
    using Durandal.Common.Audio;
    using Durandal.Common.Collections;
    using Durandal.Common.Logger;
    using Durandal.Common.NLP;
    using Durandal.Common.NLP.Alignment;
    using Durandal.Common.Speech.TTS;
    using Durandal.Common.Utils;
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using System.Xml;
    using System.Xml.Linq;

    public static class SpeechUtils
    {
        private static readonly Regex XML_TAG_MATCHER = new Regex("<.+?>");

        /// <summary>
        /// Determines if the given string is valid SSML
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static bool IsSsml(string input)
        {
            return input.Contains("<speak");
        }

        /// <summary>
        /// Removes all SSML tags from the input
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string StripSsml(string input)
        {
            return StringUtils.RegexRemove(XML_TAG_MATCHER, input);
        }

        /// <summary>
        /// If the string is not wrapped in a speak tag, add one.
        /// If it _is_ wrapped in a speak tag, normalize its format
        /// </summary>
        /// <param name="input"></param>
        /// <param name="queryLogger">A logger for errors</param>
        /// <returns></returns>
        public static string NormalizeSsml(string input, ILogger queryLogger)
        {
            if (IsSsml(input))
            {
                if (input.Contains("&"))
                {
                    queryLogger.Log("SSML appears to contain unescaped characters; you need to xml-encode the string properly for TTS to parse it. " + input, LogLevel.Wrn);
                }

                return input;
            }

            //if (!string.IsNullOrWhiteSpace(ssml) &&
            //    !string.IsNullOrWhiteSpace(locale) &&
            //    !string.IsNullOrWhiteSpace(voiceName))
            //{
            //    var doc = XDocument.Parse(ssml);
            //    if (doc.Root != null)
            //    {
            //        var voiceElement = new XElement("voice");
            //        voiceElement.SetAttributeValue(XNamespace.Xml + "lang", locale);
            //        voiceElement.SetAttributeValue(XNamespace.Xml + "gender", "female");
            //        voiceElement.SetAttributeValue("name", voiceName);

            //        foreach (var element in doc.Root.Elements())
            //        {
            //            if (element.Name.Namespace != "http://www.w3.org/2001/mstts" && element.Name.LocalName != "audiosegment")
            //            {
            //                voiceElement.Add(element);
            //            }
            //            else
            //            {
            //                voiceElement.Value = element.Value;
            //            }
            //        }

            //        doc.Root.RemoveNodes();
            //        doc.Root.AddFirst(voiceElement);
            //        ssml = doc.ToString();
            //    }
            //}

            XElement baseElement = new XElement(XName.Get("speak"), input);
            return baseElement.ToString(SaveOptions.DisableFormatting);
        }

        //public static string WrapWithStandardSsmlTag(string text, string locale)
        //{
        //    return string.Format("<speak version=\"1.0\" " +
        //                         "xmlns=\"http://www.w3.org/2001/10/synthesis\" " +
        //                         "xmlns:mstts=\"http://www.w3.org/2001/mstts\" " +
        //                         "xmlns:emo=\"http://www.w3.org/2009/10/emotionml\" " +
        //                         "xml:lang=\"{0}\">{1}</speak>", locale, text);
        //}

        /// <summary>
        /// Given a piece of synthesized audio and the SSML string which generated it, attempt to calculate precise word timings for the entire synthesized output.
        /// </summary>
        /// <param name="speech">The synthesized speech</param>
        /// <param name="ssml">The SSML string that was used to generate the speech</param>
        /// <param name="wordBreaker">A whole word tokenizer for the current locale</param>
        /// <param name="timingEstimator">A speech timing estimator for the current locale</param>
        /// <param name="logger">A logger for debug output and warnings</param>
        /// <returns>A list of wordbroken sentence tokens with associated timing information based on lexical to audio alignment. </returns>
        public static IList<SynthesizedWord> EstimateSynthesizedWordTimings(
            AudioSample speech,
            string ssml,
            IWordBreaker wordBreaker,
            ISpeechTimingEstimator timingEstimator,
            ILogger logger)
        {
            // Normalize input
            string plainText = StripSsml(ssml);
            Sentence wordBrokenSentence = wordBreaker.Break(plainText);

            const float SILENCE_THRESHOLD = 0.0003f;

            // Find all silent regions in the generated audio
            IList<Tuple<TimeSpan, TimeSpan>> silentRegions = FindSilenceRegions(speech, SILENCE_THRESHOLD, TimeSpan.FromMilliseconds(200));
            // Remove bookends on the silent regions
            if (silentRegions.Count > 0 && silentRegions[0].Item1 == TimeSpan.Zero)
            {
                silentRegions.RemoveAt(0);
            }
            if (silentRegions.Count > 0 && silentRegions[silentRegions.Count - 1].Item1 >= speech.Duration - TimeSpan.FromMilliseconds(5))
            {
                silentRegions.RemoveAt(silentRegions.Count - 1);
            }

            // Find the length of silence at each endpoint
            int preSilenceSamples;
            for (preSilenceSamples = 0; preSilenceSamples < speech.LengthSamplesPerChannel &&
                speech.Data.Array[speech.Data.Offset + (preSilenceSamples * speech.Format.NumChannels)] < SILENCE_THRESHOLD; preSilenceSamples++) { }
            TimeSpan preSilence = AudioMath.ConvertSamplesPerChannelToTimeSpan(speech.Format.SampleRateHz, preSilenceSamples);
            
            int postSilenceSamples;
            for (postSilenceSamples = speech.LengthSamplesPerChannel - 1; postSilenceSamples > preSilenceSamples && 
                speech.Data.Array[speech.Data.Offset + (postSilenceSamples * speech.Format.NumChannels)] < SILENCE_THRESHOLD; postSilenceSamples--) { }
            TimeSpan postSilence = AudioMath.ConvertSamplesPerChannelToTimeSpan(speech.Format.SampleRateHz, speech.LengthSamplesPerChannel - postSilenceSamples);

            // Ignore pre and post silence if they take up a majority of the entire sample
            if (preSilence > speech.Duration - TimeSpan.FromMilliseconds(200))
            {
                preSilence = TimeSpan.Zero;
            }
            if (postSilence > speech.Duration - TimeSpan.FromMilliseconds(200))
            {
                postSilence = TimeSpan.Zero;
            }

            TimeSpan actualUtteranceTime = speech.Duration - (preSilence + postSilence);

            // Estimate the initial word timings
            IList<SynthesizedWord> initialWordEstimate = timingEstimator.EstimatePhraseWeights(wordBrokenSentence, ssml, actualUtteranceTime);

            // Improve upon the original estimate by aligning silence in the audio with breaks in the text
            try
            {
                IList<SynthesizedWord> refinedWordTimings = RefineWordTimingsUsingAlignmentData(silentRegions, initialWordEstimate, preSilence, actualUtteranceTime);
                return refinedWordTimings;
            }
            catch (Exception e)
            {
                // Sometimes alignment can fail. Don't blow up everything over that.
                logger.Log(e, LogLevel.Wrn);
                return initialWordEstimate;
            }
        }
        
        /// <summary>
        /// Returns a list of all regions within the input audio that are equal to or longer than minimumBreakTime and where
        /// each sample's magnitude is equal to or less than noiseGate.
        /// </summary>
        /// <param name="audio">The audio to analyze</param>
        /// <param name="noiseGate">The maximum allowed sample value to qualify as "silent"</param>
        /// <param name="minimumBreakTime">The minimum length of silence region to return</param>
        /// <returns>A list of tuples representing the beginning and ending of a region of silence</returns>
        public static IList<Tuple<TimeSpan, TimeSpan>> FindSilenceRegions(AudioSample audio, float noiseGate, TimeSpan minimumBreakTime)
        {
            List<Tuple<TimeSpan, TimeSpan>> returnVal = new List<Tuple<TimeSpan, TimeSpan>>();
            int breakTimeInSamples = (int)(minimumBreakTime.TotalSeconds * audio.Format.SampleRateHz);
            int silenceBegin = 0;
            int currentSample;
            bool inSilence = true;
            int numChannels = audio.Format.NumChannels;
            bool isCurrentlySilent;
            for (currentSample = 0; currentSample < audio.LengthSamplesPerChannel; currentSample++)
            {
                isCurrentlySilent = true;
                for (int chan = 0; chan < numChannels; chan++)
                {
                    if (audio.Data.Array[audio.Data.Offset + (currentSample * numChannels) + chan] > noiseGate)
                    {
                        isCurrentlySilent = false;
                        break;
                    }
                }

                if (!isCurrentlySilent)
                {
                    // Audio is loud
                    if (inSilence)
                    {
                        // Transitioning from silent to loud
                        if (currentSample - silenceBegin >= breakTimeInSamples)
                        {
                            // Create new region if it is long enough
                            returnVal.Add(new Tuple<TimeSpan, TimeSpan>(
                                AudioMath.ConvertSamplesPerChannelToTimeSpan(audio.Format.SampleRateHz, silenceBegin),
                                AudioMath.ConvertSamplesPerChannelToTimeSpan(audio.Format.SampleRateHz, currentSample)
                                ));
                        }
                        inSilence = false;
                    }
                    else
                    {
                        // Remaining at loud
                        silenceBegin = currentSample;
                    }
                }
                else
                {
                    // Audio is silent
                    if (!inSilence)
                    {
                        // Transitioning from loud to silent - mark beginning of silence
                        silenceBegin = currentSample;
                    }

                    inSilence = true;
                }
            }

            // Append an end region if needed
            if (currentSample - silenceBegin >= breakTimeInSamples)
            {
                returnVal.Add(new Tuple<TimeSpan, TimeSpan>(
                    AudioMath.ConvertSamplesPerChannelToTimeSpan(audio.Format.SampleRateHz, silenceBegin),
                    AudioMath.ConvertSamplesPerChannelToTimeSpan(audio.Format.SampleRateHz, currentSample)
                    ));
            }

            return returnVal;
        }

        /// <summary>
        /// Given two lists of float values representing the beginning of silent periods, attempt to align the two using the magic box edit distance algorithm
        /// </summary>
        /// <param name="offsetsA"></param>
        /// <param name="offsetsB"></param>
        /// <returns></returns>
        private static AlignmentStep[] Align(IList<int> offsetsA, IList<int> offsetsB)
        {
            // The old magic box
            AlignmentNode[][] magicBox = new AlignmentNode[offsetsA.Count + 1][];
            for (int y = 0; y < offsetsA.Count + 1; y++)
            {
                magicBox[y] = new AlignmentNode[offsetsB.Count + 1];
            }

            magicBox[0][0] = new AlignmentNode()
            {
                Score = 0,
                Distance = 0,
                Step = AlignmentStep.None,
                Backpointer = null
            };

            // Initialize the top edge
            for (int x = 1; x <= offsetsA.Count; x++)
            {
                magicBox[x][0] = new AlignmentNode()
                {
                    Score = offsetsA[x - 1],
                    Distance = x,
                    Step = AlignmentStep.Add,
                    Backpointer = magicBox[x - 1][0]
                };
            }

            magicBox[0][0].Backpointer = null;

            for (int y = 1; y <= offsetsB.Count; y++)
            {
                // Initialize the left edge
                magicBox[0][y] = new AlignmentNode()
                {
                    Score =  offsetsB[y - 1],
                    Distance = y,
                    Step = AlignmentStep.Skip,
                    Backpointer = magicBox[0][y - 1]
                };

                // Iterate through the DP table
                for (int x = 1; x <= offsetsA.Count; x++)
                {
                    int thisCellDifference = Math.Abs(offsetsA[x - 1] - offsetsB[y - 1]);
                    bool matchExact = thisCellDifference == 0;

                    AlignmentNode diag = magicBox[x - 1][y - 1];
                    int diagWeight = diag.Score + thisCellDifference;
                    AlignmentNode left = magicBox[x - 1][y];
                    int leftWeight = left.Score + thisCellDifference;
                    AlignmentNode up = magicBox[x][y - 1];
                    int upWeight = up.Score + thisCellDifference;

                    if (diagWeight <= leftWeight && diagWeight <= upWeight)
                    {
                        magicBox[x][y] = new AlignmentNode()
                        {
                            Score = diagWeight,
                            Distance = diag.Distance + 1,
                            Backpointer = diag,
                            Step = matchExact ? AlignmentStep.Match : AlignmentStep.Edit
                        };
                    }
                    else if (leftWeight < upWeight)
                    {
                        magicBox[x][y] = new AlignmentNode()
                        {
                            Score = leftWeight,
                            Distance = left.Distance + 1,
                            Backpointer = left,
                            Step = AlignmentStep.Add
                        };
                    }
                    else
                    {
                        magicBox[x][y] = new AlignmentNode()
                        {
                            Score = upWeight,
                            Distance = up.Distance + 1,
                            Backpointer = up,
                            Step = AlignmentStep.Skip
                        };
                    }
                }
            }

            AlignmentNode endAlignment = magicBox[offsetsA.Count][offsetsB.Count];

            AlignmentNode iter = endAlignment;
            AlignmentStep[] returnVal = new AlignmentStep[endAlignment.Distance];
            for (int c = endAlignment.Distance - 1; c >= 0; c--)
            {
                returnVal[c] = iter.Step;
                iter = iter.Backpointer;
            }

            return returnVal;
        }

        private class RealignedWordGrouping
        {
            public List<SynthesizedWord> Words = new List<SynthesizedWord>();
            public float AudioStart;
            public float AudioEnd;
            public bool IsDegenerate = false;
        }

        /// <summary>
        /// Performs some complicated alignment. The idea is this:
        /// We have a sample of audio that came from a synthesizer. We have an ordered list of all the periods of silence in that audio. We also have an approximation of word timings based on lexical information,
        /// including the approximate position and duration of pauses (periods, breaks, semicolons, etc.). What this function attempts to do is to align the pauses in the lexical reading of the script (the SSML text) with the actual speaking
        /// and silent regions within the synthesized audio. If the alignment is good, this gives us a highly accurate measurement of the timing and span of each spoken word, which is supremely useful
        /// in cases such as barge-in selection where the system is e.g. reading out a long list of options and the user says "That one" - we need to have accurate timing information to know exactly what it was
        /// that the user was reacting to.
        /// </summary>
        /// <param name="audioSilentRegions">A list of all the regions of silence within the input audio, EXCLUDING the endpoints (preroll and postroll silence) while still factoring presilence in the offset values</param>
        /// <param name="lexicalEstimates">An initial estimate of word timings based on a ISpeechTimingEstimator output</param>
        /// <param name="audioPresilence">The length of the presilence period in the generated audio</param>
        /// <param name="actualUtteranceAudioLength">The total actual length of the synthesized audio clip excluding the preroll and postroll silence</param>
        /// <returns>A list of synthesized tokens with timing information derived from lexical to audio alignment</returns>
        private static IList<SynthesizedWord> RefineWordTimingsUsingAlignmentData(IList<Tuple<TimeSpan, TimeSpan>> audioSilentRegions, IList<SynthesizedWord> lexicalEstimates, TimeSpan audioPresilence, TimeSpan actualUtteranceAudioLength)
        {
            if (audioSilentRegions.Count <= 1)
            {
                // no silence data. just offset everything by the presilence amount
                foreach (var est in lexicalEstimates)
                {
                    est.Offset += audioPresilence;
                }

                return lexicalEstimates;
            }

            if (lexicalEstimates.Count <= 1)
            {
                // no lexical alignment to enhance, so nothing to do here
                return lexicalEstimates;
            }

            List<int> audioSilenceStarts = new List<int>();
            foreach (Tuple<TimeSpan, TimeSpan> silentRegion in audioSilentRegions)
            {
                audioSilenceStarts.Add((int)(silentRegion.Item1.TotalMilliseconds - audioPresilence.TotalMilliseconds));
            }

            audioSilenceStarts.Sort();
            
            List<int> lexicalSilenceStarts = new List<int>();
            foreach (SynthesizedWord estimatedWordTiming in lexicalEstimates)
            {
                if (estimatedWordTiming.Word == null)
                {
                    lexicalSilenceStarts.Add((int)estimatedWordTiming.Offset.TotalMilliseconds);
                }
            }

            AlignmentStep[] steps = Align(audioSilenceStarts, lexicalSilenceStarts);

            List<RealignedWordGrouping> groupings = new List<RealignedWordGrouping>();

            int nextAudioSilentRegionIdx = 0;
            int currentLexicalWordIdx = 0;
            int currentAlignmentStep = 0;
            List<SynthesizedWord> currentGroup = new List<SynthesizedWord>();
            bool done = false;
            while (!done)
            {
                if (steps[currentAlignmentStep] == AlignmentStep.Add)
                {
                    // Consume a lexical block but not an audio block
                    while (currentLexicalWordIdx < lexicalEstimates.Count &&
                        lexicalEstimates[currentLexicalWordIdx].Word != null)
                    {
                        SynthesizedWord word = lexicalEstimates[currentLexicalWordIdx];
                        currentGroup.Add(word);
                        currentLexicalWordIdx++;
                    }

                    // Add lexical block to the most recently added grouping
                    if (groupings.Count == 0)
                    {
                        // This is really janky because it means we need to generate 2 groups that span the same area which we'll have to post-process later
                        groupings.Add(new RealignedWordGrouping()
                        {
                            Words = new List<SynthesizedWord>(currentGroup),
                            AudioStart = (float)audioPresilence.TotalMilliseconds,
                            AudioEnd = (float)audioSilentRegions[0].Item1.TotalMilliseconds,
                            IsDegenerate = true
                        });
                    }
                    else
                    {
                        groupings[groupings.Count - 1].Words.FastAddRangeList(currentGroup);
                    }

                    currentGroup.Clear();
                }
                else if (steps[currentAlignmentStep] == AlignmentStep.Skip)
                {
                    // Consume an audio block but not a lexical block
                    if (nextAudioSilentRegionIdx == 0)
                    {
                        groupings[groupings.Count - 1].AudioEnd = (float)audioSilentRegions[0].Item1.TotalMilliseconds;
                    }
                    else if (nextAudioSilentRegionIdx >= audioSilentRegions.Count - 1)
                    {
                        groupings[groupings.Count - 1].AudioEnd = (float)audioPresilence.TotalMilliseconds + (float)actualUtteranceAudioLength.TotalMilliseconds;
                    }
                    else
                    {
                        groupings[groupings.Count - 1].AudioEnd = (float)(audioSilentRegions[nextAudioSilentRegionIdx].Item1).TotalMilliseconds;
                    }

                    nextAudioSilentRegionIdx++;
                }
                else if (steps[currentAlignmentStep] == AlignmentStep.Edit || steps[currentAlignmentStep] == AlignmentStep.Match)
                {
                    // Consume a lexical and audio block and make a newly created grouping
                    RealignedWordGrouping newGrouping = new RealignedWordGrouping();
                    if (lexicalEstimates[currentLexicalWordIdx].Word == null)
                    {
                        currentLexicalWordIdx++;
                    }

                    while (currentLexicalWordIdx < lexicalEstimates.Count &&
                        lexicalEstimates[currentLexicalWordIdx].Word != null)
                    {
                        SynthesizedWord word = lexicalEstimates[currentLexicalWordIdx];
                        newGrouping.Words.Add(word);
                        currentLexicalWordIdx++;
                    }

                    if (nextAudioSilentRegionIdx == 0)
                    {
                        newGrouping.AudioStart = (float)audioPresilence.TotalMilliseconds;
                        newGrouping.AudioEnd = (float)audioSilentRegions[0].Item1.TotalMilliseconds;
                    }
                    else if (nextAudioSilentRegionIdx == audioSilentRegions.Count)
                    {
                        newGrouping.AudioStart = (float)(audioSilentRegions[nextAudioSilentRegionIdx - 1].Item2).TotalMilliseconds;
                        newGrouping.AudioEnd = (float)audioPresilence.TotalMilliseconds + (float)actualUtteranceAudioLength.TotalMilliseconds;
                    }
                    else if (nextAudioSilentRegionIdx > audioSilentRegions.Count)
                    {
                        newGrouping.AudioStart = (float)(audioSilentRegions[audioSilentRegions.Count - 1].Item2).TotalMilliseconds;
                        newGrouping.AudioEnd = (float)audioPresilence.TotalMilliseconds + (float)actualUtteranceAudioLength.TotalMilliseconds;
                        newGrouping.IsDegenerate = true;
                    }
                    else
                    {
                        newGrouping.AudioStart = (float)(audioSilentRegions[nextAudioSilentRegionIdx - 1].Item2).TotalMilliseconds;
                        newGrouping.AudioEnd = (float)(audioSilentRegions[nextAudioSilentRegionIdx].Item1).TotalMilliseconds;
                    }

                    groupings.Add(newGrouping);
                    nextAudioSilentRegionIdx++;
                }
                else
                {
                    throw new ArithmeticException("Speech timing alignment data is invalid!");
                }

                currentAlignmentStep++;
                done = currentAlignmentStep == steps.Length;
            }

            // Create a final grouping out of any stragglers
            if (currentLexicalWordIdx < lexicalEstimates.Count)
            {
                RealignedWordGrouping newGrouping = new RealignedWordGrouping();
                if (lexicalEstimates[currentLexicalWordIdx].Word == null)
                {
                    currentLexicalWordIdx++;
                }

                while (currentLexicalWordIdx < lexicalEstimates.Count)
                {
                    SynthesizedWord word = lexicalEstimates[currentLexicalWordIdx];
                    newGrouping.Words.Add(word);
                    currentLexicalWordIdx++;
                }

                if (nextAudioSilentRegionIdx == 0)
                {
                    newGrouping.AudioStart = (float)audioPresilence.TotalMilliseconds;
                    newGrouping.AudioEnd = (float)audioSilentRegions[0].Item1.TotalMilliseconds;
                }
                else if (nextAudioSilentRegionIdx == audioSilentRegions.Count)
                {
                    newGrouping.AudioStart = (float)(audioSilentRegions[nextAudioSilentRegionIdx - 1].Item2).TotalMilliseconds;
                    newGrouping.AudioEnd = (float)audioPresilence.TotalMilliseconds + (float)actualUtteranceAudioLength.TotalMilliseconds;
                }
                else if (nextAudioSilentRegionIdx > audioSilentRegions.Count)
                {
                    newGrouping.AudioStart = (float)(audioSilentRegions[audioSilentRegions.Count - 1].Item2).TotalMilliseconds;
                    newGrouping.AudioEnd = (float)audioPresilence.TotalMilliseconds + (float)actualUtteranceAudioLength.TotalMilliseconds;
                    newGrouping.IsDegenerate = true;
                }
                else
                {
                    newGrouping.AudioStart = (float)(audioSilentRegions[nextAudioSilentRegionIdx - 1].Item2).TotalMilliseconds;
                    newGrouping.AudioEnd = (float)(audioSilentRegions[nextAudioSilentRegionIdx].Item1).TotalMilliseconds;
                }

                groupings.Add(newGrouping);
                nextAudioSilentRegionIdx++;
            }

            // Fix degenerate cases - starting from the left-hand side of the grouping list
            int numDegenerates;
            int numGroupings = groupings.Count;
            for (numDegenerates = 0; numDegenerates < numGroupings && groupings[numDegenerates].IsDegenerate; numDegenerates++) ;
            // For loop wizardry; numDegenerates is now set to the index of the first non-degenerate grouping, or groupings.Count if all are degenerate
            if (numDegenerates == numGroupings)
            {
                throw new ArithmeticException("Impossible alignment detected - all groupings are degenerate");
            }

            if (numDegenerates > 0)
            {
                float start = groupings[numDegenerates].AudioStart;
                float end = groupings[numDegenerates].AudioEnd;
                float newGroupWidth = (end - start) / ((float)numDegenerates + 1);
                for (int c = 0; c <= numDegenerates; c++)
                {
                    groupings[c].AudioStart = start + (c * newGroupWidth);
                    groupings[c].AudioEnd = groupings[c].AudioStart + newGroupWidth;
                    groupings[c].IsDegenerate = false;
                }
            }

            // Now from the right-hand side
            for (numDegenerates = 0; numDegenerates < numGroupings && groupings[numGroupings - numDegenerates - 1].IsDegenerate; numDegenerates++) ;

            if (numDegenerates == numGroupings)
            {
                throw new ArithmeticException("Impossible alignment detected - all groupings are degenerate");
            }

            if (numDegenerates > 0)
            {
                float start = groupings[numGroupings - numDegenerates - 1].AudioStart;
                float end = groupings[numGroupings - numDegenerates - 1].AudioEnd;
                float newGroupWidth = (end - start) / ((float)numDegenerates + 1);
                for (int c = 0; c <= numDegenerates; c++)
                {
                    groupings[numGroupings - c - 1].AudioEnd = end - (c * newGroupWidth);
                    groupings[numGroupings - c - 1].AudioStart = groupings[numGroupings - c - 1].AudioEnd - newGroupWidth; 
                    groupings[numGroupings - c - 1].IsDegenerate = false;
                }
            }

            IList<SynthesizedWord> returnVal = new List<SynthesizedWord>();
            foreach (var grouping in groupings)
            {
                float currentOffsetMs = grouping.AudioStart;
                float currentGroupSizeAudioMs = grouping.AudioEnd - grouping.AudioStart;
                float currentGroupSizeLexicalMs = 0;
                foreach (var word in grouping.Words)
                {
                    currentGroupSizeLexicalMs += (float)word.ApproximateLength.TotalMilliseconds;
                }

                float lexicalToAudioScaleFactor = currentGroupSizeAudioMs / currentGroupSizeLexicalMs;
                foreach (SynthesizedWord word in grouping.Words)
                {
                    float scaledWordLength = (float)word.ApproximateLength.TotalMilliseconds * lexicalToAudioScaleFactor;
                    if (word.Word != null)
                    {
                        SynthesizedWord rescaledWord = new SynthesizedWord()
                        {
                            Word = word.Word,
                            Offset = TimeSpan.FromMilliseconds(currentOffsetMs),
                            ApproximateLength = TimeSpan.FromMilliseconds(scaledWordLength)
                        };

                        returnVal.Add(rescaledWord);
                    }

                    currentOffsetMs += scaledWordLength;
                }
            }

            return returnVal;
        }
    }
}
