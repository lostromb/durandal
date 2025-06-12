using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.LG;
using Durandal.Common.NLP.Tagging;
using Durandal.Common.Utils;
using Durandal.Common.MathExt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Durandal.Common.Time;

namespace Durandal.Common.NLP.Alignment
{
    /// <summary>
    /// Static algorithms to determine optimal alignments between two token strings, with varying levels of complexity
    /// </summary>
    public static class LexicalAlignment
    {
        /// <summary>
        /// Detects numerical strings, which are considered to be equal in certain cases
        /// </summary>
        private static readonly Regex NUMBER_MATCHER = new Regex("^\\d+$");

        /// <summary>
        /// used to mark tokens which are tagged but have zero length, which can happen for optional phrase components
        /// </summary>
        internal static readonly string NULL_TOKEN = "_NULL_TOKEN";

        private static bool StringEquals(string a, string b, StringComparison comparisonType = StringComparison.OrdinalIgnoreCase)
        {
            if (NUMBER_MATCHER.IsMatch(a) && NUMBER_MATCHER.IsMatch(b))
            {
                return true;
            }

            return string.Equals(a, b, comparisonType);
        }

        /// <summary>
        /// Runs the edit distance algorithm between two lists of strings, and calculates the word-level alignment result between them.
        /// This is used to align the lexical form of a sentence with its display form, to allow us to get tag-specific lexical strings
        /// extracted from speech reco results.
        /// </summary>
        /// <param name="tokenStringA"></param>
        /// <param name="tokenStringB"></param>
        /// <returns></returns>
        public static AlignmentStep[] Align(IList<string> tokenStringA, IList<string> tokenStringB)
        {
            // A genetic algorithm "optimized" these values to Insert=46 Offset=33 Edit=59 but I don't trust those at all
            const int InsertCost = 20;
            const int OffsetCost = 20;
            const int EditCost = 10;

            // The old magic box
            AlignmentNode[][] magicBox = new AlignmentNode[tokenStringA.Count + 1][];
            for (int y = 0; y < tokenStringA.Count + 1; y++)
            {
                magicBox[y] = new AlignmentNode[tokenStringB.Count + 1];
            }

            magicBox[0][0] = new AlignmentNode()
            {
                Score = 0,
                Distance = 0,
                Step = AlignmentStep.None,
                Backpointer = null
            };

            // Initialize the top edge
            for (int x = 1; x <= tokenStringA.Count; x++)
            {
                magicBox[x][0] = new AlignmentNode()
                {
                    Score = x * InsertCost,
                    Distance = x,
                    Step = AlignmentStep.Add,
                    Backpointer = magicBox[x - 1][0]
                };
            }

            magicBox[0][0].Backpointer = null;

            for (int y = 1; y <= tokenStringB.Count; y++)
            {
                // Initialize the left edge
                magicBox[0][y] = new AlignmentNode()
                {
                    Score = y * InsertCost,
                    Distance = y,
                    Step = AlignmentStep.Skip,
                    Backpointer = magicBox[0][y - 1]
                };

                // Iterate through the DP table
                for (int x = 1; x <= tokenStringA.Count; x++)
                {
                    bool matchExact = true;
                    AlignmentNode diag = magicBox[x - 1][y - 1];
                    int diagWeight = diag.Score;
                    bool eq = StringEquals(tokenStringA[x - 1], tokenStringB[y - 1]);
                    if (!eq)
                    {
                        diagWeight += EditCost;
                        matchExact = false;
                    }

                    AlignmentNode left = magicBox[x - 1][y];
                    int leftWeight = left.Score;
                    if (!eq)
                    {
                        leftWeight += OffsetCost;
                    }
                    else
                    {
                        leftWeight += InsertCost;
                    }

                    AlignmentNode up = magicBox[x][y - 1];
                    int upWeight = up.Score;
                    if (!eq)
                    {
                        upWeight += OffsetCost;
                    }
                    else
                    {
                        upWeight += InsertCost;
                    }

                    if (diagWeight < leftWeight && diagWeight < upWeight)
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

            AlignmentNode endAlignment = magicBox[tokenStringA.Count][tokenStringB.Count];

            AlignmentNode iter = endAlignment;
            AlignmentStep[] returnVal = new AlignmentStep[endAlignment.Distance];
            for (int c = endAlignment.Distance - 1; c >= 0; c--)
            {
                returnVal[c] = iter.Step;
                iter = iter.Backpointer;
            }

            return returnVal;
        }

        private static AlignmentStep[] Align(IList<LexicalGraphNode> tokenStringA, IList<LexicalGraphNode> tokenStringB)
        {
            const int InsertCost = 20;
            const int OffsetCost = 20;
            const int EditCost = 10;

            // The old magic box
            AlignmentNode[][] magicBox = new AlignmentNode[tokenStringA.Count + 1][];
            for (int y = 0; y < tokenStringA.Count + 1; y++)
            {
                magicBox[y] = new AlignmentNode[tokenStringB.Count + 1];
            }

            magicBox[0][0] = new AlignmentNode()
            {
                Score = 0,
                Distance = 0,
                Step = AlignmentStep.None,
                Backpointer = null
            };

            // Initialize the top edge
            for (int x = 1; x <= tokenStringA.Count; x++)
            {
                magicBox[x][0] = new AlignmentNode()
                {
                    Score = x * InsertCost,
                    Distance = x,
                    Step = AlignmentStep.Add,
                    Backpointer = magicBox[x - 1][0]
                };
            }

            magicBox[0][0].Backpointer = null;

            for (int y = 1; y <= tokenStringB.Count; y++)
            {
                // Initialize the left edge
                magicBox[0][y] = new AlignmentNode()
                {
                    Score = y * InsertCost,
                    Distance = y,
                    Step = AlignmentStep.Skip,
                    Backpointer = magicBox[0][y - 1]
                };

                // Iterate through the DP table
                for (int x = 1; x <= tokenStringA.Count; x++)
                {
                    bool matchExact = true;
                    int tagBenefit;
                    bool eq = LexicalGraphNode.TokenEquals(tokenStringA[x - 1], tokenStringB[y - 1], out tagBenefit);
                    AlignmentNode diag = magicBox[x - 1][y - 1];
                    int diagWeight = diag.Score;
                    if (!eq)
                    {
                        diagWeight += EditCost;
                        matchExact = false;
                    }

                    diagWeight += tagBenefit;

                    AlignmentNode left = magicBox[x - 1][y];
                    int leftWeight = left.Score;
                    if (!eq)
                    {
                        leftWeight += OffsetCost;
                    }
                    else
                    {
                        leftWeight += InsertCost;
                    }

                    leftWeight += tagBenefit;

                    AlignmentNode up = magicBox[x][y - 1];
                    int upWeight = up.Score;
                    if (!eq)
                    {
                        upWeight += OffsetCost;
                    }
                    else
                    {
                        upWeight += InsertCost;
                    }

                    upWeight += tagBenefit;

                    if (diagWeight < leftWeight && diagWeight < upWeight)
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

            AlignmentNode endAlignment = magicBox[tokenStringA.Count][tokenStringB.Count];

            AlignmentNode iter = endAlignment;
            AlignmentStep[] returnVal = new AlignmentStep[endAlignment.Distance];
            for (int c = endAlignment.Distance - 1; c >= 0; c--)
            {
                returnVal[c] = iter.Step;
                iter = iter.Backpointer;
            }

            return returnVal;
        }

        private static void CorrelateChains(List<LexicalGraphNode> chain1, List<LexicalGraphNode> chain2)
        {
            if (chain1.Count == 0 || chain2.Count == 0)
                return;

            AlignmentStep[] alignment = Align(chain1, chain2);

            List<LexicalGraphNode> topRow = new List<LexicalGraphNode>();
            List<LexicalGraphNode> bottomRow = new List<LexicalGraphNode>();

            int idx1 = 0;
            int idx2 = 0;
            LexicalGraphNode top = chain1[idx1];
            LexicalGraphNode bottom = chain2[idx2];
            foreach (AlignmentStep step in alignment)
            {
                bool advance1 = false;
                bool advance2 = false;

                if (step == AlignmentStep.Match || step == AlignmentStep.Edit)
                {
                    ProcessCorrelationGroups(topRow, bottomRow);

                    topRow.Add(top);
                    bottomRow.Add(bottom);

                    advance1 = true;
                    advance2 = true;
                }
                else if (step == AlignmentStep.Add)
                {
                    topRow.Add(top);
                    advance1 = true;
                }
                else if (step == AlignmentStep.Skip)
                {
                    bottomRow.Add(bottom);
                    advance2 = true;
                }

                if (advance1)
                {
                    idx1++;
                    if (idx1 < chain1.Count)
                    {
                        top = chain1[idx1];
                    }
                }

                if (advance2)
                {
                    idx2++;
                    if (idx2 < chain2.Count)
                    {
                        bottom = chain2[idx2];
                    }
                }
            }

            // Finish out the last group
            ProcessCorrelationGroups(topRow, bottomRow);
        }

        private static void ProcessCorrelationGroups(IList<LexicalGraphNode> topRow, IList<LexicalGraphNode> bottomRow)
        {
            if (topRow.Count > 1 && bottomRow.Count > 1)
            {
                // The alignments are all muddled. Clear all the correlations and reset.
                topRow.Clear();
                bottomRow.Clear();
                return;
            }

            if (topRow.Count > 0 && bottomRow.Count > 0)
            {
                LexicalGraphNode source = null;
                IList<LexicalGraphNode> targets = null;

                // Clear out the previous group if needed
                if (topRow.Count == 1)
                {
                    // Top-forking-down pattern
                    source = topRow[0];
                    targets = bottomRow;
                }
                else if (bottomRow.Count == 1)
                {
                    // Bottom-forking-up pattern
                    source = bottomRow[0];
                    targets = topRow;
                }
                else
                {
                    // With the check at the start of this function this should never happen
                    throw new Exception("Lexical correlation failure: correlation groups do not follow a specific direction");
                }

                float inc = 1.0f / (float)targets.Count;
                foreach (LexicalGraphNode target in targets)
                {
                    float factor = inc;
                    //if (source.Token.Equals(target.Token))
                    //    factor *= 2.0f;
                    source.Connections.Increment(target, factor);
                    source.TokenCount.Increment(target.Token);
                    target.Connections.Increment(source, factor);
                    target.TokenCount.Increment(source.Token);
                }

                topRow.Clear();
                bottomRow.Clear();
            }
        }

        /// <summary>
        /// Converts a tagged sentence into a chain of lexical graph nodes, optionally
        /// inserting extra tokens to facilitate alignment later on
        /// </summary>
        /// <param name="taggedData"></param>
        /// <returns></returns>
        private static List<LexicalGraphNode> BuildChain(TaggedSentence taggedData)
        {
            Sentence utterance = taggedData.Utterance;
            List<LexicalGraphNode> returnVal = new List<LexicalGraphNode>();
            string curTag = "O";
            int nonTokenIdx = 0;

            for (int curToken = 0; curToken < utterance.Length; curToken++)
            {
                // First thing, detect if the transition to the next node will change our tag
                IList<string> tagSet = taggedData.Words[curToken].Tags;
                string nextTag = tagSet.Count == 0 ? "O" : tagSet[0];
                if (!string.Equals(curTag, nextTag) && 
                    (curToken == 0 || (!string.Equals("O", curTag) && !string.Equals("O", nextTag))))
                {
                    // Capture the whitespace before this token as a separate token outside of the tag
                    // This helps us a lot later if we have things like tags adjacent to each other or the
                    // string bounds, as we can guarantee the whitespace won't get overwritten by the tag substitution
                    LexicalGraphNode emptyNode = new LexicalGraphNode(string.Empty);
                    emptyNode.PreSpace = utterance.NonTokens[nonTokenIdx++];
                    returnVal.Add(emptyNode);
                }

                curTag = nextTag;

                // If the input data has explicitly tagged an empty string, we substitute the special null token so we can differentiate it later
                string thisWord = utterance.Words[curToken];
                if (string.IsNullOrEmpty(thisWord) && !string.Equals(curTag, "O"))
                {
                    thisWord = NULL_TOKEN;
                }

                LexicalGraphNode next = new LexicalGraphNode(thisWord);

                bool reachedEnd = curToken >= (utterance.Length - 1);

                // Now capture the whitespace around the token
                if (!string.Equals("O", curTag))
                {
                    // If we're inside a tag just use basic catch-up logic
                    if (nonTokenIdx <= curToken)
                    {
                        next.PreSpace = utterance.NonTokens[nonTokenIdx++];
                    }
                }
                else
                {
                    if (nonTokenIdx <= curToken)
                    {
                        next.PreSpace = utterance.NonTokens[nonTokenIdx++];
                    }

                    // Inspect the next token to see if we are about to go to another tag
                    // Only use PostSpace if we are outside a tag about to enter one
                    bool aboutToEnterTag = reachedEnd;
                    if (!reachedEnd)
                    {
                        tagSet = taggedData.Words[curToken + 1].Tags;
                        nextTag = tagSet.Count == 0 ? string.Empty : tagSet[0];
                        aboutToEnterTag = !string.Equals("O", nextTag) && !string.Equals(curTag, nextTag);
                    }

                    if (aboutToEnterTag && (!reachedEnd || string.Equals("O", curTag)))
                    {
                        next.PostSpace = utterance.NonTokens[nonTokenIdx++];
                    }
                }

                returnVal.Add(next);

                if (reachedEnd && !string.Equals("O", curTag))
                {
                    // We ended the sentence on a tag. We need to add a new token in this case
                    LexicalGraphNode emptyNode = new LexicalGraphNode(string.Empty);
                    emptyNode.PreSpace = utterance.NonTokens[nonTokenIdx++];
                    returnVal.Add(emptyNode);
                }
            }

            return returnVal;
        }

        /// <summary>
        /// Performs a complicated series of alignments which aims to convert a set of similar-looking input strings
        /// into a minimal-sized chain of decisions whose permutations will constitute the entire input space.
        /// Think of this operation as being the inverse of template expansion, where we have an expanded data set and we
        /// want to reduce it back to a template. This function works on a best-effort model which is not guaranteed
        /// to succeed. Check log errors and warnings to determine any points of failure
        /// </summary>
        /// <param name="inputs">The set of input strings to be aligned. If this data is tagged, those tags will be factored into the alignment</param>
        /// <param name="wordBreaker">A wordbreaker for parsing the input tokens</param>
        /// <param name="logger">A logger for warnings</param>
        /// <param name="numCrossCorrelations">The number of cross-correlations to perform on the data. More equals higher quality alignment. 3-6 is usually good</param>
        /// <param name="debugOutput">Flag to output verbose logging</param>
        /// <returns>An alignment result object containing the parsed data and its final aligned form, expressed as LGSurfaceForm tokens. An empty object may be returned if no high-confidence alignment could be found.</returns>
        public static GroupingAlignmentResult PerformGroupingAlignment(IEnumerable<string> inputs, IWordBreaker wordBreaker, ILogger logger, int numCrossCorrelations = 4, bool debugOutput = false)
        {
            List<TaggedSentence> taggedInputs;
            List<List<LexicalGraphNode>> chains = ParseAlignmentInputs(inputs, wordBreaker, out taggedInputs, logger);
            int inputCount = chains.Count;

            // If zero inputs, return an empty result
            if (inputCount == 0)
            {
                return new GroupingAlignmentResult()
                {
                    Groups = new List<LGSurfaceForm[]>(),
                    TaggedInputs = new List<TaggedSentence>()
                };
            }

            // Run a bunch of alignments between different input lines to form a weighted graph connecting all tokens in those inputs
            RunCrossCorrelations(inputCount, numCrossCorrelations, chains);

            // Now create a spanning forest which greedily selects the highest-weighted edges until the entire token space is spanned
            ISet<SpanningTreeEdge> finalEdges = CreateSpanningTree(chains, logger);

            // Now we have the spanning forest expressed as a set of edges. Divide each unique tree in the forest into a "cluster"
            int clusterCount = ClusterSpanningChains(chains, finalEdges, logger, debugOutput);

            // And then convert those clusters from a graph into a linear form that matches the LG surface form
            ISet<LGSurfaceForm>[] finalGroups = LinearizeClusters(chains, clusterCount, logger);

            GroupingAlignmentResult returnVal = new GroupingAlignmentResult()
            {
                Groups = new List<LGSurfaceForm[]>(),
                TaggedInputs = taggedInputs
            };

            foreach (ISet<LGSurfaceForm> group in finalGroups)
            {
                returnVal.Groups.Add(group.ToArray());
            }

            return returnVal;
        }

        /// <summary>
        /// Performs wordbreaking and tag parsing on the list of input training strings
        /// and convert them into chains lf lexical graph nodes
        /// </summary>
        /// <param name="inputs"></param>
        /// <param name="wordBreaker"></param>
        /// <param name="taggedInputs"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        private static List<List<LexicalGraphNode>> ParseAlignmentInputs(IEnumerable<string> inputs, IWordBreaker wordBreaker, out List<TaggedSentence> taggedInputs, ILogger logger)
        {
            List<List<LexicalGraphNode>> returnVal = new List<List<LexicalGraphNode>>();
            taggedInputs = new List<TaggedSentence>();

            // Parse all inputs
            foreach (string i in inputs)
            {
                if (returnVal.Count > 200)
                {
                    logger.Log("The lexical alignment engine cannot process more than 200 inputs at a time. Please split your data into smaller chunks first", LogLevel.Wrn);
                    return returnVal;
                }

                TaggedSentence tagged = TaggedDataSplitter.ParseTags(i, wordBreaker, true);
                taggedInputs.Add(tagged);
                List<LexicalGraphNode> chain = BuildChain(tagged);

                returnVal.Add(chain);

                // Apply tags to hln nodes
                int chainIter = 0;
                foreach (TaggedWord word in tagged.Words)
                {
                    // Skip over the empty "shim" tokens we may have inserted earlier
                    while (chainIter < chain.Count && string.IsNullOrEmpty(chain[chainIter].Token))
                    {
                        chainIter++;
                    }

                    if (chainIter < chain.Count)
                    {
                        // We only support one tag at a time for LG, for obvious reasons
                        if (word.Tags.Count > 0 && !word.Tags[0].Equals("O"))
                        {
                            chain[chainIter].Tag = word.Tags[0];
                        }

                        chainIter++;
                    }
                }
            }

            return returnVal;
        }

        /// <summary>
        /// Performs correlation between sampled random chains and incrementing the neighbor counts on each chain
        /// which will be used to determine correlation groups later on
        /// </summary>
        /// <param name="inputCount"></param>
        /// <param name="numCrossCorrelations"></param>
        /// <param name="chains"></param>
        private static void RunCrossCorrelations(int inputCount, int numCrossCorrelations, List<List<LexicalGraphNode>> chains)
        {
            if (inputCount <= 1)
            {
                return;
            }

            IRandom random = new FastRandom(inputCount);
            
            // don't run redundant correlations
            numCrossCorrelations = Math.Max(1, Math.Min(inputCount - 1, numCrossCorrelations));

            // Run cross-correlation between each input and a random set of others
            // OPT: cache the correlation results?
            for (int idx = 0; idx < inputCount; idx++)
            {
                int otherIdx;
                for (int o = 0; o < numCrossCorrelations; o++)
                {
                    do
                    {
                        otherIdx = random.NextInt(0, inputCount);
                    } while (otherIdx == idx);

                    CorrelateChains(chains[idx], chains[otherIdx]);
                }
            }
        }

        /// <summary>
        /// Create a greedy spanning tree across the chains in order to form correlation groups
        /// </summary>
        /// <param name="chains"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        private static ISet<SpanningTreeEdge> CreateSpanningTree(List<List<LexicalGraphNode>> chains, ILogger logger)
        {
            ISet<SpanningTreeEdge> allEdges = new HashSet<SpanningTreeEdge>();
            int totalNodes = 0;
            const float LARGE_VALUE = 10000f;
            foreach (List<LexicalGraphNode> root in chains)
            {
                int nodeIter = 0;
                while (nodeIter < root.Count)
                {
                    LexicalGraphNode node = root[nodeIter];

                    // Manually insert edges between all tokens within the same tag group (horizontally)
                    if (nodeIter + 1 < root.Count && !string.IsNullOrEmpty(node.Tag) && node.Tag.Equals(root[nodeIter + 1].Tag))
                    {
                        LexicalGraphNode next = root[nodeIter + 1];
                        node.Connections.Increment(next, LARGE_VALUE);
                        node.TokenCount.Increment(next.Token, LARGE_VALUE);
                        next.Connections.Increment(node, LARGE_VALUE);
                        next.TokenCount.Increment(node.Token, LARGE_VALUE);
                    }

                    // And now make edges along the correlation lines (vertically)
                    foreach (KeyValuePair<LexicalGraphNode, float> connectionCount in node.Connections)
                    {
                        LexicalGraphNode conn = connectionCount.Key;
                        // Do not allow edges to cross slot boundaries
                        if ((!string.IsNullOrEmpty(node.Tag) || !string.IsNullOrEmpty(conn.Tag)) && !string.Equals(node.Tag, conn.Tag))
                        {
                            continue;
                        }

                        SpanningTreeEdge edge = new SpanningTreeEdge()
                        {
                            A = node,
                            B = conn
                        };

                        if (!allEdges.Contains(edge))
                        {
                            edge.Score = node.TokenCount.GetCount(conn.Token) + conn.TokenCount.GetCount(node.Token);
                            // Are the two tokens the same tag?
                            if ((!string.IsNullOrEmpty(node.Tag) && !string.IsNullOrEmpty(conn.Tag)) && string.Equals(node.Tag, conn.Tag))
                            {
                                edge.Score += LARGE_VALUE;
                            }

                            if (edge.Score > 0)
                            {
                                allEdges.Add(edge);
                            }
                            else
                            {
                                logger.Log("Zero-score connection between " + node.Token + " => " + conn.Token, LogLevel.Wrn);
                            }
                        }
                    }

                    totalNodes++;
                    nodeIter++;
                }
            }

            List<SpanningTreeEdge> sortedEdges = new List<SpanningTreeEdge>(allEdges);
            sortedEdges.Sort((a, b) => Math.Sign(b.Score - a.Score));

            ISet<SpanningTreeEdge> finalEdges = new HashSet<SpanningTreeEdge>();
            int nodesSpanned = 0;

            int edgeIdx = 0;
            while (nodesSpanned < totalNodes && edgeIdx < sortedEdges.Count)
            {
                SpanningTreeEdge edge = sortedEdges[edgeIdx];
                //if (!edge.A.Visited || !edge.B.Visited)
                {
                    finalEdges.Add(edge);
                }

                if (!edge.A.Visited)
                {
                    edge.A.Visited = true;
                    nodesSpanned++;
                }

                if (!edge.B.Visited)
                {
                    edge.B.Visited = true;
                    nodesSpanned++;
                }

                edgeIdx++;
            }

            if (nodesSpanned < totalNodes && chains.Count > 1)
            {
                // If we did not span all nodes, we are in trouble. (unless we're in the degenerate case where there's only one input)
                // Solution: Create an edge from that node to itself?
                // Or just let the group resolver use the default group value, which will cause that to be grouped by default
                logger.Log("Spanning tree did not span entire token space; this will lead to orphaned groups. This is usually caused by bad alignment of tokens", LogLevel.Wrn);
            }

            return finalEdges;
        }

        /// <summary>
        /// Minimizes the spanning tree into a set of linear clusters
        /// </summary>
        /// <param name="chains"></param>
        /// <param name="spanningTree"></param>
        /// <param name="logger"></param>
        /// <param name="debug"></param>
        /// <returns>The number of clusters created</returns>
        private static int ClusterSpanningChains(List<List<LexicalGraphNode>> chains, ISet<SpanningTreeEdge> spanningTree, ILogger logger, bool debug)
        {
            foreach (List<LexicalGraphNode> chain in chains)
            {
                foreach (LexicalGraphNode node in chain)
                {
                    node.Visited = false;
                    node.GroupNum = -1;
                }
            }

            int groupNum = 0;
            bool more = true;
            for (int tok = 0; more; tok++)
            {
                more = false;
                foreach (List<LexicalGraphNode> chain in chains)
                {
                    if (tok < chain.Count)
                    {
                        LexicalGraphNode node = chain[tok];
                        if (RecurseSpanningTree(node, spanningTree, groupNum))
                        {
                            // True means we found an unassociated node, so we made a new group for it
                            groupNum++;
                        }

                        more = true;
                    }
                }
            }

            if (debug)
            {
                foreach (List<LexicalGraphNode> chain in chains)
                {
                    PooledStringBuilder debugger = StringBuilderPool.Rent();
                    debugger.Builder.Append("CHAIN: ");
                    foreach (LexicalGraphNode node in chain)
                    {
                        debugger.Builder.Append(node.Token + "(" + node.GroupNum + ")\t");
                    }

                    logger.Log(PooledLogEvent.Create(
                        logger.ComponentName,
                        debugger,
                        LogLevel.Vrb,
                        HighPrecisionTimer.GetCurrentUTCTime(),
                        logger.TraceId,
                        DataPrivacyClassification.SystemMetadata));
                }
            }

            return groupNum;
        }

        private static bool RecurseSpanningTree(LexicalGraphNode node, ISet<SpanningTreeEdge> minimalTree, int thisGroupNum)
        {
            if (node.Visited)
                return false;

            node.Visited = true;
            node.GroupNum = thisGroupNum;

            foreach (KeyValuePair<LexicalGraphNode, float> other in node.Connections)
            {
                SpanningTreeEdge edge = new SpanningTreeEdge()
                {
                    A = node,
                    B = other.Key
                };

                if (minimalTree.Contains(edge)/* || node.Token.Equals(other.Token)*/)
                {
                    RecurseSpanningTree(other.Key, minimalTree, thisGroupNum);
                }
            }

            return true;
        }

        /// <summary>
        /// Converts clustered chains in to a single linear branched chain that represents all possible cluster variations
        /// </summary>
        /// <param name="chains"></param>
        /// <param name="clusterCount"></param>
        /// <param name="columnCount"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        private static int[] InterpretClusterTransitions(List<List<LexicalGraphNode>> chains, int clusterCount, out int columnCount, ILogger logger)
        {
            int STKN = clusterCount;
            int ETKN = clusterCount + 1;

            // Discover the possible left-to-right transitions between each group
            // Index = source group, value set = target groups
            List<int>[] groupTransitions = new List<int>[clusterCount + 2];
            // We also need to make a "reverse group transition" graph which helps map strings of common groups
            // Maps from target group to sources which go to it
            // Index = target group, value set = source groups
            ISet<int>[] groupTransitionsInverse = new ISet<int>[groupTransitions.Length];

            for (int c = 0; c < groupTransitions.Length; c++)
            {
                groupTransitions[c] = new List<int>();
                groupTransitionsInverse[c] = new HashSet<int>();
            }

            foreach (List<LexicalGraphNode> chain in chains)
            {
                for (int tok = -1; tok < chain.Count; tok++)
                {
                    int source = STKN;
                    int target = ETKN;
                    if (tok >= 0)
                        source = chain[tok].GroupNum;
                    if (tok < chain.Count - 1)
                        target = chain[tok + 1].GroupNum;

                    if (source != target)
                    {
                        if (!groupTransitions[source].Contains(target))
                        {
                            groupTransitions[source].Add(target);
                        }

                        groupTransitionsInverse[target].Add(source);
                    }
                }
            }

            // Now turn those into a flat structure by mapping groups into "columns"
            int startGroup = STKN;
            int columnIdx = 0;
            int[] groupToColumn = new int[clusterCount];
            while (startGroup != ETKN)
            {
                int count = groupTransitions[startGroup].Count;

                // Make a new column for this token regardless of what comes next
                if (startGroup != STKN)
                {
                    groupToColumn[startGroup] = columnIdx++;
                }
                if (count == 1)
                {
                    // Simple case. Just make a new column and iterate
                    startGroup = groupTransitions[startGroup][0];
                }
                else if (count > 1)
                {
                    List<int> choices = groupTransitions[startGroup];
                    // It's a fork. Try all possibilities and find the nearest common group #
                    int nextCommonGroup = choices[0];
                    // Determine this by finding the next node with more than 1 input
                    while (groupTransitionsInverse[nextCommonGroup].Count == 1 && nextCommonGroup != ETKN)
                    {
                        // If we have more than 2 choices going forwards, this is an unsupported case
                        if (groupTransitions[nextCommonGroup].Count > 1)
                        {
                            logger.Log("Too many LG sentence forks! We're in trouble", LogLevel.Err);
                        }

                        nextCommonGroup = groupTransitions[nextCommonGroup][0];
                    }

                    // Now assign columns to all groups between start and end
                    int maxColumn = columnIdx;
                    foreach (int root in choices)
                    {
                        int thisNode = root;
                        int thisColumn = columnIdx;
                        int loopCounter = 0;
                        while (thisNode != nextCommonGroup && loopCounter++ <= ETKN)
                        {
                            if (thisNode >= groupToColumn.Length)
                            {
                                logger.Log("Found an orphaned alignment column! This usually means one of the alignment inputs has fewer tags than the others", LogLevel.Wrn);
                                columnCount = columnIdx;
                                return groupToColumn;
                            }

                            groupToColumn[thisNode] = thisColumn++;
                            if (thisColumn > maxColumn)
                            {
                                maxColumn = thisColumn;
                            }

                            int rightmostTarget = -1;
                            foreach (int x in groupTransitions[thisNode])
                                if (x > rightmostTarget)
                                    rightmostTarget = x;

                            // FIXME: I should just enforce that thisNode is only allowed to increase, rather than
                            // trying to escape the infinite loop after the fact

                            thisNode = rightmostTarget;
                        }

                        if (loopCounter > ETKN)
                        {
                            logger.Log("Infinite loop detected while trying to straighten columns", LogLevel.Wrn);
                        }
                    }

                    columnIdx = maxColumn;
                    startGroup = nextCommonGroup;
                }
            }

            columnCount = columnIdx;
            return groupToColumn;
        }

        private static ISet<LGSurfaceForm>[] LinearizeClusters(List<List<LexicalGraphNode>> chains, int clusterCount, ILogger logger)
        {
            int columnCount;
            int[] groupToColumn = InterpretClusterTransitions(chains, clusterCount, out columnCount, logger);

            ISet<LGSurfaceForm>[] finalGroups = new ISet<LGSurfaceForm>[columnCount];
            for (int c = 0; c < columnCount; c++)
            {
                finalGroups[c] = new HashSet<LGSurfaceForm>();
            }

            //Dictionary<int, HashSet<int>> validTransitions = new Dictionary<int, HashSet<int>>();

            foreach (List<LexicalGraphNode> chain in chains)
            {
                LGSurfaceForm current = new LGSurfaceForm();
                int curColumn = 0;
                foreach (LexicalGraphNode node in chain)
                {
                    int thisColumn = groupToColumn[node.GroupNum];
                    if (thisColumn != curColumn && curColumn < columnCount)
                    {
                        // Finalize previous group
                        if (!finalGroups[curColumn].Contains(current))
                        {
                            finalGroups[curColumn].Add(current);
                        }

                        // Keep track of all column transitions, to detect if we skip over certain columns
                        //HashSet<int> validTargets;
                        //if (!validTransitions.TryGetValue(curColumn, out validTargets))
                        //{
                        //    validTargets = new HashSet<int>();
                        //    validTransitions[curColumn] = validTargets;
                        //}
                        //if (!validTargets.Contains(thisColumn))
                        //{
                        //    validTargets.Add(thisColumn);
                        //}

                        current = new LGSurfaceForm();
                        curColumn = thisColumn;
                    }

                    // Now add current token to cur
                    current.Tokens.Add(node.ConvertToLgToken());
                }

                // Finalize final group
                if (current.Length > 0 && curColumn < columnCount)
                {
                    if (!finalGroups[curColumn].Contains(current))
                    {
                        finalGroups[curColumn].Add(current);
                    }
                }
            }

            // Detect if we need to insert empty tokens to allow the lattice to match back up
            // (i.e. to give the tree an option to output an entire token group conditionally, which some patterns do)
            // We do this by inspecting all the transitions in the chains and detecting "jumps" where a column was sometimes skipped
            //for (int firstColumn = 0; firstColumn < columnCount; firstColumn++)
            //{
            //    // Find the transitions from this node
            //    HashSet<int> firstColumnTargets;
            //    if (validTransitions.TryGetValue(firstColumn, out firstColumnTargets))
            //    {
            //        // And now find all the transitions from those target nodes, and intersect their targets
            //        if (firstColumnTargets.Count > 1)
            //        {
            //            HashSet<int> intersection = new HashSet<int>(firstColumnTargets);
            //            foreach (int secondColumn in firstColumnTargets)
            //            {
            //                HashSet<int> secondColumnTargets;
            //                HashSet<int> blah = new HashSet<int>();
            //                if (validTransitions.TryGetValue(secondColumn, out secondColumnTargets))
            //                {
            //                    blah.Add(secondColumn);
            //                    blah.UnionWith(secondColumnTargets);
            //                    intersection.IntersectWith(blah);
            //                }
            //            }

            //            // If the intersection set is non-empty, negate it to find out which columns were skipped over
            //            if (intersection.Count > 0)
            //            {
            //                HashSet<int> skippedColumns = new HashSet<int>(firstColumnTargets);
            //                skippedColumns.ExceptWith(intersection);
            //                foreach (int skippedColumn in skippedColumns)
            //                {
            //                    if (finalGroups[skippedColumn].Count > 0)
            //                    {
            //                        // HACKHACK: To get whitespace looking right, copy the first leading whitespace field from the non-empty token in this group
            //                        LGSurfaceForm nonEmptyGroup = finalGroups[skippedColumn].First();
            //                        string preSpace = string.Empty;
            //                        if (nonEmptyGroup.Length > 0)
            //                        {
            //                            preSpace = nonEmptyGroup.Tokens[0].NonTokensPre;
            //                        }

            //                        LGSurfaceForm emptyToken = new LGSurfaceForm();
            //                        emptyToken.Tokens.Add(new LGToken(string.Empty, preSpace, string.Empty));
            //                        finalGroups[skippedColumn].Add(emptyToken);
            //                    }
            //                }
            //            }
            //        }
            //    }
            //}

            return finalGroups;
        }
    }
}
