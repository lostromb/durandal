using Durandal.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Logger;
using Durandal.Common.LG.Statistical.Transformers;
using Durandal.Common.Utils;
using Durandal.Common.MathExt;
using Durandal.Common.NLP;
using Durandal.Common.Config;
using Durandal.Common.NLP.Language;

namespace Durandal.Common.LG.Statistical
{
    public class StatisticalLGPattern : BaseLGPattern
    {
        #region Static fields shared between clones

        private StatisticalLGEngine _owningEngine; // Allows us to cross-reference models, translation tables, and subphrases
        private IDictionary<string, List<ISlotTransformer>> _transformers; // maps from slot name to transformer chain
        private IList<string> _scripts;
        private LocalizedKey _textModel;
        private LocalizedKey _shortTextModel;
        private LocalizedKey _spokenModel;
        private IDictionary<string, string> _extraFields;
        private INLPToolsCollection _nlTools;

        #endregion

        #region These fields can change in between clonings of patterns, and are considered query-specific

        private readonly IDictionary<string, object> _substitutions;
        private readonly ILogger _logger;
        private readonly ClientContext _currentClientContext;
        private readonly bool _debugMode = false;

        #endregion

        public StatisticalLGPattern(
            ILogger logger,
            ClientContext emptyClientContext,
            INLPToolsCollection nlTools,
            StatisticalLGEngine owningEngine)
        {
            Name = string.Empty;
            Locale = null;
            _textModel = null;
            _shortTextModel = null;
            _spokenModel = null;
            _extraFields = new Dictionary<string, string>();
            _substitutions = new Dictionary<string, object>();
            _transformers = new Dictionary<string, List<ISlotTransformer>>();
            _scripts = new List<string>();
            _logger = logger;
            _currentClientContext = emptyClientContext;
            _nlTools = nlTools;
            _owningEngine = owningEngine;
        }

        private StatisticalLGPattern(
            ILogger logger,
            ClientContext newClientContext,
            bool debug)
        {
            _substitutions = new Dictionary<string, object>();
            _logger = logger;
            _currentClientContext = newClientContext;
            _debugMode = debug;
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
                return _currentClientContext;
            }
        }

        /// <summary>
        /// For debugging. Returns the set of all fields that should be filled in before this pattern can be properly rendered.
        /// Not strictly accurate if this phrase uses subphrase transformers, scripts, or anything complicated like that.
        /// </summary>
        public ISet<string> SubstitutionFieldNames
        {
            get
            {
                HashSet<string> returnVal = new HashSet<string>();
                if (_textModel != null && !_textModel.IsEmpty() && Models.ContainsKey(_textModel))
                {
                    returnVal.UnionWith(Models[_textModel].SubstitutionFieldNames);
                }
                if (_shortTextModel != null && !_shortTextModel.IsEmpty() && Models.ContainsKey(_shortTextModel))
                {
                    returnVal.UnionWith(Models[_shortTextModel].SubstitutionFieldNames);
                }
                if (_spokenModel != null && !_spokenModel.IsEmpty() && Models.ContainsKey(_spokenModel))
                {
                    returnVal.UnionWith(Models[_spokenModel].SubstitutionFieldNames);
                }

                return returnVal;
            }
        }

        /// <summary>
        /// Creates a new instance of this LG pattern. This method will be called to create a new object before every single
        /// query is processed, so this constructor will accept query-specific data to be stored for later.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="newClientContext"></param>
        /// <param name="debug"></param>
        /// <returns></returns>
        public override ILGPattern Clone(ILogger logger, ClientContext newClientContext, bool debug)
        {
            return new StatisticalLGPattern(logger, newClientContext, debug)
            {
                Name = this.Name,
                Locale = this.Locale,
                _textModel = this._textModel,
                _shortTextModel = this._shortTextModel,
                _spokenModel = this._spokenModel,
                _extraFields = this._extraFields,
                _transformers = this._transformers,
                _nlTools = this._nlTools,
                _owningEngine = this._owningEngine,
                _scripts = this._scripts
            };
        }

        public override async Task<RenderedLG> Render()
        {
            RenderedLG returnVal = new RenderedLG();

            if (_debugMode)
            {
                _logger.Log("Input subtitutions to \"" + this.Name + "\" are:", LogLevel.Vrb);
                foreach (var sub in this._substitutions)
                {
                    _logger.Log("    " + sub.Key + "=" + sub.Value, LogLevel.Vrb);
                }
            }

            // Run scripts first
            foreach (string scriptName in _scripts)
            {
                if (_debugMode) _logger.Log("Running script \"" + scriptName + "\"", LogLevel.Vrb);
                LgCommon.RunLGScript script = _owningEngine.GetScript(scriptName, this.Locale);
                if (script == null)
                {
                    _logger.Log("Could not find script named \"" + scriptName + "\" referenced in pattern \"" + this.Name + "\"!", LogLevel.Wrn);
                }
                else
                {
                    string phraseName = this.Name;
                    IDictionary<string, object> substitutions = this._substitutions;
                    ClientContext context = this.CurrentClientContext;
                    try
                    {
                        phraseName = script(substitutions, phraseName, Locale.ToBcp47Alpha2String(), (a) => _logger.Log(a, LogLevel.Vrb));
                    }
                    catch (Exception e)
                    {
                        _logger.Log("Error while running LG script", LogLevel.Err);
                        _logger.Log(e, LogLevel.Err);
                        break;
                    }

                    if (_debugMode)
                    {
                        _logger.Log("After running script \"" + scriptName + "\" subtitutions are:", LogLevel.Vrb);
                        foreach (var sub in substitutions)
                        {
                            _logger.Log("    " + sub.Key + "=" + sub.Value, LogLevel.Vrb);
                        }
                    }

                    // Did the script change the current phrase? Handle that
                    if (!string.Equals(this.Name, phraseName))
                    {
                        if (_debugMode) _logger.Log("The script \"" + scriptName + "\" is diverting the LG phrase from \"" + this.Name + "\" to \"" + phraseName + "\"", LogLevel.Vrb);
                        ILGPattern subphrasePattern = _owningEngine.GetPattern(phraseName, _currentClientContext, _logger, _debugMode);
                        foreach (var substitution in substitutions)
                        {
                            subphrasePattern = subphrasePattern.Sub(substitution.Key, substitution.Value);
                        }

                        return await subphrasePattern.Render().ConfigureAwait(false);
                    }
                }
            }

            returnVal.ExtraFields = new Dictionary<string, string>();
            Dictionary<string, string> transformedSubstitutions = await ApplySlotTransformers(_substitutions, Locale, returnVal.ExtraFields).ConfigureAwait(false);
            
            // Render text
            if (Models != null && _textModel != null && Models.ContainsKey(_textModel))
            {
                returnVal.Text = Models[_textModel].Render(transformedSubstitutions, false, _logger);
            }
            else if (_textModel != null)
            {
                returnVal.Text = "NULL_MODEL_" + _textModel;
            }
            
            // Render short text
            if (Models != null && _shortTextModel != null && Models.ContainsKey(_shortTextModel))
            {
                returnVal.ShortText = Models[_shortTextModel].Render(transformedSubstitutions, false, _logger);
            }
            else if (_shortTextModel != null)
            {
                returnVal.ShortText = "NULL_MODEL_" + _shortTextModel;
            }

            // Render speech
            if (Models != null && _spokenModel != null && Models.ContainsKey(_spokenModel))
            {
                returnVal.Spoken = Models[_spokenModel].Render(transformedSubstitutions, true, _logger);
            }
            else if (Models != null && _textModel != null && Models.ContainsKey(_textModel))
            {
                // If no spoken model is specified, render the text model as SSML
                returnVal.Spoken = Models[_textModel].Render(transformedSubstitutions, true, _logger);
            }
            else if (_spokenModel != null)
            {
                returnVal.Spoken = "NULL_MODEL_" + _spokenModel;
            }

            
            foreach (string extraFieldName in _extraFields.Keys)
            {
                returnVal.ExtraFields[extraFieldName] = _extraFields[extraFieldName];
            }

            return returnVal;
        }

        //public override string RenderText()
        //{
        //    if (Models != null && Models.ContainsKey(_textModel))
        //    {
        //        Dictionary<string, string> transformedSubstitutions = ApplySlotTransformers(_substitutions, Locale);
        //        return Models[_textModel].Render(transformedSubstitutions, false);
        //    }

        //    return "NULL_MODEL_" + _textModel;
        //}

        public override ILGPattern Sub(string key, object value)
        {
            _substitutions[key] = value;
            return this;
        }

        internal void SetTextModel(LocalizedKey value)
        {
            _textModel = value;
        }

        internal void SetShortTextModel(LocalizedKey value)
        {
            _shortTextModel = value;
        }

        internal void SetSpokenModel(LocalizedKey value)
        {
            _spokenModel = value;
        }

        internal void SetExtraField(string key, string value)
        {
            if (_extraFields.ContainsKey(key))
            {
                _extraFields.Remove(key);
            }

            _extraFields[key] = value;
        }

        internal void AddSlotTransformer(string slotName, List<ISlotTransformer> transformers)
        {
            _transformers[slotName] = transformers;
        }

        internal void AddScriptName(string name)
        {
            _scripts.Add(name);
        }

        internal Dictionary<string, string> GetTranslationTable(string tableName)
        {
            Dictionary<string, string> returnVal;
            if (_owningEngine.TranslationTables.TryGetValue(tableName, out returnVal))
            {
                return returnVal;
            }

            return null;
        }

        private IDictionary<LocalizedKey, StatisticalLGPhrase> Models
        {
            get
            {
                return _owningEngine.Models;
            }
        }

        private IDictionary<VariantConfig, IList<StatisticalLGPattern>> Patterns
        {
            get
            {
                return _owningEngine.Patterns;
            }
        }

        /// <summary>
        /// Runs all transformers specified for slots in this pattern. This returns a list of "stringified" slots, which are in the form
        /// of free-form SSML strings specified by key. This function can also have the side effect of rendering subphrases to become
        /// individual slot values, so some extra care has to be done to merge those results properly.
        /// </summary>
        /// <param name="substitutions">The dictionary of raw substitutions provided by the caller</param>
        /// <param name="locale">The locale being rendered in</param>
        /// <param name="extraFieldsReturnVal">The dictionary of extra fields that will be included in the return value. Here because we want to allow rendered subphrases to return extra fields if possible</param>
        /// <returns>The dictionary of stringified slot values</returns>
        private async Task<Dictionary<string, string>> ApplySlotTransformers(IDictionary<string, object> substitutions, LanguageCode locale, IDictionary<string, string> extraFieldsReturnVal)
        {
            Dictionary<string, object> returnVal = new Dictionary<string, object>();
            NLPTools thisLocaleNlTools;
            if (!_nlTools.TryGetNLPTools(locale, out thisLocaleNlTools))
            {
                thisLocaleNlTools = null;
            }

            // Start with a dictionary containing the raw slot values
            foreach (var slot in substitutions)
            {
                if (!returnVal.ContainsKey(slot.Key))
                {
                    returnVal[slot.Key] = slot.Value;
                }
            }

            // Figure out which transformer chains contains subphrases. If so, they have to be processed in a second pass
            // (This is because subphrases might depend on substitutions that have not happened yet)
            // FIXME is that even valid any more? since subphrases no longer refer to models but discrete patterns that manager their own transformer chains?
            IDictionary<string, IList<ISlotTransformer>> normalTransformerChains = new Dictionary<string, IList<ISlotTransformer>>();
            IDictionary<string, IList<ISlotTransformer>> subphraseTransformerChains = new Dictionary<string, IList<ISlotTransformer>>();

            foreach (var kvp in _transformers)
            {
                bool isSubphrase = false;
                IList<ISlotTransformer> chain = kvp.Value;
                foreach (ISlotTransformer trans in chain)
                {
                    if ("Subphrase".Equals(trans.Name))
                    {
                        isSubphrase = true;
                    }
                }

                if (isSubphrase)
                {
                    subphraseTransformerChains[kvp.Key] = kvp.Value;
                }
                else
                {
                    normalTransformerChains[kvp.Key] = kvp.Value;
                }
            }

            // Pass 1 - process transformer chains without any subphrases
            foreach (var transformerDef in normalTransformerChains)
            {
                string slotName = transformerDef.Key;
                object slotValue;
                if (!substitutions.TryGetValue(slotName, out slotValue))
                {
                    slotValue = string.Empty;
                }

                foreach (ISlotTransformer transformer in transformerDef.Value)
                {
                    object oldValue = slotValue;
                    slotValue = transformer.Apply(slotValue, locale, _logger, thisLocaleNlTools, this);
                    if (_debugMode) _logger.Log(string.Format("Transformer \"{0}\" made this change: {1} {2} => {3}", transformer.OriginalText, slotName, oldValue, slotValue), LogLevel.Vrb);
                }

                if (returnVal.ContainsKey(slotName))
                {
                    returnVal.Remove(slotName);
                }

                returnVal[slotName] = slotValue;
            }

            // Pass 2 - process transformer chains that contain subphrases
            foreach (var transformerDef in subphraseTransformerChains)
            {
                string slotName = transformerDef.Key;
                object slotValue;
                if (!substitutions.TryGetValue(slotName, out slotValue))
                {
                    slotValue = string.Empty;
                }

                foreach (ISlotTransformer transformer in transformerDef.Value)
                {
                    if ("Subphrase".Equals(transformer.Name))
                    {
                        string subphraseKey = ((SubphraseTransformer)transformer).SubphraseModelName;

                        ILGPattern subphrasePattern = _owningEngine.GetPattern(subphraseKey, _currentClientContext, _logger, _debugMode);
                        foreach (var substitution in substitutions)
                        {
                            subphrasePattern = subphrasePattern.Sub(substitution.Key, substitution.Value);
                        }

                        RenderedLG subphraseResult = await subphrasePattern.Render().ConfigureAwait(false);

                        if (_debugMode) _logger.Log(string.Format("Subphrase \"{0}\" made this change: {1} {2} => {3}", subphraseKey, slotName, slotValue, subphraseResult.Text), LogLevel.Vrb);

                        //if (!string.IsNullOrEmpty(subphraseResult.Spoken))
                        //{
                        //    slotValue = subphraseResult.Spoken; // Return the subphrase SSML by default because we can always strip the tags later
                        // // BUGBUG That causes problems for models that aren't expecting SSML though. Really what I need to do is append the <speak> tag at the top level of the rendering only.
                        //}
                        //else
                        //{
                        slotValue = subphraseResult.Text;
                        //}

                        // Union extra fields from the subphrase with those from the top-level response
                        if (subphraseResult.ExtraFields != null)
                        {
                            foreach (var kvp in subphraseResult.ExtraFields)
                            {
                                extraFieldsReturnVal[kvp.Key] = kvp.Value;
                                if (_debugMode) _logger.Log(string.Format("Subphrase \"{0}\" added extra field: {1}={2}", subphraseKey, kvp.Key, kvp.Value), LogLevel.Vrb);
                            }
                        }
                    }
                    else
                    {
                        object oldValue = slotValue;
                        slotValue = transformer.Apply(slotValue, locale, _logger, thisLocaleNlTools, this);
                        if (_debugMode) _logger.Log(string.Format("Transformer \"{0}\" made this change: {1} {2} => {3}", transformer.OriginalText, slotName, oldValue, slotValue), LogLevel.Vrb);
                    }
                }
                
                returnVal[slotName] = slotValue;
            }

            Dictionary<string, string> convertedReturnVal = new Dictionary<string, string>();
            foreach (var sub in returnVal)
            {
                convertedReturnVal[sub.Key] = sub.Value.ToString();
            }

            return convertedReturnVal;
        }
    }
}
