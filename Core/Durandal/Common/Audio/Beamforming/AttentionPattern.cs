using Durandal.Common.MathExt;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Durandal.Common.Audio.Beamforming
{
    /// <summary>
    /// Represents an array of vectors which are relative positions in space which an array microphone may be interested in
    /// focusing on and which we should calculate excitation / correlation values for. This is intentionally decoupled from
    /// the array geometry itself because the array geometry alone doesn't always inform us of its intended use and what points
    /// it may be interested in listening to, and so the same array may have multiple potential usage patterns, or just
    /// different levels of granularity within the same pattern.
    /// </summary>
    public class AttentionPattern
    {
        /// <summary>
        /// The array of positions in this attention pattern, in units of millimeters, not necessarily in any particular order.
        /// </summary>
        public Vector3f[] Positions { get; private set; }

        /// <summary>
        /// Gets the total number of elements in this pattern.
        /// </summary>
        public int NumElements => Positions.Length;

        /// <summary>
        /// Constructs a new attention pattern out of a set of position vectors.
        /// </summary>
        /// <param name="positions"></param>
        public AttentionPattern(Vector3f[] positions)
        {
            Positions = positions.AssertNonNull(nameof(positions));
        }

        /// <summary>
        /// Constructs an attention pattern of vectors forming a semicircle with a given radius and width away from the origin point.
        /// </summary>
        /// <param name="originPoint">The origin point of the semicircle, in units of millimeters</param>
        /// <param name="forwardVector">The vector defining "forwards", along which the central point of the pattern should lie.</param>
        /// <param name="upVector">The vector defining "up", which is the axis being pivoted around when defining the circle.</param>
        /// <param name="radius">The radius of the pattern in millimeters.</param>
        /// <param name="widenessDegrees">The total width of the pattern in degrees</param>
        /// <param name="granularityDegrees">The desired spacing in degrees between each element in the pattern.</param>
        /// <returns>A newly created pattern with the desired geometry.</returns>
        public static AttentionPattern SemiCircle(
            Vector3f originPoint,
            Vector3f forwardVector,
            Vector3f upVector,
            float radius,
            float widenessDegrees,
            float granularityDegrees)
        {
            if (forwardVector.SquaredMagnitude < 0.00001f)
            {
                throw new ArgumentOutOfRangeException("Forward vector cannot be zero");
            }
            if (upVector.SquaredMagnitude < 0.00001f)
            {
                throw new ArgumentOutOfRangeException("Up vector cannot be zero");
            }
            if (widenessDegrees <= 0 || widenessDegrees > 360)
            {
                throw new ArgumentOutOfRangeException(nameof(widenessDegrees));
            }
            if (radius <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(radius));
            }
            if (granularityDegrees <= 0 || granularityDegrees > widenessDegrees)
            {
                throw new ArgumentOutOfRangeException(nameof(granularityDegrees));
            }

            Vector3f normalizedForward = forwardVector.Normalized();
            Vector3f normalizedUp = upVector.Normalized();
            CheckPerpendicularUnitVectors(normalizedForward, normalizedUp);

            float widenessRadians = FastMath.DegreesToRads(widenessDegrees);
            float granularityRadians = FastMath.DegreesToRads(granularityDegrees);

            List<Vector3f> vectors = new List<Vector3f>();
            AppendCircularPoints(vectors, originPoint, normalizedForward, normalizedUp, radius, widenessRadians, granularityRadians);

            return new AttentionPattern(vectors.ToArray());
        }

        /// <summary>
        /// Constructs an attention pattern of vectors forming an approximately even circle with a given radius from the origin point.
        /// </summary>
        /// <param name="originPoint">The origin point of the circle, in units of millimeters</param>
        /// <param name="forwardVector">The vector defining "forwards", which in this particular case isn't really necessary.</param>
        /// <param name="upVector">The vector defining "up", which is the axis being pivoted around when defining the circle.</param>
        /// <param name="radius">The radius of the pattern in millimeters.</param>
        /// <param name="granularityDegrees">The desired spacing in degrees between each element in the pattern.</param>
        /// <returns>A newly created pattern with the desired geometry.</returns>
        public static AttentionPattern Circle(
            Vector3f originPoint,
            Vector3f forwardVector,
            Vector3f upVector,
            float radius,
            float granularityDegrees)
        {
            return SemiCircle(originPoint, forwardVector, upVector, radius, 360, granularityDegrees);
        }

        /// <summary>
        /// Constructs an attention pattern of vectors forming a rectangular slice of a sphere, constrained by both a horizontal and vertical angle,
        /// with horizontal and vertical spacing of points controlled independently.
        /// </summary>
        /// <param name="originPoint">The origin point of the sphere, in units of millimeters</param>
        /// <param name="forwardVector">The vector defining "forwards", along which the central point of the pattern should lie.</param>
        /// <param name="upVector">The vector defining "up", which is the axis being pivoted around when defining the circle.</param>
        /// <param name="radius">The radius of the pattern in millimeters.</param>
        /// <param name="horizontalWidenessDegrees">The horizontal width of the pattern in degrees, where 360 will span the full equator</param>
        /// <param name="verticalWidenessDegrees">The vertical width of the pattern in degrees, where 180 degrees spans from south to north poles</param>
        /// <param name="horizontalGranularityDegrees">The desired spacing in degrees between each element in the pattern horizontally along the equator
        /// (the actual spacing will be augmented outside of the equator poles as an optimization to prevent points from getting too bunched up</param>
        /// <param name="verticalGranularityDegrees">The desired spacing in degrees between each element in the pattern vertically.</param>
        /// <returns>A newly created pattern with the desired geometry.</returns>
        public static AttentionPattern SemiSphere(
            Vector3f originPoint,
            Vector3f forwardVector,
            Vector3f upVector,
            float radius,
            float horizontalWidenessDegrees,
            float verticalWidenessDegrees,
            float horizontalGranularityDegrees,
            float verticalGranularityDegrees)
        {
            if (forwardVector.SquaredMagnitude < 0.00001f)
            {
                throw new ArgumentOutOfRangeException("Forward vector cannot be zero");
            }
            if (upVector.SquaredMagnitude < 0.00001f)
            {
                throw new ArgumentOutOfRangeException("Up vector cannot be zero");
            }
            if (radius <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(radius));
            }
            if (horizontalWidenessDegrees <= 0 || horizontalWidenessDegrees > 361)
            {
                throw new ArgumentOutOfRangeException(nameof(horizontalWidenessDegrees));
            }
            if (verticalWidenessDegrees <= 0 || verticalWidenessDegrees > 181)
            {
                throw new ArgumentOutOfRangeException(nameof(verticalWidenessDegrees));
            }
            if (horizontalGranularityDegrees <= 0 || horizontalGranularityDegrees > horizontalWidenessDegrees)
            {
                throw new ArgumentOutOfRangeException(nameof(horizontalGranularityDegrees));
            }
            if (verticalGranularityDegrees <= 0 || verticalGranularityDegrees > verticalWidenessDegrees)
            {
                throw new ArgumentOutOfRangeException(nameof(verticalGranularityDegrees));
            }

            Vector3f normalizedForward = forwardVector.Normalized();
            Vector3f normalizedUp = upVector.Normalized();
            CheckPerpendicularUnitVectors(normalizedForward, normalizedUp);

            float horizontalGranularityRadians = FastMath.DegreesToRads(horizontalGranularityDegrees);
            float horizontalWidenessRadians = FastMath.DegreesToRads(horizontalWidenessDegrees);
            float verticalGranularityRadians = FastMath.DegreesToRads(verticalGranularityDegrees);
            float verticalWidenessRadians = FastMath.DegreesToRads(verticalWidenessDegrees);

            List<Vector3f> vectors = new List<Vector3f>();

            int numLayers = (int)Math.Floor((verticalWidenessRadians + 0.001f) / verticalGranularityRadians); // add a tiny epsilon to account for exact measurements e.g. "40 degrees divided by 10"

            // phi measures vertical angle
            // 0 == horizontal
            float phiRadians = 0 - ((((float)numLayers / 2) - 0.5f) * verticalGranularityRadians);

            // Iterate through vertical layers
            for (int point = 0; point < numLayers; point++)
            {
                float thisLayerRadiusFraction = FastMath.Cos(phiRadians);
                float thisLayerVerticalFraction = FastMath.Sin(phiRadians);
                Vector3f thisLayerOrigin = originPoint + (normalizedUp * thisLayerVerticalFraction * radius);
                AppendCircularPoints(
                    vectors,
                    thisLayerOrigin,
                    normalizedForward,
                    normalizedUp,
                    radius * thisLayerRadiusFraction,
                    horizontalWidenessRadians,
                    horizontalGranularityRadians / thisLayerRadiusFraction); // Reduce the horizontal granularity as we go up because they'll get really dense near the poles
                phiRadians += verticalGranularityRadians;
            }

            return new AttentionPattern(vectors.ToArray());
        }

        /// <summary>
        /// Constructs an attention pattern of vectors forming a slice of a sphere, constrained by vertical angle,
        /// with horizontal and vertical spacing of points controlled independently. In other words, it's a sphere with the top and bottom chopped off.
        /// Not quite mathematically a torus, but close enough to what people would imagine.
        /// </summary>
        /// <param name="originPoint">The origin point of the sphere, in units of millimeters</param>
        /// <param name="forwardVector">The vector defining "forwards", along which the central point of the pattern should lie.</param>
        /// <param name="upVector">The vector defining "up", which is the axis being pivoted around when defining the circle.</param>
        /// <param name="radius">The radius of the pattern in millimeters.</param>
        /// <param name="verticalWidenessDegrees">The vertical width of the pattern in degrees, where 180 degrees spans from south to north poles</param>
        /// <param name="horizontalGranularityDegrees">The desired spacing in degrees between each element in the pattern horizontally along the equator
        /// (the actual spacing will be augmented outside of the equator poles as an optimization to prevent points from getting too bunched up</param>
        /// <param name="verticalGranularityDegrees">The desired spacing in degrees between each element in the pattern vertically.</param>
        /// <returns>A newly created pattern with the desired geometry.</returns>
        public static AttentionPattern Torus(
            Vector3f originPoint,
            Vector3f forwardVector,
            Vector3f upVector,
            float radius,
            float verticalWidenessDegrees,
            float horizontalGranularityDegrees,
            float verticalGranularityDegrees)
        {
            return SemiSphere(originPoint, forwardVector, upVector, radius, 360, verticalWidenessDegrees, horizontalGranularityDegrees, verticalGranularityDegrees);
        }

        /// <summary>
        /// Constructs an attention pattern of vectors forming a complete sphere, with horizontal and vertical spacing of points controlled independently
        /// </summary>
        /// <param name="originPoint">The origin point of the sphere, in units of millimeters</param>
        /// <param name="forwardVector">The vector defining "forwards", along which the central point of the pattern should lie.</param>
        /// <param name="upVector">The vector defining "up", which is the axis being pivoted around when defining the circle.</param>
        /// <param name="radius">The radius of the pattern in millimeters.</param>
        /// <param name="horizontalGranularityDegrees">The desired spacing in degrees between each element in the pattern horizontally along the equator
        /// (the actual spacing will be augmented outside of the equator poles as an optimization to prevent points from getting too bunched up</param>
        /// <param name="verticalGranularityDegrees">The desired spacing in degrees between each element in the pattern vertically.</param>
        /// <returns>A newly created pattern with the desired geometry.</returns>
        public static AttentionPattern Sphere(
            Vector3f originPoint,
            Vector3f forwardVector,
            Vector3f upVector,
            float radius,
            float horizontalGranularityDegrees,
            float verticalGranularityDegrees)
        {
            return SemiSphere(originPoint, forwardVector, upVector, radius, 360, 180, horizontalGranularityDegrees, verticalGranularityDegrees);
        }

        private static void CheckPerpendicularUnitVectors(Vector3f forward, Vector3f up)
        {
            float perpendicularityDegrees = FastMath.RadsToDegrees(forward.AngleBetweenUnitVectors(up));
            if (perpendicularityDegrees < 89 || perpendicularityDegrees > 91)
            {
                throw new ArgumentOutOfRangeException($"Forward and up vectors must be perpendicular. Actual angle: {perpendicularityDegrees} degrees");
            }
        }

        private static void AppendCircularPoints(
            List<Vector3f> output,
            Vector3f originPoint,
            Vector3f normalizedForward,
            Vector3f normalizedUp,
            float radius,
            float widenessRadians,
            float granularityRadians)
        {

            int numPoints = (int)Math.Floor((widenessRadians + 0.001f) / granularityRadians); // add a tiny epsilon to account for exact measurements e.g. "90 degrees divided by 10"
            float thetaRadians = 0 - ((((float)numPoints / 2) - 0.5f) * granularityRadians);

            for (int point = 0; point < numPoints; point++)
            {
                output.Add(originPoint + (normalizedForward * radius * Matrix3x3f.CreateRotationAroundVector(normalizedUp, thetaRadians)));
                thetaRadians += granularityRadians;
            }
        }
    }
}
