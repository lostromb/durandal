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

//This file is based on the BinaryGISModelWriter.java source file found in the
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
using System.IO;
using Durandal.Common.Collections.Indexing;

namespace Durandal.Common.Statistics.SharpEntropy.IO
{
    /// <summary>
	/// A writer for GIS models that saves models in a binary format. This format is not the one
	/// used by the <see cref="SharpEntropy.IO.JavaBinaryGisModelWriter">java version of MaxEnt</see>.
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
	/// based on BinaryGISModelWriter.java $Revision: 1.1.1.1 $, $Date: 2001/10/23 14:06:53 $
	/// </version>
	public class BinaryGisModelWriter : GisModelWriter
	{
		private Stream mOutput;
		private byte[] mBuffer = new byte[7];
		private System.Text.Encoding mEncoding = System.Text.Encoding.UTF8;
        private ICompactIndex<string> _stringIndex; 

		/// <summary>
		/// Default constructor.
		/// </summary>
        public BinaryGisModelWriter(ICompactIndex<string> stringIndex)
            : base(stringIndex)
		{
		    _stringIndex = stringIndex;
		}
			
		/// <summary>
		/// Takes a GIS model and a Stream and
		/// writes the model to that Stream.
		/// </summary>
		/// <param name="model">
		/// The GIS model which is to be persisted.
		/// </param>
		/// <param name="dataOutputStream">
		/// The Stream which will be used to persist the model.
		/// </param>
		public void Persist(GisModel model, Stream dataOutputStream)
		{
			using (mOutput = dataOutputStream)
			{
				base.Persist(model);
			}
		}
		
		/// <summary>
		/// Writes a UTF-8 encoded string to the model file.
		/// </summary>
		/// <param name="data">
		/// The string data to be persisted.
		/// </param>
		protected  override void WriteString(string data)
		{
			mOutput.WriteByte((byte)mEncoding.GetByteCount(data));
			mOutput.Write(mEncoding.GetBytes(data), 0, mEncoding.GetByteCount(data));
		}
		
		/// <summary>
		/// Writes a 32-bit signed integer to the model file.
		/// </summary>
		/// <param name="data">
		/// The integer data to be persisted.
		/// </param>
		protected override void WriteInt32(int data)
		{
			mBuffer = BitConverter.GetBytes(data);
			mOutput.Write(mBuffer, 0, 4);
		}
		
		/// <summary>
		/// Writes a single-precision floating point number to the model file.
		/// </summary>
		/// <param name="data">
		/// The floating point data to be persisted.
		/// </param>
		protected override void WriteFloat(float data)
		{
			mBuffer = BitConverter.GetBytes(data);
			mOutput.Write(mBuffer, 0, 4);
		}

		/// <summary>
		/// Writes the predicate data to the file in a more efficient format to that implemented by
		/// GisModelWriter.
		/// </summary>
		/// <param name="model">
		/// The GIS model containing the predicate data to be persisted.
		/// </param>
		protected override void WritePredicates(GisModel model)
		{
			int[][] outcomePatterns = model.GetOutcomePatterns();
			PatternedPredicate[] predicates = GetPredicates();

			//write the number of outcome patterns
			WriteInt32(outcomePatterns.Length);

			//write the number of predicates
			WriteInt32(predicates.Length);

			int currentPredicate = 0;

			for (int currentOutcomePattern = 0; currentOutcomePattern < outcomePatterns.Length; currentOutcomePattern++)
			{
				//write how many outcomes in this pattern
				WriteInt32(outcomePatterns[currentOutcomePattern].Length);

				//write the outcomes in this pattern (the first value contains the number of predicates in the pattern
				//rather than an outcome)
				for (int currentOutcome = 0; currentOutcome < outcomePatterns[currentOutcomePattern].Length; currentOutcome++)
				{
					WriteInt32(outcomePatterns[currentOutcomePattern][currentOutcome]);
				}

				//write predicates for this pattern
				while (currentPredicate < predicates.Length && predicates[currentPredicate].OutcomePattern == currentOutcomePattern)
				{
					WriteString(_stringIndex.Retrieve(predicates[currentPredicate].Name));
					for (int currentParameter = 0; currentParameter < predicates[currentPredicate].ParameterCount; currentParameter++)
					{
						WriteFloat(predicates[currentPredicate].GetParameter(currentParameter));
					}
					currentPredicate++;
				}
			}
		}

	}
}
