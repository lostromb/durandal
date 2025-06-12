using Durandal.Common.Time.Timex;
using Durandal.Common.Time.Timex.Enums;
using Durandal.Common.Cache;

namespace Durandal.Common.NLP.Annotation
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;

    using Durandal.API;

    using Durandal.Common.Config;
    using Durandal.Common.Logger;
    using Durandal.Common.File;
    using Durandal.Common.Utils;
    using System.Threading.Tasks;
    using Durandal.Common.Time;
    using Durandal.Common.Ontology;
    using Durandal.Common.Tasks;
    using System.Diagnostics;
    using Instrumentation;
    using System.Threading;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.Collections;

    public class TimexAnnotator : BasicConditionalAnnotator
    {
        private const int CACHE_CAPACITY = 10000; // arbitrary
        private readonly WorkSharingCache<TimexInputParams, IList<TimexMatch>> _cache;
        private readonly ILogger _logger;
        private readonly IFileSystem _fileSystem;
        private readonly LanguageCode _locale;

        private TimexMatcher _timexMatcher;

        public TimexAnnotator(IFileSystem fileSystem, LanguageCode locale, ILogger logger) : base("Timex")
        {
            _cache = new WorkSharingCache<TimexInputParams, IList<TimexMatch>>(TimexMatchInternal, cacheLifetime: TimeSpan.FromSeconds(30), cacheCapacity: CACHE_CAPACITY);
            _fileSystem = fileSystem;
            _logger = logger;
            _locale = locale;
        }

        public override string Name
        {
            get
            {
                return "timex";
            }
        }

        public override bool Initialize()
        {
            VirtualPath timexFile = new VirtualPath(RuntimeDirectoryName.MISCDATA_DIR + "\\" + _locale.ToBcp47Alpha2String() + "\\timex_grammar.xml");
            if (_fileSystem.Exists(timexFile))
            {
                try
                {
                    _logger.Log("Loading Timex grammar...");
                    this._timexMatcher = new TimexMatcher(_fileSystem.OpenStream(timexFile, FileOpenMode.Open, FileAccessMode.Read));
                    _logger.Log("Done!");
                    return true;
                }
                catch (IOException e)
                {
                    _logger.Log("Could not load Timex grammar file. " + e.Message, LogLevel.Err);
                }
                catch (FormatException e)
                {
                    _logger.Log("Grammar exception in timex code", LogLevel.Err);
                    _logger.Log(e, LogLevel.Err);
                }
            }

            return false;
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
            if (this._timexMatcher == null)
            {
                queryLogger.Log("Timex annotator was called but no matcher was loaded! This usually means a grammar file is missing for locale " + originalRequest.Locale, LogLevel.Wrn);
                await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
                return;
            }

            List<TimexMatch> allTimeMatches = new List<TimexMatch>();
            DateTimeOffset referenceDateTime = default(DateTimeOffset);
            bool hasReferenceDateTime = !string.IsNullOrEmpty(originalRequest.ReferenceDateTime) &&
                                        DateTimeOffset.TryParse(originalRequest.ReferenceDateTime, out referenceDateTime);

            if (!hasReferenceDateTime)
            {
                return;
            }

            Stopwatch timer = Stopwatch.StartNew();
            ISet<string> allowedIntentsSlots = base.GetEnabledSlots(result.Domain, result.Intent, modelConfig, queryLogger);

            // Run annotators at the slot level
            foreach (TaggedData tagHyp in result.TagHyps)
            {
                foreach (SlotValue slot in tagHyp.Slots)
                {
                    // Is timex enabled for this slot?
                    if (!allowedIntentsSlots.Contains(slot.Name))
                        continue;
                    
                    // Tag time expressions in the results
                    TimexContext timexContext = new TimexContext()
                        {
                            TemporalType = TemporalType.All,
                            UseInference = true,
                            ReferenceDateTime = referenceDateTime.DateTime
                        };

                    TimexInputParams parameters = new TimexInputParams()
                    {
                        task_logger = queryLogger,
                        task_query = slot.Value,
                        task_context = timexContext
                    };

                    IList<TimexMatch> rawTimeMatches = _cache.ProduceValue(parameters, realTime, cancelToken, timeout: TimeSpan.FromMilliseconds(500));

                    // Convert the raw timex matches into an intermediate form, so we can serialize it
                    // as part of the RecoResult object
                    if (rawTimeMatches != null)
                    {
                        foreach (TimexMatch m in rawTimeMatches)
                        {
                            slot.AddTimexMatch(m);
                        }

                        allTimeMatches.FastAddRangeList(rawTimeMatches);
                    }
                    else
                    {
                        queryLogger.Log("Null timex matches came back for input \"" + slot.Value + "\". Timeout?", LogLevel.Wrn);
                    }
                }
            }
            
            timer.Stop();
            queryLogger.Log(CommonInstrumentation.GenerateInstancedLatencyEntry(CommonInstrumentation.Key_Latency_LU_Resolver, this.Name, timer), LogLevel.Ins);
        }

        public override void Reset()
        {
        }

        private IList<TimexMatch> TimexMatchInternal(TimexInputParams parameters, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _timexMatcher.Matches(parameters.task_query, parameters.task_context);
        }

        /// <summary>
        /// Used to encapsulate the input to a annotation request for the sake of making
        /// queries reusable inside the worksharing cache
        /// </summary>
        private class TimexInputParams
        {
            public ILogger task_logger;
            public string task_query;
            public TimexContext task_context;

            public override bool Equals(object obj)
            {
                if (obj == null || GetType() != obj.GetType())
                    return false;

                TimexInputParams other = (TimexInputParams)obj;
                if (!string.Equals(task_query, other.task_query))
                    return false;
                if (task_context != null && other.task_context != null)
                {
                    if (task_context.AmPmInferenceCutoff != other.task_context.AmPmInferenceCutoff)
                        return false;
                    if (task_context.Normalization != other.task_context.Normalization)
                        return false;
                    if (task_context.ReferenceDateTime != other.task_context.ReferenceDateTime)
                        return false;
                    if (task_context.TemporalType != other.task_context.TemporalType)
                        return false;
                    if (task_context.UseInference != other.task_context.UseInference)
                        return false;
                    if (task_context.WeekdayLogicType != other.task_context.WeekdayLogicType)
                        return false;
                }

                return true;
            }

            public override int GetHashCode()
            {
                int hashCode = 0;
                if (task_query != null)
                    hashCode += task_query.GetHashCode();
                if (task_context != null)
                    hashCode += task_context.ReferenceDateTime.GetHashCode();
                return hashCode;
            }
        }
    }
}
