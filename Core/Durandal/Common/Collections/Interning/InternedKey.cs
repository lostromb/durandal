using Durandal.Common.Utils;
using System;
using System.Globalization;

namespace Durandal.Common.Collections.Interning
{
    /// <summary>
    /// Represents a unique 32-bit value which comes as a result of interning
    /// an object of the given type from an <see cref="IPrimitiveInternalizer{T}"/>
    /// Interned keys must be non-negative integers. Negative integers are reserved internally
    /// and cannot be used.
    /// </summary>
    /// <typeparam name="T">The type of value that is being interned.</typeparam>
    public struct InternedKey<T> : IEquatable<InternedKey<T>>
    {
        public int Key;

        public InternedKey(int val)
        {
#if DEBUG
            Key = val.AssertNonNegative(nameof(val));
#else
            Key = val;
#endif
        }

        public override string ToString()
        {
            return Key.ToString(CultureInfo.InvariantCulture);
        }

        public override bool Equals(object obj)
        {
            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            InternedKey<T> o = (InternedKey<T>)obj;
            return Equals(o);
        }

        public bool Equals(InternedKey<T> other)
        {
            return Key == other.Key;
        }

        public override int GetHashCode()
        {
            return Key.GetHashCode();
        }

        public static bool operator ==(InternedKey<T> current, InternedKey<T> other)
        {
            return current.Key == other.Key;
        }

        public static bool operator !=(InternedKey<T> current, InternedKey<T> other)
        {
            return current.Key != other.Key;
        }
    }
}
