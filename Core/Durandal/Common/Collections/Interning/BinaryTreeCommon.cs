using Durandal.Common.Compression.BZip2;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Collections.Interning
{
    /// <summary>
    /// Functions to help build binary trees for sealed internalizers.
    /// </summary>
    internal static class BinaryTreeCommon
    {
        /// <summary>
        /// Returns the length of the longest entry in the list, to use for making an initial length table for
        /// linear internalizer implementations.
        /// </summary>
        /// <typeparam name="T">The type of entries used for the array; doesn't really matter here.</typeparam>
        /// <param name="allEntries">The entries that we are using to build our tree.</param>
        /// <returns>The length of the longest entry in the list, or -1 if the list is empty</returns>
        internal static int GetLongestEntryLength<T>(IReadOnlyCollection<KeyValuePair<InternedKey<ReadOnlyMemory<T>>, ReadOnlyMemory<T>>> allEntries)
        {
            int maxLength = -1;
            foreach (var entry in allEntries)
            {
                maxLength = Math.Max(maxLength, entry.Value.Length);
            }

            return maxLength;
        }

        internal static int GetLongestEntryLength(IReadOnlyCollection<KeyValuePair<InternedKey<ReadOnlyMemory<char>>, string>> allEntries)
        {
            int maxLength = -1;
            foreach (var entry in allEntries)
            {
                maxLength = Math.Max(maxLength, entry.Value.Length);
            }

            return maxLength;
        }

        /// <summary>
        /// Calculates the minimum length required to construct a perfect hash table out of the
        /// lengths of the given input arrays. That is, calculate the minimum value N such that
        /// the set of { foreach (entry.Length) % N } is unique.
        /// </summary>
        /// <typeparam name="T">The type of entries used for the array; doesn't really matter here.</typeparam>
        /// <param name="allEntries">The entries that we are using to build our tree.</param>
        /// <returns>The minimum length required to construct a perfect hash table from the given arrays' lengths.</returns>
        internal static int CalculateMinimumLengthTableSizePowerOfTwo<T>(IReadOnlyCollection<KeyValuePair<InternedKey<ReadOnlyMemory<T>>, ReadOnlyMemory<T>>> allEntries)
        {
            HashSet<int> uniqueLengths = new HashSet<int>();
            foreach (var entry in allEntries)
            {
                if (!uniqueLengths.Contains(entry.Value.Length))
                {
                    uniqueLengths.Add(entry.Value.Length);
                }
            }

            HashSet<int> scratchIntegerSet = new HashSet<int>();
            int initialTableLength = (int)FastMath.RoundUpToPowerOf2((uint)Math.Max(1, uniqueLengths.Count));
            bool initialTableLengthOk;
            do
            {
                initialTableLengthOk = true;
                scratchIntegerSet.Clear();
                foreach (int length in uniqueLengths)
                {
                    int hash = length % initialTableLength;
                    if (scratchIntegerSet.Contains(hash))
                    {
                        initialTableLength *= 2;
                        initialTableLengthOk = false;
                        break;
                    }
                    else
                    {
                        scratchIntegerSet.Add(hash);
                    }
                }
            } while (!initialTableLengthOk);

            return initialTableLength;
        }

        /// <summary>
        /// Creates the initial (length-indexed) table for a binary partition tree,
        /// appends to the end of that the recursive nodes for the rest of the tree,
        /// copies all values to the given value table, and returns the modified
        /// node and value tables as output.
        /// </summary>
        /// <typeparam name="T">The type of tree being built.</typeparam>
        /// <param name="allEntries">The list of all entries that will populate this binary tree.</param>
        /// <param name="initialTableLength">The length to use for the initial level of the tree.</param>
        /// <param name="nodeTable">The array which will hold the created nodes for the tree.</param>
        /// <param name="valueTable">The array which will store the copied values for each tree entry.</param>
        /// <param name="lengthTable">The array which contains the input lengths for each entry in the initial table</param>
        /// <param name="allKeysUnique">Whether every ordinal key in the input set is unique (thus allowing 1:1 reverse lookups)</param>
        /// <param name="valueInterpreter">A delegate for converting the primitive array value to an integer for calculating tree pivots.</param>
        internal static void CreateInitialTable<T>(
            KeyValuePair<InternedKey<ReadOnlyMemory<T>>, ReadOnlyMemory<T>>[] allEntries,
            int initialTableLength,
            out BinaryTreeNode[] nodeTable,
            out T[] valueTable,
            out int[] lengthTable,
            out bool allKeysUnique,
            Func<T, int> valueInterpreter,
            IRandom rand = null)
        {
            // Allocate the value table
            int valueTableLength = 1;
            for (int c = 0; c < allEntries.Length; c++)
            {
                valueTableLength += allEntries[c].Value.Length;
            }

            valueTable = new T[valueTableLength];
            // Insert a single padding value at the start of the data table.
            // This is used later to distinguish between "null/not found" and
            // "empty span with ordinal of zero" value nodes later on.
            // Offseting the value table index by 1 ensures that even the
            // empty span will have a data offset > 0.
            int valueTableIdx = 1;

            // Initialize the in-progress table
            List<BinaryTreeNode> inProgressTable = new List<BinaryTreeNode>();
            for (int c = 0; c < initialTableLength; c++)
            {
                inProgressTable.Add(new BinaryTreeNode());
            }

            rand = rand ?? new FastRandom(444);
            Array.Sort(allEntries, (a, b) => a.Value.Length - b.Value.Length);
            int startSetIdx = 0;
            int endSetIdx;

            while (startSetIdx < allEntries.Length)
            {
                // zoom to end of the string set that all has the same length
                KeyValuePair<InternedKey<ReadOnlyMemory<T>>, ReadOnlyMemory<T>> firstEntry = allEntries[startSetIdx];
                for (endSetIdx = startSetIdx + 1;
                    endSetIdx < allEntries.Length &&
                    allEntries[endSetIdx].Value.Length == firstEntry.Value.Length;
                    endSetIdx++) ;

                if (endSetIdx == startSetIdx + 1)
                {
                    // Only 1 entry with this length.
                    int dataPtr = valueTableIdx;
                    firstEntry.Value.Span.CopyTo(valueTable.AsSpan(valueTableIdx));
                    valueTableIdx += firstEntry.Value.Length;
                    inProgressTable[firstEntry.Value.Length % initialTableLength] =
                        BinaryTreeNode.CreateLeafNode(dataPtr, firstEntry.Value.Length, firstEntry.Key.Key);
                }
                else
                {
                    // Multiple entries. Recurse
                    // And update the existing table entry with the one created by recursion
                    inProgressTable[firstEntry.Value.Length % initialTableLength] = CreateTreeRecursive(
                        allEntries,
                        startSetIdx,
                        endSetIdx - startSetIdx,
                        ref inProgressTable,
                        valueTable,
                        ref valueTableIdx,
                        rand,
                        valueInterpreter);
                }

                startSetIdx = endSetIdx;
            }

            nodeTable = inProgressTable.ToArray();

            // create the expected length table based on the lengths of all entries
            // use -1 for all invalid entries in this table
            lengthTable = new int[initialTableLength];
            lengthTable.AsSpan().Fill(-1);

            // Also determine if the key set is unique while we're iterating here
            allKeysUnique = true;
            HashSet<int> uniqueKeys = new HashSet<int>();

            foreach (var entry in allEntries)
            {
                lengthTable[entry.Value.Length % initialTableLength] = entry.Value.Length;

                if (uniqueKeys.Contains(entry.Key.Key))
                {
                    allKeysUnique = false;
                }
                else
                {
                    uniqueKeys.Add(entry.Key.Key);
                }
            }
        }

        // Hack method for stats comparison
        internal static void CreateInitialTable<T>(
            KeyValuePair<InternedKey<ReadOnlyMemory<T>>, ReadOnlyMemory<T>>[] allEntries,
            int initialTableLength,
            out BinaryTreeNode[] nodeTable,
            out T[] valueTable,
            out int[] lengthTable,
            out bool allKeysUnique,
            Func<T, int> valueInterpreter,
            out StatisticalSet pivotJumps,
            out StatisticalSet balance)
        {
            // Allocate the value table
            int valueTableLength = 1;
            for (int c = 0; c < allEntries.Length; c++)
            {
                valueTableLength += allEntries[c].Value.Length;
            }

            pivotJumps = new StatisticalSet();
            balance = new StatisticalSet();

            valueTable = new T[valueTableLength];
            // Insert a single padding value at the start of the data table.
            // This is used later to distinguish between "null/not found" and
            // "empty span with ordinal of zero" value nodes later on.
            // Offseting the value table index by 1 ensures that even the
            // empty span will have a data offset > 0.
            int valueTableIdx = 1;

            // Initialize the in-progress table
            List<BinaryTreeNode> inProgressTable = new List<BinaryTreeNode>();
            for (int c = 0; c < initialTableLength; c++)
            {
                inProgressTable.Add(new BinaryTreeNode());
            }

            IRandom rand = new FastRandom(444);
            Array.Sort(allEntries, (a, b) => a.Value.Length - b.Value.Length);
            int startSetIdx = 0;
            int endSetIdx;

            while (startSetIdx < allEntries.Length)
            {
                // zoom to end of the string set that all has the same length
                KeyValuePair<InternedKey<ReadOnlyMemory<T>>, ReadOnlyMemory<T>> firstEntry = allEntries[startSetIdx];
                for (endSetIdx = startSetIdx + 1;
                    endSetIdx < allEntries.Length &&
                    allEntries[endSetIdx].Value.Length == firstEntry.Value.Length;
                    endSetIdx++) ;

                if (endSetIdx == startSetIdx + 1)
                {
                    // Only 1 entry with this length.
                    int dataPtr = valueTableIdx;
                    firstEntry.Value.Span.CopyTo(valueTable.AsSpan(valueTableIdx));
                    valueTableIdx += firstEntry.Value.Length;
                    inProgressTable[firstEntry.Value.Length % initialTableLength] =
                        BinaryTreeNode.CreateLeafNode(dataPtr, firstEntry.Value.Length, firstEntry.Key.Key);
                }
                else
                {
                    // Multiple entries. Recurse
                    // And update the existing table entry with the one created by recursion
                    inProgressTable[firstEntry.Value.Length % initialTableLength] = CreateTreeRecursive(
                        allEntries,
                        startSetIdx,
                        endSetIdx - startSetIdx,
                        ref inProgressTable,
                        valueTable,
                        ref valueTableIdx,
                        rand,
                        valueInterpreter);

                    GetTreeStatistics(
                        inProgressTable[firstEntry.Value.Length % initialTableLength],
                        inProgressTable,
                        0,
                        ref pivotJumps,
                        ref balance);
                }

                startSetIdx = endSetIdx;
            }

            nodeTable = inProgressTable.ToArray();

            // create the expected length table based on the lengths of all entries
            // use -1 for all invalid entries in this table
            lengthTable = new int[initialTableLength];
            lengthTable.AsSpan().Fill(-1);

            // Also determine if the key set is unique while we're iterating here
            allKeysUnique = true;
            HashSet<int> uniqueKeys = new HashSet<int>();

            foreach (var entry in allEntries)
            {
                lengthTable[entry.Value.Length % initialTableLength] = entry.Value.Length;

                if (uniqueKeys.Contains(entry.Key.Key))
                {
                    allKeysUnique = false;
                }
                else
                {
                    uniqueKeys.Add(entry.Key.Key);
                }
            }
        }

        internal static int GetTreeStatistics(
            BinaryTreeNode treeRoot,
            List<BinaryTreeNode> subNodes,
            int lastPivotIdx,
            ref StatisticalSet pivotJumps,
            ref StatisticalSet balance)
        {
            if (treeRoot.IsBranchNode)
            {
                pivotJumps.Add(treeRoot.PivotIndex - lastPivotIdx);
                int leftWeight = GetTreeStatistics(subNodes[treeRoot.SubtableIndex], subNodes, treeRoot.PivotIndex, ref pivotJumps, ref balance);
                int rightWeight = GetTreeStatistics(subNodes[treeRoot.SubtableIndex + 1], subNodes, treeRoot.PivotIndex, ref pivotJumps, ref balance);
                balance.Add(Math.Abs((float)(rightWeight - leftWeight) / (float)(rightWeight + leftWeight)));
                return leftWeight + rightWeight;
            }
            else
            {
                return 1;
            }
        }

        internal static void CreateInitialTable(
            KeyValuePair<InternedKey<ReadOnlyMemory<char>>, string>[] allEntries,
            int initialTableLength,
            out BinaryTreeNode[] nodeTable,
            out string[] valueTable,
            out int[] lengthTable,
            out bool allKeysUnique,
            Func<char, int> valueInterpreter,
            out StatisticalSet pivotJumps,
            out StatisticalSet balance)
        {
            // Allocate the value table
            int valueTableLength = 1 + allEntries.Length;
            valueTable = new string[valueTableLength];
            // Insert a single padding value at the start of the data table.
            // This is used later to distinguish between "null/not found" and
            // "empty span with ordinal of zero" value nodes later on.
            // Offseting the value table index by 1 ensures that even the
            // empty span will have a data offset > 0.
            valueTable[0] = null;
            int valueTableIdx = 1;

            pivotJumps = new StatisticalSet();
            balance = new StatisticalSet();

            // Initialize the in-progress table
            List<BinaryTreeNode> inProgressTable = new List<BinaryTreeNode>();
            for (int c = 0; c < initialTableLength; c++)
            {
                inProgressTable.Add(new BinaryTreeNode());
            }

            IRandom rand = new FastRandom(444);
            Array.Sort(allEntries, (a, b) => a.Value.Length - b.Value.Length);
            int startSetIdx = 0;
            int endSetIdx;

            while (startSetIdx < allEntries.Length)
            {
                // zoom to end of the string set that all has the same length
                KeyValuePair<InternedKey<ReadOnlyMemory<char>>, string> firstEntry = allEntries[startSetIdx];
                for (endSetIdx = startSetIdx + 1;
                    endSetIdx < allEntries.Length &&
                    allEntries[endSetIdx].Value.Length == firstEntry.Value.Length;
                    endSetIdx++) ;

                if (endSetIdx == startSetIdx + 1)
                {
                    // Only 1 entry.
                    int dataPtr = valueTableIdx;
                    valueTable[valueTableIdx++] = firstEntry.Value;
                    inProgressTable[firstEntry.Value.Length % initialTableLength] =
                        BinaryTreeNode.CreateLeafNode(dataPtr, firstEntry.Value.Length, firstEntry.Key.Key);
                }
                else
                {
                    // Multiple entries. Recurse
                    // And update the existing table entry with the one created by recursion

                    // Get statistics about the data set first to try and identify likely pivot clusters
                    //StatisticalSet[] stats = new StatisticalSet[firstEntry.Value.Length];
                    //for (int charIdx = 0; charIdx < firstEntry.Value.Length; charIdx++)
                    //{
                    //    stats[charIdx] = new StatisticalSet(endSetIdx - startSetIdx + 1);
                    //    for (int entry = startSetIdx; entry < endSetIdx; entry++)
                    //    {
                    //        stats[charIdx].Add(valueInterpreter(allEntries[entry].Value[charIdx]));
                    //    }
                    //}

                    // Calculate the pivot tree first
                    float cost;
                    PivotNode pivotNode;
                    if (endSetIdx - startSetIdx <= 12)
                    {
                        pivotNode = CreatePivotTree_Exhaustive_SLOW(allEntries, startSetIdx, endSetIdx - startSetIdx, valueInterpreter, 0, out cost);
                    }
                    else
                    {
                        pivotNode = CreatePivotTree_Balanced(allEntries, startSetIdx, endSetIdx - startSetIdx, valueInterpreter, 0, out cost);
                    }

                    inProgressTable[firstEntry.Value.Length % initialTableLength] = CreateTreeRecursive(
                        pivotNode,
                        allEntries,
                        startSetIdx,
                        endSetIdx - startSetIdx,
                        ref inProgressTable,
                        valueTable,
                        ref valueTableIdx,
                        rand,
                        valueInterpreter);

                    GetTreeStatistics(
                        inProgressTable[firstEntry.Value.Length % initialTableLength],
                        inProgressTable,
                        0,
                        ref pivotJumps,
                        ref balance);
                }

                startSetIdx = endSetIdx;
            }

            nodeTable = inProgressTable.ToArray();

            // create the expected length table based on the lengths of all entries
            // use -1 for all invalid entries in this table
            lengthTable = new int[initialTableLength];
            lengthTable.AsSpan().Fill(-1);

            // Also determine if the key set is unique while we're iterating here
            allKeysUnique = true;
            HashSet<int> uniqueKeys = new HashSet<int>();

            foreach (var entry in allEntries)
            {
                lengthTable[entry.Value.Length % initialTableLength] = entry.Value.Length;

                if (uniqueKeys.Contains(entry.Key.Key))
                {
                    allKeysUnique = false;
                }
                else
                {
                    uniqueKeys.Add(entry.Key.Key);
                }
            }
        }

        /// <summary>
        /// Recursively creates binary tree nodes, appending them to the in progress table, and returning
        /// a copy of the value of the head of the first generated table.
        /// </summary>
        /// <typeparam name="T">The type of value being stored in this tree.</typeparam>
        /// <param name="entries">All of the entries that we are adding to this table.</param>
        /// <param name="startIdx">The starting index of the entry that we are adding to the current subnode.</param>
        /// <param name="length">The length (number of entries) that we are adding to the current subnode.</param>
        /// <param name="treeNodeTable">A list to store the tree nodes which this function generates.</param>
        /// <param name="valueTable">A list to copy the actual values of each leaf node.</param>
        /// <param name="valueTableIdx">The index of the next value to be appended to the value table.</param>
        /// <param name="random">A random number generator for picking pivots.</param>
        /// <param name="valueInterpreter">A delegate for converting primitive array values to integer for pivot comparison.</param>
        /// <returns>The binary tree node (branch node) which points directly to the (2-element) table generated by this function.</returns>
        /// <exception cref="ArgumentException">If any duplicate values are encountered in the input entry list.</exception>
        private static BinaryTreeNode CreateTreeRecursive<T>(
            KeyValuePair<InternedKey<ReadOnlyMemory<T>>, ReadOnlyMemory<T>>[] entries,
            int startIdx,
            int length,
            ref List<BinaryTreeNode> treeNodeTable,
            T[] valueTable,
            ref int valueTableIdx,
            IRandom random,
            Func<T, int> valueInterpreter)
        {
            int newTableBaseIndex = treeNodeTable.Count;

            // Reserve entries in the list - this is our table that the return value points to
            treeNodeTable.Add(new BinaryTreeNode());
            treeNodeTable.Add(new BinaryTreeNode());

            // Now find a good pivot
            int bestPivotIdx = 0;
            int bestPivotValue = 0;
            int leftSubnodes;
            int rightSubnodes;
            int bestSkew = int.MaxValue;
            int randomIterBase = random.NextInt(0, entries[startIdx].Value.Length);

            // Start at a random base index and then go forward until we exhaust all possibilities
            int pivotIter;
            for (pivotIter = 0; pivotIter < entries[startIdx].Value.Length; pivotIter++)
            {
                int testPivotIdx = (pivotIter + randomIterBase) % entries[startIdx].Value.Length;

                // Estimate the median as the average of the min and max values in the set
                int max = int.MinValue;
                int min = int.MaxValue;
                for (int c = startIdx; c < startIdx + length; c++)
                {
                    int v = valueInterpreter(entries[c].Value.Span[testPivotIdx]);
                    min = Math.Min(min, v);
                    max = Math.Max(max, v);
                }

                // TODO better median calculation
                int medianVal = (int)((long)max + (long)min) / 2;

                // Evaluate the skew if we pivot along this median
                leftSubnodes = 0;
                rightSubnodes = 0;
                for (int testIdx = startIdx; testIdx < startIdx + length; testIdx++)
                {
                    if (valueInterpreter(entries[testIdx].Value.Span[testPivotIdx]) <= medianVal)
                    {
                        leftSubnodes++;
                    }
                    else
                    {
                        rightSubnodes++;
                    }
                }

                int skew = Math.Abs(leftSubnodes - rightSubnodes);
                if (skew < bestSkew)
                {
                    bestPivotIdx = testPivotIdx;
                    bestPivotValue = medianVal;
                    bestSkew = skew;
                }

                if (bestSkew <= 1)
                {
                    // nearly 50-50 balance, so stop looking for anything better
                    break;
                }
            }

            // Values within the current subrange are now all sorted by the pivot value in ascending order
            Array.Sort(entries, startIdx, length, new ArrayPivotComparer<T>(bestPivotIdx, valueInterpreter));
            leftSubnodes = 0;
            rightSubnodes = 0;

            for (int testIdx = startIdx; testIdx < startIdx + length; testIdx++)
            {
                if (valueInterpreter(entries[testIdx].Value.Span[bestPivotIdx]) <= bestPivotValue)
                {
                    leftSubnodes++;
                }
                else
                {
                    rightSubnodes++;
                }
            }

            // Detect identical values that can't be partitioned, which would lead to infinite recursion if not caught
            bool exhaustedAllPivots = pivotIter == entries[startIdx].Value.Length;
            if (exhaustedAllPivots && (leftSubnodes == 0 || rightSubnodes == 0))
            {
                throw new ArgumentException("Duplicate identical values encountered when building binary tree. Every value is required to be unique.");
            }

            if (leftSubnodes > 1)
            {
                // Create a branch node on left and recurse
                treeNodeTable[newTableBaseIndex] = CreateTreeRecursive(
                    entries,
                    startIdx,
                    leftSubnodes,
                    ref treeNodeTable,
                    valueTable,
                    ref valueTableIdx,
                    random,
                    valueInterpreter);
            }
            else if (leftSubnodes == 1)
            {
                // Create a leaf node on left
                KeyValuePair<InternedKey<ReadOnlyMemory<T>>, ReadOnlyMemory<T>> onlyEntry = entries[startIdx];
                int dataPtr = valueTableIdx;
                onlyEntry.Value.Span.CopyTo(valueTable.AsSpan(valueTableIdx));
                valueTableIdx += onlyEntry.Value.Length;
                treeNodeTable[newTableBaseIndex] =
                    BinaryTreeNode.CreateLeafNode(dataPtr, onlyEntry.Value.Length, onlyEntry.Key.Key);
            }

            if (rightSubnodes > 1)
            {
                // Create a branch node on right and recurse
                treeNodeTable[newTableBaseIndex + 1] = CreateTreeRecursive(
                    entries,
                    startIdx + leftSubnodes,
                    rightSubnodes,
                    ref treeNodeTable,
                    valueTable,
                    ref valueTableIdx,
                    random,
                    valueInterpreter);
            }
            else if (rightSubnodes == 1)
            {
                // Create a leaf node on right
                KeyValuePair<InternedKey<ReadOnlyMemory<T>>, ReadOnlyMemory<T>> onlyEntry = entries[startIdx + length - 1];
                int dataPtr = valueTableIdx;
                onlyEntry.Value.Span.CopyTo(valueTable.AsSpan(valueTableIdx));
                valueTableIdx += onlyEntry.Value.Length;
                treeNodeTable[newTableBaseIndex + 1] =
                    BinaryTreeNode.CreateLeafNode(dataPtr, onlyEntry.Value.Length, onlyEntry.Key.Key);
            }

            return BinaryTreeNode.CreateBranchNode(bestPivotIdx, bestPivotValue, newTableBaseIndex);
        }

        private class PivotNode
        {
            public int PivotIndex;
            public int PivotValue;
            public PivotNode Low;
            public PivotNode High;
            public int LowSubnodes;
            public int HighSubnodes;
            public float Skew => Math.Abs((float)(HighSubnodes - LowSubnodes) / (float)(LowSubnodes + HighSubnodes));
        }

        private static PivotNode CreatePivotTree_Balanced(
            KeyValuePair<InternedKey<ReadOnlyMemory<char>>, string>[] entries,
            int startIdx,
            int length,
            Func<char, int> valueInterpreter,
            int previousPivotIdx,
            out float cost,
            StatisticalSet statsScratch = null,
            StringPivotComparer<char> comparer = null)
        {
            cost = 0;

            if (length <= 1)
            {
                return null;
            }

            statsScratch = statsScratch ?? new StatisticalSet();
            comparer = comparer ?? new StringPivotComparer<char>(0, valueInterpreter);
            float lowestCost = float.MaxValue;

            int pivotIter;
            PivotNode returnVal = null;

            // Calculate the distinct values in each entry close to the previous pivot


            for (pivotIter = 0; pivotIter < entries[startIdx].Value.Length; pivotIter++)
            {
                int testPivotIdx = (previousPivotIdx + 1 + pivotIter) % entries[startIdx].Value.Length;

                // Find the exact imaginary median that should bisect this set
                statsScratch.Clear();
                for (int c = startIdx; c < startIdx + length; c++)
                {
                    int v = valueInterpreter(entries[c].Value[testPivotIdx]);
                    statsScratch.Add(v);
                }

                // floor is very important here because of how we define left/right comparison
                int medianVal = (int)Math.Floor(statsScratch.BisectionMedian);

                // Early check for bisection feasibility
                if (medianVal == (int)Math.Round(statsScratch.Maximum))
                {
                    continue;
                }

                // Evaluate the skew if we pivot along this median
                int leftSubnodes = 0;
                int rightSubnodes = 0;
                for (int testIdx = startIdx; testIdx < startIdx + length; testIdx++)
                {
                    if (valueInterpreter(entries[testIdx].Value[testPivotIdx]) <= medianVal)
                    {
                        leftSubnodes++;
                    }
                    else
                    {
                        rightSubnodes++;
                    }
                }

                if (leftSubnodes == 0 || rightSubnodes == 0)
                {
                    // Can't bisect the data set along this pivot; keep searching
                    continue;
                }

                // Values within the current subrange are now all sorted by the pivot value in ascending order
                comparer.PivotIndex = testPivotIdx;
                Array.Sort(entries, startIdx, length, comparer);

                float subCostLeft, subCostRight;
                PivotNode subLeft = CreatePivotTree_Balanced(
                    entries, startIdx, leftSubnodes, valueInterpreter, testPivotIdx, out subCostLeft, statsScratch, comparer);

                PivotNode subRight = CreatePivotTree_Balanced(
                    entries, startIdx + leftSubnodes, rightSubnodes, valueInterpreter, testPivotIdx, out subCostRight, statsScratch, comparer);
                float pivotIndexCost = 0;

                // do pivotIdx + 1 so we incentivize pivots that are exactly 1 space ahead
                // of the previously evaluated pivot, for data cache locality.
                // Also we penalize negative indexes with a cost to try and
                // discourage zigzagging back and forth in memory
                const float NEGATIVE_OFFSET_COST = -1.0f;
                const float SKEW_COST = 10.0f;
                if (subLeft != null)
                {
                    int pivotCost = subLeft.PivotIndex - (testPivotIdx + 1);
                    pivotIndexCost += pivotCost < 0 ? pivotCost * NEGATIVE_OFFSET_COST : pivotCost;
                }
                if (subRight != null)
                {
                    int pivotCost = subRight.PivotIndex - (testPivotIdx + 1);
                    pivotIndexCost += pivotCost < 0 ? pivotCost * NEGATIVE_OFFSET_COST : pivotCost;
                }

                // 0 is perfectly balanced, or it's +1 for each node that's off side
                int skew = Math.Abs(rightSubnodes - leftSubnodes);
                if ((length % 2) != 0)
                {
                    skew -= 1; // account for odd-length inputs that can never balance perfectly
                }

                cost = pivotIndexCost + (skew * SKEW_COST); // bad skew is far more of a problem than one or two indexes to skip
                cost += subCostLeft;
                cost += subCostRight;
                //costCurves.Add(totalCost);
                if (returnVal == null || cost < lowestCost)
                {
                    lowestCost = cost;
                    returnVal = new PivotNode()
                    {
                        Low = subLeft,
                        High = subRight,
                        PivotIndex = testPivotIdx,
                        PivotValue = medianVal,
                        LowSubnodes = leftSubnodes,
                        HighSubnodes = rightSubnodes,
                    };

                    if (cost <= 0.0001)
                    {
                        break;
                    }
                }
            }

            // Detect if our ideal pivot didn't actually bisect anything - this would cause infinite recursion
            if (returnVal == null || returnVal.LowSubnodes == 0 || returnVal.HighSubnodes == 0)
            {
                throw new ArgumentException("Duplicate identical values encountered when building binary tree. Every value is required to be unique.");
            }

            //if (length > 2)
            //{
            //    //DebugLogger.Default.Log(string.Format("N = {0} Best cost = {1:F3} Average cost {2:F3} {3:F3} Max {4:F3}",
            //    DebugLogger.Default.Log(string.Format("{0},{1:F3},{4:F3}",
            //        length,
            //        costCurves.Minimum,
            //        costCurves.Mean,
            //        costCurves.StandardDeviation,
            //        costCurves.Maximum));
            //}

            // Rebuild the actual return value without the recursion limit so we get the complete

            //returnVal = new PivotNode()
            //{
            //    Low = CreateOptimalPivotTreeRecursive(entries, startIdx, returnVal.LowSubnodes, valueInterpreter, returnVal.PivotIndex, null, out _, statsScratch, comparer),
            //    High = CreateOptimalPivotTreeRecursive(entries, startIdx + returnVal.LowSubnodes, returnVal.HighSubnodes, valueInterpreter, returnVal.PivotIndex, null, out _, statsScratch, comparer),
            //    PivotIndex = returnVal.PivotIndex,
            //    PivotValue = returnVal.PivotValue,
            //    LowSubnodes = returnVal.LowSubnodes,
            //    HighSubnodes = returnVal.HighSubnodes,
            //};

            return returnVal;
        }

        private static PivotNode CreatePivotTree_Exhaustive_SLOW(
            KeyValuePair<InternedKey<ReadOnlyMemory<char>>, string>[] entries,
            int startIdx,
            int length,
            Func<char, int> valueInterpreter,
            int previousPivotIdx,
            out float cost,
            StatisticalSet statsScratch = null,
            StringPivotComparer<char> comparer = null)
        {
            cost = 0;

            if (length <= 1)
            {
                return null;
            }

            statsScratch = statsScratch ?? new StatisticalSet();
            comparer = comparer ?? new StringPivotComparer<char>(0, valueInterpreter);
            float lowestCost = float.MaxValue;

            int pivotIter;
            PivotNode returnVal = null;

            // Iterate through all pivot indexes starting at previous pivot +1
            for (pivotIter = 0; pivotIter < entries[startIdx].Value.Length; pivotIter++)
            {
                int testPivotIdx = (previousPivotIdx + 1 + pivotIter) % entries[startIdx].Value.Length;

                // Find the exact imaginary median that should bisect this set
                statsScratch.Clear();
                for (int c = startIdx; c < startIdx + length; c++)
                {
                    int v = valueInterpreter(entries[c].Value[testPivotIdx]);
                    statsScratch.Add(v);
                }

                // floor is very important here because of how we define left/right comparison
                int medianVal = (int)Math.Floor(statsScratch.BisectionMedian);

                // Early check for bisection feasibility
                if (medianVal == (int)Math.Round(statsScratch.Maximum))
                {
                    continue;
                }

                // Evaluate the skew if we pivot along this median
                int leftSubnodes = 0;
                int rightSubnodes = 0;
                for (int testIdx = startIdx; testIdx < startIdx + length; testIdx++)
                {
                    if (valueInterpreter(entries[testIdx].Value[testPivotIdx]) <= medianVal)
                    {
                        leftSubnodes++;
                    }
                    else
                    {
                        rightSubnodes++;
                    }
                }

                if (leftSubnodes == 0 || rightSubnodes == 0)
                {
                    // Can't bisect the data set along this pivot; keep searching
                    continue;
                }

                // Values within the current subrange are now all sorted by the pivot value in ascending order
                comparer.PivotIndex = testPivotIdx;
                Array.Sort(entries, startIdx, length, comparer);

                float subCostLeft, subCostRight;
                PivotNode subLeft = CreatePivotTree_Exhaustive_SLOW(
                    entries, startIdx, leftSubnodes, valueInterpreter, testPivotIdx, out subCostLeft, statsScratch, comparer);
                if (returnVal != null && subCostLeft > lowestCost) break; // early exit

                PivotNode subRight = CreatePivotTree_Exhaustive_SLOW(
                    entries, startIdx + leftSubnodes, rightSubnodes, valueInterpreter, testPivotIdx, out subCostRight, statsScratch, comparer);
                if (returnVal != null && subCostRight > lowestCost) break; // early exit
                float pivotIndexCost = 0;

                // do pivotIdx + 1 so we incentivize pivots that are exactly 1 space ahead
                // of the previously evaluated pivot, for data cache locality.
                // Also we penalize negative indexes with a cost to try and
                // discourage zigzagging back and forth in memory
                const float NEGATIVE_OFFSET_COST = -2.0f;
                const float SKEW_COST = 10.0f;
                if (subLeft != null)
                {
                    int pivotCost = subLeft.PivotIndex - (testPivotIdx + 1);
                    pivotIndexCost += pivotCost < 0 ? pivotCost * NEGATIVE_OFFSET_COST : pivotCost;
                }
                if (subRight != null)
                {
                    int pivotCost = subRight.PivotIndex - (testPivotIdx + 1);
                    pivotIndexCost += pivotCost < 0 ? pivotCost * NEGATIVE_OFFSET_COST : pivotCost;
                }

                // 0 is perfectly balanced, or it's +1 for each node that's off side
                int skew = Math.Abs(rightSubnodes - leftSubnodes);
                if ((length % 2) != 0)
                {
                    skew -= 1; // account for odd-length inputs that can never balance perfectly
                }

                cost = pivotIndexCost + (skew * SKEW_COST); // bad skew is far more of a problem than one or two indexes to skip
                cost += subCostLeft;
                cost += subCostRight;
                if (returnVal == null || cost < lowestCost)
                {
                    lowestCost = cost;
                    returnVal = new PivotNode()
                    {
                        Low = subLeft,
                        High = subRight,
                        PivotIndex = testPivotIdx,
                        PivotValue = medianVal,
                        LowSubnodes = leftSubnodes,
                        HighSubnodes = rightSubnodes,
                    };

                    if (cost <= 0.0001)
                    {
                        break;
                    }
                }
            }

            // Detect if our ideal pivot didn't actually bisect anything - this would cause infinite recursion
            if (returnVal == null || returnVal.LowSubnodes == 0 || returnVal.HighSubnodes == 0)
            {
                throw new ArgumentException("Duplicate identical values encountered when building binary tree. Every value is required to be unique.");
            }

            return returnVal;
        }

        private static BinaryTreeNode CreateTreeRecursive(
            PivotNode rootNode,
            KeyValuePair<InternedKey<ReadOnlyMemory<char>>, string>[] entries,
            int startIdx,
            int length,
            ref List<BinaryTreeNode> treeNodeTable,
            string[] valueTable,
            ref int valueTableIdx,
            IRandom random,
            Func<char, int> valueInterpreter)
        {
            // Then create binary nodes that match that optimal pivot tree
            int newTableBaseIndex = treeNodeTable.Count;

            // Reserve entries in the list - this is our table that the return value points to
            treeNodeTable.Add(new BinaryTreeNode());
            treeNodeTable.Add(new BinaryTreeNode());

            // Values within the current subrange are now all sorted by the pivot value in ascending order
            Array.Sort(entries, startIdx, length, new StringPivotComparer<char>(rootNode.PivotIndex, valueInterpreter));

            if (rootNode.LowSubnodes > 1)
            {
                // Create a branch node on left and recurse
                BinaryTreeNode newNode = CreateTreeRecursive(
                    rootNode.Low,
                    entries,
                    startIdx,
                    rootNode.LowSubnodes,
                    ref treeNodeTable,
                    valueTable,
                    ref valueTableIdx,
                    random,
                    valueInterpreter);
                treeNodeTable[newTableBaseIndex] = newNode;
            }
            else if (rootNode.LowSubnodes == 1)
            {
                // Create a leaf node on left
                KeyValuePair<InternedKey<ReadOnlyMemory<char>>, string> onlyEntry = entries[startIdx];
                int dataPtr = valueTableIdx;
                valueTable[valueTableIdx++] = onlyEntry.Value;
                treeNodeTable[newTableBaseIndex] =
                    BinaryTreeNode.CreateLeafNode(dataPtr, onlyEntry.Value.Length, onlyEntry.Key.Key);
            }

            if (rootNode.HighSubnodes > 1)
            {
                // Create a branch node on right and recurse
                BinaryTreeNode newNode = CreateTreeRecursive(
                    rootNode.High,
                    entries,
                    startIdx + rootNode.LowSubnodes,
                    rootNode.HighSubnodes,
                    ref treeNodeTable,
                    valueTable,
                    ref valueTableIdx,
                    random,
                    valueInterpreter);
                treeNodeTable[newTableBaseIndex + 1] = newNode;
            }
            else if (rootNode.HighSubnodes == 1)
            {
                // Create a leaf node on right
                KeyValuePair<InternedKey<ReadOnlyMemory<char>>, string> onlyEntry = entries[startIdx + length - 1];
                int dataPtr = valueTableIdx;
                valueTable[valueTableIdx++] = onlyEntry.Value;
                treeNodeTable[newTableBaseIndex + 1] =
                    BinaryTreeNode.CreateLeafNode(dataPtr, onlyEntry.Value.Length, onlyEntry.Key.Key);
            }

            return BinaryTreeNode.CreateBranchNode(rootNode.PivotIndex, rootNode.PivotValue, newTableBaseIndex);
        }
    }
}
