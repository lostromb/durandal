using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.NLP.Classification.NeuralNet
{
    public class NetworkData
    {
        /// <summary>
        /// The number of layers
        /// </summary>
        public int NumLayers;

        /// <summary>
        /// The actual layers
        /// </summary>
        public Layer[] Layers;

        /// <summary>
        /// The number of inputs
        /// </summary>
        public int NumInputs;

        /// <summary>
        /// The number of outputs
        /// </summary>
        public int NumOutputs;

        /// <summary>
        /// Serializes this network to the given output stream, and optionally leaves the stream open afterwards
        /// </summary>
        /// <param name="writeStream">The stream to write to</param>
        /// <param name="leaveStreamOpen">If true, don't close the stream when done</param>
        public void Serialize(Stream writeStream, bool leaveStreamOpen = false)
        {
            if (writeStream == null)
            {
                throw new ArgumentNullException("writeStream");
            }

            using (BinaryWriter writer = new BinaryWriter(writeStream, Encoding.UTF8, leaveStreamOpen))
            {
                writer.Write(NumLayers);
                writer.Write(NumInputs);
                writer.Write(NumOutputs);
                foreach (Layer l in Layers)
                {
                    l.Serialize(writer);
                }
            }
        }

        public static NetworkData Deserialize(Stream readStream, bool leaveStreamOpen = false)
        {
            if (readStream == null)
            {
                throw new ArgumentNullException("readStream");
            }

            NetworkData returnVal = new NetworkData();
            using (BinaryReader reader = new BinaryReader(readStream, Encoding.UTF8, leaveStreamOpen))
            {
                returnVal.NumLayers = reader.ReadInt32();
                returnVal.NumInputs = reader.ReadInt32();
                returnVal.NumOutputs = reader.ReadInt32();
                returnVal.Layers = new Layer[returnVal.NumLayers];
                for (int c = 0; c < returnVal.NumLayers; c++)
                {
                    returnVal.Layers[c] = Layer.Deserialize(reader);
                }
            }

            return returnVal;
        }
    }
}
