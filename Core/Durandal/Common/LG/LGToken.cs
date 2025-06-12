using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.LG
{
    public class LGToken
    {
        /// <summary>
        /// The actual string value of this token (the word)
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// Non-token characters (i.e. whitespace) which precede this token
        /// </summary>
        public string NonTokensPre { get; set; }

        /// <summary>
        /// Non-token characters (i.e. whitespace) which follow this token
        /// </summary>
        public string NonTokensPost { get; set; }

        /// <summary>
        /// An optional slot tag for this token
        /// </summary>
        public string Tag { get; set; }

        public IDictionary<string, string> Attributes { get; private set; }

        public LGToken(string token, string preSpace, string postSpace, string tag = null)
        {
            Token = token;
            NonTokensPre = preSpace;
            NonTokensPost = postSpace;
            Tag = tag;
            Attributes = new Dictionary<string, string>();
        }

        public override bool Equals(object obj)
        {
            if (!(obj is LGToken))
                return false;

            LGToken other = (LGToken)obj;
            return string.Equals(Token, other.Token) &&
                string.Equals(NonTokensPre, other.NonTokensPre) &&
                string.Equals(NonTokensPost, other.NonTokensPost) &&
                string.Equals(Tag, other.Tag);
            // todo factor attributes
        }

        public override int GetHashCode()
        {
            return (Token == null ? 0 : Token.GetHashCode()) +
                (NonTokensPost == null ? 0 : NonTokensPost.GetHashCode()) +
                (NonTokensPre == null ? 0 : NonTokensPre.GetHashCode()) +
                (Tag == null ? 0 : Tag.GetHashCode());
        }

        public override string ToString()
        {
            return NonTokensPre + Token + NonTokensPost;
        }
    }
}
