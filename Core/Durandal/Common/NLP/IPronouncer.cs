using System;
using System.Collections.Generic;

namespace Durandal.Common.NLP
{
    public interface IPronouncer : IDisposable
    {
        string PronounceAsString(string word);
        Syllable[] PronounceAsSyllables(string word);
        string PronouncePhraseAsString(IEnumerable<string> phrase);
    }
}
