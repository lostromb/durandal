using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.NLP.Classification.NeuralNet
{
    public static class BasicNetworkBuilder
    {
        /// <summary>
        /// Builds a fully connected basic neural network, with the specified geometry
        /// </summary>
        /// <param name="numInputs">The number of network inputs</param>
        /// <param name="numOutputs">The number of network outputs</param>
        /// <param name="numHiddenLayers">The number of hidden layers</param>
        /// <param name="hiddenLayerSize">The number of neurons in each hidden layer (not counting bias)</param>
        /// <param name="hiddenLayerActivation">The activation type to use for each neuron on the hidden layers</param>
        /// <param name="useBias">If true, add constant-weight neurons to each layer to add bias to training</param>
        /// <returns>A newly initialized neural network, with random connection weights and neuron values set to 0</returns>
        public static NetworkData BuildNetwork(int numInputs, int numOutputs, int numHiddenLayers, int hiddenLayerSize, ActivationType hiddenLayerActivation = ActivationType.Sigmoid, bool useBias = false)
        {
            NetworkData network = new NetworkData();
            network.NumInputs = numInputs;
            network.NumOutputs = numOutputs;
            network.NumLayers = numHiddenLayers + 2;
            network.Layers = new Layer[network.NumLayers];

            // Build the top layer (note that it has bias)
            network.Layers[0] = BuildLayer(network.NumInputs + (useBias ? 1 : 0), ActivationType.Ramp, true);
            
            // Build the hidden layers
            for (int layer = 1; layer <= numHiddenLayers; layer++)
            {
                int sourceLayerSize = network.Layers[layer - 1].Size;
                network.Layers[layer] = BuildLayer(hiddenLayerSize + (useBias ? 1 : 0), ActivationType.Sigmoid, true);
            }

            // Build the bottom layer
            network.Layers[network.NumLayers - 1] = BuildLayer(network.NumOutputs, ActivationType.Sigmoid, false);

            // Now connect all of the layers together
            for (int layer = 0; layer < network.NumLayers - 1; layer++)
            {
                Layer sourceLayer = network.Layers[layer];
                Layer targetLayer = network.Layers[layer + 1];
                bool targetLayerIsOutput = layer == (network.NumLayers - 2);

                for (int sourceNeuron = 0; sourceNeuron < sourceLayer.Size; sourceNeuron++)
                {
                    int targetSize = (!useBias || targetLayerIsOutput ? targetLayer.Size : targetLayer.Size - 1);
                    sourceLayer.ConnectionsTo[sourceNeuron] = BuildSequence(targetSize);
                    sourceLayer.ConnectionWeights[sourceNeuron] = new float[targetSize];
                }
            }

            // Assign initial values to the neurons and connections
            Random rand = new Random();
            for (int layerIdx = 0; layerIdx < network.NumLayers; layerIdx++)
            {
                Layer layer = network.Layers[layerIdx];
                for (int neuronIdx  = 0; neuronIdx < layer.Size; neuronIdx++)
                {
                    bool isBiasNeuron = layerIdx < network.NumLayers - 1 && neuronIdx == layer.Size - 1;
                    layer.Values[neuronIdx] = useBias && isBiasNeuron ? 1.0f : 0.0f;
                    if (layerIdx < network.NumLayers - 1)
                    {
                        for (int connIdx = 0; connIdx < layer.ConnectionWeights[neuronIdx].Length; connIdx++)
                        {
                            layer.ConnectionWeights[neuronIdx][connIdx] = (float)(rand.NextDouble() * 0.5);
                        }
                    }
                }
            }

            return network;
        }

        private static int[] BuildSequence(int length)
        {
            int[] returnVal = new int[length];
            for (int c = 0; c < length; c++)
            {
                returnVal[c] = c;
            }

            return returnVal;
        }

        private static Layer BuildLayer(int size, ActivationType type, bool hasOutputs)
        {
            Layer returnVal = new Layer();
            returnVal.Size = size;
            returnVal.LayerActivation = type;
            returnVal.Values = new float[size];
            if (hasOutputs)
            {
                returnVal.ConnectionsTo = new int[size][];
                returnVal.ConnectionWeights = new float[size][];
            }
            return returnVal;
        }
    }
}
