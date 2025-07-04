//Copyright (C) 2005 Richard J. Northedge
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

//This file is based on the GISModel.java source file found in the
//original java implementation of MaxEnt.  That source file contains the following header:

// Copyright (C) 2001 Jason Baldridge and Gann Bierner
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later version.
//
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.

using System;
using System.Collections.Generic;
using System.Text;
using Durandal.Common.Collections.Indexing;
using Durandal.Common.MathExt;

namespace Durandal.Common.Statistics.SharpEntropy
{
    /// <summary>
    /// A maximum entropy model which has been trained using the Generalized
    /// Iterative Scaling procedure.
    /// </summary>
    /// <author>
    /// Tom Morton and Jason Baldridge
    /// </author>
    /// <author>
    /// Richard J. Northedge
    /// </author>
    /// <version>
    /// based on GISModel.java, $Revision: 1.13 $, $Date: 2004/06/11 20:51:44 $
    /// </version>
    public sealed class GisModel : IMaximumEntropyModel
    {
		private IO.IGisModelReader mReader;

		private string[] mOutcomeNames;
        private float mCorrectionConstant;
        private float mCorrectionParameter;
		
		private int mOutcomeCount;
        private float mInitialProbability;
        private float mCorrectionConstantInverse;
		
		private int[] mFeatureCounts;
		
		/// <summary>
		/// Constructor for a maximum entropy model trained using the
		/// Generalized Iterative Scaling procedure.
		/// </summary>
		/// <param name="reader">
		/// A reader providing the data for the model.
		/// </param>
		public GisModel(IO.IGisModelReader reader)
		{
			mReader = reader;
			mOutcomeNames = reader.GetOutcomeLabels();
            mCorrectionConstant = (float)reader.CorrectionConstant;
			mCorrectionParameter = reader.CorrectionParameter;
			
			mOutcomeCount = mOutcomeNames.Length;
			mInitialProbability = FastMath.Log(1.0f / mOutcomeCount);
			mCorrectionConstantInverse = 1.0f / mCorrectionConstant;
			mFeatureCounts = new int[mOutcomeCount];
		}

		#region implementation of IMaxentModel

		/// <summary>
		/// Returns the number of outcomes for this model.
		/// </summary>
		/// <returns>
		/// The number of outcomes.
		/// </returns>
		public int OutcomeCount
		{
			get
			{
				return (mOutcomeCount);
			}
		}

        public long GetMemoryUse()
        {
            long returnVal = mFeatureCounts.Length * 4L;
            foreach (string s in mOutcomeNames)
            {
                returnVal += Encoding.UTF8.GetByteCount(s);
            }
            foreach (string s in mReader.GetOutcomeLabels())
            {
                returnVal += Encoding.UTF8.GetByteCount(s);
            }
            foreach (int[] x in mReader.GetOutcomePatterns())
            {
                returnVal += (x.Length * 4L);
            }
            // Each predicate is at least 20 bytes
            returnVal += 20L * mReader.GetPredicates().Count;
            return returnVal;
        }

		/// <summary> 
		/// Evaluates a context.
		/// </summary>
		/// <param name="context">
		/// A list of string names of the contextual predicates
		/// which are to be evaluated together.
		/// </param>
		/// <returns>
		/// An array of the probabilities for each of the different
		/// outcomes, all of which sum to 1.
		/// </returns>
        public float[] Evaluate(string[] context)
		{
            return Evaluate(context, new float[mOutcomeCount]);
		}

		/// <summary>
		/// Use this model to evaluate a context and return an array of the
		/// likelihood of each outcome given that context.
		/// </summary>
		/// <param name="context">
		/// The names of the predicates which have been observed at
		/// the present decision point.
		/// </param>
		/// <param name="outcomeSums">
		/// This is where the distribution is stored.
		/// </param>
		/// <returns>
		/// The normalized probabilities for the outcomes given the
		/// context. The indexes of the double[] are the outcome
		/// ids, and the actual string representation of the
		/// outcomes can be obtained from the method
		/// GetOutcome(int outcomeIndex).
		/// </returns>
        public float[] Evaluate(string[] context, float[] outcomeSums)
		{
            if (outcomeSums.Length == 1)
            {
                outcomeSums[0] = 1.0f;
                return outcomeSums;
            }

            for (int outcomeIndex = 0; outcomeIndex < mOutcomeCount; outcomeIndex++)
			{
				outcomeSums[outcomeIndex] = mInitialProbability;
				mFeatureCounts[outcomeIndex] = 0;
			}

			for (int currentContext = 0;currentContext < context.Length; currentContext++)
			{
				mReader.GetPredicateData(context[currentContext], mFeatureCounts, outcomeSums);
			}

            float normal = 0.0f;
			for (int outcomeIndex = 0;outcomeIndex < mOutcomeCount; outcomeIndex++)
			{
				outcomeSums[outcomeIndex] = FastMath.Exp((outcomeSums[outcomeIndex] * mCorrectionConstantInverse) + ((1.0f -(mFeatureCounts[outcomeIndex] / mCorrectionConstant)) * mCorrectionParameter));
				normal += outcomeSums[outcomeIndex];
			}

            if (normal != 0 && !float.IsNaN(normal) && !float.IsInfinity(normal))
            {
                for (int outcomeIndex = 0; outcomeIndex < mOutcomeCount; outcomeIndex++)
                {
                    outcomeSums[outcomeIndex] /= normal;
                }
            }

            bool isAnyInfinity = false;
            foreach (float sum in outcomeSums)
            {
                if (float.IsPositiveInfinity(sum))
                {
                    isAnyInfinity = true;
                    break;
                }
            }

            // Are there any infinities? Then set that to 1 and make everything else 0
            if (isAnyInfinity)
            {
                for (int outcomeIndex = 0; outcomeIndex < mOutcomeCount; outcomeIndex++)
                {
                    if (float.IsInfinity(outcomeSums[outcomeIndex]))
                    {
                        outcomeSums[outcomeIndex] = 1.0f;
                    }
                    else
                    {
                        outcomeSums[outcomeIndex] = 0.0f;
                    }
                }
            }
            else
            {
                // Clamp and normalize
                for (int outcomeIndex = 0; outcomeIndex < mOutcomeCount; outcomeIndex++)
                {
                    if (float.IsNaN(outcomeSums[outcomeIndex]) || outcomeSums[outcomeIndex] < 0)
                    {
                        outcomeSums[outcomeIndex] = 0.0f;
                    }
                    else if (outcomeSums[outcomeIndex] > 1.0f)
                    {
                        outcomeSums[outcomeIndex] = 1.0f;
                    }
                }
            }

            return outcomeSums;
		}
		
		/// <summary>
		/// Return the name of the outcome corresponding to the highest likelihood
		/// in the parameter outcomes.
		/// </summary>
		/// <param name="outcomes">
		/// A double[] as returned by the Evaluate(string[] context)
		/// method.
		/// </param>
		/// <returns>
		/// The name of the most likely outcome.
		/// </returns>
        public string GetBestOutcome(float[] outcomes)
		{
			int bestOutcomeIndex = 0;
			for (int currentOutcome = 1; currentOutcome < outcomes.Length; currentOutcome++)
				if (outcomes[currentOutcome] > outcomes[bestOutcomeIndex])
				{
					bestOutcomeIndex = currentOutcome;
				}
			return mOutcomeNames[bestOutcomeIndex];
		}

		/// <summary>
		/// Return a string matching all the outcome names with all the
		/// probabilities produced by the <code>Evaluate(string[] context)</code>
		/// method.
		/// </summary>
		/// <param name="outcomes">
		/// A <code>double[]</code> as returned by the
		/// <code>eval(String[] context)</code>
		/// method.
		/// </param>
		/// <returns>
		/// String containing outcome names paired with the normalized
		/// probability (contained in the <code>double[] outcomes</code>)
		/// for each one.
		/// </returns>
        public string GetAllOutcomes(float[] outcomes)
		{
			if (outcomes.Length != mOutcomeNames.Length)
			{
                throw new ArgumentException("The float array sent as a parameter to GisModel.GetAllOutcomes() must not have been produced by this model.");
			}
			else
			{
				System.Text.StringBuilder outcomeInfo = new System.Text.StringBuilder(outcomes.Length * 2);
				outcomeInfo.Append(mOutcomeNames[0]).Append("[").Append(outcomes[0].ToString("0.0000", System.Globalization.CultureInfo.CurrentCulture)).Append("]");
				for (int currentOutcome = 1; currentOutcome < outcomes.Length; currentOutcome++)
				{
					outcomeInfo.Append("  ").Append(mOutcomeNames[currentOutcome]).Append("[").Append(outcomes[currentOutcome].ToString("0.0000", System.Globalization.CultureInfo.CurrentCulture)).Append("]");
				}
				return outcomeInfo.ToString();
			}
		}

		/// <summary>
		/// Return the name of an outcome corresponding to an integer ID value.
		/// </summary>
		/// <param name="outcomeIndex">
		/// An outcome ID.
		/// </param>
		/// <returns>
		/// The name of the outcome associated with that ID.
		/// </returns>
		public string GetOutcomeName(int outcomeIndex)
		{
			return mOutcomeNames[outcomeIndex];
		}

		/// <summary> 
		/// Gets the index associated with the string name of the given outcome.
		/// </summary>
		/// <param name="outcome">
		/// the string name of the outcome for which the
		/// index is desired
		/// </param>
		/// <returns>
		/// the index if the given outcome label exists for this
		/// model, -1 if it does not.
		/// </returns>
		public int GetOutcomeIndex(string outcome)
		{
			for (int iCurrentOutcomeName = 0; iCurrentOutcomeName < mOutcomeNames.Length; iCurrentOutcomeName++)
			{
				if (mOutcomeNames[iCurrentOutcomeName] == outcome)
				{
					return iCurrentOutcomeName;
				}
			}
			return - 1;
		}
		
		/// <summary>
		/// Provides the predicates data structure which is part of the encoding of the maxent model
		/// information.  This method will usually only be needed by
		/// GisModelWriters.
		/// </summary>
		/// <returns>
		/// Dictionary containing PatternedPredicate objects.
		/// </returns>
        public Dictionary<Compact<string>, PatternedPredicate> GetPredicates()
		{
			return mReader.GetPredicates();
		}
    
		/// <summary>
		/// Provides the list of outcome patterns used by the predicates.  This method will usually
		/// only be needed by GisModelWriters.
		/// </summary>
		/// <returns>
		/// Array of outcome patterns.
		/// </returns>
		public int[][] GetOutcomePatterns()
		{
			return mReader.GetOutcomePatterns();
		}

		/// <summary>
		/// Provides the outcome names data structure which is part of the encoding of the maxent model
		/// information.  This method will usually only be needed by
		/// GisModelWriters.
		/// </summary>
		/// <returns>
		/// Array containing the outcome names.
		/// </returns>
		public string[] GetOutcomeNames()
		{
			return mOutcomeNames;
		}

		/// <summary>
		/// Provides the model's correction constant.  This property will usually only be needed by
		/// GisModelWriters.
		/// </summary>
		public int CorrectionConstant
		{
			get
			{
				return (int)mCorrectionConstant;
			}
		}

		/// <summary>
		/// Provides the model's correction parameter.  This property will usually only be needed by
		/// GisModelWriters.
		/// </summary>
        public float CorrectionParameter
		{
			get
			{
				return mCorrectionParameter;
			}
		}

		#endregion
	}
}
