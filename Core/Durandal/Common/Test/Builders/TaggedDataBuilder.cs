using Durandal.API;
using Durandal.Common.Dialog;
using Durandal.Common.Ontology;
using Durandal.Common.Statistics;
using Durandal.Common.Time.Timex;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Durandal.Common.Test.Builders
{
    /// <summary>
    /// A builder for TaggedData objects, intended for testing purposes.
    /// </summary>
    /// <typeparam name="TParent">The type of the parameter that will be returned from a call to Build() - to support chained builders.</typeparam>
    public class TaggedDataBuilder<TParent>
    {
        private Func<TaggedData, TParent> _chainingFunc;
        private TaggedData _returnVal;
        private InputMethod _inputMethod;
        private KnowledgeContext _entityContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="TaggedDataBuilder{TParent}"/> class.
        /// </summary>
        /// <param name="chainingFunc">The chaining function to use when executing Build(). Or pass (x) => x to just return the value directly.</param>
        /// <param name="entityContext"></param>
        /// <param name="utterance"></param>
        /// <param name="confidence"></param>
        /// <param name="inputMethod"></param>
        public TaggedDataBuilder(
            Func<TaggedData, TParent> chainingFunc,
            KnowledgeContext entityContext,
            string utterance,
            float confidence,
            InputMethod inputMethod = InputMethod.Typed)
        {
            _chainingFunc = chainingFunc;
            _entityContext = entityContext;
            _returnVal = new TaggedData()
            {
                Confidence = confidence,
                Utterance = utterance,
                Slots = new List<SlotValue>()
            };
            _inputMethod = inputMethod;
        }

        public TaggedDataBuilder<TParent> AddBasicSlot(string slotName, string slotValue, string lexicalValue = null)
        {
            SlotValueFormat format = _inputMethod == InputMethod.Typed ? SlotValueFormat.TypedText : SlotValueFormat.SpokenText;
            _returnVal.Slots.Add(new SlotValue(slotName, slotValue, format, lexicalValue));
            return this;
        }

        public TaggedDataBuilder<TParent> AddEntitySlot(string slotName, string slotValue, Entity entity)
        {
            SlotValueFormat format = _inputMethod == InputMethod.Typed ? SlotValueFormat.TypedText : SlotValueFormat.SpokenText;
            // fixme: allow caller to specify lexical values for spoken slots
            SlotValue slot = new SlotValue(slotName, slotValue, format);
            entity.CopyTo(_entityContext, true);
            slot.AddEntity(new Hypothesis<Entity>(entity, 1.0f));
            _returnVal.Slots.Add(slot);
            return this;
        }

        public TaggedDataBuilder<TParent> AddCanonicalizedSlot(string slotName, string canonicalValue, string nonCanonicalValue)
        {
            SlotValueFormat format = _inputMethod == InputMethod.Typed ? SlotValueFormat.TypedText : SlotValueFormat.SpokenText;
            // fixme add non canonical annotation
            _returnVal.Slots.Add(new SlotValue(slotName, canonicalValue, format));
            return this;
        }

        public TaggedDataBuilder<TParent> AddTimexSlot(string slotName, string stringValue, ExtendedDateTime timex)
        {
            SlotValue slot = _returnVal.Slots.FirstOrDefault((s) => s.Name == slotName);
            if (slot == null)
            {
                SlotValueFormat format = _inputMethod == InputMethod.Typed ? SlotValueFormat.TypedText : SlotValueFormat.SpokenText;
                slot = new SlotValue(slotName, stringValue, format);
                _returnVal.Slots.Add(slot);
            }

            TimexMatch match = new TimexMatch(timex)
            {
                Id = 0,
                Index = 0,
                Value = stringValue,
                RuleId = "test_rule"
            };

            slot.AddTimexMatch(match);
            return this;
        }

        public TaggedDataBuilder<TParent> AddNumericSlot(string slotName, string stringValue, decimal numberValue)
        {
            SlotValue slot = _returnVal.Slots.FirstOrDefault((s) => s.Name == slotName);
            if (slot == null)
            {
                SlotValueFormat format = _inputMethod == InputMethod.Typed ? SlotValueFormat.TypedText : SlotValueFormat.SpokenText;
                slot = new SlotValue(slotName, stringValue, format);
                _returnVal.Slots.Add(slot);
            }

            slot.SetProperty(SlotPropertyName.Number, numberValue.ToString());
            return this;
        }

        /// <summary>
        /// Builds a <see cref="TaggedData"/> and returns the parent chaining target.
        /// </summary>
        /// <returns>The parent that originally created this builder, or just the <see cref="TaggedData"/> if there is no parent.</returns>
        public TParent Build()
        {
            return _chainingFunc(_returnVal);
        }
    }
}
