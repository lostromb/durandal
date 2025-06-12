using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Security;
using Durandal.Common.Security.OAuth;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Tests.Common.Security;
using Durandal.Common.Utils;
using Durandal.Common.Instrumentation;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Tests.Common.Security
{
    [TestClass]
    public class OAuthTests
    {
        private static readonly PluginStrongName PLUGIN_DOMAIN = new PluginStrongName("myplugin", 2, 0);
        private static readonly string DURANDAL_USER_ID = "testuserid";

        private static InMemoryOAuthSecretStore _secretStore = new InMemoryOAuthSecretStore();
        private static ILogger _logger = new ConsoleLogger();

        [TestCleanup]
        public void CleanupTest()
        {
            _secretStore.Clear();
        }

        [TestMethod]
        public async Task TestOAuth3PGenerateAuthUrl()
        {
            string redirectUrl = "https://localhost/auth/oauth/thirdparty";
            OAuthConfig sampleConfig = new OAuthConfig()
            {
                ClientId = "contoso-client-id",
                ClientSecret = "contoso-client-secret",
                AuthUri = "https://api.contoso.com/oauth/authorize",
                TokenUri = "https://api.contoso.com/oauth/token",
                Scope = "scope1 scope2 scope3",
                ConfigName = "sample"
            };

            AuthHttpServer callbackServer = new AuthHttpServer(_logger, _secretStore, new Uri(redirectUrl), NullMetricCollector.WeakSingleton, DimensionSet.Empty);
            FakeAuthServer tokenServer = new FakeAuthServer(_logger);
            DirectHttpClientFactory httpClientFactory = new DirectHttpClientFactory(null,
                new Dictionary<string, IHttpServerDelegate>()
                {
                    { "localhost", callbackServer },
                    { "api.contoso.com", tokenServer }
                });

            OAuthManager authManager = new OAuthManager(redirectUrl, _secretStore, NullMetricCollector.WeakSingleton, DimensionSet.Empty, httpClientFactory);

            // Build the authorize URL and make sure it looks OK
            Uri authUri = await authManager.CreateAuthUri(DURANDAL_USER_ID, PLUGIN_DOMAIN, sampleConfig, _logger, CancellationToken.None, DefaultRealTimeProvider.Singleton);

            RetrieveResult<OAuthState> hiddenState = await _secretStore.RetrieveState(DURANDAL_USER_ID, PLUGIN_DOMAIN, sampleConfig);
            string expectedUri = "https://api.contoso.com/oauth/authorize?response_type=code&client_id=contoso-client-id&scope=scope1+scope2+scope3&redirect_uri=https%3A%2F%2Flocalhost%2Fauth%2Foauth%2Fthirdparty&state=" + hiddenState.Result.UniqueId;
            Assert.AreEqual(expectedUri, authUri.AbsoluteUri);
        }

        [TestMethod]
        public async Task TestOAuth3PForbidInsecureAuthUri()
        {
            string redirectUrl = "https://localhost/auth/oauth/thirdparty";
            OAuthConfig sampleConfig = new OAuthConfig()
            {
                ClientId = "contoso-client-id",
                ClientSecret = "contoso-client-secret",
                AuthUri = "http://api.contoso.com/oauth/authorize",
                TokenUri = "https://api.contoso.com/oauth/token",
                Scope = "scope1 scope2 scope3",
                ConfigName = "sample"
            };

            AuthHttpServer callbackServer = new AuthHttpServer(_logger, _secretStore, new Uri(redirectUrl), NullMetricCollector.WeakSingleton, DimensionSet.Empty);
            FakeAuthServer tokenServer = new FakeAuthServer(_logger);
            DirectHttpClientFactory httpClientFactory = new DirectHttpClientFactory(null,
                new Dictionary<string, IHttpServerDelegate>()
                {
                    { "localhost", callbackServer },
                    { "api.contoso.com", tokenServer }
                });

            OAuthManager authManager = new OAuthManager(redirectUrl, _secretStore, NullMetricCollector.WeakSingleton, DimensionSet.Empty, httpClientFactory);

            // Build the authorize URL and make sure it looks OK
            try
            {
                Uri authUri = await authManager.CreateAuthUri(DURANDAL_USER_ID, PLUGIN_DOMAIN, sampleConfig, _logger, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                Assert.Fail("Expected an ArgumentException");
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public async Task TestOAuth3PForbidInsecureTokenUri()
        {
            string redirectUrl = "https://localhost/auth/oauth/thirdparty";
            OAuthConfig sampleConfig = new OAuthConfig()
            {
                ClientId = "contoso-client-id",
                ClientSecret = "contoso-client-secret",
                AuthUri = "https://api.contoso.com/oauth/authorize",
                TokenUri = "http://api.contoso.com/oauth/token",
                Scope = "scope1 scope2 scope3",
                ConfigName = "sample"
            };

            AuthHttpServer callbackServer = new AuthHttpServer(_logger, _secretStore, new Uri(redirectUrl), NullMetricCollector.WeakSingleton, DimensionSet.Empty);
            FakeAuthServer tokenServer = new FakeAuthServer(_logger);
            OAuthManager authManager = new OAuthManager(redirectUrl, _secretStore, NullMetricCollector.WeakSingleton, DimensionSet.Empty, new NullHttpClientFactory());

            try
            {
                Uri authUri = await authManager.CreateAuthUri(DURANDAL_USER_ID, PLUGIN_DOMAIN, sampleConfig, _logger, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                Assert.Fail("Expected an ArgumentException");
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public async Task TestOAuthRequireWellFormedAuthUri()
        {
            string redirectUrl = "https://localhost/auth/oauth/thirdparty";
            OAuthConfig sampleConfig = new OAuthConfig()
            {
                ClientId = "contoso-client-id",
                ClientSecret = "contoso-client-secret",
                AuthUri = "https:api.contoso.com/oauth/authorize",
                TokenUri = "https://api.contoso.com/oauth/token",
                Scope = "scope1 scope2 scope3",
                ConfigName = "sample"
            };

            AuthHttpServer callbackServer = new AuthHttpServer(_logger, _secretStore, new Uri(redirectUrl), NullMetricCollector.WeakSingleton, DimensionSet.Empty);
            FakeAuthServer tokenServer = new FakeAuthServer(_logger);
            OAuthManager authManager = new OAuthManager(redirectUrl, _secretStore, NullMetricCollector.WeakSingleton, DimensionSet.Empty, new NullHttpClientFactory());

            try
            {
                Uri authUri = await authManager.CreateAuthUri(DURANDAL_USER_ID, PLUGIN_DOMAIN, sampleConfig, _logger, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                Assert.Fail("Expected an ArgumentException");
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public async Task TestOAuthRequireWellFormedTokenUri()
        {
            string redirectUrl = "https://localhost/auth/oauth/thirdparty";
            OAuthConfig sampleConfig = new OAuthConfig()
            {
                ClientId = "contoso-client-id",
                ClientSecret = "contoso-client-secret",
                AuthUri = "https://api.contoso.com/oauth/authorize",
                TokenUri = "https:api.contoso.com/oauth/token",
                Scope = "scope1 scope2 scope3",
                ConfigName = "sample"
            };

            AuthHttpServer callbackServer = new AuthHttpServer(_logger, _secretStore, new Uri(redirectUrl), NullMetricCollector.WeakSingleton, DimensionSet.Empty);
            FakeAuthServer tokenServer = new FakeAuthServer(_logger);
            OAuthManager authManager = new OAuthManager(redirectUrl, _secretStore, NullMetricCollector.WeakSingleton, DimensionSet.Empty, new NullHttpClientFactory());
            try
            {
                Uri authUri = await authManager.CreateAuthUri(DURANDAL_USER_ID, PLUGIN_DOMAIN, sampleConfig, _logger, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                Assert.Fail("Expected an ArgumentException");
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public async Task TestOAuth3PBasicFlowE2E()
        {
            string redirectUrl = "https://localhost/auth/oauth/thirdparty";
            OAuthConfig sampleConfig = new OAuthConfig()
            {
                ClientId = "contoso-client-id",
                ClientSecret = "contoso-client-secret",
                AuthUri = "https://api.contoso.com/oauth/authorize",
                TokenUri = "https://api.contoso.com/oauth/token",
                Scope = "scope1 scope2 scope3",
                ConfigName = "sample"
            };

            AuthHttpServer callbackServer = new AuthHttpServer(_logger, _secretStore, new Uri(redirectUrl), NullMetricCollector.WeakSingleton, DimensionSet.Empty);
            FakeAuthServer tokenServer = new FakeAuthServer(_logger);
            DirectHttpClientFactory httpClientFactory = new DirectHttpClientFactory(null,
                new Dictionary<string, IHttpServerDelegate>()
                {
                    { "localhost", callbackServer },
                    { "api.contoso.com", tokenServer }
                });

            OAuthManager authManager = new OAuthManager(
                redirectUrl,
                _secretStore,
                NullMetricCollector.WeakSingleton,
                DimensionSet.Empty,
                httpClientFactory);

            // Build the authorize URL and make sure it looks OK
            Uri authUri = await authManager.CreateAuthUri(DURANDAL_USER_ID, PLUGIN_DOMAIN, sampleConfig, _logger, CancellationToken.None, DefaultRealTimeProvider.Singleton);

            RetrieveResult<OAuthState> hiddenState = await _secretStore.RetrieveState(DURANDAL_USER_ID, PLUGIN_DOMAIN, sampleConfig);
            string expectedUri = "https://api.contoso.com/oauth/authorize?response_type=code&client_id=contoso-client-id&scope=scope1+scope2+scope3&redirect_uri=https%3A%2F%2Flocalhost%2Fauth%2Foauth%2Fthirdparty&state=" + hiddenState.Result.UniqueId;
            Assert.AreEqual(expectedUri, authUri.AbsoluteUri);

            // Create the fake callback request (as though from 3rd party) and send it to the handler server
            HttpRequest callbackRequest = HttpRequest.CreateOutgoing(redirectUrl + "?code=12345&state=" + hiddenState.Result.UniqueId);
            DirectHttpClient httpClient = new DirectHttpClient(callbackServer);
            HttpResponse callbackResponse = await httpClient.SendRequestAsync(callbackRequest);

            // Now get the token and verify it
            OAuthToken token = await authManager.GetToken(DURANDAL_USER_ID, PLUGIN_DOMAIN, sampleConfig, _logger, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            Assert.IsNotNull(token);
            Assert.AreEqual("super_secret_token", token.Token);
            Assert.AreEqual("bearer", token.TokenType);
        }

        [TestMethod]
        public async Task TestOAuth3PCantGetToken()
        {
            string redirectUrl = "https://localhost/auth/oauth/thirdparty";
            OAuthConfig sampleConfig = new OAuthConfig()
            {
                ClientId = "contoso-client-id",
                ClientSecret = "contoso-client-secret",
                AuthUri = "https://api.contoso.com/oauth/authorize",
                TokenUri = "https://api.contoso.com/oauth/illegal_token",
                Scope = "scope1 scope2 scope3",
                ConfigName = "sample"
            };

            AuthHttpServer callbackServer = new AuthHttpServer(_logger, _secretStore, new Uri(redirectUrl), NullMetricCollector.WeakSingleton, DimensionSet.Empty);
            FakeAuthServer tokenServer = new FakeAuthServer(_logger);
            DirectHttpClientFactory httpClientFactory = new DirectHttpClientFactory(null,
                new Dictionary<string, IHttpServerDelegate>()
                {
                    { "localhost", callbackServer },
                    { "api.contoso.com", tokenServer }
                });

            OAuthManager authManager = new OAuthManager(redirectUrl, _secretStore, NullMetricCollector.WeakSingleton, DimensionSet.Empty, httpClientFactory);

            // Build the authorize URL and make sure it looks OK
            Uri authUri = await authManager.CreateAuthUri(DURANDAL_USER_ID, PLUGIN_DOMAIN, sampleConfig, _logger, CancellationToken.None, DefaultRealTimeProvider.Singleton);

            RetrieveResult<OAuthState> hiddenState = await _secretStore.RetrieveState(DURANDAL_USER_ID, PLUGIN_DOMAIN, sampleConfig);
            string expectedUri = "https://api.contoso.com/oauth/authorize?response_type=code&client_id=contoso-client-id&scope=scope1+scope2+scope3&redirect_uri=https%3A%2F%2Flocalhost%2Fauth%2Foauth%2Fthirdparty&state=" + hiddenState.Result.UniqueId;
            Assert.AreEqual(expectedUri, authUri.AbsoluteUri);

            // Create the fake callback request (as though from 3rd party) and send it to the handler server
            HttpRequest callbackRequest = HttpRequest.CreateOutgoing(redirectUrl + "?code=12345&state=" + hiddenState.Result.UniqueId);
            DirectHttpClient httpClient = new DirectHttpClient(tokenServer);
            HttpResponse callbackResponse = await httpClient.SendRequestAsync(callbackRequest);

            // Now get the token and verify it
            OAuthToken token = await authManager.GetToken(DURANDAL_USER_ID, PLUGIN_DOMAIN, sampleConfig, _logger, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            Assert.IsNull(token);
        }

        /// <summary>
        /// Simulates the third-party service in the oauth flow (the only functionality we need to emulate here is resolving a code into a token)
        /// </summary>
        private class FakeAuthServer : IHttpServerDelegate
        {
            private ILogger _logger;

            public FakeAuthServer(ILogger logger)
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
                if (string.Equals(request.RequestFile, "/oauth/token"))
                {
                    HttpResponse response = HttpResponse.OKResponse();
                    response.ResponseHeaders[HttpConstants.HEADER_KEY_CONTENT_TYPE] = "application/json";
                    response.SetContent("{ \"access_token\": \"super_secret_token\", \"expires_in\": 100000, \"token_type\": \"bearer\" }", "application/json");
                    return response;
                }
                else if (string.Equals(request.RequestFile, "/oauth/illegal_token"))
                {
                    return HttpResponse.NotAuthorizedResponse();
                }

                return await Task.FromResult(HttpResponse.NotFoundResponse());
            }
        }
    }
}
