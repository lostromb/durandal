using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Durandal.Common.Audio
{
    using Durandal.Common.Audio.Interfaces;

    using Stromberg.Utils;

    public class Evolution
    {
        public static void EvolveUtteranceThresholds()
        {
            int poolSize = 200;
            ThreadPool.SetMaxThreads(poolSize, 12);
            Random rand = new Random();
            ListenerParams seed = ListenerParams.Seed();
            ListenerParams[] pool = new ListenerParams[poolSize];
            for (int c = 0; c < poolSize; c++)
            {
                pool[c] = seed.Mutate(rand);
            }
            for (int pass = 0; pass < 100; pass++)
            {
                Console.WriteLine("Evolution generation " + pass);
                // Run all tests
                WorkAtom[] atoms = new WorkAtom[poolSize];
                for (int c = 0; c < poolSize; c++)
                {
                    atoms[c] = new WorkAtom(pool[c]);
                    ThreadPool.QueueUserWorkItem(atoms[c].TestOneMutation);
                }
                for (int c = 0; c < poolSize; c++)
                {
                    atoms[c].Join();
                }

                // Sort the specimens by score
                Array.Sort(pool);

                // Delete the least fit ones and mutate the champions
                int championCutoff = (int)((double)poolSize * 0.25);
                for (int c = championCutoff; c < poolSize; c++)
                {
                    int parent = rand.Next(0, championCutoff);
                    pool[c] = pool[parent].Mutate(rand);
                }
                Console.WriteLine("Best candidate:");
                Console.WriteLine(pool[0]);
            }
        }

        private class WorkAtom
        {
            private ListenerParams parameters;
            private EventWaitHandle handle;

            public WorkAtom(ListenerParams p)
            {
                parameters = p;
                handle = new EventWaitHandle(false, EventResetMode.AutoReset);
            }

            public void TestOneMutation(object dummy)
            {
                int idealDelay = 1500;
                IList<int> deviations = new List<int>();
                deviations.Add(RunTest("Test1.wav", 4034 + idealDelay, parameters));
                deviations.Add(RunTest("Test2.wav", 1459 + idealDelay, parameters));
                deviations.Add(RunTest("Test3.wav", 4195 + idealDelay, parameters));
                deviations.Add(RunTest("Test4.wav", 3117 + idealDelay, parameters));
                deviations.Add(RunTest("Test5.wav", 4317 + idealDelay, parameters));
                deviations.Add(RunTest("Test6.wav", 2909 + idealDelay, parameters));
                deviations.Add(RunTest("Test7.wav", 4432 + idealDelay, parameters));
                deviations.Add(RunTest("Test8.wav", 2301 + idealDelay, parameters));
                deviations.Add(RunTest("Test9.wav", 1348 + idealDelay, parameters));
                deviations.Add(RunTest("Test10.wav", 8104 + idealDelay, parameters));
                deviations.Add(RunTest("Test11.wav", 4836 + idealDelay, parameters));
                deviations.Add(RunTest("Test12.wav", 1751 + idealDelay, parameters));
                deviations.Add(RunTest("Test13.wav", 4743 + idealDelay, parameters));
                deviations.Add(RunTest("Test14.wav", 1137 + idealDelay, parameters));
                deviations.Add(RunTest("Test15.wav", 1031 + idealDelay, parameters));
                deviations.Add(RunTest("Test16.wav", 2917 + idealDelay, parameters));
                deviations.Add(RunTest("Silence1.wav", 0, parameters));
                deviations.Add(RunTest("Silence2.wav", 0, parameters));
                deviations.Add(RunTest("Silence3.wav", 0, parameters));
                deviations.Add(RunTest("Silence4.wav", 0, parameters));
                deviations.Add(RunTest("Silence5.wav", 0, parameters));
                int avgDeviation = (int)deviations.Average();
                int maxDeviation = deviations.Max();
                parameters.Score = maxDeviation + avgDeviation;
                handle.Set();
            }

            public void Join()
            {
                handle.WaitOne();
            }
        }

        private static int RunTest(string fileName, int expectedSampleLengthMs, ListenerParams parameters)
        {
            //Console.WriteLine("Testing " + fileName);
            AudioChunk inputData = new AudioChunk(fileName);
            IMicrophone testMic = new MockMicrophone(inputData);
            AudioChunk result = RecordUtterance(testMic, fileName + ".csv", parameters);
            int thisLength = 0;
            if (result != null)
            {
                thisLength = (int)result.Length.TotalMilliseconds;
                //result.WriteToFile(fileName.Replace(".wav", "_captured.wav"));
            }
            int deviation = thisLength - expectedSampleLengthMs;
            //Console.WriteLine("Captured " + thisLength + "ms (deviation = " + deviation + "ms)");
            return Math.Abs(deviation);
        }

        public class ListenerParams : IComparable
        {
            public double InitialVolume;
            public int SlowerAverageWindow;
            public int SlowAverageWindow;
            public int FastAverageWindow;
            public double UtteranceStartVelocity;
            public double UtteranceEndThreshold;
            public double SilenceThreshold;
            public int MaxMsOfSilence;
            public double MinVolume;
            public int Score;

            public static ListenerParams Seed()
            {
                return new ListenerParams()
                {
                    InitialVolume = 1088,
                    SlowerAverageWindow = 22,
                    SlowAverageWindow = 10,
                    FastAverageWindow = 4,
                    UtteranceStartVelocity = 0.279839516992572,
                    UtteranceEndThreshold = 0.316104528934743,
                    SilenceThreshold = 0.0926201062323151,
                    MaxMsOfSilence = 992,
                    MinVolume = 230.067475249317,
                    Score = 1000000
                };
            }

            public int CompareTo(object obj)
            {
                ListenerParams other = obj as ListenerParams;
                if (other == null)
                    return -1;
                return Score - other.Score;
            }

            public ListenerParams Mutate(Random rand)
            {
                ListenerParams returnVal = new ListenerParams();
                returnVal.InitialVolume = Mutate(rand, InitialVolume);
                returnVal.SlowerAverageWindow = Mutate(rand, SlowerAverageWindow);
                returnVal.SlowAverageWindow = Mutate(rand, SlowAverageWindow);
                returnVal.FastAverageWindow = Mutate(rand, FastAverageWindow);
                returnVal.UtteranceStartVelocity = Mutate(rand, UtteranceStartVelocity);
                returnVal.UtteranceEndThreshold = Mutate(rand, UtteranceEndThreshold);
                returnVal.SilenceThreshold = Mutate(rand, SilenceThreshold);
                returnVal.MaxMsOfSilence = Mutate(rand, MaxMsOfSilence);
                returnVal.MinVolume = Mutate(rand, MinVolume);
                returnVal.Score = 1000000;
                return returnVal;
            }

            public double Mutate(Random rand, double input)
            {
                if (rand.NextDouble() > 0.4)
                {
                    return input;
                }

                double factor;
                if (input > 10)
                {
                    factor = (rand.NextDouble() * 0.4) + 0.8;
                }
                else
                {
                    factor = rand.NextDouble() + 0.5;
                }
                return input * factor;
            }

            public int Mutate(Random rand, int input)
            {
                return (int)(Mutate(rand, (double)input));
            }

            public override string ToString()
            {
                return string.Format("Parameters:\r\nInitialVolume={0}\r\nSlowerAverageWindow={1}\r\nSlowAverageWindow={2}\r\nFastAverageWindow={3}\r\nUtteranceStartVelocity={4}\r\nUtteranceEndThreshold={5}\r\nSilenceThreshold={6}\r\nMaxMsOfSilence={7}\r\nMinVolume={8}\r\nScore={9}", InitialVolume,
                    SlowerAverageWindow,
                    SlowAverageWindow,
                    FastAverageWindow,
                    UtteranceStartVelocity,
                    UtteranceEndThreshold,
                    SilenceThreshold,
                    MaxMsOfSilence,
                    MinVolume,
                    Score);
            }
        }

        public static AudioChunk RecordUtterance(IMicrophone audioSource, string fileName, ListenerParams parameters)
        {
            const int INCREMENT_SIZE = 50;
            const int MAX_INITIAL_WAIT_TIME = 4000;
            BucketAudioStream bucket = new BucketAudioStream();
            MovingAverage fastAverage = new MovingAverage(parameters.FastAverageWindow, parameters.InitialVolume);
            MovingAverage slowAverage = new MovingAverage(parameters.SlowAverageWindow, parameters.InitialVolume);
            MovingAverage slowerAverage = new MovingAverage(parameters.SlowerAverageWindow, parameters.InitialVolume);
            StaticAverage overallVolume = new StaticAverage();
            int msOfSilence = 0;

            audioSource.ClearBuffers();
            bool started = false;
            bool ended = false;
            int pos = 0;
            int startTime = 0;
            int endTime = 0;
            //using (StreamWriter csvOut = new StreamWriter(fileName))
            {
                //csvOut.WriteLine("pos, overallVolume, curVolume, velocity, fastAverage, slowAverage, slowerAverage");
                while (!ended)
                {
                    AudioChunk chunk = audioSource.ReadMicrophone(TimeSpan.FromMilliseconds(INCREMENT_SIZE));
                    if (chunk == null)
                    {
                        break; // Reached end of input, or the microphone had an error or something
                    }
                    bucket.Write(chunk.Data);
                    double curVolume = chunk.Volume();
                    overallVolume.Add(chunk.Volume());
                    double volumeFactor = Math.Max(parameters.MinVolume, overallVolume.Average);
                    fastAverage.Add(curVolume);
                    slowAverage.Add(curVolume);
                    slowerAverage.Add(curVolume);
                    double velocity = fastAverage.Average - slowAverage.Average;
                    //csvOut.WriteLine("{0},{1},{2},{3},{4},{5},{6}", pos, overallVolume, curVolume, velocity, fastAverage, slowAverage, slowerAverage);
                    pos += INCREMENT_SIZE;

                    // Is there silence?
                    if (Math.Abs(velocity) < volumeFactor * parameters.SilenceThreshold)
                    {
                        msOfSilence += INCREMENT_SIZE;
                    }
                    else
                    {
                        msOfSilence = 0;
                    }

                    // Detect silence or extended hesitation
                    if (!started && pos > MAX_INITIAL_WAIT_TIME)
                    {
                        break;
                    }
                    // Detect the start
                    if (!started && velocity > volumeFactor * parameters.UtteranceStartVelocity)
                    {
                        started = true;
                        startTime = pos;
                    }
                    // Detect the end, either by rapid waveform decay
                    else if (started &&
                        slowerAverage.Average > fastAverage.Average &&
                        slowerAverage.Average > slowAverage.Average &&
                        slowerAverage.Average < volumeFactor * parameters.UtteranceEndThreshold)
                    {
                        ended = true;
                        endTime = pos;
                    }
                    // or by prolonged silence
                    else if (pos > MAX_INITIAL_WAIT_TIME && overallVolume.Average < parameters.MinVolume)
                    {
                        ended = true;
                    }
                    else if (started && msOfSilence > parameters.MaxMsOfSilence)
                    {
                        ended = true;
                        endTime = pos;
                    }
                }
                //csvOut.Close();
            }

            /*Console.WriteLine("StartTime: " + startTime);
            Console.WriteLine("EndTime: " + endTime);
            Console.WriteLine(overallVolume.Average);*/
            //Console.WriteLine(pos);

            if (!started || !ended || endTime < startTime)
            {
                // No utterance
                return null;
            }

            //Console.WriteLine("Length: " + (endTime - startTime));

            AudioChunk finalChunk = new AudioChunk(bucket.GetAllData(), audioSource.GetSampleRate()).Normalize();
            return finalChunk;
        }
    }
}
