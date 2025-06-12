using Durandal.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Logger;
using Durandal.Common.NLP.Language;

namespace Durandal.Common.LG
{
    public class NullLGEngine : ILGEngine
    {
        public ILGPattern GetPattern(string patternName, ClientContext clientContext, ILogger logger = null, bool debug = false, int? phraseNum = null)
        {
            return new NullLGPattern(patternName, clientContext.Locale);
        }

        public Task<string> GetText(string patternName, ClientContext clientContext, ILogger logger = null, bool debug = false, int? phraseNum = null)
        {
            return Task.FromResult("NULL_PATTERN_" + patternName);
        }

        public void RegisterCustomCode(string patternName, LgCommon.RunLanguageGeneration method, LanguageCode locale)
        {
        }
    }

    public class NullLGPattern : BaseLGPattern
    {
        private string _patternName;
        private LanguageCode _locale;

        public NullLGPattern(string patternName, LanguageCode locale)
        {
            _patternName = patternName;
            _locale = locale;
        }

        public override LanguageCode Locale
        {
            get
            {
                return _locale;
            }
            internal set
            {
                _locale = value;
            }
        }

        public override string Name
        {
            get
            {
                return "NULL_PATTERN_" + _patternName;
            }
            internal set
            {
                _patternName = value;
            }
        }

        protected override ClientContext CurrentClientContext
        {
            get
            {
                return new ClientContext();
            }
        }

        public override ILGPattern Clone(ILogger logger, ClientContext newClientContext, bool debug)
        {
            return this;
        }

        public override Task<RenderedLG> Render()
        {
            return Task.FromResult(new RenderedLG()
            {
                Text = Name,
                ShortText = Name,
                Spoken = Name,
                ExtraFields = new Dictionary<string, string>(),
            });
        }

        public override ILGPattern Sub(string key, object value)
        {
            return this;
        }
    }
}
