using Durandal.Common.Audio;
using Durandal.Common.Audio.Interfaces;
using Durandal.Common.Client;
using Durandal.Common.Utils;
using Stromberg.Logger;
using Stromberg.Utils;
using Stromberg.Utils.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers
{
    public class MarkovTrigger : IAudioTrigger
    {
        private const int SAMPLE_RATE = AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE;
        private const int CHUNK_SIZE = SAMPLE_RATE / 10;
        private readonly BasicBufferShort _audioIn = new BasicBufferShort(SAMPLE_RATE * 3);
        private BinaryMarkovMatrixModel _model;
        private readonly ILogger _logger;
        private double[] history = new double[20];

        public MarkovTrigger(ILogger logger, string modelFileName)
        {
            _logger = logger;
            _logger.Log("Loading markov model from " + modelFileName);
            _model = new BinaryMarkovMatrixModel(modelFileName);
        }

        public MarkovTrigger(ILogger logger, IResourceManager resourceManager, ResourceName modelFileName)
        {
            if (!resourceManager.Exists(modelFileName))
            {
                logger.Log("Markov trigger model \"" + modelFileName + "\" does not exist!", LogLevel.Err);
                _model = null;
            }
            else
            {
                logger.Log("Loading Markov trigger model from \"" + modelFileName + "\"...");
                _model = new BinaryMarkovMatrixModel(resourceManager.ReadStream(modelFileName));
            }
        }

        public void NoOp(AudioChunk audio)
        {
            if (audio.SampleRate != SAMPLE_RATE)
            {
                _logger.Log("Input audio was not at the expected sample rate " + SAMPLE_RATE + ", resampling...", LogLevel.Wrn);
                //audio = audio.ResampleTo(SAMPLE_RATE);
            }

            _audioIn.Write(audio.Data);

            while (_audioIn.Available() > CHUNK_SIZE)
            {
                AudioChunk nextChunk = new AudioChunk(_audioIn.Read(CHUNK_SIZE), SAMPLE_RATE);
                
                for (int c = 0; c < 19; c++)
                {
                    history[c] = history[c + 1];
                }

                history[19] = nextChunk.Volume() / short.MaxValue;
            }
        }

        public void Reset()
        {
            history = new double[20];
        }

        public bool Try(AudioChunk audio)
        {
            NoOp(audio);
            
            if (_model == null)
            {
                return true;
            }

            return _model.Evaluate(history);
        }

        public void Measure() { }

        public void Dispose() { }
    }

    /*private class MarkovTrainer
    {
        private static ILogger logger = new ConsoleLogger();

        public static void Run()
        {
            IList<double[]> negativeTraining = new List<double[]>();
            IList<double[]> positiveTraining = new List<double[]>();
            IList<double[]> validation = new List<double[]>();

            logger.Log("Loading files");

            foreach (FileInfo file in new DirectoryInfo(@"C:\Users\lostromb\Documents\Visual Studio 2013\Projects\Durandal\Prototype\bin\goodtriggers").EnumerateFiles())
            {
                if (file.Extension.Equals(".wav"))
                {
                    AudioChunk chunk = new AudioChunk(file.FullName);
                    double[] vector = GetVectorFromAudioChunk(chunk);
                    positiveTraining.Add(vector);
                }
            }

            foreach (FileInfo file in new DirectoryInfo(@"C:\Users\lostromb\Documents\Visual Studio 2013\Projects\Durandal\Prototype\bin\badtriggers").EnumerateFiles())
            {
                if (file.Extension.Equals(".wav"))
                {
                    AudioChunk chunk = new AudioChunk(file.FullName);
                    double[] vector = GetVectorFromAudioChunk(chunk);
                    negativeTraining.Add(vector);
                }
            }

            foreach (FileInfo file in new DirectoryInfo(@"C:\Users\lostromb\Documents\Visual Studio 2013\Projects\Durandal\Prototype\bin\validtriggers").EnumerateFiles())
            {
                if (file.Extension.Equals(".wav"))
                {
                    AudioChunk chunk = new AudioChunk(file.FullName);
                    double[] vector = GetVectorFromAudioChunk(chunk);
                    validation.Add(vector);
                }
            }

            logger.Log("Training model");

            // Train the matrix
            BinaryMarkovMatrixModel model = new BinaryMarkovMatrixModel(20, 20);
            model.Train(logger.Clone("MarkovModel"), positiveTraining, negativeTraining, validation);

            model.SaveToFile("model.bin");
        }

        private static double[] GetVectorFromAudioChunk(AudioChunk input)
        {
            double[] returnVal = new double[20];

            for (int c = 0; c < 20; c++)
            {
                short[] data = new short[1600];
                Array.Copy(input.Data, 1600 * c, data, 0, 1600);
                AudioChunk slice = new AudioChunk(data, input.SampleRate);
                returnVal[c] = slice.Volume() / short.MaxValue;
            }

            return returnVal;
        }

        public static void ExtractTriggers()
        {
            SAPITrigger trigger = new SAPITrigger("durandal", 0.90f, logger);
            IMicrophone mic = new NAudioMicrophone(8000, 1.6f);
            mic.StartRecording();
            int chunkSizeBytes = (int)(trigger.GetExpectedSliceSize().TotalMilliseconds * 32);
            byte[] buf = new byte[chunkSizeBytes];
            AudioChunk[] history = new AudioChunk[20];
            int fileCount = 0;
            string outDir = @"C:\Users\lostromb\Documents\Visual Studio 2013\Projects\Durandal\Prototype\bin\validtriggers\";

            while (mic.IsRecording())
            {
                AudioChunk nextChunk = mic.ReadMicrophone(trigger.GetExpectedSliceSize()).ResampleToBad(AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE);
                for (int c = 1; c < history.Length; c++)
                {
                    history[c - 1] = history[c];
                }
                history[19] = nextChunk;
                if (trigger.Try(nextChunk))
                {
                    AudioChunk entireAudio = history[0];
                    for (int c = 1; c < history.Length; c++)
                    {
                        entireAudio = entireAudio.Concatenate(history[c]);
                    }
                    entireAudio.WriteToFile(outDir + (fileCount++) + ".wav");
                }
            }

            logger.Log("Done");
        }
    }*/
}
