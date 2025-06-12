using Durandal.Common.Compression;
using Durandal.Common.IO;
using Durandal.Common.MathExt;
using Durandal.Common.Net;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Test;
using Durandal.Common.Logger;
using Durandal.Common.Collections;
using System.Net.Sockets;
using System.IO;
using System.Runtime.CompilerServices;

namespace Durandal.Tests.Common.Net
{
    [TestClass]
    public class SocketTests
    {
        [TestMethod]
        public async Task TestDirectSocketBasic()
        {
            const int NUM_LOOPS = 100;
            DirectSocketPair socketPair = DirectSocket.CreateSocketPair();

            using (socketPair.ClientSocket)
            using (socketPair.ServerSocket)
            using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                CancellationToken cancelToken = cts.Token;
                IRandom rand = new FastRandom();

                Task serverTask = Task.Run(async () =>
                {
                    byte[] buf = new byte[10000];
                    ISocket serverSocket = socketPair.ServerSocket;

                    for (int loop = 0; loop < NUM_LOOPS; loop++)
                    {
                        // Read message size
                        int socketReadSize = await serverSocket.ReadAsync(buf, 4, 4, cancelToken, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        Assert.AreEqual(4, socketReadSize);
                        int messageSize = BinaryHelpers.ByteArrayToInt32LittleEndian(buf, 4);

                        // Read message
                        socketReadSize = await serverSocket.ReadAsync(buf, 0, messageSize, cancelToken, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                        Assert.AreEqual(messageSize, socketReadSize);

                        // And echo it back
                        await serverSocket.WriteAsync(buf, 0, messageSize, cancelToken, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    }
                });

                Task clientTask = Task.Run(async () =>
                {
                    byte[] outMessage = new byte[10000];
                    byte[] inMessage = new byte[10000];
                    ISocket clientSocket = socketPair.ClientSocket;

                    for (int loop = 0; loop < NUM_LOOPS; loop++)
                    {
                        // Write message size
                        int messageSize = rand.NextInt(1, 10000);
                        BinaryHelpers.Int32ToByteArrayLittleEndian(messageSize, outMessage, 4);
                        await clientSocket.WriteAsync(outMessage, 4, 4, cancelToken, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);

                        // Write message
                        rand.NextBytes(outMessage, 0, messageSize);
                        await clientSocket.WriteAsync(outMessage, 0, messageSize, cancelToken, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);

                        // Read back echoed message
                        int socketReadSize = await clientSocket.ReadAsync(inMessage, 0, messageSize, cancelToken, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);

                        // Assert that the same message came back
                        Assert.AreEqual(messageSize, socketReadSize);
                        Assert.IsTrue(ArrayExtensions.ArrayEquals(outMessage, 0, inMessage, 0, messageSize));
                    }
                });

                await serverTask.ConfigureAwait(false);
                await clientTask.ConfigureAwait(false);
            }
        }

        [TestMethod]
        public async Task TestDirectSocketCanReadDataAfterWriteEndpointCloses()
        {
            DirectSocketPair socketPair = DirectSocket.CreateSocketPair();

            using (DirectSocket clientSocket = socketPair.ClientSocket)
            using (DirectSocket serverSocket = socketPair.ServerSocket)
            using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                byte[] data = new byte[10] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                CancellationToken testCancel = cts.Token;
                await serverSocket.WriteAsync(data, 0, 10, testCancel, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                Assert.AreEqual(5, await clientSocket.ReadAsync(data, 0, 5, testCancel, DefaultRealTimeProvider.Singleton).ConfigureAwait(false));
                await serverSocket.Disconnect(testCancel, DefaultRealTimeProvider.Singleton, NetworkDuplex.Write, allowLinger: false);
                Assert.AreEqual(5, await clientSocket.ReadAsync(data, 5, 5, testCancel, DefaultRealTimeProvider.Singleton).ConfigureAwait(false));
                Assert.AreEqual(0, await clientSocket.ReadAnyAsync(data, 0, 1, testCancel, DefaultRealTimeProvider.Singleton).ConfigureAwait(false));
                await clientSocket.Disconnect(testCancel, DefaultRealTimeProvider.Singleton, NetworkDuplex.Read, allowLinger: false);
            }
        }

        [TestMethod]
        public async Task TestDirectSocketUnread()
        {
            const int NUM_LOOPS = 100;
            DirectSocketPair socketPair = DirectSocket.CreateSocketPair();
            Barrier lockStep = new Barrier(2);

            using (socketPair.ClientSocket)
            using (socketPair.ServerSocket)
            using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                CancellationToken cancelToken = cts.Token;
                IRandom rand = new FastRandom();

                Task serverTask = Task.Run(async () =>
                {
                    byte[] buf = new byte[1024];
                    ISocket serverSocket = socketPair.ServerSocket;

                    lockStep.SignalAndWait(cancelToken); // Let the client thread advance once so we're always one message behind
                    for (int loop = 0; loop < NUM_LOOPS; loop++)
                    {
                        // Read data into buffer until we hit a zero
                        int calculatedMessageSize = 0;
                        bool hitDelimiter = false;
                        while (!hitDelimiter)
                        {
                            int socketReadSize = await serverSocket.ReadAnyAsync(buf, 0, buf.Length, cancelToken, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);

                            if (socketReadSize > 0)
                            {
                                // See if there's a zero
                                for (int c = 0; c < socketReadSize && !hitDelimiter; c++)
                                {
                                    if (buf[c] == 0)
                                    {
                                        // Unread everything past the delimiter
                                        if (c + 1 < socketReadSize)
                                        {
                                            serverSocket.Unread(buf, c + 1, socketReadSize - c - 1);
                                        }

                                        // Now read the actual message length
                                        socketReadSize = await serverSocket.ReadAsync(buf, 0, 4, cancelToken, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                                        Assert.AreEqual(4, socketReadSize);
                                        int actualMessageSize = BinaryHelpers.ByteArrayToInt32LittleEndian(buf, 0);
                                        Assert.AreEqual(actualMessageSize, calculatedMessageSize);
                                        hitDelimiter = true;
                                    }
                                    else
                                    {
                                        calculatedMessageSize++;
                                    }
                                }
                            }
                        }

                        lockStep.SignalAndWait(cancelToken);
                    }
                });

                Task clientTask = Task.Run(async () =>
                {
                    byte[] scratch = new byte[5];
                    scratch[0] = 0;
                    byte[] outMessage = new byte[10000];
                    for (int c = 0; c < outMessage.Length; c++)
                    {
                        outMessage[c] = 0xFF;
                    }

                    ISocket clientSocket = socketPair.ClientSocket;

                    for (int loop = 0; loop < NUM_LOOPS; loop++)
                    {
                        // Determine a secret message size
                        int messageSize = rand.NextInt(1, 10000);

                        // Write [message size] bytes of 0xFF
                        await clientSocket.WriteAsync(outMessage, 0, messageSize, cancelToken, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);

                        // Write a zero as the delimiter
                        await clientSocket.WriteAsync(scratch, 0, 1, cancelToken, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);

                        // Write the actual message length we used
                        BinaryHelpers.Int32ToByteArrayLittleEndian(messageSize, scratch, 1);
                        await clientSocket.WriteAsync(scratch, 1, 4, cancelToken, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);

                        lockStep.SignalAndWait(cancelToken);
                    }

                    lockStep.SignalAndWait(cancelToken);
                });

                await serverTask.ConfigureAwait(false);
                await clientTask.ConfigureAwait(false);
            }
        }

        [TestMethod]
        public async Task TestSocketUnread()
        {
            byte[] input = new byte[100];
            for (int c = 0; c < 100; c++)
            {
                input[c] = (byte)c;
            }

            byte[] output = new byte[100];
            int bytesReadFromOutput = 0;

            DirectSocketPair socketPair = DirectSocket.CreateSocketPair();
            using (DirectSocket clientSocket = socketPair.ClientSocket)
            using (DirectSocket serverSocket = socketPair.ServerSocket)
            {
                byte[] padding = new byte[BufferPool<byte>.DEFAULT_BUFFER_SIZE - 50];
                new FastRandom().NextBytes(padding);
                serverSocket.Unread(padding, 0, padding.Length);
                serverSocket.Unread(input, 0, 100);
                bytesReadFromOutput +=  await serverSocket.ReadAsync(output, bytesReadFromOutput, 50, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                Assert.AreEqual(50, bytesReadFromOutput);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(input, 0, output, 0, bytesReadFromOutput));
                serverSocket.Unread(output, 20, 30);
                bytesReadFromOutput -= 30;
                serverSocket.Unread(output, 10, 10);
                bytesReadFromOutput -= 10;
                bytesReadFromOutput += await serverSocket.ReadAsync(output, bytesReadFromOutput, 20, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                Assert.AreEqual(30, bytesReadFromOutput);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(input, 0, output, 0, bytesReadFromOutput));
                bytesReadFromOutput += await serverSocket.ReadAsync(output, bytesReadFromOutput, 20, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                Assert.AreEqual(50, bytesReadFromOutput);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(input, 0, output, 0, bytesReadFromOutput));
                bytesReadFromOutput += await serverSocket.ReadAsync(output, bytesReadFromOutput, 50, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                Assert.AreEqual(100, bytesReadFromOutput);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(input, 0, output, 0, bytesReadFromOutput));
                serverSocket.Unread(output, 90, 10);
                bytesReadFromOutput -= 10;
                serverSocket.Unread(output, 80, 10);
                bytesReadFromOutput -= 10;
                serverSocket.Unread(output, 70, 10);
                bytesReadFromOutput -= 10;
                bytesReadFromOutput += await serverSocket.ReadAsync(output, bytesReadFromOutput, 15, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                Assert.AreEqual(85, bytesReadFromOutput);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(input, 0, output, 0, bytesReadFromOutput));
                bytesReadFromOutput += await serverSocket.ReadAsync(output, bytesReadFromOutput, 15, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                Assert.AreEqual(100, bytesReadFromOutput);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(input, 0, output, 0, bytesReadFromOutput));
                await clientSocket.Disconnect(CancellationToken.None, DefaultRealTimeProvider.Singleton, NetworkDuplex.ReadWrite);
                await serverSocket.Disconnect(CancellationToken.None, DefaultRealTimeProvider.Singleton, NetworkDuplex.ReadWrite);
            }
        }

        [TestMethod]
        public async Task TestDirectSocketWithSimulatedDelay()
        {
            ILogger logger = new ConsoleLogger();
            LockStepRealTimeProvider lockStepTime = new LockStepRealTimeProvider(logger);
            DirectSocketPair socketPair = DirectSocket.CreateSocketPair(TimeSpan.FromMilliseconds(1000)); // Simulated network latency of 1000ms

            using (socketPair.ClientSocket)
            using (socketPair.ServerSocket)
            using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                CancellationToken cancelToken = cts.Token;
                IRandom rand = new FastRandom();

                bool serverReadMessage = false;
                bool clientReadMessage = false;

                IRealTimeProvider serverThreadTime = lockStepTime.Fork("Server");
                IRealTimeProvider clientThreadTime = lockStepTime.Fork("Client");
                Task serverTask = Task.Run(async () =>
                {
                    try
                    {
                        byte[] buf = new byte[10000];
                        ISocket serverSocket = socketPair.ServerSocket;

                        int socketReadSize = await serverSocket.ReadAsync(buf, 0, 10000, cancelToken, serverThreadTime).ConfigureAwait(false);
                        Assert.AreEqual(10000, socketReadSize);

                        Volatile.Write(ref serverReadMessage, true);

                        // And echo it back in 10 parts separated by 50ms each, so it takes 500ms to send overall
                        for (int c = 0; c < 10; c++)
                        {
                            await serverSocket.WriteAsync(buf, c * 1000, 1000, cancelToken, serverThreadTime).ConfigureAwait(false);
                            await serverThreadTime.WaitAsync(TimeSpan.FromMilliseconds(50), cancelToken).ConfigureAwait(false);
                        }
                    }
                    finally
                    {
                        serverThreadTime.Merge();
                    }
                });

                Task clientTask = Task.Run(async () =>
                {
                    try
                    {
                        byte[] outMessage = new byte[10000];
                        byte[] inMessage = new byte[10000];
                        ISocket clientSocket = socketPair.ClientSocket;
                        FastRandom.Shared.NextBytes(outMessage);

                        // Write message in 10 parts over 500ms
                        for (int c = 0; c < 10; c++)
                        {
                            await clientSocket.WriteAsync(outMessage, c * 1000, 1000, cancelToken, clientThreadTime).ConfigureAwait(false);
                            await clientThreadTime.WaitAsync(TimeSpan.FromMilliseconds(50), cancelToken).ConfigureAwait(false);
                        }

                        // Read back echoed message
                        int socketReadSize = await clientSocket.ReadAsync(inMessage, 0, 10000, cancelToken, clientThreadTime).ConfigureAwait(false);

                        Volatile.Write(ref clientReadMessage, true);

                        // Assert that the same message came back
                        Assert.AreEqual(10000, socketReadSize);
                        Assert.IsTrue(ArrayExtensions.ArrayEquals(outMessage, 0, inMessage, 0, 10000));
                    }
                    finally
                    {
                        clientThreadTime.Merge();
                    }
                });

                // 0 - 500 ms: client is writing request
                // 1000 - 1500 ms: server receieves request
                // 1500 - 2000 ms: server writes response
                // 2500 - 3000 ms: client receives response
                lockStepTime.Step(TimeSpan.FromMilliseconds(1400), 50); // 1400
                Assert.IsFalse(Volatile.Read(ref serverReadMessage));
                lockStepTime.Step(TimeSpan.FromMilliseconds(200), 50); // 1600
                Assert.IsTrue(Volatile.Read(ref serverReadMessage));
                lockStepTime.Step(TimeSpan.FromMilliseconds(400), 50); // 2000
                Assert.IsFalse(Volatile.Read(ref clientReadMessage));
                lockStepTime.Step(TimeSpan.FromMilliseconds(800), 50); // 2800
                Assert.IsFalse(Volatile.Read(ref clientReadMessage));
                lockStepTime.Step(TimeSpan.FromMilliseconds(400), 50); // 3200
                Assert.IsTrue(Volatile.Read(ref clientReadMessage));

                await serverTask.ConfigureAwait(false);
                await clientTask.ConfigureAwait(false);
            }
        }

        [TestMethod]
        public async Task TestDirectSocketWithSimulatedThrottling()
        {
            ILogger logger = new ConsoleLogger();
            const int MESSAGE_SIZE = 1024 * 4;
            LockStepRealTimeProvider lockStepTime = new LockStepRealTimeProvider(logger);
            DirectSocketPair socketPair = DirectSocket.CreateSocketPair(null, 1024); // Simulated bandwidth of 1 kilobytes per second

            using (socketPair.ClientSocket)
            using (socketPair.ServerSocket)
            using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
                CancellationToken cancelToken = cts.Token;

                bool serverReadMessage = false;
                bool clientReadMessage = false;

                IRealTimeProvider serverThreadTime = lockStepTime.Fork("Server");
                IRealTimeProvider clientThreadTime = lockStepTime.Fork("Client");
                Task serverTask = Task.Run(async () =>
                {
                    try
                    {
                        byte[] buf = new byte[MESSAGE_SIZE];
                        ISocket serverSocket = socketPair.ServerSocket;

                        // Read a full message
                        int socketReadSize = await serverSocket.ReadAsync(buf, 0, MESSAGE_SIZE, cancelToken, serverThreadTime).ConfigureAwait(false);
                        Assert.AreEqual(MESSAGE_SIZE, socketReadSize);

                        Volatile.Write(ref serverReadMessage, true);

                        // Echo it back
                        await SocketHelpers.PiecewiseWrite(serverSocket, buf, 0, MESSAGE_SIZE, cancelToken, serverThreadTime).ConfigureAwait(false);
                    }
                    finally
                    {
                        serverThreadTime.Merge();
                    }
                });

                Task clientTask = Task.Run(async () =>
                {
                    try
                    {
                        byte[] outMessage = new byte[MESSAGE_SIZE];
                        byte[] inMessage = new byte[MESSAGE_SIZE];
                        ISocket clientSocket = socketPair.ClientSocket;
                        FastRandom.Shared.NextBytes(outMessage);

                        // Write message in a bunch of small parts (to get more accurate wait times)
                        await SocketHelpers.PiecewiseWrite(clientSocket, outMessage, 0, MESSAGE_SIZE, cancelToken, clientThreadTime).ConfigureAwait(false);

                        // Read back echoed message
                        int socketReadSize = await clientSocket.ReadAsync(inMessage, 0, MESSAGE_SIZE, cancelToken, clientThreadTime).ConfigureAwait(false);

                        Volatile.Write(ref clientReadMessage, true);

                        // Assert that the same message came back
                        Assert.AreEqual(MESSAGE_SIZE, socketReadSize);
                        Assert.IsTrue(ArrayExtensions.ArrayEquals(outMessage, 0, inMessage, 0, MESSAGE_SIZE));
                    }
                    finally
                    {
                        clientThreadTime.Merge();
                    }
                });

                // 0 - 4000 ms: client is writing request
                // 4000 - 8000 ms: client receives response
                lockStepTime.Step(TimeSpan.FromMilliseconds(3500), 100); // 3500
                Assert.IsFalse(Volatile.Read(ref serverReadMessage));
                lockStepTime.Step(TimeSpan.FromMilliseconds(1000), 50); // 4500
                Assert.IsTrue(Volatile.Read(ref serverReadMessage));
                lockStepTime.Step(TimeSpan.FromMilliseconds(3000), 100); // 7500
                Assert.IsFalse(Volatile.Read(ref clientReadMessage));
                lockStepTime.Step(TimeSpan.FromMilliseconds(1500), 50); // 9000
                Assert.IsTrue(Volatile.Read(ref clientReadMessage));

                await serverTask.ConfigureAwait(false);
                await clientTask.ConfigureAwait(false);
            }
        }

        [TestMethod]
        public async Task TestSocketReliableReadThrowsEndOfStreamException()
        {
            DirectSocketPair socketPair = DirectSocket.CreateSocketPair();

            using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                CancellationToken cancelToken = cts.Token;
                IRandom rand = new FastRandom();

                Task serverTask = Task.Run(async () =>
                {
                    using (socketPair.ServerSocket)
                    {
                        byte[] buf = new byte[1000];
                        ISocket serverSocket = socketPair.ServerSocket;

                        // Reliably read 1000 bytes (the client will only send 900)
                        try
                        {
                            await serverSocket.ReadAsync(buf, 0, 1000, cancelToken, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                            Assert.Fail("Expected an EndOfStreamException");
                        }
                        catch (EndOfStreamException) { }
                    }
                });

                Task clientTask = Task.Run(async () =>
                {
                    using (socketPair.ClientSocket)
                    {
                        byte[] outMessage = new byte[900];
                        ISocket clientSocket = socketPair.ClientSocket;

                        // Write 900 bytes and then close
                        rand.NextBytes(outMessage, 0, outMessage.Length);
                        await clientSocket.WriteAsync(outMessage, 0, outMessage.Length, cancelToken, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    }
                });

                await serverTask.ConfigureAwait(false);
                await clientTask.ConfigureAwait(false);
            }
        }
    }
}
