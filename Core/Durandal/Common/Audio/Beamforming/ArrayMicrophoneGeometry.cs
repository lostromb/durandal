using Durandal.Common.MathExt;
using Durandal.Common.Time.Timex;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Durandal.Common.Audio.Beamforming
{
    /// <summary>
    /// A definition of the physical arrangement and geometric characteristics of an array microphone device.
    /// </summary>
    public class ArrayMicrophoneGeometry
    {
        private readonly Vector3f[] _normalizedPositions; // "normalized" means that the average of all position vectors is 0,0,0
        private readonly float _radius;
        private readonly float _maxDistance;
        private readonly Tuple<int, int>[] _pairings;

        public ArrayMicrophoneGeometry(
            Vector3f[] positions,
            params Tuple<int, int>[] pairings)
        {
            Vector3f centroid = Vector3f.Zero;
            foreach (var position in positions)
            {
                centroid += position;
            }

            centroid /= (float)positions.Length;

            _pairings = pairings;
            _radius = 0;
            _maxDistance = 0;
            _normalizedPositions = new Vector3f[positions.Length];
            for (int c = 0; c < positions.Length; c++)
            {
                Vector3f normalizedVec= positions[c] - centroid;
                float length = normalizedVec.Magnitude;
                _normalizedPositions[c] = normalizedVec;

                // Calculate the radius as the length of the farthest vector from the centroid
                if (length > _radius)
                {
                    _radius = length;
                }
            }

            for (int x = 0; x < _normalizedPositions.Length; x++)
            {
                for (int y = x + 1; y < _normalizedPositions.Length; y++)
                {
                    float length = _normalizedPositions[x].Distance(_normalizedPositions[y]);
                    if (length > _maxDistance)
                    {
                        _maxDistance = length;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the number of elements in this microphone array
        /// </summary>
        public int NumElements => _normalizedPositions.Length;

        /// <summary>
        /// Gets the radius of the area; that is, the distance in millimeters of
        /// the farthers microphone from the centroid.
        /// </summary>
        public float Radius => _radius;

        /// <summary>
        /// Gets maximum distance between any given pair of microphones in the array, in units of millimeters
        /// </summary>
        public float MaxElementSeparation => _maxDistance;

        /// <summary>
        /// Gets the physical position of every microphone in the array relative to the centroid,
        /// indexed by channel number, and in units of millimeters.
        /// If someone is facing the microphone and speaking into it, positive X is to the speaker's right,
        /// positive Z is up, and positive Y is away from the speaker.
        /// </summary>
        public IReadOnlyList<Vector3f> MicrophonePositions => _normalizedPositions;

        /// <summary>
        /// A list of tuples suggesting which pairs of microphone elements should be used
        /// for optimal 
        /// </summary>
        public IReadOnlyList<Tuple<int, int>> MicrophonePairings => _pairings;

        /// <summary>
        /// Standardized array geometry for the PS3 Eye microphone.
        /// </summary>
        public static readonly ArrayMicrophoneGeometry PS3_EYE = new ArrayMicrophoneGeometry(new Vector3f[]
            {
                new Vector3f(-30, 0, 0), // left side, above red LED
                new Vector3f(10, 0, 0), // the two middle channels are swapped
                new Vector3f(-10, 0, 0),
                new Vector3f(30, 0, 0), // right side, above blue LED
            },
            new Tuple<int, int>(0, 3),
            new Tuple<int, int>(0, 1),
            new Tuple<int, int>(2, 3));
    }
}
