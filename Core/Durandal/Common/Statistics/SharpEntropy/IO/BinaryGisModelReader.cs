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

//This file is based on the BinaryGISModelReader.java source file found in the
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
using System.IO;
using Durandal.Common.Collections.Indexing;

namespace Durandal.Common.Statistics.SharpEntropy.IO
{
    /// <summary>
	/// A reader for GIS models stored in a binary format.  This format is not the one
	/// used by the <see cref="SharpEntropy.IO.JavaBinaryGisModelReader">java version of MaxEnt</see>.
	/// It has two main differences, designed for performance when loading the data
	/// from file: first, it uses big endian data values, which is native for C#, and secondly it
	/// encodes the outcome patterns and values in a more efficient manner.
	/// </summary>
	/// <author> 
	/// Jason Baldridge
	/// </author>
	/// <author>
	/// Richard J. Northedge
	/// </author>
	/// <version>
	/// based on BinaryGISModelReader.java, $Revision: 1.1.1.1 $, $Date: 2001/10/23 14:06:53 $
	/// </version>
	public class BinaryGisModelReader : GisModelReader
	{
		private Stream mInput;
		private byte[] mBuffer;
		private int mStringLength = 0;
		private System.Text.Encoding mEncoding = System.Text.Encoding.UTF8;
        private readonly ICompactIndex<string> _stringIndex;

		/// <summary>
		/// Constructor which directly instantiates the Stream containing
		/// the model contents.
		/// </summary>
		/// <param name="dataInputStream">
		/// The Stream containing the model information.
		/// </param>
		/// <param name="stringIndex">A compact index for compressing string data</param>
		public BinaryGisModelReader(Stream dataInputStream, ICompactIndex<string> stringIndex)
            : base(stringIndex)
		{
		    _stringIndex = stringIndex;
            using (mInput = dataInputStream)
			{
				mBuffer = new byte[256];
				base.ReadModel();
			}
		}

		/// <summary>
		/// Reads a 32-bit signed integer from the model file.
		/// </summary>
		protected override int ReadInt32()
		{
			mInput.Read(mBuffer, 0, 4);
			return BitConverter.ToInt32(mBuffer, 0);
		}
		
		/// <summary>
		/// Reads a single-precision floating point number from the model file.
		/// </summary>
        protected override float ReadFloat()
		{
			mInput.Read(mBuffer, 0, 4);
			return BitConverter.ToSingle(mBuffer, 0);
		}
		
		/// <summary>
		/// Reads a UTF-8 encoded string from the model file.
		/// </summary>
		protected override string ReadString()
		{
			mStringLength = mInput.ReadByte();
			mInput.Read(mBuffer, 0, mStringLength);
			return mEncoding.GetString(mBuffer, 0, mStringLength);
		}

		/// <summary>
		/// Reads the predicate data from the file in a more efficient format to that implemented by
		/// GisModelReader.
		/// </summary>
		/// <param name="outcomePatterns">
		/// Jagged 2-dimensional array of integers that will contain the outcome patterns for the model
		/// after this method is called.
		/// </param>
		/// <param name="predicates">
		/// Dictionary that will contain the predicate information for the model
		/// after this method is called.
		/// </param>
        protected override void ReadPredicates(out int[][] outcomePatterns, out Dictionary<Compact<string>, PatternedPredicate> predicates)
		{
			//read from the model how many outcome patterns there are
			int outcomePatternCount = ReadInt32();
			outcomePatterns = new int[outcomePatternCount][];
			int currentOutcomePatternLength = 0;
			//read from the model how many predicates there are
            predicates = new Dictionary<Compact<string>, PatternedPredicate>(ReadInt32());

			//for each outcome pattern in the model
			for (int currentOutcomePattern = 0; currentOutcomePattern < outcomePatternCount; currentOutcomePattern++)
			{
				//read the number of outcomes in this pattern.  This number is 1 greater than the real number of outcomes
				//in the pattern, because the 0th value contains the number of predicates that use this pattern.
				currentOutcomePatternLength = ReadInt32();
				outcomePatterns[currentOutcomePattern] = new int[currentOutcomePatternLength];
				//read in the outcomes for this pattern
				for (int currentOutcome = 0; currentOutcome <currentOutcomePatternLength; currentOutcome++)
				{
					outcomePatterns[currentOutcomePattern][currentOutcome] = ReadInt32();
				}
				//read in the details of the predicates in this pattern
				for (int currentPredicate = 0; currentPredicate < outcomePatterns[currentOutcomePattern][0]; currentPredicate++)
				{
					string predicateName = ReadString();
					//we know that the number of parameters in this predicate will be the number of outcomes in the pattern
                    float[] parameters = new float[currentOutcomePatternLength - 1];
					//read in the parameters for this predicate
					for (int currentParameter = 0; currentParameter < currentOutcomePatternLength - 1; currentParameter++)
					{
                        parameters[currentParameter] = ReadFloat();
					}

                    // FIXME: This is degenerate code that pads the predicate dictionary to make
                    // sure the arrays line up. It interferes with model accuracy
				    Compact<string> newPredicate = _stringIndex.Store(predicateName);
                    while (predicates.ContainsKey(newPredicate))
                    {
                        predicateName = predicateName + "_";
                        newPredicate = _stringIndex.Store(predicateName);
                    }
                    predicates.Add(newPredicate, new PatternedPredicate(currentOutcomePattern, parameters));
				}
			}
		}
	}
}
