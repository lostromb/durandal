
namespace Durandal.Common.LG.Template
{
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using Durandal.API;
        using Durandal.Common.Utils;
    using Durandal.Common.Logger;
    using System.Threading.Tasks;
    using Durandal.Common.NLP.Language;

    /// <summary>
    /// This class specifies a single set of substitutions that are applied to a fixed string to create a basic sentence.
    /// This is the most basic type of LG, and simple relies on keyword insertion into a pattern without considering
    /// grammar or such.
    /// Each plugin that requests an LGPattern will get a unique clone of that pattern, to make sure that plugin's
    /// changes are immutable and thread-safe.
    /// </summary>
    public class TemplateBasedLGPattern : BaseLGPattern
    {
        // These fields are constant for all patterns of the same type
        // They contain the raw, unsubstituted template values for their respective fields
        private string _text;
        private string _shortText;
        private string _spoken;
        private IDictionary<string, string> _extraFields;
        private readonly LgCommon.RunLanguageGeneration _mainMethod;

        // These fields can change in between clonings of patterns, and are considered query-specific
        private readonly IDictionary<string, object> _substitutions = new Dictionary<string, object>();
        private readonly ILogger _logger;
        private readonly ClientContext _currentClientContext;

        public override string Name { get; internal set; }
        public override LanguageCode Locale { get; internal set; }

        protected override ClientContext CurrentClientContext
        {
            get
            {
                return _currentClientContext;
            }
        }

        public void SetTextTemplate(string value)
        {
            _text = value;
        }

        public void SetShortTextTemplate(string value)
        {
            _shortText = value;
        }

        public void SetSpokenTemplate(string value)
        {
            _spoken = value;
        }

        public void SetExtraField(string key, string value)
        {
            if (_extraFields.ContainsKey(key))
            {
                _extraFields.Remove(key);
            }

            _extraFields[key] = value;
        }

        public TemplateBasedLGPattern(ILogger logger, ClientContext newClientContext)
        {
            Name = string.Empty;
            Locale = null;
            _text = string.Empty;
            _shortText = string.Empty;
            _spoken = string.Empty;
            _extraFields = new Dictionary<string, string>();
            _logger = logger;
            _mainMethod = DefaultLogic;
            _currentClientContext = newClientContext;
        }

        public TemplateBasedLGPattern(LgCommon.RunLanguageGeneration customLogic, ILogger logger, ClientContext newClientContext)
        {
            Name = string.Empty;
            Locale = null;
            _text = string.Empty;
            _shortText = string.Empty;
            _spoken = string.Empty;
            _extraFields = new Dictionary<string, string>();
            _logger = logger;
            _mainMethod = customLogic;
            _currentClientContext = newClientContext;
        }

        /// <summary>
        /// Creates a new instance of this LG pattern. This method will be called to create a new object before every single
        /// query is processed, so this constructor will accept query-specific data to be stored for later.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="newClientContext"></param>
        /// <param name="debug"></param>
        /// <returns></returns>
        public override ILGPattern Clone(ILogger logger, ClientContext newClientContext, bool debug) // TODO plumb debug flag
        {
            return new TemplateBasedLGPattern(_mainMethod, logger, newClientContext)
            {
                Name = this.Name,
                Locale = this.Locale,
                _text = this._text,
                _shortText = this._shortText,
                _spoken = this._spoken,
                _extraFields = new Dictionary<string, string>(this._extraFields)
            };
        }

        /// <summary>
        /// Substitutes a value for one of the keyword slots in this pattern (specified in the pattern by {keyword} )
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public override ILGPattern Sub(string key, object value)
        {
            // New substitutions will replace old ones, rather than throwing an exception
            _substitutions[key] = value;
            return this;
        }

        public override Task<RenderedLG> Render()
        {
            return Task.FromResult(_mainMethod(_substitutions, _logger, _currentClientContext));
        }

        private static string ApplySubstitutions(string field, IDictionary<string, object> localSubstitutions)
        {
            string returnVal = field;
            foreach (KeyValuePair<string, object> sub in localSubstitutions)
            {
                string stringVal;
                if (sub.Value.GetType() == typeof(string))
                {
                    stringVal = (string)sub.Value;
                }
                else
                {
                    stringVal = sub.Value.ToString();
                }

                Regex matcher = new Regex("\\{" + sub.Key + "\\}");
                returnVal = StringUtils.RegexReplace(matcher, returnVal, stringVal);
            }

            return returnVal;
        }

        /// <summary>
        /// The default LG substitution logic. This will perform basic string substitution for all
        /// components of the given pattern.
        /// </summary>
        /// <param name="localSubstitutions"></param>
        /// <param name="logger"></param>
        /// <param name="clientContext"></param>
        /// <returns></returns>
        private RenderedLG DefaultLogic(
            IDictionary<string, object> localSubstitutions,
            ILogger logger,
            ClientContext clientContext)
        {
            RenderedLG returnVal = new RenderedLG();

            Dictionary<string, string> extraFieldsWithSubs = new Dictionary<string, string>();
            foreach (string fieldName in _extraFields.Keys)
            {
                extraFieldsWithSubs[fieldName] = ApplySubstitutions(_extraFields[fieldName], localSubstitutions);
            }
            
            // Apply the substitutions, using "Text" as the default fallback for empty fields
            returnVal.Text = ApplySubstitutions(_text, localSubstitutions);
            returnVal.ShortText = string.IsNullOrEmpty(_shortText) ? returnVal.Text : ApplySubstitutions(_shortText, localSubstitutions);
            returnVal.Spoken = string.IsNullOrEmpty(_spoken) ? returnVal.Text : ApplySubstitutions(_spoken, localSubstitutions);
            returnVal.ExtraFields = extraFieldsWithSubs;
            return returnVal;
        }
    }
}
