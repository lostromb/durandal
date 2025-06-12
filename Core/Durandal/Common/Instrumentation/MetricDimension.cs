using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Instrumentation
{
    public class MetricDimension : IEquatable<MetricDimension>
    {
        public string Key { get; private set; }
        public string Value { get; private set; }

        public MetricDimension(string key, string value)
        {
            Key = key.AssertNonNullOrEmpty(nameof(key));
            Value = value.AssertNonNullOrEmpty(nameof(value));
        }

        public override string ToString()
        {
            return Key + "=" + Value;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            MetricDimension other = (MetricDimension)obj;
            return Equals(other);
        }

        public bool Equals(MetricDimension other)
        {
            return string.Equals(Key, other.Key, StringComparison.Ordinal) &&
                string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        // override object.GetHashCode
        public override int GetHashCode()
        {
            return Key.GetHashCode() ^ (Value.GetHashCode() << 1);
        }

        public ulong GetHashCode64()
        {
            return (ulong)Key.GetHashCode() ^ ((ulong)Value.GetHashCode() << 32);
        }
    }
}
