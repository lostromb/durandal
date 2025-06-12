using Durandal.Common.MathExt;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Durandal.Common.Audio.Beamforming
{
    [DebuggerDisplay("{AIndex}-{BIndex}")]
    public class MicPair
    {
        public MicPair(
            int aIndex,
            int bIndex,
            Vector3f aVector,
            Vector3f bVector,
            int sampleRate,
            float angleResolutionDegrees)
        {
            if (aIndex < 0)
            {
                throw new ArgumentOutOfRangeException("AIndex must be a nonnegative integer");
            }
            if (bIndex < 0)
            {
                throw new ArgumentOutOfRangeException("BIndex must be a nonnegative integer");
            }
            if (aIndex == bIndex)
            {
                throw new ArgumentOutOfRangeException("AIndex and BIndex must be distinct");
            }
            if (sampleRate <= 0)
            {
                throw new ArgumentOutOfRangeException("Sample rate must be a positive integer");
            }
            if (angleResolutionDegrees <= 0)
            {
                throw new ArgumentOutOfRangeException("Angle resolution must be a positive number");
            }
            if (angleResolutionDegrees > 90)
            {
                throw new ArgumentOutOfRangeException("Angle resolution must less than 90 degrees");
            }

            AIndex = aIndex;
            BIndex = bIndex;
            ElementSeparationMm = aVector.Distance(bVector);
            if (ElementSeparationMm == 0)
            {
                throw new ArgumentOutOfRangeException("Microphone element vectors must have non-zero distance between them");
            }
            
            PrimaryAxis = (bVector - aVector) / ElementSeparationMm;
            ElementSeparationSamples = ElementSeparationMm / AudioMath.SpeedOfSoundMillimetersPerSample(sampleRate);
            Centroid = (aVector + bVector) / 2.0f;

            // Precalculate a set of offsets that correlate with angles of sound incidence
            // this math is a bit inaccurate because it assumes infinite sound source distance
            // (i.e. that incoming waves come along a perfectly flat plane, rather than a curve)
            // Potentially in the future we could change it to assume a 3 meter distance or something...
            List<Tuple<float, int>> vectorAngles = new List<Tuple<float, int>>();
            HashSet<int> anglesUsed = new HashSet<int>();
            float angleResolutionRadians = angleResolutionDegrees * (float)Math.PI / 180;
            vectorAngles.Add(new Tuple<float, int>((float)Math.PI / 2.0f, 0));
            anglesUsed.Add(0);
            float singleSampleLengthMm = AudioMath.SpeedOfSoundMillimetersPerSample(sampleRate);
            int maxSampleDelay = (int)Math.Round(ElementSeparationSamples);
            for (int sampleDelay = 1; sampleDelay <= maxSampleDelay; sampleDelay++)
            {
                float incidenceAngle;
                float del = (sampleDelay * singleSampleLengthMm);
                if (del >= ElementSeparationMm)
                {
                    // cap at 180 degrees maximum divergence
                    incidenceAngle = 0;
                }
                else
                {
                    incidenceAngle = (float)Math.Acos(del / ElementSeparationMm);
                }

                // Try rounding down and then up to the nearest equidistant angle to see if either of them
                // have been "occupied" so far. Prevents us from specifying different vectors with indistinguishable
                // element separations, while also avoiding "holes" if all the vectors tend to round one way only
                float z = (((float)Math.PI / 2) - incidenceAngle) / angleResolutionRadians;
                int roundedAngle = (int)Math.Ceiling(z);
                if (!anglesUsed.Contains(roundedAngle))
                {
                    vectorAngles.Add(new Tuple<float, int>(incidenceAngle, sampleDelay));
                    vectorAngles.Add(new Tuple<float, int>((float)Math.PI - incidenceAngle, 0 - sampleDelay));
                    anglesUsed.Add(roundedAngle);
                }
                else
                {
                    roundedAngle = (int)Math.Floor(z);
                    if (!anglesUsed.Contains(roundedAngle))
                    {
                        vectorAngles.Add(new Tuple<float, int>(incidenceAngle, sampleDelay));
                        vectorAngles.Add(new Tuple<float, int>((float)Math.PI - incidenceAngle, 0 - sampleDelay));
                        anglesUsed.Add(roundedAngle);
                    }
                }
            }

            vectorAngles.Sort((a, b) => a.Item2.CompareTo(b.Item2));
            VectorAngleOffsets = vectorAngles;
        }

        /// <summary>
        /// The index of microphone "A" in the array
        /// </summary>
        public int AIndex { get; set; }

        /// <summary>
        /// The index of microphone "B" in the array
        /// </summary>
        public int BIndex { get; set; }

        /// <summary>
        /// The physical separation distance between elements A and B, in millimeters.
        /// </summary>
        public float ElementSeparationMm { get; set; }

        /// <summary>
        /// Whole and partial samples that it takes for a wave to travel between the pair of
        /// mic elements at the current sample rate and with normal speed of sound.
        /// </summary>
        public float ElementSeparationSamples { get; set; }

        /// <summary>
        /// A collection of pairs representing an angle of deviation from the primary axis vector,
        /// paired with the number of samples of time difference correlates with that angle,
        /// expressed as the number of samples ahead of A that B would hear the sound.
        /// So offset &gt; 0 implies angle &lt; 90 degrees and B hears the sound first.
        /// The elements are sorted in ascending order by vector angle.
        /// </summary>
        public IReadOnlyList<Tuple<float, int>> VectorAngleOffsets { get; set; }

        /// <summary>
        /// A unit vector pointing from element A to element B, indicating the primary
        /// axis used when expressing angle offsets. An angle of zero == this vector,
        /// PI = the inverse of this vector, and all others represent a cone section in between
        /// </summary>
        public Vector3f PrimaryAxis { get; set; }

        /// <summary>
        /// The position of the median place between the two microphone elements,
        /// which represents roughly where the "hearing" happens.
        /// </summary>
        public Vector3f Centroid { get; set; }
    }
}
