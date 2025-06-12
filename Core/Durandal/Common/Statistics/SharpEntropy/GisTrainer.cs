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

//This file is based on the GISTrainer.java source file found in the
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
using Durandal.Common.Collections.Indexing;
using Durandal.Common.Collections;
using Durandal.Common.MathExt;

namespace Durandal.Common.Statistics.SharpEntropy
{
    /// <summary>
	/// An implementation of Generalized Iterative Scaling.  The reference paper
	/// for this implementation was Adwait Ratnaparkhi's tech report at the
	/// University of Pennsylvania's Institute for Research in Cognitive Science,
	/// and is available at <a href ="ftp://ftp.cis.upenn.edu/pub/ircs/tr/97-08.ps.Z"><code>ftp://ftp.cis.upenn.edu/pub/ircs/tr/97-08.ps.Z</code></a>. 
	/// </summary>
	/// <author>
	/// Jason Baldridge
	/// </author>
	/// <author>
	///  Richard J, Northedge
	/// </author>
	/// <version>
	/// based on GISTrainer.java, $Revision: 1.15 $, $Date: 2004/06/14 20:52:41 $
	/// </version>
	public class GisTrainer : IO.IGisModelReader
	{
		private int mTokenCount; // # of event tokens
		private int mPredicateCount; // # of predicates
		private int mOutcomeCount; // # of mOutcomes
		private int mTokenID; // global index variable for Tokens
		private int mPredicateId; // global index variable for Predicates    
		private int mOutcomeId; // global index variable for Outcomes
				
		// records the array of predicates seen in each event
		private int[][] mContexts;
		
		// records the array of outcomes seen in each event
		private int[] mOutcomes;
				
		// records the num of times an event has been seen, paired to
		// int[][] mContexts
		private int[] mNumTimesEventsSeen;
		
		// stores the string names of the outcomes.  The GIS only tracks outcomes
		// as ints, and so this array is needed to save the model to disk and
		// thereby allow users to know what the outcome was in human
		// understandable terms.
		private string[] mOutcomeLabels;
		
		// stores the string names of the predicates. The GIS only tracks
		// predicates as ints, and so this array is needed to save the model to
		// disk and thereby allow users to know what the outcome was in human
		// understandable terms.
		private string[] mPredicateLabels;

		// stores the observed expections of each of the events
		private float[][] mObservedExpections;
		
		// stores the estimated parameter value of each predicate during iteration
        private float[][] mParameters;
		
		// Stores the expected values of the features based on the current models
        private float[][] mModelExpections;
		
		//The maximum number of features fired in an event. Usually referred to as C.
		private int mMaximumFeatureCount;

		// stores inverse of constant, 1/C.
        private float mMaximumFeatureCountInverse;

		// the correction parameter of the model
        private float mCorrectionParameter;

		// observed expectation of correction feature
        private float mCorrectionFeatureObservedExpectation;

		// a global variable to help compute the amount to modify the correction
		// parameter
        private float mCorrectionFeatureModifier;

        private const float mNearZero = 0.01f;
        private const float mLLThreshold = 0.0001f;
		
		// Stores the output of the current model on a single event durring
		// training.  This will be reset for every event for every iteration.
        private float[] mModelDistribution;

		// Stores the number of features that get fired per event
		private int[] mFeatureCounts;

		// initial probability for all outcomes.
        private float mInitialProbability;

        private Dictionary<Compact<string>, PatternedPredicate> mPredicates;
		private int[][] mOutcomePatterns;

        private readonly ICompactIndex<string> _stringIndex;

		#region smoothing algorithm (unused)

//		internal class UpdateParametersWithSmoothingProcedure : Trove.IIntDoubleProcedure
//		{

//			private double mdSigma = 2.0;

//			public UpdateParametersWithSmoothingProcedure(GisTrainer enclosingInstance)
//			{
//				moEnclosingInstance = enclosingInstance;
//			}
//		
//			private GisTrainer moEnclosingInstance;
//
//			public virtual bool Execute(int outcomeID, double input)
//			{
//				double x = 0.0;
//				double x0 = 0.0;
//				double tmp;
//				double f;
//				double fp;
//				for (int i = 0; i < 50; i++) 
//				{
//					// check what domain these parameters are in
//					tmp = moEnclosingInstance.maoModelExpections[moEnclosingInstance.miPredicateID][outcomeID] * System.Math.Exp(moEnclosingInstance.miConstant * x0);
//					f = tmp + (input + x0) / moEnclosingInstance.mdSigma - moEnclosingInstance.maoObservedExpections[moEnclosingInstance.miPredicateID][outcomeID];
//					fp = tmp * moEnclosingInstance.miConstant + 1 / moEnclosingInstance.mdSigma;
//					if (fp == 0) 
//					{
//						break;
//					}
//					x = x0 - f / fp;
//					if (System.Math.Abs(x - x0) < 0.000001) 
//					{
//						x0 = x;
//						break;
//					}
//					x0 = x;
//				}
//				moEnclosingInstance.maoParameters[moEnclosingInstance.miPredicateID].Put(outcomeID, input + x0);
//				return true;
//			}
//		}

		#endregion

		#region training progress event

		/// <summary>
		/// Used to provide informational messages regarding the
		/// progress of the training algorithm.
		/// </summary>
		public event TrainingProgressEventHandler TrainingProgress;

		/// <summary>
		/// Used to raise events providing messages with information
		/// about training progress.
		/// </summary>
		/// <param name="e">
		/// Contains the message with information about the progress of 
		/// the training algorithm.
		/// </param>
		protected virtual void OnTrainingProgress(TrainingProgressEventArgs e) 
		{
			if (TrainingProgress != null) 
			{
				TrainingProgress(this, e); 
			}
		}

		private void NotifyProgress(string message)
		{
			OnTrainingProgress(new TrainingProgressEventArgs(message));
		}

		#endregion

		#region training options

		private bool mSimpleSmoothing = false;
		private bool mUseSlackParameter = false;
        private float mSmoothingObservation = 0.1f;

    	/// <summary>
    	/// Sets whether this trainer will use smoothing while training the model.
		/// This can improve model accuracy, though training will potentially take
		/// longer and use more memory.  Model size will also be larger.
		/// </summary>
		/// <remarks>
		/// Initial testing indicates improvements for models built on small data sets and
		/// few outcomes, but performance degradation for those with large data
		/// sets and lots of outcomes.
		/// </remarks>
		public virtual bool Smoothing
		{
			get
			{
				return mSimpleSmoothing;
			}
			set
			{
				mSimpleSmoothing = value;
			}
		}

		/// <summary>
		/// Sets whether this trainer will use slack parameters while training the model.
		/// </summary>
		public virtual bool UseSlackParameter
		{
			get
			{
				return mUseSlackParameter;
			}
			set
			{
				mUseSlackParameter = value;
			}
		}

		/// <summary>
		/// If smoothing is in use, this value indicates the "number" of
		/// times we want the trainer to imagine that it saw a feature that it
		/// actually didn't see.  Defaulted to 0.1.
		/// </summary>
        virtual public float SmoothingObservation
		{
			get
			{
				return mSmoothingObservation;
			}
			set
			{
				mSmoothingObservation = value;
			}
			
		}
		
		/// <summary>
		/// Creates a new <code>GisTrainer</code> instance.
		/// </summary>
        public GisTrainer(ICompactIndex<string> stringIndex)
		{
			mSimpleSmoothing = false;
			mUseSlackParameter = false;
			mSmoothingObservation = 0.1f;
		    _stringIndex = stringIndex;
		}

		/// <summary>
		/// Creates a new <code>GisTrainer</code> instance.
		/// </summary>
		/// <param name="stringIndex">A compact index for compressing string data</param>
		/// <param name="useSlackParameter">
		/// Sets whether this trainer will use slack parameters while training the model.
		/// </param>
		public GisTrainer(ICompactIndex<string> stringIndex, bool useSlackParameter)
            : this(stringIndex)
		{
			mSimpleSmoothing = false;
			mUseSlackParameter = useSlackParameter;
			mSmoothingObservation = 0.1f;
		}

		/// <summary>
		/// Creates a new <code>GisTrainer</code> instance.
		/// </summary>
		/// <param name="stringIndex">A compact index for compressing string data</param>
		/// <param name="smoothingObservation">
		/// If smoothing is in use, this value indicates the "number" of
		/// times we want the trainer to imagine that it saw a feature that it
		/// actually didn't see.  Defaulted to 0.1.
		/// </param>
		public GisTrainer(ICompactIndex<string> stringIndex, float smoothingObservation)
            : this(stringIndex)
		{
			mSimpleSmoothing = true;
			mUseSlackParameter = false;
			mSmoothingObservation = smoothingObservation;
		}

		/// <summary>
		/// Creates a new <code>GisTrainer</code> instance.
		/// </summary>
		/// <param name="stringIndex">A compact index for compressing string data</param>
		/// <param name="useSlackParameter">
		/// Sets whether this trainer will use slack parameters while training the model.
		/// </param>
		/// <param name="smoothingObservation">
		/// If smoothing is in use, this value indicates the "number" of
		/// times we want the trainer to imagine that it saw a feature that it
		/// actually didn't see.  Defaulted to 0.1.
		/// </param>
		public GisTrainer(ICompactIndex<string> stringIndex, bool useSlackParameter, float smoothingObservation)
            : this(stringIndex)
		{
			mSimpleSmoothing = true;
			mUseSlackParameter = useSlackParameter;
			mSmoothingObservation = smoothingObservation;
		}

		#endregion

		#region alternative TrainModel signatures

		/// <summary>
		/// Train a model using the GIS algorithm.
		/// </summary>
		/// <param name="eventReader">
		/// The ITrainingEventReader holding the data on which this model
		/// will be trained.
		/// </param>
		public virtual void TrainModel(ITrainingEventReader eventReader)
		{
			TrainModel(eventReader, 100, 0);
		}

		/// <summary>
		/// Train a model using the GIS algorithm.
		/// </summary>
		/// <param name="eventReader">
		/// The ITrainingEventReader holding the data on which this model
		/// will be trained.
		/// </param>
		/// <param name="iterations">
		/// The number of GIS iterations to perform.
		/// </param>
		/// <param name="cutoff">
		/// The number of times a predicate must be seen in order
		/// to be relevant for training.
		/// </param>
		public virtual void TrainModel(ITrainingEventReader eventReader, int iterations, int cutoff)
		{
			TrainModel(iterations, new OnePassDataIndexer(eventReader, cutoff));
		}
		
		#endregion

		#region training algorithm

		/// <summary>
		/// Train a model using the GIS algorithm.
		/// </summary>
		/// <param name="iterations">
		/// The number of GIS iterations to perform.
		/// </param>
		/// <param name="dataIndexer">
		/// The data indexer used to compress events in memory.
		/// </param>
		public virtual void TrainModel(int iterations, ITrainingDataIndexer dataIndexer)
		{
			int[] outcomeList;

			//incorporate all of the needed info
			NotifyProgress("Incorporating indexed data for training...");
			mContexts = dataIndexer.GetContexts();
			mOutcomes = dataIndexer.GetOutcomeList();
			mNumTimesEventsSeen = dataIndexer.GetNumTimesEventsSeen();

            //if (mContexts == null)
            //{
            //    // No data. Error
            //    mPredicates = new Dictionary<Compact<string>, PatternedPredicate>();
            //    mOutcomePatterns = new int[0][];
            //    mParameters = new float[0][];
            //    mModelExpections = new float[0][];
            //    mObservedExpections = new float[0][];
            //    return;
            //}

            mTokenCount = mContexts.Length;
			
			// determine the correction constant and its inverse
            // hack: sometimes if there is not enough training data (?), there will be 0-length contexts.
            // In this case, set the max feature count to 1 to prevent divide-by-zero later on.
            // The model will still work, although it will be quite rough.
			mMaximumFeatureCount = mContexts.Length == 0 ? 1 : mContexts[0].Length;
			for (mTokenID = 1; mTokenID < mContexts.Length; mTokenID++)
			{
				if (mContexts[mTokenID].Length > mMaximumFeatureCount)
				{
					mMaximumFeatureCount = mContexts[mTokenID].Length;
				}
			}
			mMaximumFeatureCountInverse = 1.0f / mMaximumFeatureCount;
			
			NotifyProgress("done.");
			
			mOutcomeLabels = dataIndexer.GetOutcomeLabels();
			outcomeList = dataIndexer.GetOutcomeList();
			mOutcomeCount = mOutcomeLabels.Length;
			mInitialProbability = FastMath.Log(1.0f / mOutcomeCount);
			
			mPredicateLabels = dataIndexer.GetPredicateLabels();
			mPredicateCount = mPredicateLabels.Length;
            
            // Skip training if there is only 1 outcome.
            if (mOutcomeCount != 1)
            {
                NotifyProgress("\tNumber of Event Tokens: " + mTokenCount);
                NotifyProgress("\t    Number of Outcomes: " + mOutcomeCount);
                NotifyProgress("\t  Number of Predicates: " + mPredicateCount);

                // set up feature arrays
                int[][] predicateCounts = new int[mPredicateCount][];
                for (mPredicateId = 0; mPredicateId < mPredicateCount; mPredicateId++)
                {
                    predicateCounts[mPredicateId] = new int[mOutcomeCount];
                }
                for (mTokenID = 0; mTokenID < mTokenCount; mTokenID++)
                {
                    for (int currentContext = 0; currentContext < mContexts[mTokenID].Length; currentContext++)
                    {
                        predicateCounts[mContexts[mTokenID][currentContext]][outcomeList[mTokenID]] += mNumTimesEventsSeen[mTokenID];
                    }
                }

                dataIndexer = null; // don't need it anymore

                // A fake "observation" to cover features which are not detected in
                // the data.  The default is to assume that we observed "1/10th" of a
                // feature during training.
                float smoothingObservation = mSmoothingObservation;

                // Get the observed expectations of the features. Strictly speaking,
                // we should divide the counts by the number of Tokens, but because of
                // the way the model's expectations are approximated in the
                // implementation, this is cancelled out when we compute the next
                // iteration of a parameter, making the extra divisions wasteful.
                mOutcomePatterns = new int[mPredicateCount][];
                mParameters = new float[mPredicateCount][];
                mModelExpections = new float[mPredicateCount][];
                mObservedExpections = new float[mPredicateCount][];

                int activeOutcomeCount;
                int currentOutcome;

                for (mPredicateId = 0; mPredicateId < mPredicateCount; mPredicateId++)
                {
                    if (mSimpleSmoothing)
                    {
                        activeOutcomeCount = mOutcomeCount;
                    }
                    else
                    {
                        activeOutcomeCount = 0;
                        for (mOutcomeId = 0; mOutcomeId < mOutcomeCount; mOutcomeId++)
                        {
                            if (predicateCounts[mPredicateId][mOutcomeId] > 0)
                            {
                                activeOutcomeCount++;
                            }
                        }
                    }

                    mOutcomePatterns[mPredicateId] = new int[activeOutcomeCount];
                    mParameters[mPredicateId] = new float[activeOutcomeCount];
                    mModelExpections[mPredicateId] = new float[activeOutcomeCount];
                    mObservedExpections[mPredicateId] = new float[activeOutcomeCount];

                    currentOutcome = 0;
                    for (mOutcomeId = 0; mOutcomeId < mOutcomeCount; mOutcomeId++)
                    {
                        if (predicateCounts[mPredicateId][mOutcomeId] > 0)
                        {
                            mOutcomePatterns[mPredicateId][currentOutcome] = mOutcomeId;
                            mObservedExpections[mPredicateId][currentOutcome] = FastMath.Log(predicateCounts[mPredicateId][mOutcomeId]);
                            currentOutcome++;
                        }
                        else if (mSimpleSmoothing)
                        {
                            mOutcomePatterns[mPredicateId][currentOutcome] = mOutcomeId;
                            mObservedExpections[mPredicateId][currentOutcome] = smoothingObservation;
                            currentOutcome++;
                        }
                    }
                }

                // compute the expected value of correction
                if (mUseSlackParameter)
                {
                    int correctionFeatureValueSum = 0;
                    for (mTokenID = 0; mTokenID < mTokenCount; mTokenID++)
                    {
                        for (int currentContext = 0; currentContext < mContexts[mTokenID].Length; currentContext++)
                        {
                            mPredicateId = mContexts[mTokenID][currentContext];

                            if ((!mSimpleSmoothing) && predicateCounts[mPredicateId][mOutcomes[mTokenID]] == 0)
                            {
                                correctionFeatureValueSum += mNumTimesEventsSeen[mTokenID];
                            }
                        }
                        correctionFeatureValueSum += (mMaximumFeatureCount - mContexts[mTokenID].Length) * mNumTimesEventsSeen[mTokenID];
                    }
                    if (correctionFeatureValueSum == 0)
                    {
                        mCorrectionFeatureObservedExpectation = FastMath.Log(mNearZero); //nearly zero so log is defined
                    }
                    else
                    {
                        mCorrectionFeatureObservedExpectation = FastMath.Log(correctionFeatureValueSum);
                    }

                    mCorrectionParameter = 0.0f;
                }

                predicateCounts = null; // don't need it anymore

                NotifyProgress("...done.");

                mModelDistribution = new float[mOutcomeCount];
                mFeatureCounts = new int[mOutcomeCount];

                //Find the parameters
                NotifyProgress("Computing model parameters...");
                FindParameters(iterations);

                NotifyProgress("Converting to new predicate format...");
                ConvertPredicates();
            }
            else
            {
                // In the degenerate case, use dummy values to satisfy model serializers, etc.
                mPredicates = new Dictionary<Compact<string>, PatternedPredicate>();
                mOutcomePatterns = new int[0][];
                mParameters = new float[0][];
                mModelExpections = new float[0][];
                mObservedExpections = new float[0][];
            }
		}
		
		/// <summary>
		/// Estimate and return the model parameters.
		/// </summary>
		/// <param name="iterations">
		/// Number of iterations to run through.
		/// </param>
		private void FindParameters(int iterations)
		{
			double previousLogLikelihood = 0.0;
			double currentLogLikelihood = 0.0;
			NotifyProgress("Performing " + iterations + " iterations.");
			for (int currentIteration = 1; currentIteration <= iterations; currentIteration++)
			{
				if (currentIteration < 10)
				{
					NotifyProgress("  " + currentIteration + ":  ");
				}
				else if (currentIteration < 100)
				{
					NotifyProgress(" " + currentIteration + ":  ");
				}
				else
				{
					NotifyProgress(currentIteration + ":  ");
				}
				currentLogLikelihood = NextIteration();
				if (currentIteration > 1)
				{
					if (previousLogLikelihood > currentLogLikelihood)
					{
						throw new ArithmeticException("Model Diverging: loglikelihood decreased");
					}
					if (currentLogLikelihood - previousLogLikelihood < mLLThreshold)
					{
						break;
					}
				}
				previousLogLikelihood = currentLogLikelihood;
			}
			
			// kill a bunch of these big objects now that we don't need them
			mObservedExpections = null;
			mModelExpections = null;
			mNumTimesEventsSeen = null;
			mContexts = null;
		}
		
		/// <summary>
		/// Use this model to evaluate a context and return an array of the
		/// likelihood of each outcome given that context.
		/// </summary>
		/// <param name="context">
		/// The integers of the predicates which have been
		/// observed at the present decision point.
		/// </param>
		/// <param name="outcomeSums">
		/// The normalized probabilities for the outcomes given the
		/// context. The indexes of the double[] are the outcome
		/// ids.
		/// </param>
        protected virtual void Evaluate(int[] context, float[] outcomeSums)
		{
            if (outcomeSums.Length == 1)
            {
                outcomeSums[0] = 1.0f;
                return;
            }

            for (int outcomeIndex = 0; outcomeIndex < mOutcomeCount; outcomeIndex++)
			{
				outcomeSums[outcomeIndex] = mInitialProbability;
				mFeatureCounts[outcomeIndex] = 0;
			}
			int[] activeOutcomes;
			int outcomeId;
			int predicateId;
			int currentActiveOutcome;

            int cl = context.Length;
			for (int currentContext = 0; currentContext < cl; currentContext++)
			{
				predicateId = context[currentContext];
				activeOutcomes = mOutcomePatterns[predicateId];
                int aol = activeOutcomes.Length;
				for (currentActiveOutcome = 0; currentActiveOutcome < aol; currentActiveOutcome++)
				{
					outcomeId = activeOutcomes[currentActiveOutcome];
					mFeatureCounts[outcomeId]++;
					outcomeSums[outcomeId] += mMaximumFeatureCountInverse * mParameters[predicateId][currentActiveOutcome];
				}
			}

            float sum = 0.0f;
			for (int currentOutcomeId = 0; currentOutcomeId < mOutcomeCount; currentOutcomeId++)
			{
                outcomeSums[currentOutcomeId] = FastMath.Exp(outcomeSums[currentOutcomeId]);
                if (mUseSlackParameter) 
				{
                    outcomeSums[currentOutcomeId] += ((1.0f - ((float)mFeatureCounts[currentOutcomeId] / mMaximumFeatureCount)) * mCorrectionParameter);
				}
				sum += outcomeSums[currentOutcomeId];
			}
			
			for (int currentOutcomeId = 0; currentOutcomeId < mOutcomeCount; currentOutcomeId++)
			{
				outcomeSums[currentOutcomeId] /= sum;
			}
		}
				
		/// <summary>
		/// Compute one iteration of GIS and retutn log-likelihood.
		/// </summary>
		/// <returns>The log-likelihood.</returns>
        private float NextIteration()
		{
			// compute contribution of p(a|b_i) for each feature and the new
			// correction parameter
            float logLikelihood = 0.0f;
			mCorrectionFeatureModifier = 0.0f;
			int eventCount = 0;
			int numCorrect = 0;
			int outcomeId;

            for (mTokenID = 0; mTokenID < mTokenCount; mTokenID++)
            {
                Evaluate(mContexts[mTokenID], mModelDistribution);
                for (int currentContext = 0; currentContext < mContexts[mTokenID].Length; currentContext++)
                {
                    mPredicateId = mContexts[mTokenID][currentContext];
                    for (int currentActiveOutcome = 0; currentActiveOutcome < mOutcomePatterns[mPredicateId].Length; currentActiveOutcome++)
                    {
                        outcomeId = mOutcomePatterns[mPredicateId][currentActiveOutcome];
                        mModelExpections[mPredicateId][currentActiveOutcome] += (mModelDistribution[outcomeId] * mNumTimesEventsSeen[mTokenID]);

                        if (mUseSlackParameter)
                        {
                            mCorrectionFeatureModifier += mModelDistribution[mOutcomeId] * mNumTimesEventsSeen[mTokenID];
                        }
                    }
                }

				if (mUseSlackParameter)
				{
					mCorrectionFeatureModifier += (mMaximumFeatureCount - mContexts[mTokenID].Length) * mNumTimesEventsSeen[mTokenID];
				}

                logLikelihood += FastMath.Log(mModelDistribution[mOutcomes[mTokenID]]) * mNumTimesEventsSeen[mTokenID];
				eventCount += mNumTimesEventsSeen[mTokenID];
				
				//calculation solely for the information messages
				int max = 0;
				for (mOutcomeId = 1; mOutcomeId < mOutcomeCount; mOutcomeId++)
				{
					if (mModelDistribution[mOutcomeId] > mModelDistribution[max])
					{
						max = mOutcomeId;
					}
				}
				if (max == mOutcomes[mTokenID])
				{
					numCorrect += mNumTimesEventsSeen[mTokenID];
				}
			}
			NotifyProgress(".");
			
			// compute the new parameter values
			for (mPredicateId = 0; mPredicateId < mPredicateCount; mPredicateId++)
			{
				for (int currentActiveOutcome = 0; currentActiveOutcome < mOutcomePatterns[mPredicateId].Length; currentActiveOutcome++)
				{
					outcomeId = mOutcomePatterns[mPredicateId][currentActiveOutcome];
                    mParameters[mPredicateId][currentActiveOutcome] += (mObservedExpections[mPredicateId][currentActiveOutcome] - FastMath.Log(mModelExpections[mPredicateId][currentActiveOutcome]));
					mModelExpections[mPredicateId][currentActiveOutcome] = 0.0f;// re-initialize to 0.0's
				}
			}

			if (mCorrectionFeatureModifier > 0.0 && mUseSlackParameter)
			{
                mCorrectionParameter += (mCorrectionFeatureObservedExpectation - FastMath.Log(mCorrectionFeatureModifier));
			}

			NotifyProgress(". logLikelihood=" + logLikelihood + "\t" + ((double) numCorrect / eventCount));
			return (logLikelihood);
		}
		
		/// <summary>
		/// Convert the predicate data into the outcome pattern / patterned predicate format used by the GIS models.
		/// </summary>
		private void ConvertPredicates()
		{
			PatternedPredicate[] predicates = new PatternedPredicate[mParameters.Length];
			
			for (mPredicateId = 0; mPredicateId < mPredicateCount; mPredicateId++)
			{
                float[] parameters = mParameters[mPredicateId];
				predicates[mPredicateId] = new PatternedPredicate(_stringIndex.Store(mPredicateLabels[mPredicateId]), parameters);
			}

			OutcomePatternComparer comparer = new OutcomePatternComparer();
			ArrayExtensions.Sort(mOutcomePatterns, predicates, comparer);

            List<int[]> outcomePatterns = new List<int[]>();
			int currentPatternId = 0;
			int predicatesInPattern = 0;
			int[] currentPattern = mOutcomePatterns[0];

			for (mPredicateId = 0; mPredicateId < mPredicateCount; mPredicateId++)
			{
				if (comparer.Compare(currentPattern, mOutcomePatterns[mPredicateId]) == 0)
				{
					predicates[mPredicateId].OutcomePattern = currentPatternId;
					predicatesInPattern++;
				}
				else
				{
					int[] pattern = new int[currentPattern.Length + 1];
					pattern[0] = predicatesInPattern;
					currentPattern.CopyTo(pattern, 1);
					outcomePatterns.Add(pattern);
					currentPattern = mOutcomePatterns[mPredicateId];
					currentPatternId++;
					predicates[mPredicateId].OutcomePattern = currentPatternId;
					predicatesInPattern = 1;
				}
			}
			int[] finalPattern = new int[currentPattern.Length + 1];
			finalPattern[0] = predicatesInPattern;
			currentPattern.CopyTo(finalPattern, 1);
			outcomePatterns.Add(finalPattern);

			mOutcomePatterns = outcomePatterns.ToArray();
            mPredicates = new Dictionary<Compact<string>, PatternedPredicate>(predicates.Length);
			for (mPredicateId = 0; mPredicateId < mPredicateCount; mPredicateId++)
			{
                // FIXME: This is degenerate code that pads the predicate dictionary to make
                // sure the arrays line up. It interferes with model accuracy
                Compact<string> key = predicates[mPredicateId].Name;
                string id = _stringIndex.Retrieve(key);
			    while (mPredicates.ContainsKey(key))
			    {
			        id = id + "_";
                    key = _stringIndex.Store(id);
			    }
                mPredicates.Add(key, predicates[mPredicateId]);
			}
		}

		#endregion

		#region IGisModelReader implementation
		
		/// <summary>
		/// The correction constant for the model produced as a result of training.
		/// </summary>
		public int CorrectionConstant
		{
			get
			{
				return mMaximumFeatureCount;
			}
		}
	
		/// <summary>
		/// The correction parameter for the model produced as a result of training.
		/// </summary>
        public float CorrectionParameter
		{
			get
			{
				return mCorrectionParameter;
			}
		}
	
		/// <summary>
		/// Obtains the outcome labels for the model produced as a result of training.
		/// </summary>
		/// <returns>
		/// Array of outcome labels.
		/// </returns>
		public string[] GetOutcomeLabels()
		{
			return mOutcomeLabels;
		}
	
		/// <summary>
		/// Obtains the outcome patterns for the model produced as a result of training.
		/// </summary>
		/// <returns>
		/// Array of outcome patterns.
		/// </returns>
		public int[][] GetOutcomePatterns()
		{
			return mOutcomePatterns;
		}

		/// <summary>
		/// Obtains the predicate data for the model produced as a result of training.
		/// </summary>
		/// <returns>
		/// Dictionary containing PatternedPredicate objects.
		/// </returns>
        public Dictionary<Compact<string>, PatternedPredicate> GetPredicates()
		{
			return mPredicates;
		}

		/// <summary>
		/// Returns trained model information for a predicate, given the predicate label.
		/// </summary>
		/// <param name="predicateLabel">
		/// The predicate label to fetch information for.
		/// </param>
		/// <param name="featureCounts">
		/// Array to be passed in to the method; it should have a length equal to the number of outcomes
		/// in the model.  The method increments the count of each outcome that is active in the specified
		/// predicate.
		/// </param>
		/// <param name="outcomeSums">
		/// Array to be passed in to the method; it should have a length equal to the number of outcomes
		/// in the model.  The method adds the parameter values for each of the active outcomes in the
		/// predicate.
		/// </param>
        public void GetPredicateData(string predicateLabel, int[] featureCounts, float[] outcomeSums)
		{
		    Compact<string> predicateLabelEncoded = _stringIndex.GetIndex(predicateLabel);
            PatternedPredicate predicate = mPredicates.ContainsKey(predicateLabelEncoded) ?
                mPredicates[predicateLabelEncoded] :
                null;
			if (predicate != null)
			{
				int[] activeOutcomes = mOutcomePatterns[predicate.OutcomePattern];
					
				for (int currentActiveOutcome = 1; currentActiveOutcome < activeOutcomes.Length; currentActiveOutcome++)
				{
					int outcomeIndex = activeOutcomes[currentActiveOutcome];
					featureCounts[outcomeIndex]++;
					outcomeSums[outcomeIndex] += predicate.GetParameter(currentActiveOutcome - 1);
				}
			}
		}
		#endregion

		private class OutcomePatternComparer : IComparer<int[]>
		{

			internal OutcomePatternComparer()
			{
			}

			/// <summary>
			/// Compare two outcome patterns and determines which comes first,
			/// based on the outcome ids (lower outcome ids first)
			/// </summary>
            /// <param name="firstPattern">
			/// First outcome pattern to compare.
			/// </param>
            /// <param name="secondPattern">
			/// Second outcome pattern to compare.
			/// </param>
			/// <returns></returns>
            public virtual int Compare(int[] firstPattern, int[] secondPattern)
			{			
				int smallerLength = (firstPattern.Length > secondPattern.Length ? secondPattern.Length : firstPattern.Length);
			
				for (int currentOutcome = 0; currentOutcome < smallerLength; currentOutcome++)
				{
					if (firstPattern[currentOutcome] < secondPattern[currentOutcome])
					{
						return - 1;
					}
					else if (firstPattern[currentOutcome] > secondPattern[currentOutcome])
					{
						return 1;
					}
				}
			
				if (firstPattern.Length < secondPattern.Length)
				{
					return - 1;
				}
				else if (firstPattern.Length > secondPattern.Length)
				{
					return 1;
				}
			
				return 0;
			}
		}
	}

	/// <summary>
	/// Event arguments class for training progress events.
	/// </summary>
	public class TrainingProgressEventArgs : EventArgs
	{
		private string mMessage;
	
		/// <summary>
		/// Constructor for the training progress event arguments.
		/// </summary>
		/// <param name="message">
		/// Information message about the progress of training.
		/// </param>
		public TrainingProgressEventArgs(string message)
		{
			mMessage = message;
		}

		/// <summary>
		/// Information message about the progress of training.
		/// </summary>
		public string Message 
		{
			get
			{
				return mMessage;
			}
		}
	}

	/// <summary>
	/// Event handler delegate for the training progress event.
	/// </summary>
	public delegate void TrainingProgressEventHandler(object sender, TrainingProgressEventArgs e);


}
