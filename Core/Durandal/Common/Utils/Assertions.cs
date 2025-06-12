using Durandal.Common.ServiceMgmt;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Utils
{
    public static class Assertions
    {
        /// <summary>
        /// Asserts that an object reference is non-null, throwing an <see cref="ArgumentNullException"/> if it is.
        /// </summary>
        /// <typeparam name="T">The type of object to check</typeparam>
        /// <param name="reference">The reference to check.</param>
        /// <param name="objectName">The name of the argument being validated (will be included in the thrown exception)</param>
        /// <returns>The validated object.</returns>
#if NET5_0_OR_GREATER
        public static T AssertNonNull<T>([System.Diagnostics.CodeAnalysis.NotNull] this T reference, string objectName)
#else
        public static T AssertNonNull<T>(this T reference, string objectName)
#endif
        {
            if (reference == null)
            {
                throw new ArgumentNullException(objectName);
            }

            return reference;
        }

        /// <summary>
        /// Asserts that a string reference is non-null or empty, throwing an <see cref="ArgumentNullException"/> if it is.
        /// </summary>
        /// <param name="reference">The reference to check.</param>
        /// <param name="objectName">The name of the argument being validated (will be included in the thrown exception)</param>
        /// <returns>The validated object.</returns>
        public static string AssertNonNullOrEmpty(this string reference, string objectName)
        {
            if (string.IsNullOrEmpty(reference))
            {
                throw new ArgumentNullException(objectName);
            }

            return reference;
        }

        /// <summary>
        /// Asserts that a weak pointer and its target are both non-null, throwing an <see cref="ArgumentNullException"/> if it is.
        /// </summary>
        /// <typeparam name="T">The type of object to check</typeparam>
        /// <param name="reference">The weak pointer to check.</param>
        /// <param name="objectName">The name of the argument being validated (will be included in the thrown exception)</param>
        /// <returns>The validated pointer.</returns>
        public static WeakPointer<T> AssertNonNull<T>(this WeakPointer<T> reference, string objectName) where T : class, IDisposable
        {
            if (reference.Value == null)
            {
                throw new ArgumentNullException(objectName);
            }

            return reference;
        }

        /// <summary>
        /// Asserts that a numerical value is not less than or equal to zero <see cref="ArgumentOutOfRangeException"/> if it is.
        /// </summary>
        /// <param name="reference">The reference to check.</param>
        /// <param name="objectName">The name of the argument being validated (will be included in the thrown exception)</param>
        /// <returns>The validated object.</returns>
        public static int AssertPositive(this int reference, string objectName)
        {
            if (reference <= 0)
            {
                throw new ArgumentOutOfRangeException(objectName, $"{objectName} must be a positive number");
            }

            return reference;
        }

        /// <summary>
        /// Asserts that a numerical value is not less than or equal to zero <see cref="ArgumentOutOfRangeException"/> if it is.
        /// </summary>
        /// <param name="reference">The reference to check.</param>
        /// <param name="objectName">The name of the argument being validated (will be included in the thrown exception)</param>
        /// <returns>The validated object.</returns>
        public static long AssertPositive(this long reference, string objectName)
        {
            if (reference <= 0)
            {
                throw new ArgumentOutOfRangeException(objectName, $"{objectName} must be a positive number");
            }

            return reference;
        }

        /// <summary>
        /// Asserts that a numerical value is not less than or equal to zero <see cref="ArgumentOutOfRangeException"/> if it is.
        /// </summary>
        /// <param name="reference">The reference to check.</param>
        /// <param name="objectName">The name of the argument being validated (will be included in the thrown exception)</param>
        /// <returns>The validated object.</returns>
        public static float AssertPositive(this float reference, string objectName)
        {
            if (reference <= 0.0f)
            {
                throw new ArgumentOutOfRangeException(objectName, $"{objectName} must be a positive number");
            }

            return reference;
        }

        /// <summary>
        /// Asserts that a numerical value is not less than or equal to zero <see cref="ArgumentOutOfRangeException"/> if it is.
        /// </summary>
        /// <param name="reference">The reference to check.</param>
        /// <param name="objectName">The name of the argument being validated (will be included in the thrown exception)</param>
        /// <returns>The validated object.</returns>
        public static double AssertPositive(this double reference, string objectName)
        {
            if (reference <= 0.0)
            {
                throw new ArgumentOutOfRangeException(objectName, $"{objectName} must be a positive number");
            }

            return reference;
        }

        /// <summary>
        /// Asserts that a numerical value is not less than zero <see cref="ArgumentOutOfRangeException"/> if it is.
        /// </summary>
        /// <param name="reference">The reference to check.</param>
        /// <param name="objectName">The name of the argument being validated (will be included in the thrown exception)</param>
        /// <returns>The validated object.</returns>
        public static int AssertNonNegative(this int reference, string objectName)
        {
            if (reference < 0)
            {
                throw new ArgumentOutOfRangeException(objectName, $"{objectName} cannot be a negative number");
            }

            return reference;
        }

        /// <summary>
        /// Asserts that a numerical value is not less than zero <see cref="ArgumentOutOfRangeException"/> if it is.
        /// </summary>
        /// <param name="reference">The reference to check.</param>
        /// <param name="objectName">The name of the argument being validated (will be included in the thrown exception)</param>
        /// <returns>The validated object.</returns>
        public static long AssertNonNegative(this long reference, string objectName)
        {
            if (reference < 0)
            {
                throw new ArgumentOutOfRangeException(objectName, $"{objectName} cannot be a negative number");
            }

            return reference;
        }

        /// <summary>
        /// Asserts that a numerical value is not less than zero <see cref="ArgumentOutOfRangeException"/> if it is.
        /// </summary>
        /// <param name="reference">The reference to check.</param>
        /// <param name="objectName">The name of the argument being validated (will be included in the thrown exception)</param>
        /// <returns>The validated object.</returns>
        public static float AssertNonNegative(this float reference, string objectName)
        {
            if (reference < 0.0f)
            {
                throw new ArgumentOutOfRangeException(objectName, $"{objectName} cannot be a negative number");
            }

            return reference;
        }

        /// <summary>
        /// Asserts that a numerical value is not less than zero <see cref="ArgumentOutOfRangeException"/> if it is.
        /// </summary>
        /// <param name="reference">The reference to check.</param>
        /// <param name="objectName">The name of the argument being validated (will be included in the thrown exception)</param>
        /// <returns>The validated object.</returns>
        public static double AssertNonNegative(this double reference, string objectName)
        {
            if (reference < 0.0)
            {
                throw new ArgumentOutOfRangeException(objectName, $"{objectName} cannot be a negative number");
            }

            return reference;
        }
    }
}
