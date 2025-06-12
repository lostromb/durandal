using Durandal.Common.MathExt;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Time.Scheduling
{
    /// <summary>
    /// This is a static helper class which is used to calculate approximately optimal schedules for events that happen periodically but should overlap as little as possible.
    /// This is for scenarios like continouously running tests, where we want to run tests constantly but also stagger them a bit so not all of them run at the same time.
    /// </summary>
    public static class PeriodicExclusivitySolver
    {
        private static readonly IRandom _rand = new FastRandom();

        /// <summary>
        /// Accepts a list of periodic events with non-zero periods, and calculates a reasonably optimal
        /// offset for each of those events such that when run simultaneously, the event "waveforms" will
        /// have as little correlation with each other as possible. This is used in the scheduler to try
        /// and ensure more-or-less equal spacing between all executions
        /// </summary>
        /// <typeparam name="T">The type of events being solved for</typeparam>
        /// <param name="periodicEvents">The list of input events with periods (offsets don't matter on input).</param>
        /// <param name="rand">A random source to use (for deterministic unit tests).</param>
        public static void Solve<T>(IList<PeriodicEvent<T>> periodicEvents, IRandom rand = null)
        {
            if (periodicEvents == null)
            {
                throw new ArgumentException(nameof(periodicEvents));
            }

            rand = rand ?? _rand;
            int numObjects = periodicEvents.Count;

            if (numObjects <= 1)
            {
                return;
            }

            // Initialize
            long longestPeriod = 0;
            long shortestPeriod = long.MaxValue;
            foreach (PeriodicEvent<T> obj in periodicEvents)
            {
                longestPeriod = Math.Max(longestPeriod, obj.Period.Ticks);
                shortestPeriod = Math.Min(shortestPeriod, obj.Period.Ticks);
                obj.Offset = TimeSpan.Zero;
            }

            double minDeviation = double.MaxValue;
            EventOccurenceComparer<T> comparer = new EventOccurenceComparer<T>();
            List<EventOccurence<T>> events = new List<EventOccurence<T>>();
            
            for (int iteration = 0; iteration < numObjects * 50; iteration++)
            {
                // Virtually "schedule" a list of events based on the current set of periods + offsets
                events.Clear();
                foreach (PeriodicEvent<T> obj in periodicEvents)
                {
                    long time = obj.Offset.Ticks;
                    while (time <= longestPeriod * 10)
                    {
                        events.Add(new EventOccurence<T>() { Event = obj, OccurrenceTime = time });
                        time += obj.Period.Ticks;
                    }
                }

                events.Sort(comparer);
                
                // Find the point that is closest to a neighbor on its left
                PeriodicEvent<T> toChange = null;
                
                long smallestDiff = long.MaxValue;
                for (int c = 1; c < events.Count; c++)
                {
                    long diff = events[c].OccurrenceTime - events[c - 1].OccurrenceTime;
                    if (diff < smallestDiff)
                    {
                        smallestDiff = diff;
                        toChange = events[c].Event;
                    }
                }

                // Add a random delay to that event's phase (with modulo)
                long oldOffset = toChange.Offset.Ticks;
                toChange.Offset = TimeSpan.FromTicks(oldOffset + (long)(rand.NextDouble() * toChange.Period.Ticks));

                // Calculate the new schedule
                events.Clear();
                foreach (PeriodicEvent<T> obj in periodicEvents)
                {
                    long time = obj.Offset.Ticks;
                    while (time <= longestPeriod * 10)
                    {
                        events.Add(new EventOccurence<T>() { Event = obj, OccurrenceTime = time });
                        time += obj.Period.Ticks;
                    }
                }

                events.Sort(comparer);

                // calculate mean and variance of the time between each event
                // this algorithm tries to minimize the variance of the time between events,
                // in an attempt to make events clash as little as possible
                double variance = CalculateDeltaVariance(events);

                // If the deviation has not decreased, undo this change
                if (variance > minDeviation)
                {
                    toChange.Offset = TimeSpan.FromTicks(oldOffset);
                }
                else
                {
                    minDeviation = variance;
                    //System.Diagnostics.Debug.WriteLine("Changed event " + toChange.Object + " from " + TimeSpan.FromTicks(oldOffset) + " to " + toChange.Offset);
                    //System.Diagnostics.Debug.WriteLine(variance);
                }
            }
        }

        private class EventOccurence<T>
        {
            public PeriodicEvent<T> Event { get; set; }
            public long OccurrenceTime { get; set; }

            public override string ToString()
            {
                return OccurrenceTime + " " + Event.Object?.ToString();
            }
        }
        
        private class EventOccurenceComparer<T> : IComparer<EventOccurence<T>>
        {
            public int Compare(EventOccurence<T> x, EventOccurence<T> y)
            {
                return Math.Sign(x.OccurrenceTime - y.OccurrenceTime);
            }
        }

        private static double CalculateDeltaVariance<T>(List<EventOccurence<T>> events)
        {
            //double sum = 0;
            //for (int c = 0; c < events.Count - 1; c++)
            //{
            //    double diff = (events[c + 1].OccurrenceTime - events[c].OccurrenceTime) / TimeSpan.TicksPerMillisecond;
            //    sum += (diff * diff);
            //}

            //return sum / (double)events.Count;


            double mean = 0;
            double variance = 0;
            for (int c = 0; c < events.Count - 1; c++)
            {
                mean += (events[c + 1].OccurrenceTime - events[c].OccurrenceTime) / TimeSpan.TicksPerMillisecond;
            }

            mean = mean / (events.Count - 1);
            for (int c = 0; c < events.Count - 1; c++)
            {
                double diff = (events[c + 1].OccurrenceTime - events[c].OccurrenceTime) / TimeSpan.TicksPerMillisecond;
                variance += (diff - mean) * (diff - mean);
            }

            variance = variance / (events.Count - 1);
            return variance;
        }
    }
}
