using Durandal.API;
using Durandal.Common.Utils;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Services;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Time;
using System.Threading;

namespace Durandal.Plugins.Plugins.USRepresentatives.MyRepresentative
{
    public class MyRepresentativeApi
    {
        private readonly string WhoIsUrl = "/getall_mems.php?zip={0}&output=json";
        private readonly IHttpClient _httpClient;
        
        public MyRepresentativeApi(IPluginServices services)
        {
            _httpClient = services.HttpClientFactory.CreateHttpClient(new Uri("http://whoismyrepresentative.com"));
        }

        private async Task<List<MyRepLegislator>> GetLegislatorsInternal(ILogger queryLogger, int zipCode)
        {
            using (HttpRequest request = HttpRequest.CreateOutgoing(string.Format(WhoIsUrl, zipCode)))
            using (NetworkResponseInstrumented<HttpResponse> netResponse = await _httpClient.SendInstrumentedRequestAsync(
                request,
                CancellationToken.None,
                DefaultRealTimeProvider.Singleton,
                queryLogger).ConfigureAwait(false))
            {
                try
                {
                    if (!netResponse.Success)
                    {
                        queryLogger.Log("No response from whois service", LogLevel.Err);
                        return new List<MyRepLegislator>();
                    }

                    if (netResponse.Response.ResponseCode != 200)
                    {
                        queryLogger.Log("Error HTTP " + netResponse.Response.ResponseCode + " from whois service", LogLevel.Err);
                        return new List<MyRepLegislator>();
                    }

                    ApiResponse response = await netResponse.Response.ReadContentAsJsonObjectAsync<ApiResponse>(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    return response.results;
                }
                finally
                {
                    if (netResponse != null)
                    {
                        await netResponse.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    }
                }
            }
        }

        public async Task<List<Legislator>> GetLegislators(ILogger queryLogger, IPluginServices services, int zipCode)
        {
            List<MyRepLegislator> legislators = await GetLegislatorsInternal(queryLogger, zipCode).ConfigureAwait(false);
            return await Convert(legislators, services).ConfigureAwait(false);
        }

        /// <summary>
        /// Determines the relevant state and district given a zip code
        /// </summary>
        /// <param name="zipCode"></param>
        /// <param name="queryLogger"></param>
        /// <returns></returns>
        public async Task<StateDistrict> GetStateDistrictInfo(ILogger queryLogger, int zipCode)
        {
            List<MyRepLegislator> legislators = await GetLegislatorsInternal(queryLogger, zipCode).ConfigureAwait(false);
            foreach (MyRepLegislator leg in legislators)
            {
                int district;
                if (!string.IsNullOrEmpty(leg.state) && !string.IsNullOrEmpty(leg.district) && int.TryParse(leg.district, out district))
                {
                    return new StateDistrict()
                    {
                        StateCode = leg.state.ToUpperInvariant(),
                        District = district
                    };
                }
            }

            return null;
        }

        private static async Task<List<Legislator>> Convert(List<MyRepLegislator> legislators, IPluginServices services)
        {
            // Since entity resolution is potentially async, we start all of the conversion tasks and then await them so they can potentially run in parallel
            List<Task<Legislator>> resolverTasks = new List<Task<Legislator>>();
            foreach (MyRepLegislator leg in legislators)
            {
                resolverTasks.Add(Convert(leg, services));
            }

            List<Legislator> returnVal = new List<Legislator>();
            foreach (Task<Legislator> task in resolverTasks)
            {
                returnVal.Add(await task.ConfigureAwait(false));
            }

            return returnVal;
        }

        private static async Task<Legislator> Convert(MyRepLegislator legislator, IPluginServices services)
        {
            Legislator returnVal = new Legislator();
            returnVal.FullName = legislator.name;
            returnVal.Party = await PartyResolver.ResolveParty(new LexicalString(legislator.party), services).ConfigureAwait(false);
            returnVal.PartyName = PartyResolver.PartyToString(returnVal.Party);
            int district;
            if (int.TryParse(legislator.district, out district))
            {
                returnVal.DistrictNumber = district;
            }
            returnVal.PhoneNumber = legislator.phone;
            Uri parsedUri;
            if (Uri.TryCreate(legislator.link, UriKind.Absolute, out parsedUri))
            {
                returnVal.WebSite = parsedUri;
            }
            returnVal.CapitolAddress = legislator.office; // ???
            return returnVal;
        }
    }

    public class ApiResponse
    {
        public List<MyRepLegislator> results { get; set; }
    }

    public class MyRepLegislator
    {
        public string name { get; set; }
        public string party { get; set; }
        public string state { get; set; }
        public string district { get; set; }
        public string phone { get; set; }
        public string office { get; set; }
        public string link { get; set; }
    }
}
