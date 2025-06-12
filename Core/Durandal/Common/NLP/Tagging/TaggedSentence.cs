namespace Durandal.Common.NLP.Tagging
{
    using System.Collections.Generic;

    using Durandal.API;

    public class TaggedSentence
    {
        public Sentence Utterance;
        public IList<TaggedWord> Words = new List<TaggedWord>();
    }
}
