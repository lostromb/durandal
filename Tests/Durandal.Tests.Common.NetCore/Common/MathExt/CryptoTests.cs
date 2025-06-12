namespace Durandal.Tests.Common.MathExt
{
    using System;
    using System.Text;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Durandal.API;
    using Durandal.Common.Security;
    using Durandal.Common.Logger;
    using Durandal.Common.File;
    using System.Threading.Tasks;
    using Durandal.Common.Time;
    using Durandal.Common.Utils;
    using Durandal.Common.Security.Client;
    using Durandal.Common.Security.Server;
    using Durandal.Common.Security.Login;
    using Durandal.Common.Security.Login.Providers;
    using Durandal.Common.MathExt;
using Durandal.Common.Test;
    using Durandal.Common.Collections;
    using Durandal.Common.IO.Hashing;

    [TestClass]
    public class CryptoTests
    {
        private readonly ClientIdentifier CLIENT_INFO = new ClientIdentifier("User-1234", "Test user", "Client-1234", "Test client");
        private readonly ClientAuthenticationScope[] ALL_SCOPES = new ClientAuthenticationScope[] { ClientAuthenticationScope.Client, ClientAuthenticationScope.User, ClientAuthenticationScope.UserClient };

        ///// <summary>
        ///// Verifies that client/server handshake and basic requests will work
        ///// </summary>
        //[TestMethod]
        //public async Task TestAuthSimpleAuthentication()
        //{
        //    ILogger logger = new ConsoleLogger("Test", LogLevel.All);
        //    IRSADelegates rsa = new StandardRSADelegates();
        //    IRealTimeProvider realTime = new ManualTimeProvider();
            
        //    IClientSideKeyStore clientKeyStore = new InMemoryClientKeyStore();
        //    InMemoryPublicKeyStore serverKeyStore = new InMemoryPublicKeyStore();
        //    await RunClientServerAuthTest(logger, rsa, clientKeyStore, serverKeyStore, realTime);
        //}

        ///// <summary>
        ///// Verifies that the System.Numerics namespace can provide RSA as well
        ///// </summary>
        //[TestMethod]
        //public async Task TestAuthNumericsDelegates()
        //{
        //    ILogger logger = new ConsoleLogger("Test", LogLevel.All);
        //    IRSADelegates rsa = new NumericsRSADelegates();
        //    IRealTimeProvider realTime = new ManualTimeProvider();
            
        //    IClientSideKeyStore clientKeyStore = new InMemoryClientKeyStore();
        //    InMemoryPublicKeyStore serverKeyStore = new InMemoryPublicKeyStore();
        //    await RunClientServerAuthTest(logger, rsa, clientKeyStore, serverKeyStore, realTime);
        //}

        //private async Task RunClientServerAuthTest(ILogger logger, IRSADelegates rsa, IClientSideKeyStore clientKeyStore, InMemoryPublicKeyStore serverKeyStore, IRealTimeProvider realTime)
        //{
        //    foreach (ClientAuthenticationScope scope in ALL_SCOPES)
        //    {
        //        ClientAuthenticator client = new ClientAuthenticator(logger, rsa, clientKeyStore, realTime);
        //        ServerAuthenticator server = new ServerAuthenticator(logger, serverKeyStore, rsa, realTime);
        //        // Generate a new key with the given scope
        //        ClientKeyIdentifier keyId = CLIENT_INFO.GetKeyIdentifier(scope);
        //        client.LoadPrivateKey(CLIENT_INFO, scope, rsa.GenerateRSAKey(512));
        //        await server.RegisterNewClient(CLIENT_INFO, scope, client.GetPublicKey(keyId));
        //        BigInteger challenge = await server.GenerateChallengeToken(keyId);
        //        client.StoreChallengeToken(challenge, keyId);
        //        BigInteger answer = client.DecryptChallengeToken(keyId);
        //        bool verified = await server.VerifyChallengeToken(keyId, answer);
        //        Assert.IsTrue(verified);
        //        BigInteger secret = await server.GenerateSharedSecret(keyId);
        //        client.DecryptSharedSecret(secret, keyId);
        //        serverKeyStore.PromoteClient(keyId);

        //        for (int turn = 0; turn < 10; turn++)
        //        {
        //            RequestToken token = client.GenerateUniqueRequestToken(keyId);
        //            Assert.AreEqual(AuthLevel.Authorized, await server.VerifyRequestToken(keyId, token));
        //        }
        //    }
        //}

        //[TestMethod]
        //public async Task TestAuthStoringClientKeyLocally()
        //{
        //    ILogger logger = new ConsoleLogger("Test", LogLevel.All);
        //    IRSADelegates rsa = new NumericsRSADelegates();
        //    IRealTimeProvider realTime = new ManualTimeProvider();

        //    IClientSideKeyStore clientKeyStore = new InMemoryClientKeyStore();
        //    InMemoryPublicKeyStore serverKeyStore = new InMemoryPublicKeyStore();
        //    ClientAuthenticator tempClient = new ClientAuthenticator(logger, rsa, clientKeyStore, realTime);

        //    foreach (ClientAuthenticationScope scope in new ClientAuthenticationScope[] { ClientAuthenticationScope.Client, ClientAuthenticationScope.User, ClientAuthenticationScope.UserClient })
        //    {
        //        tempClient.LoadPrivateKey(CLIENT_INFO, scope, rsa.GenerateRSAKey(512));
        //        await tempClient.PersistPrivateKey(CLIENT_INFO.GetKeyIdentifier(scope));

        //        for (int c = 0; c < 4; c++)
        //        {
        //            ClientAuthenticator client = new ClientAuthenticator(logger, rsa, clientKeyStore, realTime);
        //            ServerAuthenticator server = new ServerAuthenticator(logger, serverKeyStore, TimeSpan.FromHours(24), rsa, realTime);
        //            await client.LoadPrivateKeyFromLocalStore(CLIENT_INFO, scope);
        //            ClientKeyIdentifier keyId = CLIENT_INFO.GetKeyIdentifier(scope);
        //            await server.RegisterNewClient(CLIENT_INFO, scope, client.GetPublicKey(keyId));
        //            BigInteger challenge = await server.GenerateChallengeToken(keyId);
        //            client.StoreChallengeToken(challenge, keyId);
        //            BigInteger answer = client.DecryptChallengeToken(keyId);
        //            bool verified = await server.VerifyChallengeToken(keyId, answer);
        //            Assert.IsTrue(verified);
        //            BigInteger secret = await server.GenerateSharedSecret(keyId);
        //            client.DecryptSharedSecret(secret, keyId);
        //            serverKeyStore.PromoteClient(keyId);

        //            for (int turn = 0; turn < 10; turn++)
        //            {
        //                RequestToken token = client.GenerateUniqueRequestToken(keyId);
        //                Assert.AreEqual(AuthLevel.Authorized, await server.VerifyRequestToken(keyId, token));
        //            }
        //        }
        //    }
        //}

        /// <summary>
        /// Lower-level tests for numerics delegate
        /// </summary>
        [TestMethod]
        public void TestAuthNumericsDelegates2()
        {
            IRSADelegates compatible = new StandardRSADelegates();
            IRSADelegates numerics = new NumericsRSADelegates();

            for (int t = 0; t < 100; t++)
            {
                PrivateKey pri = numerics.GenerateRSAKey(32);
                PublicKey pub = pri.GetPublicKey();

                BigInteger a;
                BigInteger b;

                for (int c = 0; c < 50; c++)
                {
                    BigInteger token = CryptographyHelpers.GenerateRandomToken(pri.N, 32);

                    a = compatible.Encrypt(token, pri);
                    b = numerics.Encrypt(token, pri);
                    Assert.AreEqual(a, b);
                    a = compatible.Decrypt(a, pub);
                    b = numerics.Decrypt(b, pub);
                    Assert.AreEqual(a, b);
                }
            }
        }

        ///// <summary>
        ///// Verifies that the client can choose to generate a larger than default key and it will be honored by the server
        ///// </summary>
        //[TestMethod]
        //public async Task TestAuthLargerKeySizes()
        //{
        //    ILogger logger = new ConsoleLogger("Test", LogLevel.All);
        //    IRSADelegates rsa = new StandardRSADelegates();
        //    IRealTimeProvider realTime = new ManualTimeProvider();

        //    IClientSideKeyStore clientKeyStore = new InMemoryClientKeyStore();
        //    InMemoryPublicKeyStore serverKeyStore = new InMemoryPublicKeyStore();
        //    ClientAuthenticationScope scope = ClientAuthenticationScope.UserClient;
        //    ClientAuthenticator client = new ClientAuthenticator(logger, rsa, clientKeyStore, realTime);
        //    ServerAuthenticator server = new ServerAuthenticator(logger, serverKeyStore, TimeSpan.FromHours(24), rsa, realTime);
        //    // Generate a new key with the given scope
        //    ClientKeyIdentifier keyId = CLIENT_INFO.GetKeyIdentifier(scope);
        //    client.LoadPrivateKey(CLIENT_INFO, scope, rsa.GenerateRSAKey(2048));
        //    await server.RegisterNewClient(CLIENT_INFO, scope, client.GetPublicKey(keyId));
        //    BigInteger challenge = await server.GenerateChallengeToken(keyId);
        //    client.StoreChallengeToken(challenge, keyId);
        //    BigInteger answer = client.DecryptChallengeToken(keyId);
        //    bool verified = await server.VerifyChallengeToken(keyId, answer);
        //    Assert.IsTrue(verified);
        //    BigInteger secret = await server.GenerateSharedSecret(keyId);
        //    client.DecryptSharedSecret(secret, keyId);
        //    serverKeyStore.PromoteClient(keyId);

        //    for (int turn = 0; turn < 10; turn++)
        //    {
        //        RequestToken token = client.GenerateUniqueRequestToken(keyId);
        //        Assert.AreEqual(AuthLevel.Authorized, await server.VerifyRequestToken(keyId, token));
        //    }
        //}

        //[TestMethod]
        //public async Task TestAuthAdHocUserClientSecurity()
        //{
        //    ManualTimeProvider realTime = new ManualTimeProvider();
        //    realTime.Time = new DateTimeOffset(2012, 1, 1, 0, 0, 0, TimeSpan.Zero);
        //    ILogger logger = new ConsoleLogger("Test", LogLevel.All);

        //    IClientSideKeyStore clientKeyStore = new InMemoryClientKeyStore();
        //    InMemoryServerKeyStore serverKeyStore = new InMemoryServerKeyStore();
        //    IRSADelegates rsa = new StandardRSADelegates();
        //    IAuthenticationProvider authProvider = new AdhocAuthenticationProvider(rsa);
        //    ClientAuthenticationScope scope = ClientAuthenticationScope.UserClient;
        //    ClientKeyIdentifier keyId = CLIENT_INFO.GetKeyIdentifier(scope);

        //    ClientAuthenticator client = new ClientAuthenticator(logger, rsa, clientKeyStore, realTime);
        //    ServerAuthenticator server = new ServerAuthenticator(logger, serverKeyStore, TimeSpan.FromHours(24), rsa, realTime);

        //    // Generate a new key on client side
        //    PrivateKey adhocKey = PrivateKey.ReadFromXml((await authProvider.GetSecretUserInfo(client, CLIENT_INFO, scope, logger)).PrivateKey);
        //    client.LoadPrivateKey(CLIENT_INFO, scope, adhocKey);
        //    await client.PersistPrivateKey(CLIENT_INFO.GetKeyIdentifier(scope));
            
        //    // Now do the auth handshake and generate a request token
        //    await server.RegisterNewClient(CLIENT_INFO, scope, client.GetPublicKey(keyId));
        //    BigInteger challenge = await server.GenerateChallengeToken(keyId);
        //    Assert.IsFalse(await server.IsClientVerified(keyId));
        //    client.StoreChallengeToken(challenge, keyId);
        //    BigInteger answer = client.DecryptChallengeToken(keyId);
        //    Assert.IsTrue(await server.VerifyChallengeToken(keyId, answer));
        //    BigInteger secret = await server.GenerateSharedSecret(keyId);
        //    client.DecryptSharedSecret(secret, keyId);
        //    serverKeyStore.PromoteClient(keyId);
        //    RequestToken token = client.GenerateUniqueRequestToken(keyId);
        //    Assert.AreEqual(AuthLevel.Authorized, await server.VerifyRequestToken(keyId, token));
        //}

        //[TestMethod]
        //public async Task TestAuthSecretRefreshesWhenNearExpiry()
        //{
        //    ManualTimeProvider realTime = new ManualTimeProvider();
        //    realTime.Time = new DateTimeOffset(2012, 1, 1, 0, 0, 0, TimeSpan.Zero);
        //    ILogger logger = new ConsoleLogger("Test", LogLevel.All);

        //    IClientSideKeyStore clientKeyStore = new InMemoryClientKeyStore();
        //    InMemoryServerKeyStore serverKeyStore = new InMemoryServerKeyStore();
        //    IRSADelegates rsa = new StandardRSADelegates();
        //    IAuthenticationProvider authProvider = new AdhocAuthenticationProvider(rsa);
        //    ClientAuthenticationScope scope = ClientAuthenticationScope.UserClient;
        //    ClientKeyIdentifier keyId = CLIENT_INFO.GetKeyIdentifier(scope);

        //    ClientAuthenticator client = new ClientAuthenticator(logger, rsa, clientKeyStore, realTime);
        //    ServerAuthenticator server = new ServerAuthenticator(logger, serverKeyStore, TimeSpan.FromDays(100), rsa, realTime);

        //    // Generate a new key on client side
        //    PrivateKey adhocKey = PrivateKey.ReadFromXml((await authProvider.GetSecretUserInfo(client, CLIENT_INFO, scope, logger)).PrivateKey);
        //    client.LoadPrivateKey(CLIENT_INFO, scope, adhocKey);
        //    await client.PersistPrivateKey(CLIENT_INFO.GetKeyIdentifier(scope));

        //    // Now do the auth handshake and generate a request token
        //    await server.RegisterNewClient(CLIENT_INFO, scope, client.GetPublicKey(keyId));
        //    BigInteger challenge = await server.GenerateChallengeToken(keyId);
        //    Assert.IsFalse(await server.IsClientVerified(keyId));
        //    client.StoreChallengeToken(challenge, keyId);
        //    BigInteger answer = client.DecryptChallengeToken(keyId);
        //    Assert.IsTrue(await server.VerifyChallengeToken(keyId, answer));
        //    BigInteger secret = await server.GenerateSharedSecret(keyId);
        //    client.DecryptSharedSecret(secret, keyId);
        //    serverKeyStore.PromoteClient(keyId);

        //    // Now fast forward 99 days so the secret is about to expire.
        //    realTime.Time = realTime.Time.AddDays(99);

        //    // Ensure that requests are still valid
        //    RequestToken token = client.GenerateUniqueRequestToken(keyId, TimeSpan.FromSeconds(10));
        //    Assert.AreEqual(AuthLevel.Authorized, await server.VerifyRequestToken(keyId, token));

        //    // Ensure that the auth handshake does two-step
        //    Assert.IsTrue(await server.IsSharedSecretNearExpiry(keyId));
        //    Assert.IsFalse(await server.IsSharedSecretExpired(keyId));
            
        //    challenge = await server.GenerateChallengeToken(keyId);
        //    client.StoreChallengeToken(challenge, keyId);
        //    answer = client.DecryptChallengeToken(keyId);
        //    Assert.IsTrue(await server.VerifyChallengeToken(keyId, answer));
        //    secret = await server.GenerateSharedSecret(keyId);
        //    client.DecryptSharedSecret(secret, keyId);

        //    token = client.GenerateUniqueRequestToken(keyId, TimeSpan.FromSeconds(10));
        //    Assert.AreEqual(AuthLevel.Authorized, await server.VerifyRequestToken(keyId, token));
        //}

        //[TestMethod]
        //public async Task TestAuthAdhocClientNotTrusted()
        //{
        //    ManualTimeProvider realTime = new ManualTimeProvider();
        //    realTime.Time = new DateTimeOffset(2012, 1, 1, 0, 0, 0, TimeSpan.Zero);
        //    ILogger logger = new ConsoleLogger("Test", LogLevel.All);

        //    IClientSideKeyStore clientKeyStore = new InMemoryClientKeyStore();
        //    InMemoryServerKeyStore serverKeyStore = new InMemoryServerKeyStore();
        //    IRSADelegates rsa = new StandardRSADelegates();
        //    IAuthenticationProvider authProvider = new AdhocAuthenticationProvider(rsa);
        //    ClientAuthenticationScope scope = ClientAuthenticationScope.UserClient;
        //    ClientKeyIdentifier keyId = CLIENT_INFO.GetKeyIdentifier(scope);

        //    ClientAuthenticator client = new ClientAuthenticator(logger, rsa, clientKeyStore, realTime);
        //    ServerAuthenticator server = new ServerAuthenticator(logger, serverKeyStore, TimeSpan.FromHours(24), rsa, realTime);

        //    // Generate a new key on client side
        //    PrivateKey adhocKey = PrivateKey.ReadFromXml((await authProvider.GetSecretUserInfo(client, CLIENT_INFO, scope, logger)).PrivateKey);
        //    client.LoadPrivateKey(CLIENT_INFO, scope, adhocKey);
        //    await client.PersistPrivateKey(CLIENT_INFO.GetKeyIdentifier(scope));

        //    // Now do the auth handshake and generate a request token
        //    await server.RegisterNewClient(CLIENT_INFO, scope, client.GetPublicKey(keyId));
        //    BigInteger challenge = await server.GenerateChallengeToken(keyId);
        //    Assert.IsFalse(await server.IsClientVerified(keyId));
        //    client.StoreChallengeToken(challenge, keyId);
        //    BigInteger answer = client.DecryptChallengeToken(keyId);
        //    Assert.IsTrue(await server.VerifyChallengeToken(keyId, answer));
        //    BigInteger secret = await server.GenerateSharedSecret(keyId);
        //    client.DecryptSharedSecret(secret, keyId);
        //    RequestToken token = client.GenerateUniqueRequestToken(keyId);
        //    Assert.AreEqual(AuthLevel.Unverified, await server.VerifyRequestToken(keyId, token));
        //}

        //[TestMethod]
        //public async Task TestAuthRequestTokenExpiration()
        //{
        //    ManualTimeProvider realTime = new ManualTimeProvider();
        //    realTime.Time = new DateTimeOffset(2012, 1, 1, 0, 0, 0, TimeSpan.Zero);
        //    ILogger logger = new ConsoleLogger("Test", LogLevel.All);

        //    IClientSideKeyStore clientKeyStore = new InMemoryClientKeyStore();
        //    InMemoryServerKeyStore serverKeyStore = new InMemoryServerKeyStore();
        //    IRSADelegates rsa = new StandardRSADelegates();
        //    IAuthenticationProvider authProvider = new AdhocAuthenticationProvider(rsa);
        //    ClientAuthenticationScope scope = ClientAuthenticationScope.UserClient;
        //    ClientKeyIdentifier keyId = CLIENT_INFO.GetKeyIdentifier(scope);

        //    ClientAuthenticator client = new ClientAuthenticator(logger, rsa, clientKeyStore, realTime);
        //    ServerAuthenticator server = new ServerAuthenticator(logger, serverKeyStore, TimeSpan.FromHours(24), rsa, realTime);

        //    // Generate a new key on client side
        //    PrivateKey adhocKey = PrivateKey.ReadFromXml((await authProvider.GetSecretUserInfo(client, CLIENT_INFO, scope, logger)).PrivateKey);
        //    client.LoadPrivateKey(CLIENT_INFO, scope, adhocKey);
        //    await client.PersistPrivateKey(CLIENT_INFO.GetKeyIdentifier(scope));

        //    // Now do the auth handshake and generate a request token
        //    await server.RegisterNewClient(CLIENT_INFO, scope, client.GetPublicKey(keyId));
        //    BigInteger challenge = await server.GenerateChallengeToken(keyId);
        //    Assert.IsFalse(await server.IsClientVerified(keyId));
        //    client.StoreChallengeToken(challenge, keyId);
        //    BigInteger answer = client.DecryptChallengeToken(keyId);
        //    Assert.IsTrue(await server.VerifyChallengeToken(keyId, answer));
        //    BigInteger secret = await server.GenerateSharedSecret(keyId);
        //    client.DecryptSharedSecret(secret, keyId);
        //    serverKeyStore.PromoteClient(keyId);
        //    RequestToken token = client.GenerateUniqueRequestToken(keyId, TimeSpan.FromSeconds(10));

        //    // fast-forward!!!!
        //    realTime.Time = realTime.Time.AddSeconds(20);
        //    Assert.AreEqual(AuthLevel.RequestExpired, await server.VerifyRequestToken(keyId, token));
        //}

        ///// <summary>
        ///// Verifies that multiple users can user the same client authenticator
        ///// </summary>
        //[TestMethod]
        //public async Task TestAuthClientMultitenancy()
        //{
        //    ManualTimeProvider realTime = new ManualTimeProvider();
        //    realTime.Time = new DateTimeOffset(2012, 1, 1, 0, 0, 0, TimeSpan.Zero);
        //    ILogger logger = new ConsoleLogger("Test", LogLevel.All);

        //    ClientIdentifier user1 = new ClientIdentifier("USER1", "User Name 1", null, null);
        //    ClientIdentifier user2 = new ClientIdentifier("USER2", "User Name 2", null, null);
        //    ClientAuthenticationScope scope = ClientAuthenticationScope.User;
        //    ClientKeyIdentifier keyId1 = user1.GetKeyIdentifier(scope);
        //    ClientKeyIdentifier keyId2 = user2.GetKeyIdentifier(scope);

        //    InMemoryClientKeyStore clientKeyStore = new InMemoryClientKeyStore();
        //    InMemoryServerKeyStore serverKeyStore = new InMemoryServerKeyStore();
        //    IRSADelegates rsa = new StandardRSADelegates();
        //    IAuthenticationProvider authProvider = new AdhocAuthenticationProvider(rsa);

        //    ClientAuthenticator client = new ClientAuthenticator(logger, rsa, clientKeyStore, realTime);
        //    ServerAuthenticator server = new ServerAuthenticator(logger, serverKeyStore, TimeSpan.FromHours(24), rsa, realTime);

        //    // Generate a new key on client side
        //    PrivateKey adhocKey1 = PrivateKey.ReadFromXml((await authProvider.GetSecretUserInfo(client, user1, scope, logger)).PrivateKey);
        //    PrivateKey adhocKey2 = PrivateKey.ReadFromXml((await authProvider.GetSecretUserInfo(client, user2, scope, logger)).PrivateKey);
        //    client.LoadPrivateKey(user1, scope, adhocKey1);
        //    client.LoadPrivateKey(user2, scope, adhocKey2);

        //    // Try both users and make sure they are both authorized
        //    await server.RegisterNewClient(user1, scope, client.GetPublicKey(keyId1));
        //    BigInteger challenge = await server.GenerateChallengeToken(keyId1);
        //    Assert.IsFalse(await server.IsClientVerified(keyId1));
        //    client.StoreChallengeToken(challenge, keyId1);
        //    BigInteger answer = client.DecryptChallengeToken(keyId1);
        //    Assert.IsTrue(await server.VerifyChallengeToken(keyId1, answer));
        //    BigInteger secret = await server.GenerateSharedSecret(keyId1);
        //    client.DecryptSharedSecret(secret, keyId1);
        //    serverKeyStore.PromoteClient(keyId1);
        //    RequestToken token = client.GenerateUniqueRequestToken(keyId1);
        //    Assert.AreEqual(AuthLevel.Authorized, await server.VerifyRequestToken(keyId1, token));

        //    await server.RegisterNewClient(user2, scope, client.GetPublicKey(keyId2));
        //    challenge = await server.GenerateChallengeToken(keyId2);
        //    Assert.IsFalse(await server.IsClientVerified(keyId2));
        //    client.StoreChallengeToken(challenge, keyId2);
        //    answer = client.DecryptChallengeToken(keyId2);
        //    Assert.IsTrue(await server.VerifyChallengeToken(keyId2, answer));
        //    secret = await server.GenerateSharedSecret(keyId2);
        //    client.DecryptSharedSecret(secret, keyId2);
        //    serverKeyStore.PromoteClient(keyId2);
        //    token = client.GenerateUniqueRequestToken(keyId2);
        //    Assert.AreEqual(AuthLevel.Authorized, await server.VerifyRequestToken(keyId2, token));
        //}

        //[TestMethod]
        //public async Task TestAuthClientHandshakeNotFinished()
        //{
        //    ManualTimeProvider realTime = new ManualTimeProvider();
        //    realTime.Time = new DateTimeOffset(2012, 1, 1, 0, 0, 0, TimeSpan.Zero);
        //    ILogger logger = new ConsoleLogger("Test", LogLevel.All);

        //    IClientSideKeyStore clientKeyStore = new InMemoryClientKeyStore();
        //    InMemoryServerKeyStore serverKeyStore = new InMemoryServerKeyStore();
        //    IRSADelegates rsa = new StandardRSADelegates();
        //    IAuthenticationProvider authProvider = new AdhocAuthenticationProvider(rsa);
        //    ClientAuthenticationScope scope = ClientAuthenticationScope.UserClient;
        //    ClientKeyIdentifier keyId = CLIENT_INFO.GetKeyIdentifier(scope);

        //    ClientAuthenticator client = new ClientAuthenticator(logger, rsa, clientKeyStore, realTime);
        //    ServerAuthenticator server = new ServerAuthenticator(logger, serverKeyStore, TimeSpan.FromHours(24), rsa, realTime);

        //    // Generate a new key on client side
        //    PrivateKey adhocKey = PrivateKey.ReadFromXml((await authProvider.GetSecretUserInfo(client, CLIENT_INFO, scope, logger)).PrivateKey);
        //    client.LoadPrivateKey(CLIENT_INFO, scope, adhocKey);

        //    // Completely unknown client
        //    RequestToken token = client.GenerateUniqueRequestToken(keyId);
        //    Assert.AreEqual(AuthLevel.Unknown, await server.VerifyRequestToken(keyId, token));

        //    // Say hello
        //    await server.RegisterNewClient(CLIENT_INFO, scope, client.GetPublicKey(keyId));

        //    // Client should still be unknown
        //    token = client.GenerateUniqueRequestToken(keyId);
        //    Assert.AreEqual(AuthLevel.Unknown, await server.VerifyRequestToken(keyId, token));

        //    BigInteger challenge = await server.GenerateChallengeToken(keyId);
        //    Assert.IsFalse(await server.IsClientVerified(keyId));
        //    client.StoreChallengeToken(challenge, keyId);

        //    // Client should still be unknown
        //    token = client.GenerateUniqueRequestToken(keyId);
        //    Assert.AreEqual(AuthLevel.Unknown, await server.VerifyRequestToken(keyId, token));

        //    BigInteger answer = client.DecryptChallengeToken(keyId);
        //    Assert.IsTrue(await server.VerifyChallengeToken(keyId, answer));
        //    BigInteger secret = await server.GenerateSharedSecret(keyId);
        //    client.DecryptSharedSecret(secret, keyId);

        //    // Client is now known but unverified
        //    token = client.GenerateUniqueRequestToken(keyId);
        //    Assert.AreEqual(AuthLevel.Unverified, await server.VerifyRequestToken(keyId, token));

        //    // Now client should be verified
        //    serverKeyStore.PromoteClient(keyId);
        //    token = client.GenerateUniqueRequestToken(keyId);
        //    Assert.AreEqual(AuthLevel.Authorized, await server.VerifyRequestToken(keyId, token));
        //}

        [TestMethod]
        public void TestAuthSHA1TestVectors()
        {
            SHA1 hasher = new SHA1();
            Assert.AreEqual("da39a3ee5e6b4b0d3255bfef95601890afd80709", ToHexString(hasher.ComputeHash(ToBytes(string.Empty))));
            hasher.Reset();
            Assert.AreEqual("a9993e364706816aba3e25717850c26c9cd0d89d", ToHexString(hasher.ComputeHash(ToBytes("abc"))));
            hasher.Reset();
            Assert.AreEqual("84983e441c3bd26ebaae4aa1f95129e5e54670f1", ToHexString(hasher.ComputeHash(ToBytes("abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq"))));
            hasher.Reset();
            Assert.AreEqual("a49b2446a02c645bf419f995b67091253a04a259", ToHexString(hasher.ComputeHash(ToBytes("abcdefghbcdefghicdefghijdefghijkefghijklfghijklmghijklmnhijklmnoijklmnopjklmnopqklmnopqrlmnopqrsmnopqrstnopqrstu"))));
        }

        [TestMethod]
        public void TestAuthSHA256TestVectors()
        {
            SHA256 hasher = new SHA256();
            Assert.AreEqual("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", ToHexString(hasher.ComputeHash(ToBytes(string.Empty))));
            hasher.Reset();
            Assert.AreEqual("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", ToHexString(hasher.ComputeHash(ToBytes("abc"))));
            hasher.Reset();
            Assert.AreEqual("248d6a61d20638b8e5c026930c3e6039a33ce45964ff2167f6ecedd419db06c1", ToHexString(hasher.ComputeHash(ToBytes("abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq"))));
            hasher.Reset();
            Assert.AreEqual("cf5b16a778af8380036ce59e7b0492370b249b11e8f07a51afac45037afee9d1", ToHexString(hasher.ComputeHash(ToBytes("abcdefghbcdefghicdefghijdefghijkefghijklfghijklmghijklmnhijklmnoijklmnopjklmnopqklmnopqrlmnopqrsmnopqrstnopqrstu"))));
        }

        private static byte[] ToBytes(string data)
        {
            return Encoding.ASCII.GetBytes(data);
        }

        private static string ToHexString(byte[] data)
        {
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                StringBuilder returnVal = pooledSb.Builder;
                for (int c = 0; c < data.Length; c++)
                {
                    returnVal.AppendFormat("{0:X2}", data[c]);
                }

                return returnVal.ToString().ToLowerInvariant();
            }
        }

        [TestMethod]
        public void TestBigIntegerPadLeft()
        {
            IRandom rand = new FastRandom(363);
            const int padSize = 5;
            for (int c = 0; c < 100000; c++)
            {
                byte[] field = new byte[16];
                rand.NextBytes(field);
                BigInteger firstVal = new BigInteger(field);
                if (rand.NextBool())
                {
                    firstVal = BigInteger.Zero - firstVal;
                }

                byte[] firstValBytes = firstVal.GetBytes();
                field = new byte[firstValBytes.Length + padSize];
                Array.Copy(firstValBytes, 0, field, padSize, firstValBytes.Length);

                int firstNonZeroIdx;
                for (firstNonZeroIdx = 0; firstNonZeroIdx < field.Length && field[firstNonZeroIdx] == 0; firstNonZeroIdx++) ;

                byte[] secondValBytes = new byte[field.Length - firstNonZeroIdx];
                Array.Copy(field, firstNonZeroIdx, secondValBytes, 0, secondValBytes.Length);
                BigInteger secondVal = new BigInteger(secondValBytes);
                Assert.AreEqual(firstVal, secondVal);
            }
        }

        [TestMethod]
        public void TestBinaryHelpersFromHexString()
        {
            IRandom rand = new FastRandom();
            byte[] originalBytes = new byte[32];
            rand.NextBytes(originalBytes);
            string hexString = BinaryHelpers.ToHexString(originalBytes);
            byte[] decodedbytes = BinaryHelpers.FromHexString(hexString);
            Assert.IsTrue(ArrayExtensions.ArrayEquals<byte>(originalBytes, decodedbytes));
        }

        //[TestMethod]
        //public void TestX509CertificateConversion()
        //{
        //    IRandom rand = new FastRandom();
        //    IRSADelegates rsa = new StandardRSADelegates(rand);
        //    PrivateKey key = rsa.GenerateRSAKey(1024);
        //    ClientKeyIdentifier keyId = new ClientKeyIdentifier(ClientAuthenticationScope.User, userId: "msa:12345");
        //    var x509Cert = CertificateHelpers.ConvertDurandalKeyToX509Cert(keyId, key);
        //    PrivateKey convertedPrivateKey = CertificateHelpers.ConvertX509CertificateToDurandalKey(x509Cert);

        //    // To ensure that the RSA key is exactly the same after packing it into a certificate, perform a quick encryption / decryption using both keys
        //    BigInteger testValue = BigInteger.GenPseudoPrime(512, 10, rand);
        //    BigInteger encrypted = rsa.Encrypt(testValue, key);
        //    BigInteger decrypted = rsa.Decrypt(encrypted, convertedPrivateKey);
        //    Assert.AreEqual(testValue.ToHexString(), decrypted.ToHexString());
        //    encrypted = rsa.Encrypt(testValue, convertedPrivateKey);
        //    decrypted = rsa.Decrypt(encrypted, key);
        //    Assert.AreEqual(testValue.ToHexString(), decrypted.ToHexString());
        //}
    }
}
