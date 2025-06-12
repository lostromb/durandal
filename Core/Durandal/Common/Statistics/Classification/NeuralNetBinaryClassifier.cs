using Durandal.Common.Statistics.SharpEntropy;
using Durandal.Common.NLP.Feature;

namespace Durandal.Common.Statistics.Classification
{
    using System;
    using System.Diagnostics;
    using System.IO;

    using Durandal.Common.Config;
    using Durandal.Common.Logger;

    using Durandal.Common.NLP.Train;

    using Durandal.Common.Utils;
    using Durandal.Common.File;
    using Durandal.Common.Collections.Indexing;
    using System.Collections.Generic;
    using Durandal.Common.MathExt;

    /// <summary>
    /// Don't use this; it's terrible
    /// </summary>
    public class NeuralNetBinaryClassifier
    {
        private const int INPUTS_PER_NEURON = 10;
        private const float NEURON_DENSITY = 0.01f;
        private readonly ILogger _logger;
        private readonly ICompactIndex<string> _stringIndex;
        private readonly IFileSystem _fileSystem;
        private readonly string _modelName;

        private IList<Neuron> _neurons;

        public NeuralNetBinaryClassifier(ILogger logger, IFileSystem fileSystem, ICompactIndex<string> stringIndex, string modelName)
        {
            this._logger = logger;
            this._stringIndex = stringIndex;
            this._fileSystem = fileSystem;
            this._modelName = modelName;
        }

        public static bool IsTrainingRequired(VirtualPath cachedWeightFile, IFileSystem fileSystem, IConfiguration modelConfig)
        {
            return true;
        }

        public void TrainFromData(
            TrainingDataList<DomainIntentContextFeature> positiveEvents,
            TrainingDataList<DomainIntentContextFeature> negativeEvents,
            VirtualPath cachedWeightFile,
            ITrainingEventReader eventReader,
            IConfiguration modelConfig)
        {
            Stopwatch timer = new Stopwatch();
            timer.Start();

            if (!IsTrainingRequired(cachedWeightFile, _fileSystem, modelConfig))
            {
                // Load the cache if possible
                this.LoadCache(cachedWeightFile);
                timer.Stop();
            }
            {
                // Compile the list of all input features
                ISet<string> uniqueInputFeatures = ExtractFeatureNameSet(positiveEvents, negativeEvents);
                List<string> featureNameList = new List<string>(uniqueInputFeatures);
                int numNeurons = Math.Max(1, (int)((float)uniqueInputFeatures.Count * NEURON_DENSITY));
                Compact<string>[] outcomes = new Compact<string>[2];
                outcomes[0] = _stringIndex.Store("0");
                outcomes[1] = _stringIndex.Store("1");
                IRandom rand = new FastRandom(99);
                _neurons = new List<Neuron>();

                for (int c = 0; c < numNeurons; c++)
                {
                    ISet<string> inputs = GetUniqueFeatureSet(INPUTS_PER_NEURON, rand, featureNameList);
                    ISet<Compact<string>> compactInputs = new HashSet<Compact<string>>();
                    foreach (string x in inputs)
                    {
                        compactInputs.Add(_stringIndex.Store(x));
                    }
                    Neuron newNeuron = new Neuron(compactInputs, outcomes);
                    _neurons.Add(newNeuron);
                }

                // Training loop
                foreach (DomainIntentContextFeature positive in positiveEvents.TrainingData)
                {
                    ISet<string> featureSet = new HashSet<string>(positive.Context);
                    ISet<Compact<string>> compactFeatures = new HashSet<Compact<string>>();
                    foreach (string x in featureSet)
                    {
                        compactFeatures.Add(_stringIndex.Store(x));
                    }
                    foreach (Neuron n in _neurons)
                    {
                        n.Train(compactFeatures, outcomes[1]);
                    }
                }
                foreach (DomainIntentContextFeature negative in negativeEvents.TrainingData)
                {
                    ISet<string> featureSet = new HashSet<string>(negative.Context);
                    ISet<Compact<string>> compactFeatures = new HashSet<Compact<string>>();
                    foreach (string x in featureSet)
                    {
                        compactFeatures.Add(_stringIndex.Store(x));
                    }
                    foreach (Neuron n in _neurons)
                    {
                        n.Train(compactFeatures, outcomes[0]);
                    }
                }
                foreach (Neuron n in _neurons)
                {
                    n.FinishTraining();
                }

                timer.Stop();
                this._logger.Log("Model trained in " + timer.ElapsedMilliseconds + " ms.", LogLevel.Std);
                WriteCache(cachedWeightFile);
            }

        }

        private ISet<string> GetUniqueFeatureSet(int count, IRandom rand, IList<string> featureSet)
        {
            ISet<string> returnVal = new HashSet<string>();
            while (returnVal.Count < Math.Min(count, featureSet.Count))
            {
                string candidate = featureSet[rand.NextInt(0, featureSet.Count)];
                if (!returnVal.Contains(candidate))
                    returnVal.Add(candidate);
            }
            return returnVal;
        }

        private ISet<string> ExtractFeatureNameSet(TrainingDataList<DomainIntentContextFeature> positiveEvents,
            TrainingDataList<DomainIntentContextFeature> negativeEvents)
        {
            ISet<string> returnVal = new HashSet<string>();
            foreach (DomainIntentContextFeature positive in positiveEvents.TrainingData)
            {
                foreach (string feature in positive.Context)
                {
                    if (!returnVal.Contains(feature))
                    {
                        returnVal.Add(feature);
                    }
                }
            }
            foreach (DomainIntentContextFeature negative in negativeEvents.TrainingData)
            {
                foreach (string feature in negative.Context)
                {
                    if (!returnVal.Contains(feature))
                    {
                        returnVal.Add(feature);
                    }
                }
            }
            return returnVal;
        }

        public long GetMemoryUse()
        {
            return 0;
        }

        private void WriteCache(VirtualPath cacheFileName)
        {
            
        }

        private void LoadCache(VirtualPath cacheFileName)
        {
            
        }

        private static float Sigmoid(float val)
        {
            return 1.0f - (1.0f / (1.0f + (float)Math.Exp(((val * 15.0f) - 7.5f))));
        }

        /// <summary>
        /// Returns the probability that the outcome is TRUE
        /// </summary>
        /// <param name="featureSet"></param>
        /// <returns></returns>
        public float Classify(string[] featureSet)
        {
            ISet<Compact<string>> compactFeatures = new HashSet<Compact<string>>();
            foreach (string x in featureSet)
            {
                if (_stringIndex.Contains(x))
                {
                    compactFeatures.Add(_stringIndex.GetIndex(x));
                }
            }
            float sumScore = 0;
            float triggeredNeurons = 0f;
            foreach (Neuron x in _neurons)
            {
                float[] result = x.Evaluate(compactFeatures);
                if (result == null)
                    continue;
                int outcomeIndex = x.GetOutcomeIndex(_stringIndex.GetIndex("1"));
                if (outcomeIndex >= 0)
                {
                    sumScore += result[outcomeIndex];
                    triggeredNeurons += 1;
                }
            }
            if (triggeredNeurons > 0)
            {
                return Sigmoid(sumScore / triggeredNeurons);
            }
            return 0f;
        }

        private class Neuron
        {
            private Compact<string>[] _inputFeatures;
            private Compact<string>[] _outcomes;
            private IDictionary<uint, float[]> _modelProbabilities = new Dictionary<uint, float[]>();

            // temporary training values
            private IDictionary<Compact<string>, int> _tempOutcomes = new Dictionary<Compact<string>, int>();
            private IDictionary<Compact<string>, int> _tempInputFeatures = new Dictionary<Compact<string>, int>();
            private IDictionary<uint, int[]> _tempModelCounts = new Dictionary<uint, int[]>();


            public Neuron(IEnumerable<Compact<string>> inputFeatures, IEnumerable<Compact<string>> outcomes)
            {
                int c = 0;
                foreach (Compact<string> feature in inputFeatures)
                {
                    _tempInputFeatures.Add(feature, c++);
                }
                _inputFeatures = new Compact<string>[_tempInputFeatures.Count];
                foreach (var feature in _tempInputFeatures)
                {
                    _inputFeatures[feature.Value] = feature.Key;
                }
                c = 0;
                foreach (Compact<string> outcome in outcomes)
                {
                    _tempOutcomes.Add(outcome, c++);
                }
                _outcomes = new Compact<string>[_tempOutcomes.Count];
                foreach (var feature in _tempOutcomes)
                {
                    _outcomes[feature.Value] = feature.Key;
                }
            }

            public int InputSize
            {
                get
                {
                    return _inputFeatures.Length;
                }
            }

            public int OutcomeSize
            {
                get
                {
                    return _outcomes.Length;
                }
            }

            public void Train(ISet<Compact<string>> inputFeatures, Compact<string> outcome)
            {
                // Find out what triggered
                uint outcomeCode = 0;
                uint iter = 0x1;
                for (int c = 0; c < InputSize; c++)
                {
                    if (inputFeatures.Contains(_inputFeatures[c]))
                    {
                        outcomeCode += iter;
                    }
                    iter = iter << 1;
                }

                // Calculate the outcome
                if (!_tempModelCounts.ContainsKey(outcomeCode))
                {
                    _tempModelCounts.Add(outcomeCode, new int[OutcomeSize]);
                }

                // Increment this outcome
                _tempModelCounts[outcomeCode][_tempOutcomes[outcome]] += 1;
            }

            public void FinishTraining()
            {
                foreach (var x in _tempModelCounts)
                {
                    float total = 0;
                    foreach (int y in x.Value)
                    {
                        total += y;
                    }
                    _modelProbabilities[x.Key] = new float[OutcomeSize];
                    for (int c = 0; c < OutcomeSize; c++)
                    {
                        _modelProbabilities[x.Key][c] = (float)x.Value[c] / total;
                    }
                }

                _tempModelCounts = null;
                _tempOutcomes = null;
                _tempInputFeatures = null;
            }

            public float[] Evaluate(ISet<Compact<string>> features)
            {
                // Find out what triggered
                uint outcomeCode = 0;
                uint iter = 0x1;
                for (int c = 0; c < InputSize; c++)
                {
                    if (features.Contains(_inputFeatures[c]))
                    {
                        outcomeCode += iter;
                    }
                    iter = iter << 1;
                }

                if (!_modelProbabilities.ContainsKey(outcomeCode))
                {
                    return null;
                }
                else
                {
                    return _modelProbabilities[outcomeCode];
                }
            }

            public int GetOutcomeIndex(Compact<string> outcome)
            {
                int returnVal = 0;
                foreach (Compact<string> o in _outcomes)
                {
                    if (o.Equals(outcome))
                    {
                        return returnVal;
                    }
                    returnVal++;
                }
                return -1;
            }
        }
    }
}
