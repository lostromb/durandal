using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Durandal.Common.Statistics.NeuralNet
{
    public class NetworkData
    {
        public List<LayerData> Layers { get; set; }
        public List<ConnectionData> Connections { get; set; }
        public int InputLayerId { get; set; }
        public int OutputLayerId { get; set; }

        public NetworkData()
        {
            Layers = new List<LayerData>();
            Connections = new List<ConnectionData>();
        }

        private const int VERSION_NUM = 0;

        public void Serialize(Stream fileStream, bool leaveStreamOpen = false)
        {
            using (BinaryWriter writer = new BinaryWriter(fileStream, Encoding.UTF8, leaveStreamOpen))
            {
                Serialize(writer);
            }
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(VERSION_NUM);
            writer.Write(InputLayerId);
            writer.Write(OutputLayerId);
            writer.Write(Layers.Count);
            foreach (LayerData layer in Layers)
            {
                writer.Write(layer.NumNeuron);
                writer.Write((int)layer.ActType);
                writer.Write(layer.Bias.Count);
                foreach (float b in layer.Bias)
                {
                    writer.Write(b);
                }
            }
            writer.Write(Connections.Count);
            foreach (ConnectionData connection in Connections)
            {
                writer.Write(connection.From.Layer);
                writer.Write(connection.From.Node);
                writer.Write(connection.To.Layer);
                writer.Write(connection.To.Node);
                writer.Write(connection.Weight);
            }
        }

        public static NetworkData Deserialize(Stream readStream, bool leaveStreamOpen = false)
        {
            using (BinaryReader reader = new BinaryReader(readStream, Encoding.UTF8, leaveStreamOpen))
            {
                return Deserialize(reader);
            }
        }

        public static NetworkData Deserialize(BinaryReader reader)
        {
            NetworkData returnVal = new NetworkData();

            int version = reader.ReadInt32();
            if (version != VERSION_NUM)
            {
                throw new IOException("The serialized neural network claims to be version " + version + " but I am expecting " + VERSION_NUM);
            }

            returnVal.InputLayerId = reader.ReadInt32();
            returnVal.OutputLayerId = reader.ReadInt32();
            int layerCount = reader.ReadInt32();
            returnVal.Layers = new List<LayerData>(layerCount);
            for (int layer = 0; layer < layerCount; layer++)
            {
                LayerData newLayer = new LayerData();
                newLayer.NumNeuron = reader.ReadInt32();
                newLayer.ActType = (ActivationType)reader.ReadInt32();
                int biasCount = reader.ReadInt32();
                newLayer.Bias = new List<float>(biasCount);
                for (int b = 0; b < biasCount; b++)
                {
                    newLayer.Bias.Add(reader.ReadSingle());
                }
                returnVal.Layers.Add(newLayer);
            }

            int connectionCount = reader.ReadInt32();
            returnVal.Connections = new List<ConnectionData>(connectionCount);

            for (int connection = 0; connection < connectionCount; connection++)
            {
                ConnectionData newConnection = new ConnectionData();
                newConnection.From = new NeuronData();
                newConnection.From.Layer = reader.ReadInt32();
                newConnection.From.Node = reader.ReadInt32();
                newConnection.To = new NeuronData();
                newConnection.To.Layer = reader.ReadInt32();
                newConnection.To.Node = reader.ReadInt32();
                newConnection.Weight = reader.ReadSingle();
                returnVal.Connections.Add(newConnection);
            }

            return returnVal;
        }
    }
}
