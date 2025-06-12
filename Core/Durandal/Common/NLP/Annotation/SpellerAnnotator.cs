
namespace Durandal.Common.NLP.Annotation
{
    using Durandal.API;
        using Durandal.Common.Cache;
    using Durandal.Common.Config;
    using Durandal.Common.Dialog;
    using Durandal.Common.Logger;
    using Durandal.Common.Net.Http;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.Ontology;
    using Durandal.Common.Statistics;
    using Durandal.Common.Time;
    using Durandal.ExternalServices.Bing.Speller;
    using Instrumentation;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// LU Annotator which applies spell correction to slot values
    /// </summary>
    public class SpellerAnnotator : BasicConditionalAnnotator
    {
        private const string ConfigKeyName = "SlotAnnotator_Speller";
        private const int MAX_CACHE_SIZE = 10000; // arbitrary size

        private readonly WorkSharingCacheAsync<SpellerInputParams, IList<Hypothesis<string>>> _cache;
        private readonly string _apiKey;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger _logger;
        private BingSpeller _api;

        public SpellerAnnotator(string apiKey, IHttpClientFactory httpClientFactory, ILogger logger) : base("Speller")
        {
            _cache = new WorkSharingCacheAsync<SpellerInputParams, IList<Hypothesis<string>>>(SpellcheckInternal, TimeSpan.FromMinutes(30), MAX_CACHE_SIZE);
            _apiKey = apiKey;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public override string Name
        {
            get
            {
                return "speller";
            }
        }

        public override bool Initialize()
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.Log("API key not provided to Speller annotator; annotation will not run", LogLevel.Err);
                return false;
            }

            _api = new BingSpeller(_apiKey, _httpClientFactory, _logger);
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
            ISet<string> allowedIntentsSlots = base.GetEnabledSlots(result.Domain, result.Intent, modelConfig, queryLogger);
            Stopwatch timer = Stopwatch.StartNew();

            // FIXME This needs to be moved into the stateless annotation pattern
            foreach (TaggedData tagHyp in result.TagHyps)
            {
                foreach (SlotValue slot in tagHyp.Slots)
                {
                    if (!allowedIntentsSlots.Contains(slot.Name))
                    {
                        continue;
                    }

                    SpellerInputParams parameters = new SpellerInputParams()
                    {
                        task_logger = queryLogger,
                        task_query = slot.Value,
                        task_locale = originalRequest.Locale
                    };

                    try
                    {
                        IList<Hypothesis<string>> spellSuggestions = await _cache.ProduceValue(parameters, realTime, cancelToken, timeout: TimeSpan.FromSeconds(5)).ConfigureAwait(false);

                        if (spellSuggestions == null)
                        {
                            queryLogger.Log($"Null spelling suggestions came back for input \"{slot.Value}\". Service timeout?", LogLevel.Wrn);
                        }
                        else
                        {
                            if (spellSuggestions.Count > 0)
                            {
                                string[] suggestionStrings = new string[spellSuggestions.Count];
                                for (int c = 0; c < spellSuggestions.Count; c++)
                                {
                                    suggestionStrings[c] = spellSuggestions[c].Value;
                                }

                                slot.SetProperty(SlotPropertyName.SpellSuggestions, string.Join("\n", suggestionStrings));
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        queryLogger.Log(e);
                    }
                }
            }

            timer.Stop();
            queryLogger.Log(CommonInstrumentation.GenerateInstancedLatencyEntry(CommonInstrumentation.Key_Latency_LU_Resolver, this.Name, timer), LogLevel.Ins);
        }

        public override void Reset()
        {
        }

        private Task<IList<Hypothesis<string>>> SpellcheckInternal(SpellerInputParams parameters, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _api.SpellCorrect(parameters.task_query, parameters.task_locale, parameters.task_logger, cancelToken, realTime);
        }

        /// <summary>
        /// Used to encapsulate the input to a annotation request for the sake of making
        /// queries reusable inside the worksharing cache
        /// </summary>
        private class SpellerInputParams
        {
            public ILogger task_logger;
            public string task_query;
            public LanguageCode task_locale;

            public override bool Equals(object obj)
            {
                if (obj == null || GetType() != obj.GetType())
                    return false;

                SpellerInputParams other = (SpellerInputParams)obj;
                if (!string.Equals(task_query, other.task_query))
                    return false;
                if (!object.Equals(task_locale, other.task_locale))
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
                return hashCode;
            }
        }
    }
}
