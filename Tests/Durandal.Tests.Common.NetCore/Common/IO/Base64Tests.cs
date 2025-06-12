using Durandal.Common.Audio;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.Audio.Components;
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
using Durandal.Common.Test;
using Durandal.Common.Collections;

namespace Durandal.Tests.Common.IO
{
    [TestClass]
    public class Base64Tests
    {
        #region Encoder stream

        [TestMethod]
        public async Task TestBase64EncodingStreamWriteParity()
        {
            IRandom rand = new FastRandom();
            byte[] inputData = new byte[65536 * 4];
            for (int c = 0; c < 100; c++)
            {
                int inputDataLength = rand.NextInt(1, inputData.Length);
                rand.NextBytes(inputData, 0, inputDataLength);
                string expectedBase64 = Convert.ToBase64String(inputData, 0, inputDataLength);
                byte[] expectedBase64Ascii = Encoding.ASCII.GetBytes(expectedBase64);

                using (MemoryStream encodedOutput = new MemoryStream())
                {
                    using (Base64AsciiEncodingStream encoder = new Base64AsciiEncodingStream(encodedOutput, StreamDirection.Write, false))
                    {
                        int bytesWritten = 0;
                        while (bytesWritten < inputDataLength)
                        {
                            int nextWriteSize = Math.Min(inputDataLength - bytesWritten, rand.NextInt(1, 2048));

                            // Use random different variations of the write method
                            int operationType = rand.NextInt(0, 10);
                            if (operationType == 0)
                            {
                                encoder.Write(inputData, bytesWritten, nextWriteSize);
                            }
                            else if (operationType == 1)
                            {
                                encoder.Write(inputData, bytesWritten, nextWriteSize, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                            }
                            else
                            {
                                await encoder.WriteAsync(inputData, bytesWritten, nextWriteSize, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                            }

                            operationType = rand.NextInt(0, 10);
                            if (operationType == 0)
                            {
                                encoder.Flush();
                            }
                            else if (operationType == 1)
                            {
                                await encoder.FlushAsync().ConfigureAwait(false);
                            }
                            else if (operationType == 2)
                            {
                                await encoder.FlushAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                            }

                            bytesWritten += nextWriteSize;
                        }

                        await encoder.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        Assert.AreEqual(encodedOutput.Position, encoder.Position);
                    }

                    byte[] actualBase64Ascii = encodedOutput.ToArray();
                    if (!ArrayExtensions.ArrayEquals(expectedBase64Ascii, actualBase64Ascii))
                    {
                        string actualBase64 = Encoding.ASCII.GetString(actualBase64Ascii);
                        Console.WriteLine("Expect " + expectedBase64);
                        Console.WriteLine("Actual " + actualBase64);
                        Assert.Fail("Base64 did not match");
                    }
                }
            }
        }

        [TestMethod]
        public async Task TestBase64EncodingStreamReadParity()
        {
            IRandom rand = new FastRandom();
            byte[] inputData = new byte[65536 * 4];
            for (int c = 0; c < 100; c++)
            {
                int inputDataLength = rand.NextInt(1, inputData.Length);
                rand.NextBytes(inputData, 0, inputDataLength);
                string expectedBase64 = Convert.ToBase64String(inputData, 0, inputDataLength);
                byte[] expectedBase64Ascii = Encoding.ASCII.GetBytes(expectedBase64);

                using (MemoryStream inputDataSource = new MemoryStream(inputData, 0, inputDataLength))
                using (NonRealTimeStreamWrapper wrapper = new NonRealTimeStreamWrapper(inputDataSource, false))
                using (SimulatedUnreliableStream unreliableStream = new SimulatedUnreliableStream(wrapper))
                using (Base64AsciiEncodingStream encoder = new Base64AsciiEncodingStream(unreliableStream, StreamDirection.Read, false))
                using (PooledBuffer<byte> scratch = BufferPool<byte>.Rent(2048))
                {
                    using (MemoryStream outputData = new MemoryStream())
                    {
                        int thisReadSize = 1;
                        while (thisReadSize > 0)
                        {
                            int nextReadOffset = rand.NextInt(0, 256);
                            int nextReadSize = rand.NextInt(1, scratch.Length - nextReadOffset);

                            // Use random different variations of the read method
                            int operationType = rand.NextInt(0, 10);
                            if (operationType == 0)
                            {
                                thisReadSize = encoder.Read(scratch.Buffer, nextReadOffset, nextReadSize);
                            }
                            else if (operationType == 1)
                            {
                                thisReadSize = encoder.Read(scratch.Buffer, nextReadOffset, nextReadSize, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                            }
                            else
                            {
                                thisReadSize = await encoder.ReadAsync(scratch.Buffer, nextReadOffset, nextReadSize, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                            }
                            
                            if (thisReadSize > 0)
                            {
                                outputData.Write(scratch.Buffer, nextReadOffset, thisReadSize);
                            }
                        }

                        byte[] actualBase64Ascii = outputData.ToArray();
                        Assert.AreEqual(outputData.Position, encoder.Position);
                        if (!ArrayExtensions.ArrayEquals(expectedBase64Ascii, actualBase64Ascii))
                        {
                            string actualBase64 = Encoding.ASCII.GetString(actualBase64Ascii);
                            Console.WriteLine("Expect " + expectedBase64);
                            Console.WriteLine("Actual " + actualBase64);
                            Assert.Fail("Base64 did not match");
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void TestBase64EncodingStreamCopyFrom()
        {
            IRandom rand = new FastRandom();
            byte[] inputData = new byte[65536];
            for (int c = 0; c < 100; c++)
            {
                int inputDataLength = rand.NextInt(1, inputData.Length);
                rand.NextBytes(inputData, 0, inputDataLength);
                string expectedBase64 = Convert.ToBase64String(inputData, 0, inputDataLength);
                byte[] expectedBase64Ascii = Encoding.ASCII.GetBytes(expectedBase64);

                using (MemoryStream inputDataSource = new MemoryStream(inputData, 0, inputDataLength))
                using (NonRealTimeStreamWrapper wrapper = new NonRealTimeStreamWrapper(inputDataSource, false))
                using (SimulatedUnreliableStream unreliableStream = new SimulatedUnreliableStream(wrapper))
                using (Base64AsciiEncodingStream encoder = new Base64AsciiEncodingStream(unreliableStream, StreamDirection.Read, false))
                using (MemoryStream outputData = new MemoryStream())
                {
                    encoder.CopyTo(outputData);
                    byte[] actualBase64Ascii = outputData.ToArray();
                    Assert.AreEqual(outputData.Position, encoder.Position);
                    if (!ArrayExtensions.ArrayEquals(expectedBase64Ascii, actualBase64Ascii))
                    {
                        string actualBase64 = Encoding.ASCII.GetString(actualBase64Ascii);
                        Console.WriteLine("Expect " + expectedBase64);
                        Console.WriteLine("Actual " + actualBase64);
                        Assert.Fail("Base64 did not match");
                    }
                }
            }
        }

        [TestMethod]
        public async Task TestBase64EncodingStreamCopyFromAsync()
        {
            IRandom rand = new FastRandom();
            byte[] inputData = new byte[65536];
            for (int c = 0; c < 100; c++)
            {
                int inputDataLength = rand.NextInt(1, inputData.Length);
                rand.NextBytes(inputData, 0, inputDataLength);
                string expectedBase64 = Convert.ToBase64String(inputData, 0, inputDataLength);
                byte[] expectedBase64Ascii = Encoding.ASCII.GetBytes(expectedBase64);

                using (MemoryStream inputDataSource = new MemoryStream(inputData, 0, inputDataLength))
                using (NonRealTimeStreamWrapper wrapper = new NonRealTimeStreamWrapper(inputDataSource, false))
                using (SimulatedUnreliableStream unreliableStream = new SimulatedUnreliableStream(wrapper))
                using (Base64AsciiEncodingStream encoder = new Base64AsciiEncodingStream(unreliableStream, StreamDirection.Read, false))
                using (MemoryStream outputData = new MemoryStream())
                {
                    await encoder.CopyToAsync(outputData);
                    byte[] actualBase64Ascii = outputData.ToArray();
                    Assert.AreEqual(outputData.Position, encoder.Position);
                    if (!ArrayExtensions.ArrayEquals(expectedBase64Ascii, actualBase64Ascii))
                    {
                        string actualBase64 = Encoding.ASCII.GetString(actualBase64Ascii);
                        Console.WriteLine("Expect " + expectedBase64);
                        Console.WriteLine("Actual " + actualBase64);
                        Assert.Fail("Base64 did not match");
                    }
                }
            }
        }

        [TestMethod]
        public async Task TestBase64EncodingStreamCopyToAsync()
        {
            IRandom rand = new FastRandom();
            byte[] inputData = new byte[65536];
            for (int c = 0; c < 100; c++)
            {
                int inputDataLength = rand.NextInt(1, inputData.Length);
                rand.NextBytes(inputData, 0, inputDataLength);
                string expectedBase64 = Convert.ToBase64String(inputData, 0, inputDataLength);
                byte[] expectedBase64Ascii = Encoding.ASCII.GetBytes(expectedBase64);

                using (MemoryStream outputData = new MemoryStream())
                using (NonRealTimeStreamWrapper wrapper = new NonRealTimeStreamWrapper(outputData, false))
                using (Base64AsciiEncodingStream encoder = new Base64AsciiEncodingStream(wrapper, StreamDirection.Write, false))
                using (MemoryStream inputDataSource = new MemoryStream(inputData, 0, inputDataLength))
                {
                    await inputDataSource.CopyToAsync(encoder).ConfigureAwait(false);
                    await encoder.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    byte[] actualBase64Ascii = outputData.ToArray();
                    Assert.AreEqual(outputData.Position, encoder.Position);
                    if (!ArrayExtensions.ArrayEquals(expectedBase64Ascii, actualBase64Ascii))
                    {
                        string actualBase64 = Encoding.ASCII.GetString(actualBase64Ascii);
                        Console.WriteLine("Expect " + expectedBase64);
                        Console.WriteLine("Actual " + actualBase64);
                        Assert.Fail("Base64 did not match");
                    }
                }
            }
        }

        [TestMethod]
        public void TestBase64EncodingStreamNullInnerStream()
        {
            try
            {
                using (Base64AsciiEncodingStream encoder = new Base64AsciiEncodingStream(null, StreamDirection.Write, false))
                {
                    Assert.Fail("Should have thrown a ArgumentNullException");
                }
            }
            catch (ArgumentNullException) { }
        }


        [TestMethod]
        public void TestBase64EncodingStreamUnknownStreamDirection()
        {
            try
            {
                using (MemoryStream innerStream = new MemoryStream(new byte[100], true))
                using (Base64AsciiEncodingStream encoder = new Base64AsciiEncodingStream(innerStream, StreamDirection.Unknown, false))
                {
                    Assert.Fail("Should have thrown a ArgumentException");
                }
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void TestBase64EncodingStreamNonWritableWriteStream()
        {
            try
            {
                using (MemoryStream innerStream = new MemoryStream(new byte[100], false))
                using (Base64AsciiEncodingStream encoder = new Base64AsciiEncodingStream(innerStream, StreamDirection.Write, false))
                {
                    Assert.Fail("Should have thrown a ArgumentException");
                }
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void TestBase64EncodingStreamNonReadableReadStream()
        {
            try
            {
                using (PipeStream pipe = new PipeStream())
                using (Base64AsciiEncodingStream encoder = new Base64AsciiEncodingStream(pipe.GetWriteStream(), StreamDirection.Read, false))
                {
                    Assert.Fail("Should have thrown a ArgumentException");
                }
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void TestBase64EncodingStreamExpectedPropertiesOnReadStream()
        {
            using (MemoryStream innerStream = new MemoryStream(new byte[100], false))
            using (Base64AsciiEncodingStream encoder = new Base64AsciiEncodingStream(innerStream, StreamDirection.Read, false))
            {
                Assert.IsTrue(encoder.CanRead);
                Assert.IsFalse(encoder.CanWrite);
                Assert.IsFalse(encoder.CanSeek);
                Assert.IsFalse(encoder.CanTimeout);
                Assert.AreEqual(0, encoder.Position);
            }
        }

        [TestMethod]
        public void TestBase64EncodingStreamExpectedPropertiesOnWriteStream()
        {
            using (PipeStream pipe = new PipeStream())
            using (Base64AsciiEncodingStream encoder = new Base64AsciiEncodingStream(pipe.GetWriteStream(), StreamDirection.Write, false))
            {
                Assert.IsFalse(encoder.CanRead);
                Assert.IsTrue(encoder.CanWrite);
                Assert.IsFalse(encoder.CanSeek);
                Assert.IsFalse(encoder.CanTimeout);
                Assert.AreEqual(0, encoder.Position);
            }
        }

        [TestMethod]
        public void TestBase64EncodingStreamInvalidCopyToOnWriteStream()
        {
            try
            {
                using (MemoryStream innerStream = new MemoryStream())
                using (Base64AsciiEncodingStream encoder = new Base64AsciiEncodingStream(innerStream, StreamDirection.Write, false))
                using (MemoryStream targetStream = new MemoryStream())
                {
                    encoder.CopyTo(targetStream);
                    Assert.Fail("Should have thrown a NotSupportedException");
                }
            }
            catch (NotSupportedException) { }
        }

        [TestMethod]
        public async Task TestBase64EncodingStreamInvalidFlushOnReadStream()
        {
            try
            {
                using (MemoryStream innerStream = new MemoryStream())
                using (Base64AsciiEncodingStream encoder = new Base64AsciiEncodingStream(innerStream, StreamDirection.Read, false))
                {
                    encoder.Flush();
                    Assert.Fail("Should have thrown a InvalidOperationException");
                }
            }
            catch (InvalidOperationException) { }

            try
            {
                using (MemoryStream innerStream = new MemoryStream())
                using (Base64AsciiEncodingStream encoder = new Base64AsciiEncodingStream(innerStream, StreamDirection.Read, false))
                {
                    await encoder.FlushAsync().ConfigureAwait(false);
                    Assert.Fail("Should have thrown a InvalidOperationException");
                }
            }
            catch (InvalidOperationException) { }
        }

        [TestMethod]
        public async Task TestBase64EncodingStreamInvalidReadFromWriteStream()
        {
            byte[] scratch = new byte[100];

            try
            {
                using (MemoryStream innerStream = new MemoryStream())
                using (Base64AsciiEncodingStream encoder = new Base64AsciiEncodingStream(innerStream, StreamDirection.Write, false))
                {
                    encoder.Read(scratch, 0, 100, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.Fail("Should have thrown a InvalidOperationException");
                }
            }
            catch (InvalidOperationException) { }

            try
            {
                using (MemoryStream innerStream = new MemoryStream())
                using (Base64AsciiEncodingStream encoder = new Base64AsciiEncodingStream(innerStream, StreamDirection.Write, false))
                {
                    await encoder.ReadAsync(scratch, 0, 100, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.Fail("Should have thrown a InvalidOperationException");
                }
            }
            catch (InvalidOperationException) { }
        }

        [TestMethod]
        public async Task TestBase64EncodingStreamInvalidWriteToReadStream()
        {
            byte[] scratch = new byte[100];

            try
            {
                using (MemoryStream innerStream = new MemoryStream())
                using (Base64AsciiEncodingStream encoder = new Base64AsciiEncodingStream(innerStream, StreamDirection.Read, false))
                {
                    encoder.Write(scratch, 0, 100, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.Fail("Should have thrown a InvalidOperationException");
                }
            }
            catch (InvalidOperationException) { }

            try
            {
                using (MemoryStream innerStream = new MemoryStream())
                using (Base64AsciiEncodingStream encoder = new Base64AsciiEncodingStream(innerStream, StreamDirection.Read, false))
                {
                    await encoder.WriteAsync(scratch, 0, 100, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.Fail("Should have thrown a InvalidOperationException");
                }
            }
            catch (InvalidOperationException) { }

            try
            {
                using (MemoryStream innerStream = new MemoryStream())
                using (Base64AsciiEncodingStream encoder = new Base64AsciiEncodingStream(innerStream, StreamDirection.Read, false))
                {
                    await encoder.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.Fail("Should have thrown a InvalidOperationException");
                }
            }
            catch (InvalidOperationException) { }
        }

        [TestMethod]
        public async Task TestBase64EncodingStreamInvalidOperationsAfterFinish()
        {
            byte[] scratch = new byte[100];

            using (MemoryStream innerStream = new MemoryStream())
            using (Base64AsciiEncodingStream encoder = new Base64AsciiEncodingStream(innerStream, StreamDirection.Write, false))
            {
                await encoder.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);

                try
                {
                    await encoder.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.Fail("Should have thrown a InvalidOperationException");
                }
                catch (InvalidOperationException) { }

                try
                {
                    await encoder.WriteAsync(scratch, 0, 100, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.Fail("Should have thrown a InvalidOperationException");
                }
                catch (InvalidOperationException) { }

                try
                {
                    encoder.Write(scratch, 0, 100, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.Fail("Should have thrown a InvalidOperationException");
                }
                catch (InvalidOperationException) { }
            }
        }

        [TestMethod]
        public async Task TestBase64EncodingStreamOwnsInnerStream()
        {
            byte[] scratch = new byte[100];
            using (Base64AsciiEncodingStream encoder = new Base64AsciiEncodingStream(new MemoryStream(), StreamDirection.Write, true))
            {
                await encoder.WriteAsync(scratch, 0, 100, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                await encoder.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
            }
        }

        [TestMethod]
        public async Task TestBase64EncodingStreamBehavesAfterDisposal()
        {
            using (Base64AsciiEncodingStream encoder = new Base64AsciiEncodingStream(new MemoryStream(), StreamDirection.Write, true))
            {
                encoder.Dispose();

                try
                {
                    encoder.Flush();
                    Assert.Fail("Should have thrown a ObjectDisposedException");
                } catch (ObjectDisposedException) { }

                try
                {
                    await encoder.FlushAsync().ConfigureAwait(false);
                    Assert.Fail("Should have thrown a ObjectDisposedException");
                } catch (ObjectDisposedException) { }

                try
                {
                    encoder.Read(new byte[10], 0, 10, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.Fail("Should have thrown a ObjectDisposedException");
                } catch (ObjectDisposedException) { }

                try
                {
                    await encoder.ReadAsync(new byte[10], 0, 10, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.Fail("Should have thrown a ObjectDisposedException");
                } catch (ObjectDisposedException) { }

                try
                {
                    encoder.Write(new byte[10], 0, 10, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.Fail("Should have thrown a ObjectDisposedException");
                } catch (ObjectDisposedException) { }

                try
                {
                    await encoder.WriteAsync(new byte[10], 0, 10, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.Fail("Should have thrown a ObjectDisposedException");
                } catch (ObjectDisposedException) { }

                try
                {
                    await encoder.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.Fail("Should have thrown a ObjectDisposedException");
                } catch (ObjectDisposedException) { }

                encoder.Dispose();
            }
        }

        [TestMethod]
        public void TestBase64EncodingStreamNotSupportedOperations()
        {
            using (MemoryStream innerStream = new MemoryStream(new byte[100], false))
            using (Base64AsciiEncodingStream encoder = new Base64AsciiEncodingStream(innerStream, StreamDirection.Read, false))
            {
                try
                {
                    encoder.Length.GetHashCode();
                    Assert.Fail("Should have thrown a NotSupportedException");
                }
                catch (NotSupportedException) { }

                try
                {
                    encoder.Position = 0;
                    Assert.Fail("Should have thrown a InvalidOperationException");
                }
                catch (InvalidOperationException) { }

                try
                {
                    encoder.Seek(0, SeekOrigin.Begin);
                    Assert.Fail("Should have thrown a InvalidOperationException");
                }
                catch (InvalidOperationException) { }

                try
                {
                    encoder.SetLength(0);
                    Assert.Fail("Should have thrown a InvalidOperationException");
                }
                catch (InvalidOperationException) { }
            }
        }

        #endregion

        #region Decoder stream

        [TestMethod]
        public async Task TestBase64DecodingStreamWriteParity()
        {
            IRandom rand = new FastRandom();
            byte[] inputData = new byte[65536];
            for (int c = 0; c < 100; c++)
            {
                int expectedDataLength = rand.NextInt(1, inputData.Length);
                rand.NextBytes(inputData, 0, expectedDataLength);

                string inputBase64 = Convert.ToBase64String(inputData, 0, expectedDataLength);
                byte[] inputBase64Ascii = Encoding.ASCII.GetBytes(inputBase64);
                int inputDataLength = inputBase64Ascii.Length;

                using (MemoryStream decodedOutput = new MemoryStream())
                {
                    using (Base64AsciiDecodingStream decoder = new Base64AsciiDecodingStream(decodedOutput, StreamDirection.Write, false))
                    {
                        int bytesWritten = 0;
                        while (bytesWritten < inputDataLength)
                        {
                            int nextWriteSize = Math.Min(inputDataLength - bytesWritten, rand.NextInt(1, 2048));

                            // Use random different variations of the write method
                            int operationType = rand.NextInt(0, 10);
                            if (operationType == 0)
                            {
                                decoder.Write(inputBase64Ascii, bytesWritten, nextWriteSize);
                            }
                            else if (operationType == 1)
                            {
                                decoder.Write(inputBase64Ascii, bytesWritten, nextWriteSize, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                            }
                            else
                            {
                                await decoder.WriteAsync(inputBase64Ascii, bytesWritten, nextWriteSize, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                            }

                            operationType = rand.NextInt(0, 10);
                            if (operationType == 0)
                            {
                                decoder.Flush();
                            }
                            else if (operationType == 1)
                            {
                                await decoder.FlushAsync().ConfigureAwait(false);
                            }
                            else if (operationType == 2)
                            {
                                await decoder.FlushAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                            }

                            bytesWritten += nextWriteSize;
                        }

                        await decoder.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        Assert.AreEqual(decodedOutput.Position, decoder.Position);
                    }

                    byte[] actualDecodedData = decodedOutput.ToArray();
                    if (!ArrayExtensions.ArrayEquals(inputData, 0, actualDecodedData, 0, expectedDataLength))
                    {
                        Assert.Fail("Decoded data did not match");
                    }
                }
            }
        }

        [TestMethod]
        public async Task TestBase64DecodingStreamReadParity()
        {
            IRandom rand = new FastRandom();
            byte[] expectedOutputData = new byte[65536];
            for (int c = 0; c < 100; c++)
            {
                int expectedDataLength = rand.NextInt(1, expectedOutputData.Length);
                rand.NextBytes(expectedOutputData, 0, expectedDataLength);
                string inputBase64 = Convert.ToBase64String(expectedOutputData, 0, expectedDataLength);
                byte[] inputBase64Ascii = Encoding.ASCII.GetBytes(inputBase64);

                using (MemoryStream inputDataSource = new MemoryStream(inputBase64Ascii, 0, inputBase64Ascii.Length))
                using (NonRealTimeStreamWrapper wrapper = new NonRealTimeStreamWrapper(inputDataSource, false))
                using (SimulatedUnreliableStream unreliableStream = new SimulatedUnreliableStream(wrapper))
                using (Base64AsciiDecodingStream encoder = new Base64AsciiDecodingStream(unreliableStream, StreamDirection.Read, false))
                using (PooledBuffer<byte> scratch = BufferPool<byte>.Rent(2048))
                {
                    using (MemoryStream outputData = new MemoryStream())
                    {
                        int thisReadSize = 1;
                        while (thisReadSize > 0)
                        {
                            int nextReadOffset = rand.NextInt(0, 256);
                            int nextReadSize = rand.NextInt(1, scratch.Length - nextReadOffset);

                            // Use random different variations of the read method
                            int operationType = rand.NextInt(0, 10);
                            if (operationType == 0)
                            {
                                thisReadSize = encoder.Read(scratch.Buffer, nextReadOffset, nextReadSize);
                            }
                            else if (operationType == 1)
                            {
                                thisReadSize = encoder.Read(scratch.Buffer, nextReadOffset, nextReadSize, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                            }
                            else
                            {
                                thisReadSize = await encoder.ReadAsync(scratch.Buffer, nextReadOffset, nextReadSize, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                            }

                            if (thisReadSize > 0)
                            {
                                outputData.Write(scratch.Buffer, nextReadOffset, thisReadSize);
                            }
                        }

                        byte[] actualDecodedData = outputData.ToArray();
                        Assert.AreEqual(outputData.Position, encoder.Position);
                        if (!ArrayExtensions.ArrayEquals(expectedOutputData, 0, actualDecodedData, 0, actualDecodedData.Length))
                        {
                            Assert.Fail("Decoded data did not match");
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void TestBase64DecodingStreamCopyFrom()
        {
            IRandom rand = new FastRandom();
            byte[] expectedOutputData = new byte[65536];
            for (int c = 0; c < 100; c++)
            {
                int expectedDataLength = rand.NextInt(1, expectedOutputData.Length);
                rand.NextBytes(expectedOutputData, 0, expectedDataLength);
                string inputBase64 = Convert.ToBase64String(expectedOutputData, 0, expectedDataLength);
                byte[] inputBase64Ascii = Encoding.ASCII.GetBytes(inputBase64);

                using (MemoryStream inputDataSource = new MemoryStream(inputBase64Ascii, 0, inputBase64Ascii.Length))
                using (NonRealTimeStreamWrapper wrapper = new NonRealTimeStreamWrapper(inputDataSource, false))
                using (SimulatedUnreliableStream unreliableStream = new SimulatedUnreliableStream(wrapper))
                using (Base64AsciiDecodingStream encoder = new Base64AsciiDecodingStream(unreliableStream, StreamDirection.Read, false))
                using (MemoryStream outputData = new MemoryStream())
                {
                    encoder.CopyTo(outputData);
                    byte[] actualOutputData = outputData.ToArray();
                    Assert.AreEqual(outputData.Position, encoder.Position);
                    if (!ArrayExtensions.ArrayEquals(expectedOutputData, 0, actualOutputData, 0, expectedDataLength))
                    {
                        Assert.Fail("Decoded data did not match");
                    }
                }
            }
        }

        [TestMethod]
        public async Task TestBase64DecodingStreamCopyFromAsync()
        {
            IRandom rand = new FastRandom();
            byte[] expectedOutputData = new byte[65536];
            for (int c = 0; c < 100; c++)
            {
                int expectedDataLength = rand.NextInt(1, expectedOutputData.Length);
                rand.NextBytes(expectedOutputData, 0, expectedDataLength);
                string inputBase64 = Convert.ToBase64String(expectedOutputData, 0, expectedDataLength);
                byte[] inputBase64Ascii = Encoding.ASCII.GetBytes(inputBase64);

                using (MemoryStream inputDataSource = new MemoryStream(inputBase64Ascii, 0, inputBase64Ascii.Length))
                using (NonRealTimeStreamWrapper wrapper = new NonRealTimeStreamWrapper(inputDataSource, false))
                using (SimulatedUnreliableStream unreliableStream = new SimulatedUnreliableStream(wrapper))
                using (Base64AsciiDecodingStream encoder = new Base64AsciiDecodingStream(unreliableStream, StreamDirection.Read, false))
                using (MemoryStream outputData = new MemoryStream())
                {
                    await encoder.CopyToAsync(outputData);
                    byte[] actualOutputData = outputData.ToArray();
                    Assert.AreEqual(outputData.Position, encoder.Position);
                    if (!ArrayExtensions.ArrayEquals(expectedOutputData, 0, actualOutputData, 0, expectedDataLength))
                    {
                        Assert.Fail("Decoded data did not match");
                    }
                }
            }
        }

        [TestMethod]
        public async Task TestBase64DecodingStreamCopyToAsync()
        {
            IRandom rand = new FastRandom();
            byte[] expectedOutputData = new byte[65536];
            for (int c = 0; c < 100; c++)
            {
                int expectedDataLength = rand.NextInt(1, expectedOutputData.Length);
                rand.NextBytes(expectedOutputData, 0, expectedDataLength);
                string inputBase64 = Convert.ToBase64String(expectedOutputData, 0, expectedDataLength);
                byte[] inputBase64Ascii = Encoding.ASCII.GetBytes(inputBase64);

                using (MemoryStream outputData = new MemoryStream())
                using (NonRealTimeStreamWrapper wrapper = new NonRealTimeStreamWrapper(outputData, false))
                using (Base64AsciiDecodingStream encoder = new Base64AsciiDecodingStream(wrapper, StreamDirection.Write, false))
                using (MemoryStream inputDataSource = new MemoryStream(inputBase64Ascii, 0, inputBase64Ascii.Length))
                {
                    await inputDataSource.CopyToAsync(encoder).ConfigureAwait(false);
                    await encoder.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    byte[] actualOutputData = outputData.ToArray();
                    Assert.AreEqual(outputData.Position, encoder.Position);
                    if (!ArrayExtensions.ArrayEquals(expectedOutputData, 0, actualOutputData, 0, expectedDataLength))
                    {
                        Assert.Fail("Decoded data did not match");
                    }
                }
            }
        }

        [TestMethod]
        public void TestBase64DecodingStreamNullInnerStream()
        {
            try
            {
                using (Base64AsciiDecodingStream encoder = new Base64AsciiDecodingStream(null, StreamDirection.Write, false))
                {
                    Assert.Fail("Should have thrown a ArgumentNullException");
                }
            }
            catch (ArgumentNullException) { }
        }


        [TestMethod]
        public void TestBase64DecodingStreamUnknownStreamDirection()
        {
            try
            {
                using (MemoryStream innerStream = new MemoryStream(new byte[100], true))
                using (Base64AsciiDecodingStream encoder = new Base64AsciiDecodingStream(innerStream, StreamDirection.Unknown, false))
                {
                    Assert.Fail("Should have thrown a ArgumentException");
                }
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void TestBase64DecodingStreamNonWritableWriteStream()
        {
            try
            {
                using (MemoryStream innerStream = new MemoryStream(new byte[100], false))
                using (Base64AsciiDecodingStream encoder = new Base64AsciiDecodingStream(innerStream, StreamDirection.Write, false))
                {
                    Assert.Fail("Should have thrown a ArgumentException");
                }
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void TestBase64DecodingStreamNonReadableReadStream()
        {
            try
            {
                using (PipeStream pipe = new PipeStream())
                using (Base64AsciiDecodingStream encoder = new Base64AsciiDecodingStream(pipe.GetWriteStream(), StreamDirection.Read, false))
                {
                    Assert.Fail("Should have thrown a ArgumentException");
                }
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void TestBase64DecodingStreamExpectedPropertiesOnReadStream()
        {
            using (MemoryStream innerStream = new MemoryStream(new byte[100], false))
            using (Base64AsciiDecodingStream encoder = new Base64AsciiDecodingStream(innerStream, StreamDirection.Read, false))
            {
                Assert.IsTrue(encoder.CanRead);
                Assert.IsFalse(encoder.CanWrite);
                Assert.IsFalse(encoder.CanSeek);
                Assert.IsFalse(encoder.CanTimeout);
                Assert.AreEqual(0, encoder.Position);
            }
        }

        [TestMethod]
        public void TestBase64DecodingStreamExpectedPropertiesOnWriteStream()
        {
            using (PipeStream pipe = new PipeStream())
            using (Base64AsciiDecodingStream encoder = new Base64AsciiDecodingStream(pipe.GetWriteStream(), StreamDirection.Write, false))
            {
                Assert.IsFalse(encoder.CanRead);
                Assert.IsTrue(encoder.CanWrite);
                Assert.IsFalse(encoder.CanSeek);
                Assert.IsFalse(encoder.CanTimeout);
                Assert.AreEqual(0, encoder.Position);
            }
        }

        [TestMethod]
        public async Task TestBase64DecodingStreamHandlesWhitespace()
        {
            string inputBase64 = "  \r\n  \t ABCD AB\r\nCD AB\tCD ABCD\t  \r\n";
            byte[] inputAscii = Encoding.ASCII.GetBytes(inputBase64);
            using (MemoryStream decodedOutput = new MemoryStream())
            using (Base64AsciiDecodingStream decoder = new Base64AsciiDecodingStream(decodedOutput, StreamDirection.Write, false))
            {
                await decoder.WriteAsync(inputAscii, 0, inputAscii.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                await decoder.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                Assert.AreEqual(12, decodedOutput.Length);
            }
        }

        [TestMethod]
        public async Task TestBase64DecodingStreamInvalidInputLengthNotMultipleOf4()
        {
            string inputBase64 = "AAAAAAA";
            byte[] inputAscii = Encoding.ASCII.GetBytes(inputBase64);
            using (MemoryStream decodedOutput = new MemoryStream())
            using (Base64AsciiDecodingStream decoder = new Base64AsciiDecodingStream(decodedOutput, StreamDirection.Write, false))
            {
                try
                {
                    await decoder.WriteAsync(inputAscii, 0, inputAscii.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    await decoder.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.Fail("Expected a FormatException");
                }
                catch (FormatException e)
                {
                    Assert.IsTrue(e.Message.Contains("not a multiple of 4"));
                }
            }
        }

        [TestMethod]
        public async Task TestBase64DecodingStreamInvalidInputPaddingAtInvalidLocation()
        {
            string inputBase64 = "AAAA=";
            byte[] inputAscii = Encoding.ASCII.GetBytes(inputBase64);
            using (MemoryStream decodedOutput = new MemoryStream())
            using (Base64AsciiDecodingStream decoder = new Base64AsciiDecodingStream(decodedOutput, StreamDirection.Write, false))
            {
                try
                {
                    await decoder.WriteAsync(inputAscii, 0, inputAscii.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    await decoder.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.Fail("Expected a FormatException");
                }
                catch (FormatException e)
                {
                    Assert.IsTrue(e.Message.Contains("Padding char at invalid position"));
                }
            }

            inputBase64 = "AAAAA=";
            inputAscii = Encoding.ASCII.GetBytes(inputBase64);
            using (MemoryStream decodedOutput = new MemoryStream())
            using (Base64AsciiDecodingStream decoder = new Base64AsciiDecodingStream(decodedOutput, StreamDirection.Write, false))
            {
                try
                {
                    await decoder.WriteAsync(inputAscii, 0, inputAscii.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    await decoder.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.Fail("Expected a FormatException");
                }
                catch (FormatException e)
                {
                    Assert.IsTrue(e.Message.Contains("Padding char at invalid position"));
                }
            }
        }

        [TestMethod]
        public async Task TestBase64DecodingStreamInvalidInputPaddingTooLong()
        {
            string inputBase64 = "AAAAAA======";
            byte[] inputAscii = Encoding.ASCII.GetBytes(inputBase64);
            using (MemoryStream decodedOutput = new MemoryStream())
            using (Base64AsciiDecodingStream decoder = new Base64AsciiDecodingStream(decodedOutput, StreamDirection.Write, false))
            {
                try
                {
                    await decoder.WriteAsync(inputAscii, 0, inputAscii.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    await decoder.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.Fail("Expected a FormatException");
                }
                catch (FormatException e)
                {
                    Assert.IsTrue(e.Message.Contains("Too many padding chars"));
                }
            }
        }

        [TestMethod]
        public async Task TestBase64DecodingStreamInvalidBadInputChar()
        {
            string inputBase64 = "AAAA@AAA";
            byte[] inputAscii = Encoding.ASCII.GetBytes(inputBase64);
            using (MemoryStream decodedOutput = new MemoryStream())
            using (Base64AsciiDecodingStream decoder = new Base64AsciiDecodingStream(decodedOutput, StreamDirection.Write, false))
            {
                try
                {
                    await decoder.WriteAsync(inputAscii, 0, inputAscii.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    await decoder.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.Fail("Expected a FormatException");
                }
                catch (FormatException e)
                {
                    Assert.IsTrue(e.Message.Contains("Invalid character found"));
                }
            }

            inputBase64 = "AAAA!AAA";
            inputAscii = Encoding.ASCII.GetBytes(inputBase64);
            using (MemoryStream decodedOutput = new MemoryStream())
            using (Base64AsciiDecodingStream decoder = new Base64AsciiDecodingStream(decodedOutput, StreamDirection.Write, false))
            {
                try
                {
                    await decoder.WriteAsync(inputAscii, 0, inputAscii.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    await decoder.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.Fail("Expected a FormatException");
                }
                catch (FormatException e)
                {
                    Assert.IsTrue(e.Message.Contains("Invalid character found"));
                }
            }
        }

        [TestMethod]
        public async Task TestBase64DecodingStreamInvalidExtraDataAfterPadding()
        {
            string inputBase64 = "AAAAAA=A";
            byte[] inputAscii = Encoding.ASCII.GetBytes(inputBase64);
            using (MemoryStream decodedOutput = new MemoryStream())
            using (Base64AsciiDecodingStream decoder = new Base64AsciiDecodingStream(decodedOutput, StreamDirection.Write, false))
            {
                try
                {
                    await decoder.WriteAsync(inputAscii, 0, inputAscii.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    await decoder.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.Fail("Expected a FormatException");
                }
                catch (FormatException e)
                {
                    Assert.IsTrue(e.Message.Contains("char data found after padding"));
                }
            }
        }

        [TestMethod]
        public void TestBase64DecodingStreamInvalidCopyToOnWriteStream()
        {
            try
            {
                using (MemoryStream innerStream = new MemoryStream())
                using (Base64AsciiDecodingStream decoder = new Base64AsciiDecodingStream(innerStream, StreamDirection.Write, false))
                using (MemoryStream targetStream = new MemoryStream())
                {
                    decoder.CopyTo(targetStream);
                    Assert.Fail("Should have thrown a NotSupportedException");
                }
            }
            catch (NotSupportedException) { }
        }

        [TestMethod]
        public async Task TestBase64DecodingStreamInvalidFlushOnReadStream()
        {
            try
            {
                using (MemoryStream innerStream = new MemoryStream())
                using (Base64AsciiDecodingStream decoder = new Base64AsciiDecodingStream(innerStream, StreamDirection.Read, false))
                {
                    decoder.Flush();
                    Assert.Fail("Should have thrown a InvalidOperationException");
                }
            }
            catch (InvalidOperationException) { }

            try
            {
                using (MemoryStream innerStream = new MemoryStream())
                using (Base64AsciiDecodingStream decoder = new Base64AsciiDecodingStream(innerStream, StreamDirection.Read, false))
                {
                    await decoder.FlushAsync().ConfigureAwait(false);
                    Assert.Fail("Should have thrown a InvalidOperationException");
                }
            }
            catch (InvalidOperationException) { }
        }

        [TestMethod]
        public async Task TestBase64DecodingStreamInvalidReadFromWriteStream()
        {
            byte[] scratch = new byte[100];

            try
            {
                using (MemoryStream innerStream = new MemoryStream())
                using (Base64AsciiDecodingStream decoder = new Base64AsciiDecodingStream(innerStream, StreamDirection.Write, false))
                {
                    decoder.Read(scratch, 0, 100, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.Fail("Should have thrown a InvalidOperationException");
                }
            }
            catch (InvalidOperationException) { }

            try
            {
                using (MemoryStream innerStream = new MemoryStream())
                using (Base64AsciiDecodingStream decoder = new Base64AsciiDecodingStream(innerStream, StreamDirection.Write, false))
                {
                    await decoder.ReadAsync(scratch, 0, 100, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.Fail("Should have thrown a InvalidOperationException");
                }
            }
            catch (InvalidOperationException) { }
        }

        [TestMethod]
        public async Task TestBase64DecodingStreamInvalidWriteToReadStream()
        {
            byte[] scratch = new byte[100];

            try
            {
                using (MemoryStream innerStream = new MemoryStream())
                using (Base64AsciiDecodingStream decoder = new Base64AsciiDecodingStream(innerStream, StreamDirection.Read, false))
                {
                    decoder.Write(scratch, 0, 100, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.Fail("Should have thrown a InvalidOperationException");
                }
            }
            catch (InvalidOperationException) { }

            try
            {
                using (MemoryStream innerStream = new MemoryStream())
                using (Base64AsciiDecodingStream decoder = new Base64AsciiDecodingStream(innerStream, StreamDirection.Read, false))
                {
                    await decoder.WriteAsync(scratch, 0, 100, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.Fail("Should have thrown a InvalidOperationException");
                }
            }
            catch (InvalidOperationException) { }

            try
            {
                using (MemoryStream innerStream = new MemoryStream())
                using (Base64AsciiDecodingStream decoder = new Base64AsciiDecodingStream(innerStream, StreamDirection.Read, false))
                {
                    await decoder.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.Fail("Should have thrown a InvalidOperationException");
                }
            }
            catch (InvalidOperationException) { }
        }

        [TestMethod]
        public async Task TestBase64DecodingStreamInvalidOperationsAfterFinish()
        {
            byte[] scratch = new byte[100];

            using (MemoryStream innerStream = new MemoryStream())
            using (Base64AsciiDecodingStream decoder = new Base64AsciiDecodingStream(innerStream, StreamDirection.Write, false))
            {
                await decoder.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);

                try
                {
                    await decoder.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.Fail("Should have thrown a InvalidOperationException");
                }
                catch (InvalidOperationException) { }

                try
                {
                    await decoder.WriteAsync(scratch, 0, 100, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.Fail("Should have thrown a InvalidOperationException");
                }
                catch (InvalidOperationException) { }

                try
                {
                    decoder.Write(scratch, 0, 100, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.Fail("Should have thrown a InvalidOperationException");
                }
                catch (InvalidOperationException) { }
            }
        }

        [TestMethod]
        public async Task TestBase64DecodingStreamOwnsInnerStream()
        {
            byte[] scratch = Encoding.ASCII.GetBytes("ABCDABCD");
            using (Base64AsciiDecodingStream decoder = new Base64AsciiDecodingStream(new MemoryStream(), StreamDirection.Write, true))
            {
                await decoder.WriteAsync(scratch, 0, scratch.Length, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                await decoder.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
            }
        }

        [TestMethod]
        public async Task TestBase64DecodingStreamBehavesAfterDisposal()
        {
            using (Base64AsciiDecodingStream decoder = new Base64AsciiDecodingStream(new MemoryStream(), StreamDirection.Write, true))
            {
                decoder.Dispose();

                try
                {
                    decoder.Flush();
                    Assert.Fail("Should have thrown a ObjectDisposedException");
                }
                catch (ObjectDisposedException) { }

                try
                {
                    await decoder.FlushAsync().ConfigureAwait(false);
                    Assert.Fail("Should have thrown a ObjectDisposedException");
                }
                catch (ObjectDisposedException) { }

                try
                {
                    decoder.Read(new byte[10], 0, 10, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.Fail("Should have thrown a ObjectDisposedException");
                }
                catch (ObjectDisposedException) { }

                try
                {
                    await decoder.ReadAsync(new byte[10], 0, 10, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.Fail("Should have thrown a ObjectDisposedException");
                }
                catch (ObjectDisposedException) { }

                try
                {
                    decoder.Write(new byte[10], 0, 10, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.Fail("Should have thrown a ObjectDisposedException");
                }
                catch (ObjectDisposedException) { }

                try
                {
                    await decoder.WriteAsync(new byte[10], 0, 10, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.Fail("Should have thrown a ObjectDisposedException");
                }
                catch (ObjectDisposedException) { }

                try
                {
                    await decoder.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.Fail("Should have thrown a ObjectDisposedException");
                }
                catch (ObjectDisposedException) { }

                decoder.Dispose();
            }
        }

        [TestMethod]
        public void TestBase64DecodingStreamNotSupportedOperations()
        {
            using (MemoryStream innerStream = new MemoryStream(new byte[100], false))
            using (Base64AsciiDecodingStream decoder = new Base64AsciiDecodingStream(innerStream, StreamDirection.Read, false))
            {
                try
                {
                    decoder.Length.GetHashCode();
                    Assert.Fail("Should have thrown a NotSupportedException");
                }
                catch (NotSupportedException) { }

                try
                {
                    decoder.Position = 0;
                    Assert.Fail("Should have thrown a InvalidOperationException");
                }
                catch (InvalidOperationException) { }

                try
                {
                    decoder.Seek(0, SeekOrigin.Begin);
                    Assert.Fail("Should have thrown a InvalidOperationException");
                }
                catch (InvalidOperationException) { }

                try
                {
                    decoder.SetLength(0);
                    Assert.Fail("Should have thrown a InvalidOperationException");
                }
                catch (InvalidOperationException) { }
            }
        }

        #endregion

        #region Base64 Binary Helpers

        [TestMethod]
        public void TestUrlSafeBase64()
        {
            IRandom rand = new FastRandom();
            byte[] inputData = new byte[65536];
            for (int c = 0; c < 100; c++)
            {
                int expectedDataLength = rand.NextInt(1, inputData.Length);
                rand.NextBytes(inputData, 0, expectedDataLength);
                string base64 = BinaryHelpers.EncodeUrlSafeBase64(inputData, 0, expectedDataLength);
                using (PooledBuffer<byte> decoded = BinaryHelpers.DecodeUrlSafeBase64(base64))
                {
                    Assert.IsTrue(ArrayExtensions.ArrayEquals(inputData, 0, decoded.Buffer, 0, expectedDataLength));
                }
            }
        }

        #endregion
    }
}
