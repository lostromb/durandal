using Durandal.Common.Audio;
using Durandal.Common.Audio.FFT;
using Durandal.Common.NLP.NeuralNet;
using Durandal.Common.Speech.Triggers;
using Stromberg.Logger;
using Stromberg.Utils;
using Stromberg.Utils.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TriggerModelTrainer
{
    public class Program
    {
        private const int WINDOW_SIZE = 512;
        private const int LAP_INCREMENT = 256;
        private const int HISTORY_LENGTH = 30;
        private const int SPECTRUM_HEIGHT = 64;
        private const int NUM_INPUTS = HISTORY_LENGTH * SPECTRUM_HEIGHT;

        public static void Main(string[] args)
        {
            ILogger logger = new ConsoleLogger();
            IResourceManager resourceManager = new FileResourceManager(logger);
            ResourceName modelFile = new ResourceName("model.json");
            TrainModel(logger, resourceManager, modelFile);
            EvaluateModel(logger, resourceManager, modelFile);
        }

        private static void EvaluateModel(ILogger logger, IResourceManager resourceManager, ResourceName modelFile)
        {
            NetworkData model = NetworkData.ReadFromFile(resourceManager, modelFile);
            BackPropogationNetwork validationNetwork = new BackPropogationNetwork(model);
            DirectoryInfo positiveValidationDir = new DirectoryInfo(@"E:\Audio Data\TrainingDurandalBig");
            DirectoryInfo negativeValidationDir = new DirectoryInfo(@"E:\Audio Data\ValidationBad");

            logger.Log("Starting validation...");
            StaticAverage posAvg = new StaticAverage();
            foreach (FileInfo file in positiveValidationDir.EnumerateFiles("*.wav", SearchOption.TopDirectoryOnly))
            {
                float score = ValidateFile(file, validationNetwork);
                posAvg.Add(score);
                logger.Log("POSITIVE: " + score + "\t" + posAvg.Average + "\t" + file.Name);
            }
            StaticAverage negAvg = new StaticAverage();
            foreach (FileInfo file in negativeValidationDir.EnumerateFiles("*.wav", SearchOption.TopDirectoryOnly))
            {
                float score = ValidateFile(file, validationNetwork);
                negAvg.Add(score);
                logger.Log("NEGATIVE: " + score + "\t" + negAvg.Average + "\t" + file.Name);
            }
            logger.Log("Done.");
        }

        private static void TrainModel(ILogger logger, IResourceManager resourceManager, ResourceName modelFile)
        {
            BackPropogationNetwork trainingNetwork = new BackPropogationNetwork(HISTORY_LENGTH * SPECTRUM_HEIGHT, 1, HISTORY_LENGTH, 4, 4);
            List<DataSet> negative = BuildNegativeTrainingDataSet(new DirectoryInfo(@"E:\Audio Data\Long-Form Audio 2"));
            List<DataSet> positive = BuildPositiveTrainingDataSet(new DirectoryInfo(@"E:\Audio Data\TrainingDurandal"));
            int negativeTrainingInstances = positive.Count * 25;
            logger.Log("Starting training...");
            int iterations = 1000;
            float learningRate = 0.5f;
            float momentum = 0.1f;
            TaskFactory taskFactory = new TaskFactory();
            for (int i = 0; i < iterations; i++)
            {
                logger.Log("Iteration " + i);
                float iterPercent = (float)i / iterations;
                for (int c = 0; c < negativeTrainingInstances; c++)
                {
                    DataSet data = negative[RandomProvider.random.Next(0, negative.Count)];
                    trainingNetwork.BackPropagate(taskFactory, data, (1 - iterPercent) * learningRate, momentum);
                }
                foreach (DataSet data in positive)
                {
                    trainingNetwork.BackPropagate(taskFactory, data, (1 - iterPercent) * learningRate, momentum);
                }
                trainingNetwork.GetNetworkData().WriteToFile(resourceManager, modelFile);
            }
            logger.Log("Done.");
        }

        private static List<DataSet> BuildNegativeTrainingDataSet(DirectoryInfo negativeTrainingDir)
        {
            List<DataSet> returnVal = new List<DataSet>();
            foreach (FileInfo file in negativeTrainingDir.EnumerateFiles("*.wav", SearchOption.TopDirectoryOnly))
            {
                ExtractNegativeVectorsFromFile(file, returnVal);
            }
            return returnVal;
        }

        private static List<DataSet> BuildPositiveTrainingDataSet(DirectoryInfo positiveTrainingDir)
        {
            List<DataSet> returnVal = new List<DataSet>();
            foreach (FileInfo file in positiveTrainingDir.EnumerateFiles("*.raw", SearchOption.TopDirectoryOnly))
            {
                ExtractPositiveVectorsFromFile(file, returnVal);
            }
            return returnVal;
        }

        private static void ExtractNegativeVectorsFromFile(FileInfo fileName, List<DataSet> destination)
        {
            float[] outcomeVector = new float[] { 0 };
            float[] trainingBuf = new float[NUM_INPUTS];
            short[] window = new short[WINDOW_SIZE];
            float[] suppression = GenerateSuppressionVector(WINDOW_SIZE);
            int cursor = 0;
            AudioChunk audio = AudioChunkFactory.CreateFromFile(fileName.FullName);
            while (cursor < audio.DataLength - LAP_INCREMENT)
            {
                MemMove(window, LAP_INCREMENT, 0, WINDOW_SIZE - LAP_INCREMENT);
                Array.Copy(audio.Data, cursor, window, WINDOW_SIZE - LAP_INCREMENT, LAP_INCREMENT);
                MemMove(trainingBuf, SPECTRUM_HEIGHT, 0, NUM_INPUTS - SPECTRUM_HEIGHT);
                GetVector(window, null, trainingBuf, NUM_INPUTS - SPECTRUM_HEIGHT);
                float[] trainingVector = new float[NUM_INPUTS];
                Array.Copy(trainingBuf, 0, trainingVector, 0, NUM_INPUTS);
                destination.Add(new DataSet() { Inputs = trainingVector, Outputs = outcomeVector });
                cursor += LAP_INCREMENT;
            }
        }

        private static void ExtractPositiveVectorsFromFile(FileInfo fileName, List<DataSet> destination)
        {
            //SpectralImage image = new SpectralImage();
            float[] outcomeVector = new float[] { 1 };
            float[] trainingVector = new float[NUM_INPUTS];
            short[] window = new short[WINDOW_SIZE];
            float[] suppression = GenerateSuppressionVector(WINDOW_SIZE);
            int cursor = 0;
            AudioChunk audio = new AudioChunk(File.ReadAllBytes(fileName.FullName), 16000);
            while (cursor < audio.DataLength - LAP_INCREMENT)
            {
                MemMove(window, LAP_INCREMENT, 0, WINDOW_SIZE - LAP_INCREMENT);
                Array.Copy(audio.Data, cursor, window, WINDOW_SIZE - LAP_INCREMENT, LAP_INCREMENT);
                MemMove(trainingVector, SPECTRUM_HEIGHT, 0, NUM_INPUTS - SPECTRUM_HEIGHT);
                GetVector(window, null, trainingVector, NUM_INPUTS - SPECTRUM_HEIGHT);
                /*float[] thisVec = new float[SPECTRUM_HEIGHT];
                Array.Copy(trainingVector, NUM_INPUTS - SPECTRUM_HEIGHT, thisVec, 0, SPECTRUM_HEIGHT);
                image.Add(thisVec);*/
                cursor += LAP_INCREMENT;
            }
            destination.Add(new DataSet() { Inputs = trainingVector, Outputs = outcomeVector });
            //image.WriteAsImage(fileName.FullName + ".png");
        }

        private static float ValidateFile(FileInfo fileName, BackPropogationNetwork network)
        {
            float highestScore = 0;
            float[] inputVector = new float[NUM_INPUTS];
            short[] window = new short[WINDOW_SIZE];
            float[] suppression = GenerateSuppressionVector(WINDOW_SIZE);
            int cursor = 0;
            AudioChunk audio = AudioChunkFactory.CreateFromFile(fileName.FullName);
            while (cursor < audio.DataLength - LAP_INCREMENT)
            {
                MemMove(window, LAP_INCREMENT, 0, WINDOW_SIZE - LAP_INCREMENT);
                Array.Copy(audio.Data, cursor, window, WINDOW_SIZE - LAP_INCREMENT, LAP_INCREMENT);
                MemMove(inputVector, SPECTRUM_HEIGHT, 0, NUM_INPUTS - SPECTRUM_HEIGHT);
                GetVector(window, null, inputVector, NUM_INPUTS - SPECTRUM_HEIGHT);
                network.ApplyInput(inputVector);
                network.CalculateOutput();
                float thisTrig = network.ReadSingleOutput();
                highestScore = Math.Max(thisTrig, highestScore);
                cursor += LAP_INCREMENT;
            }

            return highestScore;
        }

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
            ComplexF[] inputVector = new ComplexF[WINDOW_SIZE];

            for (int c = 0; c < WINDOW_SIZE; c++)
            {
                float real = (float)window[c] / (float)short.MaxValue;
                // Apply a window function
                real = real * (float)AudioMath.NuttallWindow((double)c / WINDOW_SIZE);
                inputVector[c] = new ComplexF(real, 0);
            }
            
            float averageDecay = 0.99f;

            Fourier.FFT(inputVector, FourierDirection.Forward);

            for (int c = 0; c < SPECTRUM_HEIGHT; c++)
            {
                targetArray[targetOffset + c] = inputVector[c].GetModulus();
                if (suppressionVector != null)
                {
                    // Apply the "noise filter" using a moving average array
                    suppressionVector[c] = (suppressionVector[c] * averageDecay) + (targetArray[targetOffset + c] * (1 - averageDecay));
                    targetArray[targetOffset + c] = Math.Max(0, targetArray[targetOffset + c] - suppressionVector[c]);
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
