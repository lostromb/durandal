using Durandal.API;
using Durandal.Common.Config;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Ontology;
using Durandal.Common.Statistics;
using Durandal.Common.Time;
using Durandal.Common.Utils;
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
    public class PersonEntityAnnotator : BasicConditionalAnnotator
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger _logger;
        private IHttpClient _httpClient;

        public PersonEntityAnnotator(IHttpClientFactory httpClientFactory, ILogger logger) : base("PersonEntity")
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        public override string Name
        {
            get
            {
                return "person";
            }
        }

        public override bool Initialize()
        {
            _httpClient = _httpClientFactory.CreateHttpClient("www.googleapis.com", 443, true, _logger);
            return true;
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
            if (result.TagHyps.Count == 0)
            {
                return;
            }

            // FIXME this needs to be moved into the stateless annotation pattern
            Stopwatch timer = Stopwatch.StartNew();
            double latitude = originalRequest.Context.Latitude.GetValueOrDefault(0);
            double longitude = originalRequest.Context.Longitude.GetValueOrDefault(0);

            ISet<string> allowedIntentsSlots = base.GetEnabledSlots(result.Domain, result.Intent, modelConfig, queryLogger);

            foreach (TaggedData tagHyp in result.TagHyps)
            {
                foreach (SlotValue slot in tagHyp.Slots)
                {
                    if (!allowedIntentsSlots.Contains(slot.Name))
                        continue;
                    
                    string rawResult = await GetFreebaseResults(slot.Value, queryLogger, cancelToken, realTime).ConfigureAwait(false);
                    IList<SchemaDotOrg.Person> allEntities = ParseResults(entityContext, rawResult, queryLogger);
                    queryLogger.Log("PersonEntity resolver found " + allEntities.Count + " entities");
                    // Add all entity results to the output
                    foreach (SchemaDotOrg.Person entity in allEntities)
                    {
                        slot.AddEntity(new Hypothesis<Entity>(entity, 1.0f));
                    }
                }
            }

            timer.Stop();
            queryLogger.Log(CommonInstrumentation.GenerateInstancedLatencyEntry(CommonInstrumentation.Key_Latency_LU_Resolver, this.Name, timer), LogLevel.Ins);
        }

        public override void Reset()
        {
        }

        private async Task<string> GetFreebaseResults(string query, ILogger logger, CancellationToken cancelToken, IRealTimeProvider realTime, string languageCode = "/lang/en")
        {
            try
            {
                string finalUrl = string.Format(
                    "/freebase/v1/mqlread/?lang={0}&query={1}",
                    languageCode,
                    WebUtility.UrlEncode("[{\"name~=\":\"" + query + "\",\"type\":\"/people/person\",\"limit\":10,\"name\":null,\"date_of_birth\":null,\"place_of_birth\":null,\"profession\":[],\"gender\":null,\"mid\":null,\"/common/topic/official_website\":[{}],\"/common/topic/topic_equivalent_webpage\":[{}],\"/common/topic/alias\":[{\"value\": null,\"optional\":true}]}]")
                    );
                using (HttpRequest request = HttpRequest.CreateOutgoing(finalUrl, "GET"))
                using (NetworkResponseInstrumented<HttpResponse> netResp = await _httpClient.SendInstrumentedRequestAsync(
                        request, cancelToken, realTime, logger).ConfigureAwait(false))
                {
                    try
                    {
                        if (netResp == null || !netResp.Success)
                        {
                            return string.Empty;
                        }

                        HttpResponse response = netResp.Response;
                        return await response.ReadContentAsStringAsync(cancelToken, realTime).ConfigureAwait(false);
                    }
                    finally
                    {
                        if (netResp != null)
                        {
                            await netResp.FinishAsync(cancelToken, realTime).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                logger.Log("Caught exception while querying people resolver service: " + e.Message, LogLevel.Err);
                return string.Empty;
            }
        }

        private static IList<SchemaDotOrg.Person> ParseResults(KnowledgeContext entityContext, string json, ILogger queryLogger)
        {
            IList<SchemaDotOrg.Person> returnVal = new List<SchemaDotOrg.Person>();

            if (string.IsNullOrEmpty(json))
            {
                return returnVal;
            }

            JObject parsedObj = JObject.Parse(json);
            if (parsedObj == null)
            {
                return returnVal;
            }

            JArray resultsArray = parsedObj["result"] as JArray;
            if (resultsArray == null)
            {
                return returnVal;
            }

            foreach (JObject result in resultsArray.Children<JObject>())
            {
                if (result != null)
                {
                    string entityId = null;
                    if (result["mid"] != null)
                    {
                        entityId = "freebase://" + result["mid"].Value<string>();
                    }

                    SchemaDotOrg.Person newEntity = new SchemaDotOrg.Person(entityContext, entityId);
                    if (result["name"] != null)
                    {
                        newEntity.Name.Value = result["name"].Value<string>();
                    }
                    if (result["gender"] != null)
                    {
                        string genderString = result["gender"].Value<string>();
                        if (string.Equals("Male", genderString))
                        {
                            newEntity.Gender_as_GenderType.SetValue(new SchemaDotOrg.Male(entityContext));
                        }
                        else if (string.Equals("Female", genderString))
                        {
                            newEntity.Gender_as_GenderType.SetValue(new SchemaDotOrg.Female(entityContext));
                        }
                    }
                    if (result["place_of_birth"] != null)
                    {
                        SchemaDotOrg.Place birthPlace = new SchemaDotOrg.Place(entityContext);
                        birthPlace.Name.Value = result["place_of_birth"].Value<string>();
                        newEntity.BirthPlace.SetValue(birthPlace);
                    }
                    if (result["date_of_birth"] != null)
                    {
                        string value = result["date_of_birth"].Value<string>();
                        if (!string.IsNullOrEmpty(value))
                        {
                            newEntity.BirthDate.Value = Durandal.Common.Ontology.DateTimeEntity.FromIso8601(value);
                        }
                    }
                    if (result["profession"] != null)
                    {
                        JArray professionArray = result["profession"] as JArray;
                        if (professionArray != null)
                        {
                            foreach (var profession in professionArray.Children())
                            {
                                string value = profession.Value<string>();
                                if (!newEntity.JobTitle.List.Contains(value))
                                {
                                    newEntity.JobTitle.Add(value);
                                }
                            }
                        }
                    }
                    if (result["/common/topic/official_website"] != null)
                    {
                        JArray websiteArray = result["/common/topic/official_website"] as JArray;
                        foreach (JObject websiteObject in websiteArray.Children<JObject>())
                        {
                            newEntity.Url.Value = websiteObject["value"].Value<string>();
                            break;
                        }
                    }
                    if (result["/common/topic/alias"] != null)
                    {
                        JArray aliasArray = result["/common/topic/alias"] as JArray;
                        foreach (JObject aliasObject in aliasArray.Children<JObject>())
                        {
                            string value = aliasObject["value"].Value<string>();
                            if (!newEntity.AlternateName.List.Contains(value))
                            {
                                newEntity.AlternateName.Add(value);
                            }
                        }
                    }
                    if (result["/common/topic/topic_equivalent_webpage"] != null)
                    {
                        JArray websiteArray = result["/common/topic/topic_equivalent_webpage"] as JArray;
                        foreach (JObject websiteObject in websiteArray.Children<JObject>())
                        {
                            string value = websiteObject["value"].Value<string>();
                            newEntity.MainEntityOfPage_as_URL.Add(value);
                        }
                    }
                    returnVal.Add(newEntity);
                }
            }

            return returnVal;
        }
    }
}
