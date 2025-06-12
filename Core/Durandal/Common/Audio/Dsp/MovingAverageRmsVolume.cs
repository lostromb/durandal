using Durandal.Common.MathExt;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Audio
{
    /// <summary>
    /// Used for calculating the average RMS volume of an audio signal over time, for one channel.
    /// </summary>
    public class MovingAverageRmsVolume
    {
        /// <summary>
        /// The array that contains the data. Each value is SQUARED from the public-facing input.
        /// This means all values are also non-negative so you can skip Abs() calls.
        /// </summary>
        private float[] _data;

        /// <summary>
        /// Always points to the oldest data item in the set (the tail)
        /// </summary>
        private int _dataPointer;

        /// <summary>
        /// The current average
        /// </summary>
        private float _curAverage;

        /// <summary>
        /// The value of the current peak
        /// </summary>
        private float _curPeak;

        /// <summary>
        /// The index of the current peak
        /// </summary>
        private float _curPeakIdx;

        /// <summary>
        /// Creates a moving average RMS volume tracker for a single channel of audio
        /// </summary>
        /// <param name="samplesToAverage"></param>
        /// <param name="initialValues"></param>
        public MovingAverageRmsVolume(int samplesToAverage, float initialValues)
        {
            if (samplesToAverage <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(samplesToAverage));
            }

            initialValues = initialValues * initialValues;
            _data = new float[samplesToAverage];
            _dataPointer = 0;
            _curPeakIdx = 0;
            _curPeak = initialValues;

            // Fill the array with an initial value
            for (int c = 0; c < samplesToAverage; c++)
            {
                _data[c] = initialValues;
            }

            _curAverage = initialValues;
        }

        /// <summary>
        /// Adds a new sample value to this average. DO NOT square it beforehand.
        /// </summary>
        /// <param name="sampleValue">The unmodified sample value.</param>
        public void Add(float sampleValue)
        {
            sampleValue = sampleValue * sampleValue;
            // Add the value to the dataset, overwriting older data
            float oldValue = _data[_dataPointer];
            _data[_dataPointer] = sampleValue;

            // Update current peak here before we update datapointer
            if (sampleValue > _curPeak)
            {
                // Incoming value becomes the new peak
                _curPeak = sampleValue;
                _curPeakIdx = _dataPointer;
            }
            else if (_dataPointer == _curPeakIdx)
            {
                // Old peak is rolling off - we have to search the whole dataset to find the next highest value
                _curPeak = 0;
                _curPeakIdx = _dataPointer;
                for (int c = 0; c < _data.Length; c++)
                {
                    if (_data[c] > _curPeak)
                    {
                        _curPeak = _data[c];
                        _curPeakIdx = c;
                    }
                }
            }

            _dataPointer++;
            if (_dataPointer >= _data.Length)
            {
                // Loop datapointer to the beginning of the array if necessary
                _dataPointer = 0;
            }

            // Update the current average
            _curAverage += (sampleValue / (float)_data.Length) - (oldValue / (float)_data.Length);
        }

        /// <summary>
        /// Gets the moving average root-mean-square of the input set.
        /// </summary>
        public float RmsVolume
        {
            get
            {
                if (_curAverage <= 0.0f)
                {
                    // catch NaNs caused by numeric instability
                    return 0;
                }

                return (float)Math.Sqrt(_curAverage);
            }
            set
            {
                float tgt = value * value;
                _curAverage = tgt;
                for (int c = 0; c < _data.Length; c++)
                {
                    _data[c] = tgt;
                }

                // update peak value here also
                _curPeak = tgt;
                _curPeakIdx = _dataPointer - 1;
                if (_curPeakIdx < 0)
                {
                    _curPeakIdx += _data.Length;
                }
            }
        }

        /// <summary>
        /// Gets the highest single peak sample value from the input set
        /// </summary>
        public float PeakVolume => (float)Math.Sqrt(_curPeak);

        public override string ToString()
        {
            return RmsVolume.ToString();
        }
    }
}
