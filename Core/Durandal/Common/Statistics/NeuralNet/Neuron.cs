using System;
using System.Collections.Generic;
using Durandal.Common.MathExt;
using System.Diagnostics;

namespace Durandal.Common.Statistics.NeuralNet
{
    public class Neuron
    {
        public readonly int Index;

        //public float AccumulateStore;

        public float Value;

        public float Bias;

        public ActivationType ActType;

        public List<Connection> ConnectionsTemp;

        public Connection[] ConnectionsBaked;

        private Connection[] _inputConnections;

        public Layer SelfLayer;

        public Neuron(int index, Layer initLayer, ActivationType actType = ActivationType.SIGMOID, float bias = 0)
        {
            Index = index;
            ConnectionsTemp = new List<Connection>();
            SelfLayer = initLayer;
            ActType = actType;
            Bias = bias;
        }

        public void AddConnection(Neuron neuron, bool to, float weight = 0/*, double bias = 0*/)
        {
            Connection c;

            if (to)
                c = new Connection(this, neuron, weight);
            else
                c = new Connection(neuron, this, weight);

            this.ConnectionsTemp.Add(c);
            neuron.ConnectionsTemp.Add(c);
        }

        public void BakeConnections()
        {
            ConnectionsBaked = ConnectionsTemp.ToArray();
            ConnectionsTemp = null;
        }

        public float CalculateValue()  //NEURON UPDATE
        {
            if (_inputConnections == null)
            {
                // Cache the list of input connections on first run
                List<Connection> inputConnectionsTemp = new List<Connection>();
                foreach (Connection c in ConnectionsBaked)
                {
                    if (c.toNeuron == this)
                        inputConnectionsTemp.Add(c);
                }

                _inputConnections = inputConnectionsTemp.ToArray();
            }

            float val = 0;
            
            bool hasInputs = false;

            for (int iter = 0; iter < _inputConnections.Length; iter++)
            {
                Connection c = _inputConnections[iter];
                val += (c.fromNeuron.Value * c.Weight);
                hasInputs = true;
            }

            if (hasInputs)
            {
                val += Bias;
                //AccumulateStore = val;
                Value = Activate(val);
            }

            //Console.WriteLine("NeCal: Acc:" + AccumulateStore + ", Val: " + Value + "Bia: " + Bias );

            return Value;
        }

        public float Activate(float input)
        {
            switch (ActType)
            {
                case ActivationType.BINARY:
                    if (input > 0)
                        return 1;
                    else
                        return 0;

                case ActivationType.BIPOLAR:

                    if (input > 0)
                        return 1;
                    else
                        return -1;

                case ActivationType.RAMP:
                    return input;

                case ActivationType.SIGMOID:
                    return FastMath.Sigmoid(input);

                default:
                    return 0;
                case ActivationType.BIPOLARSIGMOID:
                    return (float)((2 / (1 +Math.Exp(0 - input))) - 1);

            }
        }
    }
}
