using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.NLP.Classification.NeuralNet
{
    public class BackPropagationNetworkTrainer
    {
        private NetworkData _network = null;
        private BasicNetworkEvaluator _evaluator = null;
        
        public BackPropagationNetworkTrainer(NetworkData network)
        {
            _network = network;
            _evaluator = new BasicNetworkEvaluator(network);
        }

        public NetworkData Network
        {
            get
            {
                return _network;
            }
        }

        public void Train(IList<DataSet> training, int iterations = 100, float learningRate = 1.0f, float momentum = 0.1f)
        {
            // Initialize prevError[Layer][Neuron][Connection]
            float[][][] prevError = new float[_network.NumLayers - 1][][];
            // Initialize error[Layer][Neuron]
            float[][] error = new float[_network.NumLayers][];
            
            for (int layerIdx = 0; layerIdx < _network.NumLayers; layerIdx++)
            {
                Layer thisLayer = _network.Layers[layerIdx];
                error[layerIdx] = new float[thisLayer.Size];
                if (layerIdx < _network.NumLayers - 1)
                {
                    prevError[layerIdx] = new float[thisLayer.Size][];
                    for (int neuronIdx = 0; neuronIdx < thisLayer.Size; neuronIdx++)
                    {
                        prevError[layerIdx][neuronIdx] = new float[thisLayer.ConnectionsTo[neuronIdx].Length];
                    }
                }
            }

            bool hasMomentum = momentum > 0.0f;

            // Run iterations
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                float actualLearningRate = learningRate * ((float)(iterations - iteration) / iterations);

                // Iterate through the training set
                foreach (DataSet trainingInstance in training)
                {
                    // STEP 1: Feed forward
                    float[] t = _evaluator.Evaluate(trainingInstance.Inputs);

                    // STEP 2: Calculate total error
                    // Iterate layers in reverse
                    for (int layerIdx = _network.NumLayers - 1; layerIdx > 0; layerIdx--)
                    {
                        Layer thisLayer = _network.Layers[layerIdx];

                        // Calculate error for this neuron = expected - actual
                        if (layerIdx == _network.NumLayers - 1)
                        {
                            // for output layer this is easy
                            for (int outputNeuron = 0; outputNeuron < thisLayer.Size; outputNeuron++)
                            {
                                float thisValue = thisLayer.Values[outputNeuron];
                                float e = trainingInstance.Outputs[outputNeuron] - thisValue;
                                error[layerIdx][outputNeuron] = e * thisValue * (1 - thisValue);
                            }
                        }
                        else
                        {
                            // for hidden layers error equals epsilon (weight * error) for all connections from this neuron to other neurons
                            for (int sourceNeuron = 0; sourceNeuron < thisLayer.Size; sourceNeuron++)
                            {
                                float e = 0;
                                float thisValue = thisLayer.Values[sourceNeuron];
                                for (int connIdx = 0; connIdx < thisLayer.ConnectionsTo[sourceNeuron].Length; connIdx++)
                                {
                                    int targetNeuron = thisLayer.ConnectionsTo[sourceNeuron][connIdx];
                                    e += thisLayer.ConnectionWeights[sourceNeuron][connIdx] * error[layerIdx + 1][targetNeuron];
                                }

                                error[layerIdx][sourceNeuron] = e * thisValue * (1 - thisValue);
                            }
                        }
                    }

                    // STEP 3: Apply error delta to connection weights
                    // Iterate layers in reverse
                    for (int layerIdx = _network.NumLayers - 2; layerIdx >= 0; layerIdx--)
                    {
                        Layer thisLayer = _network.Layers[layerIdx];
                        Layer nextLayer = _network.Layers[layerIdx + 1];

                        // Iterate through all neurons in this layer
                        for (int sourceNeuron = 0; sourceNeuron < thisLayer.Size; sourceNeuron++)
                        {
                            // PrevDW is equal to the previous error for the connections from this neuron
                            float[] pdw = hasMomentum ? prevError[layerIdx][sourceNeuron] : null;

                            for (int connIdx = 0; connIdx < thisLayer.ConnectionsTo[sourceNeuron].Length; connIdx++)
                            {
                                // set dw = (deltaArr for this neuron * learningRate * the value of the fromNeuron) + (momentum * previous delta error)
                                float thisValue = thisLayer.Values[sourceNeuron];
                                int targetNeuron = thisLayer.ConnectionsTo[sourceNeuron][connIdx];
                                float dw = (error[layerIdx + 1][targetNeuron] * actualLearningRate * thisValue);
                                if (hasMomentum)
                                {
                                    dw += (momentum * pdw[connIdx]);
                                    pdw[connIdx] = dw;
                                }

                                // Increment connection weight based on dw
                                // Also apply a cap to keep weights from running away
                                float newWeight = thisLayer.ConnectionWeights[sourceNeuron][connIdx] + dw;
                                if (newWeight > 1000f) newWeight = 1000f;
                                else if (newWeight < -1000f) newWeight = -1000f;

                                thisLayer.ConnectionWeights[sourceNeuron][connIdx] = newWeight;
                            }
                        }
                    }
                }
            }
        }
    }
}
