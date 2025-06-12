using Durandal.API;
using Durandal.Extensions.BondProtocol;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Runtime;
using Durandal.Common.Dialog.Services;
using Durandal.Common.Dialog.Web;
using Durandal.Common.File;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.NLP;
using Durandal.Common.Ontology;
using Durandal.Common.Remoting;
using Durandal.Common.Remoting.Protocol;
using Durandal.Common.Speech.SR;
using Durandal.Common.Speech.TTS;
using Durandal.Common.Test;
using Durandal.Common.Utils;
using Durandal.Common.MathExt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.IO;
using Durandal.Common.Config;
using Durandal.Common.LG.Statistical;
using System.Diagnostics;
using Durandal.Tests.Common.Dialog.Runtime;
using Durandal.Tests.Common.Remoting;
using Durandal.Common.NLP.Language;
using Durandal.Common.Collections;
using Durandal.Common.ServiceMgmt;
using System.Runtime.InteropServices;

namespace Durandal.Tests.Common.Remoting
{
    [TestClass]
    public class RemotingTests
    {
        private static readonly IRemoteDialogProtocol[] ALL_PROTOCOLS = new IRemoteDialogProtocol[] { new JsonRemoteDialogProtocol(), new BondRemoteDialogProtocol() };

        private static ILogger _logger;
        private static IConfiguration _baseConfig;
        private static RemotingConfiguration _remotingConfig;
        private static IThreadPool _serverThreadPool;
        private static IDialogExecutor _dialogExecutor;
        private static IRemoteDialogProtocol _remotingProtocol;
        private static BasicPluginLoader _pluginLoader;
        private static LocallyRemotedPluginProvider _remotePluginProvider;
        private static PluginStrongName _pluginId;
        private static RemotingPlugin _testPlugin;

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            _logger = new ConsoleLogger("Main");
            _baseConfig = new InMemoryConfiguration(_logger.Clone("Config"));
            _remotingConfig = DialogTestHelpers.GetTestRemotingConfiguration(new WeakPointer<IConfiguration>(_baseConfig));
            ILogger testFrameworkLogger = _logger.Clone("TestFramework");
            _serverThreadPool = new TaskThreadPool();
            _dialogExecutor = new BasicDialogExecutor(true);
            _remotingProtocol = new BondRemoteDialogProtocol();
            _pluginLoader = new BasicPluginLoader(_dialogExecutor, NullFileSystem.Singleton);
            _testPlugin = new RemotingPlugin();
            _pluginLoader.RegisterPluginType(_testPlugin);
            _remotePluginProvider = new LocallyRemotedPluginProvider(
                testFrameworkLogger,
                _pluginLoader,
                _remotingProtocol,
                _remotingConfig,
                new WeakPointer<IThreadPool>(_serverThreadPool),
                new NullSpeechSynth(),
                NullSpeechRecoFactory.Singleton,
                new Durandal.Common.Security.OAuth.OAuthManager("https://null", new FakeOAuthSecretStore(), NullMetricCollector.WeakSingleton, DimensionSet.Empty),
                new NullHttpClientFactory(),
                NullFileSystem.Singleton,
                new RoslynLGScriptCompiler(),
                new NLPToolsCollection(),
                new DefaultEntityResolver(new GenericEntityResolver(new NLPToolsCollection())),
                NullMetricCollector.WeakSingleton,
                DimensionSet.Empty,
                serverSocketFactory: null,
                clientSocketFactory: null,
                realTime: DefaultRealTimeProvider.Singleton,
                useDebugTimeouts: false);

            _pluginId = _testPlugin.GetStrongName();
            _remotePluginProvider.LoadPlugin(_pluginId, testFrameworkLogger, DefaultRealTimeProvider.Singleton).Await();
        }

        [TestInitialize]
        public void ResetBeforeTest()
        {
        }

        [ClassCleanup]
        public static void CleanupAllTests()
        {
            //_remotePluginProvider.UnloadPlugin(_pluginId, _logger.Clone("TestFramework")).Await();
            _baseConfig?.Dispose(); // this doesn't actually do anything but whatever
        }

        [TestMethod]
        public async Task TestRemotingAnonymousPipeSocket()
        {
            IRandom rand = new FastRandom();
            CancellationTokenSource testKiller = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            using (AnonymousPipeServerSocket server = new AnonymousPipeServerSocket(65536))
            {
                using (AnonymousPipeClientSocket client = new AnonymousPipeClientSocket(server.RemoteEndpointString))
                {
                    for (int c = 0; c < 10; c++)
                    {
                        byte[] serverBuf = new byte[1024 * (c + 1)];
                        rand.NextBytes(serverBuf);
                        await server.WriteAsync(serverBuf, 0, serverBuf.Length, testKiller.Token, realTime);

                        byte[] clientBuf = new byte[1024 * (c + 1)];
                        await client.ReadAsync(clientBuf, 0, clientBuf.Length, testKiller.Token, realTime);

                        Assert.IsTrue(ArrayExtensions.ArrayEquals(serverBuf, clientBuf));
                        rand.NextBytes(clientBuf);

                        await client.WriteAsync(clientBuf, 0, clientBuf.Length, testKiller.Token, realTime);
                        await server.ReadAsync(serverBuf, 0, serverBuf.Length, testKiller.Token, realTime);

                        Assert.IsTrue(ArrayExtensions.ArrayEquals(serverBuf, clientBuf));
                    }
                }
            }

            Assert.IsFalse(testKiller.IsCancellationRequested);
        }

        [TestMethod]
        public async Task TestRemotingNamedPipeSocket()
        {
            IRandom rand = new FastRandom();
            string pipeName = "test-pipe-" + rand.NextInt(1, 999);
            int payloadSize = 200000;

            EventOnlyLogger errorLogger = new EventOnlyLogger("ErrorLogs");
            ILogger logger = new AggregateLogger("Main", new TaskThreadPool(), 
                new ConsoleLogger("Console", LogLevel.All),
                new DebugLogger("Debug", LogLevel.All),
                errorLogger);
            
            CancellationTokenSource testKiller = new CancellationTokenSource();
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            testKiller.CancelAfter(TimeSpan.FromSeconds(10));
            int threadsFinished = 0;

            int numTestThreads = 4;
            using (IThreadPool clientThreadPool = new CustomThreadPool(logger, NullMetricCollector.Singleton, DimensionSet.Empty, ThreadPriority.Normal, "ThreadPool", numTestThreads, false))
            {
                using (NamedPipeServer server = new NamedPipeServer(logger.Clone("PipeServer"), pipeName, 64))
                {
                    server.RegisterSubclass(new EchoServer(payloadSize));
                    await server.StartServer("NamedPipeServer", testKiller.Token, realTime);
                    NamedPipeClientSocketFactory clientSocketFactory = new NamedPipeClientSocketFactory();
                    for (int c = 0; c < numTestThreads; c++)
                    {
                        clientThreadPool.EnqueueUserAsyncWorkItem(async () =>
                        {
                            using (ISocket clientSocket = await clientSocketFactory.Connect(pipeName, 0, false, null, testKiller.Token, realTime))
                            {
                                byte[] payloadOut = new byte[payloadSize];
                                byte[] payloadIn = new byte[payloadSize];
                                rand.NextBytes(payloadOut);
                                int bytesProcessed = 0;
                                while (bytesProcessed < payloadSize)
                                {
                                    int chunkSize = Math.Min(rand.NextInt(1, 5000), payloadSize - bytesProcessed);
                                    await clientSocket.WriteAsync(payloadOut, bytesProcessed, chunkSize, testKiller.Token, realTime);
                                    //logger.Log("Wrote " + chunkSize + " bytes");
                                    await clientSocket.ReadAsync(payloadIn, bytesProcessed, chunkSize, testKiller.Token, realTime);
                                    //logger.Log("Read " + chunkSize + " bytes");
                                    bytesProcessed += chunkSize;
                                }

                                Assert.IsTrue(ArrayExtensions.ArrayEquals(payloadOut, payloadIn));
                                logger.Log("Client finished successfully");
                                Interlocked.Increment(ref threadsFinished);
                            }
                        });
                    }

                    while (threadsFinished < numTestThreads &&
                        !testKiller.IsCancellationRequested)
                    {
                        await Task.Delay(100);
                    }

                    await Task.Delay(1000);
                }

                clientThreadPool.EnqueueUserWorkItem(() => { }); // This will force a pool health check and will cause unhandled exceptions in threads to be re-thrown
            }

            Assert.IsFalse(testKiller.IsCancellationRequested);

            // Verify that no error logs were generated
            ILoggingHistory history = errorLogger.History;
            IList<LogEvent> errorLogs = history.FilterByCriteria(new FilterCriteria()
            {
                Level = LogLevel.Err
            }).ToList();

            Assert.AreEqual(0, errorLogs.Count, "Error logs were present");
        }

        [TestMethod]
        public async Task TestRemotingMMIOSocket()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                IRandom rand = new FastRandom();
                CancellationTokenSource testKiller = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
                using (MMIOServerSocket server = new MMIOServerSocket(_logger, NullMetricCollector.WeakSingleton, DimensionSet.Empty, 65536))
                {
                    using (MMIOClientSocket client = new MMIOClientSocket(server.RemoteEndpointString, _logger, NullMetricCollector.Singleton, DimensionSet.Empty))
                    {
                        for (int c = 0; c < 10; c++)
                        {
                            byte[] serverBuf = new byte[1024 * (c + 1)];
                            rand.NextBytes(serverBuf);
                            await server.WriteAsync(serverBuf, 0, serverBuf.Length, testKiller.Token, realTime);

                            byte[] clientBuf = new byte[1024 * (c + 1)];
                            await client.ReadAsync(clientBuf, 0, clientBuf.Length, testKiller.Token, realTime);

                            Assert.IsTrue(ArrayExtensions.ArrayEquals(serverBuf, clientBuf));
                            rand.NextBytes(clientBuf);

                            await client.WriteAsync(clientBuf, 0, clientBuf.Length, testKiller.Token, realTime);
                            await server.ReadAsync(serverBuf, 0, serverBuf.Length, testKiller.Token, realTime);

                            Assert.IsTrue(ArrayExtensions.ArrayEquals(serverBuf, clientBuf));
                        }
                    }
                }

                Assert.IsFalse(testKiller.IsCancellationRequested);
            }
            else
            {
                Assert.Inconclusive("Test must run on Windows platform");
            }
        }

        private class EchoServer : ISocketServerDelegate
        {
            private readonly int _payloadSize;

            public EchoServer(int payloadSize)
            {
                _payloadSize = payloadSize;
            }

            public async Task HandleSocketConnection(ISocket clientSocket, ServerBindingInfo socketBinding, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                try
                {
                    int totalRead = 0;
                    byte[] buf = new byte[1024];
                    while (totalRead < _payloadSize)
                    {
                        int read = await clientSocket.ReadAnyAsync(buf, 0, buf.Length, cancelToken, realTime);
                        if (read > 0)
                        {
                            await clientSocket.WriteAsync(buf, 0, read, cancelToken, realTime);
                        }

                        totalRead += read;
                    }
                }
                catch (Exception e)
                {
                    e.GetHashCode();
                }
            }
        }

        [TestMethod]
        public async Task TestRemotingMemoryMappedFileStream()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ILogger logger = new ConsoleLogger();
                IRandom rand = new FastRandom(500);

                for (int loop = 0; loop < 1000; loop++)
                {
                    string fileName = Guid.NewGuid().ToString("N").Substring(0, 8);
                    int bufSize = rand.NextInt(100, 10000);
                    int transferSize = rand.NextInt(bufSize, 100000);
                    logger.Log("Test run " + loop + ": filename " + fileName + " \tbufSize " + bufSize + " \ttransferSize " + transferSize);
                    using (MemoryMappedFileStream writeStream = new MemoryMappedFileStream(fileName, bufSize, logger, NullMetricCollector.WeakSingleton, DimensionSet.Empty, false))
                    {
                        using (MemoryMappedFileStream readStream = new MemoryMappedFileStream(fileName, logger, NullMetricCollector.Singleton, DimensionSet.Empty, true))
                        {
                            byte[] writeBuf = new byte[transferSize];
                            byte[] readBuf = new byte[transferSize];
                            for (int c = 0; c < transferSize; c++)
                            {
                                writeBuf[c] = (byte)(c % 256);
                            }

                            // Create a background thread that produces incrementing bytes
                            Task writeTask = Task.Run(async () =>
                            {
                                try
                                {
                                    int amountWritten = 0;
                                    while (amountWritten < transferSize)
                                    {
                                        int toWrite = Math.Min(transferSize - amountWritten, rand.NextInt(1, bufSize * 10));
                                        await writeStream.WriteAsync(writeBuf, amountWritten, toWrite, CancellationToken.None);
                                        amountWritten += toWrite;
                                    }
                                }
                                catch (Exception e)
                                {
                                    logger.Log(e, LogLevel.Err);
                                    Assert.Fail("Write task threw an exception");
                                }
                                finally
                                {
                                    logger.Log("Write finished");
                                }
                            });

                            // And read it on the main thread
                            Task<int> readTask = readStream.ReadAsync(readBuf, 0, transferSize, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                            int totalRead = await readTask;
                            await writeTask;
                            logger.Log("Read finished " + totalRead);

                            Assert.IsTrue(ArrayExtensions.ArrayEquals(readBuf, writeBuf));
                        }
                    }
                }
            }
            else
            {
                Assert.Inconclusive("Test must run on Windows platform");
            }
        }

        [TestMethod]
        public async Task TestPostOfficeResiliency()
        {
            CancellationTokenSource testKiller = new CancellationTokenSource();
            testKiller.CancelAfter(TimeSpan.FromSeconds(60));
            EventOnlyLogger errorLogger = new EventOnlyLogger("ErrorLogs");
            ILogger logger = new AggregateLogger("Main", new TaskThreadPool(),
                new ConsoleLogger("Console", LogLevel.All),
                new DebugLogger("Debug", LogLevel.All),
                errorLogger);
            IRandom rand = new FastRandom(10);
            IThreadPool serverThreadPool = new TaskThreadPool();
            uint protocolId = 3223;
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;

            DirectSocketPair socketPair = DirectSocket.CreateSocketPair();

            byte[] capturePattern = new byte[] { (byte)'D', (byte)'P', (byte)'o', (byte)'P' };

            using (PostOffice serverPostOffice = new PostOffice(socketPair.ServerSocket, logger.Clone("ServerPostOffice"), TimeSpan.FromSeconds(30), true, realTime))
            {
                using (PostOffice clientPostOffice = new PostOffice(socketPair.ClientSocket, logger.Clone("ClientPostOffice"), TimeSpan.FromSeconds(30), false, realTime))
                {
                    for (int c = 0; c < 20; c++)
                    {
                        PooledBuffer<byte> clientPayload;

                        if (rand.NextFloat() < 0.1f)
                        {
                            clientPayload = BufferPool<byte>.Rent(0);
                        }
                        else
                        {
                            clientPayload = BufferPool<byte>.Rent(rand.NextInt(100, 200000));

                            // Create a random message
                            rand.NextBytes(clientPayload.Buffer, 0, clientPayload.Length);
                            for (int x = 0; x < 100; x++)
                            {
                                // Embed capture patterns into the correct message
                                Array.Copy(capturePattern, 0, clientPayload.Buffer, rand.NextInt(0, clientPayload.Length - 4), 4);
                            }
                        }

                        PooledBuffer<byte> copyOfClientPayload = BufferPool<byte>.Rent(clientPayload.Length);
                        if (clientPayload.Length > 0)
                        {
                            ArrayExtensions.MemCopy(clientPayload.Buffer, 0, copyOfClientPayload.Buffer, 0, clientPayload.Length);
                        }

                        MailboxId clientMailboxId = clientPostOffice.CreateTransientMailbox(realTime);
                        MailboxMessage outMessage = new MailboxMessage(clientMailboxId, protocolId, clientPayload);
                        await clientPostOffice.SendMessage(outMessage, testKiller.Token, realTime);

                        // Inject garbage into the stream (between packets), also containing more capture patterns
                        byte[] randomJunk = new byte[rand.NextInt(100, 200000)];
                        rand.NextBytes(randomJunk);
                        for (int x = 0; x < 100; x++)
                        {
                            Array.Copy(capturePattern, 0, randomJunk, rand.NextInt(0, randomJunk.Length - 4), 4);
                        }

                        await socketPair.ClientSocket.WriteAsync(randomJunk, 0, randomJunk.Length, testKiller.Token, realTime);
                        await socketPair.ClientSocket.FlushAsync(testKiller.Token, realTime);

                        MailboxId serverMailboxId = await serverPostOffice.WaitForMessagesOnNewMailbox(testKiller.Token, realTime);
                        RetrieveResult<MailboxMessage> inMessage = await serverPostOffice.TryReceiveMessage(serverMailboxId, testKiller.Token, TimeSpan.FromSeconds(10), realTime);
                        Assert.IsTrue(inMessage.Success);
                        Assert.IsTrue(ArrayExtensions.ArrayEquals(copyOfClientPayload, inMessage.Result.Buffer));
                    }
                }
            }
        }
        
        [TestMethod]
        public async Task TestRemoteMailboxProtocol()
        {
            using (CancellationTokenSource testKiller = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
            {
                EventOnlyLogger errorLogger = new EventOnlyLogger("ErrorLogs");
                ILogger logger = new AggregateLogger("Main", new TaskThreadPool(),
                    new ConsoleLogger("Console", LogLevel.All),
                    new DebugLogger("Debug", LogLevel.All),
                    errorLogger);
                IRandom rand = new FastRandom();
                IThreadPool serverThreadPool = new TaskThreadPool();
                uint protocolId = 3223;
                IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;

                DirectSocketPair socketPair = DirectSocket.CreateSocketPair();
                ManualResetEvent serverFinished = new ManualResetEvent(false);
                ManualResetEvent clientFinished = new ManualResetEvent(false);
                int serverTurns = 0;
                int clientTurns = 0;

                // Start thread to represent the server
                Task serverTask = Task.Run(async () =>
                {
                    ILogger serverLogger = logger.Clone("Server");

                    try
                    {
                        using (socketPair.ServerSocket)
                        using (PostOffice postOffice = new PostOffice(
                            socketPair.ServerSocket,
                            serverLogger.Clone("ServerPostOffice"),
                            TimeSpan.FromSeconds(30),
                            isServer: true,
                            realTime: realTime))
                        {
                            for (int c = 0; c < 10; c++)
                            {
                                if (testKiller.IsCancellationRequested)
                                {
                                    break;
                                }

                                MailboxId newMailboxId = await postOffice.WaitForMessagesOnNewMailbox(testKiller.Token, realTime);

                                for (int rep = 0; rep < 3; rep++)
                                {
                                    // Receive the message on the box
                                    //serverLogger.Log("Server waiting for message");
                                    RetrieveResult<MailboxMessage> gotMessage = await postOffice.TryReceiveMessage(newMailboxId, testKiller.Token, TimeSpan.FromSeconds(10), realTime);

                                    // We don't expect this to fail because that would mean we got a new mailbox signal without
                                    // anything in it
                                    if (!gotMessage.Success)
                                    {
                                        serverLogger.Log("Failed to read message", LogLevel.Err);
                                        return;
                                    }

                                    await realTime.WaitAsync(TimeSpan.FromMilliseconds(10), testKiller.Token);

                                    // And echo back the original message
                                    //serverLogger.Log("Server got message, sending junk");
                                    // Send a callback message on a new mailbox
                                    MailboxId callbackBoxId = postOffice.CreateTransientMailbox(realTime);
                                    PooledBuffer<byte> data = BufferPool<byte>.Rent(10000);
                                    rand.NextBytes(data.Buffer, 0, data.Length);
                                    MailboxMessage outMessage = new MailboxMessage(callbackBoxId, 123456, data);
                                    await postOffice.SendMessage(outMessage, testKiller.Token, realTime);

                                    //serverLogger.Log("Server sending response");
                                    await postOffice.SendMessage(gotMessage.Result, testKiller.Token, realTime);

                                    gotMessage.Result.DisposeOfBuffer();
                                    Interlocked.Increment(ref serverTurns);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        serverLogger.Log(e, LogLevel.Err);
                        Assert.Fail("Unhandled exception in server");
                    }
                    finally
                    {
                        //serverLogger.Log("Server is closing");
                        await socketPair.ServerSocket.Disconnect(testKiller.Token, realTime, NetworkDuplex.ReadWrite);
                        serverFinished.Set();
                    }
                });

                Task clientTask = Task.Run(async () =>
                {
                    ILogger clientLogger = logger.Clone("Client");
                    try
                    {
                        using (socketPair.ClientSocket)
                        using (PostOffice clientPostOffice = new PostOffice(
                            socketPair.ClientSocket,
                            clientLogger.Clone("ClientPostOffice"),
                            TimeSpan.FromSeconds(30),
                            isServer: false,
                            realTime: realTime))
                        {
                            for (int c = 0; c < 10; c++)
                            {
                                MailboxId boxId = clientPostOffice.CreateTransientMailbox(realTime);

                                for (int rep = 0; rep < 3; rep++)
                                {
                                    PooledBuffer<byte> data = BufferPool<byte>.Rent(10000);
                                    rand.NextBytes(data.Buffer, 0, data.Length);
                                    PooledBuffer<byte> copyOfData = BufferPool<byte>.Rent(10000);
                                    ArrayExtensions.MemCopy(data.Buffer, 0, copyOfData.Buffer, 0, data.Length);
                                    MailboxMessage outMessage = new MailboxMessage(boxId, protocolId, data);
                                    //clientLogger.Log("Client sending message");
                                    await clientPostOffice.SendMessage(outMessage, testKiller.Token, realTime);
                                    //logger.Log("Client waiting for response");
                                    RetrieveResult<MailboxMessage> inMessage = new RetrieveResult<MailboxMessage>();
                                    int loops = 0;
                                    while (!inMessage.Success && loops < 10)
                                    {
                                        // loop a while because TryReceieveMessage will return immediately
                                        // if there's nothing currently in the box
                                        await realTime.WaitAsync(TimeSpan.FromMilliseconds(10), testKiller.Token);
                                        inMessage = await clientPostOffice.TryReceiveMessage(boxId, testKiller.Token, TimeSpan.FromSeconds(10), realTime);
                                    }

                                    if (!inMessage.Success)
                                    {
                                        clientLogger.Log("Failed to read message", LogLevel.Err);
                                        return;
                                    }
                                    if (!ArrayExtensions.ArrayEquals(copyOfData, inMessage.Result.Buffer))
                                    {
                                        clientLogger.Log("Message content does not match", LogLevel.Err);
                                        return;
                                    }
                                    if (outMessage.ProtocolId != inMessage.Result.ProtocolId)
                                    {
                                        clientLogger.Log("Message content does not match", LogLevel.Err);
                                        return;
                                    }

                                    copyOfData.Dispose();
                                    inMessage.Result.DisposeOfBuffer();
                                    Interlocked.Increment(ref clientTurns);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        clientLogger.Log(e, LogLevel.Err);
                        Assert.Fail("Unhandled exception in client");
                    }
                    finally
                    {
                        //clientLogger.Log("Client is closing");
                        await socketPair.ClientSocket.Disconnect(testKiller.Token, realTime, NetworkDuplex.ReadWrite);
                        clientFinished.Set();
                    }
                });


                //realTime.Step(TimeSpan.FromMilliseconds(10000), 10);

                //testKiller.Cancel();
                await serverTask;
                await clientTask;
                serverFinished.WaitOne(TimeSpan.FromSeconds(10));
                clientFinished.WaitOne(TimeSpan.FromSeconds(10));
                Assert.AreEqual(30, serverTurns);
                Assert.AreEqual(30, clientTurns);

                // Verify that no error logs were generated
                ILoggingHistory history = errorLogger.History;
                IList<LogEvent> errorLogs = history.FilterByCriteria(new FilterCriteria()
                {
                    Level = LogLevel.Err
                }).ToList();

                Assert.AreEqual(0, errorLogs.Count, "Error logs were present");
            }
        }

        [TestMethod]
        public async Task TestRemoteMailboxProtocolZeroLengthPacket()
        {
            CancellationTokenSource testKiller = new CancellationTokenSource();
            testKiller.CancelAfter(TimeSpan.FromSeconds(60));
            EventOnlyLogger errorLogger = new EventOnlyLogger("ErrorLogs");
            ILogger logger = new AggregateLogger("Main", new TaskThreadPool(), 
                new ConsoleLogger("Console", LogLevel.All),
                new DebugLogger("Debug", LogLevel.All),
                errorLogger);
            IThreadPool serverThreadPool = new TaskThreadPool();
            uint protocolId = 3223;
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;

            DirectSocketPair socketPair = DirectSocket.CreateSocketPair();
            ManualResetEvent serverFinished = new ManualResetEvent(false);
            ManualResetEvent clientFinished = new ManualResetEvent(false);
            int serverTurns = 0;
            int clientTurns = 0;

            // Start thread to represent the server
            Task serverTask = Task.Run(async () =>
            {
                try
                {
                    using (PostOffice postOffice = new PostOffice(socketPair.ServerSocket, logger.Clone("ServerPostOffice"), TimeSpan.FromSeconds(30), true, realTime))
                    {
                        if (testKiller.IsCancellationRequested)
                        {
                            return;
                        }

                        logger.Log("Server waiting for new mailbox");
                        MailboxId newMailboxId = await postOffice.WaitForMessagesOnNewMailbox(testKiller.Token, realTime);
                        
                        // Receive the message on the box
                        logger.Log("Server waiting for message");
                        RetrieveResult<MailboxMessage> gotMessage = await postOffice.TryReceiveMessage(newMailboxId, testKiller.Token, TimeSpan.FromSeconds(10), realTime);

                        Assert.IsTrue(gotMessage.Success);
                        Assert.AreEqual(protocolId, gotMessage.Result.ProtocolId);
                        Assert.AreEqual(0, gotMessage.Result.Buffer.Length);

                        // And echo back the original message
                        logger.Log("Server sending response");
                        await postOffice.SendMessage(gotMessage.Result, testKiller.Token, realTime);

                        Interlocked.Increment(ref serverTurns);
                    }
                }
                catch (Exception e)
                {
                    logger.Log(e, LogLevel.Err);
                    Assert.Fail("Unhandled exception in server");
                }
                finally
                {
                    await socketPair.ServerSocket.Disconnect(testKiller.Token, realTime, NetworkDuplex.ReadWrite);
                    serverFinished.Set();
                }
            });

            Task clientTask = Task.Run(async () =>
            {
                try
                {
                    using (PostOffice clientPostOffice = new PostOffice(socketPair.ClientSocket, logger.Clone("ClientPostOffice"), TimeSpan.FromSeconds(30), false, realTime))
                    {
                        MailboxId boxId = clientPostOffice.CreateTransientMailbox(realTime);
                        MailboxMessage outMessage = new MailboxMessage(boxId, protocolId, BufferPool<byte>.Rent(0));
                        logger.Log("Client sending message");
                        await clientPostOffice.SendMessage(outMessage, testKiller.Token, realTime);
                        logger.Log("Client waiting for response");
                        RetrieveResult<MailboxMessage> inMessage = await clientPostOffice.TryReceiveMessage(boxId, testKiller.Token, TimeSpan.FromSeconds(10), realTime);
                        logger.Log("Client got response");
                        Assert.IsTrue(inMessage.Success);
                        Assert.AreEqual(0, inMessage.Result.Buffer.Length);
                        Assert.AreEqual(protocolId, inMessage.Result.ProtocolId);

                        Interlocked.Increment(ref clientTurns);
                    }
                }
                catch (Exception e)
                {
                    logger.Log(e, LogLevel.Err);
                    Assert.Fail("Unhandled exception in client");
                }
                finally
                {
                    await socketPair.ClientSocket.Disconnect(testKiller.Token, realTime, NetworkDuplex.ReadWrite);
                    clientFinished.Set();
                }
            });


            //realTime.Step(TimeSpan.FromMilliseconds(10000), 10);

            //testKiller.Cancel();
            await serverTask;
            await clientTask;
            serverFinished.WaitOne(TimeSpan.FromSeconds(10));
            clientFinished.WaitOne(TimeSpan.FromSeconds(10));
            Assert.AreEqual(1, serverTurns);
            Assert.AreEqual(1, clientTurns);

            // Verify that no error logs were generated
            ILoggingHistory history = errorLogger.History;
            IList<LogEvent> errorLogs = history.FilterByCriteria(new FilterCriteria()
            {
                Level = LogLevel.Err
            }).ToList();

            Assert.AreEqual(0, errorLogs.Count, "Error logs were present");
        }

        [TestMethod]
        public async Task TestRemoteMailboxProtocolPermanentMailbox()
        {
            CancellationTokenSource testKiller = new CancellationTokenSource();
            testKiller.CancelAfter(TimeSpan.FromSeconds(60));
            EventOnlyLogger errorLogger = new EventOnlyLogger("ErrorLogs");
            ILogger logger = new AggregateLogger("Main", new TaskThreadPool(),
                new ConsoleLogger("Console", LogLevel.All),
                new DebugLogger("Debug", LogLevel.All),
                errorLogger);
            IThreadPool serverThreadPool = new TaskThreadPool();
            uint protocolId = 3223;
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            IRandom rand = new FastRandom();

            DirectSocketPair socketPair = DirectSocket.CreateSocketPair();
            ManualResetEvent serverFinished = new ManualResetEvent(false);
            ManualResetEvent clientFinished = new ManualResetEvent(false);
            int serverTurns = 0;
            int clientTurns = 0;
            ushort permanentBoxId = 23;

            // Start thread to represent the server
            Task serverTask = Task.Run(async () =>
            {
                try
                {
                    using (PostOffice postOffice = new PostOffice(socketPair.ServerSocket, logger.Clone("ServerPostOffice"), TimeSpan.FromSeconds(30), true, realTime))
                    {
                        if (testKiller.IsCancellationRequested)
                        {
                            return;
                        }

                        MailboxId newMailboxId = postOffice.CreatePermanentMailbox(realTime, permanentBoxId);

                        // Receive the message on the box
                        logger.Log("Server waiting for message on permanent mailbox " + newMailboxId);
                        RetrieveResult<MailboxMessage> gotMessage = await postOffice.TryReceiveMessage(newMailboxId, testKiller.Token, TimeSpan.FromSeconds(10), realTime);

                        Assert.IsTrue(gotMessage.Success);
                        Assert.AreEqual(protocolId, gotMessage.Result.ProtocolId);
                        Assert.AreEqual(100, gotMessage.Result.Buffer.Length);

                        // And echo back the original message
                        logger.Log("Server sending response");
                        await postOffice.SendMessage(gotMessage.Result, testKiller.Token, realTime);

                        Interlocked.Increment(ref serverTurns);
                    }
                }
                catch (Exception e)
                {
                    logger.Log(e, LogLevel.Err);
                    Assert.Fail("Unhandled exception in server");
                }
                finally
                {
                    await socketPair.ServerSocket.Disconnect(testKiller.Token, realTime, NetworkDuplex.ReadWrite);
                    serverFinished.Set();
                }
            });

            Task clientTask = Task.Run(async () =>
            {
                try
                {
                    using (PostOffice clientPostOffice = new PostOffice(socketPair.ClientSocket, logger.Clone("ClientPostOffice"), TimeSpan.FromSeconds(30), false, realTime))
                    {
                        MailboxId boxId = clientPostOffice.CreatePermanentMailbox(realTime, permanentBoxId);
                        PooledBuffer<byte> data = BufferPool<byte>.Rent(100);
                        rand.NextBytes(data.Buffer, 0, data.Length);
                        PooledBuffer<byte> copyOfData = BufferPool<byte>.Rent(100);
                        ArrayExtensions.MemCopy(data.Buffer, 0, copyOfData.Buffer, 0, data.Length);
                        MailboxMessage outMessage = new MailboxMessage(boxId, protocolId, data);
                        logger.Log("Client sending message on permanent mailbox " + boxId);
                        await clientPostOffice.SendMessage(outMessage, testKiller.Token, realTime);
                        logger.Log("Client waiting for response");
                        RetrieveResult<MailboxMessage> inMessage = await clientPostOffice.TryReceiveMessage(boxId, testKiller.Token, TimeSpan.FromSeconds(10), realTime);
                        logger.Log("Client got response");
                        Assert.IsTrue(inMessage.Success);
                        Assert.IsTrue(ArrayExtensions.ArrayEquals(copyOfData, inMessage.Result.Buffer));
                        Assert.AreEqual(protocolId, inMessage.Result.ProtocolId);

                        Interlocked.Increment(ref clientTurns);
                    }
                }
                catch (Exception e)
                {
                    logger.Log(e, LogLevel.Err);
                    Assert.Fail("Unhandled exception in client");
                }
                finally
                {
                    await socketPair.ClientSocket.Disconnect(testKiller.Token, realTime, NetworkDuplex.ReadWrite);
                    clientFinished.Set();
                }
            });


            //realTime.Step(TimeSpan.FromMilliseconds(10000), 10);

            //testKiller.Cancel();
            await serverTask;
            await clientTask;
            serverFinished.WaitOne(TimeSpan.FromSeconds(10));
            clientFinished.WaitOne(TimeSpan.FromSeconds(10));
            Assert.AreEqual(1, serverTurns);
            Assert.AreEqual(1, clientTurns);

            // Verify that no error logs were generated
            ILoggingHistory history = errorLogger.History;
            IList<LogEvent> errorLogs = history.FilterByCriteria(new FilterCriteria()
            {
                Level = LogLevel.Err
            }).ToList();

            Assert.AreEqual(0, errorLogs.Count, "Error logs were present");
        }

        [TestMethod]
        public void TestRemotingProtocolsExecutionRequest()
        {
            ILogger queryLogger = new ConsoleLogger();
            foreach (IRemoteDialogProtocol protocol in ALL_PROTOCOLS)
            {
                RemoteExecutePluginRequest toSerialize = new RemoteExecutePluginRequest()
                {
                    ContextualEntities = new List<ContextualEntity>(),
                    EntityContext = new Durandal.Common.Ontology.KnowledgeContext(),
                    EntityHistory = new InMemoryEntityHistory(),
                    EntryPoint = "Test.EntryPoint",
                    GlobalUserProfile = new InMemoryDataStore(),
                    IsRetry = true,
                    LocalUserProfile = new InMemoryDataStore(),
                    PluginId = new PluginStrongName("MyPlugin", 2, 5),
                    Query = new QueryWithContext()
                    {
                        ClientContext = DialogTestHelpers.GetTestClientContextTextQuery(),
                        Understanding = DialogTestHelpers.GetSimpleRecoResult(_testPlugin.LUDomain, "test1", 1.0f, "this is a test"),
                        InputAudio = new AudioData()
                        {
                            Codec = string.Empty,
                            CodecParams = string.Empty,
                            Data = new ArraySegment<byte>(BinaryHelpers.EMPTY_BYTE_ARRAY)
                        },
                    },
                    SessionStore = new InMemoryDataStore(),
                    TraceId = CommonInstrumentation.FormatTraceId(Guid.NewGuid()),
                    ValidLogLevels = (int)queryLogger.ValidLogLevels,
                };

                PooledBuffer<byte> serialized = protocol.Serialize(toSerialize, queryLogger);
                Tuple<object, Type> parsed = protocol.Parse(serialized, queryLogger);
                Assert.AreEqual(toSerialize.GetType(), parsed.Item2);
                JObject expected = JObject.FromObject(toSerialize);
                JObject actual = JObject.FromObject(parsed.Item1);
                Assert.IsTrue(JObject.DeepEquals(expected, actual), "Not equal: expected\r\n{0}\r\nactual\r\n{1}. Protocol {2}", expected.ToString(), actual.ToString(), protocol.GetType().ToString());
            }
        }

        [TestMethod]
        public void TestRemotingProtocolsTriggerRequest()
        {
            ILogger queryLogger = new ConsoleLogger();
            foreach (IRemoteDialogProtocol protocol in ALL_PROTOCOLS)
            {
                RemoteTriggerPluginRequest toSerialize = new RemoteTriggerPluginRequest()
                {
                    PluginId = new PluginStrongName("MyPlugin", 2, 5),
                    Query = new QueryWithContext()
                    {
                        ClientContext = DialogTestHelpers.GetTestClientContextTextQuery(),
                        Understanding = DialogTestHelpers.GetSimpleRecoResult(_testPlugin.LUDomain, "test1", 1.0f, "this is a test"),
                        InputAudio = new AudioData()
                        {
                            Codec = string.Empty,
                            CodecParams = string.Empty,
                            Data = new ArraySegment<byte>(BinaryHelpers.EMPTY_BYTE_ARRAY)
                        },
                    },
                    TraceId = CommonInstrumentation.FormatTraceId(Guid.NewGuid()),
                    ValidLogLevels = (int)queryLogger.ValidLogLevels,
                };

                PooledBuffer<byte> serialized = protocol.Serialize(toSerialize, queryLogger);
                Tuple<object, Type> parsed = protocol.Parse(serialized, queryLogger);
                Assert.AreEqual(toSerialize.GetType(), parsed.Item2);
                JObject expected = JObject.FromObject(toSerialize);
                JObject actual = JObject.FromObject(parsed.Item1);
                Assert.IsTrue(JObject.DeepEquals(expected, actual), "Not equal: expected\r\n{0}\r\nactual\r\n{1}. Protocol {2}", expected.ToString(), actual.ToString(), protocol.GetType().ToString());
            }
        }

        [TestMethod]
        public void TestRemotingProtocolsDialogProcessingResponse()
        {
            ILogger queryLogger = new ConsoleLogger();
            foreach (IRemoteDialogProtocol protocol in ALL_PROTOCOLS)
            {
                RemoteProcedureResponse<DialogProcessingResponse> toSerialize = new RemoteProcedureResponse<DialogProcessingResponse>(
                    RemoteExecutePluginRequest.METHOD_NAME,
                    new DialogProcessingResponse(
                        new PluginResult(Result.Success)
                        {
                            AugmentedQuery = "this is a Test.",
                            ClientAction = "client action",
                            ContinuationFuncName = "continuation func",
                            ErrorMessage = "error message",
                            MultiTurnResult = MultiTurnBehavior.ContinueBasic,
                            ResponseAudio = null,
                            ResponseData = new Dictionary<string, string>(),
                            ResponseHtml = "html",
                            ResponseSsml = "ssml",
                            ResponseText = "text",
                            ResponseUrl = "url",
                            ResultConversationNode = "result node",
                            SuggestedQueries = new List<string>() { "query 1", "query 2" },
                            TriggerKeywords = new List<TriggerKeyword>()
                            {
                                new TriggerKeyword()
                                {
                                    TriggerPhrase = "HELLO",
                                    ExpireTimeSeconds = 60,
                                    AllowBargeIn = false
                                }
                            }
                        },
                        false)
                        {
                            UpdatedLocalUserProfile = new InMemoryDataStore(),
                            UpdatedSessionStore = new InMemoryDataStore(),
                            UpdatedGlobalUserProfile = new InMemoryDataStore(),
                            UpdatedEntityContext = new KnowledgeContext(),
                            UpdatedEntityHistory = new InMemoryEntityHistory()
                        });

                PooledBuffer<byte> serialized = protocol.Serialize(toSerialize, queryLogger);
                Tuple<object, Type> parsed = protocol.Parse(serialized, queryLogger);
                Assert.AreEqual(toSerialize.GetType(), parsed.Item2);
                JObject expected = JObject.FromObject(toSerialize);
                JObject actual = JObject.FromObject(parsed.Item1);
                Assert.IsTrue(JObject.DeepEquals(expected, actual), "Not equal: expected\r\n{0}\r\nactual\r\n{1}. Protocol {2}", expected.ToString(), actual.ToString(), protocol.GetType().ToString());
            }
        }

        [TestMethod]
        public void TestRemotingProtocolsDialogProcessingResponseWithEntityContexts()
        {
            ILogger queryLogger = new ConsoleLogger();
            foreach (IRemoteDialogProtocol protocol in ALL_PROTOCOLS)
            {
                RemoteProcedureResponse<DialogProcessingResponse> toSerialize = new RemoteProcedureResponse<DialogProcessingResponse>(
                    RemoteExecutePluginRequest.METHOD_NAME,
                    new DialogProcessingResponse(
                        new PluginResult(Result.Success)
                        {
                            AugmentedQuery = "this is a Test.",
                            ClientAction = "client action",
                            ContinuationFuncName = "continuation func",
                            ErrorMessage = "error message",
                            MultiTurnResult = MultiTurnBehavior.ContinueBasic,
                            ResponseAudio = null,
                            ResponseData = new Dictionary<string, string>(),
                            ResponseHtml = "html",
                            ResponseSsml = "ssml",
                            ResponseText = "text",
                            ResponseUrl = "url",
                            ResultConversationNode = "result node",
                            SuggestedQueries = new List<string>() { "query 1", "query 2" },
                            TriggerKeywords = new List<TriggerKeyword>()
                            {
                                new TriggerKeyword()
                                {
                                    TriggerPhrase = "HELLO",
                                    ExpireTimeSeconds = 60,
                                    AllowBargeIn = false
                                }
                            }
                        },
                        false)
                        {
                            UpdatedLocalUserProfile = new InMemoryDataStore(),
                            UpdatedSessionStore = new InMemoryDataStore(),
                            UpdatedGlobalUserProfile = new InMemoryDataStore(),
                            UpdatedEntityContext = new KnowledgeContext(),
                            UpdatedEntityHistory = new InMemoryEntityHistory()
                        });

                Durandal.Tests.EntitySchemas.Person someEntity = new Durandal.Tests.EntitySchemas.Person(toSerialize.ReturnVal.UpdatedEntityContext);
                toSerialize.ReturnVal.UpdatedEntityHistory.AddOrUpdateEntity(someEntity);

                PooledBuffer<byte> serialized = protocol.Serialize(toSerialize, queryLogger);
                Tuple<object, Type> parsed = protocol.Parse(serialized, queryLogger);
                Assert.AreEqual(toSerialize.GetType(), parsed.Item2);
                JObject expected = JObject.FromObject(toSerialize);
                JObject actual = JObject.FromObject(parsed.Item1);
                Assert.IsTrue(JObject.DeepEquals(expected, actual), "Not equal: expected\r\n{0}\r\nactual\r\n{1}. Protocol {2}", expected.ToString(), actual.ToString(), protocol.GetType().ToString());
            }
        }

        [TestMethod]
        public void TestRemotingProtocolsLogMessageRequest()
        {
            ILogger queryLogger = new ConsoleLogger();
            foreach (IRemoteDialogProtocol protocol in ALL_PROTOCOLS)
            {
                RemoteLogMessageRequest toSerialize = new RemoteLogMessageRequest()
                {
                    LogEvents = new InstrumentationEventList()
                    {
                        Events = new List<InstrumentationEvent>()
                        {
                            InstrumentationEvent.FromLogEvent(new LogEvent("Component1", "My message", LogLevel.Ins, DateTimeOffset.UtcNow, Guid.NewGuid(), DataPrivacyClassification.PublicPersonalData)),
                            InstrumentationEvent.FromLogEvent(new LogEvent("Component2", "My message", LogLevel.Ins, DateTimeOffset.UtcNow, Guid.NewGuid(), DataPrivacyClassification.PublicPersonalData))
                        }
                    }
                };

                PooledBuffer<byte> serialized = protocol.Serialize(toSerialize, queryLogger);
                Tuple<object, Type> parsed = protocol.Parse(serialized, queryLogger);
                Assert.AreEqual(toSerialize.GetType(), parsed.Item2);
                JObject expected = JObject.FromObject(toSerialize);
                JObject actual = JObject.FromObject(parsed.Item1);
                Assert.IsTrue(JObject.DeepEquals(expected, actual), "Not equal: expected\r\n{0}\r\nactual\r\n{1}. Protocol {2}", expected.ToString(), actual.ToString(), protocol.GetType().ToString());
            }
        }

        [TestMethod]
        public void TestRemotingProtocolsLoadPluginRequest()
        {
            ILogger queryLogger = new ConsoleLogger();
            foreach (IRemoteDialogProtocol protocol in ALL_PROTOCOLS)
            {
                RemoteLoadPluginRequest toSerialize = new RemoteLoadPluginRequest()
                {
                    PluginId = new PluginStrongName("My Plugin", 2, 5)
                };

                PooledBuffer<byte> serialized = protocol.Serialize(toSerialize, queryLogger);
                Tuple<object, Type> parsed = protocol.Parse(serialized, queryLogger);
                Assert.AreEqual(toSerialize.GetType(), parsed.Item2);
                JObject expected = JObject.FromObject(toSerialize);
                JObject actual = JObject.FromObject(parsed.Item1);
                Assert.IsTrue(JObject.DeepEquals(expected, actual), "Not equal: expected\r\n{0}\r\nactual\r\n{1}. Protocol {2}", expected.ToString(), actual.ToString(), protocol.GetType().ToString());
            }
        }

        [TestMethod]
        public void TestRemotingProtocolsUnloadPluginRequest()
        {
            ILogger queryLogger = new ConsoleLogger();
            foreach (IRemoteDialogProtocol protocol in ALL_PROTOCOLS)
            {
                RemoteUnloadPluginRequest toSerialize = new RemoteUnloadPluginRequest()
                {
                    PluginId = new PluginStrongName("My Plugin", 2, 5)
                };

                PooledBuffer<byte> serialized = protocol.Serialize(toSerialize, queryLogger);
                Tuple<object, Type> parsed = protocol.Parse(serialized, queryLogger);
                Assert.AreEqual(toSerialize.GetType(), parsed.Item2);
                JObject expected = JObject.FromObject(toSerialize);
                JObject actual = JObject.FromObject(parsed.Item1);
                Assert.IsTrue(JObject.DeepEquals(expected, actual), "Not equal: expected\r\n{0}\r\nactual\r\n{1}. Protocol {2}", expected.ToString(), actual.ToString(), protocol.GetType().ToString());
            }
        }
        
        [TestMethod]
        public async Task TestRemotePluginGetAvailablePlugins()
        {
            List<PluginStrongName> returnVal = (await _remotePluginProvider.GetAllAvailablePlugins(DefaultRealTimeProvider.Singleton)).ToList();
            Assert.IsNotNull(returnVal);
            Assert.AreEqual(1, returnVal.Count);
            Assert.AreEqual(_testPlugin.GetStrongName(), returnVal[0]);
        }

        [TestMethod]
        public async Task TestRemotePluginBasicExecution()
        {
            Guid traceId = Guid.NewGuid();
            EventOnlyLogger historyLogger = new EventOnlyLogger("Default");
            ILogger queryLogger = new AggregateLogger(
                "Default",
                LoggerBase.DEFAULT_BACKGROUND_LOGGING_THREAD_POOL,
                _logger,
                historyLogger)
                    .CreateTraceLogger(traceId, "Query");

            QueryWithContext qc = new QueryWithContext()
            {
                ClientContext = DialogTestHelpers.GetTestClientContextTextQuery(),
                Understanding = DialogTestHelpers.GetSimpleRecoResult(_testPlugin.LUDomain, "test1", 1.0f, "this is a test"),
            };

            DialogProcessingResponse response = await _remotePluginProvider.LaunchPlugin(
                _pluginId,
                AbstractDialogExecutor.GetNameOfPluginContinuation(_testPlugin.Test1),
                false,
                qc,
                queryLogger,
                new InMemoryDataStore(),
                new UserProfileCollection(new InMemoryDataStore(), new InMemoryDataStore(), new InMemoryEntityHistory()),
                new Durandal.Common.Ontology.KnowledgeContext(),
                new List<ContextualEntity>(),
                DefaultRealTimeProvider.Singleton);

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.PluginOutput);
            Assert.IsTrue(string.IsNullOrEmpty(response.PluginOutput.ErrorMessage), "Plugin returned error message: {0}", response.PluginOutput.ErrorMessage);
            Assert.AreEqual("Doctor Grant, the phones are working", response.PluginOutput.ResponseText);

            // Also assert that the log message which the plugin wrote showed up in the logger on this side
            Assert.IsTrue(
                historyLogger.History.FilterByCriteria(
                    new FilterCriteria()
                    {
                        TraceId = traceId,
                        Level = LogLevel.Wrn,
                        PrivacyClass = DataPrivacyClassification.PublicNonPersonalData,
                        SearchTerm = "This is a logging message generated in the plugin"
                    })
                .Any());
        }

        [TestMethod]
        public async Task TestRemotePluginBasicTriggering()
        {
            ILogger queryLogger = _logger.CreateTraceLogger(Guid.NewGuid(), "Query");
            QueryWithContext qc = new QueryWithContext()
            {
                ClientContext = DialogTestHelpers.GetTestClientContextTextQuery(),
                Understanding = DialogTestHelpers.GetSimpleRecoResult(_testPlugin.LUDomain, "test1", 1.0f, "this is a test"),
            };

            TriggerProcessingResponse response = await _remotePluginProvider.TriggerPlugin(
                _pluginId,
                qc,
                queryLogger,
                new InMemoryDataStore(),
                new UserProfileCollection(new InMemoryDataStore(), new InMemoryDataStore(), new InMemoryEntityHistory()),
                new Durandal.Common.Ontology.KnowledgeContext(),
                new List<ContextualEntity>(),
                DefaultRealTimeProvider.Singleton);

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.PluginOutput);
            Assert.AreEqual("Call someone on the phone", response.PluginOutput.ActionDescription);
            Assert.IsNotNull(response.UpdatedSessionStore);
            Assert.IsTrue(response.UpdatedSessionStore.ContainsKey("UserState"));
            Assert.AreEqual("SomeUserState", response.UpdatedSessionStore.GetString("UserState"));
        }

        [TestMethod]
        public async Task TestRemotePluginLogging()
        {
            EventOnlyLogger queryLogger = new EventOnlyLogger();
            Guid traceId = Guid.NewGuid();
            QueryWithContext qc = new QueryWithContext()
            {
                ClientContext = DialogTestHelpers.GetTestClientContextTextQuery(),
                Understanding = DialogTestHelpers.GetSimpleRecoResult(_testPlugin.LUDomain, "test1", 1.0f, "this is a test"),
            };

            DialogProcessingResponse response = await _remotePluginProvider.LaunchPlugin(
                _pluginId,
                AbstractDialogExecutor.GetNameOfPluginContinuation(_testPlugin.Test1),
                false,
                qc,
                queryLogger.CreateTraceLogger(traceId, queryLogger.ComponentName),
                new InMemoryDataStore(),
                new UserProfileCollection(new InMemoryDataStore(), new InMemoryDataStore(), new InMemoryEntityHistory()),
                new Durandal.Common.Ontology.KnowledgeContext(),
                new List<ContextualEntity>(),
                DefaultRealTimeProvider.Singleton);

            ILoggingHistory history = queryLogger.History;
            IList<LogEvent> generatedEvents = history.FilterByCriteria(new FilterCriteria()
            {
                TraceId = traceId,
                SearchTerm = "This is a logging message"
            }).ToList();
            Assert.AreEqual(1, generatedEvents.Count);
        }

        [TestMethod]
        public void TestCapturePatternMatcher()
        {
            byte[] capturePattern = new byte[] { 1, 3, 3, 7 };
            // matches at 6 and 21
            byte[] searchRegion = new byte[] { 3, 7, 3, 7, 7, 3, 1, 3, 3, 7, 1, 3, 1, 1, 3, 7, 3, 7, 1, 3, 3, 1, 3, 3, 7, 1 };
            CapturePatternMatcher matcher = new CapturePatternMatcher(capturePattern);
            List<int> matchIndexes = new List<int>();
            for (int c = 0; c < searchRegion.Length; c++)
            {
                byte b = searchRegion[c];
                bool success = matcher.Match(b);
                if (success)
                {
                    int matchIndex = c - capturePattern.Length + 1;
                    Console.WriteLine("Match at " + matchIndex);
                    matchIndexes.Add(matchIndex);
                }
            }

            Assert.AreEqual(2, matchIndexes.Count);
            Assert.AreEqual(6, matchIndexes[0]);
            Assert.AreEqual(21, matchIndexes[1]);
        }

        [TestMethod]
        public void TestCapturePatternMatcherRecursive()
        {
            byte[] capturePattern = new byte[] { 1, 2, 1, 2 };
            // matches at 5, 7, 9
            byte[] searchRegion = new byte[] { 0, 1, 2, 1, 0, 1, 2, 1, 2, 1, 2, 1, 2, 0, 0 };
            CapturePatternMatcher matcher = new CapturePatternMatcher(capturePattern);
            List<int> matchIndexes = new List<int>();
            for (int c = 0; c < searchRegion.Length; c++)
            {
                byte b = searchRegion[c];
                bool success = matcher.Match(b);
                if (success)
                {
                    int matchIndex = c - capturePattern.Length + 1;
                    Console.WriteLine("Match at " + matchIndex);
                    matchIndexes.Add(matchIndex);
                }
            }

            Assert.AreEqual(3, matchIndexes.Count);
            Assert.AreEqual(5, matchIndexes[0]);
            Assert.AreEqual(7, matchIndexes[1]);
            Assert.AreEqual(9, matchIndexes[2]);
        }

        [TestMethod]
        public void TestJsonSerializeMultiTurnBehavior()
        {
            JsonRemoteDialogProtocol protocol = new JsonRemoteDialogProtocol();

            RemoteProcedureResponse<DialogProcessingResponse> toSerialize = new RemoteProcedureResponse<DialogProcessingResponse>(
                RemoteExecutePluginRequest.METHOD_NAME,
                new DialogProcessingResponse(
                    new PluginResult(Result.Success)
                    {
                        AugmentedQuery = "this is a Test.",
                        ClientAction = "client action",
                        ContinuationFuncName = "continuation func",
                        ErrorMessage = "error message",
                        MultiTurnResult = new MultiTurnBehavior()
                        {
                            Continues = true,
                            ConversationTimeoutSeconds = 33,
                            FullConversationControl = false,
                            IsImmediate = true,
                            SuggestedPauseDelay = 15
                        },
                        ResponseAudio = null,
                        ResponseData = new Dictionary<string, string>(),
                        ResponseHtml = "html",
                        ResponseSsml = "ssml",
                        ResponseText = "text",
                        ResponseUrl = "url",
                        ResultConversationNode = "result node",
                        SuggestedQueries = new List<string>() { "query 1", "query 2" },
                        TriggerKeywords = new List<TriggerKeyword>()
                        {
                            new TriggerKeyword()
                            {
                                TriggerPhrase = "HELLO",
                                ExpireTimeSeconds = 60,
                                AllowBargeIn = false
                            }
                        }
                    },
                    false)
                    {
                    UpdatedLocalUserProfile = new InMemoryDataStore(),
                    UpdatedSessionStore = new InMemoryDataStore(),
                    UpdatedGlobalUserProfile = new InMemoryDataStore(),
                    UpdatedEntityContext = new KnowledgeContext(),
                    UpdatedEntityHistory = new InMemoryEntityHistory()
                });

            PooledBuffer<byte> serialized = protocol.Serialize(toSerialize, _logger);
            Tuple<object, Type> parsed = protocol.Parse(serialized, _logger);
            Assert.AreEqual(typeof(RemoteProcedureResponse<DialogProcessingResponse>), parsed.Item2);
            RemoteProcedureResponse<DialogProcessingResponse> cast = parsed.Item1 as RemoteProcedureResponse<DialogProcessingResponse>;
            Assert.AreEqual(true, cast.ReturnVal.PluginOutput.MultiTurnResult.Continues);
            Assert.AreEqual(33, cast.ReturnVal.PluginOutput.MultiTurnResult.ConversationTimeoutSeconds);
            Assert.AreEqual(false, cast.ReturnVal.PluginOutput.MultiTurnResult.FullConversationControl);
            Assert.AreEqual(true, cast.ReturnVal.PluginOutput.MultiTurnResult.IsImmediate);
            Assert.AreEqual(15, cast.ReturnVal.PluginOutput.MultiTurnResult.SuggestedPauseDelay);
        }
    }
}
