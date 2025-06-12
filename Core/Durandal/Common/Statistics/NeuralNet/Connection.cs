namespace Durandal.Common.Statistics.NeuralNet
{
    public class Connection
    {
        public readonly Neuron toNeuron;
        public readonly Neuron fromNeuron;
        public float Weight;
        //public float Bias { get; set; }

        public Connection(Neuron from, Neuron to)
        {
            toNeuron = to;
            fromNeuron = from;
            Weight = (float)RandomProvider.random.NextDouble() * 0.5f;
        }

        public Connection(Neuron from, Neuron to, float weight)
        {
            toNeuron = to;
            fromNeuron = from;
            Weight = weight;
            //Bias = bias;
        }
    }
}
