using Durandal.Common.ServiceMgmt;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Audio.Components
{
    public sealed class PeakFilter : BiquadFilter
    {
        private float _peakGainDb;
        private float _frequency;
        private float _bandWidth;

        /// <summary>
        /// Constructs a new peak filter.
        /// </summary>
        /// <param name="graph">The graph this component will be part of</param>
        /// <param name="format">The filter's audio format</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        /// <param name="centerFrequencyHz">The center frequency to adjust</param>
        /// <param name="peakGainDb">The gain to apply at the peak in decibels</param>
        /// <param name="bandWidth">The width of the peak to be adjusted (analogous to q parameter in other filters)</param>
        public PeakFilter(WeakPointer<IAudioGraph> graph, AudioSampleFormat format, string nodeCustomName, float centerFrequencyHz, float peakGainDb, float bandWidth)
            : base(graph, format, nameof(PeakFilter), nodeCustomName)
        {
            if (centerFrequencyHz < 0)
            {
                throw new ArgumentOutOfRangeException("Frequency must be non-negative");
            }
            if (bandWidth <= 0)
            {
                throw new ArgumentOutOfRangeException("Band width must be a positive value");
            }
            if (float.IsNaN(peakGainDb) || float.IsInfinity(peakGainDb))
            {
                throw new ArgumentOutOfRangeException("Gain parameter accepts finite values only");
            }

            _frequency = centerFrequencyHz;
            _peakGainDb = peakGainDb;
            _bandWidth = bandWidth;
            Coefficients = CalculateBiQuadCoefficients();
        }

        /// <summary>
        /// Gets or sets the filter's center frequency
        /// </summary>
        public float FilterFrequencyHz
        {
            get
            {
                return _frequency;
            }
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException("Frequency must be non-negative");
                }

                _frequency = value;
                Coefficients = CalculateBiQuadCoefficients();
            }
        }

        /// <summary>
        /// Gets or sets the filter's gain parameter
        /// </summary>
        public float GainDB
        {
            get
            {
                return _peakGainDb;
            }
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException("Gain parameter accepts finite values only");
                }

                _peakGainDb = value;
                Coefficients = CalculateBiQuadCoefficients();
            }
        }

        /// <summary>
        /// Gets or sets the filter's band width parameter, which determines how widely the gain is applied.
        /// </summary>
        public float BandWidth
        {
            get
            {
                return _bandWidth;
            }
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException("Band width must be a positive value");
                }

                _bandWidth = value;
                Coefficients = CalculateBiQuadCoefficients();
            }
        }

        protected override BiquadCoefficients CalculateBiQuadCoefficients()
        {
            float A0, A1, A2, B1, B2;
            float norm;
            float v = (float)Math.Pow(10, Math.Abs(_peakGainDb) / 20.0);
            float k = (float)Math.Tan(Math.PI * _frequency / (float)InputFormat.SampleRateHz);
            float q = _bandWidth;

            if (_peakGainDb >= 0)
            {
                // boost
                norm = 1 / (1 + 1 / q * k + k * k);
                A0 = (1 + v / q * k + k * k) * norm;
                A1 = 2 * (k * k - 1) * norm;
                A2 = (1 - v / q * k + k * k) * norm;
                B1 = A1;
                B2 = (1 - 1 / q * k + k * k) * norm;
            }
            else
            {
                // cut
                norm = 1 / (1 + v / q * k + k * k);
                A0 = (1 + 1 / q * k + k * k) * norm;
                A1 = 2 * (k * k - 1) * norm;
                A2 = (1 - 1 / q * k + k * k) * norm;
                B1 = A1;
                B2 = (1 - v / q * k + k * k) * norm;
            }

            return new BiquadCoefficients() { A0 = A0, A1 = A1, A2 = A2, B1 = B1, B2 = B2 };
        }
    }
}
