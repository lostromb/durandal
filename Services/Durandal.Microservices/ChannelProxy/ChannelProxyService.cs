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
using Durandal.Common.Instrumentation;
using Durandal.Common.Utils;
using Durandal.Common.Security;
using System.Threading;
using Durandal.Common.Time;
using Durandal.Common.ServiceMgmt;

namespace DurandalServices.ChannelProxy
{
    public class ChannelProxyService : BasicService
    {
        private readonly ProxyServer _server = null;

        public ChannelProxyService(ILogger logger, IFileSystem configManager, WeakPointer<IThreadPool> threadPool, WeakPointer<IMetricCollector> metrics, DimensionSet dimensions)
            : base("ChannelProxy", logger, configManager, threadPool, metrics, dimensions)
        {
            IList<IConnector> connectors = new List<IConnector>();
            ISet<string> enabledProviders = new HashSet<string>(ServiceConfig.GetStringList("providerList"));

            connectors.Add(new PingConnector());

            if (enabledProviders.Contains("twilio"))
            {
                ServiceLogger.Log("Enabling Twilio provider...");
                connectors.Add(new TwilioConnector(ServiceLogger.Clone("TwilioConnector"), threadPool));
            }
            if (enabledProviders.Contains("telegram"))
            {
                ServiceLogger.Log("Enabling Telegram provider...");
                connectors.Add(new TelegramConnector(ServiceLogger.Clone("TelegramConnector"), threadPool));
            }
            if (enabledProviders.Contains("dorado"))
            {
                ServiceLogger.Log("Enabling Dorado provider...");
                connectors.Add(new DoradoConnector(ServiceLogger.Clone("DoradoConnector")));
            }

            int localListenPort = ServiceConfig.GetInt32("listeningPort", 62294);
            bool dialogUseSSL = ServiceConfig.GetBool("remoteDialogUseSSL", false);
            bool listenerUseSSL = ServiceConfig.GetBool("listenerUseSSL", false);
            string remoteDialogHost = ServiceConfig.GetString("remoteDialogServerAddress", "localhost");
            int remoteDialogPort = ServiceConfig.GetInt32("remoteDialogServerPort", 62292);

            Uri dialogTarget = new Uri((dialogUseSSL ? "https://" : "http://") + remoteDialogHost + ":" + remoteDialogPort);
            ServerBindingInfo bindingEndpoint;
            if (listenerUseSSL)
            {
                // fixme hardcoded SSL certificate id
                bindingEndpoint = new ServerBindingInfo(ServerBindingInfo.WILDCARD_HOSTNAME, localListenPort, CertificateIdentifier.BySubjectName("durandal-ai.net"));
            }
            else
            {
                bindingEndpoint = new ServerBindingInfo(ServerBindingInfo.WILDCARD_HOSTNAME, localListenPort);
            }

            _server = new ProxyServer(ServiceLogger.Clone("ChannelProxyServer"), connectors, new ServerBindingInfo[] { bindingEndpoint }, dialogTarget, ThreadPool);
        }

        public override async Task Start(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            ServiceLogger.Log("Starting service...");
            await _server.StartServer("ChannelProxy", cancelToken, realTime);
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
