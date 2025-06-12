using Durandal.Common.Collections;
using Durandal.Common.Compression;
using Durandal.Common.Events;
using Durandal.Common.File;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Net;
using Durandal.Common.Remoting;
using Durandal.Common.Remoting.Handlers;
using Durandal.Common.Remoting.Protocol;
using Durandal.Common.Remoting.Proxies;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Durandal.Extensions.BondProtocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.File
{
    [TestClass]
    public class FileSystemTests
    {
        [TestMethod]
        public void TestFileSystemHybridBasic()
        {
            InMemoryFileSystem defaultHandler = new InMemoryFileSystem();
            InMemoryFileSystem cacheManager = new InMemoryFileSystem();
            InMemoryFileSystem modelManager = new InMemoryFileSystem();
            HybridFileSystem hybridManager = new HybridFileSystem(defaultHandler);
            hybridManager.AddRoute(new VirtualPath("cache"), cacheManager);
            hybridManager.AddRoute(new VirtualPath("\\models\\en-US"), modelManager);

            defaultHandler.AddFile(new VirtualPath("1.dat"), new byte[1]);
            defaultHandler.AddFile(new VirtualPath("data\\2.dat"), new byte[2]);
            cacheManager.AddFile(new VirtualPath("cache\\3.dat"), new byte[3]);
            cacheManager.AddFile(new VirtualPath("cache\\en-US\\4.dat"), new byte[4]);
            modelManager.AddFile(new VirtualPath("models\\en-US\\5.dat"), new byte[5]);

            Assert.AreEqual(1, ReadFile(new VirtualPath("1.dat"), hybridManager).Length);
            Assert.AreEqual(2, ReadFile(new VirtualPath("data\\2.dat"), hybridManager).Length);
            Assert.AreEqual(3, ReadFile(new VirtualPath("cache\\3.dat"), hybridManager).Length);
            Assert.AreEqual(4, ReadFile(new VirtualPath("cache\\en-US\\4.dat"), hybridManager).Length);
            Assert.AreEqual(5, ReadFile(new VirtualPath("models\\en-US\\5.dat"), hybridManager).Length);
        }

        [TestMethod]
        public void TestFileSystemHybridListDirectories()
        {
            InMemoryFileSystem defaultHandler = new InMemoryFileSystem();
            InMemoryFileSystem cacheManager1 = new InMemoryFileSystem();
            InMemoryFileSystem cacheManager2 = new InMemoryFileSystem();
            HybridFileSystem hybridManager = new HybridFileSystem(defaultHandler);
            hybridManager.AddRoute(new VirtualPath("\\cache\\files\\en-US"), cacheManager1);
            hybridManager.AddRoute(new VirtualPath("\\cache\\files\\es-mx"), cacheManager2);
            cacheManager1.AddFile(new VirtualPath("\\cache\\files\\en-US\\yo\\file1.dat"), new byte[1]);
            cacheManager2.AddFile(new VirtualPath("\\cache\\files\\es-mx\\yo\\file2.dat"), new byte[1]);
            List<VirtualPath> subDirs = hybridManager.ListDirectories(new VirtualPath("\\")).ToList();
            Assert.AreEqual(1, subDirs.Count);
            Assert.AreEqual("\\cache", subDirs[0].FullName);
            Assert.IsTrue(hybridManager.Exists(subDirs[0]));
            subDirs = hybridManager.ListDirectories(subDirs[0]).ToList();
            Assert.AreEqual(1, subDirs.Count);
            Assert.AreEqual("\\cache\\files", subDirs[0].FullName);
            Assert.IsTrue(hybridManager.Exists(subDirs[0]));
            subDirs = hybridManager.ListDirectories(subDirs[0]).ToList();
            Assert.AreEqual(2, subDirs.Count);
            Assert.IsTrue(hybridManager.Exists(new VirtualPath("\\cache\\files\\en-US")));
            Assert.IsTrue(hybridManager.Exists(new VirtualPath("\\cache\\files\\es-mx")));
            subDirs = hybridManager.ListDirectories(new VirtualPath("\\cache\\files\\en-US")).ToList();
            Assert.AreEqual(1, subDirs.Count);
            Assert.AreEqual("\\cache\\files\\en-US\\yo", subDirs[0].FullName);
            subDirs = hybridManager.ListDirectories(new VirtualPath("\\cache\\files\\es-mx")).ToList();
            Assert.AreEqual(1, subDirs.Count);
            Assert.AreEqual("\\cache\\files\\es-mx\\yo", subDirs[0].FullName);
        }

        [TestMethod]
        public void TestFileSystemHybridArbitraryChroot()
        {
            NullFileSystem defaultHandler = NullFileSystem.Singleton;
            InMemoryFileSystem cacheManager = new InMemoryFileSystem();
            HybridFileSystem hybridManager = new HybridFileSystem(defaultHandler);
            hybridManager.AddRoute(new VirtualPath("\\virtual\\path\\to\\cache"), cacheManager, new VirtualPath("\\physical\\cache"));
            cacheManager.AddFile(new VirtualPath("\\physical\\cache\\file1.dat"), new byte[1]);
            Assert.IsTrue(hybridManager.Exists(new VirtualPath("\\virtual\\path\\to\\cache\\file1.dat")));
            var subFiles = hybridManager.ListFiles(new VirtualPath("\\virtual\\path\\to\\cache")).ToList();
            Assert.AreEqual(1, subFiles.Count);
            Assert.AreEqual("\\virtual\\path\\to\\cache\\file1.dat", subFiles[0].FullName);
        }

        [TestMethod]
        public void TestFileSystemHybridArbitraryChroot2()
        {
            NullFileSystem defaultHandler = NullFileSystem.Singleton;
            InMemoryFileSystem cacheManager = new InMemoryFileSystem();
            HybridFileSystem hybridManager = new HybridFileSystem(defaultHandler);
            hybridManager.AddRoute(new VirtualPath("\\virtual\\path\\to"), cacheManager, new VirtualPath("\\physical"));
            cacheManager.AddFile(new VirtualPath("\\physical\\cache\\file1.dat"), new byte[1]);
            Assert.IsTrue(hybridManager.Exists(new VirtualPath("\\virtual\\path\\to\\cache\\file1.dat")));
            var subFiles = hybridManager.ListFiles(new VirtualPath("\\virtual\\path\\to\\cache")).ToList();
            Assert.AreEqual(1, subFiles.Count);
            Assert.AreEqual("\\virtual\\path\\to\\cache\\file1.dat", subFiles[0].FullName);
        }

        [TestMethod]
        public async Task TestFileSystemInMemory()
        {
            InMemoryFileSystem FileSystem = new InMemoryFileSystem();
            await TestFileSystem(FileSystem);
        }

        [TestMethod]
        public async Task TestFileSystemReal()
        {
            ILogger logger = new ConsoleLogger();
            IRandom random = new FastRandom();
            string randomDirectoryName = Path.Combine(Path.GetTempPath(), "durandal" + random.NextInt(0, int.MaxValue));
            logger.Log($"Using random temp directory {randomDirectoryName} for test");
            Directory.CreateDirectory(randomDirectoryName);
            try
            {
                RealFileSystem FileSystem = new RealFileSystem(logger, randomDirectoryName);
                await TestFileSystem(FileSystem);
            }
            finally
            {
                Directory.Delete(randomDirectoryName, true);
            }
        }

        [TestMethod]
        public async Task TestFileSystemHybrid()
        {
            InMemoryFileSystem defaultHandler = new InMemoryFileSystem();
            InMemoryFileSystem cacheManager = new InMemoryFileSystem();
            InMemoryFileSystem modelManager = new InMemoryFileSystem();
            HybridFileSystem hybridManager = new HybridFileSystem(defaultHandler);
            hybridManager.AddRoute(new VirtualPath("cache"), cacheManager);
            hybridManager.AddRoute(new VirtualPath("\\models\\en-US"), modelManager);
            await TestFileSystem(hybridManager, isHybrid: true);
        }

        [TestMethod]
        public async Task TestFileSystemHybridChroot()
        {
            InMemoryFileSystem defaultHandler = new InMemoryFileSystem();
            InMemoryFileSystem cacheManager = new InMemoryFileSystem();
            InMemoryFileSystem modelManager = new InMemoryFileSystem();
            HybridFileSystem hybridManager = new HybridFileSystem(defaultHandler);
            hybridManager.AddRoute(new VirtualPath("cache"), cacheManager, VirtualPath.Root);
            hybridManager.AddRoute(new VirtualPath("\\models\\en-US"), modelManager, VirtualPath.Root);
            await TestFileSystem(hybridManager, isHybrid: true);
        }

        [TestMethod]
        public async Task TestFileSystemZipFile()
        {
            ILogger logger = new ConsoleLogger();
            IRandom random = new FastRandom();
            string randomZipFileName = "temp" + random.NextInt(0, int.MaxValue).ToString() + ".zip";
            try
            {
                RealFileSystem tempFileSystem = new RealFileSystem(logger);
                using (ZipFileFileSystem zipFileSystem = new ZipFileFileSystem(logger, tempFileSystem, new VirtualPath(randomZipFileName)))
                {
                    await TestFileSystem(zipFileSystem, isZip: true);
                }
            }
            finally
            {
                if (System.IO.File.Exists(randomZipFileName))
                {
                    System.IO.File.Delete(randomZipFileName);
                }
            }
        }

        [TestMethod]
        public async Task TestFileSystemRemotedJson()
        {
            await TestFileSystemRemoted(new JsonRemoteDialogProtocol());
        }

        [TestMethod]
        public async Task TestFileSystemRemotedBond()
        {
            await TestFileSystemRemoted(new BondRemoteDialogProtocol());
        }

        private async Task TestFileSystemRemoted(IRemoteDialogProtocol remoteProtocol)
        {
            InMemoryFileSystem actualFileSystem = new InMemoryFileSystem();
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            ILogger testLogger = new DebugLogger("Main"/*, LogLevel.All*/);

            CancellationTokenSource testFinished = new CancellationTokenSource();
            testFinished.CancelAfter(TimeSpan.FromSeconds(30));
            Task serverThread = DurandalTaskExtensions.NoOpTask;
            RemoteDialogMethodDispatcher clientDispatcher = null;

            try
            {
                // Create a socket pair and a post office for each end
                DirectSocketPair socketPair = DirectSocket.CreateSocketPair();
                ushort mailboxId = 0;

                IRealTimeProvider serverTime = realTime.Fork("FileServerTime");
                serverThread = Task.Run(async () =>
                {
                    try
                    {
                        using (PostOffice serverPostOffice = new PostOffice(socketPair.ServerSocket, testLogger, TimeSpan.FromSeconds(30), isServer: true, realTime: serverTime))
                        {
                            MailboxId serverMailbox = serverPostOffice.CreatePermanentMailbox(realTime, mailboxId);
                            RemoteProcedureRequestOrchestrator serverRemotedServiceOrchestrator = new RemoteProcedureRequestOrchestrator(
                                remoteProtocol,
                                new WeakPointer<PostOffice>(serverPostOffice),
                                testLogger,
                                new FileSystemRemoteProcedureRequestHandler(actualFileSystem, testLogger.Clone("FileSystemRemoteHandler")));

                            while (!testFinished.IsCancellationRequested)
                            {
                                RetrieveResult<MailboxMessage> message = await serverPostOffice.TryReceiveMessage(
                                    serverMailbox,
                                    testFinished.Token,
                                    TimeSpan.FromMinutes(1),
                                    serverTime);

                                if (message.Success)
                                {
                                    Tuple<object, Type> parsedMessage = remoteProtocol.Parse(message.Result.Buffer, testLogger);
                                    BufferPool<byte>.Shred();
                                    await serverRemotedServiceOrchestrator.HandleIncomingMessage(
                                        parsedMessage,
                                        message.Result,
                                        testFinished.Token,
                                        serverTime);
                                }
                            }
                        }
                    }
                    finally
                    {
                        serverTime.Merge();
                    }
                });

                using (PostOffice clientPostOffice = new PostOffice(socketPair.ClientSocket, testLogger, TimeSpan.FromSeconds(30), isServer: false, realTime: realTime))
                {
                    MailboxId clientMailbox = clientPostOffice.CreatePermanentMailbox(realTime, mailboxId);
                    clientDispatcher = new RemoteDialogMethodDispatcher(clientPostOffice, clientMailbox, testLogger, remoteProtocol);
                    RemotedFileSystem remotedFileSystem = new RemotedFileSystem(new WeakPointer<RemoteDialogMethodDispatcher>(clientDispatcher), testLogger.Clone("RemoteFS"), realTime);
                    await TestFileSystem(remotedFileSystem);
                    Assert.IsFalse(testFinished.IsCancellationRequested, "Test ran too long and was canceled");
                }
            }
            finally
            {
                testFinished.Cancel();
                clientDispatcher?.Stop();
                await serverThread;
            }
        }

        [TestMethod]
        public async Task TestRemoteFileSystemConcurrency()
        {
            IRemoteDialogProtocol remoteProtocol = new BondRemoteDialogProtocol();
            InMemoryFileSystem actualFileSystem = new InMemoryFileSystem();
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            ILogger testLogger = new DebugLogger("Main", LogLevel.All);

            using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
            {
                CancellationToken testFinished = cts.Token;
                Task serverThread = DurandalTaskExtensions.NoOpTask;

                // Create a socket pair and a post office for each end
                DirectSocketPair socketPair = DirectSocket.CreateSocketPair();
                IRealTimeProvider serverTime = realTime.Fork("FileServerTime");
                ushort mailboxId = 0;

                serverThread = Task.Run(async () =>
                {
                    try
                    {
                        using (PostOffice serverPostOffice = new PostOffice(socketPair.ServerSocket, testLogger, TimeSpan.FromSeconds(1), isServer: true, realTime: serverTime))
                        {
                            MailboxId serverMailbox = serverPostOffice.CreatePermanentMailbox(realTime, mailboxId);
                            RemoteProcedureRequestOrchestrator serverRemotedServiceOrchestrator = new RemoteProcedureRequestOrchestrator(
                                remoteProtocol,
                                new WeakPointer<PostOffice>(serverPostOffice),
                                testLogger,
                                new FileSystemRemoteProcedureRequestHandler(actualFileSystem, testLogger.Clone("FileSystemRemoteHandler")));

                            while (!testFinished.IsCancellationRequested)
                            {
                                RetrieveResult<MailboxMessage> message = await serverPostOffice.TryReceiveMessage(
                                    serverMailbox,
                                    testFinished,
                                    TimeSpan.FromMinutes(1),
                                    serverTime);

                                if (message.Success)
                                {
                                    Tuple<object, Type> parsedMessage = remoteProtocol.Parse(message.Result.Buffer, testLogger);
                                    BufferPool<byte>.Shred();
                                    await serverRemotedServiceOrchestrator.HandleIncomingMessage(
                                        parsedMessage,
                                        message.Result,
                                        testFinished,
                                        serverTime);
                                }
                            }
                        }
                    }
                    finally
                    {
                        serverTime.Merge();
                    }
                });

                using (PostOffice clientPostOffice = new PostOffice(socketPair.ClientSocket, testLogger, TimeSpan.FromSeconds(30), isServer: false, realTime: realTime))
                {
                    MailboxId clientMailbox = clientPostOffice.CreatePermanentMailbox(realTime, mailboxId);
                    using (RemoteDialogMethodDispatcher clientDispatcher = new RemoteDialogMethodDispatcher(clientPostOffice, clientMailbox, testLogger, remoteProtocol))
                    {
                        RemotedFileSystem remotedFileSystem = new RemotedFileSystem(new WeakPointer<RemoteDialogMethodDispatcher>(clientDispatcher), testLogger.Clone("RemoteFS"), realTime);

                        IRandom random = new FastRandom();
                        byte[][] files = new byte[6][];
                        files[0] = new byte[110];
                        random.NextBytes(files[0]);
                        files[1] = new byte[120];
                        random.NextBytes(files[1]);
                        files[2] = new byte[130];
                        random.NextBytes(files[2]);
                        files[3] = new byte[140];
                        random.NextBytes(files[3]);
                        files[4] = new byte[150];
                        random.NextBytes(files[4]);
                        files[5] = new byte[160];
                        random.NextBytes(files[5]);

                        actualFileSystem.AddFile(new VirtualPath("1.dat"), files[0]);
                        actualFileSystem.AddFile(new VirtualPath("2.dat"), files[1]);
                        actualFileSystem.AddFile(new VirtualPath("3.dat"), files[2]);
                        actualFileSystem.AddFile(new VirtualPath("4.dat"), files[3]);
                        actualFileSystem.AddFile(new VirtualPath("5.dat"), files[4]);
                        actualFileSystem.AddFile(new VirtualPath("6.dat"), files[5]);

                        try
                        {
                            // Use the special FileStatWorkItem classes so we can carefully control inputs/outputs for each execution without using closures
                            int numWorkItems = 200;
                            testLogger.Log("Creating threads");
                            List<FileStatWorkItem> workItems = new List<FileStatWorkItem>();
                            for (int thread = 0; thread < numWorkItems; thread++)
                            {
                                int targetFile = (thread % 6) + 1;
                                workItems.Add(new FileStatWorkItem(remotedFileSystem, new VirtualPath(targetFile + ".dat")));
                            }

                            testLogger.Log("Starting threads");
                            for (int thread = 0; thread < numWorkItems; thread++)
                            {
                                if ((thread % 10) == 0)
                                {
                                    testLogger.Log("Thread " + thread);
                                }

                                Task x = Task.Run(workItems[thread].Run, testFinished);
                                testFinished.ThrowIfCancellationRequested();
                                serverThread.ThrowIfExceptional();
                            }

                            testLogger.Log("Waiting for threads");
                            // Wait for client threads to finish, aborting early if they take too long
                            for (int thread = 0; thread < numWorkItems; thread++)
                            {
                                if ((thread % 10) == 0)
                                {
                                    testLogger.Log("Thread " + thread);
                                }

                                FileStatWorkItem workItem = workItems[thread];
                                await workItem.WaitForFinish(testFinished).ConfigureAwait(false);
                                testFinished.ThrowIfCancellationRequested();
                                serverThread.ThrowIfExceptional();
                            }

                            testLogger.Log("Asserting");
                            // Assert the return values were correct
                            for (int thread = 0; thread < numWorkItems; thread++)
                            {
                                int targetFile = thread % 6;
                                FileStat stat = workItems[thread].GetReturnVal();
                                Assert.IsNotNull(stat);
                                Assert.AreEqual(files[targetFile].Length, stat.Size);
                            }

                            testLogger.Log("Assertions are done");
                        }
                        catch (OperationCanceledException)
                        {
                            testLogger.Log("The test was aborted because it took too long");
                            throw;
                        }
                        catch (Exception e)
                        {
                            testLogger.Log(e);
                            throw;
                        }

                        testLogger.Log("Disposing of dispatcher");
                    }

                    testLogger.Log("Awaiting server thread");
                    cts.Cancel();
                }

                await serverThread;
            }

            testLogger.Log("Done");
        }

        private class FileStatWorkItem
        {
            private readonly IFileSystem _fileSystem;
            private readonly VirtualPath _path;
            private readonly ManualResetEventAsync _finished;
            private ExceptionDispatchInfo _exception = null;
            private FileStat _returnVal;
            
            public FileStatWorkItem(IFileSystem fileSystem, VirtualPath path)
            {
                _fileSystem = fileSystem;
                _path = path;
                _finished = new ManualResetEventAsync(false);
            }

            public async Task Run()
            {
                try
                {
                    Task<FileStat> fakeTask = _fileSystem.StatAsync(VirtualPath.Root);
                    Task<FileStat> realTask = _fileSystem.StatAsync(_path);

                    await fakeTask.ConfigureAwait(false);
                    _returnVal = await realTask.ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    _exception = ExceptionDispatchInfo.Capture(e);
                }
                finally
                {
                    _finished.Set();
                }
            }

            public async Task WaitForFinish(CancellationToken cancelToken)
            {
                await _finished.WaitAsync(cancelToken).ConfigureAwait(false);
                _exception?.Throw();
            }

            public FileStat GetReturnVal()
            {
                _exception?.Throw();
                return _returnVal;
            }
        }


        /// <summary>
        /// Runs tests against a newly initialized, empty file system
        /// </summary>
        /// <param name="FileSystem">The object to test</param>
        /// <param name="isHybrid">If true, assume this is a hybrid file system and certain directories cannot be changed</param>
        private async Task TestFileSystem(IFileSystem FileSystem, bool isHybrid = false, bool isZip = false)
        {
            IRandom random = new FastRandom();
            byte[] file1 = new byte[110];
            random.NextBytes(file1);
            byte[] file2 = new byte[61723 * 7];
            random.NextBytes(file2);
            byte[] file3 = new byte[130];
            random.NextBytes(file3);
            byte[] file4 = new byte[140];
            random.NextBytes(file4);
            byte[] file5 = new byte[150];
            random.NextBytes(file5);
            byte[] file6 = new byte[160];
            random.NextBytes(file6);

            // Iterate through the root and make sure it's empty
            Assert.IsTrue(FileSystem.Exists(VirtualPath.Root));
            Assert.IsTrue(await FileSystem.ExistsAsync(VirtualPath.Root));
            Assert.AreEqual(ResourceType.Directory, FileSystem.WhatIs(VirtualPath.Root));
            Assert.AreEqual(ResourceType.Directory, await FileSystem.WhatIsAsync(VirtualPath.Root));

            if (!isHybrid)
            {
                Assert.AreEqual(0, FileSystem.ListDirectories(VirtualPath.Root).ToList().Count);
                Assert.AreEqual(0, (await FileSystem.ListDirectoriesAsync(VirtualPath.Root)).ToList().Count);
                Assert.AreEqual(0, FileSystem.ListFiles(VirtualPath.Root).ToList().Count);
                Assert.AreEqual(0, (await FileSystem.ListFilesAsync(VirtualPath.Root)).ToList().Count);
            }

            // Write files and read them back
            WriteFile(new VirtualPath("1.dat"), FileSystem, file1);
            WriteFile(new VirtualPath("data\\2.dat"), FileSystem, file2);
            WriteFile(new VirtualPath("\\cache\\3.dat"), FileSystem, file3);
            WriteFile(new VirtualPath("/cache\\en-US/4.dat"), FileSystem, file4);
            WriteFile(new VirtualPath("models\\en-US\\5.dat"), FileSystem, file5);
            WriteFile(new VirtualPath("\\cache/6.dat"), FileSystem, file6);

            BufferPool<byte>.Shred();
            AssertArraysEqual(file1, ReadFile(new VirtualPath("1.dat"), FileSystem));
            AssertArraysEqual(file2, ReadFile(new VirtualPath("data\\2.dat"), FileSystem));
            AssertArraysEqual(file3, ReadFile(new VirtualPath("cache/3.dat"), FileSystem));
            AssertArraysEqual(file4, ReadFile(new VirtualPath("cache\\en-US\\4.dat"), FileSystem));
            AssertArraysEqual(file5, ReadFile(new VirtualPath("/models\\en-US/5.dat"), FileSystem));
            AssertArraysEqual(file6, ReadFile(new VirtualPath("cache\\6.dat"), FileSystem));

            // Assert that the directory structure looks right
            List<VirtualPath> resourceList = FileSystem.ListDirectories(VirtualPath.Root).ToList();
            Assert.AreEqual(3, resourceList.Count);
            Assert.IsTrue(resourceList.Contains(new VirtualPath("\\data")));
            Assert.IsTrue(resourceList.Contains(new VirtualPath("\\cache")));
            Assert.IsTrue(resourceList.Contains(new VirtualPath("\\models")));

            resourceList = FileSystem.ListDirectories(new VirtualPath("\\data")).ToList();
            Assert.AreEqual(0, resourceList.Count);
            resourceList = FileSystem.ListFiles(new VirtualPath("\\data")).ToList();
            Assert.AreEqual(1, resourceList.Count);
            Assert.IsTrue(resourceList.Contains(new VirtualPath("\\data\\2.dat")));

            resourceList = FileSystem.ListDirectories(new VirtualPath("\\cache")).ToList();
            Assert.AreEqual(1, resourceList.Count);
            Assert.IsTrue(resourceList.Contains(new VirtualPath("\\cache\\en-US")));
            resourceList = FileSystem.ListFiles(new VirtualPath("\\cache")).ToList();
            Assert.AreEqual(2, resourceList.Count);
            Assert.IsTrue(resourceList.Contains(new VirtualPath("\\cache\\3.dat")));
            Assert.IsTrue(resourceList.Contains(new VirtualPath("\\cache\\6.dat")));

            resourceList = FileSystem.ListDirectories(new VirtualPath("\\cache\\en-US")).ToList();
            Assert.AreEqual(0, resourceList.Count);
            resourceList = FileSystem.ListFiles(new VirtualPath("\\cache\\en-US")).ToList();
            Assert.AreEqual(1, resourceList.Count);
            Assert.IsTrue(resourceList.Contains(new VirtualPath("\\cache\\en-US\\4.dat")));

            resourceList = FileSystem.ListDirectories(new VirtualPath("\\models\\en-US")).ToList();
            Assert.AreEqual(0, resourceList.Count);
            resourceList = FileSystem.ListFiles(new VirtualPath("\\models\\en-US")).ToList();
            Assert.AreEqual(1, resourceList.Count);
            Assert.IsTrue(resourceList.Contains(new VirtualPath("\\models\\en-US\\5.dat")));

            // Listing files or directories using a file path should return an empty enumerable
            resourceList = FileSystem.ListDirectories(new VirtualPath("\\models\\en-US\\5.dat")).ToList();
            Assert.AreEqual(0, resourceList.Count);
            resourceList = FileSystem.ListFiles(new VirtualPath("\\models\\en-US\\5.dat")).ToList();
            Assert.AreEqual(0, resourceList.Count);

            // Write and read file stat
            DateTimeOffset newCreateTime = new DateTimeOffset(1992, 10, 31, 18, 10, 10, TimeSpan.Zero);
            DateTimeOffset newModifyTime = new DateTimeOffset(1993, 10, 31, 21, 13, 15, TimeSpan.Zero);
            FileSystem.WriteStat(new VirtualPath("\\cache\\3.dat"), newCreateTime, newModifyTime);
            FileStat stats = FileSystem.Stat(new VirtualPath("\\cache\\3.dat"));
            Assert.IsTrue(Math.Abs((newCreateTime - stats.CreationTime).TotalSeconds) < 5);
            Assert.IsTrue(Math.Abs((newModifyTime - stats.LastWriteTime).TotalSeconds) < 5);

            // Delete all the files we created
            Assert.IsTrue(FileSystem.Delete(new VirtualPath("1.dat")));
            Assert.IsTrue(FileSystem.Delete(new VirtualPath("\\data\\2.dat")));
            Assert.IsTrue(FileSystem.Delete(new VirtualPath("cache/3.dat")));
            Assert.IsFalse(FileSystem.Delete(new VirtualPath("cache/3.dat")));
            Assert.IsTrue(FileSystem.Delete(new VirtualPath("cache\\en-US\\4.dat")));
            Assert.IsTrue(FileSystem.Delete(new VirtualPath("\\models\\en-US\\5.dat")));
            Assert.IsTrue(FileSystem.Delete(new VirtualPath("cache\\6.dat")));
            Assert.IsFalse(FileSystem.Delete(new VirtualPath("cache\\6.dat")));

            Assert.AreEqual(!isZip, FileSystem.Delete(new VirtualPath("cache\\en-US")));
            Assert.IsFalse (FileSystem.Delete(new VirtualPath("cache\\en-US")));
            Assert.AreEqual(!isHybrid && !isZip, FileSystem.Delete(new VirtualPath("models\\en-US")));
            Assert.AreEqual(!isHybrid && !isZip, FileSystem.Delete(new VirtualPath("cache")));
            Assert.AreEqual(!isHybrid && !isZip, FileSystem.Delete(new VirtualPath("models")));
            Assert.IsFalse(FileSystem.Delete(new VirtualPath("models")));
            Assert.AreEqual(!isZip, FileSystem.Delete(new VirtualPath("data")));

            if (!isHybrid)
            {
                Assert.AreEqual(0, FileSystem.ListDirectories(VirtualPath.Root).ToList().Count);
                Assert.AreEqual(0, (await FileSystem.ListDirectoriesAsync(VirtualPath.Root)).ToList().Count);
                Assert.AreEqual(0, FileSystem.ListFiles(VirtualPath.Root).ToList().Count);
                Assert.AreEqual(0, (await FileSystem.ListFilesAsync(VirtualPath.Root)).ToList().Count);
            }
        }

        [TestMethod]
        public async Task TestFileSystemStreamsInMemory()
        {
            InMemoryFileSystem FileSystem = new InMemoryFileSystem();
            await TestFileSystemFileStreams(FileSystem, CancellationToken.None);
        }

        [TestMethod]
        public async Task TestFileSystemStreamsReal()
        {
            ILogger logger = new ConsoleLogger();
            IRandom random = new FastRandom();
            string randomDirectoryName = Path.Combine(Path.GetTempPath(), "durandal" + random.NextInt(0, int.MaxValue));
            logger.Log($"Using random temp directory {randomDirectoryName} for test");
            Directory.CreateDirectory(randomDirectoryName);
            try
            {
                RealFileSystem FileSystem = new RealFileSystem(logger, randomDirectoryName);
                await TestFileSystemFileStreams(FileSystem, CancellationToken.None);
            }
            finally
            {
                Directory.Delete(randomDirectoryName, true);
            }
        }

        [TestMethod]
        public async Task TestFileSystemStreamsRemotedJson()
        {
            await TestFileSystemRemotedStreams(new JsonRemoteDialogProtocol());
        }

        [TestMethod]
        public async Task TestFileSystemStreamsRemotedBond()
        {
            await TestFileSystemRemotedStreams(new BondRemoteDialogProtocol());
        }

        private async Task TestFileSystemRemotedStreams(IRemoteDialogProtocol remoteProtocol)
        {
            InMemoryFileSystem actualFileSystem = new InMemoryFileSystem();
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            ILogger testLogger = new DebugLogger("Main"/*, LogLevel.All*/);

            CancellationTokenSource testFinished = new CancellationTokenSource();
            testFinished.CancelAfter(TimeSpan.FromSeconds(30));
            Task serverThread = DurandalTaskExtensions.NoOpTask;
            RemoteDialogMethodDispatcher clientDispatcher = null;

            try
            {
                // Create a socket pair and a post office for each end
                DirectSocketPair socketPair = DirectSocket.CreateSocketPair();
                ushort mailboxId = 0;

                IRealTimeProvider serverTime = realTime.Fork("FileServerTime");
                serverThread = Task.Run(async () =>
                {
                    try
                    {
                        using (PostOffice serverPostOffice = new PostOffice(socketPair.ServerSocket, testLogger, TimeSpan.FromSeconds(30), isServer: true, realTime: serverTime))
                        {
                            MailboxId serverMailbox = serverPostOffice.CreatePermanentMailbox(realTime, mailboxId);
                            RemoteProcedureRequestOrchestrator serverRemotedServiceOrchestrator = new RemoteProcedureRequestOrchestrator(
                                remoteProtocol,
                                new WeakPointer<PostOffice>(serverPostOffice),
                                testLogger,
                                new FileSystemRemoteProcedureRequestHandler(actualFileSystem, testLogger.Clone("FileSystemRemoteHandler")));

                            while (!testFinished.IsCancellationRequested)
                            {
                                RetrieveResult<MailboxMessage> message = await serverPostOffice.TryReceiveMessage(
                                    serverMailbox,
                                    testFinished.Token,
                                    TimeSpan.FromMinutes(1),
                                    serverTime);

                                if (message.Success)
                                {
                                    Tuple<object, Type> parsedMessage = remoteProtocol.Parse(message.Result.Buffer, testLogger);
                                    BufferPool<byte>.Shred();
                                    await serverRemotedServiceOrchestrator.HandleIncomingMessage(
                                        parsedMessage,
                                        message.Result,
                                        testFinished.Token,
                                        serverTime);
                                }
                            }
                        }
                    }
                    finally
                    {
                        serverTime.Merge();
                    }
                });

                using (PostOffice clientPostOffice = new PostOffice(socketPair.ClientSocket, testLogger, TimeSpan.FromSeconds(30), isServer: false, realTime: realTime))
                {
                    MailboxId clientMailbox = clientPostOffice.CreatePermanentMailbox(realTime, mailboxId);
                    clientDispatcher = new RemoteDialogMethodDispatcher(clientPostOffice, clientMailbox, testLogger, remoteProtocol);
                    RemotedFileSystem remotedFileSystem = new RemotedFileSystem(new WeakPointer<RemoteDialogMethodDispatcher>(clientDispatcher), testLogger.Clone("RemoteFS"), realTime);
                    await TestFileSystemFileStreams(remotedFileSystem, testFinished.Token);
                    Assert.IsFalse(testFinished.IsCancellationRequested, "Test ran too long and was canceled");
                }
            }
            finally
            {
                testFinished.Cancel();
                clientDispatcher?.Stop();
                await serverThread;
            }
        }

        private async Task TestFileSystemFileStreams(IFileSystem fileSystem, CancellationToken testCancel)
        {
            IRandom rand = new FastRandom(199051);
            byte[] block = new byte[65536];

            // Test file open cases
            // - Basic creating a new file
            using (Stream stream = fileSystem.OpenStream(new VirtualPath("file.dat"), FileOpenMode.CreateNew, FileAccessMode.ReadWrite))
            {
                Assert.IsNotNull(stream);
            }

            // - Overwriting that file with a new stream
            using (Stream stream = fileSystem.OpenStream(new VirtualPath("file.dat"), FileOpenMode.Create, FileAccessMode.ReadWrite))
            {
                Assert.IsNotNull(stream);
                rand.NextBytes(block);
                stream.Write(block.AsSpan());
            }

            // - Opening existing wih OpenOrCreate
            using (Stream stream = fileSystem.OpenStream(new VirtualPath("file.dat"), FileOpenMode.OpenOrCreate, FileAccessMode.ReadWrite))
            {
                Assert.IsNotNull(stream);
                // Ensure that it opened the previously created file
                Assert.AreNotEqual(0, stream.Length);
            }

            // - Opening a file that doesn't exist
            await TestAssert.ExceptionThrown<Exception>(() => fileSystem.OpenStreamAsync(new VirtualPath("notexist.dat"), FileOpenMode.Open, FileAccessMode.Read));

            // - CreateNew on a file that does exist
            await TestAssert.ExceptionThrown<Exception>(() => fileSystem.OpenStreamAsync(new VirtualPath("file.dat"), FileOpenMode.CreateNew, FileAccessMode.ReadWrite));

            // - Creating a new file in read-only mode
            await TestAssert.ExceptionThrown<Exception>(() => fileSystem.OpenStreamAsync(new VirtualPath("somethingelse.dat"), FileOpenMode.CreateNew, FileAccessMode.Read));

            // Open a file in different modes and assert that the stream matches our expected capabilities
            // - Read-only mode
            using (Stream stream = fileSystem.OpenStream(new VirtualPath("file.dat"), FileOpenMode.Open, FileAccessMode.Read))
            {
                Assert.IsTrue(stream.CanRead);
                Assert.IsFalse(stream.CanWrite);
            }

            // - Write-only mode
            using (Stream stream = fileSystem.OpenStream(new VirtualPath("file.dat"), FileOpenMode.Open, FileAccessMode.Write))
            {
                Assert.IsFalse(stream.CanRead);
                Assert.IsTrue(stream.CanWrite);
            }

            // - Read+write mode
            using (Stream stream = fileSystem.OpenStream(new VirtualPath("file.dat"), FileOpenMode.Open, FileAccessMode.ReadWrite))
            {
                Assert.IsTrue(stream.CanRead);
                Assert.IsTrue(stream.CanWrite);
            }

            // Write some data to a file and close it
            using (Stream stream = fileSystem.OpenStream(new VirtualPath("file.dat"), FileOpenMode.Open, FileAccessMode.Write))
            {
                rand.NextBytes(block);
                await stream.WriteAsync(block, 0, block.Length, testCancel).ConfigureAwait(false);
                Assert.AreEqual(block.Length, stream.Position);
                Assert.AreEqual(block.Length, stream.Length);
            }

            // Read back that same data
            using (Stream stream = fileSystem.OpenStream(new VirtualPath("file.dat"), FileOpenMode.Open, FileAccessMode.Read))
            {
                byte[] readData = new byte[block.Length];
                Assert.AreEqual(block.Length, stream.Length);
                await stream.ReadExactlyAsync(readData, 0, block.Length, testCancel).ConfigureAwait(false);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(block, 0, readData, 0, block.Length));
            }

            using (Stream stream = fileSystem.OpenStream(new VirtualPath("file.dat"), FileOpenMode.Open, FileAccessMode.ReadWrite))
            {
                // Write over the file, seek to middle, then overwrite half of it
                rand.NextBytes(block);
                await stream.WriteAsync(block, 0, block.Length, testCancel).ConfigureAwait(false);
                Assert.AreEqual(block.Length, stream.Position);
                Assert.AreEqual(block.Length, stream.Length);

                int rewriteRegionLength = block.Length / 2;
                stream.Seek(0 - rewriteRegionLength, SeekOrigin.End);
                rand.NextBytes(block, block.Length - rewriteRegionLength, rewriteRegionLength);
                await stream.WriteAsync(block, block.Length - rewriteRegionLength, rewriteRegionLength, testCancel).ConfigureAwait(false);
                Assert.AreEqual(block.Length, stream.Position);
                Assert.AreEqual(block.Length, stream.Length);

                // Then seek to the beginning and read the whole file
                byte[] readData = new byte[block.Length];
                stream.Seek(0, SeekOrigin.Begin);
                await stream.ReadExactlyAsync(readData, 0, block.Length, testCancel).ConfigureAwait(false);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(block, 0, readData, 0, block.Length));
            }

            // Do pipelined large writes + reads to a file now
            // This relies on deterministic byte stream generation from this random provider here, to avoid having to allocate a giant block of "expected" data.
            IRandom randomStream = new FastRandom(511298);
            int targetFileSize = 1 * 1024 * 1024; // 1 MB
            using (Stream stream = fileSystem.OpenStream(new VirtualPath("large.dat"), FileOpenMode.CreateNew, FileAccessMode.Write))
            {
                int bytesWritten = 0;
                const int blockSize = 1024;
                while (bytesWritten < targetFileSize)
                {
                    int thisSize = Math.Min(blockSize, targetFileSize - bytesWritten);
                    randomStream.NextBytes(block, 0, thisSize);
                    await stream.WriteAsync(block, 0, thisSize, testCancel).ConfigureAwait(false);
                    bytesWritten += thisSize;
                }
            }

            randomStream = new FastRandom(511298);
            using (Stream stream = fileSystem.OpenStream(new VirtualPath("large.dat"), FileOpenMode.Open, FileAccessMode.Read))
            {
                int bytesRead = 0;
                const int blockSize = 1024;
                byte[] readData = new byte[blockSize];
                while (bytesRead < targetFileSize)
                {
                    int thisSize = Math.Min(blockSize, targetFileSize - bytesRead);
                    int bytesActuallyRead = await stream.ReadAsync(readData, 0, thisSize, testCancel).ConfigureAwait(false);
                    Assert.AreNotEqual(0, bytesActuallyRead);
                    randomStream.NextBytes(block, 0, bytesActuallyRead);
                    Assert.IsTrue(ArrayExtensions.ArrayEquals(block, 0, readData, 0, bytesActuallyRead));
                    bytesRead += bytesActuallyRead;
                }
            }

            // Now do random writes and reads to a file and jump all over the place
            byte[] expectedFile = new byte[100000];
            byte[] actualFile = new byte[100000];
            rand.NextBytes(expectedFile);
            expectedFile.AsSpan().CopyTo(actualFile);
            int assumedStreamPosition = 0;
            using (Stream stream = fileSystem.OpenStream(new VirtualPath("randomaccess.dat"), FileOpenMode.Create, FileAccessMode.ReadWrite))
            {
                // Write initial data
                await stream.WriteAsync(expectedFile, 0, expectedFile.Length, testCancel).ConfigureAwait(false);
                stream.Seek(0, SeekOrigin.Begin);

                for (int c = 0; c < 100; c++)
                {
                    if (assumedStreamPosition > expectedFile.Length - 10 || rand.NextBool())
                    {
                        // Seek somewhere
                        assumedStreamPosition = rand.NextInt(0, expectedFile.Length - 10);
                        stream.Seek((long)assumedStreamPosition, SeekOrigin.Begin);
                        Assert.AreEqual(stream.Position, (long)assumedStreamPosition);
                    }
                    
                    if (rand.NextBool())
                    {
                        // Write something
                        int writeLength = rand.NextInt(1, expectedFile.Length - assumedStreamPosition);
                        rand.NextBytes(expectedFile, assumedStreamPosition, writeLength);
                        await stream.WriteAsync(expectedFile, assumedStreamPosition, writeLength, testCancel).ConfigureAwait(false);
                        assumedStreamPosition += writeLength;
                        Assert.AreEqual(stream.Position, (long)assumedStreamPosition);
                    }
                    else
                    {
                        // Read something
                        int readLength = rand.NextInt(1, expectedFile.Length - assumedStreamPosition);
                        await stream.ReadExactlyAsync(actualFile, assumedStreamPosition, readLength, testCancel).ConfigureAwait(false);
                        //Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedFile, expectedStreamPosition, actualFile, expectedStreamPosition, readLength));
                        assumedStreamPosition += readLength;
                        Assert.AreEqual(stream.Position, (long)assumedStreamPosition);
                    }
                }
            }
        }

        [TestMethod]
        public void TestFileSystemInMemorySerialization()
        {
            IRandom rand = new FastRandom();
            byte[] file1 = new byte[110];
            rand.NextBytes(file1);
            byte[] file2 = new byte[120];
            rand.NextBytes(file2);
            byte[] file3 = new byte[61723 * 7];
            rand.NextBytes(file3);
            byte[] file4 = new byte[140];
            rand.NextBytes(file4);

            InMemoryFileSystem fileSystem = new InMemoryFileSystem();
            fileSystem.AddFile(new VirtualPath("file1.dat"), file1);
            fileSystem.AddFile(new VirtualPath("Cache/file2.dat"), file2);
            fileSystem.AddFile(new VirtualPath("Cache/file3.dat"), file3);
            fileSystem.AddFile(new VirtualPath("Cache/Sublevel/EmptyFolder/file4.dat"), file4);

            MemoryStream outStream = new MemoryStream();
            fileSystem.Serialize(outStream, false, false);
            byte[] array = outStream.ToArray();

            MemoryStream inStream = new MemoryStream(array, false);
            InMemoryFileSystem newFileSystem = InMemoryFileSystem.Deserialize(inStream, false);
            AssertArraysEqual(file1, fileSystem.GetFile(new VirtualPath("file1.dat")).ToArray());
            AssertArraysEqual(file2, fileSystem.GetFile(new VirtualPath("Cache/file2.dat")).ToArray());
            AssertArraysEqual(file3, fileSystem.GetFile(new VirtualPath("Cache/file3.dat")).ToArray());
            AssertArraysEqual(file4, fileSystem.GetFile(new VirtualPath("Cache/Sublevel/EmptyFolder/file4.dat")).ToArray());

            // Also test compressed serialization
            outStream = new MemoryStream();
            fileSystem.Serialize(outStream, true, false);
            array = outStream.ToArray();

            inStream = new MemoryStream(array, false);
            newFileSystem = InMemoryFileSystem.Deserialize(inStream, false);
            AssertArraysEqual(file1, fileSystem.GetFile(new VirtualPath("file1.dat")).ToArray());
            AssertArraysEqual(file2, fileSystem.GetFile(new VirtualPath("Cache/file2.dat")).ToArray());
            AssertArraysEqual(file3, fileSystem.GetFile(new VirtualPath("Cache/file3.dat")).ToArray());
            AssertArraysEqual(file4, fileSystem.GetFile(new VirtualPath("Cache/Sublevel/EmptyFolder/file4.dat")).ToArray());
        }

        [TestMethod]
        public async Task TestFileSystemInMemoryChangeWatchers()
        {
            LockStepRealTimeProvider realTime = new LockStepRealTimeProvider(new DebugLogger());
            InMemoryFileSystem fileSystem = new InMemoryFileSystem();
            string[] data = new string[] { "test data" };
            fileSystem.WriteLines(new VirtualPath("one.txt"), data);
            fileSystem.WriteLines(new VirtualPath("two.txt"), data);
            fileSystem.WriteLines(new VirtualPath("\\two.bin"), data);
            fileSystem.WriteLines(new VirtualPath("/dir1/three.txt"), data);
            fileSystem.WriteLines(new VirtualPath("/dir1/three.bin"), data);
            fileSystem.WriteLines(new VirtualPath("/dir2/four.bin"), data);
            fileSystem.WriteLines(new VirtualPath("/dir1/dir3/five.txt"), data);
            fileSystem.WriteLines(new VirtualPath("/dir1/dir3/dir4/six.txt"), data);
            fileSystem.WriteLines(new VirtualPath("/dir1/dir3/dir4/seven.bin"), data);
            fileSystem.WriteLines(new VirtualPath("/dir1/dir3/dir4/eight"), data);
            fileSystem.WriteLines(new VirtualPath("/nine"), data);

            List<IFileSystemWatcher> watchers = new List<IFileSystemWatcher>();
            watchers.Add(await fileSystem.CreateDirectoryWatcher(VirtualPath.Root, string.Empty, true));
            watchers.Add(await fileSystem.CreateDirectoryWatcher(VirtualPath.Root, "*", false));
            watchers.Add(await fileSystem.CreateDirectoryWatcher(VirtualPath.Root, "*.bin", true));
            watchers.Add(await fileSystem.CreateDirectoryWatcher(new VirtualPath("/dir1"), "*.*", false));
            watchers.Add(await fileSystem.CreateDirectoryWatcher(new VirtualPath("/dir1"), "*.txt", true));
            watchers.Add(await fileSystem.CreateDirectoryWatcher(VirtualPath.Root, "/dir1/*.txt", false));
            watchers.Add(await fileSystem.CreateDirectoryWatcher(VirtualPath.Root, "/dir1/*.txt", true));

            List<EventRecorder<FileSystemChangedEventArgs>> eventRecorders = new List<EventRecorder<FileSystemChangedEventArgs>>();
            foreach (IFileSystemWatcher watcher in watchers)
            {
                EventRecorder<FileSystemChangedEventArgs> recorder = new EventRecorder<FileSystemChangedEventArgs>();
                watcher.ChangedEvent.Subscribe(recorder.HandleEventAsync);
                eventRecorders.Add(recorder);
            }

            List<Tuple<VirtualPath, bool[]>> testCases = new List<Tuple<VirtualPath, bool[]>>();
            testCases.Add(new Tuple<VirtualPath, bool[]>(new VirtualPath("one.txt"),                    new bool[] { true,  true,  false, false, false, false, false }));
            testCases.Add(new Tuple<VirtualPath, bool[]>(new VirtualPath("two.txt"),                    new bool[] { true,  true,  false, false, false, false, false }));
            testCases.Add(new Tuple<VirtualPath, bool[]>(new VirtualPath("\\two.bin"),                  new bool[] { true,  true,  true,  false, false, false, false }));
            testCases.Add(new Tuple<VirtualPath, bool[]>(new VirtualPath("/dir1/three.txt"),            new bool[] { true,  false, false, true,  true,  true,  true }));
            testCases.Add(new Tuple<VirtualPath, bool[]>(new VirtualPath("/dir1/three.bin"),            new bool[] { true,  false, true,  true,  false, false, false }));
            testCases.Add(new Tuple<VirtualPath, bool[]>(new VirtualPath("/dir2/four.bin"),             new bool[] { true,  false, true,  false, false, false, false }));
            testCases.Add(new Tuple<VirtualPath, bool[]>(new VirtualPath("/dir1/dir3/five.txt"),        new bool[] { true,  false, false, false, true,  false, false }));
            testCases.Add(new Tuple<VirtualPath, bool[]>(new VirtualPath("/dir1/dir3/dir4/six.txt"),    new bool[] { true,  false, false, false, true,  false, false }));
            testCases.Add(new Tuple<VirtualPath, bool[]>(new VirtualPath("/dir1/dir3/dir4/seven.bin"),  new bool[] { true,  false, true,  false, false, false, false }));
            testCases.Add(new Tuple<VirtualPath, bool[]>(new VirtualPath("/dir1/dir3/dir4/eight"),      new bool[] { true,  false, false, false, false, false, false }));
            testCases.Add(new Tuple<VirtualPath, bool[]>(new VirtualPath("/nine"),                      new bool[] { true,  true,  false, false, false, false, false }));

            foreach (var testCase in testCases)
            {
                fileSystem.WriteLines(testCase.Item1, data, realTime);
                realTime.Step(TimeSpan.FromMilliseconds(5));

                int idx = 0;
                foreach (EventRecorder<FileSystemChangedEventArgs> eventRecorder in eventRecorders)
                {
                    Assert.AreEqual(
                        testCase.Item2[idx],
                        (await eventRecorder.WaitForEvent(
                            CancellationToken.None,
                            realTime,
                            TimeSpan.Zero)).Success,
                        testCase.Item1.FullName + " recorder " + idx);
                    eventRecorder.Reset();
                    idx++;
                }
            }
        }

        private static void AssertArraysEqual(byte[] a, byte[] b)
        {
            Assert.IsNotNull(a);
            Assert.IsNotNull(b);
            Assert.AreEqual(a.Length, b.Length);
            for (int c = 0; c < a.Length; c++)
            {
                Assert.AreEqual(a[c], b[c]);
            }
        }

        private byte[] ReadFile(VirtualPath file, IFileSystem manager)
        {
            if (!manager.Exists(file))
            {
                return BinaryHelpers.EMPTY_BYTE_ARRAY;
            }

            using (MemoryStream stream = new MemoryStream())
            {
                using (Stream readStream = manager.OpenStream(file, FileOpenMode.Open, FileAccessMode.Read))
                {
                    readStream.CopyToPooled(stream);
                    return stream.ToArray();
                }
            }
        }

        private void WriteFile(VirtualPath file, IFileSystem manager, byte[] data)
        {
            using (MemoryStream stream = new MemoryStream(data, false))
            {
                using (Stream writeStream = manager.OpenStream(file, FileOpenMode.Create, FileAccessMode.Write))
                {
                    stream.CopyTo(writeStream);
                }
            }
        }

        private async Task<byte[]> ReadFileAsync(VirtualPath file, IFileSystem manager)
        {
            if (!manager.Exists(file))
            {
                return BinaryHelpers.EMPTY_BYTE_ARRAY;
            }

            using (MemoryStream stream = new MemoryStream())
            {
                using (Stream readStream = await manager.OpenStreamAsync(file, FileOpenMode.Open, FileAccessMode.Read))
                {
                    readStream.CopyTo(stream);
                    return stream.ToArray();
                }
            }
        }

        private async Task WriteFileAsync(VirtualPath file, IFileSystem manager, byte[] data)
        {
            using (MemoryStream stream = new MemoryStream(data, false))
            {
                using (Stream writeStream = await manager.OpenStreamAsync(file, FileOpenMode.Create, FileAccessMode.Write))
                {
                    stream.CopyTo(writeStream);
                }
            }
        }
    }
}
