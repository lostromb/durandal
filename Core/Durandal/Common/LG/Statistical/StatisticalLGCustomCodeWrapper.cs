using Durandal.API;
using System;
using System.Collections.Generic;
using System.Text;
using Durandal.Common.Logger;
using System.Threading.Tasks;
using Durandal.Common.NLP.Language;

namespace Durandal.Common.LG.Statistical
{
    public class StatisticalLGCustomCodeWrapper : BaseLGPattern
    {
        // Static shared state
        private readonly LgCommon.RunLanguageGeneration _methodImpl;

        // Unique to each clone
        private readonly Dictionary<string, object> _substitutions = new Dictionary<string, object>();
        private readonly ClientContext _clientContext;
        private readonly ILogger _queryLogger;

        public StatisticalLGCustomCodeWrapper(string phraseName, LgCommon.RunLanguageGeneration implementation, LanguageCode locale, ILogger globalLogger)
        {
            _methodImpl = implementation;
            Name = phraseName;
            Locale = locale;
            _clientContext = null;
            _queryLogger = globalLogger;
        }

        private StatisticalLGCustomCodeWrapper(string phraseName, LgCommon.RunLanguageGeneration implementation, LanguageCode locale, ClientContext newContext, ILogger logger)
        {
            _methodImpl = implementation;
            Name = phraseName;
            Locale = locale;
            _clientContext = newContext;
            _queryLogger = logger;
        }

        public override LanguageCode Locale
        {
            get;
            internal set;
        }

        public override string Name
        {
            get;
            internal set;
        }

        protected override ClientContext CurrentClientContext
        {
            get
            {
                return _clientContext;
            }
        }

        public override ILGPattern Clone(ILogger logger, ClientContext newClientContext, bool debug)
        {
            return new StatisticalLGCustomCodeWrapper(Name, _methodImpl, Locale, newClientContext, logger);
        }

        public override Task<RenderedLG> Render()
        {
            return Task.FromResult(_methodImpl(_substitutions, _queryLogger, _clientContext));
        }

        public override ILGPattern Sub(string key, object value)
        {
            _substitutions[key] = value;
            return this;
        }
    }
}
