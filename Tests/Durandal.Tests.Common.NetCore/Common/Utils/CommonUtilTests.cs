using Durandal.API;
using Durandal.Common.Collections;
using Durandal.Common.Compression.BZip2;
using Durandal.Common.Compression.LZ4;
using Durandal.Common.Compression.ZLib;
using Durandal.Common.File;
using Durandal.Common.Instrumentation;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.NLP.ApproxString;
using Durandal.Common.NLP.Language.English;
using Durandal.Common.Statistics;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Tests.Common.Utils;
using Durandal.Common.NLP.Language;
using Durandal.Common.Test;

namespace Durandal.Tests.Common.Utils
{
    [TestClass]
    public class CommonUtilTests
    {
        [TestMethod]
        public void TestTaskFromCanceled()
        {
            CancellationTokenSource source = new CancellationTokenSource();
            Task t;
            try
            {
                t = DurandalTaskExtensions.FromCanceled<bool>(source.Token);
                Assert.Fail("Should have thrown ArgumentOutOfRangeException");
            }
            catch (ArgumentOutOfRangeException) { }

            source.Cancel();
            t = DurandalTaskExtensions.FromCanceled<bool>(source.Token);
            Assert.IsTrue(t.IsCanceled);
        }

        [TestMethod]
        public void TestLZ4CompressionStream()
        {
            IRandom rand = new FastRandom();
            byte[] data = new byte[100000];
            rand.NextBytes(data);

            byte[] compressedData;
            using (MemoryStream sourceStream = new MemoryStream(data))
            {
                using (MemoryStream compressedStream = new MemoryStream())
                {
                    using (LZ4Stream compressor = new LZ4Stream(compressedStream, LZ4StreamMode.Compress))
                    {
                        sourceStream.CopyTo(compressor);
                        compressor.Flush();
                    }

                    compressedData = compressedStream.ToArray();
                }
            }

            byte[] decodedData;
            using (MemoryStream sourceStream = new MemoryStream(compressedData))
            {
                using (MemoryStream decompressedStream = new MemoryStream())
                {
                    using (LZ4Stream decompressor = new LZ4Stream(sourceStream, LZ4StreamMode.Decompress))
                    {
                        decompressor.CopyTo(decompressedStream);
                        decompressor.Flush();
                    }

                    decodedData = decompressedStream.ToArray();
                }
            }

            Assert.AreEqual(data.Length, decodedData.Length);
            for (int c = 0; c < data.Length; c++)
            {
                Assert.AreEqual(data[c], decodedData[c]);
            }
        }

        [TestMethod]
        public void TestZlibCompressionStream()
        {
            IRandom rand = new FastRandom();
            byte[] data = new byte[100000];
            rand.NextBytes(data);

            byte[] compressedData;
            using (MemoryStream sourceStream = new MemoryStream(data))
            {
                using (MemoryStream compressedStream = new MemoryStream())
                {
                    using (ZlibStream compressor = new ZlibStream(compressedStream, CompressionMode.Compress, true))
                    {
                        sourceStream.CopyTo(compressor);
                        compressor.Flush();
                    }
                    
                    compressedData = compressedStream.ToArray();
                }
            }

            byte[] decodedData;
            using (MemoryStream sourceStream = new MemoryStream(compressedData))
            {
                using (MemoryStream decompressedStream = new MemoryStream())
                {
                    using (ZlibStream decompressor = new ZlibStream(sourceStream, CompressionMode.Decompress, true))
                    {
                        decompressor.CopyTo(decompressedStream);
                        decompressor.Flush();
                    }

                    decodedData = decompressedStream.ToArray();
                }
            }

            Assert.AreEqual(data.Length, decodedData.Length);
            for (int c = 0; c < data.Length; c++)
            {
                Assert.AreEqual(data[c], decodedData[c]);
            }
        }

        [TestMethod]
        public void TestBZip2CompressionStream()
        {
            ILogger logger = new ConsoleLogger();
            IRandom rand = new FastRandom();
            byte[] data = new byte[100000];
            rand.NextBytes(data);

            byte[] compressedData;
            using (MemoryStream sourceStream = new MemoryStream(data))
            {
                using (MemoryStream compressedStream = new MemoryStream())
                {
                    using (BZip2OutputStream compressor = new BZip2OutputStream(compressedStream, true, logger))
                    {
                        sourceStream.CopyTo(compressor);
                        compressor.Flush();
                    }

                    compressedData = compressedStream.ToArray();
                }
            }

            byte[] decodedData;
            using (MemoryStream sourceStream = new MemoryStream(compressedData))
            {
                using (MemoryStream decompressedStream = new MemoryStream())
                {
                    using (BZip2InputStream decompressor = new BZip2InputStream(sourceStream, true))
                    {
                        decompressor.CopyTo(decompressedStream);
                        decompressor.Flush();
                    }

                    decodedData = decompressedStream.ToArray();
                }
            }

            Assert.AreEqual(data.Length, decodedData.Length);
            for (int c = 0; c < data.Length; c++)
            {
                Assert.AreEqual(data[c], decodedData[c]);
            }
        }

        [TestMethod]
        public void TestGZipCompressionStream()
        {
            ILogger logger = new ConsoleLogger();
            IRandom rand = new FastRandom();
            byte[] data = new byte[100000];
            rand.NextBytes(data);

            byte[] compressedData;
            using (MemoryStream sourceStream = new MemoryStream(data))
            using (MemoryStream compressedStream = new MemoryStream())
            {
                using (GZipStream compressor = new GZipStream(compressedStream, CompressionMode.Compress))
                {
                    sourceStream.CopyTo(compressor);
                    compressor.Flush();
                }

                compressedData = compressedStream.ToArray();
            }

            byte[] decodedData;
            using (MemoryStream sourceStream = new MemoryStream(compressedData))
            using (MemoryStream decompressedStream = new MemoryStream())
            {
                using (GZipStream decompressor = new GZipStream(sourceStream, CompressionMode.Decompress))
                {
                    decompressor.CopyTo(decompressedStream);
                    decompressor.Flush();
                }

                decodedData = decompressedStream.ToArray();
            }

            Assert.AreEqual(data.Length, decodedData.Length);
            for (int c = 0; c < data.Length; c++)
            {
                Assert.AreEqual(data[c], decodedData[c]);
            }
        }

        [TestMethod]
        public void TestTimeSpanParserPositiveCases()
        {
            Assert.AreEqual(new TimeSpan(0, 1, 1), TimeSpanExtensions.ParseTimeSpan("61"));
            Assert.AreEqual(new TimeSpan(0, 1, 1), TimeSpanExtensions.ParseTimeSpan("1:01"));
            Assert.AreEqual(new TimeSpan(0, 0, 13), TimeSpanExtensions.ParseTimeSpan("13"));
            Assert.AreEqual(new TimeSpan(0, 0, 13), TimeSpanExtensions.ParseTimeSpan("13.00"));
            Assert.AreEqual(new TimeSpan(0, 0, 13), TimeSpanExtensions.ParseTimeSpan("0:13.00"));
            Assert.AreEqual(new TimeSpan(0, 0, 13), TimeSpanExtensions.ParseTimeSpan("0:13"));
            Assert.AreEqual(new TimeSpan(0, 0, 1), TimeSpanExtensions.ParseTimeSpan("0:01"));
            Assert.AreEqual(new TimeSpan(0, 5, 13), TimeSpanExtensions.ParseTimeSpan("5:13"));
            Assert.AreEqual(new TimeSpan(0, 5, 13), TimeSpanExtensions.ParseTimeSpan("05:13"));
            Assert.AreEqual(new TimeSpan(0, 5, 13), TimeSpanExtensions.ParseTimeSpan("05:13.0"));
            Assert.AreEqual(new TimeSpan(0, 5, 13), TimeSpanExtensions.ParseTimeSpan("05:13.000"));
            Assert.AreEqual(new TimeSpan(0, 5, 13), TimeSpanExtensions.ParseTimeSpan("5:13.0000000"));
            Assert.AreEqual(new TimeSpan(2, 5, 13), TimeSpanExtensions.ParseTimeSpan("2:05:13"));
            Assert.AreEqual(new TimeSpan(2, 5, 13), TimeSpanExtensions.ParseTimeSpan("02:05:13"));
            Assert.AreEqual(new TimeSpan(2, 5, 13), TimeSpanExtensions.ParseTimeSpan("0.02:05:13"));
            Assert.AreEqual(new TimeSpan(7, 2, 5, 13), TimeSpanExtensions.ParseTimeSpan("7.02:05:13"));
            Assert.AreEqual(new TimeSpan(7, 2, 5, 13, 347), TimeSpanExtensions.ParseTimeSpan("7.02:05:13.347"));
            Assert.AreEqual(new TimeSpan(10, 2, 5, 13, 007), TimeSpanExtensions.ParseTimeSpan("10.02:05:13.007"));
            Assert.AreEqual(new TimeSpan(0, 0, 0, 0, 7), TimeSpanExtensions.ParseTimeSpan("0.007"));
            Assert.AreEqual(new TimeSpan(0, 0, 0, 0, 70), TimeSpanExtensions.ParseTimeSpan("0.07"));
            Assert.AreEqual(new TimeSpan(0, 0, 0, 0, 700), TimeSpanExtensions.ParseTimeSpan("0.7"));
            Assert.AreEqual(new TimeSpan(0, 0, 0, 1, 700), TimeSpanExtensions.ParseTimeSpan("1.7"));
        }

        [TestMethod]
        public void TestTimeSpanParserNegativeCases()
        {
            AssertTimeSpanParsingFails("1:2");
            AssertTimeSpanParsingFails("1A:2");
            AssertTimeSpanParsingFails("01:2");
            AssertTimeSpanParsingFails("1.123456789");
            AssertTimeSpanParsingFails("1.12345678");
            AssertTimeSpanParsingFails("1.123B678");
            AssertTimeSpanParsingFails("1:5:13");
            AssertTimeSpanParsingFails("1:5A:13");
            AssertTimeSpanParsingFails("1:35:3");
            AssertTimeSpanParsingFails("F:35:3");
            AssertTimeSpanParsingFails("1.2:05:13");
            AssertTimeSpanParsingFails("1.2B:05:13");
            AssertTimeSpanParsingFails("1.21:05.12");
            AssertTimeSpanParsingFails("1.23.12");
            AssertTimeSpanParsingFails("1.2C.12");
            AssertTimeSpanParsingFails("1:60");
            AssertTimeSpanParsingFails("1:60:00");
            AssertTimeSpanParsingFails("1:00:60");
        }

        private static void AssertTimeSpanParsingFails(string stringFormat)
        {
            try
            {
                TimeSpanExtensions.ParseTimeSpan(stringFormat);
                Assert.Fail("The string \"" + stringFormat + "\" should have failed to parse");
            }
            catch (FormatException) { }
        }

        [TestMethod]
        public void TestTimeSpanFormatter()
        {
            Assert.AreEqual("3", new TimeSpan(0, 0, 3).PrintTimeSpan());
            Assert.AreEqual("13", new TimeSpan(0, 0, 13).PrintTimeSpan());
            Assert.AreEqual("1:00", new TimeSpan(0, 1, 0).PrintTimeSpan());
            Assert.AreEqual("1:01", new TimeSpan(0, 1, 1).PrintTimeSpan());
            Assert.AreEqual("1:59", new TimeSpan(0, 1, 59).PrintTimeSpan());
            Assert.AreEqual("59:59", new TimeSpan(0, 59, 59).PrintTimeSpan());
            Assert.AreEqual("1:10:59", new TimeSpan(1, 10, 59).PrintTimeSpan());
            Assert.AreEqual("9:10:59", new TimeSpan(9, 10, 59).PrintTimeSpan());
            Assert.AreEqual("9:10:01", new TimeSpan(9, 10, 1).PrintTimeSpan());
            Assert.AreEqual("9:01:01", new TimeSpan(9, 1, 1).PrintTimeSpan());
            Assert.AreEqual("23:01:01", new TimeSpan(23, 1, 1).PrintTimeSpan());
            Assert.AreEqual("4.09:01:01", new TimeSpan(4, 9, 1, 1).PrintTimeSpan());
            Assert.AreEqual("4.00:00:00", new TimeSpan(4, 0, 0, 0).PrintTimeSpan());
            Assert.AreEqual("4.00:00:00.1", new TimeSpan(4, 0, 0, 0, 100).PrintTimeSpan());
            Assert.AreEqual("4.00:00:00.01", new TimeSpan(4, 0, 0, 0, 10).PrintTimeSpan());
            Assert.AreEqual("4.00:00:00.001", new TimeSpan(4, 0, 0, 0, 1).PrintTimeSpan());
            Assert.AreEqual("0.0000001", new TimeSpan(1L).PrintTimeSpan());
            Assert.AreEqual("0.000001", new TimeSpan(10L).PrintTimeSpan());
            Assert.AreEqual("0.00001", new TimeSpan(100L).PrintTimeSpan());
            Assert.AreEqual("0.0001", new TimeSpan(1000L).PrintTimeSpan());
            Assert.AreEqual("0.001", new TimeSpan(10000L).PrintTimeSpan());
            Assert.AreEqual("0.01", new TimeSpan(100000L).PrintTimeSpan());
            Assert.AreEqual("0.1", new TimeSpan(1000000L).PrintTimeSpan());
            Assert.AreEqual("0.123", new TimeSpan(1230000L).PrintTimeSpan());
            Assert.AreEqual("0.12345", new TimeSpan(1234500L).PrintTimeSpan());
        }

        [TestMethod]
        public void TestTimeSpanFormatterParserFuzzer()
        {
            IRandom rand = new FastRandom();

            for (int decimalPlace = 0; decimalPlace < 14; decimalPlace++)
            {
                for (int loop = 0; loop < 1000; loop++)
                {
                    long ticks = rand.NextInt(0, 10);
                    for (int p = 0; p < decimalPlace; p++)
                    {
                        ticks = ticks * 10;
                        if (rand.NextFloat() < 0.1f)
                        {
                            ticks = ticks + rand.NextInt(0, 10);
                        }
                    }

                    TimeSpan orig = TimeSpan.FromTicks(ticks);
                    string toString = orig.PrintTimeSpan();
                    TimeSpan reparsed = TimeSpanExtensions.ParseTimeSpan(toString);
                    Assert.AreEqual(orig, reparsed);
                }
            }
        }

        [TestMethod]
        public void TestTimeSpanVary()
        {
            double lastRate = 0;
            LockStepRealTimeProvider lockStepTime = new LockStepRealTimeProvider(NullLogger.Singleton);
            IRealTimeProvider threadTime = lockStepTime.Fork("ThreadTime");
            CancellationTokenSource testCancelizer = new CancellationTokenSource();
            Task.Run(async () =>
            {
                TimeSpan baseRate = TimeSpan.FromMilliseconds(1000);
                RateCounter counter = new RateCounter(TimeSpan.FromHours(3), threadTime);
                while (!testCancelizer.IsCancellationRequested)
                {
                    await threadTime.WaitAsync(baseRate.Vary(0.7), testCancelizer.Token);
                    counter.Increment();
                    lastRate = counter.Rate;
                }
            });

            lockStepTime.Step(TimeSpan.FromHours(6));
            testCancelizer.Cancel();
            Assert.AreEqual(1.0, lastRate, 0.1);
        }

        [TestMethod]
        public void TestTimeSpanVaryOutOfRangeNegative()
        {
            try
            {
                TimeSpan.FromMilliseconds(1000).Vary(-0.1);
                Assert.Fail("Should have thrown an ArgumentOfRangeException");
            }
            catch (ArgumentOutOfRangeException) { }
        }

        [TestMethod]
        public void TestTimeSpanVaryOutOfRangeTooHigh()
        {
            try
            {
                TimeSpan.FromMilliseconds(1000).Vary(1.1);
                Assert.Fail("Should have thrown an ArgumentOfRangeException");
            }
            catch (ArgumentOutOfRangeException) { }
        }

        [TestMethod]
        public async Task TestApproxStringMatchingIndex()
        {
            ILogger logger = new ConsoleLogger();
            // Item1 is actual name, Item2 is name with misspelling
            List<Tuple<string, string>> approximateMonthNames = new List<Tuple<string, string>>();
            approximateMonthNames.Add(new Tuple<string, string>("January", "janary"));
            approximateMonthNames.Add(new Tuple<string, string>("February", "febuary"));
            approximateMonthNames.Add(new Tuple<string, string>("March", "marcch"));
            approximateMonthNames.Add(new Tuple<string, string>("April", "abril"));
            approximateMonthNames.Add(new Tuple<string, string>("May", "mayy"));
            approximateMonthNames.Add(new Tuple<string, string>("June", "jun"));
            approximateMonthNames.Add(new Tuple<string, string>("July", "jul"));
            approximateMonthNames.Add(new Tuple<string, string>("August", "augst"));
            approximateMonthNames.Add(new Tuple<string, string>("September", "septimber"));
            approximateMonthNames.Add(new Tuple<string, string>("October", "octubre"));
            approximateMonthNames.Add(new Tuple<string, string>("November", "noviembre"));
            approximateMonthNames.Add(new Tuple<string, string>("December", "deciembre"));

            IApproxStringFeatureExtractor featureExtractor = new EnglishNgramApproxStringFeatureExtractor(3);
            ApproxStringMatchingIndex index = new ApproxStringMatchingIndex(featureExtractor, LanguageCode.EN_US, logger);
            foreach (Tuple<string, string> input in approximateMonthNames)
            {
                index.Index(new LexicalString(input.Item1));
            }


            // Serialize and deserialize the index
            InMemoryFileSystem fakeFileSystem = new InMemoryFileSystem();
            VirtualPath indexFile = new VirtualPath("temp.index");
            await index.Serialize(fakeFileSystem, indexFile);

            index = await ApproxStringMatchingIndex.Deserialize(fakeFileSystem, indexFile, featureExtractor, logger, LanguageCode.EN_US);

            foreach (Tuple<string, string> input in approximateMonthNames)
            {
                logger.Log("Testing " + input.Item2);
                LexicalString lex = new LexicalString(input.Item2);
                IList<Hypothesis<LexicalString>> results = index.Match(lex, 5);
                Assert.AreNotEqual(0, results.Count);
                Assert.AreEqual(input.Item1, results[0].Value.WrittenForm);
            }
        }

        /// <summary>
        /// Test concurrent access to buffer pools of all varying lengths
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestBufferPool()
        {
            const int NumTestThreads = 8;
            bool testsFailed = false;
            TimeSpan testRunTime = TimeSpan.FromSeconds(4);

            using (IThreadPool threadPool = new CustomThreadPool(NullLogger.Singleton, NullMetricCollector.Singleton, DimensionSet.Empty, ThreadPriority.Normal, "ThreadPool", NumTestThreads))
            {
                CancellationTokenSource cancelToken = new CancellationTokenSource(testRunTime);

                for (int thread = 0; thread < NumTestThreads; thread++)
                {
                    threadPool.EnqueueUserAsyncWorkItem(async () =>
                    {
                        FastRandom rand = new FastRandom();
                        while (!cancelToken.Token.IsCancellationRequested)
                        {
                            int workingSize = (int)Math.Pow(2, rand.NextDouble() * 19);
                            using (PooledBuffer<byte> buf = BufferPool<byte>.Rent(workingSize))
                            {
                                byte[] buffer = buf.Buffer;
                                for (int c = 0; c < buffer.Length; c++)
                                {
                                    buffer[c] = (byte)(c % 256);
                                }

                                await Task.Delay(1);

                                for (int c = 0; c < buffer.Length; c++)
                                {
                                    if (buffer[c] != (byte)(c % 256))
                                    {
                                        testsFailed = true;
                                    }
                                }
                            }
                        }
                    });
                }

                while (!cancelToken.Token.IsCancellationRequested)
                {
                    await Task.Delay(10);
                }

                await threadPool.WaitForCurrentTasksToFinish(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                Assert.IsFalse(testsFailed);
            }
        }

        [TestMethod]
        public async Task TestPipeStreamBasic()
        {
            await TestPipeStream(null, null);
        }

        [TestMethod]
        public async Task TestPipeStreamSlowRead()
        {
            await TestPipeStream(TimeSpan.FromMilliseconds(100), null);
        }

        [TestMethod]
        public async Task TestPipeStreamSlowWrite()
        {
            await TestPipeStream(null, TimeSpan.FromMilliseconds(100));
        }

        [TestMethod]
        public async Task TestPipeStreamSlowReadWrite()
        {
            await TestPipeStream(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
        }

        private async Task TestPipeStream(TimeSpan? readDelay = null, TimeSpan? writeDelay = null)
        {
            ILogger logger = new ConsoleLogger();
            PipeStream stream = new PipeStream();
            LockStepRealTimeProvider realTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));
            const int amountToTransfer = 100000;

            IRealTimeProvider writeTaskTime = realTime.Fork("PipeWriteTask");
            Task<bool> writer = Task.Run<bool>(() =>
            {
                try
                {
                    using (Stream writeStream = stream.GetWriteStream())
                    {
                        writeTaskTime.Wait(TimeSpan.FromSeconds(1), CancellationToken.None);
                        ILogger localLogger = logger.Clone("Writer");
                        IRandom random = new FastRandom(54);
                        byte[] buffer = new byte[amountToTransfer];
                        for (int c = 0; c < buffer.Length; c++)
                        {
                            buffer[c] = (byte)(c % byte.MaxValue);
                        }

                        int writeCursor = 0;
                        while (writeCursor < buffer.Length)
                        {
                            int writeSize = Math.Min(buffer.Length - writeCursor, random.NextInt(1, 1000));
                            localLogger.Log("Writing " + writeSize + " (total " + writeCursor + ")");
                            writeStream.Write(buffer, writeCursor, writeSize);
                            localLogger.Log("Wrote " + writeSize);
                            writeCursor += writeSize;

                            if (writeDelay.HasValue)
                            {
                                writeTaskTime.Wait(writeDelay.Value, CancellationToken.None);
                            }
                        }

                        return true;
                    }
                }
                finally
                {
                    writeTaskTime.Merge();
                }
            });

            IRealTimeProvider readTaskTime = realTime.Fork("PipeReadTask");
            Task<bool> reader = Task.Run<bool>(() =>
            {

                try
                {
                    IRandom random = new FastRandom(22);
                    byte[] buffer = new byte[amountToTransfer];
                    ILogger localLogger = logger.Clone("Reader");
                    readTaskTime.Wait(TimeSpan.FromSeconds(1), CancellationToken.None);

                    using (PipeStream.PipeReadStream readStream = stream.GetReadStream())
                    {
                        int readCursor = 0;
                        while (readCursor < buffer.Length)
                        {
                            int readSize = Math.Min(buffer.Length - readCursor, random.NextInt(1, 1000));
                            localLogger.Log("Reading " + readSize + " (total " + readCursor + ")");
                            int actuallyRead = readStream.Read(buffer, readCursor, readSize, CancellationToken.None, readTaskTime);
                            if (actuallyRead == 0)
                            {
                                localLogger.Log("Stream ended prematurely after " + readCursor + " bytes");
                                return false;
                            }

                            localLogger.Log("Read " + actuallyRead);
                            readCursor += actuallyRead;

                            if (readDelay.HasValue)
                            {
                                readTaskTime.Wait(readDelay.Value, CancellationToken.None);
                            }
                        }
                    }

                    for (int c = 0; c < buffer.Length; c++)
                    {
                        if (buffer[c] != (byte)(c % byte.MaxValue))
                        {
                            localLogger.Log("Failed to validate byte " + c);
                            return false;
                        }
                    }

                    return true;
                }
                finally
                {
                    readTaskTime.Merge();
                }
            });

            realTime.Step(TimeSpan.FromSeconds(60), 1000);

            Assert.IsTrue(await writer);
            Assert.IsTrue(await reader);
        }

        [TestMethod]
        public void TestByteArraySegmentHashability()
        {
            byte[] block = new byte[100];
            ByteArraySegment test1 = new ByteArraySegment(block, 0, Encoding.UTF8.GetBytes("Test", 0, "Test".Length, block, 0));
            ByteArraySegment test2 = new ByteArraySegment(block, 10, Encoding.UTF8.GetBytes("Test", 0, "Test".Length, block, 10));
            ByteArraySegment foo = new ByteArraySegment(block, 20, Encoding.UTF8.GetBytes("Foo", 0, "Foo".Length, block, 20));
            ByteArraySegment bar = new ByteArraySegment(block, 30, Encoding.UTF8.GetBytes("Bar", 0, "Bar".Length, block, 30));

            byte[] block2 = new byte[100];
            ByteArraySegment test3 = new ByteArraySegment(block2, 50, Encoding.UTF8.GetBytes("Test", 0, "Test".Length, block2, 50));
            Assert.IsTrue(test1.Equals(test2));
            Assert.IsTrue(test1.GetHashCode() == test2.GetHashCode());
            Assert.IsTrue(test1.Equals(test3));
            Assert.IsTrue(test1.GetHashCode() == test3.GetHashCode());
            Assert.IsFalse(test1.GetHashCode() == foo.GetHashCode());
            Assert.IsFalse(test1.GetHashCode() == bar.GetHashCode());
            Assert.IsFalse(test2.GetHashCode() == foo.GetHashCode());
            Assert.IsFalse(test2.GetHashCode() == bar.GetHashCode());
            Assert.IsFalse(test1.Equals(foo));
            Assert.IsFalse(test1.Equals(bar));
            Assert.IsFalse(test2.Equals(foo));
            Assert.IsFalse(test2.Equals(bar));

            ByteArraySegment empty1 = new ByteArraySegment(block, 0, 0);
            ByteArraySegment empty2 = new ByteArraySegment(block, 5, 0);
            Assert.IsTrue(empty1.Equals(empty2));
            Assert.IsTrue(empty1.GetHashCode() == empty2.GetHashCode());
        }
    }
}
