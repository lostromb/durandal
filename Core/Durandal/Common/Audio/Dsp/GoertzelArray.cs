
using Durandal.Common.Audio.Codecs.ADPCM;
using Durandal.Common.Audio.WebRtc;
using Durandal.Common.Collections;
using Durandal.Common.MathExt;
using Durandal.Common.Utils;
using System;
using System.Numerics;

namespace Durandal.Common.Audio.Dsp
{
    // Based on https://github.com/bert2/DtmfDetection/blob/master/src/DtmfDetection/Goertzel.cs
    /// <summary>
    /// This class implements a row of filters for detecting specific frequencies inside of a waveform.
    /// It can detect many different frequencies simultaneously, hence the "array" of filters.
    /// </summary>
    public class GoertzelArray
    {
        private readonly int _inputSampleRate;

        /// <summary>
        /// Gets the number of filters in the array.
        /// </summary>
        public int NumFilters => _numFilters;

        /// <summary>
        /// Gets the number of samples required to accumulate before the filter output is ready to use.
        /// </summary>
        public int AccumulatorSizeSamples => _accumulatorSizeSamples;

        private int _numFilters;
        private int _paddedWidth;
        private int _accumulatorSizeSamples;

        /// <summary>
        /// Stores a pre-computed coefficient calculated from the parameters of Init().
        /// </summary>
        private float[] _C;

        /// <summary>
        /// Stores the state of the Goertzel. Used to determine the strength of the target frequency in the signal
        /// </summary>
        private float[] _S1;
        private float[] _S2;
        private float[] _R;

        /// <summary>
        /// Accumulates the total signal energy of the signal. Used for normalization.
        /// </summary>
        private float _E;

        // for lazily calculating the response vector
        private bool _responseUpToDate = false;

        /// <summary>
        /// Creates an empty Goertzel filter array
        /// </summary>
        /// <param name="samplesToAccumulate">The number of samples that you plan to accumulate to the filter before Response is calculated. This should be at least 25ms of samples (200 at 8Khz).</param>
        public GoertzelArray(int samplesToAccumulate, int sampleRateHz)
        {
            _accumulatorSizeSamples = samplesToAccumulate.AssertPositive(nameof(samplesToAccumulate));
            _inputSampleRate = sampleRateHz.AssertPositive(nameof(sampleRateHz));
            _numFilters = 0;
            _paddedWidth = 0;
            _C = BinaryHelpers.EMPTY_FLOAT_ARRAY;
            _S1 = BinaryHelpers.EMPTY_FLOAT_ARRAY;
            _S2 = BinaryHelpers.EMPTY_FLOAT_ARRAY;
            _R = BinaryHelpers.EMPTY_FLOAT_ARRAY;
            _E = 0.0f;
        }

        /// <summary>
        /// Gets the total frequency response across all filters in the array
        /// </summary>
        public ReadOnlySpan<float> Response
        {
            get
            {
                UpdateResponseVectorIfNeeded();
                return _R.AsSpan(0, _numFilters);
            }
        }

        /// <summary>
        /// Gets the current total energy of all samples so far. You can use Response / SignalEnergy to get loudness-normalized response values.
        /// </summary>
        public float SignalEnergy => _E;

        /// <summary>
        /// Adds a filter for a given target frequency.
        /// </summary>
        /// <param name="targetFreq">The target frequency to estimate the strength for in a signal.</param>
        public void AddFilter(float targetFreq)
        {
            int filterIdx = _numFilters;
            Resize(_numFilters + 1);

            float k = targetFreq / (float) _inputSampleRate * (float)_accumulatorSizeSamples;
            _C[filterIdx] = 2.0f * (float)Math.Cos(2.0 * Math.PI * k / _accumulatorSizeSamples);
            _S1[filterIdx] = 0.0f;
            _S2[filterIdx] = 0.0f;
            _R[filterIdx] = 0.0f;
            _responseUpToDate = false;
        }

        /// <summary>
        /// Adds a single sample to the filter array.
        /// </summary>
        /// <param name="sample">The waveform sample to add, from -1 to 1</param>
        public void AddSample(float sample)
        {
#if DEBUG
            if (_numFilters > 1 && Vector.IsHardwareAccelerated && FastRandom.Shared.NextBool())
#else
            if (_numFilters > 1 && Vector.IsHardwareAccelerated)
#endif
            {
                int idx = 0;
                while (idx < _paddedWidth)
                {
                    // Vector implementation. No residual loop because we have padded our data to the vector width in advance.
                    Vector<float> vectorSwap = new Vector<float>(_S1, idx);
                    Vector.Subtract(
                        Vector.Add(
                            new Vector<float>(sample),
                            Vector.Multiply(
                                new Vector<float>(_C, idx),
                                new Vector<float>(_S1, idx))),
                        new Vector<float>(_S2, idx))
                        .CopyTo(_S1, idx);
                    vectorSwap.CopyTo(_S2, idx);
                    idx += Vector<float>.Count;
                }
            }
            else
            {
                // Naive implementation
                for (int idx = 0; idx < _numFilters; idx++)
                {
                    float swap = _S1[idx];
                    _S1[idx] = sample + _C[idx] * _S1[idx] - _S2[idx];
                    _S2[idx] = swap;
                }
            }

            _E += sample * sample;
            _responseUpToDate = false;
        }

        /// <summary>
        /// Adds an array of samples to the filter array.
        /// </summary>
        /// <param name="samples">The span of samples to add.</param>
        public void AddSamples(ReadOnlySpan<float> samples)
        {
#if DEBUG
            if (_numFilters > 1 && Vector.IsHardwareAccelerated && FastRandom.Shared.NextBool())
#else
            if (_numFilters > 1 && Vector.IsHardwareAccelerated)
#endif
            {
                foreach (float sample in samples)
                {
                    int idx = 0;
                    while (idx < _paddedWidth)
                    {
                        // Vector implementation. No residual loop because we have padded our data to the vector width in advance.
                        Vector<float> vectorSwap = new Vector<float>(_S1, idx);
                        Vector.Subtract(
                            Vector.Add(
                                new Vector<float>(sample),
                                Vector.Multiply(
                                    new Vector<float>(_C, idx),
                                    new Vector<float>(_S1, idx))),
                            new Vector<float>(_S2, idx))
                            .CopyTo(_S1, idx);
                        vectorSwap.CopyTo(_S2, idx);
                        idx += Vector<float>.Count;
                    }
                }
            }
            else
            {
                foreach (float sample in samples)
                {
                    // Naive implementation
                    for (int idx = 0; idx < _numFilters; idx++)
                    {
                        float swap = _S1[idx];
                        _S1[idx] = sample + _C[idx] * _S1[idx] - _S2[idx];
                        _S2[idx] = swap;
                    }
                }
            }

            foreach (float sample in samples)
            {
                _E += sample * sample;
            }

            _responseUpToDate = false;
        }

        /// <summary>
        /// Resets the state of this filter array.
        /// </summary>
        public void Reset()
        {
            _S1.AsSpan().Clear();
            _S2.AsSpan().Clear();
            _R.AsSpan().Clear();
            _E = 0.0f;
            _responseUpToDate = false;
        }

        private void UpdateResponseVectorIfNeeded()
        {
            if (_responseUpToDate)
            {
                return;
            }

#if DEBUG
            if (_numFilters > 1 && Vector.IsHardwareAccelerated && FastRandom.Shared.NextBool())
#else
            if (_numFilters > 1 && Vector.IsHardwareAccelerated)
#endif
            {
                int idx = 0;
                while (idx < _paddedWidth)
                {
                    // Vector implementation. No residual loop because we have padded our data to the vector width in advance.
                    Vector<float> s1 = new Vector<float>(_S1, idx);
                    Vector<float> s2 = new Vector<float>(_S2, idx);
                    Vector.Subtract(
                        Vector.Add(
                            Vector.Multiply(s1, s1),
                            Vector.Multiply(s2, s2)),
                        Vector.Multiply(s1,
                            Vector.Multiply(s2, new Vector<float>(_C, idx))))
                        .CopyTo(_R, idx);
                    idx += Vector<float>.Count;
                }
            }
            else
            {
                // Naive implementation
                for (int idx = 0; idx < _numFilters; idx++)
                {
                    _R[idx] = _S1[idx] * _S1[idx] + _S2[idx] * _S2[idx] - _S1[idx] * _S2[idx] * _C[idx];
                }
            }

            _responseUpToDate = true;
        }

        private void Resize(int newFilterCount)
        {
            if (newFilterCount > _paddedWidth)
            {
                int newPaddedWidth = newFilterCount;
                if (Vector.IsHardwareAccelerated)
                {
                    newPaddedWidth = newPaddedWidth - (newPaddedWidth % Vector<float>.Count) + Vector<float>.Count;
                }

                float[] newC = new float[newPaddedWidth];
                float[] newS1 = new float[newPaddedWidth];
                float[] newS2 = new float[newPaddedWidth];
                float[] newR = new float[newPaddedWidth];
                if (_numFilters > 0)
                {
                    _C.AsSpan(0, _numFilters).CopyTo(newC.AsSpan(0, _numFilters));
                    _S1.AsSpan(0, _numFilters).CopyTo(newS1.AsSpan(0, _numFilters));
                    _S2.AsSpan(0, _numFilters).CopyTo(newS2.AsSpan(0, _numFilters));
                    _R.AsSpan(0, _numFilters).CopyTo(newR.AsSpan(0, _numFilters));
                }

                _C = newC;
                _S1 = newS1;
                _S2 = newS2;
                _R = newR;
                _numFilters = newFilterCount;
                _paddedWidth = newPaddedWidth;
            }

            _numFilters = newFilterCount;
        }
    }
}