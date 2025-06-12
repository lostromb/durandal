using Durandal.API;
using Durandal.Common.Cache;
using Durandal.Common.Config;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Net.Http;
using Durandal.Common.NLP.Language;
using Durandal.Common.Ontology;
using Durandal.Common.Statistics;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.ExternalServices.Bing.Maps;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.NLP.Annotation
{
    /// <summary>
    /// LU Annotator which resolves location references ("Long beach") into actual city / province / country entities.
    /// </summary>
    public class LocationEntityAnnotator : BasicConditionalAnnotator
    {
        private const int MAX_CACHE_SIZE = 10000; // arbitrary size

        private readonly string _apiKey;
        private readonly WorkSharingCacheAsync<AnnotationInputParams, IList<Hypothesis<BingMapsPlace>>> _cache;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger _logger;
        private BingMaps _bingMaps;

        public LocationEntityAnnotator(string bingMapsApiKey, IHttpClientFactory httpClientFactory, ILogger logger) : base("LocationEntity")
        {
            _httpClientFactory = httpClientFactory;
            _apiKey = bingMapsApiKey;
            _logger = logger;
            _cache = new WorkSharingCacheAsync<AnnotationInputParams, IList<Hypothesis<BingMapsPlace>>> (
                ResolveLocationInternal,
                TimeSpan.FromMinutes(30),
                MAX_CACHE_SIZE);
        }

        public override string Name
        {
            get
            {
                return "location";
            }
        }

        public override bool Initialize()
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.Log("API key not provided to LocationEntity annotator; annotation will not run", LogLevel.Err);
                return false;
            }

            _bingMaps = new BingMaps(_apiKey, _httpClientFactory, _logger.Clone("BingMaps"));
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
            if (result.TagHyps.Count == 0)
            {
                return null;
            }

            Stopwatch timer = Stopwatch.StartNew();
            Dictionary<string, Task<IList<Hypothesis<BingMapsPlace>>>> resolverTasks = new Dictionary<string, Task<IList<Hypothesis<BingMapsPlace>>>>();
            double? latitude = originalRequest.Context.Latitude;
            double? longitude = originalRequest.Context.Longitude;
            ISet<string> allowedIntentsSlots = base.GetEnabledSlots(result.Domain, result.Intent, modelConfig, queryLogger);

            foreach (TaggedData tagHyp in result.TagHyps)
            {
                foreach (SlotValue slot in tagHyp.Slots)
                {
                    if (!allowedIntentsSlots.Contains(slot.Name))
                    {
                        continue;
                    }

                    // Start each request but do not await it serially, since we may be resolving multiple locations in the same utterance
                    resolverTasks[slot.Value] = ResolveLocation(queryLogger, slot.Value, latitude, longitude, originalRequest.Locale, realTime, cancelToken);
                }
            }

            // Now await all of the requests in parallel
            Dictionary<string, IList<Hypothesis<BingMapsPlace>>> returnVal = new Dictionary<string, IList<Hypothesis<BingMapsPlace>>>();
            foreach (var kvp in resolverTasks)
            {
                returnVal[kvp.Key] = await kvp.Value.ConfigureAwait(false);
            }

            timer.Stop();
            queryLogger.Log(CommonInstrumentation.GenerateInstancedLatencyEntry(CommonInstrumentation.Key_Latency_LU_Resolver, this.Name, timer), LogLevel.Ins);
            return returnVal;
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
                await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
                return;
            }

            if (asyncState == null)
            {
                queryLogger.Log("Asynchronous annotation failed, state is null!", LogLevel.Err);
                return;
            }

            if (!(asyncState is Dictionary<string, IList<Hypothesis<BingMapsPlace>>>))
            {
                queryLogger.Log("Asynchronous annotation failed, state is not in the expected format!", LogLevel.Err);
                return;
            }

            Dictionary<string, IList<Hypothesis<BingMapsPlace>>> asyncResults = asyncState as Dictionary<string, IList<Hypothesis<BingMapsPlace>>>;

            ISet<string> allowedIntentsSlots = base.GetEnabledSlots(result.Domain, result.Intent, modelConfig, queryLogger);

            foreach (TaggedData tagHyp in result.TagHyps)
            {
                foreach (SlotValue slot in tagHyp.Slots)
                {
                    if (!allowedIntentsSlots.Contains(slot.Name))
                    {
                        continue;
                    }

                    if (!asyncResults.ContainsKey(slot.Value))
                    {
                        queryLogger.Log("No location entities stored for input \"" + slot.Value + "\".", LogLevel.Wrn);
                        continue;
                    }

                    IList<Hypothesis<BingMapsPlace>> allEntities = asyncResults[slot.Value];

                    if (allEntities == null)
                    {
                        queryLogger.Log("Null location entities came back for input \"" + slot.Value + "\". Service timeout?", LogLevel.Wrn);
                    }
                    else
                    {
                        queryLogger.Log("LocationEntity resolver found " + allEntities.Count + " entities named \"" + slot.Value + "\"", LogLevel.Vrb);
                        
                        // Add all entity results to the output
                        foreach (Hypothesis<BingMapsPlace> entity in allEntities)
                        {
                            // Convert to schema.org entity format
                            Entity schemaDotOrgPlace = entity.Value.ConvertToSchemaDotOrg(entityContext);
                            slot.AddEntity(new Hypothesis<Entity>(entityContext.GetEntityInMemory(schemaDotOrgPlace.EntityId), entity.Conf));
                        }
                    }
                }
            }
        }

        public override void Reset()
        {
        }

        private async Task<IList<Hypothesis<BingMapsPlace>>> ResolveLocation(
            ILogger queryLogger,
            string query,
            double? latitude,
            double? longitude,
            LanguageCode locale,
            IRealTimeProvider realTime,
            CancellationToken cancelToken)
        {
            AnnotationInputParams parameters = new AnnotationInputParams()
            {
                task_logger = queryLogger,
                task_query = query,
                task_latitude = latitude,
                task_longitude = longitude,
                task_locale = locale
            };

            try
            {
                return await _cache.ProduceValue(parameters, realTime, cancelToken, timeout: TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                queryLogger.Log(e, LogLevel.Err);
                return new List<Hypothesis<BingMapsPlace>>();
            }
        }

        private async Task<IList<Hypothesis<BingMapsPlace>>> ResolveLocationInternal(AnnotationInputParams parameters, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            GeoCoordinate userLoc = default(GeoCoordinate);
            if (parameters.task_latitude.HasValue && parameters.task_longitude.HasValue)
            {
                userLoc = new GeoCoordinate(parameters.task_latitude.Value, parameters.task_longitude.Value);
            }
            
            return await _bingMaps.Query(parameters.task_query, parameters.task_logger, cancelToken, realTime, parameters.task_locale, userLoc).ConfigureAwait(false);
        }
        
        /// <summary>
        /// Used to encapsulate the input to a annotation request for the sake of making
        /// queries reusable inside the worksharing cache
        /// </summary>
        private class AnnotationInputParams
        {
            public ILogger task_logger;
            public string task_query;
            public double? task_latitude;
            public double? task_longitude;
            public LanguageCode task_locale;

            public override bool Equals(object obj)
            {
                if (obj == null || GetType() != obj.GetType())
                    return false;

                AnnotationInputParams other = (AnnotationInputParams)obj;
                if (!string.Equals(task_query, other.task_query))
                    return false;
                if (!object.Equals(task_locale, other.task_locale))
                    return false;
                if (!task_latitude.Equals(other.task_latitude))
                    return false;
                if (!task_longitude.Equals(other.task_longitude))
                    return false;
                return true;
            }

            public override int GetHashCode()
            {
                int hashCode = 0;
                if (task_query != null)
                    hashCode += task_query.GetHashCode();
                if (task_locale != null)
                    hashCode += task_locale.GetHashCode();
                hashCode += task_latitude.GetValueOrDefault().GetHashCode();
                hashCode += task_longitude.GetValueOrDefault().GetHashCode();
                return hashCode;
            }
        }
    }
}
