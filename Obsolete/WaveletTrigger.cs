using Durandal.Common.Audio;
using Durandal.Common.Audio.FFT;
using Durandal.Common.Client;
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
    public class WaveletTrigger : IAudioTrigger
    {
        private const int WINDOW_SIZE = 512;
        private const int SAMPLE_RATE = AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE;
        private float _sensitivity;
        private SpectralImage _runningImage = new SpectralImage();
        private readonly float[] _movingAverage;
        private readonly ILogger _logger;
        private readonly IList<WaveletDecomposition> _triggerImages = new List<WaveletDecomposition>();
        private readonly BasicBufferShort _audioIn = new BasicBufferShort(SAMPLE_RATE * 3);

        public WaveletTrigger(ILogger logger, IResourceManager resourceManager, ResourceName triggerDirectory, float sensitivity = 0.0f)
        {
            _logger = logger;
            _movingAverage = SpectralImage.GenerateSuppressionVector(WINDOW_SIZE);
            _sensitivity = sensitivity;

            if (!resourceManager.Exists(triggerDirectory) || !resourceManager.IsContainer(triggerDirectory))
            {
                _logger.Log("Trigger directory \"" + triggerDirectory + "\" does not exist or is not a directory!", LogLevel.Err);
            }
            else
            {
                _logger.Log("Loading trigger words...");

                foreach (ResourceName file in resourceManager.ListResources(triggerDirectory))
                {
                    if (file.Extension.Equals(".wav", StringComparison.OrdinalIgnoreCase))
                    {
                        SpectralImage spectrum = SpectralImage.BuildFromFile(resourceManager, file);
                        _triggerImages.Add(HaarWavelet.Transform(spectrum.ToMatrix()));
                    }
                }

                _logger.Log("Loaded " + _triggerImages.Count + " trigger words");
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
        }

        public bool Try(AudioChunk audio)
        {
            NoOp(audio);

            bool triggered = false;
            while (_audioIn.Available() > WINDOW_SIZE)
            {
                short[] sample = _audioIn.Read(WINDOW_SIZE);
                triggered = TryInternal(sample) || triggered;
            }

            return triggered;
        }

        public void Reset()
        {
            _runningImage = new SpectralImage();
            _audioIn.Clear();
        }

        private bool TryInternal(short[] sample)
        {
            float[] magnitudes = SpectralImage.GetVector(sample, _movingAverage);
            _runningImage.Add(magnitudes);
            if (_runningImage.Length < 20)
            {
                return false;
            }

            float minimumDiff = float.MaxValue;
            StaticAverage averageDiff = new StaticAverage();
            WaveletDecomposition inputVector = HaarWavelet.Transform(_runningImage.ToMatrix());
            foreach (WaveletDecomposition triggerImage in _triggerImages)
            {
                float diff = triggerImage.CalculateDifference(inputVector, 8);
                averageDiff.Add(diff);
                minimumDiff = Math.Min(diff, minimumDiff);
            }
            //Console.WriteLine("Min: " + minimumDiff + "\t Avg: " + averageDiff.Average);
            //Console.Write(Visualize(magnitudes));

            // These numbers control the sensitivity of the trigger.
            // Negative samples: mean = 0.1365 dev = 0.0258
            // Positive samples: mean = 0.0732 dev = 0.01577
            float minThresh = 0.1107f + (_sensitivity * 0.0258f);
            // Negative samples: mean = 0.1609 dev = 0.0231
            // Positive samples: mean = 0.1138 dev = 0.01534
            float avgThresh = 0.1378f + (_sensitivity * 0.0231f);

            curMeasure = Math.Min(curMeasure, (float)minimumDiff);

            //return minimumDiff < minThresh;
            //return averageDiff.Average < avgThresh;
            return minimumDiff < minThresh && averageDiff.Average < avgThresh;
        }

        private IList<float> measures = new List<float>();
        private float curMeasure = float.MaxValue;
        private StaticAverage mean = new StaticAverage();

        public void Measure()
        {
            // Gather statistics
            if (curMeasure < float.MaxValue)
            {
                measures.Add(curMeasure);
                mean.Add(curMeasure);
                // Calculate mean and deviation
                float u = 0;
                float m = (float)mean.Average;
                foreach (float x in measures)
                {
                    u += ((m - x) * (m - x)) / measures.Count;
                }
                u = (float)Math.Sqrt(u);
                Console.WriteLine("Mean: " + m + " StDev: " + u);
            }

            curMeasure = float.MaxValue;
        }

        public void Dispose() { }

        // For debugging - returns a string that visualizes this vector on the console
        private static string Visualize(float[] input)
        {
            string output = string.Empty;
            int width = Math.Min(120, input.Length);
            for (int bucket = 0; bucket < width; bucket++)
            {
                float start = (float)bucket * (float)input.Length / width;
                float end = (float)(bucket + 1) * (float)input.Length / width;
                float sum = 0;
                float numBuckets = 0;
                for (int t = (int)start; t < (int)end && t < input.Length; t++)
                {
                    sum += input[t];
                    numBuckets += 1;
                }
                if (numBuckets > 0)
                {
                    sum = sum / numBuckets;
                }

                if (sum < 0.2)
                    output += ' ';
                else if (sum < 0.4)
                    output += '░';
                else if (sum < 0.6)
                    output += '▒';
                else if (sum < 0.8)
                    output += '▓';
                else
                    output += '█';
            }
            for (int c = width; c < 120; c++)
            {
                output += '|';
            }

            return output;
        }
    }
}
