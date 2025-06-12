using System.Collections.Generic;
using System.Threading.Tasks;
using Durandal.Common.Logger;

namespace Durandal.Common.Statistics.NeuralNet
{
    public class BackPropogationNetwork : NeuralNetwork
    {
        private float[][][] PrevDW = null;
        private float[][] deltaArr = null;

        public BackPropogationNetwork(NetworkData data)
            : base(data)
        {
            Bake();
        }

        public BackPropogationNetwork(int numInputs, int numOutputs, int numHidden, int numHiddenLayers = 1, int numInternalSubdivisions = 1)
            : base(numHiddenLayers + 2)
        {
            Layers[0] = new Layer(0, numInputs, ActivationType.RAMP);
            Layers[2 + numHiddenLayers - 1] = new Layer(2 + numHiddenLayers - 1, numOutputs, ActivationType.SIGMOID);
            for (int i = 0; i < numHiddenLayers; i++)
            {
                Layers[i + 1] = new Layer(i + 1, numHidden, ActivationType.SIGMOID);
            }
            
            Layers[0].ConnectToSubdivided(Layers[1], numInternalSubdivisions);

            for (int i = 1; i < NumLayers - 2; i++)
            {
                Layers[i].ConnectAllTo(Layers[i + 1]);
            }

            Layers[NumLayers - 2].ConnectAllTo(Layers[NumLayers - 1]);
            
            Bake();

            InputIndex = 0;
            OutputIndex = NumLayers - 1;
        }

        public void BackPropagate(DataSet datain, float learningRate, float momentum = 0)
        {
            if (PrevDW == null)
            {
                PrevDW = new float[NumLayers][][];
                deltaArr = new float[NumLayers][];
                for (int i = 0; i < NumLayers; i++)
                {
                    deltaArr[i] = new float[Layers[i].NumNeurons];
                    PrevDW[i] = new float[Layers[i].NumNeurons][];

                    for (int n = 0; n < Layers[i].NumNeurons; n++)
                    {
                        //deltaArr[i][n] = 0;
                        int d = 0;
                        foreach (Connection c in Layers[i].Neurons[n].ConnectionsBaked)
                        {
                            if (c.toNeuron != Layers[i].Neurons[n])
                                continue;
                            d++;
                        }

                        PrevDW[i][n] = new float[d];
                    }
                }
            }

            ApplyInput(datain.Inputs);
            CalculateOutput();

#if THREADED_RNN
            Task[] subtasks = new Task[Environment.ProcessorCount];
#endif
            Layer currentLayer = Layers[OutputIndex];

            while (currentLayer != Layers[InputIndex])
            {
#if THREADED_RNN
                for (int taskid = 0; taskid < subtasks.Length; taskid++)
                {
                    subtasks[taskid] = taskFactory.StartNew((state) =>
                    {
                        int task_id = (int)state;
                        for (int c = task_id; c < currentLayer.NumNeurons; c += subtasks.Length)
#else
                        for (int c = 0; c < currentLayer.NumNeurons; c++)
#endif
                        {
                            Neuron n = currentLayer.Neurons[c];
                            float error = 0;

                            if (currentLayer == Layers[OutputIndex])
                            {
                                error = datain.Outputs[n.Index] - n.Value;
                            }
                            else
                            {
                                int cc = n.ConnectionsBaked.Length;
                                for (int ci = 0; ci < cc; ci++)
                                {
                                    Connection conn = n.ConnectionsBaked[ci];
                                    if (conn.fromNeuron != n)
                                        continue;
                                    error += conn.Weight * deltaArr[conn.toNeuron.SelfLayer.Index][conn.toNeuron.Index];
                                }
                            }

                            error = error * n.Value * (1 - n.Value);
                            deltaArr[currentLayer.Index][n.Index] = error;

                        }
#if THREADED_RNN
                    }, taskid);
            }

            Task.WaitAll(subtasks);
#endif

                currentLayer = Layers[currentLayer.Index - 1];
            }

            currentLayer = Layers[OutputIndex];


            while (currentLayer != Layers[InputIndex])
            {
#if THREADED_RNN
                for (int taskid = 0; taskid < subtasks.Length; taskid++)
                {
                    subtasks[taskid] = taskFactory.StartNew((state) =>
                    {
                        int task_id = (int)state;
                        for (int i = task_id; i < currentLayer.NumNeurons; i += subtasks.Length)
#else
                        for (int i = 0; i < currentLayer.NumNeurons; i ++)
#endif
                        {
                            float[] pdw = PrevDW[currentLayer.Index][i];
                            Neuron n = currentLayer.Neurons[i];

                            int cc = n.ConnectionsBaked.Length;
                            for (int ci = 0; ci < cc; ci++)
                            {
                                Connection c = n.ConnectionsBaked[ci];
                                if (c.toNeuron != n)
                                    continue;

                                float dw = (deltaArr[c.toNeuron.SelfLayer.Index][c.toNeuron.Index] * learningRate * c.fromNeuron.Value) + (momentum * pdw[ci]);
                                c.Weight += dw;
                                pdw[ci] = dw;
                            }

                            n.Bias += deltaArr[currentLayer.Index][i] * learningRate;
                        }
#if THREADED_RNN
                    }, taskid);
                }
                Task.WaitAll(subtasks);
#endif

                currentLayer = Layers[currentLayer.Index - 1];
            }
        }

        public void BatchBackPropagate(IEnumerable<DataSet> dataSet, ILogger logger, int iterations, float learningRate, float momentum = 0)
        {
            int y = 0;
            for (int i = 0; i < iterations; i++)
            {
                //logger.Log("Iteration " + i);
                float iterPercent = (float)i / iterations;
                foreach (DataSet data in dataSet)
                {
                    BackPropagate(data, (1 - iterPercent) * learningRate, momentum);
                    y++;
                }
            }
        }
    }
}
