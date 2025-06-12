using System.Collections.Generic;

namespace Durandal.API
{
    public class LexicalNamedEntity
    {
        public readonly int Ordinal;
        public readonly List<LexicalString> KnownAs;

        public LexicalNamedEntity(int ordinal, IEnumerable<LexicalString> knownAs)
        {
            Ordinal = ordinal;
            KnownAs = (knownAs is List<LexicalString>) ? (knownAs as List<LexicalString>) : (new List<LexicalString>(knownAs));
        }

        public override string ToString()
        {
            return Ordinal.ToString();
        }
    }
}
