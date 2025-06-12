using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Durandal.Common.NLP.Tagging
{
    public class TaggedWord
    {
        /// <summary>
        /// The string token being tagged
        /// </summary>
        public string Word { get; set; }

        /// <summary>
        /// The list of tags that apply to this word, if any
        /// </summary>
        public IList<string> Tags { get; set; }

        /// <summary>
        /// The list of tags for which this is the first token after the tag opening.
        /// This is used to differentiate the gap when two adjacent phrases are separately tagged with the same tag.
        /// </summary>
        public IList<string> StartTags { get; set; }

        public TaggedWord()
        {
            Word = null;
            Tags = new List<string>();
            StartTags = new List<string>();
        }

        public override string ToString()
        {
            if (Tags.Count > 0 && !Tags[0].Equals("O"))
            {
                return string.Format("[{0}]{1}[/{0}]", string.Join(" ", Tags), Word);
            }

            return Word;
        }
    }
}
