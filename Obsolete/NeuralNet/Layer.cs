using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.NLP.Classification.NeuralNet
{
    public class Layer
    {
        /// <summary>
        /// The number of neurons on this layer
        /// </summary>
        public int Size;

        /// <summary>
        /// Activation type for all neurons on this layer
        /// </summary>
        public ActivationType LayerActivation;

        /// <summary>
        /// The raw neuron values
        /// </summary>
        public float[] Values;

        /// <summary>
        /// Index 0 is the source neuron. Index 1 is the target neuron on the next layer
        /// </summary>
        public int[][] ConnectionsTo;

        /// <summary>
        /// Index 0 is the source neuron. Index 1 is the connection index (not the target neuron idx)
        /// </summary>
        public float[][] ConnectionWeights;

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(Size);
            writer.Write((int)LayerActivation);
            // Write "true" if connections to the next layer exist
            writer.Write(ConnectionsTo != null);
            if (ConnectionsTo != null)
            {
                for (int sourceNeuron = 0; sourceNeuron < Size; sourceNeuron++)
                {
                    // Write number of connections coming out from this neuron
                    writer.Write(ConnectionsTo[sourceNeuron].Length);
                    for (int connIdx = 0; connIdx < ConnectionsTo[sourceNeuron].Length; connIdx++)
                    {
                        // Write each connection as a pair of targetIdx + weight
                        writer.Write(ConnectionsTo[sourceNeuron][connIdx]);
                        writer.Write(ConnectionWeights[sourceNeuron][connIdx]);
                    }
                }
            }
        }

        public static Layer Deserialize(BinaryReader reader)
        {
            Layer returnVal = new Layer();
            returnVal.Size = reader.ReadInt32();
            returnVal.LayerActivation = (ActivationType)reader.ReadInt32();
            bool hasConnections = reader.ReadBoolean();
            if (hasConnections)
            {
                returnVal.ConnectionsTo = new int[returnVal.Size][];
                returnVal.ConnectionWeights = new float[returnVal.Size][];
                for (int sourceNeuron = 0; sourceNeuron < returnVal.Size; sourceNeuron++)
                {
                    // Read number of connections coming out from this neuron
                    int numConnections = reader.ReadInt32();
                    returnVal.ConnectionsTo[sourceNeuron] = new int[numConnections];
                    returnVal.ConnectionWeights[sourceNeuron] = new float[numConnections];
                    for (int connIdx = 0; connIdx < numConnections; connIdx++)
                    {
                        // Read each connection as a pair of targetIdx + weight
                        returnVal.ConnectionsTo[sourceNeuron][connIdx] = reader.ReadInt32();
                        returnVal.ConnectionWeights[sourceNeuron][connIdx] = reader.ReadSingle();
                    }
                }
            }
            return returnVal;
        }
    }
}
