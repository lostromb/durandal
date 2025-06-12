using Durandal.API;
using System.Collections.Generic;

namespace Durandal.Common.NLP
{
    /// <summary>
    /// Assigns a typed entity with a list of strings (typed or spoken) that designate that entity.
    /// Used for selection modeling and certain types of inference.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class NamedEntity<T>
    {
        public readonly T Handle;
        public readonly IList<LexicalString> KnownAs;

        public NamedEntity(T handle, IEnumerable<LexicalString> knownAs)
        {
            Handle = handle;
            KnownAs = (knownAs is IList<LexicalString>) ? (knownAs as IList<LexicalString>) : (new List<LexicalString>(knownAs));
        }

        public NamedEntity(T handle, LexicalString name)
        {
            Handle = handle;
            KnownAs = new List<LexicalString>()
            {
                name
            };
        }

        public override string ToString()
        {
            return Handle.ToString();
        }
    }
}
