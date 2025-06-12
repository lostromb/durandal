using Durandal.Common.Collections;
using Durandal.Common.Instrumentation;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.IO
{
    [TestClass]
    public class PipeStreamTests
    {
        [TestMethod]
        public void TestPipeStreamBasic()
        {
            using (PipeStream pipe = new PipeStream())
            using (Stream read = pipe.GetReadStream())
            using (Stream write = pipe.GetWriteStream())
            {
                byte[] writeBuf = new byte[1024];
                byte[] readBuf = new byte[1024];
                IRandom rand = new FastRandom();
                for (int c = 0; c < 100; c++)
                {
                    rand.NextBytes(writeBuf);
                    int length = rand.NextInt(1, writeBuf.Length);
                    write.Write(writeBuf, 0, length);
                    read.ReadExactly(readBuf, 0, length);
                    Assert.IsTrue(ArrayExtensions.ArrayEquals(readBuf, 0, writeBuf, 0, length));
                }
            }
        }
        [TestMethod]
        public async Task TestPipeStreamBasicAsync()
        {
            using (PipeStream pipe = new PipeStream())
            using (Stream read = pipe.GetReadStream())
            using (Stream write = pipe.GetWriteStream())
            using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
            {
                byte[] writeBuf = new byte[1024];
                byte[] readBuf = new byte[1024];
                IRandom rand = new FastRandom();
                for (int c = 0; c < 100; c++)
                {
                    rand.NextBytes(writeBuf);
                    int length = rand.NextInt(1, writeBuf.Length);
                    await write.WriteAsync(writeBuf, 0, length, cts.Token).ConfigureAwait(false);
                    await read.ReadExactlyAsync(readBuf, 0, length, cts.Token).ConfigureAwait(false);
                    Assert.IsTrue(ArrayExtensions.ArrayEquals(readBuf, 0, writeBuf, 0, length));
                }
            }
        }
        [TestMethod]
        public void TestPipeStreamLarge()
        {
            using (PipeStream pipe = new PipeStream())
            using (Stream read = pipe.GetReadStream())
            using (Stream write = pipe.GetWriteStream())
            {
                byte[] writeBuf = new byte[512 * 1024];
                byte[] readBuf = new byte[512 * 1024];
                IRandom rand = new FastRandom();
                for (int c = 0; c < 10; c++)
                {
                    rand.NextBytes(writeBuf);
                    int length = rand.NextInt(1, writeBuf.Length);
                    write.Write(writeBuf, 0, length);
                    read.ReadExactly(readBuf, 0, length);
                    Assert.IsTrue(ArrayExtensions.ArrayEquals(readBuf, 0, writeBuf, 0, length));
                }
            }
        }

        [TestMethod]
        public async Task TestPipeStreamLargeAsync()
        {
            using (PipeStream pipe = new PipeStream())
            using (Stream read = pipe.GetReadStream())
            using (Stream write = pipe.GetWriteStream())
            using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
            {
                byte[] writeBuf = new byte[512 * 1024];
                byte[] readBuf = new byte[512 * 1024];
                IRandom rand = new FastRandom();
                for (int c = 0; c < 10; c++)
                {
                    rand.NextBytes(writeBuf);
                    int length = rand.NextInt(1, writeBuf.Length);
                    await write.WriteAsync(writeBuf, 0, length, cts.Token).ConfigureAwait(false);
                    await read.ReadExactlyAsync(readBuf, 0, length, cts.Token).ConfigureAwait(false);
                    Assert.IsTrue(ArrayExtensions.ArrayEquals(readBuf, 0, writeBuf, 0, length));
                }
            }
        }

        [TestMethod]
        public void TestPipeStreamInvalidStreamFetch()
        {
            using (PipeStream pipe = new PipeStream())
            using (Stream read = pipe.GetReadStream())
            using (Stream write = pipe.GetWriteStream())
            {
                try
                {
                    pipe.GetReadStream();
                    Assert.Fail("Expected an InvalidOperationException");
                }
                catch (InvalidOperationException) { }
                try
                {
                    pipe.GetWriteStream();
                    Assert.Fail("Expected an InvalidOperationException");
                }
                catch (InvalidOperationException) { }
            }
        }

        [TestMethod]
        public void TestPipeStreamDisposalRegular()
        {
            using (PipeStream pipe = new PipeStream())
            {
            }
        }

        [TestMethod]
        public void TestPipeStreamDisposalWriteOnly()
        {
            using (PipeStream pipe = new PipeStream())
            {
                using (Stream write = pipe.GetWriteStream())
                {
                }
            }
        }

        [TestMethod]
        public void TestPipeStreamDisposalReadOnly()
        {
            using (PipeStream pipe = new PipeStream())
            {
                using (Stream read = pipe.GetReadStream())
                {
                }
            }
        }

        [TestMethod]
        public void TestPipeStreamDisposalWriteRead()
        {
            using (PipeStream pipe = new PipeStream())
            {
                using (Stream write = pipe.GetWriteStream())
                {
                    using (Stream read = pipe.GetReadStream())
                    {
                        byte[] dataIn = new byte[100];
                        byte[] dataOut = new byte[100];
                        FastRandom.Shared.NextBytes(dataIn);
                        write.Write(dataIn.AsSpan());
                        read.ReadExactly(dataOut.AsSpan());
                        Assert.IsTrue(ArrayExtensions.ArrayEquals(dataIn, 0, dataOut, 0, 100));
                    }
                }
            }
        }

        [TestMethod]
        public void TestPipeStreamDisposalReadWrite()
        {
            using (PipeStream pipe = new PipeStream())
            {
                using (Stream read = pipe.GetReadStream())
                {
                    using (Stream write = pipe.GetWriteStream())
                    {
                        byte[] dataIn = new byte[100];
                        byte[] dataOut = new byte[100];
                        FastRandom.Shared.NextBytes(dataIn);
                        write.Write(dataIn.AsSpan());
                        read.ReadExactly(dataOut.AsSpan());
                        Assert.IsTrue(ArrayExtensions.ArrayEquals(dataIn, 0, dataOut, 0, 100));
                    }
                }
            }
        }

        [TestMethod]
        public void TestPipeStreamDisposalReadWriteOutOfOrder()
        {
            PipeStream pipe = new PipeStream();
            Stream write = pipe.GetWriteStream();
            Stream read = pipe.GetReadStream();
            pipe.Dispose();
            byte[] dataIn = new byte[100];
            byte[] dataOut = new byte[100];
            FastRandom.Shared.NextBytes(dataIn);
            write.Write(dataIn.AsSpan());
            read.ReadExactly(dataOut.AsSpan());
            Assert.IsTrue(ArrayExtensions.ArrayEquals(dataIn, 0, dataOut, 0, 100));
            read.Dispose();
            write.Dispose();
        }

        [TestMethod]
        public void TestPipeStreamMultipleDisposal()
        {
            using (PipeStream pipe = new PipeStream())
            using (Stream read = pipe.GetReadStream())
            using (Stream write = pipe.GetWriteStream())
            {
                read.Dispose();
                write.Dispose();
                pipe.Dispose();
            }
        }

        [TestMethod]
        public void TestPipeStreamReadProperties()
        {
            using (PipeStream pipe = new PipeStream())
            using (Stream read = pipe.GetReadStream())
            using (Stream write = pipe.GetWriteStream())
            {
                Assert.IsTrue(read.CanRead);
                Assert.IsFalse(read.CanWrite);
                Assert.IsFalse(read.CanSeek);
                byte[] data = new byte[1024];
                write.Write(data, 0, 1024);
                Assert.AreEqual(500, read.Read(data, 0, 500));
                Assert.AreEqual(524, read.Length);
                Assert.AreEqual(500, read.Position);

                try
                {
                    read.Position = 0;
                    Assert.Fail("Expected an InvalidOperationException");
                }
                catch (InvalidOperationException) { }
            }
        }

        [TestMethod]
        public void TestPipeStreamReadClosed()
        {
            using (PipeStream pipe = new PipeStream())
            using (Stream read = pipe.GetReadStream())
            {
                pipe.GetWriteStream().Dispose();
                byte[] data = new byte[1024];
                Assert.AreEqual(0, read.Length);
                Assert.AreEqual(0, read.Position);
                Assert.AreEqual(0, read.Read(data, 0, 1));
            }
        }

        [TestMethod]
        public async Task TestPipeStreamReadInvalidOperations()
        {
            using (PipeStream pipe = new PipeStream())
            using (NonRealTimeStream read = pipe.GetReadStream())
            using (Stream write = pipe.GetWriteStream())
            {
                try
                {
                    read.Seek(0, SeekOrigin.Begin);
                    Assert.Fail("Expected an InvalidOperationException");
                }
                catch (InvalidOperationException) { }
                try
                {
                    read.SetLength(0);
                    Assert.Fail("Expected an InvalidOperationException");
                }
                catch (InvalidOperationException) { }
                try
                {
                    read.Write(new byte[1], 0, 1);
                    Assert.Fail("Expected an InvalidOperationException");
                }
                catch (InvalidOperationException) { }
                try
                {
                    read.Write(new byte[1], 0, 1, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.Fail("Expected an InvalidOperationException");
                }
                catch (InvalidOperationException) { }
                try
                {
                    await read.WriteAsync(new byte[1], 0, 1, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.Fail("Expected an InvalidOperationException");
                }
                catch (InvalidOperationException) { }
            }
        }

        [TestMethod]
        public void TestPipeStreamWriteProperties()
        {
            using (PipeStream pipe = new PipeStream())
            using (Stream read = pipe.GetReadStream())
            using (Stream write = pipe.GetWriteStream())
            {
                Assert.IsFalse(write.CanRead);
                Assert.IsTrue(write.CanWrite);
                Assert.IsFalse(write.CanSeek);
                byte[] data = new byte[1024];
                write.Write(data, 0, 1024);
                Assert.AreEqual(1024, write.Length);
                Assert.AreEqual(1024, write.Position);

                try
                {
                    write.Position = 0;
                    Assert.Fail("Expected an InvalidOperationException");
                }
                catch (InvalidOperationException) { }
            }
        }

        [TestMethod]
        public async Task TestPipeStreamWriteInvalidOperations()
        {
            using (PipeStream pipe = new PipeStream())
            using (NonRealTimeStream read = pipe.GetReadStream())
            using (NonRealTimeStream write = pipe.GetWriteStream())
            {
                try
                {
                    write.Seek(0, SeekOrigin.Begin);
                    Assert.Fail("Expected an InvalidOperationException");
                }
                catch (InvalidOperationException) { }
                try
                {
                    write.SetLength(0);
                    Assert.Fail("Expected an InvalidOperationException");
                }
                catch (InvalidOperationException) { }
                try
                {
                    write.Read(new byte[1], 0, 1);
                    Assert.Fail("Expected an InvalidOperationException");
                }
                catch (InvalidOperationException) { }
                try
                {
                    write.Read(new byte[1], 0, 1, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.Fail("Expected an InvalidOperationException");
                }
                catch (InvalidOperationException) { }
                try
                {
                    await write.ReadAsync(new byte[1], 0, 1, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.Fail("Expected an InvalidOperationException");
                }
                catch (InvalidOperationException) { }
            }
        }

        [TestMethod]
        public async Task TestPipeStreamReadWriteBlockingNRT()
        {
            ILogger logger = new ConsoleLogger();
            using (PipeStream pipe = new PipeStream())
            using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
            {
                CancellationToken testCancel = cts.Token;
                LockStepRealTimeProvider lockStepTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));
                IRealTimeProvider readTaskTime = lockStepTime.Fork("PipeRead");
                IRealTimeProvider writeTaskTime = lockStepTime.Fork("PipeWrite");

                Task readTask = Task.Run(() =>
                {
                    try
                    {
                        using (NonRealTimeStream readStream = pipe.GetReadStream())
                        {
                            byte[] readBuffer = new byte[1024];
                            int readSize = 1;
                            while (readSize > 0)
                            {
                                readSize = readStream.Read(readBuffer, 0, readBuffer.Length, testCancel, readTaskTime);
                            }
                        }
                    }
                    finally
                    {
                        readTaskTime.Merge();
                    }
                });

                Task writeTask = Task.Run(() =>
                {
                    try
                    {
                        using (NonRealTimeStream writeStream = pipe.GetWriteStream())
                        {
                            IRandom rand = new FastRandom(85410);
                            TimeSpan elapsedTime = TimeSpan.Zero;
                            byte[] writeBuffer = new byte[1200];
                            while (elapsedTime < TimeSpan.FromSeconds(5))
                            {
                                int writeSize = rand.NextInt(1, writeBuffer.Length);
                                rand.NextBytes(writeBuffer, 0, writeSize);
                                writeStream.Write(writeBuffer, 0, writeSize, testCancel, writeTaskTime);
                                writeTaskTime.Wait(TimeSpan.FromMilliseconds(10), testCancel);
                                elapsedTime += TimeSpan.FromMilliseconds(10);
                            }
                        }
                    }
                    finally
                    {
                        writeTaskTime.Merge();
                    }
                });

                lockStepTime.Step(TimeSpan.FromSeconds(10), 50);
                await readTask;
                await writeTask;
            }
        }

        [TestMethod]
        public async Task TestPipeStreamReadWriteBlockingAsyncNRT()
        {
            ILogger logger = new ConsoleLogger();
            using (PipeStream pipe = new PipeStream())
            using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
            {
                CancellationToken testCancel = cts.Token;
                LockStepRealTimeProvider lockStepTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));
                IRealTimeProvider readTaskTime = lockStepTime.Fork("PipeRead");
                IRealTimeProvider writeTaskTime = lockStepTime.Fork("PipeWrite");

                Task readTask = Task.Run(async () =>
                {
                    try
                    {
                        using (NonRealTimeStream readStream = pipe.GetReadStream())
                        {
                            byte[] readBuffer = new byte[1024];
                            int readSize = 1;
                            while (readSize > 0)
                            {
                                readSize = await readStream.ReadAsync(readBuffer, 0, readBuffer.Length, testCancel, readTaskTime).ConfigureAwait(false);
                            }
                        }
                    }
                    finally
                    {
                        readTaskTime.Merge();
                    }
                });

                Task writeTask = Task.Run(async () =>
                {
                    try
                    {
                        using (NonRealTimeStream writeStream = pipe.GetWriteStream())
                        {
                            IRandom rand = new FastRandom(85410);
                            TimeSpan elapsedTime = TimeSpan.Zero;
                            byte[] writeBuffer = new byte[1200];
                            while (elapsedTime < TimeSpan.FromSeconds(5))
                            {
                                int writeSize = rand.NextInt(1, writeBuffer.Length);
                                rand.NextBytes(writeBuffer, 0, writeSize);
                                await writeStream.WriteAsync(writeBuffer, 0, writeSize, testCancel, writeTaskTime).ConfigureAwait(false);
                                await writeTaskTime.WaitAsync(TimeSpan.FromMilliseconds(10), testCancel).ConfigureAwait(false);
                                elapsedTime += TimeSpan.FromMilliseconds(10);
                            }
                        }
                    }
                    finally
                    {
                        writeTaskTime.Merge();
                    }
                });

                lockStepTime.Step(TimeSpan.FromSeconds(10), 50);
                await readTask;
                await writeTask;
            }
        }

        [TestMethod]
        public async Task TestPipeStreamReadWriteBlocking()
        {
            ILogger logger = new ConsoleLogger();
            const int LOOPS = 10000;
            using (Barrier lockStep = new Barrier(3))
            using (PipeStream pipe = new PipeStream())
            using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
            {
                CancellationToken testCancel = cts.Token;

                Task readTask = Task.Run(() =>
                {
                    using (NonRealTimeStream readStream = pipe.GetReadStream())
                    {
                        byte[] readBuffer = new byte[1024];
                        int readSize;
                        for (int loops = 0; loops < LOOPS; loops++)
                        {
                            lockStep.SignalAndWait(testCancel);
                            readSize = readStream.Read(readBuffer, 0, readBuffer.Length, testCancel, DefaultRealTimeProvider.Singleton);
                            Assert.AreNotEqual(0, readSize);
                        }

                        // Write has now closed. Drain the pipe
                        readSize = readStream.Read(readBuffer, 0, readBuffer.Length, testCancel, DefaultRealTimeProvider.Singleton);
                        Assert.AreNotEqual(0, readSize);

                        while (readSize > 0)
                        {
                            readSize = readStream.Read(readBuffer, 0, readBuffer.Length, testCancel, DefaultRealTimeProvider.Singleton);
                        }
                    }
                });

                Task writeTask = Task.Run(() =>
                {
                    using (NonRealTimeStream writeStream = pipe.GetWriteStream())
                    {
                        IRandom rand = new FastRandom(85410);
                        byte[] writeBuffer = new byte[1200];
                        for (int loops = 0; loops < LOOPS; loops++)
                        {
                            int writeSize = rand.NextInt(1, writeBuffer.Length);
                            rand.NextBytes(writeBuffer, 0, writeSize);
                            lockStep.SignalAndWait(testCancel);
                            writeStream.Write(writeBuffer, 0, writeSize, testCancel, DefaultRealTimeProvider.Singleton);
                        }
                    }
                });

                for (int loops = 0; loops < LOOPS; loops++)
                {
                    lockStep.SignalAndWait(testCancel);
                }

                try
                {
                    await readTask;
                }
                catch (OperationCanceledException) { }

                try
                {
                    await writeTask;
                }
                catch (OperationCanceledException) { }

                Assert.IsFalse(testCancel.IsCancellationRequested, "Test ran too long and was aborted");
            }
        }

        [TestMethod]
        public async Task TestPipeStreamReadWriteBlockingAsync()
        {
            ILogger logger = new ConsoleLogger();
            const int LOOPS = 10000;
            using (Barrier lockStep = new Barrier(3))
            using (PipeStream pipe = new PipeStream())
            using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
            {
                CancellationToken testCancel = cts.Token;

                Task readTask = Task.Run(async () =>
                {
                    using (NonRealTimeStream readStream = pipe.GetReadStream())
                    {
                        byte[] readBuffer = new byte[1024];
                        int readSize;
                        for (int loops = 0; loops < LOOPS; loops++)
                        {
                            lockStep.SignalAndWait(testCancel);
                            readSize = await readStream.ReadAsync(readBuffer, 0, readBuffer.Length, testCancel, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                            Assert.AreNotEqual(0, readSize);
                        }

                        // Write has now closed. Drain the pipe
                        readSize = await readStream.ReadAsync(readBuffer, 0, readBuffer.Length, testCancel, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        Assert.AreNotEqual(0, readSize);

                        while (readSize > 0)
                        {
                            readSize = await readStream.ReadAsync(readBuffer, 0, readBuffer.Length, testCancel, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        }
                    }
                });

                Task writeTask = Task.Run(async () =>
                {
                    using (NonRealTimeStream writeStream = pipe.GetWriteStream())
                    {
                        IRandom rand = new FastRandom(85410);
                        byte[] writeBuffer = new byte[1200];
                        for (int loops = 0; loops < LOOPS; loops++)
                        {
                            int writeSize = rand.NextInt(1, writeBuffer.Length);
                            rand.NextBytes(writeBuffer, 0, writeSize);
                            lockStep.SignalAndWait(testCancel);
                            await writeStream.WriteAsync(writeBuffer, 0, writeSize, testCancel, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        }
                    }
                });

                for (int loops = 0; loops < LOOPS; loops++)
                {
                    lockStep.SignalAndWait(testCancel);
                }

                try
                {
                    await readTask;
                }
                catch (OperationCanceledException) { }

                try
                {
                    await writeTask;
                }
                catch (OperationCanceledException) { }

                Assert.IsFalse(testCancel.IsCancellationRequested, "Test ran too long and was aborted");
            }
        }

        [TestMethod]
        public void TestPipeStreamThreadSafeGetReadStream()
        {
            const int THREAD_COUNT = 10;
            ILogger testLogger = new ConsoleLogger();
            using (CancellationTokenSource testCancel = new CancellationTokenSource())
            using (CustomThreadPool pool = new CustomThreadPool(
                NullLogger.Singleton,
                NullMetricCollector.Singleton,
                DimensionSet.Empty,
                threadCount: THREAD_COUNT,
                poolName: "TestThreadPool"))
            using (Barrier barrier = new Barrier(THREAD_COUNT + 1))
            {
                testCancel.CancelAfter(TimeSpan.FromSeconds(10));
                CancellationToken testCancelToken = testCancel.Token;
                for (int loop = 0; loop < 100; loop++)
                {
                    using (PipeStream pipe = new PipeStream())
                    {
                        int successfulReads = 0;
                        for (int c = 0; c < THREAD_COUNT; c++)
                        {
                            pool.EnqueueUserWorkItem(() =>
                            {
                                barrier.SignalAndWait(testCancelToken);
                                try
                                {
                                    var stream = pipe.GetReadStream();
                                    if (stream != null)
                                    {
                                        Interlocked.Increment(ref successfulReads);
                                        stream.Dispose();
                                    }
                                }
                                catch (InvalidOperationException) { } // Expected
                                catch (Exception e)
                                {
                                    testLogger.Log(e);
                                }
                                finally
                                {
                                    barrier.SignalAndWait(testCancelToken);
                                }
                            });
                        }

                        barrier.SignalAndWait(testCancelToken);
                        barrier.SignalAndWait(testCancelToken);
                        Assert.AreEqual(1, successfulReads);
                    }
                }
            }
        }



        [TestMethod]
        public void TestPipeStreamThreadSafeGetWriteStream()
        {
            const int THREAD_COUNT = 10;
            ILogger testLogger = new ConsoleLogger();
            using (CancellationTokenSource testCancel = new CancellationTokenSource())
            using (CustomThreadPool pool = new CustomThreadPool(
                NullLogger.Singleton,
                NullMetricCollector.Singleton,
                DimensionSet.Empty,
                threadCount: THREAD_COUNT,
                poolName: "TestThreadPool"))
            using (Barrier barrier = new Barrier(THREAD_COUNT + 1))
            {
                testCancel.CancelAfter(TimeSpan.FromSeconds(10));
                CancellationToken testCancelToken = testCancel.Token;
                for (int loop = 0; loop < 100; loop++)
                {
                    using (PipeStream pipe = new PipeStream())
                    {
                        int successfulReads = 0;
                        for (int c = 0; c < THREAD_COUNT; c++)
                        {
                            pool.EnqueueUserWorkItem(() =>
                            {
                                barrier.SignalAndWait(testCancelToken);
                                try
                                {
                                    var stream = pipe.GetWriteStream();
                                    if (stream != null)
                                    {
                                        Interlocked.Increment(ref successfulReads);
                                        stream.Dispose();
                                    }
                                }
                                catch (InvalidOperationException) { } // Expected
                                catch (Exception e)
                                {
                                    testLogger.Log(e);
                                }
                                finally
                                {
                                    barrier.SignalAndWait(testCancelToken);
                                }
                            });
                        }

                        barrier.SignalAndWait(testCancelToken);
                        barrier.SignalAndWait(testCancelToken);
                        Assert.AreEqual(1, successfulReads);
                    }
                }
            }
        }
    }
}
