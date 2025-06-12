namespace Photon.Common.Util
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net.Http;
    using System.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading.Tasks;
    using ExtendCortana;
    using Microsoft.Bond;
    using Microsoft.Search.ObjectStore;
    using Newtonsoft.Json;
    using ObjectStore;
    using Photon.Common.Config;
    using Photon.Common.JWT;
    using Durandal.Common.Net;

    public class ObjectStoreConfiguration
    {
        public IEnvironmentConfiguration environmentConfig;
        public string EnvironmentName;
        public string NamespaceName;
        public string TableName;

        public ObjectStoreConfiguration(IEnvironmentConfiguration _environmentConfig, string _EnvironmentName, string _NamespaceName, string _TableName)
        {
            environmentConfig = _environmentConfig;
            EnvironmentName = _EnvironmentName;
            NamespaceName = _NamespaceName;
            TableName = _TableName;
        }

    }

    public class ObjectStoreException : Exception
    {
        public ObjectStoreException() : base()
        {
        }

        public ObjectStoreException(string message) : base(message)
        {
        }
    }

    public class ObjectStoreClient
    {
        private static readonly string ReadVip = "https://ObjectStoreFD.Prod.CO.binginternal.com/sds";
        private static X509CertificateCollection certCollection;

        private HttpClient httpClient;
        private ObjectStoreConfiguration configuration;

        public ObjectStoreClient(ObjectStoreConfiguration _configuration)
        {
            configuration = _configuration;
            WebRequestHandler handler = new WebRequestHandler();
            httpClient = new HttpClient(handler);

            string thumbprint = _configuration.environmentConfig.GetConfigurationValue("ObjectStoreThumbprint"); // ?? "8C568F95D518FFCBFB2C593C8979F469951A03B5";
            X509Certificate2 certificate = CertificateHelper.GetCertificateByThumbprint(thumbprint);
            certCollection = new X509CertificateCollection();
            certCollection.Add(certificate);
        }

        private static IClient<AzureKey, TValue> GetReadClient<TValue>(string table, string tableNamespace)
            where TValue : class, IBondSerializable, new() =>
             Client.Builder<AzureKey, TValue>(
                 environment: ReadVip,
                 osNamespace: tableNamespace,
                 osTable: table,
                 timeout: new TimeSpan(0, 0, 0, 500),
                 maxRetries: 1
                 ).WithClientCertificates(certCollection).Create();

        private async Task<T> GetFromTableExAsync<T>(string table, string key, string tableNamespace, T defaultValue = default(T))
            where T : class, IBondSerializable, new()
        {
            try
            {
                using (var client = GetReadClient<T>(table, tableNamespace))
                {
                    var result = client.Read(new AzureKey { key = key }).SendAsync();
                    return (await result)?.FirstOrDefault() ?? defaultValue;
                }
            }
            catch (Exception ex)
            {
                throw new ObjectStoreException(ex.Message);
            }
        }

        public async Task<T> GetAsync<T>(string data)
            where T : class, IBondSerializable, new()
        {
            return await GetFromTableExAsync<T>(configuration.TableName, data, configuration.NamespaceName);
        }
    }
}