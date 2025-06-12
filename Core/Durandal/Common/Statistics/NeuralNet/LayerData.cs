using System.Collections.Generic;

namespace Durandal.Common.Statistics.NeuralNet
{
    public class LayerData
    {
        public ActivationType ActType { get; set; }
        public int NumNeuron { get; set; }
        public List<float> Bias { get; set; }
    }
}
