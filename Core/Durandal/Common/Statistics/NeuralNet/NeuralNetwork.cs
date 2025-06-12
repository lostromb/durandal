//Original author: hemanthk119
//Documentation: http://www.codeproject.com/Articles/1016734/Parallel-Artificial-Neural-Networks-in-NET-Frame

//This code is a modified version of the Parallel Neural Networks library, adapted for
//PCL and the Durandal environment.

//CPOL License

using System;
using System.Collections.Generic;

namespace Durandal.Common.Statistics.NeuralNet
{
    public class NeuralNetwork
    {
        //Array of layers
        //Index of inputlayer
        //Index of outputlayer

        public Layer[] Layers { get; set; }
        public int InputIndex { get; set; }
        public int OutputIndex { get; set; }

        public readonly int NumLayers;

        public NeuralNetwork(int numLayers)
        {
            Layers = new Layer[numLayers];
            NumLayers = numLayers;
        }

        public NeuralNetwork(NetworkData network)
        {
            if (network == null)
                throw new ArgumentNullException("network");

            Layers = new Layer[network.Layers.Count];

            NumLayers = network.Layers.Count;
            foreach (LayerData ld in network.Layers)
            {
                Layers[network.Layers.IndexOf(ld)] = new Layer(network.Layers.IndexOf(ld), ld.NumNeuron, ld.ActType, ld.Bias);
            }

            foreach (ConnectionData cd in network.Connections)
            {
                Layers[cd.From.Layer].Neurons[cd.From.Node].AddConnection(Layers[cd.To.Layer].Neurons[cd.To.Node], true, cd.Weight);
            }
            
            InputIndex = network.InputLayerId;
            OutputIndex = network.OutputLayerId;
        }

        public void Bake()
        {
            foreach (Layer layer in Layers)
            {
                foreach (Neuron neuron in layer.Neurons)
                {
                    neuron.BakeConnections();
                }
            }
        }

        public NetworkData GetNetworkData()
        {
            List<ConnectionData> conns = new List<ConnectionData>();
            List<LayerData> lays = new List<LayerData>();
            
            foreach (Layer layer in Layers)
            {
                LayerData newlayerData = new LayerData() { NumNeuron = layer.NumNeurons, ActType = layer.ActType, Bias = new List<float>() };
                int layerid = layer.Index;
                foreach (Neuron neuron in layer.Neurons)
                {
                    int neuronid = neuron.Index;

                    newlayerData.Bias.Add(neuron.Bias);

                    foreach (Connection conn in neuron.ConnectionsBaked)
                    {
                        if (conn.fromNeuron == neuron)
                        {
                            conns.Add(new ConnectionData()
                            {
                                From = new NeuronData() { Layer = layerid, Node = neuronid },
                                To = new NeuronData() { Layer = conn.toNeuron.SelfLayer.Index, Node = conn.toNeuron.Index },
                                Weight = conn.Weight

                            });
                        }
                    }
                }
                lays.Add(newlayerData);
            }

            return new NetworkData()
            {
                Connections = conns,
                InputLayerId = InputIndex,
                OutputLayerId = OutputIndex,
                Layers = lays
            };
        }

        public virtual void ApplyInput(float[] input)
        {
            Layers[InputIndex].SetValues(input);
        }

        public virtual IEnumerable<float> ReadOutput()
        {
            return Layers[OutputIndex].GetValues();
        }

        public virtual float ReadSingleOutput()
        {
            return Layers[OutputIndex].GetFirstValue();
        }

        public virtual void CalculateOutput() //TEST
        {
            //ASSUMING FORWARD FEED
            for (int i = 0; i < NumLayers; i++)
            {
                Layers[i].CalculateLayer();
            }
        }
    }
}
