using Durandal.Common.ServiceMgmt;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Audio.Components
{
    public sealed class LowShelfFilter : BiquadFilter
    {
        private float _gainDb;
        private float _frequency;

        /// <summary>
        /// Constructs a new low shelf filter (lowpass without completely muting the higher bands)
        /// </summary>
        /// <param name="graph">The graph this component will be part of</param>
        /// <param name="format">The filter's audio format</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        /// <param name="frequencyHz">The filter's corner frequency</param>
        /// <param name="gainDB">The filter's gain value in dB, or in other words, the amount that higher bands will be muted.</param>
        public LowShelfFilter(WeakPointer<IAudioGraph> graph, AudioSampleFormat format, string nodeCustomName, float frequencyHz, float gainDB)
            : base(graph, format, nameof(LowShelfFilter), nodeCustomName)
        {
            if (frequencyHz < 0)
            {
                throw new ArgumentOutOfRangeException("Frequency must be non-negative");
            }
            if (float.IsNaN(gainDB) || float.IsInfinity(gainDB))
            {
                throw new ArgumentOutOfRangeException("Gain parameter accepts finite values only");
            }

            _frequency = frequencyHz;
            _gainDb = gainDB;
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
        /// Gets or sets the filter's gain parameter
        /// </summary>
        public float GainDB
        {
            get
            {
                return _gainDb;
            }
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException("Gain parameter accepts finite values only");
                }

                _gainDb = value;
                Coefficients = CalculateBiQuadCoefficients();
            }
        }

        protected override BiquadCoefficients CalculateBiQuadCoefficients()
        {
            float A0, A1, A2, B1, B2;
            const float sqrt2 = 1.4142135623730951f;
            float k = (float)Math.Tan(Math.PI * _frequency / (float)InputFormat.SampleRateHz);
            float v = (float)Math.Pow(10, Math.Abs(_gainDb) / 20.0);
            float norm;
            if (_gainDb >= 0)
            {
                // boost
                norm = 1 / (1 + sqrt2 * k + k * k);
                A0 = (1 + (float)Math.Sqrt(2 * v) * k + v * k * k) * norm;
                A1 = 2 * (v * k * k - 1) * norm;
                A2 = (1 - (float)Math.Sqrt(2 * v) * k + v * k * k) * norm;
                B1 = 2 * (k * k - 1) * norm;
                B2 = (1 - sqrt2 * k + k * k) * norm;
            }
            else
            { 
                // cut
                norm = 1 / (1 + (float)Math.Sqrt(2 * v) * k + v * k * k);
                A0 = (1 + sqrt2 * k + k * k) * norm;
                A1 = 2 * (k * k - 1) * norm;
                A2 = (1 - sqrt2 * k + k * k) * norm;
                B1 = 2 * (v * k * k - 1) * norm;
                B2 = (1 - (float)Math.Sqrt(2 * v) * k + v * k * k) * norm;
            }

            return new BiquadCoefficients() { A0 = A0, A1 = A1, A2 = A2, B1 = B1, B2 = B2 };
        }
    }
}
