using Durandal.API;
using Durandal.Common.Client;
using Durandal.Common.Config;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Web;
using Durandal.Common.Dialog.Services;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Security;
using Durandal.Common.Security.Client;
using Durandal.Common.Security.Login;
using Durandal.Common.Security.Login.Providers;
using Durandal.Common.Security.OAuth;
using Durandal.Common.Security.Server;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.MathExt;
using Durandal.Tests.Common.Security;
using Durandal.Common.Instrumentation;
using Durandal.Common.Utils;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Tests.Common.Security
{
    [TestClass]
    public class LoginTests
    {
        private static readonly string TEST_PUID = "348423423";

        private static ILogger _logger;
        private static InMemoryPrivateKeyStore _privateKeyVault;
        private static InMemoryPublicKeyStore _publicKeyVault;

        [ClassInitialize]
        public static void InitializeTest(TestContext context)
        {
            _logger = new ConsoleLogger();
            _privateKeyVault = new InMemoryPrivateKeyStore();
            _publicKeyVault = new InMemoryPublicKeyStore();
        }

        [TestCleanup]
        public void CleanupTest()
        {
            _privateKeyVault.Clear();
            _publicKeyVault.ClearAllClients();
        }

        [TestMethod]
        public async Task TestLoginMSAPortable()
        {
            Uri redirectUrl = new Uri("https://localhost/auth/login/oauth/msa-portable");
            FakeMSAServer tokenServer = new FakeMSAServer(_logger);
            FakeMSGraphServer msGraphServer = new FakeMSGraphServer(_logger);
            ManualTimeProvider realTime = new ManualTimeProvider();
            realTime.Time = new DateTimeOffset(2016, 2, 1, 12, 0, 0, TimeSpan.Zero);

            DirectHttpClientFactory serverSideHttpClientFactory = new DirectHttpClientFactory(null,
                new Dictionary<string, IHttpServerDelegate>()
                {
                    { "graph.microsoft.com", msGraphServer },
                    { "login.microsoftonline.com", tokenServer }
                });

            MSAPortableLoginProvider loginProviderServerSide = MSAPortableLoginProvider.BuildForServer(serverSideHttpClientFactory, _logger, "msa-client-id", "msa-client-secret", redirectUrl, _privateKeyVault, _publicKeyVault, null, 512);
            AuthHttpServer callbackServer = new AuthHttpServer(_logger, null, null, NullMetricCollector.WeakSingleton, DimensionSet.Empty, new ILoginProvider[] { loginProviderServerSide });

            DirectHttpClientFactory clientSideHttpClientFactory = new DirectHttpClientFactory(null,
                new Dictionary<string, IHttpServerDelegate>()
                {
                    { "localhost", callbackServer },
                });
            
            MSAPortableLoginProvider loginProviderClientSide = MSAPortableLoginProvider.BuildForClient(clientSideHttpClientFactory, _logger, new Uri("https://localhost/"));

            // Step 1 - Client's UX starts polling for the user secret info
            string state = Guid.NewGuid().ToString("N");
            //CancellationTokenSource cancelToken = new CancellationTokenSource(1000);
            //RetrieveResult<UserClientSecretInfo> secretUserInfoResult = await loginProviderClientSide.GetSecretUserInfo(ClientAuthenticationScope.User, _logger, state, cancelToken.Token);
            //Assert.IsNotNull(secretUserInfoResult);
            //Assert.IsFalse(secretUserInfoResult.Success);

            // Step 2 - User has logged in with MSA, this triggers a callback from MSA to our callback to hand off the auth code
            HttpRequest authcodeCallbackRequest = HttpRequest.CreateOutgoing(redirectUrl.AbsoluteUri + "?code=12345&state=" + state);
            DirectHttpClient httpClient = new DirectHttpClient(callbackServer);
            HttpResponse callbackResponse = await httpClient.SendRequestAsync(authcodeCallbackRequest);
            Assert.IsNotNull(callbackResponse);

            // Step 3 - Client's UX can poll the user secret info again and get valid credentials back, including a newly generated RSA private key
            RetrieveResult<UserClientSecretInfo> secretUserInfoResult = await loginProviderClientSide.GetSecretUserInfo(ClientAuthenticationScope.User, _logger, state, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            Assert.IsNotNull(secretUserInfoResult);
            Assert.IsTrue(secretUserInfoResult.Success);
            UserClientSecretInfo secretUserInfo = secretUserInfoResult.Result;

            Assert.AreEqual("Test User", secretUserInfo.UserFullName);
            Assert.AreEqual("Test", secretUserInfo.UserGivenName);
            Assert.AreEqual("User", secretUserInfo.UserSurname);
            Assert.AreEqual("testuser@outlook.com", secretUserInfo.UserEmail);
            Assert.AreEqual("msa:" + TEST_PUID, secretUserInfo.UserId);
            Assert.IsNotNull(secretUserInfo.PrivateKey);

            RetrieveResult<ServerSideAuthenticationState> publicKeyStateRetrieve = await _publicKeyVault.GetClientState(new ClientKeyIdentifier(ClientAuthenticationScope.User, userId: "msa:" + TEST_PUID));
            Assert.IsTrue(publicKeyStateRetrieve.Success);
            ServerSideAuthenticationState publicKeyState = publicKeyStateRetrieve.Result;
            Assert.IsNotNull(publicKeyState);
            Assert.AreEqual(true, publicKeyState.Trusted);
            Assert.AreEqual(ClientAuthenticationScope.User, publicKeyState.KeyScope);
            Assert.AreEqual(new ClientIdentifier("msa:" + TEST_PUID, "Test User", null, null), publicKeyState.ClientInfo);
            Assert.AreEqual(secretUserInfo.SaltValue, publicKeyState.SaltValue);
        }

        [TestMethod]
        public async Task TestLoginMSAPortableTwoDevices()
        {
            Uri redirectUrl = new Uri("https://localhost/auth/login/oauth/msa-portable");
            FakeMSAServer tokenServer = new FakeMSAServer(_logger);
            FakeMSGraphServer msGraphServer = new FakeMSGraphServer(_logger);
            ManualTimeProvider realTime = new ManualTimeProvider();
            realTime.Time = new DateTimeOffset(2016, 2, 1, 12, 0, 0, TimeSpan.Zero);

            DirectHttpClientFactory serverSideHttpClientFactory = new DirectHttpClientFactory(null,
                new Dictionary<string, IHttpServerDelegate>()
                {
                    { "graph.microsoft.com", msGraphServer },
                    { "login.microsoftonline.com", tokenServer }
                });

            MSAPortableLoginProvider loginProviderServerSide = MSAPortableLoginProvider.BuildForServer(serverSideHttpClientFactory, _logger, "msa-client-id", "msa-client-secret", redirectUrl, _privateKeyVault, _publicKeyVault, null, 512);
            AuthHttpServer callbackServer = new AuthHttpServer(_logger, null, null, NullMetricCollector.WeakSingleton, DimensionSet.Empty, new ILoginProvider[] { loginProviderServerSide });

            DirectHttpClientFactory clientSideHttpClientFactory = new DirectHttpClientFactory(null,
                new Dictionary<string, IHttpServerDelegate>()
                {
                    { "localhost", callbackServer },
                });

            MSAPortableLoginProvider loginProviderClientSide = MSAPortableLoginProvider.BuildForClient(clientSideHttpClientFactory, _logger, new Uri("https://localhost/"));

            // Step 1 - Client's UX starts polling for the user secret info
            string state = Guid.NewGuid().ToString("N");
            //CancellationTokenSource cancelToken = new CancellationTokenSource(1000);
            //RetrieveResult<UserClientSecretInfo> secretUserInfoResult = await loginProviderClientSide.GetSecretUserInfo(ClientAuthenticationScope.User, _logger, state, cancelToken.Token);
            //Assert.IsNotNull(secretUserInfoResult);
            //Assert.IsFalse(secretUserInfoResult.Success);

            // Step 2 - User has logged in with MSA, this triggers a callback from MSA to our callback to hand off the auth code
            HttpRequest authcodeCallbackRequest = HttpRequest.CreateOutgoing(redirectUrl.AbsoluteUri + "?code=12345&state=" + state);
            DirectHttpClient httpClient = new DirectHttpClient(callbackServer);
            HttpResponse callbackResponse = await httpClient.SendRequestAsync(authcodeCallbackRequest);
            Assert.IsNotNull(callbackResponse);

            // Step 3 - Client's UX can poll the user secret info again and get valid credentials back, including a newly generated RSA private key
            RetrieveResult<UserClientSecretInfo> secretUserInfoResultClient1 = await loginProviderClientSide.GetSecretUserInfo(ClientAuthenticationScope.User, _logger, state, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            Assert.IsNotNull(secretUserInfoResultClient1);
            Assert.IsTrue(secretUserInfoResultClient1.Success);
            UserClientSecretInfo secretUserInfoClient1 = secretUserInfoResultClient1.Result;

            Assert.AreEqual("Test User", secretUserInfoClient1.UserFullName);
            Assert.AreEqual("Test", secretUserInfoClient1.UserGivenName);
            Assert.AreEqual("User", secretUserInfoClient1.UserSurname);
            Assert.AreEqual("testuser@outlook.com", secretUserInfoClient1.UserEmail);
            Assert.AreEqual("msa:" + TEST_PUID, secretUserInfoClient1.UserId);
            Assert.IsNotNull(secretUserInfoClient1.PrivateKey);
            Assert.AreNotEqual(BigInteger.Zero, secretUserInfoClient1.SaltValue);

            RetrieveResult<ServerSideAuthenticationState> publicKeyStateRetrieve = await _publicKeyVault.GetClientState(new ClientKeyIdentifier(ClientAuthenticationScope.User, userId: "msa:" + TEST_PUID));
            Assert.IsTrue(publicKeyStateRetrieve.Success);
            ServerSideAuthenticationState publicKeyState = publicKeyStateRetrieve.Result;
            Assert.IsNotNull(publicKeyState);
            Assert.AreEqual(true, publicKeyState.Trusted);
            Assert.AreEqual(ClientAuthenticationScope.User, publicKeyState.KeyScope);
            Assert.AreEqual(new ClientIdentifier("msa:" + TEST_PUID, "Test User", null, null), publicKeyState.ClientInfo);
            Assert.AreEqual(secretUserInfoClient1.SaltValue, publicKeyState.SaltValue);

            // Now do the whole login again for another client
            state = Guid.NewGuid().ToString("N");
            authcodeCallbackRequest = HttpRequest.CreateOutgoing(redirectUrl.AbsoluteUri + "?code=12345&state=" + state);
            callbackResponse = await httpClient.SendRequestAsync(authcodeCallbackRequest);
            Assert.IsNotNull(callbackResponse);

            RetrieveResult<UserClientSecretInfo>  secretUserInfoResultClient2 = await loginProviderClientSide.GetSecretUserInfo(ClientAuthenticationScope.User, _logger, state, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            Assert.IsNotNull(secretUserInfoResultClient2);
            Assert.IsTrue(secretUserInfoResultClient2.Success);
            UserClientSecretInfo secretUserInfoClient2 = secretUserInfoResultClient1.Result;

            Assert.AreEqual(secretUserInfoClient1.UserFullName, secretUserInfoClient2.UserFullName);
            Assert.AreEqual(secretUserInfoClient1.UserGivenName, secretUserInfoClient2.UserGivenName);
            Assert.AreEqual(secretUserInfoClient1.UserSurname, secretUserInfoClient2.UserSurname);
            Assert.AreEqual(secretUserInfoClient1.UserEmail, secretUserInfoClient2.UserEmail);
            Assert.AreEqual(secretUserInfoClient1.UserId, secretUserInfoClient2.UserId);
            Assert.AreEqual(secretUserInfoClient1.PrivateKey.WriteToXml(), secretUserInfoClient2.PrivateKey.WriteToXml());
            Assert.AreEqual(secretUserInfoClient1.SaltValue.ToHexString(), secretUserInfoClient2.SaltValue.ToHexString());

            publicKeyStateRetrieve = await _publicKeyVault.GetClientState(new ClientKeyIdentifier(ClientAuthenticationScope.User, userId: "msa:" + TEST_PUID));
            Assert.IsTrue(publicKeyStateRetrieve.Success);
            publicKeyState = publicKeyStateRetrieve.Result;
            Assert.IsNotNull(publicKeyState);
            Assert.AreEqual(true, publicKeyState.Trusted);
            Assert.AreEqual(ClientAuthenticationScope.User, publicKeyState.KeyScope);
            Assert.AreEqual(new ClientIdentifier("msa:" + TEST_PUID, "Test User", null, null), publicKeyState.ClientInfo);
            Assert.AreEqual(secretUserInfoClient2.SaltValue, publicKeyState.SaltValue);
        }

        [TestMethod]
        public async Task TestLoginMSAPortableInvalidateKeyAfterDuplicateRequest()
        {
            Uri redirectUrl = new Uri("https://localhost/auth/login/oauth/msa-portable");
            FakeMSAServer tokenServer = new FakeMSAServer(_logger);
            FakeMSGraphServer msGraphServer = new FakeMSGraphServer(_logger);
            ManualTimeProvider realTime = new ManualTimeProvider();
            realTime.Time = new DateTimeOffset(2016, 2, 1, 12, 0, 0, TimeSpan.Zero);

            DirectHttpClientFactory serverSideHttpClientFactory = new DirectHttpClientFactory(null,
                new Dictionary<string, IHttpServerDelegate>()
                {
                    { "graph.microsoft.com", msGraphServer },
                    { "login.microsoftonline.com", tokenServer }
                });

            MSAPortableLoginProvider loginProviderServerSide = MSAPortableLoginProvider.BuildForServer(serverSideHttpClientFactory, _logger, "msa-client-id", "msa-client-secret", redirectUrl, _privateKeyVault, _publicKeyVault, null, 512);
            AuthHttpServer callbackServer = new AuthHttpServer(_logger, null, null, NullMetricCollector.WeakSingleton, DimensionSet.Empty, new ILoginProvider[] { loginProviderServerSide });

            DirectHttpClientFactory clientSideHttpClientFactory = new DirectHttpClientFactory(null,
                new Dictionary<string, IHttpServerDelegate>()
                {
                    { "localhost", callbackServer },
                });

            MSAPortableLoginProvider loginProviderClientSide = MSAPortableLoginProvider.BuildForClient(clientSideHttpClientFactory, _logger, new Uri("https://localhost/"));
            
            string state = Guid.NewGuid().ToString("N");
            
            HttpRequest authcodeCallbackRequest = HttpRequest.CreateOutgoing(redirectUrl.AbsoluteUri + "?code=12345&state=" + state);
            DirectHttpClient httpClient = new DirectHttpClient(callbackServer);
            HttpResponse callbackResponse = await httpClient.SendRequestAsync(authcodeCallbackRequest);
            Assert.IsNotNull(callbackResponse);
            
            RetrieveResult<UserClientSecretInfo> secretUserInfoResult = await loginProviderClientSide.GetSecretUserInfo(ClientAuthenticationScope.User, _logger, state, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            Assert.IsNotNull(secretUserInfoResult);
            Assert.IsTrue(secretUserInfoResult.Success);
            UserClientSecretInfo secretUserInfo = secretUserInfoResult.Result;

            Assert.AreEqual("Test User", secretUserInfo.UserFullName);
            Assert.AreEqual("Test", secretUserInfo.UserGivenName);
            Assert.AreEqual("User", secretUserInfo.UserSurname);
            Assert.AreEqual("testuser@outlook.com", secretUserInfo.UserEmail);
            Assert.AreEqual("msa:" + TEST_PUID, secretUserInfo.UserId);
            Assert.IsNotNull(secretUserInfo.PrivateKey);

            RetrieveResult<ServerSideAuthenticationState> publicKeyStateRetrieve = await _publicKeyVault.GetClientState(new ClientKeyIdentifier(ClientAuthenticationScope.User, userId: "msa:" + TEST_PUID));
            Assert.IsTrue(publicKeyStateRetrieve.Success);
            ServerSideAuthenticationState publicKeyState = publicKeyStateRetrieve.Result;
            Assert.IsNotNull(publicKeyState);
            Assert.AreEqual(true, publicKeyState.Trusted);
            Assert.AreEqual(ClientAuthenticationScope.User, publicKeyState.KeyScope);
            Assert.AreEqual(new ClientIdentifier("msa:" + TEST_PUID, "Test User", null, null), publicKeyState.ClientInfo);

            // Now request the same info again and assert that is has been invalidated
            try
            {
                secretUserInfo = await loginProviderServerSide.VerifyExternalToken(ClientAuthenticationScope.User, _logger, state, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                Assert.Fail("Should have thrown an exception");
            }
            catch (Exception) { }
            publicKeyStateRetrieve = await _publicKeyVault.GetClientState(new ClientKeyIdentifier(ClientAuthenticationScope.User, userId: "msa:" + TEST_PUID));
            Assert.IsFalse(publicKeyStateRetrieve.Success);
            RetrieveResult<PrivateKeyVaultEntry> privateKeyState = await _privateKeyVault.GetUserInfoById(new ClientKeyIdentifier(ClientAuthenticationScope.User, userId: "msa:" + TEST_PUID));
            Assert.IsFalse(privateKeyState.Success);
        }

        [TestMethod]
        public async Task TestLoginMSAPortableEndToEnd()
        {
            Uri redirectUrl = new Uri("https://localhost/auth/login/oauth/msa-portable");
            FakeMSAServer tokenServer = new FakeMSAServer(_logger);
            FakeMSGraphServer msGraphServer = new FakeMSGraphServer(_logger);
            LockStepRealTimeProvider realTime = new LockStepRealTimeProvider(_logger.Clone("LockStepTime"));
            CancellationTokenSource testAbort = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            CancellationToken cancelToken = testAbort.Token;

            DirectHttpClientFactory serverSideHttpClientFactory = new DirectHttpClientFactory(null,
                new Dictionary<string, IHttpServerDelegate>()
                {
                    { "graph.microsoft.com", msGraphServer },
                    { "login.microsoftonline.com", tokenServer }
                });

            MSAPortableLoginProvider loginProviderServerSide = MSAPortableLoginProvider.BuildForServer(serverSideHttpClientFactory, _logger, "msa-client-id", "msa-client-secret", redirectUrl, _privateKeyVault, _publicKeyVault, null);
            AuthHttpServer callbackServer = new AuthHttpServer(_logger, null, null, NullMetricCollector.WeakSingleton, DimensionSet.Empty, new ILoginProvider[] { loginProviderServerSide });

            DirectHttpClientFactory clientSideHttpClientFactory = new DirectHttpClientFactory(null,
               new Dictionary<string, IHttpServerDelegate>()
               {
                    { "localhost", callbackServer },
               });

            MSAPortableLoginProvider loginProviderClientSide = MSAPortableLoginProvider.BuildForClient(clientSideHttpClientFactory, _logger, new Uri("https://localhost/"));

            ClientCore clientCore = new ClientCore();
            ClientConfiguration clientConfig = new ClientConfiguration(new InMemoryConfiguration(_logger));
            ClientCoreParameters clientParameters = new ClientCoreParameters(clientConfig, () =>
            {
                ClientContext returnVal = new ClientContext();
                returnVal.Capabilities = ClientCapabilities.DisplayUnlimitedText;
                returnVal.UTCOffset = 0;
                return returnVal;
            });

            clientParameters.LoginProviders = new List<ILoginProvider>() { loginProviderClientSide };
            clientParameters.EnableRSA = true;
            clientParameters.PrivateKeyStore = new InMemoryClientKeyStore();
            clientParameters.DialogConnection = new DialogHttpClient(new NullHttpClient(), _logger, new DialogJsonTransportProtocol());
            clientParameters.RealTimeProvider = realTime;
            _logger.Log("Initializing client");
            await clientCore.Initialize(clientParameters);
            
            // Now it gets complex. Split into 3 threads
            // 1. This is the "auth callback going to the auth server" thread
            string state = Guid.NewGuid().ToString("N");
            IRealTimeProvider serverTime = realTime.Fork("AuthCallback");
            _logger.Log("Starting server task");
            Task serverTask = Task.Run(async () =>
            {
                try
                {
                    await serverTime.WaitAsync(TimeSpan.FromSeconds(2), cancelToken);
                    DirectHttpClient httpClient = new DirectHttpClient(callbackServer);
                    using (HttpRequest authcodeCallbackRequest = HttpRequest.CreateOutgoing(redirectUrl.AbsoluteUri + "?code=12345&state=" + state))
                    using (HttpResponse callbackResponse = await httpClient.SendRequestAsync(authcodeCallbackRequest, cancelToken, serverTime))
                    {
                        Assert.IsNotNull(callbackResponse);
                    }
                }
                finally
                {
                    serverTime.Merge();
                }
            });

            // 2. This is the client registering a new user (which triggers a wait on the callback from thread 1)
            IRealTimeProvider clientTime = realTime.Fork("Client");
            _logger.Log("Starting client task");
            Task clientTask = Task.Run(async () =>
            {
                try
                {
                    UserIdentity clientSideUserIdentity = await clientCore.RegisterNewAuthenticatedUser(loginProviderClientSide.ProviderName, state, cancelToken, clientTime);
                    Assert.IsNotNull(clientSideUserIdentity);
                }
                finally
                {
                    clientTime.Merge();
                }
            });

            // And then thread 3 is driving the lockstep for the test
            _logger.Log("Beginning lockstep");
            while (!clientTask.IsFinished() && !cancelToken.IsCancellationRequested)
            {
                realTime.Step(TimeSpan.FromSeconds(5));
            }

            _logger.Log("Done with lockstep");
            await clientTask;
            await serverTask;
            _logger.Log("All tasks completed");

            RetrieveResult<ServerSideAuthenticationState> publicKeyStateRetrieve = await _publicKeyVault.GetClientState(new ClientKeyIdentifier(ClientAuthenticationScope.User, userId: "msa:" + TEST_PUID));
            Assert.IsTrue(publicKeyStateRetrieve.Success);
            ServerSideAuthenticationState publicKeyState = publicKeyStateRetrieve.Result;
            Assert.IsNotNull(publicKeyState);
            Assert.AreEqual(true, publicKeyState.Trusted);
            Assert.AreEqual(ClientAuthenticationScope.User, publicKeyState.KeyScope);
            Assert.AreEqual(new ClientIdentifier("msa:" + TEST_PUID, "Test User", null, null), publicKeyState.ClientInfo);
        }

        [TestMethod]
        public async Task TestLoginMSAPortableStateExpires()
        {
            Uri redirectUrl = new Uri("https://localhost/auth/login/oauth/msa-portable");
            FakeMSAServer tokenServer = new FakeMSAServer(_logger);
            FakeMSGraphServer msGraphServer = new FakeMSGraphServer(_logger);
            ManualTimeProvider realTime = new ManualTimeProvider();
            realTime.Time = new DateTimeOffset(2016, 2, 1, 12, 0, 0, TimeSpan.Zero);

            DirectHttpClientFactory serverSideHttpClientFactory = new DirectHttpClientFactory(null,
                new Dictionary<string, IHttpServerDelegate>()
                {
                    { "graph.microsoft.com", msGraphServer },
                    { "login.microsoftonline.com", tokenServer }
                });

            MSAPortableLoginProvider loginProviderServerSide = MSAPortableLoginProvider.BuildForServer(serverSideHttpClientFactory, _logger, "msa-client-id", "msa-client-secret", redirectUrl, _privateKeyVault, _publicKeyVault, null, 512);
            AuthHttpServer callbackServer = new AuthHttpServer(_logger, null, null, NullMetricCollector.WeakSingleton, DimensionSet.Empty, new ILoginProvider[] { loginProviderServerSide });

            DirectHttpClientFactory clientSideHttpClientFactory = new DirectHttpClientFactory(null,
                new Dictionary<string, IHttpServerDelegate>()
                {
                    { "localhost", callbackServer },
                });

            MSAPortableLoginProvider loginProviderClientSide = MSAPortableLoginProvider.BuildForClient(clientSideHttpClientFactory, _logger, new Uri("https://localhost/"));

            // Step 1 - Client's UX starts polling for the user secret info
            string state = Guid.NewGuid().ToString("N");
            //CancellationTokenSource cancelToken = new CancellationTokenSource(1000);
            //RetrieveResult<UserClientSecretInfo> secretUserInfoResult = await loginProviderClientSide.GetSecretUserInfo(ClientAuthenticationScope.User, _logger, state, cancelToken.Token);
            //Assert.IsNotNull(secretUserInfoResult);
            //Assert.IsFalse(secretUserInfoResult.Success);

            // Step 2 - User has logged in with MSA, this triggers a callback from MSA to our callback to hand off the auth code
            HttpRequest authcodeCallbackRequest = HttpRequest.CreateOutgoing(redirectUrl.AbsoluteUri + "?code=12345&state=" + state);
            DirectHttpClient httpClient = new DirectHttpClient(callbackServer);
            HttpResponse callbackResponse = await httpClient.SendRequestAsync(authcodeCallbackRequest);
            Assert.IsNotNull(callbackResponse);
            
            // Wait 10 minutes
            realTime.Time = new DateTimeOffset(2016, 2, 1, 12, 10, 0, TimeSpan.Zero);

            // Now do UX poll again. It should throw an exception because the state has expired
            try
            {
                RetrieveResult<UserClientSecretInfo> secretUserInfoResult = await loginProviderClientSide.GetSecretUserInfo(ClientAuthenticationScope.User, _logger, state, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                Assert.Fail("Should have thrown an exception");
            }
            catch (Exception) { }
        }

        [TestMethod]
        public async Task TestLoginAdhocUser()
        {
            ManualTimeProvider realTime = new ManualTimeProvider();
            realTime.Time = new DateTimeOffset(2016, 2, 1, 12, 0, 0, TimeSpan.Zero);

            AdhocLoginProvider loginProviderServerSide = AdhocLoginProvider.BuildForServer(_logger, _privateKeyVault, _publicKeyVault, null, 512);
            AuthHttpServer callbackServer = new AuthHttpServer(_logger, null, null, NullMetricCollector.WeakSingleton, DimensionSet.Empty, new ILoginProvider[] { loginProviderServerSide });

            DirectHttpClientFactory clientSideHttpClientFactory = new DirectHttpClientFactory(null,
                new Dictionary<string, IHttpServerDelegate>()
                {
                    { "localhost", callbackServer },
                });

            AdhocLoginProvider loginProviderClientSide = AdhocLoginProvider.BuildForClient(clientSideHttpClientFactory, _logger, new Uri("https://localhost/"));

            // Step 1 - Client's UX initiates request for secret user info and immediately get valid credentials back, including a newly generated RSA private key
            UserClientSecretInfo adhocInfo = new UserClientSecretInfo()
            {
                UserId = "test-user-id",
                UserEmail = "testuser@outlook.com",
                UserFullName = "Test User",
                UserGivenName = "Test",
                UserSurname = "User"
            };

            string state = JsonConvert.SerializeObject(adhocInfo);
            RetrieveResult<UserClientSecretInfo> secretUserInfoResult = await loginProviderClientSide.GetSecretUserInfo(ClientAuthenticationScope.User, _logger, state, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            Assert.IsNotNull(secretUserInfoResult);
            Assert.IsTrue(secretUserInfoResult.Success);
            UserClientSecretInfo secretUserInfo = secretUserInfoResult.Result;

            Assert.AreEqual("Test User", secretUserInfo.UserFullName);
            Assert.AreEqual("Test", secretUserInfo.UserGivenName);
            Assert.AreEqual("User", secretUserInfo.UserSurname);
            Assert.AreEqual("testuser@outlook.com", secretUserInfo.UserEmail);
            Assert.AreEqual("test-user-id", secretUserInfo.UserId);
            Assert.IsNotNull(secretUserInfo.PrivateKey);
        }

        [TestMethod]
        public async Task TestLoginAdhocClient()
        {
            ManualTimeProvider realTime = new ManualTimeProvider();
            realTime.Time = new DateTimeOffset(2016, 2, 1, 12, 0, 0, TimeSpan.Zero);

            AdhocLoginProvider loginProviderServerSide = AdhocLoginProvider.BuildForServer(_logger, _privateKeyVault, _publicKeyVault, null, 512);
            AuthHttpServer callbackServer = new AuthHttpServer(_logger, null, null, NullMetricCollector.WeakSingleton, DimensionSet.Empty, new ILoginProvider[] { loginProviderServerSide });

            DirectHttpClientFactory clientSideHttpClientFactory = new DirectHttpClientFactory(null,
                new Dictionary<string, IHttpServerDelegate>()
                {
                    { "localhost", callbackServer },
                });

            AdhocLoginProvider loginProviderClientSide = AdhocLoginProvider.BuildForClient(clientSideHttpClientFactory, _logger, new Uri("https://localhost/"));

            // Step 1 - Client's UX initiates request for secret user info and immediately get valid credentials back, including a newly generated RSA private key
            UserClientSecretInfo adhocInfo = new UserClientSecretInfo()
            {
                ClientId = "test-client-id",
                ClientName = "test-client-name"
            };

            string state = JsonConvert.SerializeObject(adhocInfo);
            RetrieveResult<UserClientSecretInfo> secretUserInfoResult = await loginProviderClientSide.GetSecretUserInfo(ClientAuthenticationScope.Client, _logger, state, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            Assert.IsNotNull(secretUserInfoResult);
            Assert.IsTrue(secretUserInfoResult.Success);
            UserClientSecretInfo secretUserInfo = secretUserInfoResult.Result;

            Assert.AreEqual("test-client-id", secretUserInfo.ClientId);
            Assert.AreEqual("test-client-name", secretUserInfo.ClientName);
            Assert.IsNotNull(secretUserInfo.PrivateKey);
        }

        [TestMethod]
        public async Task TestLoginAdhocUserSetsGlobalUserProfileFields()
        {
            ManualTimeProvider realTime = new ManualTimeProvider();
            realTime.Time = new DateTimeOffset(2016, 2, 1, 12, 0, 0, TimeSpan.Zero);

            IUserProfileStorage userProfiles = new InMemoryProfileStorage();

            AdhocLoginProvider loginProviderServerSide = AdhocLoginProvider.BuildForServer(_logger, _privateKeyVault, _publicKeyVault, null, 512);
            AuthHttpServer callbackServer = new AuthHttpServer(_logger, null, null, NullMetricCollector.WeakSingleton, DimensionSet.Empty, new ILoginProvider[] { loginProviderServerSide }, userProfiles);

            DirectHttpClientFactory clientSideHttpClientFactory = new DirectHttpClientFactory(null,
                new Dictionary<string, IHttpServerDelegate>()
                {
                    { "localhost", callbackServer },
                });

            AdhocLoginProvider loginProviderClientSide = AdhocLoginProvider.BuildForClient(clientSideHttpClientFactory, _logger, new Uri("https://localhost/"));
            
            UserClientSecretInfo adhocInfo = new UserClientSecretInfo()
            {
                UserId = "test-user-id",
                UserFullName = "test-user-name",
                UserEmail = "test@email.com",
                UserGivenName = "test",
                UserSurname = "user"
            };

            string state = JsonConvert.SerializeObject(adhocInfo);
            RetrieveResult<UserClientSecretInfo> secretUserInfoResult = await loginProviderClientSide.GetSecretUserInfo(ClientAuthenticationScope.User, _logger, state, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            Assert.IsNotNull(secretUserInfoResult);
            Assert.IsTrue(secretUserInfoResult.Success);

            RetrieveResult<UserProfileCollection> profileRetrieveResult = await userProfiles.GetProfiles(UserProfileType.PluginGlobal, "test-user-id", null, _logger);
            Assert.IsNotNull(profileRetrieveResult);
            Assert.IsTrue(profileRetrieveResult.Success);
            InMemoryDataStore globalProfile = profileRetrieveResult.Result.GlobalProfile;
            Assert.AreEqual("test-user-name", globalProfile.GetString(ClientContextField.UserFullName));
            Assert.AreEqual("test", globalProfile.GetString(ClientContextField.UserGivenName));
            Assert.AreEqual("user", globalProfile.GetString(ClientContextField.UserSurname));
            Assert.AreEqual("test@email.com", globalProfile.GetString(ClientContextField.UserEmail));
        }

        [TestMethod]
        public async Task TestLoginAdhocUserUpdatesGlobalUserProfileFields()
        {
            ManualTimeProvider realTime = new ManualTimeProvider();
            realTime.Time = new DateTimeOffset(2016, 2, 1, 12, 0, 0, TimeSpan.Zero);

            // Create an initial profile
            IUserProfileStorage userProfiles = new InMemoryProfileStorage();
            InMemoryDataStore initialProfile = new InMemoryDataStore();
            initialProfile.Put(ClientContextField.UserFullName, "Mr. Cool ICE");
            initialProfile.Put(ClientContextField.UserNickname, "Ice man");
            UserProfileCollection initialProfileCollection = new UserProfileCollection(null, initialProfile, null);
            await userProfiles.UpdateProfiles(UserProfileType.PluginGlobal, initialProfileCollection, "test-user-id", null, _logger);

            AdhocLoginProvider loginProviderServerSide = AdhocLoginProvider.BuildForServer(_logger, _privateKeyVault, _publicKeyVault, null, 512);
            AuthHttpServer callbackServer = new AuthHttpServer(_logger, null, null, NullMetricCollector.WeakSingleton, DimensionSet.Empty, new ILoginProvider[] { loginProviderServerSide }, userProfiles);

            DirectHttpClientFactory clientSideHttpClientFactory = new DirectHttpClientFactory(null,
                new Dictionary<string, IHttpServerDelegate>()
                {
                    { "localhost", callbackServer },
                });

            AdhocLoginProvider loginProviderClientSide = AdhocLoginProvider.BuildForClient(clientSideHttpClientFactory, _logger, new Uri("https://localhost/"));

            UserClientSecretInfo adhocInfo = new UserClientSecretInfo()
            {
                UserId = "test-user-id",
                UserFullName = "test-user-name",
                UserEmail = "test@email.com",
                UserGivenName = "test",
                UserSurname = "user"
            };

            string state = JsonConvert.SerializeObject(adhocInfo);
            RetrieveResult<UserClientSecretInfo> secretUserInfoResult = await loginProviderClientSide.GetSecretUserInfo(ClientAuthenticationScope.User, _logger, state, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            Assert.IsNotNull(secretUserInfoResult);
            Assert.IsTrue(secretUserInfoResult.Success);

            RetrieveResult<UserProfileCollection> profileRetrieveResult = await userProfiles.GetProfiles(UserProfileType.PluginGlobal, "test-user-id", null, _logger);
            Assert.IsNotNull(profileRetrieveResult);
            Assert.IsTrue(profileRetrieveResult.Success);
            InMemoryDataStore globalProfile = profileRetrieveResult.Result.GlobalProfile;
            Assert.AreEqual("Mr. Cool ICE", globalProfile.GetString(ClientContextField.UserFullName));
            Assert.AreEqual("test", globalProfile.GetString(ClientContextField.UserGivenName));
            Assert.AreEqual("user", globalProfile.GetString(ClientContextField.UserSurname));
            Assert.AreEqual("test@email.com", globalProfile.GetString(ClientContextField.UserEmail));
            Assert.AreEqual("Ice man", globalProfile.GetString(ClientContextField.UserNickname));
        }

        [TestMethod]
        public async Task TestLoginAdhocUserTwoInARow()
        {
            ManualTimeProvider realTime = new ManualTimeProvider();
            realTime.Time = new DateTimeOffset(2016, 2, 1, 12, 0, 0, TimeSpan.Zero);

            AdhocLoginProvider loginProviderServerSide = AdhocLoginProvider.BuildForServer(_logger, _privateKeyVault, _publicKeyVault, null, 512);
            AuthHttpServer callbackServer = new AuthHttpServer(_logger, null, null, NullMetricCollector.WeakSingleton, DimensionSet.Empty, new ILoginProvider[] { loginProviderServerSide });

            DirectHttpClientFactory clientSideHttpClientFactory = new DirectHttpClientFactory(null,
                new Dictionary<string, IHttpServerDelegate>()
                {
                    { "localhost", callbackServer },
                });

            AdhocLoginProvider loginProviderClientSide = AdhocLoginProvider.BuildForClient(clientSideHttpClientFactory, _logger, new Uri("https://localhost/"));

            // Step 1 - Client's UX initiates request for secret user info and immediately get valid credentials back, including a newly generated RSA private key
            string state = "{\"PrivateKey\":null,\"SaltValue\":null,\"AuthProvider\":\"adhoc\",\"UserId\":\"f7d3e5ef222446318166233844878ce4\",\"UserFullName\":\"Test\",\"UserGivenName\":\"Test\",\"UserSurname\":\"\",\"UserEmail\":null,\"UserIconPng\":null,\"ClientId\":null,\"ClientName\":null}";
            RetrieveResult<UserClientSecretInfo> secretUserInfoResult = await loginProviderClientSide.GetSecretUserInfo(ClientAuthenticationScope.User, _logger, state, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            Assert.IsNotNull(secretUserInfoResult);
            Assert.IsTrue(secretUserInfoResult.Success);
            UserClientSecretInfo secretUserInfo = secretUserInfoResult.Result;

            Assert.AreEqual("f7d3e5ef222446318166233844878ce4", secretUserInfo.UserId);
            Assert.AreEqual("Test", secretUserInfo.UserFullName);
            Assert.IsNotNull(secretUserInfo.PrivateKey);

            state = "{\"PrivateKey\":null,\"SaltValue\":null,\"AuthProvider\":\"adhoc\",\"UserId\":\"aeb5f824622a4bf1b085ad997d834ffe\",\"UserFullName\":\"Test 2\",\"UserGivenName\":\"Test\",\"UserSurname\":\"2\",\"UserEmail\":null,\"UserIconPng\":null,\"ClientId\":null,\"ClientName\":null}";
            secretUserInfoResult = await loginProviderClientSide.GetSecretUserInfo(ClientAuthenticationScope.User, _logger, state, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            Assert.IsNotNull(secretUserInfoResult);
            Assert.IsTrue(secretUserInfoResult.Success);
            secretUserInfo = secretUserInfoResult.Result;

            Assert.AreEqual("aeb5f824622a4bf1b085ad997d834ffe", secretUserInfo.UserId);
            Assert.AreEqual("Test 2", secretUserInfo.UserFullName);
            Assert.IsNotNull(secretUserInfo.PrivateKey);
        }

        private class FakeMSAServer : IHttpServerDelegate
        {
            private ILogger _logger;

            public FakeMSAServer(ILogger logger)
            {
                _logger = logger;
            }

            public async Task HandleConnection(IHttpServerContext serverContext, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                HttpResponse resp = await HandleConnectionInternal(serverContext.HttpRequest, cancelToken, realTime).ConfigureAwait(false);
                if (resp != null)
                {
                    try
                    {
                        await serverContext.WritePrimaryResponse(resp, _logger, cancelToken, realTime).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        _logger.Log(e, LogLevel.Err);
                    }
                }
            }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "This method returns an IDisposable so the caller should be responsible for disposal")]
            private async Task<HttpResponse> HandleConnectionInternal(HttpRequest request, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                if (string.Equals(request.RequestFile, "/common/oauth2/v2.0/token"))
                {
                    HttpResponse response = HttpResponse.OKResponse();
                    response.SetContent("{ \"access_token\": \"super_secret_token\", \"expires_in\": 100000, \"token_type\": \"bearer\" }", "application/json");
                    return response;
                }

                return await Task.FromResult(HttpResponse.NotFoundResponse());
            }
        }

        private class FakeMSGraphServer : IHttpServerDelegate
        {
            private ILogger _logger;

            public FakeMSGraphServer(ILogger logger)
            {
                _logger = logger;
            }

            public async Task HandleConnection(IHttpServerContext serverContext, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                HttpResponse resp = await HandleConnectionInternal(serverContext.HttpRequest, cancelToken, realTime).ConfigureAwait(false);
                if (resp != null)
                {
                    try
                    {
                        await serverContext.WritePrimaryResponse(resp, _logger, cancelToken, realTime).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        _logger.Log(e, LogLevel.Err);
                    }
                }
            }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "This method returns an IDisposable so the caller should be responsible for disposal")]
            private async Task<HttpResponse> HandleConnectionInternal(HttpRequest request, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                if (string.Equals(request.RequestFile, "/v1.0/me"))
                {
                    HttpResponse response = HttpResponse.OKResponse();
                    response.SetContent("{ \"displayName\": \"Test User\", \"id\": \"" + TEST_PUID + "\", \"givenName\": \"Test\", \"surname\": \"User\", \"userPrincipalName\": \"testuser@outlook.com\" }", "application/json");
                    return response;
                }

                return await Task.FromResult(HttpResponse.NotFoundResponse());
            }
        }
    }
}
