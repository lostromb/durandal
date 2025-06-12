using Durandal.API;
using Durandal.Common.Collections.Indexing;
using Durandal.Common.File;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.NLP;
using Durandal.Common.NLP.Feature;
using Durandal.Common.NLP.Language.English;
using Durandal.Common.NLP.Tagging;
using Durandal.Common.Statistics.Classification;
using Durandal.Common.Statistics.SharpEntropy;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Durandal.Plugins.Recipe.NL
{
    public class RecipeInstructionParser
    {
        /// <summary>
        /// Matches strings containing only non-word characters, to detect garbage lines that came out of the linebreak classifier
        /// </summary>
        private static readonly Regex INVALID_LINE_MATCHER = new Regex("^[^\\w\\d]*$");

        // Outcome labels for the linebreak classifier
        private const string OUTCOME_START_LINE = "START";
        private const string OUTCOME_END_LINE = "END";
        private const string OUTCOME_CONTINUE_LINE = "CONT";

        private readonly ILogger _logger;
        private readonly string _locale;
        private readonly IWordBreaker _wordBreaker;
        private readonly ICompactIndex<string> _stringIndex;

        private GisModel _lineBreakClassifier;
        private GisModel _lineTypeClassifier;
        private RecipeCrfTagger _lineTagger;

        public RecipeInstructionParser(ILogger logger, string locale)
        {
            _logger = logger;
            _locale = locale;
            _wordBreaker = new EnglishWordBreaker();
            _stringIndex = new BasicCompactIndex<string>(new StringByteConverter());
        }

        public void Train(IEnumerable<string> trainingLines)
        {
            List<TrainingEvent> lineBreakTrainingEvents = new List<TrainingEvent>();
            List<string> lineBreakContext = new List<string>();
            foreach (string line in trainingLines)
            {
                string unescapedLine = line.Replace("\\r", "\r").Replace("\\n", "\n");
                TaggedSentence taggedTrainingData = TaggedDataSplitter.ParseTags(unescapedLine, _wordBreaker);
                bool inTag = false;
                for (int wordIndex = 0; wordIndex < taggedTrainingData.Words.Count; wordIndex++)
                {
                    lineBreakContext.Clear();
                    string outcome = OUTCOME_CONTINUE_LINE;
                    if (IsWordStartOfRecipeTag(taggedTrainingData.Words[wordIndex]))
                    {
                        outcome = OUTCOME_START_LINE;
                        inTag = true;
                    }
                    else if (wordIndex == taggedTrainingData.Words.Count - 1 || // it's last word in the line
                        (inTag && wordIndex < taggedTrainingData.Words.Count - 1 && // or it's not the last word and the next word is either no tag or start of a new tag
                        (!IsWordWithinRecipeTagBounds(taggedTrainingData.Words[wordIndex + 1]) ||
                            IsWordStartOfRecipeTag(taggedTrainingData.Words[wordIndex + 1]))
                        ))
                    {
                        outcome = OUTCOME_END_LINE;
                        inTag = false;
                    }

                    LineBreakFeatureExtractor.ExtractFeatures(taggedTrainingData.Utterance, wordIndex, ref lineBreakContext);
                    lineBreakTrainingEvents.Add(new TrainingEvent(outcome, lineBreakContext.ToArray()));
                }
            }

            GisTrainer lineBreakTrainer = new GisTrainer(_stringIndex);
            ITrainingEventReader lineBreakEventReader = new BasicTrainingEventReader(lineBreakTrainingEvents);
            ITrainingDataIndexer lineBreakIndexer = new TwoPassDataIndexer(lineBreakEventReader, new InMemoryFileSystem());
            lineBreakTrainer.TrainModel(100, lineBreakIndexer);
            _lineBreakClassifier = new GisModel(lineBreakTrainer);
            
            List<TrainingEvent> lineTypeTrainingEvents = new List<TrainingEvent>();
            HashSet<string> lineTypeContext = new HashSet<string>();

            // Count the number of "line-level" slots we have
            int totalLines = 0;
            foreach (string line in trainingLines)
            {
                string unescapedLine = line.Replace("\\r", "\r").Replace("\\n", "\n");
                TaggedData taggedTrainingData = TaggedDataSplitter.ParseSlots(unescapedLine, _wordBreaker); // OPT yeah we parse each line twice which is most likely not optimal
                foreach (SlotValue slot in taggedTrainingData.Slots)
                {
                    if (string.Equals(slot.Name, "step") ||
                        string.Equals(slot.Name, "note") ||
                        string.Equals(slot.Name, "serving_suggestion"))
                    {
                        totalLines++;
                    }
                }
            }

            List<string> taggedLineTrainingData = new List<string>();
            Regex lineBoundParser = new Regex("\\[step\\]([\\w\\W]+?)\\[\\/step\\]");
            
            int lineIdx = 1;
            foreach (string line in trainingLines)
            {
                string unescapedLine = line.Replace("\\r", "\r").Replace("\\n", "\n");
                TaggedData taggedTrainingData = TaggedDataSplitter.ParseSlots(unescapedLine, _wordBreaker);
                MatchCollection lineBoundMatches = lineBoundParser.Matches(unescapedLine);
                foreach (Match lineBoundMatch in lineBoundMatches)
                {
                    taggedLineTrainingData.Add(lineBoundMatch.Groups[1].Value);
                }

                string previousSlot = "STKN";
                foreach (SlotValue slot in taggedTrainingData.Slots)
                {
                    if (string.Equals(slot.Name, "step") ||
                        string.Equals(slot.Name, "note") ||
                        string.Equals(slot.Name, "serving_suggestion"))
                    {
                        Sentence brokenWordsInSlot = _wordBreaker.Break(slot.Value);
                        // Word
                        foreach (string word in brokenWordsInSlot.Words)
                        {
                            string feature = "wd:" + word; // todo sanitize word?
                            if (!lineTypeContext.Contains(feature))
                            {
                                lineTypeContext.Add(feature);
                            }
                        }

                        // 2-grams
                        for (int wordIdx = 0; wordIdx < brokenWordsInSlot.Words.Count - 1; wordIdx++)
                        {
                            string feature = "ng:" + brokenWordsInSlot.Words[wordIdx] + "\t" + brokenWordsInSlot.Words[wordIdx + 1];
                            if (!lineTypeContext.Contains(feature))
                            {
                                lineTypeContext.Add(feature);
                            }
                        }

                        // Previous line type
                        lineTypeContext.Add("ps:" + previousSlot);

                        // Index of line in list of all lines
                        lineTypeContext.Add("i:" + lineIdx); // index and reverse index of this span within the collection of all spans
                        lineTypeContext.Add("ri:" + (totalLines - lineIdx));
                        lineTypeTrainingEvents.Add(new TrainingEvent(slot.Name, lineTypeContext.ToArray()));
                        lineTypeContext.Clear();

                        previousSlot = slot.Name;
                        lineIdx++;
                    }
                }
            }

            GisTrainer lineTypeTrainer = new GisTrainer(_stringIndex);
            ITrainingEventReader lineTypeEventReader = new BasicTrainingEventReader(lineTypeTrainingEvents);
            ITrainingDataIndexer lineTypeIndexer = new TwoPassDataIndexer(lineTypeEventReader, new InMemoryFileSystem());
            lineTypeTrainer.TrainModel(100, lineTypeIndexer);
            _lineTypeClassifier = new GisModel(lineTypeTrainer);
            
            IStatisticalTrainer crfTrainer = new MaxEntClassifierTrainer(NullLogger.Singleton, NullFileSystem.Singleton);
            _lineTagger = new RecipeCrfTagger(crfTrainer, NullLogger.Singleton, 0.75f, NullFileSystem.Singleton, _wordBreaker);
            //_lineTagger.TrainFromData(VirtualPath.Root, VirtualPath.Root);
        }

        public void TestEvaluate(IEnumerable<string> validationLines)
        {
            foreach (string line in validationLines)
            {
                Parse(line);
            }
        }

        public ParsedRecipe Parse(string instructionsBlock)
        {
            _logger.Log("PARSING INSTRUCTIONS BLOCK");
            ParsedRecipe returnVal = new ParsedRecipe();

            string sanitizedInstructions = SanitizeRecipeInput(instructionsBlock);

            // Step 1 - Split all input sentences apart using workbreakers / tokenizers (Or possibly break using a statistical model to try and add robustness against inline periods, etc.)
            // Include empty line breaks in this output

            Sentence brokenSentence = _wordBreaker.Break(sanitizedInstructions);

            // Step 2 - Crawl through the tokens to identify when line breaks occur
            List<string> lines = new List<string>();
            StringBuilder lineBuilder = new StringBuilder();
            bool inTag = false;

            List<string> context = new List<string>();
            string lastOutcome = OUTCOME_CONTINUE_LINE;
            for (int wordIndex = 0; wordIndex < brokenSentence.Words.Count; wordIndex++)
            {
                context.Clear();
                LineBreakFeatureExtractor.ExtractFeatures(brokenSentence, wordIndex, ref context);
                float[] confidences = _lineBreakClassifier.Evaluate(context.ToArray());
                string bestOutcome = _lineBreakClassifier.GetBestOutcome(confidences);
                bool isStart = string.Equals(bestOutcome, OUTCOME_START_LINE);
                bool isEnd = string.Equals(bestOutcome, OUTCOME_END_LINE);


                if (isStart)
                {
                    if (inTag)
                    {
                        if (string.Equals(lastOutcome, OUTCOME_START_LINE))
                        {
                            // Multiple starts in a row. Finish off the previous sentence and start a new one
                            lines.Add(lineBuilder.ToString());
                            lineBuilder.Clear();
                        }
                        else
                        {
                            // A second start in the middle of a line. Ignore it and continue the line
                            lineBuilder.Append(brokenSentence.NonTokens[wordIndex]);
                            lineBuilder.Append(brokenSentence.Words[wordIndex]);
                        }
                    }
                    else
                    {
                        inTag = true;
                        lineBuilder.Append(brokenSentence.Words[wordIndex]);
                    }
                }
                else if (isEnd)
                {
                    inTag = false;
                    lineBuilder.Append(brokenSentence.NonTokens[wordIndex]);
                    lineBuilder.Append(brokenSentence.Words[wordIndex]);

                    lines.Add(lineBuilder.ToString());
                    lineBuilder.Clear();
                }
                else if (inTag)
                {
                    lineBuilder.Append(brokenSentence.NonTokens[wordIndex]);
                    lineBuilder.Append(brokenSentence.Words[wordIndex]);
                }

                lastOutcome = bestOutcome;
            }

            // Sanitize lines that contain only non-words
            List<string> temp = new List<string>();
            foreach (string line in lines)
            {
                if (!INVALID_LINE_MATCHER.IsMatch(line))
                {
                    temp.Add(line);
                }
            }

            lines = temp;
            temp = null;

            // Now classify each line type
            int totalLines = lines.Count;
            int lineIdx = 1;
            HashSet<string> lineTypeContext = new HashSet<string>();
            foreach (string line in lines)
            {
                string previousSlot = "STKN";
                Sentence brokenWordsInLine = _wordBreaker.Break(line);

                // Word
                foreach (string word in brokenWordsInLine.Words)
                {
                    string feature = "wd:" + word; // todo sanitize word?
                    if (!lineTypeContext.Contains(feature))
                    {
                        lineTypeContext.Add(feature);
                    }
                }

                // 2-grams
                for (int wordIdx = 0; wordIdx < brokenWordsInLine.Words.Count - 1; wordIdx++)
                {
                    string feature = "ng:" + brokenWordsInLine.Words[wordIdx] + "\t" + brokenWordsInLine.Words[wordIdx + 1];
                    if (!lineTypeContext.Contains(feature))
                    {
                        lineTypeContext.Add(feature);
                    }
                }

                // Previous line type
                lineTypeContext.Add("ps:" + previousSlot);

                // Index of the line within the set
                lineTypeContext.Add("i:" + lineIdx); // index and reverse index of this span within the collection of all spans
                lineTypeContext.Add("ri:" + (totalLines - lineIdx));
                float[] confidences = _lineTypeClassifier.Evaluate(lineTypeContext.ToArray());
                string bestOutcome = _lineTypeClassifier.GetBestOutcome(confidences);

                _logger.Log("PARSED LINE - TYPE " + bestOutcome + ":" + line);
                lineTypeContext.Clear();
                previousSlot = bestOutcome;
                lineIdx++;
            }

            // Step 3 - Classify each line using a maxent classifier to try and see if it is an instruction step, note, or "other".

            // Step 4 - Remove all "other" lines. Combine contiguous "Note" lines into the notes output. Reduce the remining instruction set to non-empty lines.

            // Step 5 - Apply speech normalization such as replacing "degrees F" with "degrees Fahrenheit"

            // Step 6 - Apply text formatting, remove double spaces, and generally make things pretty

            return returnVal;
        }

        private static string SanitizeRecipeInput(string input)
        {
            // Format newlines (maybe not necessary?)
            input = input.Replace("\\r", "\r");
            input = input.Replace("\\n", "\n");

            // Decode HTML entities
            input = input.Replace("&nbsp;", " ");
            input = input.Replace("&lt;", "<");
            input = input.Replace("&gt;", ">");
            input = input.Replace("&quot;", "\"");
            input = input.Replace("&apos;", "\'");

            return input;
        }

        private static bool IsWordWithinRecipeTagBounds(TaggedWord word)
        {
            return word.Tags.Contains("step") ||
                word.Tags.Contains("note") ||
                word.Tags.Contains("serving_suggestion");
        }

        private static bool IsWordStartOfRecipeTag(TaggedWord word)
        {
            return word.StartTags.Contains("step") ||
                word.StartTags.Contains("note") ||
                word.StartTags.Contains("serving_suggestion");
        }
    }
}
