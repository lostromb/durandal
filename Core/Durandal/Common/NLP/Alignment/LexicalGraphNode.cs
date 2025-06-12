using Durandal.Common.LG;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Durandal.Common.MathExt;

namespace Durandal.Common.NLP.Alignment
{
    internal class LexicalGraphNode
    {
        private Guid _guid = Guid.NewGuid();
        private string _token = null;
        private string _tag = null;
        private bool _isNumeric = false;
        private bool _hasTag = false;

        public string Token
        {
            get
            {
                return _token;
            }
            set
            {
                _token = value;
                _isNumeric = value == null ? false : NUMBER_MATCHER.IsMatch(Token);
            }
        }

        public string Tag
        {
            get
            {
                return _tag;
            }
            set
            {
                _tag = value;
                _hasTag = !string.IsNullOrEmpty(_tag);
            }
        }

        public bool Visited { get; set; }
        public Counter<LexicalGraphNode> Connections { get; set; }
        public Counter<string> TokenCount { get; set; }
        public int GroupNum { get; set; }
        public string PreSpace { get; set; }
        public string PostSpace { get; set; }


        private static readonly Regex NUMBER_MATCHER = new Regex("^(?:[-+])?(?:[0-9]+[\\.,])?[0-9]+$");

        public LexicalGraphNode(string token)
        {
            Token = token;
            Visited = false;
            GroupNum = 0;
            PreSpace = "";
            PostSpace = "";
            Connections = new Counter<LexicalGraphNode>();
            TokenCount = new Counter<string>();
        }

        public LGToken ConvertToLgToken()
        {
            if (Token == LexicalAlignment.NULL_TOKEN)
            {
                return new LGToken(string.Empty, PreSpace, PostSpace, Tag);
            }
            
            return new LGToken(Token, PreSpace, PostSpace, Tag);
        }

        public override string ToString()
        {
            return (string.IsNullOrEmpty(PreSpace) ? string.Empty : PreSpace) + "[" + Token + "]" + (string.IsNullOrEmpty(PostSpace) ? string.Empty : PostSpace) + (string.IsNullOrEmpty(Tag) ? string.Empty : (":" + Tag));
        }

        public override int GetHashCode()
        {
            return _guid.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return _guid.Equals(((LexicalGraphNode)obj)._guid);
        }

        public static bool TokenEquals(LexicalGraphNode a, LexicalGraphNode b, out int tagBenefit)
        {
            if (a._hasTag || !b._hasTag)
            {
                if (string.Equals(a._tag, b._tag))
                {
                    tagBenefit = 0;
                }
                else
                {
                    tagBenefit = 1000;
                }
            }
            else
            {
                tagBenefit = 0;
            }

            if (a._isNumeric && b._isNumeric)
            {
                return true;
            }

            return string.Equals(a._token, b._token);
        }
    }
}
