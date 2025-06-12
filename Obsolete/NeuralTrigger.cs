using Durandal.Common.Speech.Triggers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Audio;
using Stromberg.Utils;
using Durandal.Common.Audio.FFT;
using Durandal.Common.NLP.NeuralNet;
using Stromberg.Utils.IO;
using Stromberg.Logger;

namespace Durandal.Common.Speech.Triggers
{
    public class NeuralTrigger : IAudioTrigger
    {
        private const int WINDOW_SIZE = 512;
        private const int LAP_INCREMENT = 256;
        private const int HISTORY_LENGTH = 30;
        private const int SPECTRUM_HEIGHT = 64;
        private const int NUM_INPUTS = HISTORY_LENGTH * SPECTRUM_HEIGHT;
        private const float THRESHOLD = 0.002f;

        BasicBufferShort inputBuf = new BasicBufferShort(16000);
        short[] window;
        float[] currentVector;
        float[] suppressionVector;
        BackPropogationNetwork neuralNet;
        private ILogger _logger;

        public NeuralTrigger(ILogger logger, IResourceManager resourceManager, ResourceName modelFile)
        {
            _logger = logger;
            if (!resourceManager.Exists(modelFile))
            {
                _logger.Log("No neural model file was found!", LogLevel.Err);
            }
            else
            {
                NetworkData model = NetworkData.ReadFromFile(resourceManager, modelFile);
                neuralNet = new BackPropogationNetwork(model);
            }

            Reset();
        }
        
        public void NoOp(AudioChunk audio)
        {
            inputBuf.Write(audio.Data);
        }

        public void Reset()
        {
            inputBuf.Clear();
            window = new short[WINDOW_SIZE];
            currentVector = new float[NUM_INPUTS];
            suppressionVector = GenerateSuppressionVector(WINDOW_SIZE);
        }

        public bool Try(AudioChunk audio)
        {
            if (neuralNet == null)
            {
                return false;
            }

            NoOp(audio);
            bool returnVal = false;
            while (inputBuf.Available() > LAP_INCREMENT)
            {
                short[] nextSlice = inputBuf.Read(LAP_INCREMENT);
                MemMove(window, LAP_INCREMENT, 0, WINDOW_SIZE - LAP_INCREMENT);
                Array.Copy(nextSlice, 0, window, WINDOW_SIZE - LAP_INCREMENT, LAP_INCREMENT);
                MemMove(currentVector, SPECTRUM_HEIGHT, 0, NUM_INPUTS - SPECTRUM_HEIGHT);
                GetVector(window, null, currentVector, NUM_INPUTS - SPECTRUM_HEIGHT);
                neuralNet.ApplyInput(currentVector);
                neuralNet.CalculateOutput();
                float excitement = neuralNet.ReadSingleOutput();
                StringBuilder meter = new StringBuilder();
                for (int c = 0; c < (int)((excitement / THRESHOLD) * 100); c++)
                {
                    meter.Append("X");
                }
                //_logger.Log(meter.ToString());
                //_logger.Log(excitement);
                if (excitement > THRESHOLD)
                {
                    returnVal = true;
                }
            }

            return returnVal;
        }

        public void Dispose() { }
        
        /// <summary>
        /// Initializes the moving average array using a power decay function that approximates
        /// a slightly noisy curve in fourier space
        /// </summary>
        /// <param name="windowSize"></param>
        /// <returns></returns>
        private static float[] GenerateSuppressionVector(int windowSize)
        {
            float[] returnVal = new float[windowSize];
            for (int c = 0; c < windowSize; c++)
            {
                returnVal[c] = (float)Math.Pow((12 / Math.Pow((c + 1), 3)), 2);
            }
            return returnVal;
        }

        private static void GetVector(short[] window, float[] suppressionVector, float[] targetArray, int targetOffset)
        {
            ComplexF[] inputVector = new ComplexF[window.Length];

            for (int c = 0; c < WINDOW_SIZE; c++)
            {
                float real = (float)window[c] / (float)short.MaxValue;
                // Apply a window function
                real = real * (float)AudioMath.NuttallWindow((double)c / WINDOW_SIZE);
                inputVector[c] = new ComplexF(real, 0);
            }
            
            float normalizer = 15f;
            float averageDecay = 0.99f;

            Fourier.FFT(inputVector, FourierDirection.Forward);

            for (int c = 0; c < SPECTRUM_HEIGHT; c++)
            {
                targetArray[targetOffset + c] = inputVector[c].GetModulus();
                if (suppressionVector != null)
                {
                    // Apply the "noise filter" using a moving average array
                    suppressionVector[c] = (suppressionVector[c] * averageDecay) + (targetArray[targetOffset + c] * (1 - averageDecay));
                    targetArray[targetOffset + c] = Math.Max(0, targetArray[targetOffset + c] - suppressionVector[c]) / normalizer;
                }
            }
        }

        private static void MemMove<T>(T[] array, int src_idx, int dst_idx, int length)
        {
            if (src_idx == dst_idx || length == 0)
                return;

            // Do regions overlap?
            if (src_idx + length > dst_idx || dst_idx + length > src_idx)
            {
                // Take extra precautions
                if (dst_idx < src_idx)
                {
                    // Copy forwards
                    for (int c = 0; c < length; c++)
                    {
                        array[c + dst_idx] = array[c + src_idx];
                    }
                }
                else
                {
                    // Copy backwards
                    for (int c = length - 1; c >= 0; c--)
                    {
                        array[c + dst_idx] = array[c + src_idx];
                    }
                }
            }
            else
            {
                // Memory regions cannot overlap; just do a fast copy
                Array.Copy(array, src_idx, array, dst_idx, length);
            }
        }
    }
}
