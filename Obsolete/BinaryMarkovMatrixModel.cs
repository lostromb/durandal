using Durandal.Common.Logger;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Utils.MathExt;

namespace Durandal.Common.Utils
{
    public class BinaryMarkovMatrixModel
    {
        private double[][][] _likelihoods;
        private double[][][] _counts;
        private int _length;
        private int _width;
        private double _trainedThreshold = 0;
        private bool _isTrained = false;
        
        public BinaryMarkovMatrixModel(int length, int width)
        {
            _length = length - 1;
            _width = width;
            _likelihoods = new double[_length][][];
            _counts = new double[_length][][];
            for (int x = 0; x < _length; x++)
            {
                _likelihoods[x] = new double[_width][];
                _counts[x] = new double[_width][];
                for (int y = 0; y < _width; y++)
                {
                    _likelihoods[x][y] = new double[_width];
                    _counts[x][y] = new double[_width];
                }
            }
        }

        /// <summary>
        /// Read a serialized model from a file
        /// </summary>
        /// <param name="fileName"></param>
        public BinaryMarkovMatrixModel(string fileName)
        {
            using (FileStream fileIn = new FileStream(fileName, FileMode.Open))
            {
                this.LoadModelFromStream(fileIn);
                fileIn.Close();
            }
        }

        /// <summary>
        /// Read a serialized model from a stream
        /// </summary>
        /// <param name="fileName"></param>
        public BinaryMarkovMatrixModel(Stream inputStream)
        {
            this.LoadModelFromStream(inputStream);
        }

        private void LoadModelFromStream(Stream inputStream)
        {
            using (StreamReader reader = new StreamReader(inputStream))
            {
                _length = int.Parse(reader.ReadLine()) - 1;
                _width = int.Parse(reader.ReadLine());

                _likelihoods = new double[_length][][];
                _counts = new double[_length][][];
                for (int x = 0; x < _length; x++)
                {
                    _likelihoods[x] = new double[_width][];
                    _counts[x] = new double[_width][];
                    for (int y = 0; y < _width; y++)
                    {
                        _likelihoods[x][y] = new double[_width];
                        _counts[x][y] = new double[_width];
                    }
                }

                _trainedThreshold = double.Parse(reader.ReadLine());
                for (int x = 0; x < _length; x++)
                {
                    for (int y = 0; y < _width; y++)
                    {
                        for (int z = 0; z < _width; z++)
                        {
                            _likelihoods[x][y][z] = double.Parse(reader.ReadLine());
                            _counts[x][y][z] = double.Parse(reader.ReadLine());
                        }
                    }
                }
                reader.Close();
            }
            _isTrained = true;
        }

        private int BucketFunc(double input)
        {
            return System.Math.Min(_width, (int)System.Math.Floor(input * _width));
        }

        public void Train(ILogger logger, IEnumerable<double[]> positiveVectors, IEnumerable<double[]> negativeVectors, IEnumerable<double[]> validationVectors = null)
        {
            logger.Log("Training Markov matrix...");
            
            foreach (double[] vector in negativeVectors)
            {
                AddTraining(vector, false);
            }

            foreach (double[] vector in positiveVectors)
            {
                AddTraining(vector, true);
            }

            if (validationVectors == null)
            {
                logger.Log("No validation data! Model accuracy will probably be lower than expected");
                CalculateThreshold(logger, negativeVectors, positiveVectors);
            }
            else
            {
                CalculateThreshold(logger, negativeVectors, validationVectors);
            }

            _isTrained = true;
        }

        private void CalculateThreshold(ILogger logger, IEnumerable<double[]> negativeVectors, IEnumerable<double[]> validationVectors)
        {
            // Now evaluate
            StaticAverage negativeScore = new StaticAverage();
            StaticAverage positiveScore = new StaticAverage();
            IList<double> positives = new List<double>();
            IList<double> negatives = new List<double>();
            double highestNegative = 0;

            foreach (double[] vector in negativeVectors)
            {
                double score = EvaluateInternal(vector);
                negativeScore.Add(score);
                negatives.Add(score);
                highestNegative = System.Math.Max(highestNegative, score);
            }

            foreach (double[] vector in validationVectors)
            {
                double score = EvaluateInternal(vector);
                positiveScore.Add(score);
                positives.Add(score);
            }

            StaticAverage variance = new StaticAverage();
            foreach (double vector in negatives)
            {
                double dev = vector - negativeScore.Average;
                variance.Add(dev * dev);
            }

            logger.Log("Average negative = " + negativeScore.Average, LogLevel.Std);
            logger.Log("Highest negative = " + highestNegative, LogLevel.Std);
            logger.Log("Negative variance = " + variance.Average, LogLevel.Std);
            logger.Log("Negative deviation = " + System.Math.Sqrt(variance.Average), LogLevel.Std);
            logger.Log("Optimizing thresholds...");

            double bestThresh = 0;
            double bestScore = 0;
            for (double thresh = 0; thresh < positiveScore.Average; thresh += positiveScore.Average / 1000)
            {
                double negNeg = 0;
                double negPos = 0;
                double posPos = 0;
                double posNeg = 0;
                foreach (double score in negatives)
                {
                    if (score > thresh)
                        negPos++;
                    else
                        negNeg++;
                }
                foreach (double score in positives)
                {
                    if (score > thresh)
                        posPos++;
                    else
                        posNeg++;
                }

                // Calculate precision and recall
                double recall = posPos / positives.Count;
                double precision = (posPos + negNeg) / (positives.Count + negatives.Count);
                double f1 = 2 * precision * recall / (precision + recall);
                if (f1 > bestScore)
                {
                    bestThresh = thresh;
                    bestScore = f1;
                    logger.Log("Thresh " + thresh + " Prec " + precision + " Rec " + recall + " F1 " + f1, LogLevel.Std);
                }
            }

            _trainedThreshold = bestThresh;
            logger.Log("Ideal threshold is " + bestThresh + "; model F1 score is " + bestScore);
            logger.Log("Model is trained");

            // Since we just really hate type-1 errors, add an extra standard deviation to that threshold
            _trainedThreshold = _trainedThreshold + System.Math.Sqrt(variance.Average);
        }

        private void AddTraining(double[] vector, bool positive)
        {
            if (vector.Length != _length + 1)
                throw new ArgumentException("Input vector does not match markov model length");

            int current = BucketFunc(vector[0]);
            for (int c = 0; c < _length; c++)
            {
                int dest = BucketFunc(vector[c + 1]);
                _counts[c][current][dest] += 1;
                if (positive)
                {
                    _likelihoods[c][current][dest] += 1;
                }
                current = dest;
            }
        }

        private double EvaluateInternal(double[] vector)
        {
            if (vector == null)
                throw new ArgumentNullException("Markov model input vector cannot be null");
            if (vector.Length != _length + 1)
                throw new ArgumentException("Input vector does not match markov model length");

            double returnVal = 0.0;
            int current = BucketFunc(vector[0]);
            for (int c = 0; c < _length; c++)
            {
                int dest = BucketFunc(vector[c + 1]);
                if (_counts[c][current][dest] > 0)
                {
                    returnVal += (_likelihoods[c][current][dest] / _counts[c][current][dest]);
                }
                current = dest;
            }

            return returnVal;
        }

        public bool Evaluate(double[] vector)
        {
            if (!_isTrained)
                throw new InvalidOperationException("Markov model is not trained yet!");
            return EvaluateInternal(vector) > _trainedThreshold;
        }

        public bool SaveToFile(string fileName)
        {
            using (StreamWriter writer = new StreamWriter(fileName))
            {
                writer.WriteLine(_length + 1);
                writer.WriteLine(_width);
                writer.WriteLine(_trainedThreshold);
                for (int x = 0; x < _length; x++)
                {
                    for (int y = 0; y < _width; y++)
                    {
                        for (int z = 0; z < _width; z++)
                        {
                            writer.WriteLine(_likelihoods[x][y][z]);
                            writer.WriteLine(_counts[x][y][z]);
                        }
                    }
                }
                writer.Close();
            }
            return true;
        }
    }
}
