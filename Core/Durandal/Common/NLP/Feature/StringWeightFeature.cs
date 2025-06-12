using System;

namespace Durandal.Common.NLP.Feature
{
    public class StringWeightFeature : ITrainingFeature, IComparable<StringWeightFeature>, IEquatable<StringWeightFeature>
    {
        public string Name;
        public double Weight;

        public StringWeightFeature(string name = "", double weight = 0.0)
        {
            Name = name;
            Weight = weight;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        public override bool Equals(Object obj)
        {
            if (ReferenceEquals(obj, null))
                return false;
            StringWeightFeature other = obj as StringWeightFeature;
            return Equals(other);
        }

        public bool Equals(StringWeightFeature other)
        {
            if (ReferenceEquals(other, null))
            {
                return false;
            }

            return string.Equals(Name, other.Name, StringComparison.Ordinal);
        }

        public int CompareTo(StringWeightFeature other)
        {
            if (other == null)
                return 0;
            return Math.Sign(other.Weight - this.Weight);
        }

        public bool Parse(string input)
        {
            int tabIndex = input.IndexOf('\t');
            if (tabIndex > 0)
            {
                Name = input.Substring(0, tabIndex);
                if (double.TryParse(input.Substring(tabIndex + 1), out Weight))
                {
                    return true;
                }
            }
            return false;
        }

        public override string ToString()
        {
            return Name + "\t" + Weight;
        }

        public static bool operator ==(StringWeightFeature left, StringWeightFeature right)
        {
            if (ReferenceEquals(left, null))
            {
                return ReferenceEquals(right, null);
            }

            return left.Equals(right);
        }

        public static bool operator !=(StringWeightFeature left, StringWeightFeature right)
        {
            return !(left == right);
        }

        public static bool operator <(StringWeightFeature left, StringWeightFeature right)
        {
            return ReferenceEquals(left, null) ? !ReferenceEquals(right, null) : left.CompareTo(right) < 0;
        }

        public static bool operator <=(StringWeightFeature left, StringWeightFeature right)
        {
            return ReferenceEquals(left, null) || left.CompareTo(right) <= 0;
        }

        public static bool operator >(StringWeightFeature left, StringWeightFeature right)
        {
            return !ReferenceEquals(left, null) && left.CompareTo(right) > 0;
        }

        public static bool operator >=(StringWeightFeature left, StringWeightFeature right)
        {
            return ReferenceEquals(left, null) ? ReferenceEquals(right, null) : left.CompareTo(right) >= 0;
        }
    }
}
