using Photon;
using Photon.Common.Monitors;
using Photon.Common.MySQL;
using Photon.Common.Schemas;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using Durandal.Common.Utils.IO;
using Durandal.Common.Utils.Tasks;
using Microsoft.AzureAd.Icm.Types;
using Microsoft.AzureAd.Icm.WebService.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel;
using System.ServiceModel.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Photon.Common.ICM
{
    public class ICMAdapter
    {
        private const string ICM_WEB_SERVICE_BASE_URL = "https://icm.ad.msft.net";
        
        public ICMAdapter()
        {
        }

        public IncidentAddUpdateResult GenerateAlert(RaiseAlertEvent alertEvent)
        {
            if (alertEvent.Level == AlertLevel.Mute || alertEvent.Level == AlertLevel.NoAlert)
            {
                return null;
            }

            using (ConnectorIncidentManagerClient icmClient = CreateIcmClient(alertEvent.TargetTeam.CertThumbprint))
            {
                int severity = 4;
                if (alertEvent.Level == AlertLevel.Alert)
                {
                    severity = 2;
                }

                alertEvent.FailingTests.Sort();
                string failingTests = string.Join(",", alertEvent.FailingTests);

                AlertSourceIncident incident = new AlertSourceIncident()
                {
                    Source = new AlertSourceInfo()
                    {
                        Origin = "Internal",
                        CreatedBy = "Monitoring",
                        CreateDate = DateTime.UtcNow,
                        // this could be refined later to use the hash of suite name + failing tests or something, but I also don't want to correlate with errors that happened long ago
                        IncidentId = Guid.NewGuid().ToString("N"),
                        ModifiedDate = DateTime.UtcNow
                    },
                    RaisingLocation = new IncidentLocation()
                    {
                        Environment = "PROD",
                        DeviceGroup = alertEvent.FailingSuite
                    },
                    OccurringLocation = new IncidentLocation()
                    {
                        Environment = "PROD",
                        DeviceGroup = alertEvent.FailingSuite
                    },
                    CorrelationId = "photon://" + alertEvent.FailingSuite,
                    Description = "One or more tests in the monitoring suite \"" + alertEvent.FailingSuite + "\" are failing in dorado monitoring portal. Please view " + alertEvent.DashboardLink + " for details. Message: " + Cap(alertEvent.Message, 10000),
                    DescriptionEntries = new DescriptionEntry[]
                    {
                        new DescriptionEntry()
                        {
                            Cause = DescriptionEntryCause.Created,
                            Date = DateTime.UtcNow,
                            SubmitDate = DateTime.UtcNow,
                            ChangedBy = "Photon API",
                            SubmittedBy = "Photon API",
                            RenderType = DescriptionTextRenderType.Plaintext,
                            Text =  Cap(alertEvent.Message, 40000),
                        }
                    },
                    Title = "Failures detected in monitoring suite \"" + alertEvent.FailingSuite + "\"",
                    RoutingId = "photon://DefaultRouting",
                    Component = "Dorado",
                    Status = IncidentStatus.Active,
                    Severity = severity,
                    MonitorId = alertEvent.FailingSuite,
                    OwningTeamId = alertEvent.TargetTeam.IcmId,
                    ServiceResponsible = new TenantIdentifier(alertEvent.TargetTeam.IcmId)
                };

                IncidentAddUpdateResult result = icmClient.AddOrUpdateIncident2(
                    alertEvent.TargetTeam.ConnectorId,
                    incident,
                    RoutingOptions.None);

                return result;
            }
        }

        private static string Cap(string input, int length)
        {
            if (input.Length < length)
            {
                return input;
            }

            return input.Substring(0, length);
        }

        private static ConnectorIncidentManagerClient CreateIcmClient(string certThumbprint)
        {
            ConnectorIncidentManagerClient returnVal;
            WS2007HttpBinding binding;
            EndpointAddress remoteAddress;
            string url = string.Format("{0}/connector3/ConnectorIncidentManager.svc", ICM_WEB_SERVICE_BASE_URL);
            binding = new WS2007HttpBinding(SecurityMode.Transport)
            {
                Name = "IcmBindingConfigCert",
                MaxBufferPoolSize = 4194304,
                MaxReceivedMessageSize = 16777216
            };
            binding.Security.Transport.Realm = string.Empty;
            binding.Security.Transport.ProxyCredentialType = HttpProxyCredentialType.None;
            binding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Certificate;
            binding.ReaderQuotas.MaxArrayLength = 16384;
            binding.ReaderQuotas.MaxBytesPerRead = 1048576;
            binding.ReaderQuotas.MaxStringContentLength = 1048576;
            binding.Security.Message.EstablishSecurityContext = false;
            binding.Security.Message.NegotiateServiceCredential = true;
            binding.Security.Message.AlgorithmSuite = SecurityAlgorithmSuite.Default;
            binding.Security.Message.ClientCredentialType = MessageCredentialType.Certificate;
            remoteAddress = new EndpointAddress(url);
            returnVal = new ConnectorIncidentManagerClient(binding, remoteAddress);
            if (returnVal.ClientCredentials != null)
            {
                returnVal.ClientCredentials.ClientCertificate.SetCertificate(
                    StoreLocation.LocalMachine, StoreName.My, X509FindType.FindByThumbprint, certThumbprint);
            }

            return returnVal;
        }
    }
}
