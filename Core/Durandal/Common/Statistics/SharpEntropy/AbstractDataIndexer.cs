// Copyright (C) 2005 Richard J. Northedge
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later version.
//
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.

//This file is based on the AbstractDataIndexer.java source file found in the
//original java implementation of MaxEnt. 

using System;
using System.Collections.Generic;
using System.Text;
using Durandal.Common.Collections;

namespace Durandal.Common.Statistics.SharpEntropy
{
	/// <summary>
	/// Abstract base for DataIndexer implementations.
	/// </summary>
	/// <author>
	/// Tom Morton
	/// </author>
	/// <author>
	/// Richard J. Northedge
	/// </author>
	public abstract class AbstractDataIndexer : ITrainingDataIndexer
	{
		private int[][] mContexts;
        private int[] mOutcomeList;
        private int[] mNumTimesEventsSeen;
        private string[] mPredicateLabels;
        private string[] mOutcomeLabels;

		/// <summary>
		/// Gets an array of context data calculated from the training data.
		/// </summary>
		/// <returns>
		/// Array of integer arrays, each containing the context data for an event.
		/// </returns>
		public virtual int[][] GetContexts()
		{
			return mContexts;
		}

		/// <summary>
		/// Sets the array of context data calculated from the training data.
		/// </summary>
		/// <param name="newContexts">
		/// Array of integer arrays, each containing the context data for an event.
		/// </param>
		protected internal void SetContexts(int[][] newContexts) 
		{
			mContexts = newContexts;
		}

		/// <summary>
		/// Gets an array indicating how many times each event is seen.
		/// </summary>
		/// <returns>
		/// Integer array with event frequencies.
		/// </returns>
		public virtual int[] GetNumTimesEventsSeen()
		{	
			return mNumTimesEventsSeen;
		}

		/// <summary>
		/// Sets an array indicating how many times each event is seen.
		/// </summary>
		/// <param name="newNumTimesEventsSeen">
		/// Integer array with event frequencies.
		/// </param>
		protected internal void SetNumTimesEventsSeen(int[] newNumTimesEventsSeen)
		{
			mNumTimesEventsSeen = newNumTimesEventsSeen;
		}

		/// <summary>
		/// Gets an outcome list.
		/// </summary>
		/// <returns>
		/// Integer array of outcomes.
		/// </returns>
		public virtual int[] GetOutcomeList()
		{
			return mOutcomeList;
		}

		/// <summary>
		/// Sets an outcome list.
		/// </summary>
		/// <param name="newOutcomeList">
		/// Integer array of outcomes.
		/// </param>
		protected internal void SetOutcomeList(int[] newOutcomeList)
		{
			mOutcomeList = newOutcomeList;
		}

		/// <summary>
		/// Gets an array of predicate labels.
		/// </summary>
		/// <returns>
		/// Array of predicate labels.
		/// </returns>
		public virtual string[] GetPredicateLabels()
		{
			return mPredicateLabels;
		}

		/// <summary>
		/// Sets an array of predicate labels.
		/// </summary>
		/// <param name="newPredicateLabels">
		/// Array of predicate labels.
		/// </param>
		protected internal void SetPredicateLabels(string[] newPredicateLabels)
		{
			mPredicateLabels = newPredicateLabels;
		}

		/// <summary>
		/// Gets an array of outcome labels.
		/// </summary>
		/// <returns>
		/// Array of outcome labels.
		/// </returns>
		public virtual string[] GetOutcomeLabels()
		{
			return mOutcomeLabels;
		}
		
		/// <summary>
		/// Sets an array of outcome labels.
		/// </summary>
		/// <param name="newOutcomeLabels">
		/// Array of outcome labels.
		/// </param>
		protected internal void SetOutcomeLabels(string[] newOutcomeLabels)
		{
			mOutcomeLabels = newOutcomeLabels;
		}

        public long GetMemoryUse()
        {
            long returnVal = 0;
            foreach (string s in mPredicateLabels)
            {
                returnVal += Encoding.UTF8.GetByteCount(s);
            }
            foreach (string s in mOutcomeLabels)
            {
                returnVal += Encoding.UTF8.GetByteCount(s);
            }
            return returnVal;
        }

		/// <summary>
		/// Sorts and uniques the array of comparable events.  This method
		/// will alter the eventsToCompare array -- it does an in place
		/// sort, followed by an in place edit to remove duplicates.
		/// </summary>
		/// <param name="eventsToCompare">
		/// a List of <code>ComparableEvent</code> values
		/// </param>
        protected internal virtual void SortAndMerge(List<ComparableEvent> eventsToCompare)
        {
            if (eventsToCompare.Count <= 1)
            {
                return; // nothing to do; edge case
            }

            ComparableEvent[] input = eventsToCompare.ToArray();
            ComparableEvent[] output = new ComparableEvent[input.Length];
            ArrayExtensions.MemCopy(input, 0, output, 0, input.Length);
            int outLength = SortAndPruneArray(input, output, 0, input.Length - 1);

            mContexts = new int[outLength][];
            mOutcomeList = new int[outLength];
            mNumTimesEventsSeen = new int[outLength];

            for (int currentEvent = 0; currentEvent < outLength; currentEvent++)
            {
                mNumTimesEventsSeen[currentEvent] = output[currentEvent].SeenCount;
                mOutcomeList[currentEvent] = output[currentEvent].Outcome;
                mContexts[currentEvent] = output[currentEvent].GetPredicateIndexes();
            }
        }

        public static int SortAndPruneArray(ComparableEvent[] inArray, ComparableEvent[] outArray, int start, int end)
        {
            int segmentLength = (end - start) + 1;
            if (segmentLength > 2)
            {
                // Find the middle of the list
                int leftLength = segmentLength / 2;
                int middle = start + leftLength;

                // Recurse
                int length1 = SortAndPruneArray(outArray, inArray, start, middle - 1);
                int length2 = SortAndPruneArray(outArray, inArray, middle, end);

                int idx1 = start;
                int idx2 = middle;
                int outIdx = 0;

                // Merge
                while (idx1 < start + length1 && idx2 < middle + length2)
                {
                    int comparison = inArray[idx1].CompareTo(inArray[idx2]);
                    if (comparison < 0)
                    {
                        // Take left
                        outArray[start + outIdx++] = inArray[idx1++];
                    }
                    else if (comparison > 0)
                    {
                        //Take right
                        outArray[start + outIdx++] = inArray[idx2++];
                    }
                    else
                    {
                        // Equality; ignore the value from the right-hand list and advance
                        idx2++;
                    }
                }

                while (idx1 < start + length1)
                {
                    // Drain left
                    if (outArray[outIdx].CompareTo(inArray[idx1]) != 0)
                        outArray[start + outIdx++] = inArray[idx1++];
                    else
                        idx1++;
                }
                while (idx2 < middle + length2)
                {
                    // Drain right
                    if (outArray[outIdx].CompareTo(inArray[idx2]) != 0)
                        outArray[start + outIdx++] = inArray[idx2++];
                    else
                        idx2++;
                }
                return outIdx;
            }
            else if (segmentLength == 2)
            {
                int comparison = inArray[start].CompareTo(inArray[end]);
                if (comparison < 0)
                {
                    // Don't swap
                    outArray[start] = inArray[start];
                    outArray[end] = inArray[end];
                    return 2;
                }
                else if (comparison > 0)
                {
                    // Swap
                    outArray[start] = inArray[end];
                    outArray[end] = inArray[start];
                    return 2;
                }
                else
                {
                    // Equality; ignore the second value
                    outArray[start] = inArray[start];
                    return 1;
                }
            }
            else
            {
                outArray[start] = inArray[start];
                return segmentLength;
            }
        }
		
		/// <summary>
		/// Utility method for creating a string[] array from a dictionary whose
		/// keys are labels (strings) to be stored in the array and whose
		/// values are the indices (integers) at which the corresponding
		/// labels should be inserted.
		/// </summary>
		/// <param name="labelToIndexMap">
		/// a <code>Dictionary</code> value
		/// </param>
		/// <returns>
		/// a <code>string[]</code> value
		/// </returns>
		protected internal static string[] ToIndexedStringArray(Dictionary<string, int> labelToIndexMap)
		{
            string[] indexedArray = new string[labelToIndexMap.Count];
            int[] indices = new int[labelToIndexMap.Count];
            labelToIndexMap.Keys.CopyTo(indexedArray, 0);
            labelToIndexMap.Values.CopyTo(indices, 0);
            ArrayExtensions.Sort(indices, indexedArray);
			return indexedArray;
		}
	}
}
