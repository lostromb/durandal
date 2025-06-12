using Durandal.Common.Instrumentation;
using Durandal.Common.Utils;
using Durandal.Common.Cache;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Durandal.Common.MathExt
{
    /// <summary>
    /// Implements a running percentile counter for floating point metrics.
    /// Instances of this class are NOT thread-safe.
    /// </summary>
    public class MovingPercentile
    {
        /// <summary>
        /// Used to reuse heap objects to reduce allocation + garbage collection in this class
        /// </summary>
        private static LockFreeCache<PercentileNode> RECLAIMED_NODES = new LockFreeCache<PercentileNode>(128);
        
        // This class is implemented internally with a triply-linked list.
        // Each measurement is a node in a double-linked list, with each node representing a number.
        // This is the "value" list. It is sorted by measurement value.
        // Each node also tracks which node was created immediately after it, in one direction only.
        // This is the "age" list. It is used to prune the observations in a FIFO manner.
        // There is also a list of PercentileCursor objects which point to places in the value list.
        // For example, the p95 cursor will point to the 95/100th entry in the value list. This is just for optimizing lookup of percentiles.

        /// <summary>
        /// List of cursors that point to particular percentile values.
        /// </summary>
        private readonly PercentileCursor[] _cursors;

        /// <summary>
        /// Oldest value in the age list
        /// </summary>
        private PercentileNode _oldest;

        /// <summary>
        /// Newest value in the age list
        /// </summary>
        private PercentileNode _newest;

        /// <summary>
        /// First value in the value list
        /// </summary>
        private PercentileNode _first;

        /// <summary>
        /// Last value in the value list
        /// </summary>
        private PercentileNode _last;

        /// <summary>
        /// Current number of samples in linked list
        /// </summary>
        private int _numSamples;

        /// <summary>
        /// Maximum number of samples in linked list
        /// </summary>
        private int _maxSamples;
        
        /// <summary>
        /// Initializes a moving percentile tracker.
        /// </summary>
        /// <param name="sampleSize">The number of samples to track. This should be at least 100</param>
        /// <param name="percentilesToTrack">A list of percentiles value to keep track of. Each one is a floating point between 0 and 1.0 exclusive
        /// If you don't specify anything here you can still fetch arbitrary percentile values, but fetching will be non-optimal and percentiles will not be reported to any IMetricCollectors</param>
        public MovingPercentile(int sampleSize, params double[] percentilesToTrack)
        {
            if (sampleSize < 1)
            {
                throw new ArgumentOutOfRangeException("Sample size must be greater than zero");
            }
            
            _numSamples = 0;
            _maxSamples = sampleSize;
            _cursors = new PercentileCursor[percentilesToTrack.Length];
            Array.Sort(percentilesToTrack);
            for (int c = 0; c < _cursors.Length; c++)
            {
                if (percentilesToTrack[c] <= 0.0 || percentilesToTrack[c] >= 1.0)
                {
                    throw new ArgumentOutOfRangeException("Percentile must be between 0.0 and 1.0 exclusive");
                }

                _cursors[c] = new PercentileCursor(percentilesToTrack[c]);
            }
        }

        /// <summary>
        /// Adds an observation to the metric reporter and updates all percentiles accordingly.
        /// </summary>
        /// <param name="measurement">The measurement to add.</param>
        public void Add(double measurement)
        {
            if (double.IsNaN(measurement) || double.IsInfinity(measurement))
            {
                return;
            }

            // Reclaim the last removed node if possible. This saves us from constantly having to do heap allocations
            PercentileNode newNode = RECLAIMED_NODES.TryDequeue();
            if (newNode != null)
            {
                newNode.Measurement = measurement;
            }
            else
            {
                newNode = new PercentileNode(measurement);
            }

            // Is this the first sample?
            if (_numSamples == 0)
            {
                _numSamples++;
                _oldest = newNode;
                _newest = newNode;
                _first = newNode;
                _last = newNode;

                // Initialize cursors here too
                for (int c = 0; c < _cursors.Length; c++)
                {
                    _cursors[c].Node = newNode;
                }
            }
            else
            {
                // Insert new node into the age list
                _newest.Newer = newNode;
                _newest = newNode;

                // Expire oldest node if needed
                if (_numSamples >= _maxSamples)
                {
                    // Remove oldest from value list
                    if (_oldest.Prev != null)
                    {
                        _oldest.Prev.Next = _oldest.Next;
                    }
                    if (_oldest.Next != null)
                    {
                        _oldest.Next.Prev = _oldest.Prev;
                    }
                    if (_first == _oldest)
                    {
                        _first = _oldest.Next;
                    }
                    if (_last == _oldest)
                    {
                        _last = _oldest.Prev;
                    }

                    // and update cursors
                    for (int c = 0; c < _cursors.Length; c++)
                    {
                        PercentileCursor cur = _cursors[c];
                        if (cur.Node == _oldest)
                        {
                            if (_oldest.Prev == null)
                            {
                                // cursor points to oldest which is the first in the value list
                                cur.Node = _oldest.Next;
                            }
                            else if (_oldest.Next == null)
                            {
                                // cursor points to oldest which is the last in the value list
                                cur.Node = _oldest.Prev;
                                cur.ListIndex--;
                            }
                            else
                            {
                                // cursor points somewhere in the middle of the value list
                                cur.Node = _oldest.Prev;
                                cur.ListIndex--;
                            }
                        }
                        else if (cur.Node.Measurement > _oldest.Measurement)
                        {
                            cur.ListIndex--;
                        }
                    }

                    // Then remove oldest from the age list
                    PercentileNode nextOldest = _oldest.Newer;
                    _oldest.Reset();
                    RECLAIMED_NODES.TryEnqueue(_oldest);
                    _oldest = nextOldest;
                    _numSamples--;
                }

                // Find where the new node needs to go, using cursors to help optimize the insertion sort
                // OPT: If we allow iterating in both directions, and not just forwards, we can potentially speed up this insertion
                PercentileNode iter = null;
                foreach (PercentileCursor cur in _cursors)
                {
                    if (cur.Node.Measurement < measurement)
                    {
                        iter = cur.Node;
                    }
                    else
                        break;
                }

                if (iter == null)
                {
                    iter = _first;
                }
                while (iter.Next != null && iter.Next.Measurement < measurement)
                {
                    iter = iter.Next;
                }

                // Iter should now point to the place before the insertion needs to happen. OR, it points to _first, and insertion happens at end of list
                // Note: in the case of conflicting values at insertion, the new value gets inserted BEFORE the old one
                // Is new node the new head node?
                if (measurement <= iter.Measurement)
                {
                    iter.Prev = newNode;
                    newNode.Next = iter;
                    _first = newNode;
                }
                else
                {
                    // Iter now points to the non-null percentile node before the measurement we need to insert
                    // Insert new node into value list
                    if (iter.Next != null)
                    {
                        iter.Next.Prev = newNode;
                    }
                    newNode.Next = iter.Next;
                    iter.Next = newNode;
                    newNode.Prev = iter;

                    if (newNode.Next == null)
                    {
                        // New node is new last node
                        _last = newNode;
                    }
                }

                _numSamples++;

                // Finally, update cursors again
                for (int c = 0; c < _cursors.Length; c++)
                {
                    PercentileCursor cur = _cursors[c];
                    // adjust list index to reflect the insertion that happened above
                    if (cur.Node.Measurement >= measurement)
                    {
                        cur.ListIndex++;
                    }

                    int targetPos = ConvertPercentileFloatToListIndex(cur.TargetPercentile);
                    // The difference in targetPos should never be greater than 1, unless we defer cursor updates for some reason
                    if (targetPos > cur.ListIndex &&
                        cur.Node.Next != null)
                    {
                        cur.Node = cur.Node.Next;
                        cur.ListIndex++;
                    }
                    else if (targetPos < cur.ListIndex &&
                        cur.Node.Prev != null)
                    {
                        cur.Node = cur.Node.Prev;
                        cur.ListIndex--;
                    }
                }
            }

            //Debug.WriteLine("Done");
            //Debug.WriteLine(DumpLinkedList());
            //Debug.WriteLine(DumpCursors());
            //ValidateIntegrity();
        }

        /// <summary>
        /// Clears all observations
        /// </summary>
        public void Clear()
        {
            _numSamples = 0;
            _oldest = null;
            _newest = null;
            _first = null;
            _last = null;
            
            for (int c = 0; c < _cursors.Length; c++)
            {
                _cursors[c].ListIndex = 0;
                _cursors[c].Node = null;
            }
        }

        /// <summary>
        /// Fetches a percentile observation from the history.
        /// </summary>
        /// <param name="percentile">The percentile, between 0.0 and 1.0 exclusive.
        /// Optimally you would fetch a percentile value that this class was initialized with, but you can technically fetch
        /// any arbitrary percentile value here and it will work, just more slowly.</param>
        /// <returns>The observed value at that percentile.</returns>
        public double GetPercentile(double percentile)
        {
            if (_numSamples == 0)
            {
                return 0;
            }

            // First see if we have a cursor into this percentile already
            int targetPos = ConvertPercentileFloatToListIndex(percentile);
            PercentileCursor prevCursor = null;
            int closestDistance = int.MaxValue;
            foreach (PercentileCursor cur in _cursors)
            {
                if (cur.ListIndex == targetPos)
                {
                    // Cursor matches exactly
                    return cur.Node.Measurement;
                }
                else
                {
                    int dist = cur.ListIndex - targetPos;
                    if (dist < 0)
                    {
                        dist = 0 - dist;
                    }
                    if (dist < closestDistance)
                    {
                        // Cache the nearest cursor previous the percentile we want
                        prevCursor = cur;
                        closestDistance = dist;
                    }
                }
            }

            // Slow lookup - start from the nearest cursor, or head of value list, and iterate to the target index
            PercentileNode iter = prevCursor == null ? _first : prevCursor.Node;
            int startIndex = prevCursor == null ? 0 : prevCursor.ListIndex;
            if (startIndex < targetPos)
            {
                for (int c = startIndex; c < targetPos; c++)
                {
                    iter = iter.Next;
                }
            }
            else if (startIndex > targetPos)
            {
                for (int c = startIndex; c > targetPos; c--)
                {
                    iter = iter.Prev;
                }
            }
            
            return iter.Measurement;
        }

        /// <summary>
        /// Gets the number of observations that have been recorded in this percentile tracker, capped by max samples.
        /// </summary>
        public int NumSamples
        {
            get
            {
                return _numSamples;
            }
        }

        public IEnumerable<Tuple<double, double>> GetPercentiles()
        {
            foreach (var cursor in _cursors)
            {
                yield return new Tuple<double, double>(cursor.TargetPercentile, cursor.Node.Measurement);
            }
        }

        public IEnumerable<double> GetMeasurements()
        {
            PercentileNode iter = _first;
            while (iter != null)
            {
                yield return iter.Measurement;
                iter = iter.Next;
            }
        }

        //private string DumpLinkedList()
        //{
        //    StringBuilder builder = new StringBuilder();
        //    PercentileNode iter = _first;
        //    int idx = 0;
        //    while (iter != null)
        //    {
        //        builder.AppendFormat("(Idx {0} Id {1} Meas {2:F2}) ", idx, iter.Id, iter.Measurement);
        //        iter = iter.Next;
        //        idx++;
        //    }

        //    return builder.ToString();
        //}

        //private string DumpCursors()
        //{
        //    StringBuilder builder = new StringBuilder();
        //    for (int c = 0; c < _cursors.Length; c++)
        //    {
        //        PercentileCursor cur = _cursors[c];
        //        builder.AppendFormat("(Idx {0} Id {1} Perc {2:F3}) ", cur.ListIndex, cur.Node.Id, cur.TargetPercentile);
        //    }

        //    return builder.ToString();
        //}

        public override string ToString()
        {
            using (PooledStringBuilder builder = StringBuilderPool.Rent())
            {
                for (int c = 0; c < _cursors.Length; c++)
                {
                    PercentileCursor cur = _cursors[c];
                    if (cur.Node == null)
                    {
                        builder.Builder.AppendFormat("p{0:F3} ---, ", cur.TargetPercentile);
                    }
                    else
                    {
                        builder.Builder.AppendFormat("p{0:F3} {1}, ", cur.TargetPercentile, cur.Node.Measurement);
                    }
                }

                return builder.Builder.ToString();
            }
        }

        private void ValidateIntegrity()
        {
            if (_numSamples == 0)
            {
                return;
            }
            
            // Ensure that the age list has exactly the right number of entries
            PercentileNode iter = _oldest;
            int numNodes = 0;
            while (iter != null && numNodes < 100000)
            {
                numNodes++;
                iter = iter.Newer;
            }

            if (numNodes != _numSamples)
            {
                throw new Exception("Age list has incorrect number of entries");
            }

            // Ensure that the value list is consistent
            if (_first.Prev != null)
            {
                throw new Exception("First node is not actually first");
            }
            if (_last.Next != null)
            {
                throw new Exception("Last node is not actually last");
            }

            iter = _first;
            numNodes = 0;
            while (iter != null && numNodes < 100000)
            {
                if (iter.Next != null &&
                    iter.Next.Prev != iter)
                {
                    throw new Exception("Linked list integrity failed");
                }
                if (iter.Prev != null &&
                    iter.Prev.Next != iter)
                {
                    throw new Exception("Linked list integrity failed");
                }

                // Validate cursor indexes
                foreach (PercentileCursor cursor in _cursors)
                {
                    if (cursor.Node == iter &&
                        cursor.ListIndex != numNodes)
                    {
                        throw new Exception("Cursor index is incorrect");
                    }
                }

                numNodes++;
                iter = iter.Next;
            }

            if (numNodes != _numSamples)
            {
                throw new Exception("Index list has incorrect number of entries");
            }
        }

        /// <summary>
        /// Converts a percentile such as "99" into a floating point value like 0.99
        /// </summary>
        /// <param name="percentile"></param>
        /// <returns></returns>
        private static double ConvertPercentileIntToFloat(int percentile)
        {
            return (percentile / 100.0d);
        }

        /// <summary>
        /// Converts a percentile value such as "0.99" into a list index in the current linked list like 198
        /// </summary>
        /// <param name="percentile"></param>
        /// <returns></returns>
        private int ConvertPercentileFloatToListIndex(double percentile)
        {
            return (int)Math.Round(percentile * (_numSamples - 1));
        }

        /// <summary>
        /// Represents a fixed cursor in the linked list that points to the value of a particular percentile.
        /// </summary>
        private class PercentileCursor
        {
            public PercentileCursor(double targetPercentile)
            {
                TargetPercentile = targetPercentile;
            }

            /// <summary>
            /// The target percentile in the form 0.5
            /// </summary>
            public readonly double TargetPercentile;

            /// <summary>
            /// The node this cursor currently points to
            /// </summary>
            public PercentileNode Node = null;

            /// <summary>
            /// The list index of the node being pointed to
            /// </summary>
            public int ListIndex = 0;
        }

        /// <summary>
        /// Tracks a single metric observation as a triply-linked list node
        /// </summary>
        private class PercentileNode
        {
            public PercentileNode(double measurement)
            {
                Measurement = measurement;
                //Id = Guid.NewGuid().ToString("N").Substring(0, 4);
            }

            /// <summary>
            /// Resets this object so it can be reused
            /// </summary>
            public void Reset()
            {
                Prev = null;
                Next = null;
                Newer = null;
            }

            /// <summary>
            /// The observation value
            /// </summary>
            public double Measurement;
            //public readonly string Id;

            /// <summary>
            /// Pointer to next node in value list
            /// </summary>
            public PercentileNode Prev;

            /// <summary>
            /// Pointer to previous node in value list
            /// </summary>
            public PercentileNode Next;

            /// <summary>
            /// Pointer to newer node in age list
            /// </summary>
            public PercentileNode Newer;
        }
    }
}
