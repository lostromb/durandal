using Durandal.API;
using Durandal.Common.Compression;
using Durandal.Common.Compression.LZ4;
using Durandal.Common.File;
using Durandal.Common.IO;
using Durandal.Common.Ontology;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Logger;
using Durandal.Common.Instrumentation;
using Durandal.Common.Utils;
using Durandal.Common.Collections;

namespace Durandal.Common.Test.Builders
{
    /// <summary>
    /// A builder for DialogRequest objects, intended for testing purposes.
    /// </summary>
    /// <typeparam name="TParent">The type of the parameter that will be returned from a call to Build() - to support chained builders.</typeparam>
    public class DialogRequestBuilder<TParent>
    {
        private Func<DialogRequest, TParent> _chainingFunc;
        private DialogRequest _returnVal;
        private string _input;
        private KnowledgeContext _entityContext;

        /// <summary>
        ///  Initializes a new instance of the <see cref="DialogRequestBuilder{TParent}"/> class.
        /// </summary>
        /// <param name="chainingFunc">The chaining function to use when executing Build(). Or pass (x) => x to just return the value directly.</param>
        /// <param name="input"></param>
        /// <param name="inputMethod"></param>
        public DialogRequestBuilder(Func<DialogRequest, TParent> chainingFunc, string input, InputMethod inputMethod)
        {
            _input = input;
            _chainingFunc = chainingFunc;
            _entityContext = new KnowledgeContext();

            ClientContext context;
            if (inputMethod == InputMethod.Spoken)
            {
                context = DialogTestHelpers.GetTestClientContextAudioQuery();
            }
            else
            {
                context = DialogTestHelpers.GetTestClientContextTextQuery();
            }

            _returnVal = new DialogRequest()
            {
                ClientContext = context,
                InteractionType = inputMethod,
                RequestFlags = QueryFlags.Debug,
                TraceId = CommonInstrumentation.FormatTraceId(Guid.NewGuid()),
                LanguageUnderstanding = new List<RecognizedPhrase>()
            };
        }

        public DialogRequestBuilder<TParent> SetClientContext(ClientContext context)
        {
            _returnVal.ClientContext = context;
            return this;
        }

        public RecoResultBuilder<DialogRequestBuilder<TParent>> AddRecoResult(string domain, string intent, float confidence)
        {
            RecoResultBuilder<DialogRequestBuilder<TParent>> builder = new RecoResultBuilder<DialogRequestBuilder<TParent>>(
                (rr) =>
                {
                    if (rr.TagHyps.Count == 0)
                    {
                        rr.TagHyps.Add(new TaggedData()
                        {
                            Confidence = 0.9f,
                            Slots = new List<SlotValue>(),
                            Utterance = rr.Utterance.OriginalText
                        });
                    }

                    if (_returnVal.LanguageUnderstanding == null)
                    {
                        _returnVal.LanguageUnderstanding = new List<RecognizedPhrase>();
                    }
                    if (_returnVal.LanguageUnderstanding.Count == 0)
                    {
                        _returnVal.LanguageUnderstanding.Add(new RecognizedPhrase()
                        {
                            Recognition = new List<RecoResult>()
                        });
                    }

                    _returnVal.LanguageUnderstanding[0].Recognition.Add(rr);
                    return this;
                },
                _entityContext,
                _input,
                domain,
                intent,
                confidence,
                _returnVal.InteractionType);

            return builder;
        }

        /// <summary>
        /// Builds a <see cref="DialogRequest"/> and returns the parent chaining target.
        /// </summary>
        /// <returns>The parent that originally created this builder, or just the <see cref="DialogRequest"/> if there is no parent.</returns>
        public TParent Build()
        {
            if (_returnVal.LanguageUnderstanding.Count > 0)
            {
                using (PooledBuffer<byte> serializedContext = KnowledgeContextSerializer.SerializeKnowledgeContext(_entityContext))
                {
                    if (serializedContext.Length > 0)
                    {
                        byte[] newData = new byte[serializedContext.Length];
                        ArrayExtensions.MemCopy(serializedContext.Buffer, 0, newData, 0, newData.Length);
                        _returnVal.LanguageUnderstanding[0].EntityContext = new ArraySegment<byte>(newData);
                    }
                    else
                    {
                        _returnVal.LanguageUnderstanding[0].EntityContext = new ArraySegment<byte>(BinaryHelpers.EMPTY_BYTE_ARRAY);
                    }
                }
            }

            return _chainingFunc(_returnVal);
        }
    }
}
