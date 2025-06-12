using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Durandal.Common.NLP;

namespace Durandal.Tests.Common.NLP
{
    using Durandal.API;
    using Durandal.Common.NLP.Language.English;

    [TestClass]
    public class WordbreakerTests
    {
        [TestMethod]
        public void TestEnglishWordbreaker()
        {
            IWordBreaker breaker = new EnglishWordBreaker();
            IList<Tuple<string, int>> inputs = new List<Tuple<string, int>>();
            inputs.Add(new Tuple<string, int>("Hello computer", 2));
            inputs.Add(new Tuple<string, int>("This is a test of the wordbreaker", 7));
            inputs.Add(new Tuple<string, int>("I'm going to don't test those prefixes", 9));
            inputs.Add(new Tuple<string, int>("Not to mention hyphenated-words yeah.", 5));
            inputs.Add(new Tuple<string, int>("One. Two. Three. Four.", 7));
            inputs.Add(new Tuple<string, int>("And punctuation? Yes.", 4));
            inputs.Add(new Tuple<string, int>("And.... The ellipsis", 4));
            inputs.Add(new Tuple<string, int>("543 Numbers too 12 3. 912", 7));
            inputs.Add(new Tuple<string, int>("translate \"hello\" into spanish", 6));
            inputs.Add(new Tuple<string, int>("drop trailing periods.", 3));
            inputs.Add(new Tuple<string, int>("drop trailing commas,", 3));
            inputs.Add(new Tuple<string, int>("drop all exclamations!", 3));
            inputs.Add(new Tuple<string, int>("don't drop questions?", 5));
            inputs.Add(new Tuple<string, int>("don't you-", 3));
            inputs.Add(new Tuple<string, int>("outside it is -10° and partly cloudy", 7));
            foreach (Tuple<string, int> input in inputs)
            {
                Sentence val = breaker.Break(input.Item1);
                Assert.IsNotNull(val);
                Assert.AreEqual(input.Item2, val.Length);
            }
        }

        [TestMethod]
        public void TestEnglishWordbreakerCapturesParentheses()
        {
            IWordBreaker breaker = new EnglishWordBreaker();
            Sentence utterance = breaker.Break("sin(3)");
            Assert.AreEqual(4, utterance.Words.Count);
            Assert.AreEqual("sin", utterance.Words[0]);
            Assert.AreEqual("(", utterance.Words[1]);
            Assert.AreEqual("3", utterance.Words[2]);
            Assert.AreEqual(")", utterance.Words[3]);
        }

        [TestMethod]
        public void TestEnglishWordbreakerPreservesIndices()
        {
            IWordBreaker breaker = new EnglishWordBreaker();
            Sentence utterance = breaker.Break("this is  a   test.");
            Assert.AreEqual(4, utterance.Words.Count);
            Assert.AreEqual(0, utterance.Indices[0]);
            Assert.AreEqual(5, utterance.Indices[1]);
            Assert.AreEqual(9, utterance.Indices[2]);
            Assert.AreEqual(13, utterance.Indices[3]);
        }

        [TestMethod]
        public void TestEnglishWordbreakerSplitsNumbers()
        {
            IWordBreaker breaker = new EnglishWordBreaker();
            Sentence utterance = breaker.Break("43rd");
            Assert.AreEqual(2, utterance.Words.Count);
            Assert.AreEqual("43", utterance.Words[0]);
            Assert.AreEqual("rd", utterance.Words[1]);
        }

        [TestMethod]
        public void TestEnglishWordbreakerPreservesNegativeNumbers()
        {
            IWordBreaker breaker = new EnglishWordBreaker();
            Sentence utterance = breaker.Break("-10.3°");
            Assert.AreEqual(1, utterance.Words.Count);
            Assert.AreEqual("-10.3", utterance.Words[0]);
            Assert.AreEqual("°", utterance.NonTokens[1]);
        }
    }
}
