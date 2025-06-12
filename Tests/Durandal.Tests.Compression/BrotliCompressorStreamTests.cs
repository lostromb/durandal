namespace Durandal.Tests.Compression.Brotli
{
    using Durandal.Common.Utils.NativePlatform;
    using Durandal.Extensions.Compression.Brotli;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Win32.SafeHandles;
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    [TestClass]
    public class BrotliCompressorStreamTests
    {
        [ClassInitialize]
        public static void InitializeTests(TestContext context)
        {
            NativePlatformUtils.SetGlobalResolver(new NativeLibraryResolverImpl());
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            NativePlatformUtils.SetGlobalResolver(null);
        }

        [TestMethod]
        public void BrotliCompressionStream_InvalidArgs()
        {
            using (Stream wrapper = new MemoryStream())
            {
                try
                {
                    new BrotliCompressorStream(null, false, 6, 15);
                    Assert.Fail("Should have thrown an ArgumentNullException");
                }
                catch (ArgumentNullException) { }

                try
                {
                    new BrotliCompressorStream(wrapper, false, 13, 15);
                    Assert.Fail("Should have thrown an ArgumentOutOfRangeException");
                }
                catch (ArgumentOutOfRangeException) { }

                try
                {
                    new BrotliCompressorStream(wrapper, false, 6, 7);
                    Assert.Fail("Should have thrown an ArgumentOutOfRangeException");
                }
                catch (ArgumentOutOfRangeException) { }

                try
                {
                    new BrotliCompressorStream(wrapper, false, 6, 25);
                    Assert.Fail("Should have thrown an ArgumentOutOfRangeException");
                }
                catch (ArgumentOutOfRangeException) { }

                try
                {
                    new BrotliCompressorStream(new MemoryStream(new byte[1], writable: false));
                    Assert.Fail("Should have thrown an ArgumentException");
                }
                catch (ArgumentException) { }

                BrotliCompressorStream stream = new BrotliCompressorStream(wrapper, false, 6, 15);
                Assert.IsNotNull(stream);

                try
                {
                    stream.SetQuality(12);
                    Assert.Fail("Should have thrown an ArgumentOutOfRangeException");
                }
                catch (ArgumentOutOfRangeException) { }

                try
                {
                    stream.SetWindow(9);
                    Assert.Fail("Should have thrown an ArgumentOutOfRangeException");
                }
                catch (ArgumentOutOfRangeException) { }

                try
                {
                    stream.SetWindow(25);
                    Assert.Fail("Should have thrown an ArgumentOutOfRangeException");
                }
                catch (ArgumentOutOfRangeException) { }
            }
        }

        [TestMethod]
        public void BrotliCompressionStream_PassthroughInnerStream()
        {
            byte[] inputData = CreateSourceData(1000000);
            using (MemoryStream sourceStream = new MemoryStream(inputData, index: 0, count: inputData.Length, writable: false))
            using (MemoryStream innerStream = new MemoryStream())
            {
                BrotliCompressorStream compressor = new BrotliCompressorStream(innerStream, false, 6, 15);
                sourceStream.CopyTo(compressor);
                compressor.Flush();
                Assert.AreEqual(false, compressor.CanRead);
                Assert.AreEqual(false, compressor.CanSeek);
                Assert.AreEqual(innerStream.CanWrite, compressor.CanWrite);
                Assert.AreEqual(innerStream.Length, compressor.Length);
                Assert.AreEqual(innerStream.Position, compressor.Position);
            }
        }

        [TestMethod]
        public async Task BrotliCompressionStream_InvalidOperations()
        {
            using (Stream innerStream = new MemoryStream())
            {
                BrotliCompressorStream compressor = new BrotliCompressorStream(innerStream, false, 6, 15);

                try
                {
                    compressor.Seek(0, SeekOrigin.Begin);
                    Assert.Fail("Should have thrown an InvalidOperationException");
                }
                catch (InvalidOperationException) { }
                try
                {
                    compressor.SetLength(0);
                    Assert.Fail("Should have thrown an InvalidOperationException");
                }
                catch (InvalidOperationException) { }
                try
                {
                    compressor.Read(new byte[1], 0, 1);
                    Assert.Fail("Should have thrown an InvalidOperationException");
                }
                catch (InvalidOperationException) { }
                try
                {
                    await compressor.ReadAsync(new byte[1], 0, 1, CancellationToken.None).ConfigureAwait(false);
                    Assert.Fail("Should have thrown an InvalidOperationException");
                }
                catch (InvalidOperationException) { }
                try
                {
                    compressor.Position = 0;
                    Assert.Fail("Should have thrown an InvalidOperationException");
                }
                catch (InvalidOperationException) { }
            }
        }

        [TestMethod]
        public void BrotliCompressionStream_MultipleDisposal()
        {
            using (Stream innerStream = new MemoryStream())
            using (BrotliCompressorStream compressor = new BrotliCompressorStream(innerStream, false, 6, 15))
            {
                compressor.Dispose();
                compressor.Dispose();
            }
        }

        [TestMethod]
        public async Task BrotliCompressionStream_ThrowDisposedExceptions()
        {
            using (Stream innerStream = new MemoryStream())
            using (BrotliCompressorStream compressor = new BrotliCompressorStream(innerStream, false, 6, 15))
            {
                compressor.Dispose();

                try
                {
                    compressor.Flush();
                    Assert.Fail("Should have thrown an ObjectDisposedException");
                }
                catch (ObjectDisposedException) { }
                try
                {
                    await compressor.FlushAsync().ConfigureAwait(false);
                    Assert.Fail("Should have thrown an ObjectDisposedException");
                }
                catch (ObjectDisposedException) { }
                try
                {
                    compressor.Write(new byte[1], 0, 1);
                    Assert.Fail("Should have thrown an ObjectDisposedException");
                }
                catch (ObjectDisposedException) { }
                try
                {
                    await compressor.WriteAsync(new byte[1], 0, 1, CancellationToken.None).ConfigureAwait(false);
                    Assert.Fail("Should have thrown an ObjectDisposedException");
                }
                catch (ObjectDisposedException) { }
            }
        }

        [TestMethod]
        [DataRow(10, 10)]
        [DataRow(64, 1)]
        [DataRow(64 * 1024, 16)]
        [DataRow(1000, 1000)]
        [DataRow(10000, 1000)]
        [DataRow(100000, 100)]
        [DataRow(1000000, 100000)]
        [DataRow(10000000, 10000000)]
        public void BrotliCompressionStream_BasicWrite(int approximateInputSize, int writeSize)
        {
            byte[] inputData = CreateSourceData(approximateInputSize);
            using (MemoryStream sourceStream = new MemoryStream(inputData, index: 0, count: inputData.Length, writable: false))
            using (MemoryStream innerStream = new MemoryStream())
            {
                using (BrotliCompressorStream compressor = new BrotliCompressorStream(innerStream, leaveOpen: true))
                {
                    compressor.SetQuality(4);
                    compressor.SetWindow(12);
                    byte[] scratch = new byte[writeSize];
                    int bytesRead = 1;
                    while (bytesRead != 0)
                    {
                        bytesRead = sourceStream.Read(scratch, 0, scratch.Length);
                        if (bytesRead > 0)
                        {
                            compressor.Write(scratch, 0, bytesRead);
                        }
                    }

                    compressor.Flush();
                }

                // Decompress the brotli payload to ensure it is valid and the output matches the input
                byte[] decompressedData = DecompressFromBrotli(innerStream.ToArray());
                AssertSequenceEqual(inputData, decompressedData);
            }
        }

        [TestMethod]
        [DataRow(10, 10)]
        [DataRow(64, 1)]
        [DataRow(64 * 1024, 16)]
        [DataRow(1000, 1000)]
        [DataRow(10000, 1000)]
        [DataRow(100000, 100)]
        [DataRow(1000000, 100000)]
        [DataRow(10000000, 10000000)]
        public async Task BrotliCompressionStream_BasicWriteAsync(int approximateInputSize, int writeSize)
        {
            byte[] inputData = CreateSourceData(approximateInputSize);
            using (MemoryStream sourceStream = new MemoryStream(inputData, index: 0, count: inputData.Length, writable: false))
            using (MemoryStream innerStream = new MemoryStream())
            {
                using (BrotliCompressorStream compressor = new BrotliCompressorStream(innerStream, leaveOpen: true))
                {
                    compressor.SetQuality(4);
                    compressor.SetWindow(12);
                    byte[] scratch = new byte[writeSize];
                    int bytesRead = 1;
                    while (bytesRead != 0)
                    {
                        bytesRead = sourceStream.Read(scratch, 0, scratch.Length);
                        if (bytesRead > 0)
                        {
                            await compressor.WriteAsync(scratch, 0, bytesRead).ConfigureAwait(false);
                        }
                    }

                    await compressor.FlushAsync().ConfigureAwait(false);
                }

                // Decompress the brotli payload to ensure it is valid and the output matches the input
                byte[] decompressedData = DecompressFromBrotli(innerStream.ToArray());
                AssertSequenceEqual(inputData, decompressedData);
            }
        }

        [TestMethod]
        public void BrotliCompressionStream_WriteByte()
        {
            byte[] inputData = CreateSourceData(100000);
            int inIdx = 0;
            using (MemoryStream innerStream = new MemoryStream())
            {
                using (BrotliCompressorStream compressor = new BrotliCompressorStream(innerStream, leaveOpen: true))
                {
                    compressor.SetQuality(4);
                    compressor.SetWindow(12);
                    while (inIdx < inputData.Length)
                    {
                        compressor.WriteByte(inputData[inIdx++]);
                    }

                    compressor.Flush();
                }

                // Decompress the brotli payload to ensure it is valid and the output matches the input
                byte[] decompressedData = DecompressFromBrotli(innerStream.ToArray());
                AssertSequenceEqual(inputData, decompressedData);
            }
        }

        [TestMethod]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Test.Usage", "RoslynCQ_DoNotUseRandomInput:Do not use random input", Justification = "The test clearly uses a fixed random seed but the analyzer does not care about that")]
        public void BrotliCompressionStream_RandomWritesAndFlushes()
        {
            Random rand = new Random(588123534);
            byte[] inputData = CreateSourceData(1000000);
            int inputDataCursor = 0;

            using (MemoryStream compressedStream = new MemoryStream())
            {
                using (BrotliCompressorStream compressor = new BrotliCompressorStream(compressedStream, leaveOpen: true))
                {
                    while (inputDataCursor < inputData.Length)
                    {
                        int writeSize = Math.Min(inputData.Length - inputDataCursor, rand.Next(1, 150000));
                        compressor.Write(inputData, inputDataCursor, writeSize);

                        if (rand.Next(0, 10) == 0)
                        {
                            compressor.Flush();
                        }

                        inputDataCursor += writeSize;
                    }
                }

                // Decompress the brotli payload to ensure it is valid and the output matches the input
                byte[] decompressedData = DecompressFromBrotli(compressedStream.ToArray());
                AssertSequenceEqual(inputData, decompressedData);
            }
        }

        [TestMethod]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Test.Usage", "RoslynCQ_DoNotUseRandomInput:Do not use random input", Justification = "The test clearly uses a fixed random seed but the analyzer does not care about that")]
        public async Task BrotliCompressionStream_RandomWritesAndFlushesAsync()
        {
            Random rand = new Random(698123);
            byte[] inputData = CreateSourceData(1000000);
            int inputDataCursor = 0;

            using (MemoryStream compressedStream = new MemoryStream())
            {
                using (BrotliCompressorStream compressor = new BrotliCompressorStream(compressedStream, leaveOpen: true))
                {
                    while (inputDataCursor < inputData.Length)
                    {
                        int writeSize = Math.Min(inputData.Length - inputDataCursor, rand.Next(1, 150000));
                        await compressor.WriteAsync(inputData, inputDataCursor, writeSize).ConfigureAwait(false);

                        if (rand.Next(0, 10) == 0)
                        {
                            await compressor.FlushAsync().ConfigureAwait(false);
                        }

                        inputDataCursor += writeSize;
                    }
                }

                // Decompress the brotli payload to ensure it is valid and the output matches the input
                byte[] decompressedData = DecompressFromBrotli(compressedStream.ToArray());
                AssertSequenceEqual(inputData, decompressedData);
            }
        }

        [TestMethod]
        public void BrotliCompressionStream_FailsToCompress()
        {
            FakeBrotliLib fakeBrolib = new FakeBrotliLib();
            fakeBrolib.FailCompressProcess = true;

            byte[] inputData = CreateSourceData(100000);
            using (MemoryStream sourceStream = new MemoryStream(inputData, index: 0, count: inputData.Length, writable: false))
            using (MemoryStream innerStream = new MemoryStream())
            {
                using (BrotliCompressorStream compressor = new BrotliCompressorStream(innerStream, leaveOpen: true, brotliLibrary: fakeBrolib))
                {
                    try
                    {
                        byte[] scratch = new byte[1024];
                        int bytesRead = 1;
                        while (bytesRead != 0)
                        {
                            bytesRead = sourceStream.Read(scratch, 0, scratch.Length);
                            if (bytesRead > 0)
                            {
                                compressor.Write(scratch, 0, bytesRead);
                            }
                        }

                        Assert.Fail("Should have thrown a BrotliException");
                    }
                    catch (BrotliException e)
                    {
                        Assert.AreEqual("Unable to compress Brotli stream", e.Message);
                    }
                }
            }
        }

        [TestMethod]
        public void BrotliCompressionStream_FailsToFlush()
        {
            FakeBrotliLib fakeBrolib = new FakeBrotliLib();
            fakeBrolib.FailCompressFinish = true;

            byte[] inputData = CreateSourceData(100000);
            using (MemoryStream sourceStream = new MemoryStream(inputData, index: 0, count: inputData.Length, writable: false))
            using (MemoryStream innerStream = new MemoryStream())
            {
                try
                {
                    using (BrotliCompressorStream compressor = new BrotliCompressorStream(innerStream, leaveOpen: true, brotliLibrary: fakeBrolib))
                    {
                        byte[] scratch = new byte[1024];
                        int bytesRead = 1;
                        while (bytesRead != 0)
                        {
                            bytesRead = sourceStream.Read(scratch, 0, scratch.Length);
                            if (bytesRead > 0)
                            {
                                compressor.Write(scratch, 0, bytesRead);
                                compressor.Flush();
                            }
                        }

                        Assert.Fail("Should have thrown a BrotliException");
                    }
                }
                catch (BrotliException e)
                {
                    Assert.AreEqual("Unable to flush Brotli stream", e.Message);
                }
            }
        }

        [TestMethod]
        public async Task BrotliCompressionStream_FailsToFlushAsync()
        {
            FakeBrotliLib fakeBrolib = new FakeBrotliLib();
            fakeBrolib.FailCompressFinish = true;

            byte[] inputData = CreateSourceData(100000);
            using (MemoryStream sourceStream = new MemoryStream(inputData, index: 0, count: inputData.Length, writable: false))
            using (MemoryStream innerStream = new MemoryStream())
            {
                try
                {
                    using (BrotliCompressorStream compressor = new BrotliCompressorStream(innerStream, leaveOpen: true, brotliLibrary: fakeBrolib))
                    {
                        byte[] scratch = new byte[1024];
                        int bytesRead = 1;
                        while (bytesRead != 0)
                        {
                            bytesRead = sourceStream.Read(scratch, 0, scratch.Length);
                            if (bytesRead > 0)
                            {
                                await compressor.WriteAsync(scratch, 0, bytesRead).ConfigureAwait(false);
                                await compressor.FlushAsync().ConfigureAwait(false);
                            }
                        }

                        Assert.Fail("Should have thrown a BrotliException");
                    }
                }
                catch (BrotliException e)
                {
                    Assert.AreEqual("Unable to flush Brotli stream", e.Message);
                }
            }
        }

        [TestMethod]
        public void BrotliCompressionStream_UnexpectedFinishOnProcess()
        {
            FakeBrotliLib fakeBrolib = new FakeBrotliLib();
            fakeBrolib.ForceEncoderIsFinished = true;

            byte[] inputData = CreateSourceData(100000);
            using (MemoryStream sourceStream = new MemoryStream(inputData, index: 0, count: inputData.Length, writable: false))
            using (MemoryStream innerStream = new MemoryStream())
            {
                using (BrotliCompressorStream compressor = new BrotliCompressorStream(innerStream, leaveOpen: true, brotliLibrary: fakeBrolib))
                {
                    try
                    {
                        byte[] scratch = new byte[1024];
                        int bytesRead = 1;
                        while (bytesRead != 0)
                        {
                            bytesRead = sourceStream.Read(scratch, 0, scratch.Length);
                            if (bytesRead > 0)
                            {
                                compressor.Write(scratch, 0, bytesRead);
                            }
                        }

                        Assert.Fail("Should have thrown a BrotliException");
                    }
                    catch (BrotliException e)
                    {
                        Assert.AreEqual("Unexepected finish signal from Brotli encoder", e.Message);
                    }
                }
            }
        }

        [TestMethod]
        public void BrotliCompressionStream_UnexpectedFinishOnFlush()
        {
            FakeBrotliLib fakeBrolib = new FakeBrotliLib();

            byte[] inputData = CreateSourceData(100000);
            using (MemoryStream sourceStream = new MemoryStream(inputData, index: 0, count: inputData.Length, writable: false))
            using (MemoryStream innerStream = new MemoryStream())
            {
                using (BrotliCompressorStream compressor = new BrotliCompressorStream(innerStream, leaveOpen: true, brotliLibrary: fakeBrolib))
                {
                    byte[] scratch = new byte[1024];
                    int bytesRead = 1;
                    while (bytesRead != 0)
                    {
                        bytesRead = sourceStream.Read(scratch, 0, scratch.Length);
                        if (bytesRead > 0)
                        {
                            compressor.Write(scratch, 0, bytesRead);
                        }
                    }

                    try
                    {
                        fakeBrolib.ForceEncoderIsFinished = true;
                        compressor.Flush();
                        Assert.Fail("Should have thrown a BrotliException");
                    }
                    catch (BrotliException e)
                    {
                        Assert.AreEqual("Unexepected finish signal from Brotli encoder", e.Message);
                    }
                }
            }
        }

        [TestMethod]
        public async Task BrotliCompressionStream_UnexpectedFinishOnFlushAsync()
        {
            FakeBrotliLib fakeBrolib = new FakeBrotliLib();

            byte[] inputData = CreateSourceData(100000);
            using (MemoryStream sourceStream = new MemoryStream(inputData, index: 0, count: inputData.Length, writable: false))
            using (MemoryStream innerStream = new MemoryStream())
            {
                using (BrotliCompressorStream compressor = new BrotliCompressorStream(innerStream, leaveOpen: true, brotliLibrary: fakeBrolib))
                {
                    byte[] scratch = new byte[1024];
                    int bytesRead = 1;
                    while (bytesRead != 0)
                    {
                        bytesRead = sourceStream.Read(scratch, 0, scratch.Length);
                        if (bytesRead > 0)
                        {
                            await compressor.WriteAsync(scratch, 0, bytesRead).ConfigureAwait(false);
                        }
                    }

                    try
                    {
                        fakeBrolib.ForceEncoderIsFinished = true;
                        await compressor.FlushAsync().ConfigureAwait(false);
                        Assert.Fail("Should have thrown a BrotliException");
                    }
                    catch (BrotliException e)
                    {
                        Assert.AreEqual("Unexepected finish signal from Brotli encoder", e.Message);
                    }
                }
            }
        }

        [TestMethod]
        public void BrotliCompressionStream_FailsInfiniteFlush()
        {
            FakeBrotliLib fakeBrolib = new FakeBrotliLib();
            fakeBrolib.FailInfiniteFlush = true;

            byte[] inputData = CreateSourceData(100000);
            using (MemoryStream sourceStream = new MemoryStream(inputData, index: 0, count: inputData.Length, writable: false))
            using (MemoryStream innerStream = new MemoryStream())
            {
                try
                {
                    using (BrotliCompressorStream compressor = new BrotliCompressorStream(innerStream, leaveOpen: true, brotliLibrary: fakeBrolib))
                    {
                        byte[] scratch = new byte[1024];
                        int bytesRead = 1;
                        while (bytesRead != 0)
                        {
                            bytesRead = sourceStream.Read(scratch, 0, scratch.Length);
                            if (bytesRead > 0)
                            {
                                compressor.Write(scratch, 0, bytesRead);
                                compressor.Flush();
                            }
                        }

                        Assert.Fail("Should have thrown a BrotliException");
                    }
                }
                catch (BrotliException e)
                {
                    Assert.AreEqual("Infinite loop detected during Brotli stream flush", e.Message);
                }
            }
        }

        [TestMethod]
        public async Task BrotliCompressionStream_FailsInfiniteFlushAsync()
        {
            FakeBrotliLib fakeBrolib = new FakeBrotliLib();
            fakeBrolib.FailInfiniteFlush = true;

            byte[] inputData = CreateSourceData(100000);
            using (MemoryStream sourceStream = new MemoryStream(inputData, index: 0, count: inputData.Length, writable: false))
            using (MemoryStream innerStream = new MemoryStream())
            {
                try
                {
                    using (BrotliCompressorStream compressor = new BrotliCompressorStream(innerStream, leaveOpen: true, brotliLibrary: fakeBrolib))
                    {
                        byte[] scratch = new byte[1024];
                        int bytesRead = 1;
                        while (bytesRead != 0)
                        {
                            bytesRead = sourceStream.Read(scratch, 0, scratch.Length);
                            if (bytesRead > 0)
                            {
                                await compressor.WriteAsync(scratch, 0, bytesRead).ConfigureAwait(false);
                                await compressor.FlushAsync().ConfigureAwait(false);
                            }
                        }

                        Assert.Fail("Should have thrown a BrotliException");
                    }
                }
                catch (BrotliException e)
                {
                    Assert.AreEqual("Infinite loop detected during Brotli stream flush", e.Message);
                }
            }
        }

        [TestMethod]
        public void BrotliCompressionStream_InvalidInitialCompressor()
        {
            FakeBrotliLib fakeBrolib = new FakeBrotliLib();
            fakeBrolib.FailCreateEncoder = true;

            try
            {
                using (MemoryStream compressedStream = new MemoryStream())
                using (BrotliCompressorStream compressor = new BrotliCompressorStream(compressedStream, brotliLibrary: fakeBrolib))
                {
                    Assert.Fail("Should have thrown a BrotliException");
                }
            }
            catch (BrotliException) { }
        }

        [TestMethod]
        public void BrotliCompressionStream_PartialBufferWrites()
        {
            FakeBrotliLib fakeBrolib = new FakeBrotliLib();
            fakeBrolib.PartialProcess = true;
            byte[] inputData = CreateSourceData(100000);
            using (MemoryStream sourceStream = new MemoryStream(inputData, index: 0, count: inputData.Length, writable: false))
            using (MemoryStream innerStream = new MemoryStream())
            {
                using (BrotliCompressorStream compressor = new BrotliCompressorStream(innerStream, leaveOpen: true, brotliLibrary: fakeBrolib))
                {
                    byte[] scratch = new byte[1024];
                    int bytesRead = 1;
                    while (bytesRead != 0)
                    {
                        bytesRead = sourceStream.Read(scratch, 0, scratch.Length);
                        if (bytesRead > 0)
                        {
                            compressor.Write(scratch, 0, bytesRead);
                        }
                    }

                    compressor.Flush();
                }

                // The fake brotli library will copy input straight across to output, so ensure
                // it's identical to validate that the buffer shift left routine is OK
                byte[] decompressedData = innerStream.ToArray();
                AssertSequenceEqual(inputData, decompressedData);
            }
        }

        [TestMethod]
        public async Task BrotliCompressionStream_PartialBufferWritesAsync()
        {
            FakeBrotliLib fakeBrolib = new FakeBrotliLib();
            fakeBrolib.PartialProcess = true;
            byte[] inputData = CreateSourceData(100000);
            using (MemoryStream sourceStream = new MemoryStream(inputData, index: 0, count: inputData.Length, writable: false))
            using (MemoryStream innerStream = new MemoryStream())
            {
                using (BrotliCompressorStream compressor = new BrotliCompressorStream(innerStream, leaveOpen: true, brotliLibrary: fakeBrolib))
                {
                    byte[] scratch = new byte[1024];
                    int bytesRead = 1;
                    while (bytesRead != 0)
                    {
                        bytesRead = sourceStream.Read(scratch, 0, scratch.Length);
                        if (bytesRead > 0)
                        {
                            await compressor.WriteAsync(scratch, 0, bytesRead).ConfigureAwait(false);
                        }
                    }

                    await compressor.FlushAsync().ConfigureAwait(false);
                }

                // The fake brotli library will copy input straight across to output, so ensure
                // it's identical to validate that the buffer shift left routine is OK
                byte[] decompressedData = innerStream.ToArray();
                AssertSequenceEqual(inputData, decompressedData);
            }
        }

        [TestMethod]
        public void BrotliCompressionStream_PartialBufferFlush()
        {
            FakeBrotliLib fakeBrolib = new FakeBrotliLib();
            fakeBrolib.PartialProcess = true;
            fakeBrolib.PartialFlush = true;
            byte[] inputData = CreateSourceData(100000);
            using (MemoryStream sourceStream = new MemoryStream(inputData, index: 0, count: inputData.Length, writable: false))
            using (MemoryStream innerStream = new MemoryStream())
            {
                using (BrotliCompressorStream compressor = new BrotliCompressorStream(innerStream, leaveOpen: true, brotliLibrary: fakeBrolib))
                {
                    byte[] scratch = new byte[1024];
                    int bytesRead = 1;
                    while (bytesRead != 0)
                    {
                        bytesRead = sourceStream.Read(scratch, 0, scratch.Length);
                        if (bytesRead > 0)
                        {
                            compressor.Write(scratch, 0, bytesRead);
                        }
                    }

                    compressor.Flush();
                }

                // The fake brotli library will copy input straight across to output, so ensure
                // it's identical to validate that the buffer shift left routine is OK
                byte[] decompressedData = innerStream.ToArray();
                AssertSequenceEqual(inputData, decompressedData);
            }
        }

        [TestMethod]
        public async Task BrotliCompressionStream_PartialBufferFlushAsync()
        {
            FakeBrotliLib fakeBrolib = new FakeBrotliLib();
            fakeBrolib.PartialProcess = true;
            fakeBrolib.PartialFlush = true;
            byte[] inputData = CreateSourceData(100000);
            using (MemoryStream sourceStream = new MemoryStream(inputData, index: 0, count: inputData.Length, writable: false))
            using (MemoryStream innerStream = new MemoryStream())
            {
                using (BrotliCompressorStream compressor = new BrotliCompressorStream(innerStream, leaveOpen: true, brotliLibrary: fakeBrolib))
                {
                    byte[] scratch = new byte[1024];
                    int bytesRead = 1;
                    while (bytesRead != 0)
                    {
                        bytesRead = sourceStream.Read(scratch, 0, scratch.Length);
                        if (bytesRead > 0)
                        {
                            await compressor.WriteAsync(scratch, 0, bytesRead).ConfigureAwait(false);
                        }
                    }

                    await compressor.FlushAsync().ConfigureAwait(false);
                }

                // The fake brotli library will copy input straight across to output, so ensure
                // it's identical to validate that the buffer shift left routine is OK
                byte[] decompressedData = innerStream.ToArray();
                AssertSequenceEqual(inputData, decompressedData);
            }
        }

        [TestMethod]
        public void BrotliCompressionStream_MultiFlush()
        {
            FakeBrotliLib fakeBrolib = new FakeBrotliLib();
            byte[] inputData = CreateSourceData(1024);

            using (MemoryStream compressedStream = new MemoryStream())
            {
                using (BrotliCompressorStream compressor = new BrotliCompressorStream(compressedStream, leaveOpen: true, brotliLibrary: fakeBrolib))
                {
                    compressor.Write(inputData, 0, inputData.Length);
                    fakeBrolib.SetEncoderFinishedRightNow(true);
                    compressor.Flush();
                    compressor.Flush();
                    compressor.Flush();
                }

                byte[] decompressedData = compressedStream.ToArray();
                AssertSequenceEqual(inputData, decompressedData);
            }
        }

        [TestMethod]
        public async Task BrotliCompressionStream_MultiFlushAsync()
        {
            FakeBrotliLib fakeBrolib = new FakeBrotliLib();
            byte[] inputData = CreateSourceData(1024);

            using (MemoryStream compressedStream = new MemoryStream())
            {
                using (BrotliCompressorStream compressor = new BrotliCompressorStream(compressedStream, leaveOpen: true, brotliLibrary: fakeBrolib))
                {
                    await compressor.WriteAsync(inputData, 0, inputData.Length).ConfigureAwait(false);
                    fakeBrolib.SetEncoderFinishedRightNow(true);
                    await compressor.FlushAsync().ConfigureAwait(false);
                    await compressor.FlushAsync().ConfigureAwait(false);
                    await compressor.FlushAsync().ConfigureAwait(false);
                }

                byte[] decompressedData = compressedStream.ToArray();
                AssertSequenceEqual(inputData, decompressedData);
            }
        }

        private class NullSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            public NullSafeHandle() : base(ownsHandle: true)
            {
                SetHandleAsInvalid();
            }

            protected override bool ReleaseHandle()
            {
                return true;
            }
        }

        private class FakeSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            public FakeSafeHandle() : base(ownsHandle: true)
            {
                SetHandle(Marshal.AllocHGlobal(8));
            }

            protected override bool ReleaseHandle()
            {
                if (!this.IsInvalid)
                {
                    Marshal.FreeHGlobal(this.handle);
                }

                return true;
            }
        }

        private class FakeBrotliLib : IBrolib
        {
            public bool FailCreateEncoder { get; set; }
            public bool FailCompressProcess { get; set; }
            public bool FailCompressFinish { get; set; }
            public bool? ForceEncoderIsFinished { get; set; }
            public bool FailInfiniteFlush { get; set; }
            public bool PartialProcess { get; set; }
            public bool PartialFlush { get; set; }

            private bool _encoderFinished = false;

            public uint EncoderVersion { get; set; }

            public void SetEncoderFinishedRightNow(bool encoderFinished)
            {
                ForceEncoderIsFinished = encoderFinished;
                _encoderFinished = encoderFinished;
            }

            public SafeHandle CreateDecoder()
            {
                throw new NotImplementedException();
            }

            public SafeHandle CreateEncoder()
            {
                if (FailCreateEncoder)
                {
                    return new NullSafeHandle();
                }
                else
                {
                    return new FakeSafeHandle();
                }
            }

            public BrotliDecoderResult DecoderDecompressStream(SafeHandle decoder, ref ulong availableIn, ref IntPtr nextIn, ref ulong availableOut, ref IntPtr nextOut, out ulong totalOut)
            {
                throw new NotImplementedException();
            }

            public string DecoderGetErrorString(SafeHandle decoder)
            {
                throw new NotImplementedException();
            }

            public bool DecoderIsFinished(SafeHandle decoder)
            {
                throw new NotImplementedException();
            }

            public unsafe bool EncoderCompressStream(
                SafeHandle encoder,
                BrotliEncoderOperation op,
                ref ulong availableIn,
                ref IntPtr nextIn,
                ref ulong availableOut,
                ref IntPtr nextOut, out ulong totalOut)
            {
                if (op == BrotliEncoderOperation.Process &&
                    FailCompressProcess)
                {
                    totalOut = 0;
                    return false;
                }

                if ((op == BrotliEncoderOperation.Flush ||
                    op == BrotliEncoderOperation.Finish) &&
                    FailCompressFinish)
                {
                    totalOut = 0;
                    return false;
                }

                if ((op == BrotliEncoderOperation.Flush ||
                    op == BrotliEncoderOperation.Finish) &&
                    FailInfiniteFlush)
                {
                    // simulate outputting a lot of data but not consuming any input
                    totalOut = availableOut;
                    availableOut = 0;
                    return true;
                }

                ulong bytesToProcess = Math.Min(availableIn, availableOut);

                // Simulate partial processing if enabled
                bool partial =
                    (PartialFlush &&
                    (op == BrotliEncoderOperation.Flush ||
                    op == BrotliEncoderOperation.Finish)) ||
                    (PartialProcess && op == BrotliEncoderOperation.Process);
                if (partial && bytesToProcess > 600)
                {
                    bytesToProcess /= 2;
                }

                // "compress" data by copying straight across
                byte* inBuf = (byte*)nextIn.ToPointer();
                byte* outBuf = (byte*)nextOut.ToPointer();
                for (int c = 0; c < (int)bytesToProcess; c++)
                {
                    outBuf[c] = inBuf[c];
                }

                availableOut -= bytesToProcess;
                availableIn -= bytesToProcess;
                totalOut = bytesToProcess;

                // Trigger the force finish flag here if applicable
                if (ForceEncoderIsFinished.HasValue)
                {
                    _encoderFinished = ForceEncoderIsFinished.Value;
                }
                else if (op == BrotliEncoderOperation.Finish)
                {
                    // Or finish normally
                    _encoderFinished = availableIn == 0;
                }

                return true;
            }

            public bool EncoderIsFinished(SafeHandle encoder)
            {
                return _encoderFinished;
            }

            public void EncoderSetParameter(SafeHandle encoder, BrotliEncoderParameter parameter, uint value)
            {
            }
        }

        private static void AssertSequenceEqual(byte[] a, byte[] b)
        {
            Assert.AreEqual(a.Length, b.Length);
            for (int c = 0; c < a.Length; c++)
            {
                Assert.AreEqual(a[c], b[c]);
            }
        }

        private static byte[] DecompressFromBrotli(byte[] compressedData)
        {
            using (MemoryStream outputStream = new MemoryStream())
            {
                using (MemoryStream inputStream = new MemoryStream(compressedData, writable: false))
                using (BrotliDecompressorStream decompressor = new BrotliDecompressorStream(inputStream))
                {
                    decompressor.CopyTo(outputStream);
                }

                return outputStream.ToArray();
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Test.Usage", "RoslynCQ_DoNotUseRandomInput:Do not use random input", Justification = "The test clearly uses a fixed random seed but the analyzer does not care about that")]
        private static byte[] CreateSourceData(int length)
        {
            Random rand = new Random(897821);
            StringBuilder builder = new StringBuilder(length);
            while (builder.Length < length)
            {
                switch (rand.Next(0, 10))
                {
                    case 0:
                        builder.Append("<add key=\"enabled\" value=\"True\" />");
                        break;
                    case 1:
                        builder.Append("Tag.Workflows.TagSearchWorkflow");
                        break;
                    case 2:
                        builder.Append("SubstrateRestApiUrlForExternalCaller");
                        break;
                    case 3:
                        builder.Append("fill -256 6 1231 -241 6 1184");
                        break;
                    case 4:
                        builder.Append("1206292f-4087");
                        break;
                    case 5:
                        builder.Append("System.Net.TlsStream.EndWrite");
                        break;
                    case 6:
                        builder.Append("AAAAAAAAAAAAAaaaaaAAAAAAaaAAAAAAAAAAAAAAAAAaaaaaa");
                        break;
                    case 7:
                        builder.Append("\"kifMajorVersion\": 0, \"kifMinorVersion\": 0, \"kifMinorVersion2\": 0");
                        break;
                    case 8:
                        builder.Append("This is just test data to use for testing compression");
                        break;
                    case 9:
                        builder.Append("\"localResultCount\": \"10\"");
                        break;
                }
            }

            return Encoding.UTF8.GetBytes(builder.ToString());
        }
    }
}
