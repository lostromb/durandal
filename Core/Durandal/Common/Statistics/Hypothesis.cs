using System;
using System.Collections.Generic;

namespace Durandal.Common.Statistics
{
    /// <summary>
    /// Pairs an arbitrary item with a confidence value
    /// </summary>
    /// <typeparam name="T">The type to be paired with confidence</typeparam>
    public struct Hypothesis<T> : IEquatable<Hypothesis<T>>
    {
        /// <summary>
        /// The object being hypothesized
        /// </summary>
        public T Value { get; set; }

        /// <summary>
        /// The confidence of the hypothesis, from 0 to 1.
        /// </summary>
        public float Conf { get; set; }

        public Hypothesis(T obj, float conf)
        {
            Value = obj;
            Conf = conf;
        }

        public override string ToString()
        {
            return string.Format("Hyp: conf={0} {1}", Conf, Value.ToString());
        }

        /// <summary>
        /// A comparer that will sort hypotheses in ascending order, from lowest confidence to highest
        /// </summary>
        public class AscendingComparator : IComparer<Hypothesis<T>>
        {
            public int Compare(Hypothesis<T> x, Hypothesis<T> y)
            {
                return Math.Sign(x.Conf - y.Conf);
            }
        }

        /// <summary>
        /// A comparer that will sort hypotheses in descending order, from highest confidence to lowest
        /// </summary>
        public class DescendingComparator : IComparer<Hypothesis<T>>
        {
            public int Compare(Hypothesis<T> x, Hypothesis<T> y)
            {
                return Math.Sign(y.Conf - x.Conf);
            }
        }

        public override bool Equals(object obj)
        {
            if (obj.GetType() == this.GetType())
            {
                return false;
            }

            Hypothesis<T> other = (Hypothesis<T>)obj;
            return (ReferenceEquals(Value, null) && ReferenceEquals(other.Value, null)) ||
                (Value.Equals(other.Value));
        }

        public bool Equals(Hypothesis<T> other)
        {
            return (ReferenceEquals(Value, null) && ReferenceEquals(other.Value, null)) ||
                (Value.Equals(other.Value));
        }

        public override int GetHashCode()
        {
            if (Value == null)
            {
                return 0;
            }

            return Value.GetHashCode();
        }

        public static bool operator ==(Hypothesis<T> left, Hypothesis<T> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Hypothesis<T> left, Hypothesis<T> right)
        {
            return !(left == right);
        }
    }
}
