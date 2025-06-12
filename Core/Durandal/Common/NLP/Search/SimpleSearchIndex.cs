using Durandal.Common.Collections;
using Durandal.Common.NLP.Language.English;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.NLP.Search
{
    public class SimpleSearchIndex
    {
        public int MIN_NGRAM = 1;
        public int MAX_NGRAM = 3;
        public bool DO_PORTER_STEMMING = true;

        public Dictionary<int, Document> documents = new Dictionary<int, Document>();
        public static readonly HashSet<string> stopwords = new HashSet<string>();
        public Dictionary<string, HashSet<int>> invertedIndices = new Dictionary<string, HashSet<int>>();
        //return document key 

        private void Putstring(List<byte> bytes, string input)
        {
            var b1 = Encoding.UTF8.GetBytes(input);
            bytes.FastAddRangeCollection(BitConverter.GetBytes(b1.Length));
            bytes.FastAddRangeCollection(b1);
        }

        private string Getstring(BinaryReader br)
        {
            int len = br.ReadInt32();
            string s1 = Encoding.UTF8.GetString(br.ReadBytes(len), 0, len);
            return s1;
        }

        public void Deserialize(byte[] data)
        {
            invertedIndices.Clear();
            using (BinaryReader br = new BinaryReader(new MemoryStream(data)))
            {
                //deserialize settings
                MIN_NGRAM = br.ReadInt32();
                MAX_NGRAM = br.ReadInt32();
                DO_PORTER_STEMMING = br.ReadBoolean();
                //deserialize indices
                int indexcount = br.ReadInt32();
                for (int i = 0; i < indexcount; i++)
                {
                    HashSet<int> index = new HashSet<int>();
                    string word = Getstring(br);
                    int cnt = br.ReadInt32();
                    for (int j = 0; j < cnt; j++)
                    {
                        int id = br.ReadInt32();
                        index.Add(id);
                    }
                    invertedIndices.Add(word, index);
                }
                //deserialize documents
                documents.Clear();
                int doccount = br.ReadInt32();
                for (int i = 0; i < doccount; i++)
                {
                    Document d = new Document();
                    d.id = br.ReadInt32();
                    d.name = Getstring(br);
                    d.guid = Getstring(br);
                    d.title = Getstring(br);
                    d.body = Getstring(br);
                    int wordcount = br.ReadInt32();
                    for (int j = 0; j < wordcount; j++)
                    {
                        string key = Getstring(br);
                        float weight = br.ReadSingle();
                        d.words.Add(key, weight);
                    }
                    documents.Add(d.id, d);
                }
            }
        }

        public byte[] SerializeToBytes()
        {
            List<byte> bytes = new List<byte>();
            //serialize settings
            bytes.FastAddRangeCollection(BitConverter.GetBytes(MIN_NGRAM));
            bytes.FastAddRangeCollection(BitConverter.GetBytes(MAX_NGRAM));
            bytes.FastAddRangeCollection(BitConverter.GetBytes(DO_PORTER_STEMMING));
            //serialize indices
            bytes.FastAddRangeCollection(BitConverter.GetBytes(invertedIndices.Count));
            foreach (var i in invertedIndices)
            {
                Putstring(bytes, i.Key);
                bytes.FastAddRangeCollection(BitConverter.GetBytes(i.Value.Count));
                foreach (var id in i.Value)
                {
                    bytes.FastAddRangeCollection(BitConverter.GetBytes(id));
                }
            }

            //serialize documents
            bytes.FastAddRangeCollection(BitConverter.GetBytes(documents.Count));
            foreach (var d in documents)
            {
                bytes.FastAddRangeCollection(BitConverter.GetBytes(d.Key));
                Putstring(bytes, d.Value.name);
                Putstring(bytes, d.Value.guid);
                Putstring(bytes, d.Value.title);
                Putstring(bytes, d.Value.body);
                //set id!
                //serialize document words
                bytes.FastAddRangeCollection(BitConverter.GetBytes(d.Value.words.Count));
                foreach (var w in d.Value.words)
                {
                    Putstring(bytes, w.Key);
                    bytes.FastAddRangeCollection(BitConverter.GetBytes(w.Value));//weight
                }
            }

            return bytes.ToArray();
        }

        public void AddToIndex(Document d)
        {
            foreach (var w in d.words)
            {
                if (!invertedIndices.ContainsKey(w.Key)) invertedIndices.Add(w.Key, new HashSet<int>());
                invertedIndices[w.Key].Add(d.id);
            }
        }

        public static void NormalizeDocument(Document d)
        {
            float total = 0.001f;
            Dictionary<string, float> newwords = new Dictionary<string, float>();
            foreach (var w in d.words)
            {
                if (stopwords.Contains(w.Key)) newwords.Add(w.Key, 0.3f);
                else newwords.Add(w.Key, w.Value);
            }

            d.words.Clear();
            foreach (var w in newwords)
            {
                total += w.Value;
            }

            float mult = Math.Min(100f / total, 10); //let every doc start with the same weight
            foreach (var w in newwords)
            {
                d.words.Add(w.Key, w.Value * mult);
            }
        }
        public string AddDocument(string title, string body)
        {
            if (stopwords.Count == 0) GenerateStopWords(DO_PORTER_STEMMING);
            Document d = new Document(title, body, MIN_NGRAM, MAX_NGRAM, DO_PORTER_STEMMING);
            d.id = documents.Count + 1;
            documents.Add(d.id, d);
            AddToIndex(d);
            return d.guid;
        }
        public string AddDocument(string title, string body, string name)
        {
            if (stopwords.Count == 0) GenerateStopWords(DO_PORTER_STEMMING);
            Document d = new Document(title, body, MIN_NGRAM, MAX_NGRAM, DO_PORTER_STEMMING);
            d.id = documents.Count + 1;
            documents.Add(d.id, d);
            d.name = name;
            AddToIndex(d);
            return d.guid;
        }

        public List<Document> Search(string input)
        {
            Dictionary<int, float> heap = new Dictionary<int, float>();
            string norm = SearchProcessor.Normalize(input);
            var ngrams = SearchProcessor.GenerateNgrams(norm, MIN_NGRAM, MAX_NGRAM, DO_PORTER_STEMMING);
            foreach (var w1 in ngrams)
            {
                EnglishStemmer stem = new EnglishStemmer();
                foreach (var c in w1) stem.add(c);
                stem.stem();
                string word = stem.ToString();
                if (invertedIndices.ContainsKey(word))
                {
                    foreach (var id in invertedIndices[word])
                    {
                        if (!heap.ContainsKey(id)) heap.Add(id, 0);
                        heap[id] += documents[id].words[word];
                    }
                }
            }

            //if (heap.Count == 0) return null;
            List<Document> res = new List<Document>();
            var items = (from d in heap orderby d.Value descending select d).Take(5);
            foreach (var i in items)
            {
                //WE CANT CHANGE DOCUMENTS HERE
                Document d = new Document();
                d.name = documents[i.Key].name;
                d.body = documents[i.Key].body;
                d.title = documents[i.Key].title;
                d.score = i.Value;
                res.Add(d);
            }

            return res;
        }

        void GenerateStopWords(bool stem)
        {
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "a's"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "accordingly"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "again"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "allows"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "also"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "amongst"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "anybody"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "anyways"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "appropriate"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "aside"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "available"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "because"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "before"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "below"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "between"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "by"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "can't"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "certain"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "com"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "consider"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "corresponding"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "definitely"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "different"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "don't"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "each"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "else"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "et"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "everybody"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "exactly"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "fifth"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "follows"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "four"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "gets"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "goes"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "greetings"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "has"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "he"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "her"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "herein"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "him"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "how"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "i'm"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "immediate"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "indicate"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "instead"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "it"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "itself"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "know"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "later"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "lest"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "likely"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "ltd"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "me"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "more"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "must"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "nd"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "needs"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "next"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "none"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "nothing"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "of"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "okay"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "ones"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "others"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "ourselves"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "own"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "placed"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "probably"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "rather"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "regarding"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "right"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "saying"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "seeing"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "seen"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "serious"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "she"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "so"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "something"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "soon"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "still"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "t's"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "th"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "that"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "theirs"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "there"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "therein"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "they'd"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "third"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "though"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "thus"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "toward"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "try"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "under"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "unto"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "used"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "value"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "vs"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "way"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "we've"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "weren't"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "whence"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "whereas"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "whether"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "who's"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "why"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "within"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "wouldn't"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "you'll"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "yourself"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "able"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "across"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "against"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "almost"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "although"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "an"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "anyhow"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "anywhere"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "are"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "ask"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "away"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "become"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "beforehand"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "beside"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "beyond"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "c'mon"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "cannot"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "certainly"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "come"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "considering"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "could"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "described"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "do"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "done"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "edu"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "elsewhere"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "etc"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "everyone"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "example"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "first"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "for"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "from"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "getting"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "going"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "had"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "hasn't"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "he's"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "here"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "hereupon"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "himself"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "howbeit"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "i've"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "in"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "indicated"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "into"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "it'd"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "just"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "known"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "latter"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "let"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "little"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "mainly"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "mean"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "moreover"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "my"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "near"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "neither"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "nine"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "noone"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "novel"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "off"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "old"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "only"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "otherwise"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "out"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "particular"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "please"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "provides"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "rd"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "regardless"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "said"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "says"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "seem"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "self"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "seriously"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "should"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "some"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "sometime"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "sorry"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "sub"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "take"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "than"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "that's"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "them"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "there's"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "theres"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "they'll"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "this"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "three"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "to"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "towards"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "trying"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "unfortunately"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "up"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "useful"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "various"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "want"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "we"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "welcome"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "what"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "whenever"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "whereby"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "which"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "whoever"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "will"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "without"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "yes"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "you're"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "yourselves"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "about"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "actually"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "ain't"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "alone"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "always"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "and"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "anyone"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "apart"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "aren't"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "asking"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "awfully"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "becomes"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "behind"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "besides"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "both"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "c's"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "cant"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "changes"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "comes"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "contain"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "couldn't"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "despite"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "does"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "down"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "eg"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "enough"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "even"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "everything"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "except"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "five"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "former"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "further"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "given"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "gone"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "hadn't"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "have"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "hello"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "here's"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "hers"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "his"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "however"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "ie"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "inasmuch"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "indicates"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "inward"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "it'll"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "keep"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "knows"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "latterly"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "let's"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "look"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "many"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "meanwhile"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "most"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "myself"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "nearly"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "never"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "no"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "nor"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "now"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "often"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "on"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "onto"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "ought"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "outside"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "particularly"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "plus"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "que"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "re"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "regards"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "same"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "second"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "seemed"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "selves"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "seven"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "shouldn't"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "somebody"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "sometimes"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "specified"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "such"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "taken"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "thank"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "thats"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "themselves"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "thereafter"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "thereupon"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "they're"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "thorough"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "through"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "together"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "tried"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "twice"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "unless"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "upon"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "uses"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "very"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "wants"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "we'd"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "well"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "what's"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "where"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "wherein"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "while"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "whole"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "willing"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "won't"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "yet"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "you've"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "zero"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "above"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "after"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "all"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "along"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "am"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "another"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "anything"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "appear"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "around"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "associated"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "be"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "becoming"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "being"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "best"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "brief"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "came"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "cause"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "clearly"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "concerning"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "containing"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "course"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "did"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "doesn't"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "downwards"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "eight"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "entirely"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "ever"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "everywhere"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "far"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "followed"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "formerly"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "furthermore"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "gives"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "got"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "happens"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "haven't"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "help"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "hereafter"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "herself"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "hither"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "i'd"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "if"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "inc"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "inner"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "is"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "it's"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "keeps"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "last"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "least"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "like"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "looking"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "may"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "merely"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "mostly"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "name"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "necessary"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "nevertheless"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "nobody"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "normally"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "nowhere"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "oh"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "once"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "or"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "our"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "over"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "per"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "possible"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "quite"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "really"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "relatively"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "saw"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "secondly"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "seeming"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "sensible"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "several"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "since"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "somehow"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "somewhat"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "specify"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "sup"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "tell"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "thanks"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "the"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "then"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "thereby"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "these"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "they've"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "thoroughly"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "throughout"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "too"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "tries"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "two"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "unlikely"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "us"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "using"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "via"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "was"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "we'll"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "went"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "whatever"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "where's"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "whereupon"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "whither"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "whom"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "wish"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "wonder"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "you"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "your"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "according"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "afterwards"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "allow"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "already"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "among"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "any"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "anyway"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "appreciate"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "as"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "at"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "became"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "been"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "believe"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "better"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "but"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "can"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "causes"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "co"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "consequently"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "contains"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "currently"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "didn't"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "doing"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "during"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "either"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "especially"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "every"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "ex"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "few"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "following"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "forth"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "get"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "go"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "gotten"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "hardly"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "having"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "hence"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "hereby"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "hi"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "hopefully"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "i'll"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "ignored"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "indeed"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "insofar"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "isn't"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "its"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "kept"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "lately"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "less"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "liked"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "looks"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "maybe"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "might"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "much"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "namely"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "need"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "new"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "non"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "not"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "obviously"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "ok"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "one"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "other"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "ours"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "overall"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "perhaps"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "presumably"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "qv"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "reasonably"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "respectively"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "say"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "see"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "seems"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "sent"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "shall"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "six"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "someone"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "somewhere"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "specifying"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "sure"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "tends"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "thanx"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "their"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "thence"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "therefore"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "they"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "think"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "those"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "thru"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "took"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "truly"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "un"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "until"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "use"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "usually"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "viz"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "wasn't"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "we're"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "were"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "when"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "whereafter"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "wherever"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "who"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "whose"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "with"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "would"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "you'd"));
            stopwords.Add(SearchProcessor.NormalizeUnigram(stem, "yours"));

        }
    }
}
