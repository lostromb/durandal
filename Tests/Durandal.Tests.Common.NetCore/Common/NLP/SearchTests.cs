using Durandal.Common.NLP.Search;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.NLP
{
    [TestClass]
    public class SearchTests
    {
        [TestMethod]
        public void TestSimpleSearchIndex()
        {
            SimpleSearchIndex idx = new SimpleSearchIndex();
            idx.AddDocument("Lucas pseudoprime", "Lucas pseudoprimes and Fibonacci pseudoprimes are composite integers that pass certain tests which all primes and very few composite numbers pass: in this case, criteria relative to some Lucas sequence.");
            idx.AddDocument("Motion capture", "Motion capture (sometimes referred as mo-cap or mocap, for short) is the process of recording the movement of objects or people. It is used in military, entertainment, sports, medical applications, and for validation of computer vision and robotics.");
            idx.AddDocument("Tim Rogers", "William Timothy Rogers Jr. (born June 7, 1979) is an American video game journalist and developer. In games journalism, he is known for his association with mid-2000s New Games Journalism, his verbose writing style, and his video game reviews website ActionButton.net.");
            idx.AddDocument("Shrek", "Shrek is a fictional ogre character created by American author William Steig. Shrek is the protagonist of the book of the same name and of eponymous films by DreamWorks Animation. The name \"Shrek\" is derived from the German word Schreck, meaning \"fright\" or \"terror\".");

            byte[] serialized = idx.SerializeToBytes();
            idx = new SimpleSearchIndex();
            idx.Deserialize(serialized);

            List<Document> searchResults;
            searchResults = idx.Search("ogre");
            Assert.AreEqual(1, searchResults.Count);
            Assert.AreEqual("Shrek", searchResults[0].title);
            searchResults = idx.Search("journalism");
            Assert.AreEqual(1, searchResults.Count);
            Assert.AreEqual("Tim Rogers", searchResults[0].title);
            searchResults = idx.Search("robotics");
            Assert.AreEqual(1, searchResults.Count);
            Assert.AreEqual("Motion capture", searchResults[0].title);
            searchResults = idx.Search("fibonacci");
            Assert.AreEqual(1, searchResults.Count);
            Assert.AreEqual("Lucas pseudoprime", searchResults[0].title);
        }
    }
}
