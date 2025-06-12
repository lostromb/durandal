using System;

namespace Durandal.Common.MathExt
{
    /// <summary>
    /// A class that stores data points in a fixed-size array.
    /// Its purpose is to accept incremental inputs and calculate the effect of the
    /// addition on the average of the total set. This is used for when you want the
    /// "Average of the most recent ## data values", etc.
    /// <br/> Initial Configuration:
    /// <br/> dp-&gt;
    /// <br/> [0][0][0][0][0][0] Average = 0
    /// <br/>
    /// <br/> After 1 step:
    /// <br/> ___dp-&gt;
    /// <br/> [6][0][0][0][0][0] Average = 1
    /// <br/>
    /// <br/> After awhile:
    /// <br/> dp-&gt; (Datapointer loops to the beginning when it hits the end of the array)
    /// <br/> [5][7][8][6][5][4] Average = 5.8333
    /// </summary>
    public class MovingAverage
    {
        /// <summary>
        /// The array that contains the data
        /// </summary>
        private double[] _data;

        /// <summary>
        /// Always points to the oldest data item in the set (the tail)
        /// </summary>
        private int _dataPointer;

        /// <summary>
        /// The current average
        /// </summary>
        private double _curAverage;

        /// <summary>
        /// Constructs the class and initializes the array. This array size cannot be
        /// changed later.
        /// </summary>
        /// <param name="dataSize"></param>
        /// <param name="initialValues"></param>
        public MovingAverage(int dataSize, double initialValues)
        {
            if (dataSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(dataSize));
            }

            _data = new double[dataSize];
            _dataPointer = 0;

            // Fill the array with an initial value
            for (int c = 0; c < dataSize; c++)
            {
                _data[c] = initialValues;
            }

            _curAverage = initialValues;
        }

        /// <summary>
        /// Overwrites the oldest value in the average list with the new value
        /// </summary>
        /// <param name="value"></param>
        public void Add(double value)
        {
            if (double.IsInfinity(value) || double.IsNaN(value))
            {
                return;
            }

            lock (this)
            {
                // Add the value to the dataset, overwriting older data
                double oldValue = _data[_dataPointer];
                _data[_dataPointer] = value;
                _dataPointer++;
                if (_dataPointer >= _data.Length)
                {
                    // Loop datapointer to the beginning of the array if necessary
                    _dataPointer = 0;
                }

                // Update the current average
                _curAverage += (value / (double)_data.Length) - (oldValue / (double)_data.Length);

                // If curAverage is very close to zero due to rounding errors, round to zero
                if (_curAverage < 0.000001 && _curAverage > -0.000001)
                {
                    _curAverage = 0;
                }
            }
        }

        /// <summary>
        /// Gets or sets the arithmetic mean of all the values in the dataset.
        /// </summary>
        public double Average
        {
            get
            {
                return _curAverage;
            }
            set
            {
                lock (this)
                {
                    for (int c = 0; c < _data.Length; c++)
                    {
                        _data[c] = value;
                    }

                    _curAverage = value;
                }
            }
        }

        public override string ToString()
        {
            return Average.ToString();
        }
    }
}
