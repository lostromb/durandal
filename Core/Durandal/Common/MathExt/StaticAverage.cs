using System.Threading;

namespace Durandal.Common.MathExt
{
    /// <summary>
    /// A class for calculating the average value of a growing set of values.
    /// </summary>
    public class StaticAverage
    {
        /// <summary>
        /// The current average value.
        /// </summary>
        private double _curAverage;

        /// <summary>
        /// The number of observations.
        /// </summary>
        private double _dataSize;

        /// <summary>
        /// Creates a new instance of the StaticAverage class.
        /// </summary>
        public StaticAverage()
        {
            Reset();
        }

        /// <summary>
        /// Adds a new value to the set to be averaged.
        /// </summary>
        /// <param name="value"></param>
        public void Add(double value)
        {
            _curAverage = ((_curAverage * _dataSize) + value) / (_dataSize + 1);
            _dataSize += 1;
        }

        /// <summary>
        /// Returns the arithmetic mean of all the values in the dataset.
        /// </summary>
        public double Average => _curAverage;

        /// <summary>
        /// Returns the count of values that are being averaged.
        /// </summary>
        public int NumItems => (int)_dataSize;

        /// <summary>
        /// Resets this average to zero and forgets all observations.
        /// </summary>
        public void Reset()
        {
            _curAverage = 0;
            _dataSize = 0;
        }

        public override string ToString()
        {
            return Average.ToString();
        }
    }
}