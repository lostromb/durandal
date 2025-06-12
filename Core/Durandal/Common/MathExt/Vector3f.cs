using System;
using System.Diagnostics;
using System.Numerics;

namespace Durandal.Common.MathExt
{
    /// <summary>
    /// Represents a vector in 3 dimensions with 32-bit precision.
    /// The values themselves are immutable; each operation produces a clone when necessary
    /// </summary>
    [DebuggerDisplay("[ {X}, {Y}, {Z} ]")]
    public struct Vector3f : System.IEquatable<Vector3f>
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        private static readonly Vector3f ZERO_VECTOR = new Vector3f(0, 0, 0);

        public Vector3f(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// Returns the singleton zero vector.
        /// </summary>
        public static Vector3f Zero => ZERO_VECTOR;

        public float Distance(Vector3f other)
        {
            float dx = other.X - X;
            float dy = other.Y - Y;
            float dz = other.Z - Z;
            return (float)System.Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
        }

        public float DotProduct(Vector3f other)
        {
            return (other.X * X) + (other.Y * Y) + (other.Z * Z);
        }

        public Vector3f Add(Vector3f other)
        {
            return Add(other.X, other.Y, other.Z);
        }

        public Vector3f Add(float x, float y, float z)
        {
            return new Vector3f(X + x, Y + y, Z + z);
        }

        public Vector3f Subtract(Vector3f other)
        {
            return Subtract(other.X, other.Y, other.Z);
        }

        public Vector3f Subtract(float x, float y, float z)
        {
            return new Vector3f(X - x, Y - y, Z - z);
        }

        public Vector3f Multiply(Vector3f other)
        {
            return Multiply(other.X, other.Y, other.Z);
        }

        public Vector3f Multiply(float x, float y, float z)
        {
            return new Vector3f(X * x, Y * y, Z * z);
        }

        public Vector3f Multiply(float w)
        {
            return new Vector3f(X * w, Y * w, Z * w);
        }

        /// <summary>
        /// Calculates the angle between this vector and another, measured in radians
        /// </summary>
        /// <param name="other">The other vector to compare.</param>
        /// <returns>The angle in radians</returns>
        public float AngleBetween(Vector3f other)
        {
            float dot = Normalized().DotProduct(other.Normalized());
            if (dot >= 1.0f)
            {
                return 0;
            }
            else if (dot <= -1.0f)
            {
                return (float)Math.PI;
            }
            else
            {
                return (float)Math.Acos(dot);
            }
        }

        /// <summary>
        /// Assumes both vectors are unit vectors already as an optimization.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public float AngleBetweenUnitVectors(Vector3f other)
        {
            float dot = DotProduct(other);
            if (dot >= 1.0f)
            {
                return 0;
            }
            else if (dot <= -1.0f)
            {
                return (float)Math.PI;
            }
            else
            {
                return (float)Math.Acos(dot);
            }
        }

        /// <summary>
        /// Gets the magnitude, or the Euclidean length of this vector.
        /// </summary>
        public float Magnitude
        {
            get
            {
                return (float)System.Math.Sqrt((X * X) + (Y * Y) + (Z * Z));
            }
        }

        /// <summary>
        /// Gets the squared magnitude of this vector.
        /// </summary>
        public float SquaredMagnitude
        {
            get
            {
                return (X * X) + (Y * Y) + (Z * Z);
            }
        }

        /// <summary>
        /// Returns a copy of this vector with its magnitude normalized to the given value.
        /// </summary>
        /// <param name="length">The magnitude of the vector to return.</param>
        /// <returns></returns>
        /// <exception cref="DivideByZeroException">Thrown if this vector is zero.</exception>
        public Vector3f OfLength(float length)
        {
            float mag = Magnitude;
            if (mag == 0)
            {
                throw new DivideByZeroException("Cannot extend a zero-length vector");
            }
            else if (mag == 1.0f)
            {
                return this; // Save some time by returning this if the transformation would be a no-op;
            }

            float factor = length / mag;
            return new Vector3f(X * factor, Y * factor, Z * factor);
        }

        /// <summary>
        /// Returns a copy of this vector with its magnitude normalized to 1.0.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="DivideByZeroException">Thrown if this vector is zero.</exception>
        public Vector3f Normalized()
        {
            return OfLength(1);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Vector3f))
            {
                return false;
            }

            Vector3f other = (Vector3f)obj;
            return Equals(other);
        }

        public override int GetHashCode()
        {
            return
                (X.GetHashCode() * 3) ^
                (Y.GetHashCode() * 211) ^
                (Z.GetHashCode() * 5027);
        }

        public static bool operator ==(Vector3f left, Vector3f right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Vector3f left, Vector3f right)
        {
            return !(left == right);
        }

        public static Vector3f operator +(Vector3f left, Vector3f right)
        {
            return new Vector3f(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
        }

        public static Vector3f operator -(Vector3f left, Vector3f right)
        {
            return new Vector3f(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
        }

        public static Vector3f operator *(Vector3f left, Vector3f right)
        {
            return new Vector3f(left.X * right.X, left.Y * right.Y, left.Z * right.Z);
        }

        public static Vector3f operator /(Vector3f left, Vector3f right)
        {
            return new Vector3f(left.X / right.X, left.Y / right.Y, left.Z / right.Z);
        }

        public static Vector3f operator *(Vector3f vector, float constant)
        {
            return new Vector3f(vector.X * constant, vector.Y * constant, vector.Z * constant);
        }

        public static Vector3f operator /(Vector3f vector, float constant)
        {
            return new Vector3f(vector.X / constant, vector.Y / constant, vector.Z / constant);
        }

        public bool Equals(Vector3f other)
        {
            return X == other.X &&
                Y == other.Y &&
                Z == other.Z;
        }

        public override string ToString()
        {
            return $"[ {X}, {Y}, {Z} ]";
        }
    }
}
