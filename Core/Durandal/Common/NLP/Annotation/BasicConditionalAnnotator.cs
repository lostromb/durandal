using Durandal.API;
using Durandal.Common.Config;
using Durandal.Common.Logger;
using Durandal.Common.File;
using Durandal.Common.Ontology;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Time;
using System.Threading;

namespace Durandal.Common.NLP.Annotation
{
    public abstract class BasicConditionalAnnotator : IAnnotator
    {
        private readonly string ConfigKeyName;

        public BasicConditionalAnnotator(string configKey)
        {
            ConfigKeyName = "SlotAnnotator_" + configKey;
        }

        /// <inheritdoc/>
        public virtual Task<object> AnnotateStateless(
            RecoResult result,
            LURequest originalRequest,
            IConfiguration modelConfig,
            ILogger queryLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            return Task.FromResult<object>(null);
        }

        /// <inheritdoc/>
        public abstract Task CommitAnnotation(
            object asyncState,
            RecoResult result,
            LURequest originalRequest,
            KnowledgeContext entityContext,
            IConfiguration modelConfig,
            ILogger queryLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime);

        public abstract void Reset();

        public abstract string Name { get; }

        public abstract bool Initialize();

        /// <summary>
        /// Parses a model config and tries to see what slots ordinals are enabled for.
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="intent"></param>
        /// <param name="modelConfig"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        protected ISet<string> GetEnabledSlots(string domain, string intent, IConfiguration modelConfig, ILogger logger)
        {
            ISet<string> returnVal = new HashSet<string>();
            if (modelConfig.ContainsKey(ConfigKeyName))
            {
                foreach (string enabledSlot in modelConfig.GetStringList(ConfigKeyName))
                {
                    string[] parts = enabledSlot.Split('/');
                    if (parts.Length != 2)
                    {
                        logger.Log("Malformed configuration line in model config for " + domain + ": " + ConfigKeyName + "=" + enabledSlot, LogLevel.Err);
                        continue;
                    }

                    if (parts[0].Equals(intent))
                    {
                        returnVal.Add(parts[1]);
                    }
                }
            }
            return returnVal;
        }
    }
}
