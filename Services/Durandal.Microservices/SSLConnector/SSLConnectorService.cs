using Durandal.Common.Logger;
using Durandal.Common.File;
using DurandalServices.ChannelProxy.Connectors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Dialog;
using Durandal.Common.Net;
using Durandal.Common.Tasks;
using Durandal.Common.Security;
using Durandal.Common.Security.Login;
using Durandal.Common.Security.Login.Providers;
using Durandal.Common.Security.Server;
using Durandal.Common.Net.Http;
using Durandal.Common.Security.OAuth;
using Durandal.Extensions.MySql;
using Durandal.Common.MathExt;
using Durandal.Common.Instrumentation;
using Durandal.Common.Utils;
using System.Threading;
using Durandal.Common.Time;
using Durandal.Common.ServiceMgmt;

namespace DurandalServices.SSLConnector
{
    public class SSLConnectorService : BasicService
    {
        private readonly MySqlConnectionPool _sqlConnectionPool;
        private readonly SSLRoutingServer _server;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IPrivateKeyStore _privateKeyVault;
        private readonly IPublicKeyStore _publicKeyVault;
        private readonly IOAuthSecretStore _oauthSecretStore;
        private readonly IRandom _cryptoRandom;
        private readonly IRSADelegates _rsa;
        private readonly MySqlUserProfileStorage _userProfileStore;
        private readonly AuthHttpServer _authServer = null;

        public SSLConnectorService(ILogger logger, IFileSystem configManager, WeakPointer<IThreadPool> threadPool, WeakPointer<IMetricCollector> metrics, DimensionSet dimensions)
            : base("SSLConnector", logger, configManager, threadPool, metrics, dimensions)
        {
            Uri thirdPartyOauthCallbackUrl = null;
            if (ServiceConfig.ContainsKey("thirdPartyOAuthCallbackUrl"))
            {
                thirdPartyOauthCallbackUrl = new Uri(ServiceConfig.GetString("thirdPartyOAuthCallbackUrl"));
            }

            Uri msaLoginCallbackUrl = thirdPartyOauthCallbackUrl;
            if (ServiceConfig.ContainsKey("msaOAuthCallbackUrl"))
            {
                msaLoginCallbackUrl = new Uri(ServiceConfig.GetString("msaOAuthCallbackUrl"));
            }

            string msaOauthClientId = ServiceConfig.GetString("msaOAuthClientId");
            string msaOauthClientSecret = ServiceConfig.GetString("msaOAuthClientSecret");
            string sqlConnectionString = ServiceConfig.GetString("mySqlConnectionString");

            _sqlConnectionPool = MySqlConnectionPool.Create(sqlConnectionString, ServiceLogger, Metrics.Value, MetricDimensions, "SslConnector", true).Await();
            _httpClientFactory = new PortableHttpClientFactory(metrics, dimensions);
            var prikeyVault = new MySqlPrivateKeyStore(_sqlConnectionPool, ServiceLogger);
            prikeyVault.Initialize().Await();
            _privateKeyVault = prikeyVault;
            var pubKeyVault = new MySqlPublicKeyStore(_sqlConnectionPool, ServiceLogger);
            pubKeyVault.Initialize().Await();
            _publicKeyVault = pubKeyVault;
            var oauthStore = new MySqlOAuthSecretStore(_sqlConnectionPool, ServiceLogger);
            oauthStore.Initialize().Await();
            _oauthSecretStore = oauthStore;
            var profileStore = new MySqlUserProfileStorage(_sqlConnectionPool, ServiceLogger);
            profileStore.Initialize().Await();
            _userProfileStore = profileStore;

            _cryptoRandom = new CryptographicRandom();
            _rsa = new StandardRSADelegates(_cryptoRandom);

            ILoginProvider adhocLoginProvider = AdhocLoginProvider.BuildForServer(ServiceLogger, _privateKeyVault, _publicKeyVault, _rsa, 512);
            ILoginProvider msaLoginProvider = MSAPortableLoginProvider.BuildForServer(_httpClientFactory, ServiceLogger, msaOauthClientId, msaOauthClientSecret, msaLoginCallbackUrl, _privateKeyVault, _publicKeyVault, _rsa, 512);
            _authServer = new AuthHttpServer(ServiceLogger, _oauthSecretStore, thirdPartyOauthCallbackUrl, metrics, dimensions, new ILoginProvider[] { adhocLoginProvider, msaLoginProvider }, _userProfileStore);
            _server = new SSLRoutingServer(_authServer, ServiceLogger.Clone("SSLConnectorServer"), ThreadPool);
        }
        
        public override async Task Start(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            ServiceLogger.Log("Starting service...");
            await _server.StartServer("SSLConnector", cancelToken, realTime);
            ServiceLogger.Log("Started.");
        }

        public override async Task Stop(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_server != null && _server.Running)
            {
                ServiceLogger.Log("Stopping service...");
                await _server.StopServer(cancelToken, realTime);
            }
        }

        public override bool IsRunning()
        {
            if (_server != null)
            {
                return _server.Running;
            }

            return false;
        }
    }
}
