using Durandal.Common.Audio;
using Durandal.Common.Statistics.NeuralNet;
using Durandal.Common.MathExt;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Prototype
{
    public static class ArrayMicTest
    {
        public static void Run()
        {
            //const int CHANNELS = 4;
            //const int CHANNEL_WIDTH = 10;
            //int[][] CHANNEL_DELAYS = new int[2][];
            //CHANNEL_DELAYS[0] = new int[] { 0, 3, 6, 9 };
            //CHANNEL_DELAYS[1] = new int[] { 9, 6, 3, 0 };
            //int[] FAKE_DELAYS = new int[] { 0 };


            ////float[] x = new float[] { 1, 2, 3, 4, 5, 6 };
            ////float[] y = new float[9];
            ////int[] z = new int[] { 0, 1, 2 };
            ////ApplyAudioWithDelays(x, y, 3, 3, 0, z);

            //byte[][] byteAudio = new byte[2][];
            //byteAudio[0] = File.ReadAllBytes(@"C:\Users\LOSTROMB\Desktop\Undertale.raw");
            //byteAudio[1] = File.ReadAllBytes(@"C:\Users\LOSTROMB\Desktop\Fire Fire Quiet.raw");
            //short[][] shortAudio = new short[2][];
            //shortAudio[0] = AudioMath.BytesToShorts(byteAudio[0]);
            //shortAudio[1] = AudioMath.BytesToShorts(byteAudio[1]);
            //float[][] floatAudio = new float[2][];
            //floatAudio[0] = new float[shortAudio[0].Length];
            //floatAudio[1] = new float[shortAudio[1].Length];
            //for (int c = 0; c < shortAudio[0].Length; c++)
            //{
            //    floatAudio[0][c] = ((float)shortAudio[0][c]) / 32767f;
            //}
            //for (int c = 0; c < shortAudio[1].Length; c++)
            //{
            //    floatAudio[1][c] = ((float)shortAudio[1][c]) / 32767f;
            //}

            //IRandom rand = new FastRandom();
            //BackPropogationNetwork neuralNet;
            //int samplesToProcess = 240 * 48000;

            //bool load = false;
            //if (!load)
            //{
            //    neuralNet = new BackPropogationNetwork(CHANNELS * CHANNEL_WIDTH, 1, 40, 3);

            //    Console.WriteLine("Creating sample file");
            //    short[] noisyAudio = new short[samplesToProcess];
            //    float[] noiseSample = new float[1];
            //    for (int c = 0; c < samplesToProcess; c++)
            //    {
            //        noiseSample[0] = 0;
            //        ApplyAudioWithDelays(floatAudio[0], noiseSample, 1, 1, c, FAKE_DELAYS);
            //        ApplyAudioWithDelays(floatAudio[1], noiseSample, 1, 1, c, FAKE_DELAYS);
            //        noisyAudio[c] = (short)((noiseSample[0]) * 32767f);
            //    }

            //    byteAudio[0] = AudioMath.ShortsToBytes(noisyAudio);
            //    File.WriteAllBytes(@"C:\Users\LOSTROMB\Desktop\Merged Noise.raw", byteAudio[0]);

            //    Console.WriteLine("Training neural net");
            //    // Train the neural net
            //    DataSet currentInput = new DataSet();
            //    currentInput.Inputs = new float[CHANNELS * CHANNEL_WIDTH];
            //    currentInput.Outputs = new float[1];
            //    for (int c = 0; c < samplesToProcess; c++)
            //    {
            //        for (int s = 0; s < CHANNELS * CHANNEL_WIDTH; s++)
            //        {
            //            currentInput.Inputs[s] = 0f;
            //        }
            //        ApplyAudioWithDelays(floatAudio[0], currentInput.Inputs, CHANNELS, CHANNEL_WIDTH, c, CHANNEL_DELAYS[0]);
            //        ApplyAudioWithDelays(floatAudio[1], currentInput.Inputs, CHANNELS, CHANNEL_WIDTH, c, CHANNEL_DELAYS[1]);
            //        currentInput.Outputs[0] = floatAudio[0][c + CHANNEL_WIDTH];
            //        ConvertToRnn(currentInput.Inputs);
            //        ConvertToRnn(currentInput.Outputs);
            //        float percent = (((float)c) / (float)samplesToProcess);
            //        neuralNet.BackPropagate(currentInput, 1.0f - percent);
            //        Console.Write("\r" + ((c * 100) / samplesToProcess) + "%      ");
            //    }

            //    Console.WriteLine("Saving model");
            //    using (Stream modelOut = new FileStream(@"C:\Users\LOSTROMB\Desktop\model.rnn", FileMode.Create, FileAccess.Write))
            //    {
            //        neuralNet.GetNetworkData().Serialize(modelOut);
            //        modelOut.Dispose();
            //    }
            //}
            //else
            //{
            //    Console.WriteLine("Loading model");
            //    using (Stream modelIn = new FileStream(@"C:\Users\LOSTROMB\Desktop\model.rnn", FileMode.Open, FileAccess.Read))
            //    {
            //        NetworkData network = NetworkData.Deserialize(modelIn);
            //        neuralNet = new BackPropogationNetwork(network);
            //        modelIn.Dispose();
            //    }
            //}

            //Console.WriteLine("Evaluating");
            //// Now run the neural net
            //float[] input = new float[CHANNELS * CHANNEL_WIDTH];
            //short[] finalOutput = new short[samplesToProcess];
            //for (int c = 0; c < samplesToProcess; c++)
            //{
            //    for (int s = 0; s < CHANNELS * CHANNEL_WIDTH; s++)
            //    {
            //        input[s] = 0f;
            //    }
            //    ApplyAudioWithDelays(floatAudio[0], input, CHANNELS, CHANNEL_WIDTH, c, CHANNEL_DELAYS[0]);
            //    ApplyAudioWithDelays(floatAudio[1], input, CHANNELS, CHANNEL_WIDTH, c, CHANNEL_DELAYS[1]);
            //    ConvertToRnn(input);
            //    neuralNet.ApplyInput(input);
            //    neuralNet.CalculateOutput();
            //    float output = ConvertFromRnn(neuralNet.ReadSingleOutput());
            //    finalOutput[c] = (short)(output * 32767f);
            //    Console.Write("\r" + ((c * 100) / samplesToProcess) + "%      ");
            //}

            //byteAudio[0] = AudioMath.ShortsToBytes(finalOutput);
            //File.WriteAllBytes(@"C:\Users\LOSTROMB\Desktop\Merged Out.raw", byteAudio[0]);
        }

        private static void ConvertToRnn(float[] audio)
        {
            for (int c = 0; c < audio.Length; c++)
            {
                audio[c] = (audio[c] / 2f) + 0.5f;
            }
        }

        private static float ConvertFromRnn(float pulse)
        {
            return (pulse - 0.5f) * 2f;
        }

        private static void ConvertFromRnn(float[] pulse)
        {
            for (int c = 0; c < pulse.Length; c++)
            {
                pulse[c] = (pulse[c] - 0.5f) * 2f;
            }
        }

        private static void ApplyAudioWithDelays(float[] audio, float[] dest, int numChannels, int channelWidth, int baseOffset, int[] delays)
        {
            for (int chan = 0; chan < numChannels; chan++)
            {
                for (int sample = 0; sample < channelWidth; sample++)
                {
                    dest[(channelWidth * chan) + sample] += audio[baseOffset + sample + channelWidth - delays[chan]];
                }
            }
        }
    }
}
