using System;
using System.Collections.Generic;
using System.Linq;

namespace Durandal.Common.Statistics.NeuralNet
{
    public class Layer
    {
       

        //Hard index in NN
        public readonly int Index;
        //NumNeurons
        public readonly int NumNeurons;
        //Type
        public ActivationType ActType { get; set; }
        //Neurons Array
        public Neuron[] Neurons;

        public Layer(int index, int numNeurons, ActivationType actTye = ActivationType.SIGMOID, IEnumerable<float> initBias = null)
        {
            Index = index;
            NumNeurons = numNeurons;
            ActType = actTye;
            Neurons = new Neuron[NumNeurons];


            if (initBias != null)
            {
                for (int i = 0; i < NumNeurons; i++)
                {
                    Neurons[i] = new Neuron(i, this, ActType, initBias.ElementAt(i));
                }
            }

            else
            {
                for (int i = 0; i < NumNeurons; i++)
                {
                    Neurons[i] = new Neuron(i, this, ActType);
                }
            }
        }

        public void SetValues(float[] Values)
        {
            if (Values.Length != NumNeurons)
            {
                throw new ArgumentException("Number of input values must be equal to the size of the top layer!");
            }

            for (int i = 0; i < Values.Length; i++)
            {
                Neurons[i].Value = Values[i];
            }
        }

        public IEnumerable<float> GetValues()
        {
            IEnumerable<float> values = this.Neurons.Select(r => r.Value);
            return values;
        }

        public float GetFirstValue()
        {
            return this.Neurons[0].Value;
        }

        public void CalculateLayer()
        {
            // OPT: This might be parallelized across neurons
            for (int c = 0; c < NumNeurons; c++)
            {
                Neurons[c].CalculateValue();
            }
            //RaiseUpdated();
            //return Neurons.Select(r => r.Value);
        }

        public void ConnectToSubdivided(Layer nextlayer, int subdivisions)
        {
            int sourceDivisionSize = Math.Max(1, Neurons.Length / subdivisions);
            int targetDivisionSize = Math.Max(1, nextlayer.Neurons.Length / subdivisions);
            for (int sourceIdx = 0; sourceIdx < Neurons.Length; sourceIdx++)
            {
                for (int targetIdx = 0; targetIdx < nextlayer.Neurons.Length; targetIdx++)
                {
                    if (sourceIdx / sourceDivisionSize == targetIdx / targetDivisionSize)
                        Neurons[sourceIdx].AddConnection(nextlayer.Neurons[targetIdx], true, (float)RandomProvider.random.NextDouble() - 0.5f);
                }
            }
        }

        public void ConnectAllTo(Layer nextlayer)
        {
            foreach (Neuron n1 in Neurons)
            {
                foreach (Neuron n2 in nextlayer.Neurons)
                {
                    n1.AddConnection(n2, true, (float)RandomProvider.random.NextDouble() - 0.5f);
                }
            }
        }

        public event ChangedHandler Changed;
        public delegate void ChangedHandler(Layer l, EventArgs e);

        public event UpdatedHandler Updated;
        public delegate void UpdatedHandler(Layer l, EventArgs e);

        public void OnChanged()
        {
            if (Changed != null)
                Changed(this, EventArgs.Empty);
        }

        private void OnUpdated()
        {
            if (Updated != null)
                Updated(this, EventArgs.Empty);
        }
    }
}
