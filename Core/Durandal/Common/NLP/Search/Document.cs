using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.NLP.Search
{
    public class Document
    {
        public Dictionary<string, float> words = new Dictionary<string, float>();
        public float score = 0f;
        public string name = "";
        public string guid = "";
        public string title = "";
        public string body = "";
        public int id = 0;

        public Document()
        {
        }

        public Document(string t, string b, int minlen, int maxlen, bool stemming)
        {
            title = t;
            body = b;
            guid = Guid.NewGuid().ToString();
            AddWords(SearchProcessor.Normalize(title), 4f, minlen, maxlen, stemming);
            AddWords(SearchProcessor.Normalize(body), 1f, minlen, maxlen, stemming);
            SimpleSearchIndex.NormalizeDocument(this);
        }

        public void AddWords(string text, float weight, int minlen, int maxlen, bool stemming)
        {
            string res = SearchProcessor.Normalize(text);
            SearchProcessor.AddNgrams(text, minlen, maxlen, words, weight, stemming);
        }
    }
}
