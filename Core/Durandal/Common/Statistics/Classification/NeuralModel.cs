using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Durandal.Common.Logger;
using Durandal.Common.Statistics.NeuralNet;
using Durandal.Common.Statistics.SharpEntropy;
using Durandal.Common.NLP.Feature;
using Durandal.Common.File;
using Durandal.Common.Statistics;

namespace Durandal.Common.Statistics.Classification
{
    public class NeuralModel : IStatisticalClassifier
    {
        private BackPropogationNetwork _network;
        private ILogger _logger;
        private IDictionary<string, int> _inputFeatureMapping;
        private IDictionary<string, int> _outcomeToIndex;
        private IDictionary<int, string> _indexToOutcome;
        private string[] _outcomeNames;
        private int _inputFeatureCount = 0;
        private int _outcomeCount = 0;

        public NeuralModel(ILogger logger)
        {
            _logger = logger;
            _inputFeatureMapping = new Dictionary<string, int>();
            _outcomeToIndex = new Dictionary<string, int>();
            _indexToOutcome = new Dictionary<int, string>();
        }

        public static NeuralModel Deserialize(BinaryReader reader, ILogger logger)
        {
            NeuralModel returnVal = new NeuralModel(logger);
            returnVal.Deserialize(reader);
            return returnVal;
        }

        public void Train(ITrainingEventReader eventReader, VirtualPath cacheFileName = null, IFileSystem cacheManager = null, float trainingQuality = 1.0f)
        {
            // Does the cache exist?
            if (cacheFileName != null && cacheManager != null && cacheManager.Exists(cacheFileName + ".nn"))
            {
                using (Stream readStream = cacheManager.OpenStream(cacheFileName + ".nn", FileOpenMode.Open, FileAccessMode.Read))
                {
                    Deserialize(readStream, false);
                }

                return;
            }

            if (eventReader == null)
            {
                throw new ArgumentException("Null event reader after failing to read a serialized model");
            }

            IList<TrainingEvent> rawTraining = new List<TrainingEvent>();
            IList<DataSet> neuralTraining = new List<DataSet>();

            int iterations = (int)(trainingQuality * 250);
            float trainingSpeed = 3.0f / Math.Max(0.2f, trainingQuality);

            // Process the raw training
            while (eventReader.HasNext())
            {
                TrainingEvent train = eventReader.ReadNextEvent();
                foreach (string x in train.Context)
                {
                    if (!_inputFeatureMapping.ContainsKey(x))
                    {
                        _inputFeatureMapping[x] = _inputFeatureCount++;
                    }
                }
                if (!_outcomeToIndex.ContainsKey(train.Outcome))
                {
                    _indexToOutcome[_outcomeCount] = train.Outcome;
                    _outcomeToIndex[train.Outcome] = _outcomeCount;
                    _outcomeCount++;
                }

                rawTraining.Add(train);
            }

            _outcomeNames = new string[_outcomeToIndex.Count];
            foreach (var kvp in _outcomeToIndex)
            {
                _outcomeNames[kvp.Value] = kvp.Key;
            }

            // Now convert the training into neural vectors
            foreach (TrainingEvent train in rawTraining)
            {
                float[] inVec = new float[_inputFeatureCount];
                float[] outVec = new float[_outcomeCount];
                foreach (string x in train.Context)
                {
                    inVec[_inputFeatureMapping[x]] = 1.0f;
                }
                outVec[_outcomeToIndex[train.Outcome]] = 1.0f;

                DataSet neuralTrain = new DataSet()
                {
                    Inputs = inVec,
                    Outputs = outVec
                };

                neuralTraining.Add(neuralTrain);
            }

            if (_outcomeCount > 1)
            {
                int numInputs = _inputFeatureCount;
                int numOutcomes = _outcomeCount;
                int numHidden = Math.Max(2, Math.Min(80, _outcomeCount * 10));
                int hiddenLayers = 1;
                int internalSubdivisions = Math.Max(1, numHidden / 8);
                //_logger.Log(string.Format("Neural net topography: {0} inputs, {1} hidden nodes * {2} layers, {3} subdivisions, {4} outputs, {5} iterations", numInputs, numHidden, hiddenLayers, internalSubdivisions, numOutcomes, iterations), LogLevel.Std);
                _network = new BackPropogationNetwork(numInputs, numOutcomes, numHidden, hiddenLayers, internalSubdivisions);
                _network.BatchBackPropagate(neuralTraining, _logger, iterations, trainingSpeed, 0.1f);
            }

            // Drop training
            rawTraining.Clear();

            // And serialize the cached model
            if (cacheFileName != null && cacheManager != null)
            {
                using (Stream writeStream = cacheManager.OpenStream(cacheFileName + ".nn", FileOpenMode.Create, FileAccessMode.Write))
                {
                    Serialize(writeStream, false);
                }
            }
        }

        public List<Hypothesis<string>> ClassifyAll(string[] featureSet)
        {
            List<Hypothesis<string>> returnVal = new List<Hypothesis<string>>();

            if (_outcomeCount == 1)
            {
                returnVal.Add(new Hypothesis<string>(_outcomeNames[0], 1.0f));
            }
            else if (_network == null)
            {
                return returnVal;
            }
            else 
            {
                float[] inVec = new float[_inputFeatureCount];
                foreach (string x in featureSet)
                {
                    if (_inputFeatureMapping.ContainsKey(x))
                    {
                        inVec[_inputFeatureMapping[x]] = 1.0f;
                    }
                }
                _network.ApplyInput(inVec);
                _network.CalculateOutput();

                int idx = 0;
                foreach (float val in _network.ReadOutput())
                {
                    returnVal.Add(new Hypothesis<string>(_outcomeNames[idx++], val));
                }

                returnVal.Sort(new Hypothesis<string>.DescendingComparator());
            }

            return returnVal;
        }

        public Hypothesis<string>? Classify(string[] featureSet)
        {
            List<Hypothesis<string>> results = ClassifyAll(featureSet);

            // Pick the top result, if it's there
            if (results.Count == 0)
            {
                return null;
            }

            return results[0];
        }
        
        public string GetBestOutcome(float[] outcomes)
        {
            int bestOutcomeIndex = 0;
            for (int currentOutcome = 1; currentOutcome < outcomes.Length; currentOutcome++)
            {
                if (outcomes[currentOutcome] > outcomes[bestOutcomeIndex])
                {
                    bestOutcomeIndex = currentOutcome;
                }
            }

            return _indexToOutcome[bestOutcomeIndex];
        }

        public int GetOutcomeIndex(string outcome)
        {
            if (_outcomeToIndex.ContainsKey(outcome))
            {
                return _outcomeToIndex[outcome];
            }

            return -1;
        }

        public long GetMemoryUse()
        {
            return 0;
        }

        public int OutcomeCount
        {
            get
            {
                return _outcomeCount;
            }
        }

        public string GetOutcomeName(int index)
        {
            return _indexToOutcome[index];
        }

        public string[] GetOutcomeNames()
        {
            return _outcomeNames;
        }

        private const int VERSION_NUM = 0;

        public void Serialize(Stream writeStream, bool leaveStreamOpen)
        {
            using (BinaryWriter writer = new BinaryWriter(writeStream, Encoding.UTF8, true))
            {
                Serialize(writer);
            }
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(VERSION_NUM);
            writer.Write(_outcomeCount);
            writer.Write(_inputFeatureCount);

            if (_outcomeToIndex == null)
            {
                writer.Write((int)0);
            }
            else
            {
                writer.Write(_outcomeToIndex.Count);
                foreach (var kvp in _outcomeToIndex)
                {
                    writer.Write(kvp.Key);
                    writer.Write(kvp.Value);
                }
            }

            if (_indexToOutcome == null)
            {
                writer.Write((int)0);
            }
            else
            {
                writer.Write(_indexToOutcome.Count);
                foreach (var kvp in _indexToOutcome)
                {
                    writer.Write(kvp.Key);
                    writer.Write(kvp.Value);
                }
            }

            if (_inputFeatureMapping == null)
            {
                writer.Write((int)0);
            }
            else
            {
                writer.Write(_inputFeatureMapping.Count);
                foreach (var kvp in _inputFeatureMapping)
                {
                    writer.Write(kvp.Key);
                    writer.Write(kvp.Value);
                }
            }

            // Write true if the network is null
            writer.Write(_network != null);

            if (_network != null)
            {
                _network.GetNetworkData().Serialize(writer);
            }
        }

        private void Deserialize(Stream readStream, bool leaveStreamOpen)
        {
            using (BinaryReader reader = new BinaryReader(readStream, Encoding.UTF8, true))
            {
                Deserialize(reader);
            }
        }

        private void Deserialize(BinaryReader reader)
        {
            int versionNum = reader.ReadInt32();
            if (versionNum != VERSION_NUM)
            {
                throw new IOException("The serialized neural model claims to be version " + versionNum + " but I am expecting " + VERSION_NUM);
            }

            _outcomeCount = reader.ReadInt32();
            _inputFeatureCount = reader.ReadInt32();

            _outcomeToIndex = new Dictionary<string, int>();
            _outcomeNames = new string[_outcomeCount];
            int size = reader.ReadInt32();
            for (int c = 0; c < size; c++)
            {
                string key = reader.ReadString();
                int value = reader.ReadInt32();
                _outcomeToIndex[key] = value;
                _outcomeNames[value] = key;
            }

            _indexToOutcome = new Dictionary<int, string>();
            size = reader.ReadInt32();
            for (int c = 0; c < size; c++)
            {
                int key = reader.ReadInt32();
                string value = reader.ReadString();
                _indexToOutcome[key] = value;
            }

            _inputFeatureMapping = new Dictionary<string, int>();
            size = reader.ReadInt32();
            for (int c = 0; c < size; c++)
            {
                string key = reader.ReadString();
                int value = reader.ReadInt32();
                _inputFeatureMapping[key] = value;
            }

            bool hasNetwork = reader.ReadBoolean();

            if (hasNetwork)
            {
                NetworkData network = NetworkData.Deserialize(reader);
                _network = new BackPropogationNetwork(network);
            }
            else
            {
                _network = null;
            }
        }
    }
}
