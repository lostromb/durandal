using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.NLP.Classification.NeuralNet
{
    public class BasicNetworkEvaluator
    {
        private NetworkData _network;
        private float[] _sumValues;
        private bool[] _hasInputs;

        public BasicNetworkEvaluator(NetworkData network)
        {
            _network = network;

            // Init some scratch space for the calculation
            int scratchSize = 0;
            foreach (Layer l in _network.Layers)
            {
                scratchSize = Math.Max(scratchSize, l.Size);
            }
            _sumValues = new float[scratchSize];
            _hasInputs = new bool[scratchSize];
        }

        public float[] Evaluate(float[] input)
        {
            ApplyInputInternal(input);
            EvaluateInternal();
            return GetOutput();
        }

        public void ApplyInput(float[] input)
        {
            ApplyInputInternal(input);
            EvaluateInternal();
        }

        private void ApplyInputInternal(float[] input)
        {
            if (input == null)
            {
                throw new ArgumentNullException("input");
            }

            if (input.Length != _network.NumInputs)
            {
                throw new ArgumentOutOfRangeException("This model requires " + _network.NumInputs + " inputs, " + input.Length + " given");
            }

            //Array.Copy(input, 0, _network.Layers[0].Values, 0, _network.NumInputs);
            Buffer.BlockCopy(input, 0, _network.Layers[0].Values, 0, _network.NumInputs * 4);
        }

        private void EvaluateInternal()
        {
            // Iteratively apply this values of this layer and its connections on the next layer,
            // and then Activate() the next layer on those values.
            for (int layer = 0; layer < _network.NumLayers - 1; layer++)
            {
                Layer thisLayer = _network.Layers[layer];
                Layer nextLayer = _network.Layers[layer + 1];
                for (int c = 0; c < _sumValues.Length; c++)
                {
                    _sumValues[c] = 0;
                    _hasInputs[c] = false;
                }

                // Sum up the effects of this layer's values on the next layer
                // The value of each neuron = Activate(epsilon(connection weight * neuron values for all neurons connecting into this neuron))
                for (int sourceNeuron = 0; sourceNeuron < thisLayer.Size; sourceNeuron++)
                {
                    float thisNeuronVal = thisLayer.Values[sourceNeuron];
                    foreach (int connIdx in thisLayer.ConnectionsTo[sourceNeuron])
                    {
                        int targetNeuron = thisLayer.ConnectionsTo[sourceNeuron][connIdx];
                        _sumValues[targetNeuron] += thisNeuronVal * thisLayer.ConnectionWeights[sourceNeuron][connIdx];
                        _hasInputs[targetNeuron] = true;
                    }
                }

                // Activate the whole next layer
                for (int targetNeuron = 0; targetNeuron < nextLayer.Size; targetNeuron++)
                {
                    if (_hasInputs[targetNeuron])
                    {
                        nextLayer.Values[targetNeuron] = NeuralMath.Activate(nextLayer.LayerActivation, _sumValues[targetNeuron]);
                    }
                }
            }
        }

        private float[] GetOutput()
        {
            float[] returnVal = new float[_network.NumOutputs];
            Buffer.BlockCopy(_network.Layers[_network.NumLayers - 1].Values, 0, returnVal, 0, _network.NumOutputs * 4);
            //Array.Copy(_network.Layers[_network.NumLayers - 1].Values, 0, returnVal, 0, _network.NumOutputs);
            return returnVal;
        }
    }
}
