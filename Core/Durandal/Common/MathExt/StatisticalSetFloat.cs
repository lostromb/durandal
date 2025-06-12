namespace Durandal.Common.MathExt
{
    using Durandal.Common.Collections;
    using Durandal.Common.Utils;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    /// Represents a dynamically-sized set of numbers for which we may want to calculate
    /// useful statistics such as arithmetic mean, standard deviation, etc.
    /// Calculations are vectorized and cached for best performance where possible.
    /// Operations on this class are thread safe.
    /// </summary>
    public class StatisticalSetFloat
    {
        /// <summary>
        /// The current set of samples. This implementation only allows the set to be added to or cleared.
        /// </summary>
        private readonly List<float> _samples;

        /// <summary>
        /// The currently calculated arithmetic mean, updated every time we add a new value to the set.
        /// </summary>
        private float _currentMean = 0;

        /// <summary>
        /// The cached value for the variance of the set, lazily updated.
        /// </summary>
        private float? _cachedVariance = null;

        private bool _samplesAreSorted = false;

        /// <summary>
        /// Creates a new <see cref="StatisticalSetFloat"/> with default initial capacity.
        /// </summary>
        public StatisticalSetFloat() : this(16)
        {
        }

        /// <summary>
        /// Creates a new <see cref="StatisticalSetFloat"/> with a specific initial capacity (to avoid future allocations).
        /// </summary>
        public StatisticalSetFloat(int suggestedCapacity)
        {
            if (suggestedCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException("Capacity must be greater than zero", nameof(suggestedCapacity));
            }

            _samples = new List<float>(suggestedCapacity);
        }

        public override string ToString()
        {
            return string.Format("N: {0} Min: {1:F3} Max: {2:F3} Mean: {3:F3}, StdDev: {4:F3} DV: {5}",
                SampleCount,
                Minimum,
                Maximum,
                Mean,
                StandardDeviation,
                DistinctValueCount);
        }

        /// <summary>
        /// Clears all samples from the value set.
        /// </summary>
        public void Clear()
        {
            lock (_samples)
            {
                _samples.Clear();
                _cachedVariance = null;
                _currentMean = 0;
                _samplesAreSorted = false;
            }
        }

        /// <summary>
        /// Adds a single sample to the value set.
        /// </summary>
        /// <param name="sample">The sample to add. Must be a real number.</param>
        public void Add(float sample)
        {
            if (float.IsNaN(sample))
            {
                throw new ArgumentException("Numeric sample cannot be NaN", nameof(sample));
            }

            if (float.IsInfinity(sample))
            {
                throw new ArgumentException("Numeric sample must be a finite number", nameof(sample));
            }

            lock (_samples)
            {
                _samples.Add(sample);
                // OPT could potentially use insertion sort to maintain sorting of the list,
                // but that could also be costly if caller doesn't care about sorting and is adding lots of individual items in a row
                _samplesAreSorted = false;

                // Update the current average
                _currentMean = (sample + _currentMean * (_samples.Count - 1)) / _samples.Count;

                // And invalidate the cached variance
                _cachedVariance = null;
            }
        }

        /// <summary>
        /// Adds a collection of samples to this sample set.
        /// If the input is an array of values, use the array method signature for better performance.
        /// </summary>
        /// <param name="samples">The <see cref="ICollection{T}"/> of samples to add.</param>
        public void AddRange(ICollection<float> samples)
        {
            samples.AssertNonNull(nameof(samples));

            if (samples.Count == 0)
            {
                return;
            }

            // Validate all input values in advance so we're not left in an inconsistent state
            foreach (float sample in samples)
            {
                if (float.IsNaN(sample))
                {
                    throw new ArgumentException("Numeric sample cannot be NaN", nameof(sample));
                }

                if (float.IsInfinity(sample))
                {
                    throw new ArgumentException("Numeric sample must be a finite number", nameof(sample));
                }
            }

            lock (_samples)
            {
                float sumOfAllNewSamples = 0;
                int originalSampleSize = _samples.Count;
                _samples.FastAddRangeCollection(samples);

                // Vectorize calculation of the mean if possible
                ArraySegment<float> samplesAsSegment = default;
#if DEBUG
                if (Vector.IsHardwareAccelerated &&
                    samples.Count >= 128 &&
                    ListHacks.TryGetUnderlyingArraySegment(samples, out samplesAsSegment) &&
                    FastRandom.Shared.NextBool())
#else
                if (Vector.IsHardwareAccelerated &&
                    samples.Count >= 128 &&
                    ListHacks.TryGetUnderlyingArraySegment(samples, out samplesAsSegment))
#endif
                {
                    int index = samplesAsSegment.Offset;
                    int endIndex = samplesAsSegment.Count;
                    int vectorEndIndex = endIndex - samplesAsSegment.Count % Vector<float>.Count;
                    while (index < vectorEndIndex)
                    {
                        sumOfAllNewSamples += Vector.Dot(Vector<float>.One, new Vector<float>(samplesAsSegment.Array, index));
                        index += Vector<float>.Count;
                    }

                    while (index < endIndex)
                    {
                        sumOfAllNewSamples += samplesAsSegment.Array[index++];
                    }
                }
                else
                {
                    foreach (float sample in samples)
                    {
                        sumOfAllNewSamples += sample;
                    }
                }

                // Update the mean all at once
                _currentMean = (sumOfAllNewSamples + _currentMean * originalSampleSize) / _samples.Count;
                _cachedVariance = null;
                _samplesAreSorted = false;
            }
        }

        /// <summary>
        /// Adds a collection of samples to this sample set. The name has to be different because of dumb
        /// incompatibility between ICollection and IReadOnlyCollection.
        /// If the input is an array of values, use the array method signature for better performance.
        /// </summary>
        /// <param name="samples">The <see cref="IReadOnlyCollection{T}"/> of samples to add.</param>
        public void AddRangeReadOnly(IReadOnlyCollection<float> samples)
        {
            samples.AssertNonNull(nameof(samples));

            if (samples.Count == 0)
            {
                return;
            }

            // Validate all input values in advance so we're not left in an inconsistent state
            foreach (float sample in samples)
            {
                if (float.IsNaN(sample))
                {
                    throw new ArgumentException("Numeric sample cannot be NaN", nameof(sample));
                }

                if (float.IsInfinity(sample))
                {
                    throw new ArgumentException("Numeric sample must be a finite number", nameof(sample));
                }
            }

            lock (_samples)
            {
                float sumOfAllNewSamples = 0;
                int originalSampleSize = _samples.Count;
                _samples.FastAddRangeReadOnlyCollection(samples);

                // Vectorize calculation of the mean if possible
                ArraySegment<float> samplesAsSegment = default;
#if DEBUG
                if (Vector.IsHardwareAccelerated &&
                    samples.Count >= 128 &&
                    ListHacks.TryGetUnderlyingArraySegment(samples, out samplesAsSegment) &&
                    FastRandom.Shared.NextBool())
#else
                if (Vector.IsHardwareAccelerated &&
                    samples.Count >= 128 &&
                    ListHacks.TryGetUnderlyingArraySegment(samples, out samplesAsSegment))
#endif
                {
                    int index = samplesAsSegment.Offset;
                    int endIndex = samplesAsSegment.Count;
                    int vectorEndIndex = endIndex - samplesAsSegment.Count % Vector<float>.Count;
                    while (index < vectorEndIndex)
                    {
                        sumOfAllNewSamples += Vector.Dot(Vector<float>.One, new Vector<float>(samplesAsSegment.Array, index));
                        index += Vector<float>.Count;
                    }

                    while (index < endIndex)
                    {
                        sumOfAllNewSamples += samplesAsSegment.Array[index++];
                    }
                }
                else
                {
                    foreach (float sample in samples)
                    {
                        sumOfAllNewSamples += sample;
                    }
                }

                // Update the mean all at once
                _currentMean = (sumOfAllNewSamples + _currentMean * originalSampleSize) / _samples.Count;
                _cachedVariance = null;
                _samplesAreSorted = false;
            }
        }

        /// <summary>
        /// Adds an array segment of samples to this sample set.
        /// </summary>
        /// <param name="array">The input array of samples to add.</param>
        /// <param name="offset">The input array offset.</param>
        /// <param name="count">The numver of samples to add.</param>
        public void AddRange(float[] array, int offset, int count)
        {
            array.AssertNonNull(nameof(array));

            if (offset < 0 || offset >= array.Length)
            {
                throw new IndexOutOfRangeException("Offset must be within the bounds of the array");
            }

            if (count == 0)
            {
                return;
            }

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("Count must be non-negative");
            }

            if (offset + count > array.Length)
            {
                throw new IndexOutOfRangeException("Offset + count exceeds upper bound of the array");
            }

            // Validate all input values in advance so we're not left in an inconsistent state
            int index = offset;
            int endIndex = offset + count;
            int originalSampleSize = _samples.Count;
            while (index < endIndex)
            {
                float sample = array[index++];
                if (float.IsNaN(sample))
                {
                    throw new ArgumentException("Numeric sample cannot be NaN", nameof(sample));
                }

                if (float.IsInfinity(sample))
                {
                    throw new ArgumentException("Numeric sample must be a finite number", nameof(sample));
                }
            }

            // Now commit to the change
            lock (_samples)
            {
#if NET8_0_OR_GREATER
                _samples.AddRange(array.AsSpan(offset, count));
#else
                _samples.FastAddRangeEnumerable(array.Skip(offset).Take(count), count);
#endif

                float sumOfAllNewSamples = 0;
                index = offset;
                endIndex = offset + count;

                // Use SIMD if possible to sum all elements of the input array (can be faster than one-by-one enumeration)
                // 128 elements is approximate size threshold based on benchmarking
#if DEBUG
                if (Vector.IsHardwareAccelerated && count >= 128 && FastRandom.Shared.NextBool())
#else
                if (Vector.IsHardwareAccelerated && count >= 128)
#endif
                {
                    int vectorEndIndex = endIndex - count % Vector<float>.Count;
                    while (index < vectorEndIndex)
                    {
                        sumOfAllNewSamples += Vector.Dot(Vector<float>.One, new Vector<float>(array, index));
                        index += Vector<float>.Count;
                    }
                }

                // Residual loop
                while (index < endIndex)
                {
                    sumOfAllNewSamples += array[index++];
                }

                _currentMean = (sumOfAllNewSamples + _currentMean * originalSampleSize) / _samples.Count;
                _cachedVariance = null;
                _samplesAreSorted = false;
            }
        }

        // This method assumes _samples is locked!
        private float CalculateVarianceInternal()
        {
            float sumVariance = 0;

            // Calculate the variance now, using SIMD if possible
            // 128 elements is approximate size threshold based on benchmarking
            ArraySegment<float> rawArraySegment = default;
#if DEBUG
            if (Vector.IsHardwareAccelerated &&
                _samples.Count >= 128 &&
                ListHacks.TryGetUnderlyingArraySegment(_samples, out rawArraySegment) &&
                FastRandom.Shared.NextBool())
#else
            if (Vector.IsHardwareAccelerated &&
                _samples.Count >= 128 &&
                ListHacks.TryGetUnderlyingArraySegment(_samples, out rawArraySegment))
#endif
            {
                int index = rawArraySegment.Offset;
                int endIndex = rawArraySegment.Count;
                int vectorEndIndex = endIndex - rawArraySegment.Count % Vector<float>.Count;
                Vector<float> meanVec = new Vector<float>(_currentMean);
                while (index < vectorEndIndex)
                {
                    Vector<float> sampleVec = new Vector<float>(rawArraySegment.Array, index);
                    sampleVec = Vector.Subtract(sampleVec, meanVec);
                    // Use dot product as a clever trick to square and sum the entire vector as one operation
                    sumVariance += Vector.Dot(sampleVec, sampleVec);
                    index += Vector<float>.Count;
                }

                // Residual loop
                while (index < endIndex)
                {
                    float sample = rawArraySegment.Array[index++];
                    float delta = sample - _currentMean;
                    sumVariance += delta * delta;
                }
            }
            else
            {
                foreach (float sample in _samples)
                {
                    float delta = sample - _currentMean;
                    sumVariance += delta * delta;
                }
            }

            return sumVariance / _samples.Count;
        }

        /// <summary>
        /// Gets the list of sample values currently in this set.
        /// </summary>
        public IReadOnlyCollection<float> Samples => _samples;

        /// <summary>
        /// Gets the total number of samples in the set.
        /// </summary>
        public int SampleCount
        {
            get
            {
                lock (_samples)
                {
                    return _samples.Count;
                }
            }
        }

        /// <summary>
        /// Gets the current arithmetic mean of the set.
        /// </summary>
        public float Mean
        {
            get
            {
                lock (_samples)
                {
                    return _currentMean;
                }
            }
        }

        /// <summary>
        /// Gets the variance of the set.
        /// </summary>
        public float Variance
        {
            get
            {
                lock (_samples)
                {
                    if (_samples.Count == 0)
                    {
                        return 0;
                    }

                    if (!_cachedVariance.HasValue)
                    {
                        _cachedVariance = CalculateVarianceInternal();
                    }

                    return _cachedVariance.Value;
                }
            }
        }

        /// <summary>
        /// Gets the standard deviation of the set.
        /// </summary>
#if NET5_0_OR_GREATER
        public float StandardDeviation => MathF.Sqrt(Variance);
#else
        public float StandardDeviation => (float)Math.Sqrt(Variance);
#endif

        /// <summary>
        /// Gets the naive median of the set. If the set contains
        /// an even number of values, this method will tend towards
        /// the lower of the two values.
        /// </summary>
        public float Median
        {
            get
            {
                lock (_samples)
                {
                    if (_samples.Count == 0)
                    {
                        return 0;
                    }

                    if (!_samplesAreSorted)
                    {
                        _samples.Sort();
                        _samplesAreSorted = true;
                    }

                    return _samples[(_samples.Count - 1) / 2];
                }
            }
        }

        public int DistinctValueCount
        {
            get
            {
                lock (_samples)
                {
                    if (_samples.Count == 0)
                    {
                        return 0;
                    }

                    if (!_samplesAreSorted)
                    {
                        _samples.Sort();
                        _samplesAreSorted = true;
                    }

                    int returnVal = 1;
                    float current = _samples[0];
                    foreach (float sample in _samples)
                    {
                        if (sample != current)
                        {
                            current = sample;
                            returnVal++;
                        }
                    }

                    return returnVal;
                }
            }
        }

        /// <summary>
        /// Returns a value that would most equally divide this set into
        /// high and low segments. The returned value can be thought of
        /// as a median, though it is not necessarily a member of the set.
        /// This method takes into account multiple identical values near
        /// the naive median to try and find the most even bisection point.
        /// </summary>
        public float BisectionMedian
        {
            get
            {
                lock (_samples)
                {
                    if (_samples.Count == 0)
                    {
                        return 0;
                    }
                    else if (_samples.Count == 1)
                    {
                        return _samples[0];
                    }
                    else if (_samples.Count == 2)
                    {
                        return (_samples[0] + _samples[1]) / 2.0f;
                    }

                    if (!_samplesAreSorted)
                    {
                        _samples.Sort();
                        _samplesAreSorted = true;
                    }

                    bool oddLengthSet = _samples.Count % 2 != 0;
                    int naiveMedianIdx = (_samples.Count - 1) / 2;
                    float naiveMedian = _samples[naiveMedianIdx];
                    int lowIdx = naiveMedianIdx;
                    int highIdx = naiveMedianIdx;

                    // Find the boundaries of duplicate median values near the center of the data set.
                    while (lowIdx > 0 && _samples[lowIdx - 1] == naiveMedian) lowIdx--;
                    while (highIdx < _samples.Count - 1 && _samples[highIdx + 1] == naiveMedian) highIdx++;

                    if (oddLengthSet && lowIdx == highIdx)
                    {
                        // Unique median in the center of the data. Perfect
                        return _samples[lowIdx];
                    }

                    // We're now calculating index deltas in units of 1.0, 1.5, 2.5, etc..
                    // Rather than compare decimal floating point values, use fixed-point Q1
                    // logic so all the indices are whole values.
                    int trueMedianHalfIdx = _samples.Count - 1;
                    int bisectLeftCost = Math.Abs(trueMedianHalfIdx - ((lowIdx << 1) - 1));
                    int bisectRightCost = Math.Abs(trueMedianHalfIdx - ((highIdx << 1) + 1));

                    if (bisectLeftCost < bisectRightCost)
                    {
                        if (lowIdx == 0)
                        {
                            return _samples[0];
                        }
                        else
                        {
                            return (_samples[lowIdx - 1] + _samples[lowIdx]) / 2.0f;
                        }
                    }
                    else
                    {
                        if (highIdx == _samples.Count - 1)
                        {
                            return _samples[highIdx];
                        }
                        else
                        {
                            return (_samples[highIdx] + _samples[highIdx + 1]) / 2.0f;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets the highest value stored in this set,
        /// or float.MinValue if sample count is zero.
        /// </summary>
        public float Maximum
        {
            get
            {
                lock (_samples)
                {
                    if (_samples.Count == 0)
                    {
                        return float.MinValue;
                    }

                    if (!_samplesAreSorted)
                    {
                        _samples.Sort();
                        _samplesAreSorted = true;
                    }

                    return _samples[_samples.Count - 1];
                }
            }
        }

        /// <summary>
        /// Gets the lowest value stored in this set,
        /// or float.MaxValue if sample count is zero.
        /// </summary>
        public float Minimum
        {
            get
            {
                lock (_samples)
                {
                    if (_samples.Count == 0)
                    {
                        return float.MaxValue;
                    }

                    if (!_samplesAreSorted)
                    {
                        _samples.Sort();
                        _samplesAreSorted = true;
                    }

                    return _samples[0];
                }
            }
        }

        /// <summary>
        /// Gets the value at the specified percentile, between 0.0 and 1.0.
        /// So 0.99 would be the 99th percentile, 0.5 would be 50th, etc.
        /// See also <seealso cref="MovingPercentile"/> which is more efficient for
        /// sets where you need to fetch this value constantly.
        /// </summary>
        /// <param name="perc">The percentile value to look up, from 0 to 1.</param>
        /// <returns>The requested percentile, or 0 if no values are in the set.</returns>
        public float Percentile(float perc)
        {
            if (float.IsNaN(perc) || float.IsInfinity(perc))
            {
                throw new ArgumentOutOfRangeException("Percentile must be a real number");
            }

            if (perc < 0.0)
            {
                perc = 0.0f;
            }
            else if (perc > 1.0)
            {
                perc = 1.0f;
            }

            lock (_samples)
            {
                if (_samples.Count == 0)
                {
                    return 0;
                }

                if (!_samplesAreSorted)
                {
                    _samples.Sort();
                    _samplesAreSorted = true;
                }

                return _samples[Math.Min(_samples.Count - 1, Math.Max(0, (int)Math.Round(_samples.Count * perc)))];
            }
        }

        public float PercentileLerp(float perc)
        {
            if (float.IsNaN(perc) || float.IsInfinity(perc))
            {
                throw new ArgumentOutOfRangeException("Percentile must be a real number");
            }

            if (perc < 0.0)
            {
                perc = Minimum;
            }
            else if (perc > 1.0)
            {
                perc = Maximum;
            }

            lock (_samples)
            {
                if (_samples.Count == 0)
                {
                    return 0;
                }
                else if (_samples.Count == 1)
                {
                    return _samples[0];
                }

                if (!_samplesAreSorted)
                {
                    _samples.Sort();
                    _samplesAreSorted = true;
                }

                float x = _samples.Count * perc;
#if NET5_0_OR_GREATER
                float blend = x - MathF.Floor(x);
                int lowIdx = Math.Min(_samples.Count - 2, Math.Max(0, (int)MathF.Floor(x)));
#else
                float blend = x - (float)Math.Floor(x);
                int lowIdx = Math.Min(_samples.Count - 2, Math.Max(0, (int)Math.Floor(x)));
#endif
                float low = _samples[lowIdx];
                float high = _samples[lowIdx + 1];
                return Lerp(low, high, blend);
            }
        }

        /// <summary>
        /// Given a data point, return the percentile that this sample would fall within the data, from 0 to 1.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public float ReversePercentile(float value)
        {
            lock (_samples)
            {
                if (_samples.Count == 0)
                {
                    return float.MaxValue;
                }

                if (!_samplesAreSorted)
                {
                    _samples.Sort();
                    _samplesAreSorted = true;
                }

                int idx;
                for (idx = 0; idx < _samples.Count && _samples[idx] <= value; idx++) ;

                if (idx == _samples.Count)
                {
                    return 1.0f;
                }
                else
                {
                    return (float)idx / _samples.Count;
                }
            }
        }

        private static float Lerp(float start, float end, float ratio)
        {
            if (ratio <= 0)
            {
                return start;
            }
            else if (ratio >= 1)
            {
                return end;
            }
            else
            {
                return end * ratio + start * (1 - ratio);
            }
        }
    }
}
