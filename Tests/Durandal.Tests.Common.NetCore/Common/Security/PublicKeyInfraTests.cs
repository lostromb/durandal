using Durandal.API;
using Durandal.Common.Security;
using Durandal.Common.Security.Server;
using Durandal.Common.MathExt;
using Durandal.Common.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.Security
{
    [TestClass]
    public class PublicKeyInfraTests
    {
        [TestMethod]
        public async Task TestPublicKeyInMemoryStorageUser()
        {
            InMemoryPublicKeyStore publicKeyStore = new InMemoryPublicKeyStore();

            ClientKeyIdentifier keyId = new ClientKeyIdentifier(ClientAuthenticationScope.User, userId: "testuser");
            IRSADelegates rsa = new StandardRSADelegates();
            PrivateKey privateKey = rsa.GenerateRSAKey(128);
            
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

        [TestMethod]
        public async Task TestPublicKeyInMemoryStorageClient()
        {
            InMemoryPublicKeyStore publicKeyStore = new InMemoryPublicKeyStore();

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
    }
}
