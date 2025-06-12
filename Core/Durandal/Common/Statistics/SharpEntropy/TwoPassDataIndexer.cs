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

//This file is based on the TwoPassDataIndexer.java source file found in the
//original java implementation of MaxEnt.  That source file contains the following header:

//Copyright (C) 2003 Thomas Morton
//
//This library is free software; you can redistribute it and/or
//modify it under the terms of the GNU Lesser General Public
//License as published by the Free Software Foundation; either
//version 2.1 of the License, or (at your option) any later version.
//
//This library is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//GNU General Public License for more details.
//
//You should have received a copy of the GNU Lesser General Public
//License along with this program; if not, write to the Free Software
//Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Durandal.API;
using Durandal.Common.NLP.Feature;
using Durandal.Common.File;
using System.Threading;
using Durandal.Common.Utils;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Statistics.SharpEntropy
{
	/// <summary>
	/// Collecting event and context counts by making two passes over the events.  The
	/// first pass determines which contexts will be used by the model, and the
	/// second pass creates the events in memory containing only the contexts which 
	/// will be used.  This greatly reduces the amount of memory required for storing
	/// the events.  During the first pass a temporary event file is created which
	/// is read during the second pass.
	/// </summary>
	/// /// <author>  
	/// Tom Morton
	/// </author>
	/// /// /// <author>  
	/// Richard J. Northedge
	/// </author>
	public class TwoPassDataIndexer : AbstractDataIndexer
	{
        private IFileSystem _fileSystem;
        
        /// <summary>
		/// One argument constructor for DataIndexer which calls the two argument
		/// constructor assuming no cutoff.
		/// </summary>
		/// <param name="eventReader">
		/// An ITrainingEventReader which contains the list of all the events
		/// seen in the training data.
		/// </param>
		/// <param name="fileSystem">A file system</param>
		public TwoPassDataIndexer(ITrainingEventReader eventReader, IFileSystem fileSystem) : this(eventReader, 0, fileSystem)
		{
		}
		
		/// <summary> 
		/// Two argument constructor for TwoPassDataIndexer.
		/// </summary>
		/// <param name="eventReader">
		/// An ITrainingEventReader which contains the a list of all the events
		/// seen in the training data.
		/// </param>
		/// <param name="cutoff">
		/// The minimum number of times a predicate must have been
		/// observed in order to be included in the model.
		/// </param>
		/// <param name="fileSystem">A file system</param>
		public TwoPassDataIndexer(ITrainingEventReader eventReader, int cutoff, IFileSystem fileSystem)
		{
            Dictionary<string, int> predicateIndex;
			List<ComparableEvent> eventsToCompare;
            _fileSystem = fileSystem;

            predicateIndex = new Dictionary<string, int>();
            //NotifyProgress("Indexing events using cutoff of " + cutoff + "\n");

            //NotifyProgress("\tComputing event counts...  ");

            VirtualPath tempFile = CreateRandomTempFileName();


            int eventCount = ComputeEventCounts(eventReader, tempFile, predicateIndex, cutoff);
			//NotifyProgress("done. " + eventCount + " events");
			
			//NotifyProgress("\tIndexing...  ");
			
			using (FileEventReader fileEventReader = new FileEventReader(tempFile, _fileSystem))
			{
				eventsToCompare = Index(eventCount, fileEventReader, predicateIndex);
			}
			
			if (_fileSystem.Exists(tempFile))
			{
                _fileSystem.Delete(tempFile);
			}
			
			//NotifyProgress("done.");
			
			//NotifyProgress("Sorting and merging events... ");
			SortAndMerge(eventsToCompare);
			//NotifyProgress("Done indexing.");
		}

        private static VirtualPath CreateRandomTempFileName()
        {
            return new VirtualPath(RuntimeDirectoryName.CACHE_DIR + "\\" + Guid.NewGuid().ToString("N") + ".tmp");
        }
		
		/// <summary>
		/// Reads events from <tt>eventStream</tt> into a dictionary.  The
		/// predicates associated with each event are counted and any which
		/// occur at least <tt>cutoff</tt> times are added to the
		/// <tt>predicatesInOut</tt> map along with a unique integer index.
		/// </summary>
		/// <param name="eventReader">
		/// an <code>ITrainingEventReader</code> value
		/// </param>
		/// <param name="eventStoreFile">
		/// a file name to which the events are written to for later processing.
		/// </param>
		/// <param name="predicatesInOut">
		/// a <code>Dictionary</code> value
		/// </param>
		/// <param name="cutoff">
		/// an <code>int</code> value
		/// </param>
        private int ComputeEventCounts(ITrainingEventReader eventReader, VirtualPath eventStoreFile, Dictionary<string, int> predicatesInOut, int cutoff)
		{
            Dictionary<string, int> counter = new Dictionary<string, int>();
			int predicateIndex = 0;
			int eventCount = 0;

			using (StreamWriter eventStoreWriter = new StreamWriter(_fileSystem.OpenStream(eventStoreFile, FileOpenMode.Create, FileAccessMode.Write)))
			{
				while (eventReader.HasNext())
				{
					TrainingEvent currentTrainingEvent = eventReader.ReadNextEvent();
					eventCount++;
					eventStoreWriter.Write(FileEventReader.ToLine(currentTrainingEvent));
					string[] eventContext = currentTrainingEvent.Context;
					for (int currentPredicate = 0; currentPredicate < eventContext.Length; currentPredicate++)
					{
						if (!predicatesInOut.ContainsKey(eventContext[currentPredicate]))
						{
							if (counter.ContainsKey(eventContext[currentPredicate]))
							{
								counter[eventContext[currentPredicate]]++;
							}
							else
							{
								counter.Add(eventContext[currentPredicate], 1);
							}
							if (counter[eventContext[currentPredicate]] >= cutoff)
							{
								predicatesInOut.Add(eventContext[currentPredicate], predicateIndex++);
								counter.Remove(eventContext[currentPredicate]);
							}
						}
					}
				}
			}
			return eventCount;
		}

        private List<ComparableEvent> Index(int eventCount, ITrainingEventReader eventReader, Dictionary<string, int> predicateIndex)
		{
            Dictionary<string, int> outcomeMap = new Dictionary<string, int>();
			int outcomeCount = 0;
            List<ComparableEvent> eventsToCompare = new List<ComparableEvent>(eventCount);
            List<int> indexedContext = new List<int>();
			while (eventReader.HasNext())
			{
				TrainingEvent currentTrainingEvent = eventReader.ReadNextEvent();
				string[] eventContext = currentTrainingEvent.Context;
				ComparableEvent comparableEvent;
				
				int	outcomeId;
				string outcome = currentTrainingEvent.Outcome;
				
				if (outcomeMap.ContainsKey(outcome))
				{
					outcomeId = outcomeMap[outcome];
				}
				else
				{
					outcomeId = outcomeCount++;
					outcomeMap.Add(outcome, outcomeId);
				}
				
				for (int currentPredicate = 0; currentPredicate < eventContext.Length; currentPredicate++)
				{
					string predicate = eventContext[currentPredicate];
					if (predicateIndex.ContainsKey(predicate))
					{
						indexedContext.Add(predicateIndex[predicate]);
					}
				}
				
				// drop events with no active features
				if (indexedContext.Count > 0)
				{
					comparableEvent = new ComparableEvent(outcomeId, indexedContext.ToArray());
					eventsToCompare.Add(comparableEvent);
				}
				else
				{
					//"Dropped event " + currentTrainingEvent.Outcome + ":" + currentTrainingEvent.Context);
				}
				// recycle the list
				indexedContext.Clear();
			}
			SetOutcomeLabels(ToIndexedStringArray(outcomeMap));
			SetPredicateLabels(ToIndexedStringArray(predicateIndex));
			return eventsToCompare;
		}
	}
	
	class FileEventReader : ITrainingEventReader, IDisposable
	{
        private StreamReader mReader;
		private string mCurrentLine;
		private char[] mWhitespace;
        private int _disposed = 0;

        public FileEventReader(VirtualPath fileName, IFileSystem fileSystem)
		{
            mReader = new StreamReader(fileSystem.OpenStream(fileName, FileOpenMode.Open, FileAccessMode.Read), Encoding.UTF8);
			mWhitespace = new char[] {'\t', '\n', '\r', ' '};
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~FileEventReader()
		{
			Dispose(false);
        }
#endif

        public virtual bool HasNext()
		{
				mCurrentLine = mReader.ReadLine();
				return (mCurrentLine != null);
		}
		
		public virtual TrainingEvent ReadNextEvent()
		{
            // Original implementation
            /*string[] tokens = mCurrentLine.Split(mWhitespace);
            string outcome = tokens[0];
            string[] context = new string[tokens.Length - 1];
            ArrayExtensions.MemCopy(tokens, 1, context, 0, tokens.Length - 1);

            return (new TrainingEvent(outcome, context));*/

            // String.Split is actually a fairly costly operation,
            // so we do it manually here for performance
            List<string> context = new List<string>();
            string outcome = null;

            int curIndex = mCurrentLine.IndexOfAny(mWhitespace);
            int prevIndex = 0;
            outcome = mCurrentLine.Substring(0, curIndex);
            
            while (curIndex >= 0)
            {
                prevIndex = curIndex + 1;
                curIndex = mCurrentLine.IndexOfAny(mWhitespace, prevIndex);
                if (curIndex < 0)
                {
                    context.Add(mCurrentLine.Substring(prevIndex));
                }
                else
                {
                    context.Add(mCurrentLine.Substring(prevIndex, curIndex - prevIndex));
                }
            }

			return (new TrainingEvent(outcome, context.ToArray()));
		}
		
		public static string ToLine(TrainingEvent eventToConvert)
		{
			System.Text.StringBuilder lineBuilder = new System.Text.StringBuilder();
			lineBuilder.Append(eventToConvert.Outcome);
			string[] context = eventToConvert.Context;
			for (int contextIndex = 0, contextLength = context.Length; contextIndex < contextLength; contextIndex++)
			{
				lineBuilder.Append(" " + context[contextIndex]);
			}
			lineBuilder.Append(System.Environment.NewLine);
			return lineBuilder.ToString();
		}

		public void Dispose() 
		{
			Dispose(true);
			GC.SuppressFinalize(this); 
		}

		protected virtual void Dispose(bool disposing) 
		{
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing) 
			{
				mReader.Dispose();
			}
		}
	}
}
