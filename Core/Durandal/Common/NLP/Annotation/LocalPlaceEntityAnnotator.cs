using Durandal.API;
using Durandal.Common.Config;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Net.Http;
using Durandal.Common.NLP.Language;
using Durandal.Common.Ontology;
using Durandal.Common.Statistics;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using SchemaDotOrg = Durandal.Internal.CoreOntology.SchemaDotOrg;

namespace Durandal.Common.NLP.Annotation
{
    /// <summary>
    /// LU Annotator which resolves location references ("Long beach") into actual city / province / country entities.
    /// </summary>
    public class LocalPlaceEntityAnnotator : BasicConditionalAnnotator
    {
        private readonly string _apiKey;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger _logger;
        private IHttpClient _httpClient;

        public LocalPlaceEntityAnnotator(string apiKey, IHttpClientFactory httpClientFactory, ILogger logger) : base("LocalPlaceEntity")
        {
            _apiKey = apiKey;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _httpClient = _httpClientFactory.CreateHttpClient(new Uri("http://www.bing.com"), _logger);
        }

        public override string Name
        {
            get
            {
                return "localplace";
            }
        }

        public override bool Initialize()
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.Log("API key not provided to LocalPlace annotator; annotation will not run", LogLevel.Err);
                return false;
            }

            return true;
        }

        public override async Task<object> AnnotateStateless(
            RecoResult result,
            LURequest originalRequest,
            IConfiguration modelConfig,
            ILogger queryLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            AnnotationState returnVal = null;
            if (result.TagHyps.Count == 0)
            {
                return returnVal;
            }

            Stopwatch timer = Stopwatch.StartNew();
            double latitude = originalRequest.Context.Latitude.GetValueOrDefault(0);
            double longitude = originalRequest.Context.Longitude.GetValueOrDefault(0);
            ISet<string> allowedIntentsSlots = base.GetEnabledSlots(result.Domain, result.Intent, modelConfig, queryLogger);
            returnVal = new AnnotationState()
            {
                EntityContext = new KnowledgeContext(),
                EntityReferences = new List<EntityReference>()
            };
            
            foreach (TaggedData tagHyp in result.TagHyps)
            {
                foreach (SlotValue slot in tagHyp.Slots)
                {
                    if (!allowedIntentsSlots.Contains(slot.Name))
                        continue;

                    string rawResult = await GetBingLocalResults(slot.Value, latitude, longitude, originalRequest.Locale, cancelToken, realTime).ConfigureAwait(false);
                    ParseResults(rawResult, slot.Value, returnVal);
                    queryLogger.Log("LocalPlaces resolver found " + returnVal.EntityReferences.Count + " entities");
                }
            }

            timer.Stop();
            queryLogger.Log(CommonInstrumentation.GenerateInstancedLatencyEntry(CommonInstrumentation.Key_Latency_LU_Resolver, this.Name, timer), LogLevel.Ins);
            return returnVal;
        }

        private class AnnotationState
        {
            public IList<EntityReference> EntityReferences;
            public KnowledgeContext EntityContext;
        }


        public override async Task CommitAnnotation(
            object asyncState,
            RecoResult result,
            LURequest originalRequest,
            KnowledgeContext entityContext,
            IConfiguration modelConfig,
            ILogger queryLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            AnnotationState state = asyncState as AnnotationState;
            if (state == null)
            {
                await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
                return;
            }

            ISet<string> allowedIntentsSlots = base.GetEnabledSlots(result.Domain, result.Intent, modelConfig, queryLogger);
            foreach (TaggedData tagHyp in result.TagHyps)
            {
                foreach (SlotValue slot in tagHyp.Slots)
                {
                    if (!allowedIntentsSlots.Contains(slot.Name))
                        continue;
                    
                    foreach (EntityReference entity in state.EntityReferences)
                    {
                        slot.AddEntity(new Hypothesis<Entity>(state.EntityContext.GetEntityInMemory(entity.EntityId), entity.Relevance));
                    }
                }
            }
        }

        public override void Reset()
        {
        }

        private async Task<string> GetBingLocalResults(string query, double latitude, double longitude, LanguageCode locale, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            string finalUrl;
            finalUrl = string.Format(
                "/maps/search.ashx?wh=&n=11&cp=%22{0},{1}%22{2}&token={3}&q={4}",
                latitude,
                longitude,
                "&si=0&ob=&r=80&md=%221010,962%22&z=16&qh=&ep=&oj=&ai=%22eal%22&ca=&cid=&af=%22moderate%22&form=MPSRCH",
                _apiKey,
                WebUtility.UrlEncode(query));

            if (locale != null && !LanguageCode.UNDETERMINED.Equals(locale))
            {
                finalUrl += string.Format("&mkt=%22{0}%22&culture=%22{0}%22", locale);
            }

            using (HttpRequest request = HttpRequest.CreateOutgoing(finalUrl))
            using (HttpResponse response = await _httpClient.SendRequestAsync(request, cancelToken, realTime).ConfigureAwait(false))
            {
                try
                {
                    if (response == null || response.ResponseCode >= 300)
                    {
                        return null;
                    }

                    return await response.ReadContentAsStringAsync(cancelToken, realTime).ConfigureAwait(false);
                }
                finally
                {
                    if (response != null)
                    {
                        await response.FinishAsync(cancelToken, realTime).ConfigureAwait(false);
                    }
                }
            }
        }

        private void ParseResults(string json, string originalQuery, AnnotationState annotationState)
        {
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            JObject parsedObj = JObject.Parse(json);
            
            // Check for invalid parse or no result sets
            if (parsedObj == null ||
                parsedObj.SelectToken("primary.ResultSets[0].Results") == null)
            {
                return;
            }

            JToken topResultSet = parsedObj.SelectToken("primary.ResultSets[0]");
            JArray resultsList = topResultSet["Results"] as JArray;

            foreach (JToken result in resultsList.Children())
            {
                SchemaDotOrg.Place newEntity = new SchemaDotOrg.Place(annotationState.EntityContext);

                // Parse basic info
                if (result["Name"] != null)
                {
                    newEntity.Name.Value = result["Name"].Value<string>();
                }
                if (result["Website"] != null)
                {
                    newEntity.Url.Value = result["Website"].Value<string>();
                }
                if (result["PhoneNumber"] != null)
                {
                    newEntity.Telephone.Value = result["PhoneNumber"].Value<string>();
                }
                //if (result["Id"] != null)
                //{
                //    newEntity.EntityId = result["Id"].Value<string>();
                //}
                //if (topResultSet["ListingType"] != null)
                //{
                //    newEntity.EntityType = topResultSet["ListingType"].Value<string>();
                //}

                JToken address = result["Address"];

                // Parse addresses
                if (address != null)
                {
                    SchemaDotOrg.PostalAddress entityAddress = new SchemaDotOrg.PostalAddress(annotationState.EntityContext);
                    if (address["AddressLine"] != null)
                    {
                        entityAddress.StreetAddress.Value = address["AddressLine"].Value<string>();
                    }
                    if (address["Locality"] != null)
                    {
                        entityAddress.AddressLocality.Value = address["Locality"].Value<string>();
                    }
                    if (address["AdminDistrict"] != null)
                    {
                        entityAddress.AddressRegion.Value = address["AdminDistrict"].Value<string>();
                    }
                    if (address["CountryRegion"] != null)
                    {
                        entityAddress.AddressCountry_as_string.Value = address["CountryRegion"].Value<string>();
                    }
                    if (address["FormattedAddress"] != null)
                    {
                        entityAddress.Name.Value = address["FormattedAddress"].Value<string>();
                    }
                    if (address["PostalCode"] != null)
                    {
                        entityAddress.PostalCode.Value = address["PostalCode"].Value<string>();
                    }

                    newEntity.Address_as_PostalAddress.SetValue(entityAddress);
                }

                // Parse coordinates
                if (result.SelectToken("LocationData.Locations[0]") != null)
                {
                    SchemaDotOrg.GeoCoordinates coords = new SchemaDotOrg.GeoCoordinates(annotationState.EntityContext);
                    JToken location = result.SelectToken("LocationData.Locations[0]");
                    if (location["Latitude"] != null)
                    {
                        coords.Latitude_as_number.Value = location["Latitude"].Value<decimal>();
                    }
                    if (location["Longitude"] != null)
                    {
                        coords.Longitude_as_number.Value = location["Longitude"].Value<decimal>();
                    }

                    newEntity.Geo_as_GeoCoordinates.SetValue(coords);
                }

                annotationState.EntityReferences.Add(new EntityReference(newEntity.EntityId, 1.0f));
            }
        }
    }
}
