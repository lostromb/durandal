
namespace Durandal.Common.NLP.Annotation
{
    using System.Collections.Generic;

    using Durandal.API;
    using Durandal.Common.NLP.Canonical;

    using Durandal.Common.Config;
    using Durandal.Common.Logger;
    using Durandal.Common.File;
    using System.Threading.Tasks;
    using Durandal.Common.Ontology;
    using Durandal.Common.Tasks;
    using Durandal.Common.Dialog;
    using Durandal.Common.Time;
    using System.Threading;
    using Durandal.Common.NLP.Language;

    /// <summary>
    /// LU Annotator which finds ordinals ("first", "number 1", etc.)
    /// </summary>
    public class OrdinalAnnotator : BasicConditionalAnnotator
    {
        private Grammar _ordinalMatcher;
        private readonly ILogger _logger;
        private readonly IFileSystem _fileSystem;
        private readonly LanguageCode _locale;

        public OrdinalAnnotator(IFileSystem fileSystem, LanguageCode locale, ILogger logger) : base("Ordinal")
        {
            _fileSystem = fileSystem;
            _logger = logger;
            _locale = locale;
        }

        public override string Name
        {
            get
            {
                return "ordinal";
            }
        }

        public override bool Initialize()
        {
            VirtualPath ordinalFile = new VirtualPath(RuntimeDirectoryName.MISCDATA_DIR + "\\" + _locale.ToBcp47Alpha2String() + "\\ordinals.xml");
            if (_fileSystem.Exists(ordinalFile))
            {
                _logger.Log("Loading ordinal resources from " + ordinalFile);
                this._ordinalMatcher = new Grammar(_fileSystem.OpenStream(ordinalFile, FileOpenMode.Open, FileAccessMode.Read));
                _logger.Log("Done reading ordinals");
                return true;
            }
            else
            {
                _logger.Log("Could not find data file \"" + ordinalFile + "\"! Ordinal annotator will not load", LogLevel.Err);
                return false;
            }
        }

        public override Task CommitAnnotation(
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
            
            // Run annotators at the slot level
            foreach (TaggedData tagHyp in result.TagHyps)
            {
                foreach (SlotValue slot in tagHyp.Slots)
                {
                    if (!allowedIntentsSlots.Contains(slot.Name))
                        continue;

                    IList<GrammarMatch> ordinalMatches = this._ordinalMatcher.Matches(slot.Value, queryLogger.Clone("OrdinalGrammar"));
                    if (ordinalMatches.Count > 0)
                    {
                        queryLogger.Log(string.Format("The slot \"{0}\" matched {1} ordinal rule(s)", slot.Name, ordinalMatches.Count), LogLevel.Vrb);
                        foreach (GrammarMatch m in ordinalMatches)
                        {
                            queryLogger.Log(string.Format("RuleId={0} Value={1} NormalizedValue={2}", m.RuleId, m.Value, m.NormalizedValue), LogLevel.Vrb);
                        }

                        slot.SetProperty(SlotPropertyName.Ordinal, ordinalMatches[0].NormalizedValue);
                    }
                }
            }

            return DurandalTaskExtensions.NoOpTask;
        }

        public override void Reset()
        {
        }
    }
}
