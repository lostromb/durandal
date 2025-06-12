namespace Durandal.Tests.Compression.Brotli
{
    using Durandal.Common.Utils.NativePlatform;
    using Durandal.Extensions.Compression.Brotli;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Win32.SafeHandles;
    using System;
    using System.IO;
    using System.IO.Compression;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    [TestClass]
    public class BrotliDecompressorStreamTests
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
        public void BrotliDecompressionStream_InvalidArgs()
        {
            using (Stream wrapper = new MemoryStream())
            {
                try
                {
                    new BrotliDecompressorStream(null);
                    Assert.Fail("Should have thrown an ArgumentNullException");
                }
                catch (ArgumentNullException) { }

                try
                {
                    using (Stream nonReadableStream = new GZipStream(new MemoryStream(), CompressionMode.Compress))
                    {
                        new BrotliDecompressorStream(nonReadableStream);
                        Assert.Fail("Should have thrown an ArgumentException");
                    }
                }
                catch (ArgumentException) { }

                BrotliDecompressorStream stream = new BrotliDecompressorStream(wrapper);
                Assert.IsNotNull(stream);
            }
        }

        [TestMethod]
        public void BrotliDecompressionStream_PassthroughInnerStream()
        {
            byte[] inputData = CreateSourceData(1000000);
            byte[] compressedData = CompressToBrotli(inputData);
            using (MemoryStream sourceStream = new MemoryStream(compressedData, index: 0, count: compressedData.Length, writable: false))
            using (MemoryStream innerStream = new MemoryStream())
            {
                BrotliDecompressorStream decompressor = new BrotliDecompressorStream(sourceStream);
                decompressor.CopyTo(innerStream);
                Assert.AreEqual(false, decompressor.CanWrite);
                Assert.AreEqual(false, decompressor.CanSeek);
                Assert.AreEqual(innerStream.CanRead, decompressor.CanRead);
                Assert.AreEqual(inputData.LongLength, decompressor.Length);
                Assert.AreEqual(inputData.LongLength, decompressor.Position);
            }
        }

        [TestMethod]
        public async Task BrotliDecompressionStream_InvalidOperations()
        {
            using (Stream innerStream = new MemoryStream())
            {
                BrotliDecompressorStream decompressor = new BrotliDecompressorStream(innerStream);

                try
                {
                    decompressor.Seek(0, SeekOrigin.Begin);
                    Assert.Fail("Should have thrown an InvalidOperationException");
                }
                catch (InvalidOperationException) { }
                try
                {
                    decompressor.SetLength(0);
                    Assert.Fail("Should have thrown an InvalidOperationException");
                }
                catch (InvalidOperationException) { }
                try
                {
                    decompressor.Write(new byte[1], 0, 1);
                    Assert.Fail("Should have thrown an InvalidOperationException");
                }
                catch (InvalidOperationException) { }
                try
                {
                    await decompressor.WriteAsync(new byte[1], 0, 1, CancellationToken.None);
                    Assert.Fail("Should have thrown an InvalidOperationException");
                }
                catch (InvalidOperationException) { }
                try
                {
                    decompressor.Flush();
                    Assert.Fail("Should have thrown an InvalidOperationException");
                }
                catch (InvalidOperationException) { }
                try
                {
                    await decompressor.FlushAsync(CancellationToken.None);
                    Assert.Fail("Should have thrown an InvalidOperationException");
                }
                catch (InvalidOperationException) { }
                try
                {
                    decompressor.Position = 0;
                    Assert.Fail("Should have thrown an InvalidOperationException");
                }
                catch (InvalidOperationException) { }
            }
        }

        [TestMethod]
        public void BrotliDecompressionStream_MultipleDisposal()
        {
            using (Stream innerStream = new MemoryStream())
            using (BrotliDecompressorStream compressor = new BrotliDecompressorStream(innerStream))
            {
                compressor.Dispose();
                compressor.Dispose();
            }
        }

        [TestMethod]
        public async Task BrotliDecompressionStream_ChecksForDisposedOnRead()
        {
            byte[] scratch = new byte[1];
            using (Stream innerStream = new MemoryStream())
            {
                BrotliDecompressorStream decompressor = new BrotliDecompressorStream(innerStream);
                decompressor.Dispose();

                try
                {
                    decompressor.Read(scratch, 0, 1);
                    Assert.Fail("Expected an ObjectDisposedException");
                }
                catch (ObjectDisposedException) { }

                try
                {
                    await decompressor.ReadAsync(scratch, 0, 1, CancellationToken.None).ConfigureAwait(false);
                    Assert.Fail("Expected an ObjectDisposedException");
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
        [DataRow(10000000, 100000)]
        public void BrotliDecompressionStream_BasicRead(int approximateInputSize, int readSize)
        {
            byte[] originalData = CreateSourceData(approximateInputSize);
            byte[] compressedData = CompressToBrotli(originalData);

            using (MemoryStream sourceStream = new MemoryStream(compressedData, index: 0, count: compressedData.Length, writable: false))
            using (MemoryStream outputStream = new MemoryStream())
            {
                using (BrotliDecompressorStream decompressor = new BrotliDecompressorStream(sourceStream))
                {
                    byte[] scratch = new byte[readSize];
                    int bytesRead = 1;
                    while (bytesRead != 0)
                    {
                        bytesRead = decompressor.Read(scratch, 0, scratch.Length);
                        if (bytesRead > 0)
                        {
                            outputStream.Write(scratch, 0, bytesRead);
                        }
                    }
                }

                // Assert that we decompressed the data we expect
                byte[] decompressedData = outputStream.ToArray();
                AssertSequenceEqual(decompressedData, originalData);
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
        [DataRow(10000000, 100000)]
        public async Task BrotliDecompressionStream_BasicReadAsync(int approximateInputSize, int readSize)
        {
            byte[] originalData = CreateSourceData(approximateInputSize);
            byte[] compressedData = CompressToBrotli(originalData);

            using (MemoryStream sourceStream = new MemoryStream(compressedData, index: 0, count: compressedData.Length, writable: false))
            using (MemoryStream outputStream = new MemoryStream())
            {
                using (BrotliDecompressorStream decompressor = new BrotliDecompressorStream(sourceStream))
                {
                    byte[] scratch = new byte[readSize];
                    int bytesRead = 1;
                    while (bytesRead != 0)
                    {
                        bytesRead = await decompressor.ReadAsync(scratch, 0, scratch.Length).ConfigureAwait(false);
                        if (bytesRead > 0)
                        {
                            outputStream.Write(scratch, 0, bytesRead);
                        }
                    }
                }

                // Assert that we decompressed the data we expect
                byte[] decompressedData = outputStream.ToArray();
                AssertSequenceEqual(decompressedData, originalData);
            }
        }

        [TestMethod]
        [DataRow(10)]
        [DataRow(100)]
        [DataRow(1000)]
        [DataRow(10000)]
        public void BrotliDecompressionStream_TruncatedInput(int dataLength)
        {
            byte[] originalData = CreateSourceData(dataLength);
            byte[] compressedData = CompressToBrotli(originalData);

            try
            {
                using (MemoryStream sourceStream = new MemoryStream(compressedData, index: 0, count: compressedData.Length / 2, writable: false))
                using (MemoryStream outputStream = new MemoryStream())
                {
                    using (BrotliDecompressorStream decompressor = new BrotliDecompressorStream(sourceStream))
                    {
                        byte[] scratch = new byte[1024];
                        int bytesRead = 1;
                        while (bytesRead != 0)
                        {
                            bytesRead = decompressor.Read(scratch, 0, scratch.Length);
                            if (bytesRead > 0)
                            {
                                outputStream.Write(scratch, 0, bytesRead);
                            }
                        }
                    }

                    Assert.Fail("Should have thrown a BrotliException");
                }
            }
            catch (BrotliException) { }
        }

        [TestMethod]
        [DataRow(10)]
        [DataRow(100)]
        [DataRow(1000)]
        [DataRow(10000)]
        public async Task BrotliDecompressionStream_TruncatedInputAsync(int dataLength)
        {
            byte[] originalData = CreateSourceData(dataLength);
            byte[] compressedData = CompressToBrotli(originalData);

            try
            {
                using (MemoryStream sourceStream = new MemoryStream(compressedData, index: 0, count: compressedData.Length / 2, writable: false))
                using (MemoryStream outputStream = new MemoryStream())
                {
                    using (BrotliDecompressorStream decompressor = new BrotliDecompressorStream(sourceStream))
                    {
                        byte[] scratch = new byte[1024];
                        int bytesRead = 1;
                        while (bytesRead != 0)
                        {
                            bytesRead = await decompressor.ReadAsync(scratch, 0, scratch.Length).ConfigureAwait(false);
                            if (bytesRead > 0)
                            {
                                outputStream.Write(scratch, 0, bytesRead);
                            }
                        }
                    }

                    Assert.Fail("Should have thrown a BrotliException");
                }
            }
            catch (BrotliException) { }
        }

        [TestMethod]
        public void BrotliDecompressionStream_InvalidData()
        {
            byte[] junkData = CreateSourceData(10000);

            try
            {
                using (MemoryStream sourceStream = new MemoryStream(junkData, index: 0, count: junkData.Length, writable: false))
                {
                    using (BrotliDecompressorStream decompressor = new BrotliDecompressorStream(sourceStream))
                    {
                        byte[] scratch = new byte[1024];
                        decompressor.Read(scratch, 0, scratch.Length);
                    }

                    Assert.Fail("Should have thrown a BrotliException");
                }
            }
            catch (BrotliException) { }
        }

        [TestMethod]
        public async Task BrotliDecompressionStream_InvalidDataAsync()
        {
            byte[] junkData = CreateSourceData(10000);

            try
            {
                using (MemoryStream sourceStream = new MemoryStream(junkData, index: 0, count: junkData.Length, writable: false))
                {
                    using (BrotliDecompressorStream decompressor = new BrotliDecompressorStream(sourceStream))
                    {
                        byte[] scratch = new byte[1024];
                        await decompressor.ReadAsync(scratch, 0, scratch.Length).ConfigureAwait(false);
                    }

                    Assert.Fail("Should have thrown a BrotliException");
                }
            }
            catch (BrotliException) { }
        }

        [TestMethod]
        public void BrotliDecompressionStream_FailToCreateDecoder()
        {
            IBrolib fakeBrotliLibrary = new BrolibThatReturnsNullDecoder();

            try
            {
                using (Stream stream = new MemoryStream())
                {
                    new BrotliDecompressorStream(stream, fakeBrotliLibrary);
                    Assert.Fail("Should have thrown a BrotliException");
                }
            }
            catch (BrotliException) { }
        }

        private class BrolibThatReturnsNullDecoder : IBrolib
        {
            public uint EncoderVersion => throw new NotImplementedException();

            public SafeHandle CreateDecoder()
            {
                return new NullSafeHandle();
            }

            public SafeHandle CreateEncoder()
            {
                throw new NotImplementedException();
            }

            public BrotliDecoderResult DecoderDecompressStream(SafeHandle decoder, ref ulong availableIn, ref nint nextIn, ref ulong availableOut, ref nint nextOut, out ulong totalOut)
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

            public bool EncoderCompressStream(SafeHandle encoder, BrotliEncoderOperation op, ref ulong availableIn, ref nint nextIn, ref ulong availableOut, ref nint nextOut, out ulong totalOut)
            {
                throw new NotImplementedException();
            }

            public bool EncoderIsFinished(SafeHandle encoder)
            {
                throw new NotImplementedException();
            }

            public void EncoderSetParameter(SafeHandle encoder, BrotliEncoderParameter parameter, uint value)
            {
                throw new NotImplementedException();
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

        private static void AssertSequenceEqual(byte[] a, byte[] b)
        {
            Assert.AreEqual(a.Length, b.Length);
            for (int c = 0; c < a.Length; c++)
            {
                Assert.AreEqual(a[c], b[c]);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Test.Usage", "RoslynCQ_DoNotUseRandomInput:Do not use random input", Justification = "The test clearly uses a fixed random seed but the analyzer does not care about that")]
        private static byte[] CreateSourceData(int length)
        {
            Random rand = new Random(178459322);
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

        private static byte[] CompressToBrotli(byte[] data)
        {
            using (MemoryStream outputStream = new MemoryStream())
            {
                using (MemoryStream inputStream = new MemoryStream(data, writable: false))
                using (BrotliCompressorStream compressor = new BrotliCompressorStream(outputStream, leaveOpen: true, quality: 3, window: 12))
                {
                    inputStream.CopyTo(compressor);
                }

                return outputStream.ToArray();
            }
        }
    }
}
