
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
    /// LU Annotator which finds numbers ("1", "two", "forty", etc)
    /// </summary>
    public class NumberAnnotator : BasicConditionalAnnotator
    {
        private readonly IFileSystem _fileSystem;
        private readonly ILogger _logger;
        private readonly LanguageCode _locale;
        private Grammar _numberMatcher;

        public NumberAnnotator(IFileSystem fileSystem, LanguageCode locale, ILogger logger) : base("Number")
        {
            _fileSystem = fileSystem;
            _logger = logger;
            _locale = locale;
        }

        public override string Name
        {
            get
            {
                return "number";
            }
        }

        public override bool Initialize()
        {
            VirtualPath numberFile = new VirtualPath(RuntimeDirectoryName.MISCDATA_DIR + "\\" + _locale.ToBcp47Alpha2String() + "\\numbers.xml");
            if (_fileSystem.Exists(numberFile))
            {
                _logger.Log("Loading number annotation resources from " + numberFile);
                _numberMatcher = new Grammar(_fileSystem.OpenStream(numberFile, FileOpenMode.Open, FileAccessMode.Read));
                _logger.Log("Done reading number grammar");
                return true;
            }
            else
            {
                _logger.Log("Could not find data file \"" + numberFile + "\"! Number annotator will not load", LogLevel.Err);
                return false;
            }
        }

        public override Task CommitAnnotation(
            object asyncResult,
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

                    IList<GrammarMatch> numberMatches = _numberMatcher.Matches(slot.Value, queryLogger.Clone("NumberGrammar"));
                    if (numberMatches.Count > 0)
                    {
                        string numberString = numberMatches[0].NormalizedValue.Replace(",", string.Empty);
                        slot.SetProperty(SlotPropertyName.Number, numberString);
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
