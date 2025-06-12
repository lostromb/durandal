using System.Collections.Generic;
using Durandal.Common.MathExt;
using Durandal.Common.Statistics;

namespace Durandal.Common.Utils
{
    /// <summary>
    /// Like a counter, but one that tracks inputs AND outputs
    /// </summary>
    /// <typeparam name="A"></typeparam>
    public class StatisticalMapping<A>
    {
        private readonly Dictionary<A, Counter<A>> _counters = new Dictionary<A, Counter<A>>();
        private readonly IComparer<Hypothesis<A>> _sorter = new Hypothesis<A>.DescendingComparator();

        public void Increment(A input, A output)
        {
            Increment(input, output, 1);
        }

        public void Increment(A input, A output, float weight)
        {
            if (!_counters.ContainsKey(input))
            {
                _counters[input] = new Counter<A>();
            }

            _counters[input].Increment(output, weight);
        }

        public Dictionary<A, Counter<A>>.KeyCollection GetItems()
        {
            return _counters.Keys;
        }

        public A GetMostLikelyOutputFor(A input)
        {
            List<Hypothesis<A>> counts = GetCounts(input);
            if (counts.Count == 0)
            {
                return default(A);
            }

            counts.Sort(_sorter);
            return counts[0].Value;
        }

        public List<Hypothesis<A>> GetCounts(A input)
        {
            List<Hypothesis<A>> returnVal = new List<Hypothesis<A>>();
            if (_counters.ContainsKey(input))
            {
                foreach (KeyValuePair<A, float> kvp in _counters[input])
                {
                    returnVal.Add(new Hypothesis<A>(kvp.Key, kvp.Value));
                }
            }

            return returnVal;
        }

        public List<Hypothesis<A>> GetCountsSortedDescending(A input)
        {
            List<Hypothesis<A>> returnVal = GetCounts(input);
            returnVal.Sort(_sorter);
            return returnVal;
        }
        
        public void NormalizeOutputs()
        {
            foreach (var counter in _counters.Values)
            {
                counter.Normalize();
            }
        }
    }
}
