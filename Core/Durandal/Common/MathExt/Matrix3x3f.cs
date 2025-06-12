using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Durandal.Common.MathExt
{
    /// <summary>
    /// Represents an immutable 3x3 matrix with 32-bit precision
    /// </summary>
    public struct Matrix3x3f : System.IEquatable<Matrix3x3f>
    {
        /// <summary>Value at row 1 column 1</summary>
        public readonly float R1_C1;
        /// <summary>Value at row 1 column 2</summary>
        public readonly float R1_C2;
        /// <summary>Value at row 1 column 3</summary>
        public readonly float R1_C3;
        /// <summary>Value at row 2 column 1</summary>
        public readonly float R2_C1;
        /// <summary>Value at row 2 column 2</summary>
        public readonly float R2_C2;
        /// <summary>Value at row 2 column 3</summary>
        public readonly float R2_C3;
        /// <summary>Value at row 3 column 1</summary>
        public readonly float R3_C1;
        /// <summary>Value at row 3 column 2</summary>
        public readonly float R3_C2;
        /// <summary>Value at row 3 column 3</summary>
        public readonly float R3_C3;

        /// <summary>The vector of 3 values in row 1</summary>
        public Vector3f Row1 => new Vector3f(R1_C1, R1_C2, R1_C3);
        /// <summary>The vector of 3 values in row 2</summary>
        public Vector3f Row2 => new Vector3f(R2_C1, R2_C2, R2_C3);
        /// <summary>The vector of 3 values in row 3</summary>
        public Vector3f Row3 => new Vector3f(R3_C1, R3_C2, R3_C3);

        /// <summary>The vector of 3 values in column 1</summary>
        public Vector3f Column1 => new Vector3f(R1_C1, R2_C1, R3_C1);
        /// <summary>The vector of 3 values in column 2</summary>
        public Vector3f Column2 => new Vector3f(R1_C2, R2_C2, R3_C2);
        /// <summary>The vector of 3 values in column 3</summary>
        public Vector3f Column3 => new Vector3f(R1_C3, R2_C3, R3_C3);

        /// <summary>
        /// Retrieves the singleton identity matrix.
        /// </summary>
        public static Matrix3x3f Identity => IDENTITY;

        private static readonly Matrix3x3f IDENTITY = new Matrix3x3f(
            1, 0, 0,
            0, 1, 0,
            0, 0, 1);

        /// <summary>
        /// Construcs a new matrix from a set of 9 values.
        /// </summary>
        /// <param name="r1c1"></param>
        /// <param name="r1c2"></param>
        /// <param name="r1c3"></param>
        /// <param name="r2c1"></param>
        /// <param name="r2c2"></param>
        /// <param name="r2c3"></param>
        /// <param name="r3c1"></param>
        /// <param name="r3c2"></param>
        /// <param name="r3c3"></param>
        public Matrix3x3f(float r1c1, float r1c2, float r1c3, float r2c1, float r2c2, float r2c3, float r3c1, float r3c2, float r3c3)
        {
            R1_C1 = r1c1;
            R1_C2 = r1c2;
            R1_C3 = r1c3;
            R2_C1 = r2c1;
            R2_C2 = r2c2;
            R2_C3 = r2c3;
            R3_C1 = r3c1;
            R3_C2 = r3c2;
            R3_C3 = r3c3;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (!(obj is Matrix3x3f))
            {
                return false;
            }

            Matrix3x3f other = (Matrix3x3f)obj;
            return Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return
                (R1_C1.GetHashCode() * 1559) ^
                (R1_C2.GetHashCode() * 1907) ^
                (R1_C3.GetHashCode() * 3) ^
                (R2_C1.GetHashCode() * 3907) ^
                (R2_C2.GetHashCode() * 7883) ^
                (R2_C3.GetHashCode() * 5839) ^
                (R3_C1.GetHashCode() * 983) ^
                (R3_C2.GetHashCode() * 109) ^
                (R3_C3.GetHashCode() * 41);
        }

        /// <inheritdoc />
        public static bool operator ==(Matrix3x3f left, Matrix3x3f right)
        {
            return left.Equals(right);
        }

        /// <inheritdoc />
        public static bool operator !=(Matrix3x3f left, Matrix3x3f right)
        {
            return !(left == right);
        }

        /// <summary>
        /// Multiplies this matrix by a scalar value.
        /// </summary>
        /// <param name="matrix">The matrix on the left hand side</param>
        /// <param name="scalar">The scalar on the right hand side</param>
        /// <returns>The scaled matrix</returns>
        public static Matrix3x3f operator *(Matrix3x3f matrix, float scalar)
        {
            return new Matrix3x3f(
                matrix.R1_C1 * scalar,
                matrix.R1_C2 * scalar,
                matrix.R1_C3 * scalar,
                matrix.R2_C1 * scalar,
                matrix.R2_C2 * scalar,
                matrix.R2_C3 * scalar,
                matrix.R3_C1 * scalar,
                matrix.R3_C2 * scalar,
                matrix.R3_C3 * scalar
                );
        }

        /// <summary>
        /// Multiplies this matrix by another matrix.
        /// </summary>
        /// <param name="matA">The matrix on the left hand side</param>
        /// <param name="matB">The matrix on the right hand side</param>
        /// <returns>The multiplied matrix</returns>
        public static Matrix3x3f operator *(Matrix3x3f matA, Matrix3x3f matB)
        {
            // Conceptually:
            //return new Matrix3x3f(
            //   matA.Row1.DotProduct(matB.Column1), matA.Row1.DotProduct(matB.Column2), matA.Row1.DotProduct(matB.Column3),
            //   matA.Row2.DotProduct(matB.Column1), matA.Row2.DotProduct(matB.Column2), matA.Row2.DotProduct(matB.Column3),
            //   matA.Row3.DotProduct(matB.Column1), matA.Row3.DotProduct(matB.Column2), matA.Row3.DotProduct(matB.Column3)
            //   );

            // slightly faster if we do the raw numbers
            return new Matrix3x3f(
                matA.R1_C1 * matB.R1_C1 + matA.R1_C2 * matB.R2_C1 + matA.R1_C3 * matB.R3_C1, // R1A * C1B
                matA.R1_C1 * matB.R1_C2 + matA.R1_C2 * matB.R2_C2 + matA.R1_C3 * matB.R3_C2, // R1A * C2B
                matA.R1_C1 * matB.R1_C3 + matA.R1_C2 * matB.R2_C3 + matA.R1_C3 * matB.R3_C3, // R1A * C3B

                matA.R2_C1 * matB.R1_C1 + matA.R2_C2 * matB.R2_C1 + matA.R2_C3 * matB.R3_C1, // R2A * C1B
                matA.R2_C1 * matB.R1_C2 + matA.R2_C2 * matB.R2_C2 + matA.R2_C3 * matB.R3_C2, // R2A * C2B
                matA.R2_C1 * matB.R1_C3 + matA.R2_C2 * matB.R2_C3 + matA.R2_C3 * matB.R3_C3, // R2A * C3B

                matA.R3_C1 * matB.R1_C1 + matA.R3_C2 * matB.R2_C1 + matA.R3_C3 * matB.R3_C1, // R3A * C1B
                matA.R3_C1 * matB.R1_C2 + matA.R3_C2 * matB.R2_C2 + matA.R3_C3 * matB.R3_C2, // R3A * C2B
                matA.R3_C1 * matB.R1_C3 + matA.R3_C2 * matB.R2_C3 + matA.R3_C3 * matB.R3_C3  // R3A * C3B
                );
        }

        /// <summary>
        /// Multiplies this matrix by a vector value where the vector is on the left hand side.
        /// </summary>
        /// <param name="vector">The vector on the left hand side</param>
        /// <param name="matrix">The matrix on the right hand side</param>
        /// <returns>The multiplied vector</returns>
        public static Vector3f operator *(Vector3f vector, Matrix3x3f matrix)
        {
            return new Vector3f(
                vector.X * matrix.R1_C1 + vector.Y * matrix.R2_C1 + vector.Z * matrix.R3_C1,
                vector.X * matrix.R1_C2 + vector.Y * matrix.R2_C2 + vector.Z * matrix.R3_C2,
                vector.X * matrix.R1_C3 + vector.Y * matrix.R2_C3 + vector.Z * matrix.R3_C3);
        }

        /// <summary>
        /// Multiplies this matrix by a vector value where the vector is on the right hand side.
        /// </summary>
        /// <param name="matrix">The matrix on the left hand side</param>
        /// <param name="vector">The vector on the right hand side</param>
        /// <returns>The multiplied vector</returns>
        public static Vector3f operator *(Matrix3x3f matrix, Vector3f vector)
        {
            return new Vector3f(
                vector.X * matrix.R1_C1 + vector.Y * matrix.R1_C2 + vector.Z * matrix.R1_C3,
                vector.X * matrix.R2_C1 + vector.Y * matrix.R2_C2 + vector.Z * matrix.R2_C3,
                vector.X * matrix.R3_C1 + vector.Y * matrix.R3_C2 + vector.Z * matrix.R3_C3);
        }

        /// <inheritdoc />
        public bool Equals(Matrix3x3f other)
        {
            return
                R1_C1 == other.R1_C1 &&
                R1_C2 == other.R1_C2 &&
                R1_C3 == other.R1_C3 &&
                R2_C1 == other.R2_C1 &&
                R2_C2 == other.R2_C2 &&
                R2_C3 == other.R2_C3 &&
                R3_C1 == other.R3_C1 &&
                R3_C2 == other.R3_C2 &&
                R3_C3 == other.R3_C3;
        }

        /// <summary>
        /// Constructs a transformation matrix which would have the effect of rotating any given vector A by a specific angle around the basis vector B.
        /// </summary>
        /// <param name="vector">The basis vector which the rotation should be centered around.</param>
        /// <param name="radians">The angle to rotate, in radians.</param>
        /// <returns>A transformation matrix which would provide the desired rotation</returns>
        public static Matrix3x3f CreateRotationAroundVector(Vector3f vector, float radians)
        {
            vector = vector.Normalized();
            float x = vector.X;
            float y = vector.Y;
            float z = vector.Z;
            float cosTheta = FastMath.Cos(radians);
            float sinTheta = FastMath.Sin(radians);
            float oneMinusCosTheta = 1 - cosTheta;
            float xy = x * y;
            float xz = x * z;
            float yz = y * z;

            // baked-in Rodrigues Formula
            return new Matrix3x3f(
                cosTheta + (x * x * oneMinusCosTheta),     (xy * oneMinusCosTheta) - (z * sinTheta),   (xz * oneMinusCosTheta) + (y * sinTheta),

                (xy * oneMinusCosTheta) + (z * sinTheta),  cosTheta + (y * y * oneMinusCosTheta),      (yz * oneMinusCosTheta) - (x * sinTheta),

                (xz * oneMinusCosTheta) - (y * sinTheta),  (yz * oneMinusCosTheta) + (x * sinTheta),   cosTheta + (z * z * oneMinusCosTheta)
            );
        }
    }
}
