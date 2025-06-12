using Durandal.API;
using Durandal.Extensions.BondProtocol;
using Durandal.Common.Audio;
using Durandal.Extensions.MySql;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Runtime;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Security;
using Durandal.Common.Security.Login;
using Durandal.Common.Security.Server;
using Durandal.Common.IO;
using Durandal.Common.MathExt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Tests.MySql
{
    [TestClass]
    [DoNotParallelize]
    public class MySqlIntegrationTests
    {
        private static ILogger _testLogger;
        private static MySqlConnectionPool _connectionPool = null;
        private static IRealTimeProvider _realTime = DefaultRealTimeProvider.Singleton;

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            _testLogger = new ConsoleLogger("Main", LogLevel.All);
            string connectionString = context.Properties["MySqlConnectionString"]?.ToString();

            if (!string.IsNullOrEmpty(connectionString))
            {
                _connectionPool = MySqlConnectionPool.Create(connectionString, _testLogger, NullMetricCollector.Singleton, DimensionSet.Empty).Await();
            }
        }

        [ClassCleanup]
        public static void Cleanup()
        {
            _connectionPool?.Dispose();
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestMySqlPublicKeyRepositoryUser()
        {
            if (_connectionPool == null)
            {
                Assert.Inconclusive("No MySQL connection string provided in test settings");
            }

            MySqlPublicKeyStore publicKeyStore = new MySqlPublicKeyStore(_connectionPool, _testLogger);
            await publicKeyStore.Initialize();
            
            ClientKeyIdentifier keyId = new ClientKeyIdentifier(ClientAuthenticationScope.User, userId: "testuser");
            IRSADelegates rsa = new StandardRSADelegates();
            PrivateKey privateKey = rsa.GenerateRSAKey(128);

            // Delete anything that may be there already, for test sanitation
            await publicKeyStore.DeleteClientState(keyId);

            try
            {
                // Check fetching a state that doesn't exist
                RetrieveResult<ServerSideAuthenticationState> authStateRetrieve = await publicKeyStore.GetClientState(keyId);
                Assert.IsFalse(authStateRetrieve.Success);

                // Create a new state
                ServerSideAuthenticationState authState = new ServerSideAuthenticationState()
                {
                    ClientInfo = new ClientIdentifier()
                    {
                        UserId = "testuser",
                        UserName = "Test User"
                    },
                    KeyScope = ClientAuthenticationScope.User,
                    PubKey = privateKey.GetPublicKey(),
                    SaltValue = BigInteger.Zero,
                    Trusted = false
                };

                bool writeSuccess = await publicKeyStore.UpdateClientState(authState);
                Assert.IsTrue(writeSuccess);

                // Fetch it back and assert equality
                authStateRetrieve = await publicKeyStore.GetClientState(keyId);
                Assert.IsTrue(authStateRetrieve.Success);
                authState = authStateRetrieve.Result;

                Assert.IsNotNull(authState);
                Assert.AreEqual("testuser", authState.ClientInfo.UserId);
                Assert.AreEqual("Test User", authState.ClientInfo.UserName);
                Assert.AreEqual(ClientAuthenticationScope.User, authState.KeyScope);
                Assert.AreEqual(BigInteger.Zero, authState.SaltValue);
                Assert.AreEqual(false, authState.Trusted);

                // Now update it
                BigInteger saltValue = CryptographyHelpers.GenerateRandomToken(privateKey.N, 128);
                authState.Trusted = true;
                authState.SaltValue = saltValue;

                writeSuccess = await publicKeyStore.UpdateClientState(authState);
                Assert.IsTrue(writeSuccess);

                // Assert that the updates took effect
                authStateRetrieve = await publicKeyStore.GetClientState(keyId);
                Assert.IsTrue(authStateRetrieve.Success);
                authState = authStateRetrieve.Result;

                Assert.IsNotNull(authState);
                Assert.AreEqual(true, authState.Trusted);
                Assert.AreEqual(saltValue, authState.SaltValue);
                Assert.AreEqual("testuser", authState.ClientInfo.UserId);
                Assert.AreEqual("Test User", authState.ClientInfo.UserName);
                Assert.AreEqual(ClientAuthenticationScope.User, authState.KeyScope);

                // Delete state
                await publicKeyStore.DeleteClientState(keyId);

                // Assert that it got deleted
                authStateRetrieve = await publicKeyStore.GetClientState(keyId);
                Assert.IsFalse(authStateRetrieve.Success);
            }
            finally
            {
                await publicKeyStore.DeleteClientState(keyId);
            }
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestMySqlPublicKeyRepositoryClient()
        {
            if (_connectionPool == null)
            {
                Assert.Inconclusive("No MySQL connection string provided in test settings");
            }

            MySqlPublicKeyStore publicKeyStore = new MySqlPublicKeyStore(_connectionPool, _testLogger);
            await publicKeyStore.Initialize();

            ClientKeyIdentifier keyId = new ClientKeyIdentifier(ClientAuthenticationScope.Client, clientId: "testclient");
            IRSADelegates rsa = new StandardRSADelegates();
            PrivateKey privateKey = rsa.GenerateRSAKey(128);

            // Delete anything that may be there already, for test sanitation
            await publicKeyStore.DeleteClientState(keyId);

            try
            {
                // Check fetching a state that doesn't exist
                RetrieveResult<ServerSideAuthenticationState> authStateRetrieve = await publicKeyStore.GetClientState(keyId);
                Assert.IsFalse(authStateRetrieve.Success);
                
                // Create a new state
                ServerSideAuthenticationState authState = new ServerSideAuthenticationState()
                {
                    ClientInfo = new ClientIdentifier()
                    {
                        ClientId = "testclient",
                        ClientName = "Test Client"
                    },
                    KeyScope = ClientAuthenticationScope.Client,
                    PubKey = privateKey.GetPublicKey(),
                    SaltValue = BigInteger.Zero,
                    Trusted = false
                };

                bool writeSuccess = await publicKeyStore.UpdateClientState(authState);
                Assert.IsTrue(writeSuccess);

                // Fetch it back and assert equality
                authStateRetrieve = await publicKeyStore.GetClientState(keyId);
                Assert.IsTrue(authStateRetrieve.Success);
                authState = authStateRetrieve.Result;

                Assert.IsNotNull(authState);
                Assert.AreEqual("testclient", authState.ClientInfo.ClientId);
                Assert.AreEqual("Test Client", authState.ClientInfo.ClientName);
                Assert.AreEqual(ClientAuthenticationScope.Client, authState.KeyScope);
                Assert.AreEqual(BigInteger.Zero, authState.SaltValue);
                Assert.AreEqual(false, authState.Trusted);

                // Now update it
                BigInteger saltValue = CryptographyHelpers.GenerateRandomToken(privateKey.N, 128);
                authState.Trusted = true;
                authState.SaltValue = saltValue;

                writeSuccess = await publicKeyStore.UpdateClientState(authState);
                Assert.IsTrue(writeSuccess);

                // Assert that the updates took effect
                authStateRetrieve = await publicKeyStore.GetClientState(keyId);
                Assert.IsTrue(authStateRetrieve.Success);
                authState = authStateRetrieve.Result;

                Assert.IsNotNull(authState);
                Assert.AreEqual(true, authState.Trusted);
                Assert.AreEqual(saltValue, authState.SaltValue);
                Assert.AreEqual("testclient", authState.ClientInfo.ClientId);
                Assert.AreEqual("Test Client", authState.ClientInfo.ClientName);
                Assert.AreEqual(ClientAuthenticationScope.Client, authState.KeyScope);

                // Delete state
                await publicKeyStore.DeleteClientState(keyId);

                // Assert that it got deleted
                authStateRetrieve = await publicKeyStore.GetClientState(keyId);
                Assert.IsFalse(authStateRetrieve.Success);
            }
            finally
            {
                await publicKeyStore.DeleteClientState(keyId);
            }
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestMySqlConversationStateCache()
        {
            if (_connectionPool == null)
            {
                Assert.Inconclusive("No MySQL connection string provided in test settings");
            }

            IByteConverter<SerializedConversationStateStack> converter = new BondByteConverterConversationStateStack(_testLogger);
            MySqlConversationStateCache stateCache = new MySqlConversationStateCache(_connectionPool, converter, _testLogger);
            await stateCache.Initialize();
            string userId = "testuserid";
            string clientId = "testclientid";

            await stateCache.ClearBothStates(userId, clientId, _testLogger, false);

            try
            {
                // Assert that there are no initial states
                RetrieveResult<Stack<ConversationState>> stateFetchResult = await stateCache.TryRetrieveState(userId, clientId, _testLogger, DefaultRealTimeProvider.Singleton);
                Assert.IsFalse(stateFetchResult.Success);

                // Store a roaming state and fetch it
                Stack<ConversationState> stateStack = new Stack<ConversationState>();
                stateStack.Push(
                    ConversationState.Deserialize(
                        new SerializedConversationState()
                        {
                            CurrentPluginDomain = "a",
                            CurrentConversationNode = "func.a",
                            PreviousConversationTurns = new List<RecoResult>(),
                            LastMultiturnState = MultiTurnBehavior.None,
                            ConversationExpireTime = DateTimeOffset.UtcNow.AddHours(1).Ticks
                        },
                        _testLogger));
                bool storeResult = await stateCache.SetRoamingState(userId, stateStack, _testLogger, false);
                Assert.IsTrue(storeResult);

                stateFetchResult = await stateCache.TryRetrieveState(userId, clientId, _testLogger, DefaultRealTimeProvider.Singleton);
                Assert.IsTrue(stateFetchResult.Success);
                stateStack = stateFetchResult.Result;
                Assert.AreEqual(1, stateStack.Count);

                // Now write a client-specific state
                stateStack.Push(
                    ConversationState.Deserialize(
                        new SerializedConversationState()
                        {
                            CurrentPluginDomain = "b",
                            CurrentConversationNode = "func.b",
                            PreviousConversationTurns = new List<RecoResult>(),
                            LastMultiturnState = MultiTurnBehavior.None,
                            ConversationExpireTime = DateTimeOffset.UtcNow.AddHours(1).Ticks
                        },
                        _testLogger));

                storeResult = await stateCache.SetClientSpecificState(userId, clientId, stateStack, _testLogger, false);
                Assert.IsTrue(storeResult);

                // And retrieve it
                stateFetchResult = await stateCache.TryRetrieveState(userId, clientId, _testLogger, DefaultRealTimeProvider.Singleton);
                Assert.IsTrue(stateFetchResult.Success);
                stateStack = stateFetchResult.Result;
                Assert.AreEqual(2, stateStack.Count);

                // Assert the retrieving from a different client will fetch the roaming result
                stateFetchResult = await stateCache.TryRetrieveState(userId, "nonexistentclient", _testLogger, DefaultRealTimeProvider.Singleton);
                Assert.IsTrue(stateFetchResult.Success);
                stateStack = stateFetchResult.Result;
                Assert.AreEqual(1, stateStack.Count);

                // Now delete the roaming state
                bool deleteResult = await stateCache.ClearRoamingState(userId, _testLogger, false);
                Assert.IsTrue(deleteResult);

                stateFetchResult = await stateCache.TryRetrieveState(userId, clientId, _testLogger, DefaultRealTimeProvider.Singleton);
                Assert.IsTrue(stateFetchResult.Success);
                stateStack = stateFetchResult.Result;
                Assert.AreEqual(2, stateStack.Count);
                stateFetchResult = await stateCache.TryRetrieveState(userId, "nonexistentclient", _testLogger, DefaultRealTimeProvider.Singleton);
                Assert.IsFalse(stateFetchResult.Success);

                // And delete client-specific state
                deleteResult = await stateCache.ClearRoamingState(userId, _testLogger, false);
                Assert.IsFalse(deleteResult);
                deleteResult = await stateCache.ClearClientSpecificState(userId, clientId, _testLogger, false);
                Assert.IsTrue(deleteResult);

                stateFetchResult = await stateCache.TryRetrieveState(userId, clientId, _testLogger, DefaultRealTimeProvider.Singleton);
                Assert.IsFalse(stateFetchResult.Success);

                deleteResult = await stateCache.ClearBothStates(userId, clientId, _testLogger, false);
                Assert.IsFalse(deleteResult);

                // Now write back both states and then delete them again
                stateStack = new Stack<ConversationState>();
                stateStack.Push(
                    ConversationState.Deserialize(
                        new SerializedConversationState()
                        {
                            CurrentPluginDomain = "a",
                            CurrentConversationNode = "func.a",
                            PreviousConversationTurns = new List<RecoResult>(),
                            LastMultiturnState = MultiTurnBehavior.None,
                            ConversationExpireTime = DateTimeOffset.UtcNow.AddHours(1).Ticks
                        },
                        _testLogger));
                storeResult = await stateCache.SetRoamingState(userId, stateStack, _testLogger, false);
                Assert.IsTrue(storeResult);
                storeResult = await stateCache.SetClientSpecificState(userId, clientId, stateStack, _testLogger, false);
                Assert.IsTrue(storeResult);
                deleteResult = await stateCache.ClearBothStates(userId, clientId, _testLogger, false);
                Assert.IsTrue(deleteResult);
                stateFetchResult = await stateCache.TryRetrieveState(userId, clientId, _testLogger, DefaultRealTimeProvider.Singleton);
                Assert.IsFalse(stateFetchResult.Success);

                // Also try writing an expired state and asserting that it doesn't get returned
                stateStack = new Stack<ConversationState>();
                stateStack.Push(
                    ConversationState.Deserialize(
                        new SerializedConversationState()
                        {
                            CurrentPluginDomain = "a",
                            CurrentConversationNode = "func.a",
                            PreviousConversationTurns = new List<RecoResult>(),
                            LastMultiturnState = MultiTurnBehavior.None,
                            ConversationExpireTime = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMinutes(5)).Ticks
                        },
                        _testLogger));
                storeResult = await stateCache.SetRoamingState(userId, stateStack, _testLogger, false);
                Assert.IsTrue(storeResult);
                stateFetchResult = await stateCache.TryRetrieveState(userId, clientId, _testLogger, DefaultRealTimeProvider.Singleton);
                Assert.IsFalse(stateFetchResult.Success);
                deleteResult = await stateCache.ClearBothStates(userId, clientId, _testLogger, false);
                Assert.IsTrue(deleteResult);
            }
            finally
            {
                await stateCache.ClearBothStates(userId, clientId, _testLogger, false);
            }
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestMySqlPrivateKeyStoreUser()
        {
            if (_connectionPool == null)
            {
                Assert.Inconclusive("No MySQL connection string provided in test settings");
            }

            IRSADelegates rsa = new StandardRSADelegates();
            PrivateKey privateKey = rsa.GenerateRSAKey(128);

            MySqlPrivateKeyStore stateCache = new MySqlPrivateKeyStore(_connectionPool, _testLogger);
            await stateCache.Initialize();
            ClientKeyIdentifier userId = new ClientKeyIdentifier(ClientAuthenticationScope.User, userId: "testuserid");
            
            await stateCache.DeleteLoggedInUserInfo(userId);

            try
            {
                // Ensure nothing is there initially
                RetrieveResult<PrivateKeyVaultEntry> vaultEntryRetrieve = await stateCache.GetUserInfoById(userId);
                Assert.IsFalse(vaultEntryRetrieve.Success);

                UserClientSecretInfo secretInfo = new UserClientSecretInfo()
                {
                    AuthProvider = "testprovider",
                    UserId = "testuserid",
                    UserFullName = "Test User",
                    UserEmail = "user@test.com",
                    UserGivenName = "Test",
                    UserSurname = "User",
                    PrivateKey = privateKey,
                    SaltValue = BigInteger.One,
                    UserIconPng = new byte[100]
                };

                PrivateKeyVaultEntry vaultEntry = new PrivateKeyVaultEntry()
                {
                    LastLoginTime = DateTimeOffset.UtcNow,
                    LoginInProgress = true,
                    LoginState = "state",
                    VaultEntry = secretInfo
                };

                await stateCache.UpdateLoggedInUserInfo(vaultEntry);
                vaultEntryRetrieve = await stateCache.GetUserInfoById(userId);
                Assert.IsTrue(vaultEntryRetrieve.Success);
                vaultEntry = vaultEntryRetrieve.Result;

                Assert.IsNotNull(vaultEntry);
                Assert.AreEqual("testprovider", vaultEntry.VaultEntry.AuthProvider);
                Assert.AreEqual("testuserid", vaultEntry.VaultEntry.UserId);
                Assert.AreEqual("Test User", vaultEntry.VaultEntry.UserFullName);
                Assert.AreEqual("user@test.com", vaultEntry.VaultEntry.UserEmail);
                Assert.AreEqual("Test", vaultEntry.VaultEntry.UserGivenName);
                Assert.AreEqual("User", vaultEntry.VaultEntry.UserSurname);
                Assert.IsNotNull(vaultEntry.VaultEntry.PrivateKey);
                Assert.AreEqual(BigInteger.One, vaultEntry.VaultEntry.SaltValue);
                Assert.AreEqual(100, vaultEntry.VaultEntry.UserIconPng.Length);
                Assert.AreEqual("state", vaultEntry.LoginState);
                Assert.IsTrue(vaultEntry.LastLoginTime > DateTime.UtcNow - TimeSpan.FromMinutes(5));

                vaultEntryRetrieve = await stateCache.GetUserInfoByStateKey("state");
                Assert.IsTrue(vaultEntryRetrieve.Success);
                vaultEntry = vaultEntryRetrieve.Result;

                Assert.IsNotNull(vaultEntry);
                Assert.AreEqual("testuserid", vaultEntry.VaultEntry.UserId);

                vaultEntry.LoginInProgress = false;
                vaultEntry.LoginState = null;
                vaultEntry.VaultEntry.AuthProvider = "newprovider";
                vaultEntry.VaultEntry.SaltValue = BigInteger.Zero;
                vaultEntry.VaultEntry.UserEmail = "newemail";
                vaultEntry.VaultEntry.UserFullName = "newfullname";
                vaultEntry.VaultEntry.UserGivenName = "newgivenname";
                vaultEntry.VaultEntry.UserSurname = "newsurname";
                vaultEntry.VaultEntry.UserIconPng = new byte[200];

                await stateCache.UpdateLoggedInUserInfo(vaultEntry);
                vaultEntryRetrieve = await stateCache.GetUserInfoById(userId);
                Assert.IsTrue(vaultEntryRetrieve.Success);
                vaultEntry = vaultEntryRetrieve.Result;

                Assert.IsNotNull(vaultEntry);
                Assert.AreEqual("newprovider", vaultEntry.VaultEntry.AuthProvider);
                Assert.AreEqual("testuserid", vaultEntry.VaultEntry.UserId);
                Assert.AreEqual("newfullname", vaultEntry.VaultEntry.UserFullName);
                Assert.AreEqual("newemail", vaultEntry.VaultEntry.UserEmail);
                Assert.AreEqual("newgivenname", vaultEntry.VaultEntry.UserGivenName);
                Assert.AreEqual("newsurname", vaultEntry.VaultEntry.UserSurname);
                Assert.IsNotNull(vaultEntry.VaultEntry.PrivateKey);
                Assert.AreEqual(BigInteger.Zero, vaultEntry.VaultEntry.SaltValue);
                Assert.AreEqual(200, vaultEntry.VaultEntry.UserIconPng.Length);
                Assert.IsNull(vaultEntry.LoginState);

                // Delete the state and ensure it's gone
                await stateCache.DeleteLoggedInUserInfo(userId);
                vaultEntryRetrieve = await stateCache.GetUserInfoById(userId);
                Assert.IsFalse(vaultEntryRetrieve.Success);
            }
            finally
            {
                await stateCache.DeleteLoggedInUserInfo(userId);
            }
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestMySqlPrivateKeyStoreClient()
        {
            if (_connectionPool == null)
            {
                Assert.Inconclusive("No MySQL connection string provided in test settings");
            }

            IRSADelegates rsa = new StandardRSADelegates();
            PrivateKey privateKey = rsa.GenerateRSAKey(128);

            MySqlPrivateKeyStore stateCache = new MySqlPrivateKeyStore(_connectionPool, _testLogger);
            await stateCache.Initialize();
            ClientKeyIdentifier clientId = new ClientKeyIdentifier(ClientAuthenticationScope.Client, clientId: "testclientid");

            await stateCache.DeleteLoggedInUserInfo(clientId);

            try
            {
                // Ensure nothing is there initially
                RetrieveResult<PrivateKeyVaultEntry> vaultEntryRetrieve = await stateCache.GetUserInfoById(clientId);
                Assert.IsFalse(vaultEntryRetrieve.Success);

                UserClientSecretInfo secretInfo = new UserClientSecretInfo()
                {
                    AuthProvider = "testprovider",
                    ClientId = "testclientid",
                    ClientName = "Test Client",
                    PrivateKey = privateKey,
                    SaltValue = BigInteger.One,
                };

                PrivateKeyVaultEntry vaultEntry = new PrivateKeyVaultEntry()
                {
                    LastLoginTime = DateTimeOffset.UtcNow,
                    LoginInProgress = true,
                    LoginState = "state",
                    VaultEntry = secretInfo
                };

                await stateCache.UpdateLoggedInUserInfo(vaultEntry);
                vaultEntryRetrieve = await stateCache.GetUserInfoById(clientId);
                Assert.IsTrue(vaultEntryRetrieve.Success);
                vaultEntry = vaultEntryRetrieve.Result;

                Assert.IsNotNull(vaultEntry);
                Assert.AreEqual("testprovider", vaultEntry.VaultEntry.AuthProvider);
                Assert.AreEqual("testclientid", vaultEntry.VaultEntry.ClientId);
                Assert.AreEqual("Test Client", vaultEntry.VaultEntry.ClientName);
                Assert.IsNotNull(vaultEntry.VaultEntry.PrivateKey);
                Assert.AreEqual(BigInteger.One, vaultEntry.VaultEntry.SaltValue);
                Assert.AreEqual("state", vaultEntry.LoginState);
                Assert.IsTrue(vaultEntry.LastLoginTime > DateTime.UtcNow - TimeSpan.FromMinutes(5));

                vaultEntryRetrieve = await stateCache.GetUserInfoByStateKey("state");
                Assert.IsTrue(vaultEntryRetrieve.Success);
                vaultEntry = vaultEntryRetrieve.Result;

                Assert.IsNotNull(vaultEntry);
                Assert.AreEqual("testclientid", vaultEntry.VaultEntry.ClientId);

                vaultEntry.LoginInProgress = false;
                vaultEntry.LoginState = null;
                vaultEntry.VaultEntry.AuthProvider = "newprovider";
                vaultEntry.VaultEntry.SaltValue = BigInteger.Zero;
                vaultEntry.VaultEntry.ClientName = "newname";

                await stateCache.UpdateLoggedInUserInfo(vaultEntry);
                vaultEntryRetrieve = await stateCache.GetUserInfoById(clientId);
                Assert.IsTrue(vaultEntryRetrieve.Success);
                vaultEntry = vaultEntryRetrieve.Result;

                Assert.IsNotNull(vaultEntry);
                Assert.AreEqual("newprovider", vaultEntry.VaultEntry.AuthProvider);
                Assert.AreEqual("newname", vaultEntry.VaultEntry.ClientName);
                Assert.IsNotNull(vaultEntry.VaultEntry.PrivateKey);
                Assert.AreEqual(BigInteger.Zero, vaultEntry.VaultEntry.SaltValue);
                Assert.IsNull(vaultEntry.LoginState);

                // Delete the state and ensure it's gone
                await stateCache.DeleteLoggedInUserInfo(clientId);
                vaultEntryRetrieve = await stateCache.GetUserInfoById(clientId);
                Assert.IsFalse(vaultEntryRetrieve.Success);
            }
            finally
            {
                await stateCache.DeleteLoggedInUserInfo(clientId);
            }
        }

        //[TestMethod]
        //[TestCategory("ExternalService")]
        //public async Task TestMySqlStreamingAudioCache()
        //{
        //    if (_connectionPool == null)
        //    {
        //        Assert.Inconclusive("No MySQL connection string provided in test settings");
        //    }

        //    MySqlStreamingAudioCache audioCache = new MySqlStreamingAudioCache(_connectionPool, _testLogger, new TaskThreadPool(), NullMetricCollector.Singleton, DimensionSet.Empty, true);

        //    Stopwatch timer = Stopwatch.StartNew();
        //    await audioCache.Initialize();
        //    _testLogger.Log("Initialize " + timer.ElapsedMilliseconds);

        //    string key = "Test-" + Guid.NewGuid().ToString("N");
        //    _testLogger.Log("Using test key " + key);
        //    IRandom random = new FastRandom();

        //    //timer.Restart();
        //    //RetrieveResult<AudioTransportStream> retrieveResult = await audioCache.GetStreamAsync(key);
        //    //_logger.Log("Try get null stream " + timer.ElapsedMilliseconds);
        //    //Assert.IsFalse(retrieveResult.Success);

        //    timer.Restart();
        //    EncodedAudioPassthroughPipe writeStream = new EncodedAudioPassthroughPipe("opus", "packet_size=10");
        //    ILogger queryLogger = _testLogger.Clone("MySqlStreamingAudio").CreateTraceLogger(Guid.NewGuid());
        //    Task storeTask = audioCache.Store(key, writeStream.GetReadPipe(), queryLogger, _realTime);
        //    _testLogger.Log("Start store " + timer.ElapsedMilliseconds);

        //    List<byte[]> allDataList = new List<byte[]>();
        //    timer.Restart();
        //    int bytesWritten = 0;
        //    for (int c = 0; c < 10; c++)
        //    {
        //        byte[] data = new byte[random.NextInt(1, 20000)];
        //        random.NextBytes(data);
        //        writeStream.Write(data, 0, data.Length);
        //        bytesWritten += data.Length;
        //        allDataList.Add(data);
        //        _testLogger.Log("Write chunk " + timer.ElapsedMilliseconds);
        //    }

        //    _testLogger.Log("Write all chunks " + timer.ElapsedMilliseconds);
        //    byte[] allData = new byte[bytesWritten];
        //    int dataIdx = 0;
        //    foreach (byte[] chunk in allDataList)
        //    {
        //        Array.Copy(chunk, 0, allData, dataIdx, chunk.Length);
        //        dataIdx += chunk.Length;
        //    }

        //    timer.Restart();
        //    RetrieveResult<AudioReadPipe> retrieveResult = await audioCache.GetStreamAsync(key, queryLogger, _realTime);
        //    _testLogger.Log("Get read stream " + timer.ElapsedMilliseconds);
        //    Assert.IsTrue(retrieveResult.Success);

        //    AudioReadPipe readStream = retrieveResult.Result;
        //    Assert.AreEqual("opus", readStream.GetCodec());
        //    Assert.AreEqual("packet_size=10", readStream.GetCodecParams());

        //    timer.Restart();
        //    writeStream.CloseWrite();
        //    _testLogger.Log("Close write stream " + timer.ElapsedMilliseconds);

        //    int bytesRead = 0;
        //    byte[] buf = new byte[10000];
        //    timer.Restart();

        //    int thisReadSize = -1;
        //    while (thisReadSize != 0)
        //    {
        //        thisReadSize = readStream.Read(buf, 0, 10000);
        //        if (thisReadSize > 0)
        //        {
        //            // Assert equality of the data that is read, make sure it is not out-of-order
        //            for (int c = 0; c < thisReadSize; c++)
        //            {
        //                Assert.AreEqual(allData[bytesRead + c], buf[c]);
        //            }

        //            bytesRead += thisReadSize;
        //            _testLogger.Log("Read chunk " + timer.ElapsedMilliseconds);
        //        }
        //    }
        //    _testLogger.Log("Read all chunks " + timer.ElapsedMilliseconds);

        //    timer.Restart();
        //    await storeTask;
        //    _testLogger.Log("Close write stream " + timer.ElapsedMilliseconds);

        //    Assert.AreEqual(bytesWritten, bytesRead);
        //}

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestMySqlLogger()
        {
            if (_connectionPool == null)
            {
                Assert.Inconclusive("No MySQL connection string provided in test settings");
            }

            using (IHighPrecisionWaitProvider highPrecisionTimer = new Win32HighPrecisionWaitProvider())
            {
                MySqlLogger logger = new MySqlLogger(_connectionPool, "IntegrationTest", bootstrapLogger: new ConsoleLogger());
                MySqlLogEventSource logReader = new MySqlLogEventSource(_connectionPool, _testLogger);

                await logger.Initialize();
                await logReader.Initialize();

                Guid traceId = Guid.NewGuid();
                for (int c = 0; c < 10; c++)
                {
                    logger.Log("This is an integration test! Std " + c, LogLevel.Std, traceId: traceId, privacyClass: DataPrivacyClassification.EndUserPseudonymousIdentifiers | DataPrivacyClassification.PrivateContent);
                    await highPrecisionTimer.WaitAsync(1, CancellationToken.None);
                    logger.Log("This is an integration test! Err " + c, LogLevel.Err, traceId: traceId, privacyClass: DataPrivacyClassification.SystemMetadata);
                    await highPrecisionTimer.WaitAsync(1, CancellationToken.None);
                }

                FilterCriteria filter = new FilterCriteria()
                {
                    TraceId = traceId
                };

                CancellationTokenSource testKiller = new CancellationTokenSource();
                testKiller.CancelAfter(TimeSpan.FromSeconds(30));

                IList<LogEvent> readBackEvents = null;
                while (readBackEvents == null || readBackEvents.Count < 20)
                {
                    await logger.Flush(testKiller.Token, DefaultRealTimeProvider.Singleton, blocking: true);
                    await highPrecisionTimer.WaitAsync(10, testKiller.Token);
                    if (testKiller.IsCancellationRequested)
                    {
                        Assert.Fail("Spent too much time waiting for logs to come back");
                    }

                    readBackEvents = (await logReader.GetLogEvents(filter)).ToList();
                }

                Assert.AreEqual(20, readBackEvents.Count);
                LogEvent firstEvent = readBackEvents[0];
                Assert.AreEqual("This is an integration test! Std 0", firstEvent.Message);
                Assert.AreEqual(LogLevel.Std, firstEvent.Level);
                Assert.AreEqual(DataPrivacyClassification.EndUserPseudonymousIdentifiers | DataPrivacyClassification.PrivateContent, firstEvent.PrivacyClassification);
                Assert.AreEqual(traceId, firstEvent.TraceId);
                LogEvent secondEvent = readBackEvents[1];
                Assert.AreEqual("This is an integration test! Err 0", secondEvent.Message);
                Assert.AreEqual(LogLevel.Err, secondEvent.Level);
                Assert.AreEqual(DataPrivacyClassification.SystemMetadata, secondEvent.PrivacyClassification);
                Assert.AreEqual(traceId, secondEvent.TraceId);
            }
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestMySqlLoggerEscapeChars()
        {
            if (_connectionPool == null)
            {
                Assert.Inconclusive("No MySQL connection string provided in test settings");
            }

            using (IHighPrecisionWaitProvider highPrecisionTimer = new Win32HighPrecisionWaitProvider())
            {
                MySqlLogger logger = new MySqlLogger(_connectionPool, "IntegrationTest", bootstrapLogger: new ConsoleLogger());
                MySqlLogEventSource logReader = new MySqlLogEventSource(_connectionPool, _testLogger);

                await logger.Initialize();
                await logReader.Initialize();

                Guid traceId = Guid.NewGuid();
                logger.Log("Some message here", traceId: traceId);
                await highPrecisionTimer.WaitAsync(1, CancellationToken.None);
                logger.Log("Slash: \\ Message", traceId: traceId);
                logger.Log("Double Quote: \" Message", traceId: traceId);
                logger.Log("Single Quote: \' Message", traceId: traceId);
                logger.Log("CR: \r Message", traceId: traceId);
                logger.Log("LF: \n Message", traceId: traceId);
                await highPrecisionTimer.WaitAsync(1, CancellationToken.None);
                logger.Log("Some message here", traceId: traceId);

                FilterCriteria filter = new FilterCriteria()
                {
                    TraceId = traceId
                };

                CancellationTokenSource testKiller = new CancellationTokenSource();
                testKiller.CancelAfter(TimeSpan.FromSeconds(30));

                IList<LogEvent> readBackEvents = null;
                while (readBackEvents == null || readBackEvents.Count < 7)
                {
                    await logger.Flush(testKiller.Token, DefaultRealTimeProvider.Singleton, blocking: true);
                    await highPrecisionTimer.WaitAsync(10, testKiller.Token);
                    if (testKiller.IsCancellationRequested)
                    {
                        Assert.Fail("Spent too much time waiting for logs to come back");
                    }

                    readBackEvents = (await logReader.GetLogEvents(filter)).ToList();
                }

                Assert.AreEqual(7, readBackEvents.Count);
                Assert.IsTrue(readBackEvents.Any((s) => string.Equals("Slash: \\ Message", s.Message)));
                Assert.IsTrue(readBackEvents.Any((s) => string.Equals("Double Quote: \" Message", s.Message)));
                Assert.IsTrue(readBackEvents.Any((s) => string.Equals("Single Quote: \' Message", s.Message)));
                Assert.IsTrue(readBackEvents.Any((s) => string.Equals("CR: \r Message", s.Message)));
                Assert.IsTrue(readBackEvents.Any((s) => string.Equals("LF: \n Message", s.Message)));
            }
        }

        //[TestMethod]
        //[TestCategory("ExternalService")]
        //public async Task TestMySqlStreamingAudioCacheInstrumentation()
        //{
        //    if (_connectionPool == null)
        //    {
        //        Assert.Inconclusive("No MySQL connection string provided in test settings");
        //    }

        //    MySqlStreamingAudioCache audioCache = new MySqlStreamingAudioCache(_connectionPool, _testLogger, new TaskThreadPool(), NullMetricCollector.Singleton, DimensionSet.Empty, true);
        //    await audioCache.Initialize();
        //    IRandom random = new FastRandom();

        //    using (IThreadPool threadPool = new CustomThreadPool(_testLogger, NullMetricCollector.Singleton, DimensionSet.Empty, "TestPool", 4))
        //    {
        //        StaticAverage avgWriteStartTime = new StaticAverage();
        //        StaticAverage avgWriteEndTime = new StaticAverage();
        //        StaticAverage avgReadStartTime = new StaticAverage();
        //        StaticAverage avgReadEndTime = new StaticAverage();

        //        for (int testRun = 0; testRun < 10; testRun++)
        //        {
        //            string key = "Test-" + Guid.NewGuid().ToString("N");
        //            _testLogger.Log("Using test key " + key);

        //            // Write task
        //            threadPool.EnqueueUserAsyncWorkItem(async () =>
        //            {
        //                Stopwatch timer = Stopwatch.StartNew();
        //                ILogger queryLogger = _testLogger.CreateTraceLogger(Guid.NewGuid(), "MySqlStreamingAudio");
        //                EncodedAudioPassthroughPipe writeStream = new EncodedAudioPassthroughPipe("opus", "packet_size=10");
        //                Task storeTask = audioCache.Store(key, writeStream.GetReadPipe(), queryLogger, _realTime);
        //                avgWriteStartTime.Add(timer.ElapsedMillisecondsPrecise());

        //                int bytesWritten = 0;
        //                for (int c = 0; c < 10; c++)
        //                {
        //                    byte[] data = new byte[random.NextInt(1, 10000)];
        //                    random.NextBytes(data);
        //                    writeStream.Write(data, 0, data.Length);
        //                    bytesWritten += data.Length;
        //                }

        //                writeStream.CloseWrite();
        //                await storeTask;
        //                timer.Stop();
        //                avgWriteEndTime.Add(timer.ElapsedMillisecondsPrecise());
        //            });

        //            // Read task
        //            threadPool.EnqueueUserAsyncWorkItem(async () =>
        //            {
        //                Stopwatch timer = Stopwatch.StartNew();
        //                ILogger queryLogger = _testLogger.CreateTraceLogger(Guid.NewGuid(), "MySqlStreamingAudio");
        //                RetrieveResult<AudioReadPipe> retrieveResult = await audioCache.GetStreamAsync(key, queryLogger, _realTime);

        //                AudioReadPipe readStream = retrieveResult.Result;
        //                int bytesRead = 0;
        //                byte[] buf = new byte[10000];

        //                int thisReadSize = -1;
        //                while (thisReadSize != 0)
        //                {
        //                    thisReadSize = readStream.Read(buf, 0, 10000);
        //                    if (thisReadSize > 0)
        //                    {
        //                        if (bytesRead == 0)
        //                        {
        //                            avgReadStartTime.Add(timer.ElapsedMillisecondsPrecise());
        //                        }

        //                        bytesRead += thisReadSize;
        //                    }
        //                }

        //                timer.Stop();
        //                avgReadEndTime.Add(timer.ElapsedMillisecondsPrecise());
        //            });

        //            while (threadPool.TotalWorkItems > 0)
        //            {
        //                await Task.Delay(10);
        //            }

        //            if (testRun < 5)
        //            {
        //                avgWriteStartTime.Reset();
        //                avgWriteEndTime.Reset();
        //                avgReadStartTime.Reset();
        //                avgReadEndTime.Reset();
        //            }
        //        }

        //        Console.WriteLine("Average write start time:\t" + avgWriteStartTime.Average + "ms");
        //        Console.WriteLine("Average write end time:  \t" + avgWriteEndTime.Average + "ms");
        //        Console.WriteLine("Average read start time: \t" + avgReadStartTime.Average + "ms");
        //        Console.WriteLine("Average read end time:   \t" + avgReadEndTime.Average + "ms");
        //    }
        //}

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestMySqlCache()
        {
            if (_connectionPool == null)
            {
                Assert.Inconclusive("No MySQL connection string provided in test settings");
            }

            MySqlCache<string> cache = new MySqlCache<string>(
                _connectionPool,
                new StringByteConverter(),
                _testLogger.Clone("MySqlCache"));

            await cache.Initialize();

            RetrieveResult<string> fetchResult = await cache.TryRetrieve("notexist", _testLogger.Clone("MySqlQuery"), null);
            Assert.IsFalse(fetchResult.Success);

            fetchResult = await cache.TryRetrieve("notexist", _testLogger.Clone("MySqlQuery"), _realTime, TimeSpan.FromMilliseconds(1000));
            Assert.IsFalse(fetchResult.Success);
            Assert.IsTrue(fetchResult.LatencyMs > 1000);

            string guid = Guid.NewGuid().ToString("N");
            await cache.Store(guid, "mytestvalue", DateTimeOffset.Now.AddSeconds(10), null, true, _testLogger.Clone("MySqlQuery"), _realTime);

            fetchResult = await cache.TryRetrieve(guid, _testLogger.Clone("MySqlQuery"), _realTime, TimeSpan.FromMilliseconds(5000));
            Assert.IsTrue(fetchResult.Success);
            Assert.AreEqual("mytestvalue", fetchResult.Result);
        }

        [Ignore]
        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestMySqlCacheSlowWithAbsoluteExpireTime()
        {
            if (_connectionPool == null)
            {
                Assert.Inconclusive("No MySQL connection string provided in test settings");
            }

            MySqlCache<string> cache = new MySqlCache<string>(
                _connectionPool,
                new StringByteConverter(),
                _testLogger.Clone("MySqlCache"));

            await cache.Initialize();

            string key = Guid.NewGuid().ToString("N");
            string value = Guid.NewGuid().ToString("N");

            // Set a key to expire in 10 seconds with no TTL
            await cache.Store(key, value, DateTimeOffset.Now.AddSeconds(10), null, true, _testLogger.Clone("MySqlQuery"), _realTime);

            // Wait 5 seconds then fetch it
            await Task.Delay(5000);
            RetrieveResult<string> fetchResult = await cache.TryRetrieve(key, _testLogger.Clone("MySqlQuery"), _realTime, TimeSpan.FromMilliseconds(5000));
            Assert.IsTrue(fetchResult.Success);
            Assert.AreEqual(value, fetchResult.Result);

            // Then 6 more seconds. It should have expired by now.
            await Task.Delay(6000);
            fetchResult = await cache.TryRetrieve(key, _testLogger.Clone("MySqlQuery"), _realTime, TimeSpan.FromMilliseconds(5000));
            Assert.IsFalse(fetchResult.Success);
        }
        
        [Ignore]
        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task TestMySqlCacheSlowWithTTL()
        {
            if (_connectionPool == null)
            {
                Assert.Inconclusive("No MySQL connection string provided in test settings");
            }

            MySqlCache<string> cache = new MySqlCache<string>(
                _connectionPool,
                new StringByteConverter(),
                _testLogger.Clone("MySqlCache"));

            await cache.Initialize();

            string key = Guid.NewGuid().ToString("N");
            string value = Guid.NewGuid().ToString("N");

            // Set a key to expire with a 5 second TTL
            await cache.Store(key, value, null, TimeSpan.FromSeconds(5), true, _testLogger.Clone("MySqlQuery"), _realTime);

            // Keep touching it every 2 seconds. It should stay alive.
            for (int c = 0; c < 8; c++)
            {
                await Task.Delay(2000);
                RetrieveResult<string> fetchResult = await cache.TryRetrieve(key, _testLogger.Clone("MySqlQuery"), _realTime, TimeSpan.FromMilliseconds(5000));
                Assert.IsTrue(fetchResult.Success);
                Assert.AreEqual(value, fetchResult.Result);
            }
        }
    }
}
