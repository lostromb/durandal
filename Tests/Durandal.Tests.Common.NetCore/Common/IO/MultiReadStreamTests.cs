using Durandal.Common.IO;
using Durandal.Common.Test;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.IO
{
    [TestClass]
    public class MultiReadStreamTests
    {
        [TestMethod]
        public void TestMultiReadStreamSingleCursor()
        {
            byte[] data = new byte[100000];
            byte[] readBuf = new byte[100000];
            for (int c = 0; c < data.Length; c++)
            {
                data[c] = (byte)(c % 256);
            }

            using (MemoryStream baseStream = new MemoryStream(data, false))
            using (SimulatedUnreliableStream unreliableStream = new SimulatedUnreliableStream(new NonRealTimeStreamWrapper(baseStream, ownsStream: false)))
            {
                MultiReadStream multiRead = new MultiReadStream(unreliableStream, 800, 100);
                using (Stream cursor1 = multiRead.CreateCursor(0))
                {
                    int readFromCursor = 0;
                    while (readFromCursor < data.Length)
                    {
                        int thisReadSize = cursor1.Read(readBuf, readFromCursor, 777);
                        Assert.AreNotEqual(0, thisReadSize);
                        readFromCursor += thisReadSize;
                    }

                    for (int c = 0; c < data.Length; c++)
                    {
                        Assert.AreEqual((byte)(c % 256), readBuf[c]);
                    }
                }
            }
        }

        [TestMethod]
        public async Task TestMultiReadStreamSingleCursorAsync()
        {
            byte[] data = new byte[100000];
            byte[] readBuf = new byte[100000];
            for (int c = 0; c < data.Length; c++)
            {
                data[c] = (byte)(c % 256);
            }

            using (MemoryStream baseStream = new MemoryStream(data, false))
            using (SimulatedUnreliableStream unreliableStream = new SimulatedUnreliableStream(new NonRealTimeStreamWrapper(baseStream, ownsStream: false)))
            {
                MultiReadStream multiRead = new MultiReadStream(unreliableStream, 800, 100);
                using (Stream cursor1 = multiRead.CreateCursor(0))
                {
                    int readFromCursor = 0;
                    while (readFromCursor < data.Length)
                    {
                        int thisReadSize = await cursor1.ReadAsync(readBuf, readFromCursor, 777).ConfigureAwait(false);
                        Assert.AreNotEqual(0, thisReadSize);
                        readFromCursor += thisReadSize;
                    }

                    for (int c = 0; c < data.Length; c++)
                    {
                        Assert.AreEqual((byte)(c % 256), readBuf[c]);
                    }
                }
            }
        }

        [TestMethod]
        public async Task TestMultiReadStreamThroughPipe()
        {
            byte[] data = new byte[100000];
            byte[] readBuf = new byte[100000];
            for (int c = 0; c < data.Length; c++)
            {
                data[c] = (byte)(c % 256);
            }

            using (PipeStream pipe = new PipeStream())
            using (var readStream = pipe.GetReadStream())
            {
                using (var writeStream = pipe.GetWriteStream())
                {
                    writeStream.Write(data, 0, data.Length);
                }

                MultiReadStream multiRead = new MultiReadStream(readStream, 800, 100);
                using (Stream cursor = multiRead.CreateCursor(0))
                {
                    int readFromCursor = 0;
                    while (readFromCursor < data.Length)
                    {
                        int thisReadSize = await cursor.ReadAsync(readBuf, readFromCursor, 777).ConfigureAwait(false);
                        Assert.AreNotEqual(0, thisReadSize);
                        readFromCursor += thisReadSize;
                    }

                    for (int c = 0; c < data.Length; c++)
                    {
                        Assert.AreEqual((byte)(c % 256), readBuf[c]);
                    }
                }
            }
        }

        [TestMethod]
        public void TestMultiReadStreamSingleCursorInMiddleOfStream()
        {
            byte[] data = new byte[100000];
            byte[] readBuf = new byte[100000];
            for (int c = 0; c < data.Length; c++)
            {
                data[c] = (byte)(c % 256);
            }

            int skipSize = 1000;
            using (MemoryStream baseStream = new MemoryStream(data, false))
            using (SimulatedUnreliableStream unreliableStream = new SimulatedUnreliableStream(new NonRealTimeStreamWrapper(baseStream, ownsStream: false)))
            {
                MultiReadStream multiRead = new MultiReadStream(unreliableStream, 800, 100);
                using (Stream cursor1 = multiRead.CreateCursor(skipSize))
                {
                    int readFromCursor = 0;
                    while (readFromCursor < data.Length - skipSize)
                    {
                        int thisReadSize = cursor1.Read(readBuf, readFromCursor, 777);
                        Assert.AreNotEqual(0, thisReadSize);
                        readFromCursor += thisReadSize;
                    }

                    for (int c = 0; c < data.Length - skipSize; c++)
                    {
                        Assert.AreEqual((byte)((c + skipSize) % 256), readBuf[c]);
                    }
                }
            }
        }

        [TestMethod]
        public void TestMultiReadStreamTwoCursorsBufferEntireInput()
        {
            byte[] data = new byte[100000];
            byte[] readBuf = new byte[100000];
            for (int c = 0; c < data.Length; c++)
            {
                data[c] = (byte)(c % 256);
            }

            using (MemoryStream baseStream = new MemoryStream(data, false))
            using (SimulatedUnreliableStream unreliableStream = new SimulatedUnreliableStream(new NonRealTimeStreamWrapper(baseStream, ownsStream: false)))
            {
                MultiReadStream multiRead = new MultiReadStream(unreliableStream, 800, 100);
                using (Stream cursor1 = multiRead.CreateCursor(0))
                using (Stream cursor2 = multiRead.CreateCursor(0))
                {
                    int readFromCursor1 = 0;
                    while (readFromCursor1 < data.Length)
                    {
                        int thisReadSize = cursor1.Read(readBuf, readFromCursor1, 777);
                        Assert.AreNotEqual(0, thisReadSize);
                        readFromCursor1 += thisReadSize;
                    }

                    for (int c = 0; c < data.Length; c++)
                    {
                        Assert.AreEqual((byte)(c % 256), readBuf[c]);
                    }

                    Assert.AreEqual(data.Length, multiRead.BufferedDataLength);

                    int readFromCursor2 = 0;
                    while (readFromCursor2 < data.Length)
                    {
                        int thisReadSize = cursor2.Read(readBuf, readFromCursor2, 777);
                        Assert.AreNotEqual(0, thisReadSize);
                        readFromCursor2 += thisReadSize;
                    }

                    for (int c = 0; c < data.Length; c++)
                    {
                        Assert.AreEqual((byte)(c % 256), readBuf[c]);
                    }
                }
            }
        }

        [TestMethod]
        public void TestMultiReadStreamTwoCursorsSideBySide()
        {
            int halfData = 50000;
            int cursor2BehindCursor1 = 200;
            byte[] data = new byte[halfData * 2];
            byte[] cursor1ReadBuf = new byte[halfData * 2];
            byte[] cursor2ReadBuf = new byte[halfData + cursor2BehindCursor1];
            for (int c = 0; c < data.Length; c++)
            {
                data[c] = (byte)(c % 256);
            }

            using (MemoryStream baseStream = new MemoryStream(data, false))
            using (SimulatedUnreliableStream unreliableStream = new SimulatedUnreliableStream(new NonRealTimeStreamWrapper(baseStream, ownsStream: false)))
            {
                MultiReadStream multiRead = new MultiReadStream(unreliableStream, 800, 100);
                using (Stream cursor1 = multiRead.CreateCursor(0))
                {
                    // Read half of the file on cursor 1
                    int readFromCursor1 = 0;
                    int readFromCursor2 = 0;

                    while (readFromCursor1 < halfData)
                    {
                        int thisReadSize = cursor1.Read(cursor1ReadBuf, readFromCursor1, Math.Min(halfData - readFromCursor1, 777));
                        Assert.AreNotEqual(0, thisReadSize);
                        readFromCursor1 += thisReadSize;
                    }

                    // Now create a new cursor 200 bytes before the first one, and have them read side-by-side until the end
                    using (Stream cursor2 = multiRead.CreateCursor(cursor1.Position - cursor2BehindCursor1))
                    {
                        while (readFromCursor1 < cursor1ReadBuf.Length &&
                            readFromCursor2 < cursor2ReadBuf.Length)
                        {
                            int maxToReadFromCursor1 = Math.Min(cursor1ReadBuf.Length - readFromCursor1, 333);
                            if (maxToReadFromCursor1 > 0)
                            {
                                int thisReadSize = cursor1.Read(cursor1ReadBuf, readFromCursor1, maxToReadFromCursor1);
                                Assert.AreNotEqual(0, thisReadSize);
                                readFromCursor1 += thisReadSize;
                            }

                            int maxToReadFromCursor2 = Math.Min(cursor2ReadBuf.Length - readFromCursor2, 333);
                            if (maxToReadFromCursor2 > 0)
                            {
                                int thisReadSize = cursor2.Read(cursor2ReadBuf, readFromCursor2, maxToReadFromCursor2);
                                Assert.AreNotEqual(0, thisReadSize);
                                readFromCursor2 += thisReadSize;
                            }

                            Assert.IsTrue(multiRead.BufferedDataLength < 1500);
                        }

                        for (int c = 0; c < cursor1ReadBuf.Length; c++)
                        {
                            Assert.AreEqual((byte)(c % 256), cursor1ReadBuf[c]);
                        }

                        for (int c = 0; c < cursor2ReadBuf.Length; c++)
                        {
                            Assert.AreEqual((byte)((c + halfData - cursor2BehindCursor1) % 256), cursor2ReadBuf[c]);
                        }
                    }
                }
            }
        }

        [TestMethod]
        public async Task TestMultiReadStreamTwoCursorsSideBySideAsync()
        {
            int halfData = 50000;
            int cursor2BehindCursor1 = 200;
            byte[] data = new byte[halfData * 2];
            byte[] cursor1ReadBuf = new byte[halfData * 2];
            byte[] cursor2ReadBuf = new byte[halfData + cursor2BehindCursor1];
            for (int c = 0; c < data.Length; c++)
            {
                data[c] = (byte)(c % 256);
            }

            using (MemoryStream baseStream = new MemoryStream(data, false))
            using (SimulatedUnreliableStream unreliableStream = new SimulatedUnreliableStream(new NonRealTimeStreamWrapper(baseStream, ownsStream: false)))
            {
                MultiReadStream multiRead = new MultiReadStream(unreliableStream, 800, 100);
                using (Stream cursor1 = multiRead.CreateCursor(0))
                {
                    // Read half of the file on cursor 1
                    int readFromCursor1 = 0;
                    int readFromCursor2 = 0;

                    while (readFromCursor1 < halfData)
                    {
                        int thisReadSize = await cursor1.ReadAsync(cursor1ReadBuf, readFromCursor1, Math.Min(halfData - readFromCursor1, 777)).ConfigureAwait(false);
                        Assert.AreNotEqual(0, thisReadSize);
                        readFromCursor1 += thisReadSize;
                    }

                    // Now create a new cursor 200 bytes before the first one, and have them read side-by-side until the end
                    using (Stream cursor2 = multiRead.CreateCursor(cursor1.Position - cursor2BehindCursor1))
                    {
                        while (readFromCursor1 < cursor1ReadBuf.Length &&
                            readFromCursor2 < cursor2ReadBuf.Length)
                        {
                            int maxToReadFromCursor1 = Math.Min(cursor1ReadBuf.Length - readFromCursor1, 333);
                            if (maxToReadFromCursor1 > 0)
                            {
                                int thisReadSize = await cursor1.ReadAsync(cursor1ReadBuf, readFromCursor1, maxToReadFromCursor1).ConfigureAwait(false);
                                Assert.AreNotEqual(0, thisReadSize);
                                readFromCursor1 += thisReadSize;
                            }

                            int maxToReadFromCursor2 = Math.Min(cursor2ReadBuf.Length - readFromCursor2, 333);
                            if (maxToReadFromCursor2 > 0)
                            {
                                int thisReadSize = await cursor2.ReadAsync(cursor2ReadBuf, readFromCursor2, maxToReadFromCursor2).ConfigureAwait(false);
                                Assert.AreNotEqual(0, thisReadSize);
                                readFromCursor2 += thisReadSize;
                            }

                            Assert.IsTrue(multiRead.BufferedDataLength < 1500);
                        }

                        for (int c = 0; c < cursor1ReadBuf.Length; c++)
                        {
                            Assert.AreEqual((byte)(c % 256), cursor1ReadBuf[c]);
                        }

                        for (int c = 0; c < cursor2ReadBuf.Length; c++)
                        {
                            Assert.AreEqual((byte)((c + halfData - cursor2BehindCursor1) % 256), cursor2ReadBuf[c]);
                        }
                    }
                }
            }
        }
    }
}
