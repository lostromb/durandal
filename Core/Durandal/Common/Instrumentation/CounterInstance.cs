using Durandal.Common.Utils;
using System;
using System.Text;

namespace Durandal.Common.Instrumentation
{
    /// <summary>
    /// Think of this as a strong name key for a unique counter name + set of dimensions.
    /// </summary>
    public struct CounterInstance : IComparable<CounterInstance>, IEquatable<CounterInstance>
    {
        /// <summary>
        /// The base name of this counter (e.g. "Requests / sec")
        /// </summary>
        public string CounterName { get; private set; }

        /// <summary>
        /// The dimensions applied to this counter, such as instance name, machine name, environment, etc.
        /// </summary>
        public DimensionSet Dimensions { get; private set; }

        /// <summary>
        /// The type of counter that this is
        /// </summary>
        public CounterType Type { get; private set; }

        private readonly int _cachedHashCode;
        private string _cachedString;

        public CounterInstance(string counterName, DimensionSet dimensions, CounterType type)
        {
            CounterName = counterName;
            Dimensions = dimensions;
            Type = type;

            // Precalculate hash code and string representation
            _cachedHashCode = 
                CounterName.GetHashCode() ^ 
                (Dimensions.GetHashCode() << 2) ^ 
                (Type.GetHashCode() << 4);
            _cachedString = null;
        }

        public override int GetHashCode()
        {
            return _cachedHashCode;
        }

        public override string ToString()
        {
            if (_cachedString == null)
            {
                if (Dimensions.IsEmpty)
                {
                    _cachedString = CounterName;
                }
                else
                {
                    // this can run multiple times before the cache actually becomes consistent
                    // a little inefficient but there's no locking
                    using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
                    {
                        StringBuilder returnVal = pooledSb.Builder;
                        returnVal.Append(CounterName);
                        returnVal.Append(" (");
                        Dimensions.ToString(returnVal);
                        returnVal.Append(")");
                        _cachedString = returnVal.ToString();
                    }
                }
            }

            return _cachedString;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(obj, null) ||
                (!(obj.GetType() == typeof(CounterInstance))))
            {
                return false;
            }

            CounterInstance other = (CounterInstance)obj;
            return Equals(other);
        }

        public bool Equals(CounterInstance other)
        {
            return string.Equals(CounterName, other.CounterName, System.StringComparison.Ordinal) &&
                Dimensions.Equals(other.Dimensions) &&
                Type == other.Type;
        }

        public int CompareTo(CounterInstance other)
        {
            int returnVal = CounterName.CompareTo(other.CounterName);
            if (returnVal == 0)
            {
                returnVal = Dimensions.GetHashCode().CompareTo(other.Dimensions.GetHashCode());

                if (returnVal == 0)
                {
                    returnVal = Type.CompareTo(other.Type);
                }
            }

            return returnVal;
        }

        public static bool operator ==(CounterInstance left, CounterInstance right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CounterInstance left, CounterInstance right)
        {
            return !(left == right);
        }

        public static bool operator <(CounterInstance left, CounterInstance right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(CounterInstance left, CounterInstance right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >(CounterInstance left, CounterInstance right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(CounterInstance left, CounterInstance right)
        {
            return left.CompareTo(right) >= 0;
        }
    }
}