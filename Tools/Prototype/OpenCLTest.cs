//using Cloo;
//using Durandal.Common.MathExt;
//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Linq;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;

//namespace Prototype
//{
//    public class OpenCLTest
//    {
//        public static void RunTests()
//        {
//            int numThreads = 16;
//            int numWorkItems = 1;
//            ThreadPool.SetMaxThreads(numThreads, numThreads);
//            Stopwatch timer = new Stopwatch();
//            timer.Start();
//            IList<ComputeRunner> threads = new List<ComputeRunner>();
//            for (int c = 0; c < numWorkItems; c++)
//            {
//                ComputeRunner r = new ComputeRunner();
//                threads.Add(r);
//                ThreadPool.QueueUserWorkItem(r.Run, null);
//            }
//            foreach (ComputeRunner r in threads)
//            {
//                r.Join();
//            }

//            timer.Stop();
//            Console.WriteLine("Test done in " + timer.ElapsedMilliseconds + "ms");
//        }

//        private class ComputeRunner
//        {
//            private EventWaitHandle _finished;
            
//            public ComputeRunner()
//            {
//                _finished = new ManualResetEvent(false);
//            }

//            public void Run(object dummy = null)
//            {
//                ComputeContext CL = null;
//                try
//                {
//                    if (ComputePlatform.Platforms.Count > 0)
//                    {
//                        CL = new ComputeContext(ComputeDeviceTypes.All,
//                                                new ComputeContextPropertyList(ComputePlatform.Platforms[0]),
//                                                null, IntPtr.Zero);

//                        ComputeBuffer<float> _inputABuffer;
//                        ComputeBuffer<float> _inputBBuffer;
//                        ComputeBuffer<float> _outputBuffer;
//                        ComputeProgram _clProgram;
//                        ComputeKernel _kernelVectorAdd;
//                        ComputeCommandQueue _commandQueue;

//                        int vectorLength = 1000000;
//                        float[] inputA = new float[vectorLength];
//                        float[] inputB = new float[vectorLength];
//                        IRandom rand = new FastRandom();
//                        for (int c = 0; c < vectorLength; c++)
//                        {
//                            inputA[c] = (float)rand.NextDouble();
//                            inputB[c] = (float)rand.NextDouble();
//                        }
//                        _inputABuffer = new ComputeBuffer<float>(CL, ComputeMemoryFlags.ReadOnly | ComputeMemoryFlags.CopyHostPointer, inputA);
//                        _inputBBuffer = new ComputeBuffer<float>(CL, ComputeMemoryFlags.ReadOnly | ComputeMemoryFlags.CopyHostPointer, inputB);
//                        _outputBuffer = new ComputeBuffer<float>(CL, ComputeMemoryFlags.WriteOnly, (long)vectorLength);

//                        _clProgram = new ComputeProgram(CL, OpenCLProgram);
//                        _clProgram.Build(null, "-cl-mad-enable -cl-no-signed-zeros", null, IntPtr.Zero);
//                        _kernelVectorAdd = _clProgram.CreateKernel("VectorAdd");
//                        _kernelVectorAdd.SetMemoryArgument(0, _inputABuffer);
//                        _kernelVectorAdd.SetMemoryArgument(1, _inputBBuffer);
//                        _kernelVectorAdd.SetMemoryArgument(2, _outputBuffer);
//                        //_kernelCalculateDifference.SetValueArgument(4, _staticImage.Width);

//                        _commandQueue = new ComputeCommandQueue(CL, CL.Devices[0], ComputeCommandQueueFlags.None);

//                        float[] output = new float[vectorLength];

//                        Stopwatch timer = new Stopwatch();
//                        timer.Start();
//                        for (int c = 0; c < 100; c++)
//                        {
//                            _commandQueue.Execute(_kernelVectorAdd, new long[] { 0 }, new long[] { vectorLength }, new long[] { 1 }, null);
//                        }
//                        _commandQueue.ReadFromBuffer(_outputBuffer, ref output, true, null);
//                        _commandQueue.Finish();
//                        timer.Stop();
//                        Console.WriteLine("Work item complete in " + timer.ElapsedMilliseconds + "ms");

//                        _inputABuffer.Dispose();
//                        _inputBBuffer.Dispose();
//                        _outputBuffer.Dispose();
//                        _clProgram.Dispose();
//                        _kernelVectorAdd.Dispose();
//                        _commandQueue.Dispose();
//                    }
//                }
//                catch (TypeInitializationException e)
//                {
//                    Console.WriteLine(e.Message);
//                }
//                catch (ComputeException e)
//                {
//                    Console.WriteLine(e.Message);
//                }
//                finally
//                {
//                    if (CL != null)
//                    {
//                        CL.Dispose();
//                    }

//                    _finished.Set();
//                }
//            }

//            public void Join()
//            {
//                _finished.WaitOne();
//            }
//        }

//        private const string OpenCLProgram = @"
//kernel void VectorAdd(
//    global read_only float* inputA,
//    global read_only float* inputB,
//    global write_only float* output)
//{
//    int index = get_global_id(0);
//    output[index] = native_exp(inputA[index]) + native_log(inputB[index]);
//}
//";

//    }
//}