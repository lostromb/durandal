using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.MathExt
{
    /// <summary>
    /// A class for capturing a 1-dimensional set of values and then plotting them as a plain text histogram (ASCII art).
    /// Helpful in providing a simple visualization of numerical set distributions or rates of a particular event over time.
    /// </summary>
    public class TinyHistogram
    {
        // We don't use the space char as the first value in this array, the reason being that
        // we want to make sure that any non-zero value shows up as a blip. So anyone who
        // references these char arrays will always get a partially rendered char on output
        // There's special logic to handle zero values in the histogram renderer itself
        private static readonly char[] UNICODE_BAR_CHART = new char[]
        {
            //' ',
            '▁',
            '▂',
            '▃',
            '▄',
            '▅',
            '▆',
            '▇',
            '█'
        };

        private static readonly char[] IBM437_BAR_CHART = new char[]
        {
            //' ',
            '░',
            '▒',
            '▓',
            '█'
        };

        private readonly List<HistogramObservation> _values = new List<HistogramObservation>();

        public void AddValue(double value, double weight = 1)
        {
            _values.Add(new HistogramObservation(value, weight));
        }

        public void Reset()
        {
            _values.Clear();
        }

        /// <summary>
        /// Renders this histogram as a single line ASCII art, in a form like "0.00  |XXXxx   xx xXx  xX   | 13.25"
        /// </summary>
        /// <param name="charWidth">The width of the histogram area, in characters (will be equal to the bin count of the graph)</param>
        /// <param name="unicode">If true, use unicode bar graph symbols. If false, use DOS-style gradient blocks.</param>
        /// <returns>The rendered histogram</returns>
        public string RenderAsOneLine(int charWidth, bool unicode = true)
        {
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                RenderAsOneLine(charWidth, unicode, pooledSb.Builder);
                return pooledSb.Builder.ToString();
            }
        }

        /// <summary>
        /// Renders this histogram as a single line ASCII art, in a form like "0.00  |XXXxx   xx xXx  xX   | 13.25"
        /// </summary>
        /// <param name="charWidth">The width of the histogram area, in characters (will be equal to the bin count of the graph)</param>
        /// <param name="unicode">If true, use unicode bar graph symbols. If false, use DOS-style gradient blocks.</param>
        /// <param name="builder">The string builder to append the line to.</param>
        /// <returns>The rendered histogram</returns>
        public void RenderAsOneLine(int charWidth, bool unicode, StringBuilder builder)
        {
            if (charWidth < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(charWidth));
            }

            char[] renderCharSet = unicode ? UNICODE_BAR_CHART : IBM437_BAR_CHART;
            int numBucketChars = renderCharSet.Length;
            double rangeBegin;
            double rangeEnd;
            double bucketWidth;
            double[] buckets = Bucketize(charWidth, out rangeBegin, out rangeEnd, out bucketWidth);
            int precisionDigits = GetPrecisionDigitsNeeded(bucketWidth);
            builder.AppendFormat("{0:F" + precisionDigits + "}   |", rangeBegin);
            foreach (double bucketValue in buckets)
            {
                if (bucketValue == 0)
                {
                    builder.Append(' ');
                }
                else
                {
                    int charValue = FastMath.Max(0, FastMath.Min(numBucketChars - 1, (int)Math.Floor(bucketValue * numBucketChars)));
                    builder.Append(renderCharSet[charValue]);
                }
            }

            builder.AppendFormat("|   {0:F" + precisionDigits + "}", rangeEnd);
        }

        /// <summary>
        /// Renders this histogram as a set of lines, where each output line is a bin of the histogram.
        /// </summary>
        /// <param name="numLines">The number of lines (bins) to output</param>
        /// <param name="charWidth">The horizontal width of each bar, in characters</param>
        /// <returns>A set of strings representing the lines of the graph</returns>
        public string[] RenderAsMultiLine(int numLines, int charWidth)
        {
            if (numLines < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(numLines));
            }
            if (charWidth < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(charWidth));
            }

            string[] returnVal = new string[numLines];

            char[] renderCharSet = IBM437_BAR_CHART;
            int numBucketChars = renderCharSet.Length;

            double rangeBegin;
            double rangeEnd;
            double bucketWidth;
            double[] buckets = Bucketize(numLines, out rangeBegin, out rangeEnd, out bucketWidth);
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                double bucketStart = rangeBegin;
                double bucketEnd = rangeBegin + bucketWidth;
                int precisionDigits = GetPrecisionDigitsNeeded(bucketWidth);
                string formatStringWithDigitPrecision = "{0,8:F" + precisionDigits + "} -> {1,-8:F" + precisionDigits + "} |";
                for (int line = 0; line < numLines; line++)
                {
                    pooledSb.Builder.AppendFormat(formatStringWithDigitPrecision, bucketStart, bucketEnd);
                    double barWidthAbsolute = buckets[line] * charWidth;
                    int barWidthInteger = (int)Math.Floor(barWidthAbsolute);
                    double barWidthRemainder = barWidthAbsolute - barWidthInteger;
                    pooledSb.Builder.Append(renderCharSet[numBucketChars - 1], barWidthInteger); // whole bar segments
                    int fractionalChar = FastMath.Max(0, FastMath.Min(numBucketChars - 1, (int)Math.Floor(barWidthRemainder * numBucketChars)));
                    pooledSb.Builder.Append(renderCharSet[fractionalChar]); // fractional bar segment
                    returnVal[line] = pooledSb.Builder.ToString();
                    pooledSb.Builder.Clear();
                    bucketStart = bucketEnd;
                    bucketEnd += bucketWidth;
                }
            }

            return returnVal;
        }

        private static int GetPrecisionDigitsNeeded(double bucketWidth)
        {
            int returnVal = 2;
            while (bucketWidth != 0 && bucketWidth < 0.01 && returnVal < 8)
            {
                bucketWidth *= 10;
                returnVal += 1;
            }

            return returnVal;
        }

        private double[] Bucketize(int numBuckets, out double rangeBegin, out double rangeEnd, out double bucketWidth)
        {
            if (numBuckets < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(numBuckets));
            }

            double[] returnVal = new double[numBuckets];

            // Catch empty set
            if (_values.Count == 0)
            {
                rangeBegin = 0;
                rangeEnd = numBuckets;
                bucketWidth = 1;
                return returnVal;
            }

            // Find the min/max values in the set
            rangeBegin = double.MaxValue;
            rangeEnd = double.MinValue;
            foreach (HistogramObservation observation in _values)
            {
                if (observation.Value < rangeBegin)
                {
                    rangeBegin = observation.Value;
                }

                if (observation.Value > rangeEnd)
                {
                    rangeEnd = observation.Value;
                }
            }

            // Calculate the width of each bucket
            bucketWidth = (rangeEnd - rangeBegin) / (double)numBuckets;

            // And then sort each observation
            foreach (HistogramObservation observation in _values)
            {
                int bucket = FastMath.Max(0, FastMath.Min(numBuckets - 1, (int)Math.Floor((observation.Value - rangeBegin) / bucketWidth)));
                returnVal[bucket] += observation.Weight;
            }

            // Then normalize each bucket
            double largestBucket = 0;
            foreach (double bucket in returnVal)
            {
                if (bucket > largestBucket)
                {
                    largestBucket = bucket;
                }
            }

            for (int bucketIdx = 0; bucketIdx < numBuckets; bucketIdx++)
            {
                returnVal[bucketIdx] /= largestBucket;
            }

            return returnVal;
        }

        /// <summary>
        /// The data type used to represent weighted observations
        /// </summary>
        private struct HistogramObservation
        {
            /// <summary>
            /// The "x" value of the observation (which bin it falls into)
            /// </summary>
            public double Value;

            /// <summary>
            /// The "y" value of the observation (how much it fills a particular bin)
            /// </summary>
            public double Weight;

            public HistogramObservation(double value, double weight)
            {
                Value = value;
                Weight = weight;
            }
        }
    }
}
