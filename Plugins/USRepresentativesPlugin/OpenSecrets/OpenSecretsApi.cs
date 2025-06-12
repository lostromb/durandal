using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Time;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Plugins.Plugins.USRepresentatives.OpenSecrets
{
    public class OpenSecretsApi
    {
        private readonly string OpenSecretsUrl = "/api/?method=getLegislators&id={0}&output=json&apikey=88a614856a769fae976caffa6f34a329";
        private readonly IHttpClient _httpClient;

        public OpenSecretsApi(IPluginServices services)
        {
            _httpClient = services.HttpClientFactory.CreateHttpClient(new Uri("http://www.opensecrets.org"));
        }

        public async Task<List<Legislator>> GetLegislators(ILogger queryLogger, IPluginServices services, string state, int district = -1)
        {
            List<OpenSecretsLegislator> legislators = await GetAllLegislatorsInternal(queryLogger, state).ConfigureAwait(false);
            List<Legislator> returnVal = new List<Legislator>();
            List<Legislator> allConvertedLegislators = await Convert(legislators, services).ConfigureAwait(false);

            // Filter by legislative district if requested
            foreach (Legislator convertedLegislator in allConvertedLegislators)
            {
                if (district < 0 || // allow all if no filter
                    convertedLegislator.Office == GovernmentOffice.Senate || // allow all senators
                    convertedLegislator.Office == GovernmentOffice.HouseOfRepresentatives && district == convertedLegislator.DistrictNumber.GetValueOrDefault(0)) // allow reps filtered by district
                {
                    returnVal.Add(convertedLegislator);
                }
            }

            return returnVal;
        }
        
        private async Task<List<OpenSecretsLegislator>> GetAllLegislatorsInternal(ILogger queryLogger, string state)
        {
            HttpRequest request = HttpRequest.CreateOutgoing(string.Format(OpenSecretsUrl, state));
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
                        queryLogger.Log("No response from opensecrets service", LogLevel.Err);
                        return new List<OpenSecretsLegislator>();
                    }

                    if (netResponse.Response.ResponseCode != 200)
                    {
                        queryLogger.Log("Error HTTP " + netResponse.Response.ResponseCode + " from opensecrets service", LogLevel.Err);
                        return new List<OpenSecretsLegislator>();
                    }

                    string responseData = await netResponse.Response.ReadContentAsStringAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    ApiResponse response = JsonConvert.DeserializeObject<ApiResponse>(responseData);

                    List<OpenSecretsLegislator> returnVal = new List<OpenSecretsLegislator>();

                    foreach (ApiLegislator apiLeg in response.response.legislator)
                    {
                        returnVal.Add(apiLeg.obj);
                    }

                    return returnVal;
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

        private static async Task<List<Legislator>> Convert(List<OpenSecretsLegislator> legislators, IPluginServices services)
        {
            // Since entity resolution is potentially async, we start all of the conversion tasks and then await them so they can run in parallel if needed
            List<Task<Legislator>> tasks = new List<Task<Legislator>>();
            foreach (OpenSecretsLegislator leg in legislators)
            {
                tasks.Add(Convert(leg, services));
            }

            List<Legislator> returnVal = new List<Legislator>();
            foreach (Task<Legislator> task in tasks)
            {
                returnVal.Add(await task.ConfigureAwait(false));
            }

            return returnVal;
        }

        private static async Task<Legislator> Convert(OpenSecretsLegislator legislator, IPluginServices services)
        {
            Legislator returnVal = new Legislator();

            returnVal.CID = legislator.cid;
            returnVal.FullName = legislator.firstlast;
            returnVal.LastName = legislator.lastname;
            returnVal.Party = await PartyResolver.ResolveParty(new LexicalString(legislator.party), services).ConfigureAwait(false);
            returnVal.PartyName = PartyResolver.PartyToString(returnVal.Party);

            // Parse state, district info and determine which office they are in
            string stateCode = legislator.office.Substring(0, 2);
            string districtCode = legislator.office.Substring(2);
            returnVal.RepresentingState = stateCode.ToUpperInvariant();
            if (districtCode.Length >= 2)
            {
                int district;
                if (districtCode[0] == 'S' && int.TryParse(districtCode.Substring(1), out district))
                {
                    returnVal.Office = GovernmentOffice.Senate;
                    returnVal.PositionNumber = district;
                }
                else if (int.TryParse(districtCode, out district))
                {
                    returnVal.Office = GovernmentOffice.HouseOfRepresentatives;
                    returnVal.DistrictNumber = district;
                }
            }
            if ("M".Equals(legislator.gender))
            {
                returnVal.Gender = Gender.Male;
            }
            else if ("F".Equals(legislator.gender))
            {
                returnVal.Gender = Gender.Female;
            }
            else
            {
                returnVal.Gender = Gender.Unknown;
            }

            DateTime firstElected;
            if (DateTime.TryParseExact(legislator.first_elected, "yyyy", CultureInfo.CurrentCulture.DateTimeFormat, DateTimeStyles.None, out firstElected))
            {
                returnVal.FirstElected = firstElected.AddMonths(6);
            }
            
            //returnVal.ExitCode = legislator.exit_code;
            returnVal.Comments = legislator.comments;
            returnVal.PhoneNumber = legislator.phone;
            returnVal.FaxNumber = legislator.fax;
            Uri parsedUri;
            if (Uri.TryCreate(legislator.website, UriKind.Absolute, out parsedUri))
            {
                returnVal.WebSite = parsedUri;
            }
            if (Uri.TryCreate(legislator.webform, UriKind.Absolute, out parsedUri))
            {
                returnVal.WebContactForm = parsedUri;
            }
            returnVal.CapitolAddress = legislator.congress_office;
            returnVal.BioGuideId = legislator.bioguide_id;
            returnVal.VoteSmartId = legislator.votesmart_id;
            returnVal.FECCANDId = legislator.feccandid;
            returnVal.TwitterHandle = legislator.twitter_id;
            returnVal.YoutubeUrl = legislator.youtube_url;
            returnVal.FacebookId = legislator.facebook_id;

            DateTime birthdate;
            if (DateTime.TryParse(legislator.birthdate, out birthdate))
            {
                returnVal.BirthDate = birthdate;
            }

            return returnVal;
        }
    }

    public class ApiResponse
    {
        public ApiResult response { get; set; }
    }

    public class ApiResult
    {
        public List<ApiLegislator> legislator { get; set; }
    }

    public class ApiLegislator
    {
        [JsonProperty("@attributes")]
        public OpenSecretsLegislator obj { get; set; }
    }

    public class OpenSecretsLegislator
    {
        public string cid { get; set; }
        public string firstlast { get; set; }
        public string lastname { get; set; }
        public string party { get; set; }
        public string office { get; set; }
        public string gender { get; set; }
        public string first_elected { get; set; }
        public string exit_code { get; set; }
        public string comments { get; set; }
        public string phone { get; set; }
        public string fax { get; set; }
        public string website { get; set; }
        public string webform { get; set; }
        public string congress_office { get; set; }
        public string bioguide_id { get; set; }
        public string votesmart_id { get; set; }
        public string feccandid { get; set; }
        public string twitter_id { get; set; }
        public string youtube_url { get; set; }
        public string facebook_id { get; set; }
        public string birthdate { get; set; }
    }
}
