using Durandal.Common.ServiceMgmt;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Audio.Components
{
    public sealed class BandPassFilter : BiquadFilter
    {
        private float _q;
        private float _frequency;

        /// <summary>
        /// Constructs a new bandpass filter.
        /// </summary>
        /// <param name="graph">The graph this component will be part of</param>
        /// <param name="format">The filter's audio format</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        /// <param name="frequencyHz">The filter's corner frequency</param>
        /// <param name="q">The filter's q parameter</param>
        public BandPassFilter(WeakPointer<IAudioGraph> graph, AudioSampleFormat format, string nodeCustomName, float frequencyHz, float q = 0.70721f)
            : base(graph, format, nameof(BandPassFilter), nodeCustomName)
        {
            if (frequencyHz < 0)
            {
                throw new ArgumentOutOfRangeException("Frequency must be non-negative");
            }
            if (q <= 0)
            {
                throw new ArgumentOutOfRangeException("Q must be a positive value");
            }

            _frequency = frequencyHz;
            _q = q;
            Coefficients = CalculateBiQuadCoefficients();
        }

        /// <summary>
        /// Gets or sets the filter's corner frequency
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
        /// Gets or sets the filter's Q parameter, which affects the shape of the corner
        /// </summary>
        public float Q
        {
            get
            {
                return _q;
            }
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException("Q must be a positive value");
                }

                _q = value;
                Coefficients = CalculateBiQuadCoefficients();
            }
        }

        protected override BiquadCoefficients CalculateBiQuadCoefficients()
        {
            float A0, A1, A2, B1, B2;
            float k = (float)Math.Tan(Math.PI * _frequency / (float)InputFormat.SampleRateHz);
            float norm = 1 / (1 + k / _q + k * k);
            A0 = k / _q * norm;
            A1 = 0;
            A2 = -A0;
            B1 = 2 * (k * k - 1) * norm;
            B2 = (1 - k / _q + k * k) * norm;
            return new BiquadCoefficients() { A0 = A0, A1 = A1, A2 = A2, B1 = B1, B2 = B2 };
        }
    }
}
