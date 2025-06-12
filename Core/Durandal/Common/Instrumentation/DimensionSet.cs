using Durandal.Common.Collections;
using Durandal.Common.Utils;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Instrumentation
{
    /// <summary>
    /// A strongly typed immutable set of metric dimensions.
    /// You should reuse instances of this class wherever possible for best performance (rather than just passing dimensions as raw arrays or params)
    /// </summary>
    [JsonConverter(typeof(JsonConverter_Local))]
    public class DimensionSet : IEnumerable<MetricDimension>, IEquatable<DimensionSet>
    {
        /// <summary>
        /// Static set of empty dimensions.
        /// </summary>
        public static readonly DimensionSet Empty = new DimensionSet(new MetricDimension[0]);

        private readonly MetricDimension[] _dimensions;
        private readonly ulong _cachedHashCode;
        private string _cachedString = null;

        /// <summary>
        /// Creates a new dimension set
        /// </summary>
        /// <param name="dimensions"></param>
        public DimensionSet(params MetricDimension[] dimensions)
        {
            foreach (MetricDimension d in dimensions)
            {
                if (d == null)
                {
                    throw new ArgumentNullException("One or more metric dimensions is null");
                }
            }

            // Make sure no dimensions are duplicated. It doesn't matter if both keys specify the same value
            for (int a = 0; a < dimensions.Length; a++)
            {
                for (int b = a + 1; b < dimensions.Length; b++)
                {
                    if (string.Equals(dimensions[a].Key, dimensions[b].Key, StringComparison.Ordinal))
                    {
                        throw new ArgumentException("Dimension \"" + dimensions[a].Key + "\" was specified more than once in the input set");
                    }
                }
            }

            _dimensions = dimensions;
            _cachedHashCode = 0;
            foreach (MetricDimension d in _dimensions)
            {
                // We intentionally XOR each hash code together linearly so that dimensions that are the same but
                // are in a different order will still result in the same hash code, since we only use hash code for equality comparison
                _cachedHashCode ^= d.GetHashCode64();
            }
        }

        /// <summary>
        /// Indicates whether there are actually any dimensions specified in this set.
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                return _dimensions.Length == 0;
            }
        }

        /// <summary>
        /// Returns a new DimensionSet that is a combination of this set and another set of dimensions.
        /// If any dimensions have clashing keys, this will throw an exception.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public DimensionSet Combine(DimensionSet other)
        {
            int length = _dimensions.Length + other._dimensions.Length;
            MetricDimension[] newDimensions = new MetricDimension[length];
            ArrayExtensions.MemCopy(_dimensions, 0, newDimensions, 0, _dimensions.Length);
            ArrayExtensions.MemCopy(other._dimensions, 0, newDimensions, _dimensions.Length, other._dimensions.Length);
            return new DimensionSet(newDimensions);
        }

        public bool IsSubsetOf(DimensionSet other)
        {
            foreach (MetricDimension thisDim in _dimensions)
            {
                bool foundMatch = false;
                foreach (MetricDimension otherDim in other._dimensions)
                {
                    if (thisDim.Equals(otherDim))
                    {
                        foundMatch = true;
                        break;
                    }
                }

                if (!foundMatch)
                {
                    return false;
                }    
            }

            return true;
        }

        /// <summary>
        /// Returns a new DimensionSet that is a combination of this set and another single dimension.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public DimensionSet Combine(MetricDimension other)
        {
            int newLength = _dimensions.Length + 1;
            MetricDimension[] newDimensions = new MetricDimension[newLength];
            if (_dimensions.Length > 0)
            {
                ArrayExtensions.MemCopy(_dimensions, 0, newDimensions, 0, _dimensions.Length);
            }

            newDimensions[newLength - 1] = other;
            return new DimensionSet(newDimensions);
        }

        public override string ToString()
        {
            if (_cachedString == null)
            {
                using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
                {
                    StringBuilder cachedStringBuilder = pooledSb.Builder;
                    ToString(cachedStringBuilder);
                    _cachedString = cachedStringBuilder.ToString();
                }
            }

            return _cachedString;
        }

        public void ToString(StringBuilder existingBuilder)
        {
            bool first = true;
            foreach (MetricDimension dim in _dimensions)
            {
                if (!first)
                {
                    existingBuilder.Append('|');
                }

                existingBuilder.Append(dim.Key);
                existingBuilder.Append('=');
                existingBuilder.Append(dim.Value);
                first = false;
            }
        }

        public override int GetHashCode()
        {
            return (int)(_cachedHashCode >> 16);
        }

        public ulong GetHashCode64()
        {
            return _cachedHashCode;
        }

        public override bool Equals(object obj)
        {
            if (obj == null ||
                (!(obj.GetType() == typeof(DimensionSet))))
            {
                return false;
            }

            DimensionSet other = (DimensionSet)obj;
            return Equals(other);
        }

        public bool Equals(DimensionSet other)
        {
            // We don't want to do a full unordered set comparison for each Equals() operation between dimension sets,
            // so equality is based purely on hash code. This has a risk of collision, so we mitigate that
            // problem a bit (but not entirely) by using a 64-bit hash instead of the usual 32-bit.
            return GetHashCode64() == other.GetHashCode64();
        }

        public IEnumerator<MetricDimension> GetEnumerator()
        {
            return ((IEnumerable<MetricDimension>)_dimensions).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<MetricDimension>)_dimensions).GetEnumerator();
        }

        public string Serialize()
        {
            return ToString();
        }
        
        /// <summary>
        /// Parses a dimension set from the string generated by the <see cref="Serialize" /> method.
        /// </summary>
        /// <param name="serializedForm"></param>
        /// <returns></returns>
        public static DimensionSet Parse(string serializedForm)
        {
            if (string.IsNullOrWhiteSpace(serializedForm))
            {
                return DimensionSet.Empty;
            }

            int delimiterIdx = serializedForm.IndexOf('|');
            int numDimensions = 1;
            while (delimiterIdx >= 0 &&
                delimiterIdx < serializedForm.Length - 1)
            {
                numDimensions++;
                delimiterIdx = serializedForm.IndexOf('|', delimiterIdx + 1);
            }

            MetricDimension[] returnVal = new MetricDimension[numDimensions];
            int keyStartIdx, equalsIdx;
            delimiterIdx = -1;
            for (int dim = 0; dim < numDimensions; dim++)
            {
                keyStartIdx = delimiterIdx + 1;
                delimiterIdx = serializedForm.IndexOf('|', keyStartIdx);
                if (delimiterIdx < 0)
                    delimiterIdx = serializedForm.Length;
                equalsIdx = serializedForm.IndexOf('=', keyStartIdx, delimiterIdx - keyStartIdx);

                string key = serializedForm.Substring(keyStartIdx, equalsIdx - keyStartIdx);
                string value = serializedForm.Substring(equalsIdx + 1, delimiterIdx - equalsIdx - 1);
                returnVal[dim] = new MetricDimension(key, value);
            }

            return new DimensionSet(returnVal);
        }

        private class JsonConverter_Local : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return typeof(DimensionSet) == objectType;
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null)
                {
                    return null;
                }
                else if (reader.TokenType == JsonToken.String)
                {
                    string nextString = reader.Value as string;
                    return DimensionSet.Parse(nextString);
                }
                else
                {
                    throw new JsonSerializationException("Could not parse DimensionSet from json");
                }
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                if (value == null)
                {
                    writer.WriteNull();
                }
                else
                {
                    DimensionSet castObject = (DimensionSet)value;
                    if (castObject == null)
                    {
                        writer.WriteNull();
                    }
                    else
                    {
                        writer.WriteValue(castObject.Serialize());
                    }
                }
            }
        }
    }
}