using Durandal.API;
using Durandal.Common.Ontology;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Test.Builders
{
    /// <summary>
    /// A builder for RecoResult objects, intended for testing purposes.
    /// </summary>
    /// <typeparam name="TParent">The type of the parameter that will be returned from a call to Build() - to support chained builders.</typeparam>
    public class RecoResultBuilder<TParent>
    {
        private Func<RecoResult, TParent> _chainingFunc;
        private RecoResult _returnVal;
        private KnowledgeContext _entityContext;
        private InputMethod _interactionType;

        /// <summary>
        /// Initializes a new instance of the <see cref="RecoResultBuilder{TParent}"/> class.
        /// </summary>
        /// <param name="chainingFunc">The chaining function to use when executing Build(). Or pass (x) => x to just return the value directly.</param>
        /// <param name="entityContext"></param>
        /// <param name="sentence"></param>
        /// <param name="domain"></param>
        /// <param name="intent"></param>
        /// <param name="confidence"></param>
        /// <param name="interactionType"></param>
        public RecoResultBuilder(
            Func<RecoResult, TParent> chainingFunc,
            KnowledgeContext entityContext,
            string sentence,
            string domain,
            string intent,
            float confidence,
            InputMethod interactionType)
        {
            _chainingFunc = chainingFunc;
            _entityContext = entityContext;
            _interactionType = interactionType;
            _returnVal = new RecoResult()
            {
                Domain = domain,
                Intent = intent,
                Confidence = confidence,
                Source = "SyntheticTest",
                Utterance = new Sentence(sentence),
                TagHyps = new List<TaggedData>()
            };
        }

        public TaggedDataBuilder<RecoResultBuilder<TParent>> AddTagHypothesis(float confidence)
        {
            return new TaggedDataBuilder<RecoResultBuilder<TParent>>(
                (rr) =>
                {
                    _returnVal.TagHyps.Add(rr);
                    return this;
                },
                _entityContext,
                _returnVal.Utterance.OriginalText,
                confidence,
                _interactionType);
        }

        /// <summary>
        /// Builds a <see cref="RecoResult"/> and returns the parent chaining target.
        /// </summary>
        /// <returns>The parent that originally created this builder, or just the <see cref="RecoResult"/> if there is no parent.</returns>
        public TParent Build()
        {
            return _chainingFunc(_returnVal);
        }
    }
}
