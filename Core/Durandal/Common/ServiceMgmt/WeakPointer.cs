using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.ServiceMgmt
{
    /// <summary>
    /// <para>The purpose of this class is to define explicit semantics regarding ownership of <see cref="IDisposable"/> objects.
    /// It is NOT the same as the core <see cref="WeakReference"/> type.</para>
    /// <para>Typically, when you pass an IDisposable object as a parameter to a method, the caller is assumed to manage ownership of
    /// that object, meaning the callee should never Dispose() that object. By convention, the same should be true for constructor parameters.
    /// If an IDisposable is passed as a parameter to another object's constructor, that object may hold a reference to the disposable, but should
    /// not be responsible for disposing of that object. However, if you do this, the default code analyzer will complain that you should dispose
    /// of disposable member variables in all cases. By changing that member variable to a WeakPointer, you can codify the intended semantics and
    /// avoid the code analysis warning.</para>
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IDisposable"/> that this is a pointer to.</typeparam>
    public struct WeakPointer<T> : IEquatable<WeakPointer<T>> where T : class, IDisposable
    {
        /// <summary>
        /// The static pointer value referencing null.
        /// </summary>
        public static readonly WeakPointer<T> Null = new WeakPointer<T>(null);

        /// <summary>
        /// The value being pointed to.
        /// </summary>
        public T Value { get; }

        /// <summary>
        /// Constructs a new pointer that refers to a specific <see cref="IDisposable"/> object.
        /// The object may be null.
        /// </summary>
        /// <param name="value">The <see cref="IDisposable"/> object to point to.</param>
        public WeakPointer(T value)
        {
            Value = value;
        }

        /// <summary>
        /// Indicates whether this pointer refers to a null value.
        /// </summary>
        public bool IsNull => Value is null;

        /// <summary>
        /// If this <see cref="WeakPointer{T}"/> refers to a non-null object, return this.
        /// Otherwise, return a new WeakPointer referring to the output of the value producer.
        /// This behaves like a null-coalescing operator "??" (since that can't be overridden).
        /// </summary>
        /// <param name="otherValue">An alternative value to use.</param>
        /// <returns>A new pointer to either this non-null value, or the default value.</returns>
        public WeakPointer<T> DefaultIfNull(T otherValue)
        {
            return Value == null ? new WeakPointer<T>(otherValue) : this;
        }

        /// <summary>
        /// If this <see cref="WeakPointer{T}"/> refers to a non-null object, return this.
        /// Otherwise, return a new WeakPointer referring to the output of the value producer.
        /// This behaves like a null-coalescing operator "??" (since that can't be overridden).
        /// </summary>
        /// <param name="otherValue">A lambda function which can produce an alternative value.</param>
        /// <returns>A new pointer to either this non-null value, or the default value.</returns>
        public WeakPointer<T> DefaultIfNull(Func<T> otherValue)
        {
            return Value == null ? new WeakPointer<T>(otherValue()) : this;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is WeakPointer<T>))
            {
                return false;
            }

            return EqualityComparer<T>.Default.Equals(Value, ((WeakPointer<T>)obj).Value);
        }

        public override int GetHashCode()
        {
            if (Value == null)
            {
                return 0;
            }

            return Value.GetHashCode();
        }

        public static bool operator ==(WeakPointer<T> left, WeakPointer<T> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WeakPointer<T> left, WeakPointer<T> right)
        {
            return !(left == right);
        }

        public bool Equals(WeakPointer<T> other)
        {
            if (ReferenceEquals(Value, null))
            {
                return ReferenceEquals(other.Value, null);
            }

            return Value.Equals(other.Value);
        }
    }

    public static class WeakPointerExtensions
    {
        public static WeakPointer<T> AsWeakPointer<T>(this T v) where T : class, IDisposable
        {
            return new WeakPointer<T>(v);
        }
    }
}
