using Durandal.API;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Durandal.Common.Logger;
using Durandal.Common.File;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.NLP.Train;
using Durandal.Common.NLP;
using Newtonsoft.Json;
using Durandal.Common.NLP.Feature;

namespace Durandal.Tests.Common.LU
{
    [TestClass]
    public class CrossDomainRuleTests
    {
        [TestMethod]
        public void TestCDRCatchAll()
        {
            CrossTrainingRule rule = CrossTrainingRule.Parse("*:*");
            Assert.AreEqual(true, rule.Evaluate("domain1", "intent1", "domain2", "intent2"));
            Assert.AreEqual(true, rule.Evaluate("domain1", "intent1", "domain1", "intent2"));
            Assert.AreEqual(true, rule.Evaluate("domain1", "intent1", "domain2", "intent1"));
            Assert.AreEqual(true, rule.Evaluate("domain1", "intent1", "domain2", null));
        }

        [TestMethod]
        public void TestCDRCatchAllNegative()
        {
            CrossTrainingRule rule = CrossTrainingRule.Parse("~*:*");
            Assert.AreEqual(false, rule.Evaluate("domain1", "intent1", "domain2", "intent2"));
            Assert.AreEqual(false, rule.Evaluate("domain1", "intent1", "domain1", "intent2"));
            Assert.AreEqual(false, rule.Evaluate("domain1", "intent1", "domain2", "intent1"));
            Assert.AreEqual(false, rule.Evaluate("domain1", "intent1", "domain2", null));
        }

        [TestMethod]
        public void TestCDRTwoDomains()
        {
            CrossTrainingRule rule = CrossTrainingRule.Parse("domain1:domain2");
            Assert.AreEqual(true, rule.Evaluate("domain1", "intent1", "domain2", "intent2"));
            Assert.AreEqual(null, rule.Evaluate("domain1", "intent1", "domain1", "intent2"));
            Assert.AreEqual(true, rule.Evaluate("domain1", "intent1", "domain2", "intent1"));
            Assert.AreEqual(true, rule.Evaluate("domain1", "intent1", "domain2", null));
        }

        [TestMethod]
        public void TestCDRTwoDomainsNegative()
        {
            CrossTrainingRule rule = CrossTrainingRule.Parse("~domain1:domain1");
            Assert.AreEqual(null, rule.Evaluate("domain1", "intent1", "domain2", "intent2"));
            Assert.AreEqual(false, rule.Evaluate("domain1", "intent1", "domain1", "intent2"));
            Assert.AreEqual(null, rule.Evaluate("domain1", "intent1", "domain2", "intent1"));
            Assert.AreEqual(null, rule.Evaluate("domain1", "intent1", "domain2", null));
        }

        [TestMethod]
        public void TestCDRSameDomain()
        {
            CrossTrainingRule rule = CrossTrainingRule.Parse("domain1:domain1");
            Assert.AreEqual(null, rule.Evaluate("domain1", "intent1", "domain2", "intent2"));
            Assert.AreEqual(true, rule.Evaluate("domain1", "intent1", "domain1", "intent2"));
            Assert.AreEqual(null, rule.Evaluate("domain1", "intent1", "domain2", "intent1"));
            Assert.AreEqual(null, rule.Evaluate("domain1", "intent1", "domain2", null));
        }

        [TestMethod]
        public void TestCDRSameDomainNegative()
        {
            CrossTrainingRule rule = CrossTrainingRule.Parse("~domain1:domain1");
            Assert.AreEqual(null, rule.Evaluate("domain1", "intent1", "domain2", "intent2"));
            Assert.AreEqual(false, rule.Evaluate("domain1", "intent1", "domain1", "intent2"));
            Assert.AreEqual(null, rule.Evaluate("domain1", "intent1", "domain2", "intent1"));
            Assert.AreEqual(null, rule.Evaluate("domain1", "intent1", "domain2", null));
        }

        [TestMethod]
        public void TestCDRTwoDomainsTwoIntents()
        {
            CrossTrainingRule rule = CrossTrainingRule.Parse("domain1/intent1:domain2/intent2");
            Assert.AreEqual(true, rule.Evaluate("domain1", "intent1", "domain2", "intent2"));
            Assert.AreEqual(null, rule.Evaluate("domain1", "intent1", "domain1", "intent2"));
            Assert.AreEqual(null, rule.Evaluate("domain1", "intent1", "domain2", "intent1"));
            Assert.AreEqual(null, rule.Evaluate("domain1", "intent1", "domain2", null));
        }

        [TestMethod]
        public void TestCDRTwoDomainsOneIntent()
        {
            CrossTrainingRule rule = CrossTrainingRule.Parse("domain1/intent1:domain2");
            Assert.AreEqual(true, rule.Evaluate("domain1", "intent1", "domain2", "intent2"));
            Assert.AreEqual(null, rule.Evaluate("domain1", "intent1", "domain1", "intent2"));
            Assert.AreEqual(true, rule.Evaluate("domain1", "intent1", "domain2", "intent1"));
            Assert.AreEqual(true, rule.Evaluate("domain1", "intent1", "domain2", null));
        }

        [TestMethod]
        public void TestCDRDomainIntentWildcard()
        {
            CrossTrainingRule rule = CrossTrainingRule.Parse("domain2/intent2:*");
            Assert.AreEqual(null, rule.Evaluate("domain1", "intent1", "domain2", "intent2"));
            Assert.AreEqual(null, rule.Evaluate("domain1", "intent1", "domain1", "intent2"));
            Assert.AreEqual(null, rule.Evaluate("domain1", "intent1", "domain2", "intent1"));
            Assert.AreEqual(null, rule.Evaluate("domain1", "intent1", "domain2", null));
        }

        [TestMethod]
        public void TestCDRDomainIntentWildcardNegative()
        {
            CrossTrainingRule rule = CrossTrainingRule.Parse("~domain1/intent1:*");
            Assert.AreEqual(false, rule.Evaluate("domain1", "intent1", "domain2", "intent2"));
            Assert.AreEqual(false, rule.Evaluate("domain1", "intent1", "domain1", "intent2"));
            Assert.AreEqual(false, rule.Evaluate("domain1", "intent1", "domain2", "intent1"));
            Assert.AreEqual(false, rule.Evaluate("domain1", "intent1", "domain2", null));
        }

        [TestMethod]
        public void TestCDRDomainIntentWildcardNegative2()
        {
            CrossTrainingRule rule = CrossTrainingRule.Parse("~domain1/intent2:*");
            Assert.AreEqual(null, rule.Evaluate("domain1", "intent1", "domain2", "intent2"));
            Assert.AreEqual(null, rule.Evaluate("domain1", "intent1", "domain1", "intent2"));
            Assert.AreEqual(null, rule.Evaluate("domain1", "intent1", "domain2", "intent1"));
            Assert.AreEqual(null, rule.Evaluate("domain1", "intent1", "domain2", null));
        }

        [TestMethod]
        public void TestCDRDomainWildcardNegative()
        {
            CrossTrainingRule rule = CrossTrainingRule.Parse("~domain1:*");
            Assert.AreEqual(false, rule.Evaluate("domain1", "intent1", "domain2", "intent2"));
            Assert.AreEqual(false, rule.Evaluate("domain1", "intent1", "domain1", "intent2"));
            Assert.AreEqual(false, rule.Evaluate("domain1", "intent1", "domain2", "intent1"));
            Assert.AreEqual(false, rule.Evaluate("domain1", "intent1", "domain2", null));
        }

        [TestMethod]
        public void TestCDRRuleParseEnforcedDomain()
        {
            CrossTrainingRule.Parse("gooddomain:*", "gooddomain");
            CrossTrainingRule.Parse("*:gooddomain", "gooddomain");
            CrossTrainingRule.Parse("gooddomain/goodintent:gooddomain", "gooddomain");
            CrossTrainingRule.Parse("gooddomain/goodintent:*", "gooddomain");
            CrossTrainingRule.Parse("*:gooddomain/goodintent", "gooddomain");

            try
            {
                CrossTrainingRule.Parse("baddomain:*", "gooddomain");
                Assert.Fail("Should have thrown a FormatException");
            }
            catch (FormatException) { }

            try
            {
                CrossTrainingRule.Parse("*:baddomain", "gooddomain");
                Assert.Fail("Should have thrown a FormatException");
            }
            catch (FormatException) { }

            try
            {
                CrossTrainingRule.Parse("*:*", "gooddomain");
                Assert.Fail("Should have thrown a FormatException");
            }
            catch (FormatException) { }
        }

        [TestMethod]
        public void TestCDRRuleParseInvalid2()
        {
            try
            {
                CrossTrainingRule rule = CrossTrainingRule.Parse("");
                Assert.Fail("Should have thrown a FormatException");
            }
            catch (FormatException) { }
        }

        [TestMethod]
        public void TestCDRRuleParseInvalid3()
        {
            try
            {
                CrossTrainingRule rule = CrossTrainingRule.Parse(null);
                Assert.Fail("Should have thrown a FormatException");
            }
            catch (FormatException) { }
        }

        [TestMethod]
        public void TestCDRRuleParseInvalid4()
        {
            try
            {
                CrossTrainingRule rule = CrossTrainingRule.Parse("*/intent:*");
                Assert.Fail("Should have thrown a FormatException");
            }
            catch (FormatException) { }
        }

        [TestMethod]
        public void TestCDRRuleParseInvalid5()
        {
            try
            {
                CrossTrainingRule rule = CrossTrainingRule.Parse("*:*/intent");
                Assert.Fail("Should have thrown a FormatException");
            }
            catch (FormatException) { }
        }

        [TestMethod]
        public void TestCDRRuleParseInvalid6()
        {
            try
            {
                CrossTrainingRule rule = CrossTrainingRule.Parse("~d1:d2:d3");
                Assert.Fail("Should have thrown a FormatException");
            }
            catch (FormatException) { }
        }

        [TestMethod]
        public void TestCDRRuleParseInvalid7()
        {
            try
            {
                CrossTrainingRule rule = CrossTrainingRule.Parse("d1/i1");
                Assert.Fail("Should have thrown a FormatException");
            }
            catch (FormatException) { }
        }

        /// <summary>
        /// Default pipeline with full crosstraining
        /// </summary>
        [TestMethod]
        public void TestCDRDomainPipeline1()
        {
            List<CrossTrainingRule> rules = new List<CrossTrainingRule>();
            rules.Add(CrossTrainingRule.Parse("*:*"));
            DomainCrossTrainFilter filter = new DomainCrossTrainFilter("domain1", rules);

            Assert.AreEqual(false, filter.Passes(new DomainIntentContextFeature("domain1", "intent1", new string[0])));
            Assert.AreEqual(false, filter.Passes(new DomainIntentContextFeature("domain1", "intent2", new string[0])));
            Assert.AreEqual(true, filter.Passes(new DomainIntentContextFeature("domain2", "intent1", new string[0])));
            Assert.AreEqual(true, filter.Passes(new DomainIntentContextFeature("domain2", "intent2", new string[0])));

            filter = new DomainCrossTrainFilter("domain2", rules);

            Assert.AreEqual(true, filter.Passes(new DomainIntentContextFeature("domain1", "intent1", new string[0])));
            Assert.AreEqual(true, filter.Passes(new DomainIntentContextFeature("domain1", "intent2", new string[0])));
            Assert.AreEqual(false, filter.Passes(new DomainIntentContextFeature("domain2", "intent1", new string[0])));
            Assert.AreEqual(false, filter.Passes(new DomainIntentContextFeature("domain2", "intent2", new string[0])));
        }
        
        /// <summary>
        /// Pipeline with full crosstraining and private intent
        /// </summary>
        [TestMethod]
        public void TestCDRDomainPipeline2()
        {
            List<CrossTrainingRule> rules = new List<CrossTrainingRule>();
            rules.Add(CrossTrainingRule.Parse("*:*"));
            rules.Add(CrossTrainingRule.Parse("~domain1/private_intent:*"));
            rules.Add(CrossTrainingRule.Parse("domain1/private_intent:domain1"));
            DomainCrossTrainFilter filter = new DomainCrossTrainFilter("domain1", rules);

            Assert.AreEqual(false, filter.Passes(new DomainIntentContextFeature("domain1", "intent1", new string[0])));
            Assert.AreEqual(false, filter.Passes(new DomainIntentContextFeature("domain1", "private_intent", new string[0])));
            Assert.AreEqual(true, filter.Passes(new DomainIntentContextFeature("domain2", "intent1", new string[0])));
            Assert.AreEqual(true, filter.Passes(new DomainIntentContextFeature("domain2", "intent2", new string[0])));

            filter = new DomainCrossTrainFilter("domain2", rules);

            Assert.AreEqual(true, filter.Passes(new DomainIntentContextFeature("domain1", "intent1", new string[0])));
            Assert.AreEqual(false, filter.Passes(new DomainIntentContextFeature("domain1", "private_intent", new string[0])));
            Assert.AreEqual(false, filter.Passes(new DomainIntentContextFeature("domain2", "intent1", new string[0])));
            Assert.AreEqual(false, filter.Passes(new DomainIntentContextFeature("domain2", "intent2", new string[0])));
        }

        /// <summary>
        /// Pipeline with no crosstraining between domains
        /// </summary>
        [TestMethod]
        public void TestCDRDomainPipeline3()
        {
            List<CrossTrainingRule> rules = new List<CrossTrainingRule>();
            rules.Add(CrossTrainingRule.Parse("domain1:domain1"));
            rules.Add(CrossTrainingRule.Parse("domain2:domain2"));
            DomainCrossTrainFilter filter = new DomainCrossTrainFilter("domain1", rules);

            Assert.AreEqual(false, filter.Passes(new DomainIntentContextFeature("domain1", "intent1", new string[0])));
            Assert.AreEqual(false, filter.Passes(new DomainIntentContextFeature("domain1", "intent2", new string[0])));
            Assert.AreEqual(false, filter.Passes(new DomainIntentContextFeature("domain2", "intent1", new string[0])));
            Assert.AreEqual(false, filter.Passes(new DomainIntentContextFeature("domain2", "intent2", new string[0])));

            filter = new DomainCrossTrainFilter("domain2", rules);

            Assert.AreEqual(false, filter.Passes(new DomainIntentContextFeature("domain1", "intent1", new string[0])));
            Assert.AreEqual(false, filter.Passes(new DomainIntentContextFeature("domain1", "intent2", new string[0])));
            Assert.AreEqual(false, filter.Passes(new DomainIntentContextFeature("domain2", "intent1", new string[0])));
            Assert.AreEqual(false, filter.Passes(new DomainIntentContextFeature("domain2", "intent2", new string[0])));
        }

        /// <summary>
        /// Default pipeline with full crosstraining
        /// </summary>
        [TestMethod]
        public void TestCDRIntentPipeline1()
        {
            List<CrossTrainingRule> rules = new List<CrossTrainingRule>();
            rules.Add(CrossTrainingRule.Parse("*:*"));
            IntentCrossTrainFilter filter = new IntentCrossTrainFilter("domain1", "intent1", rules);

            Assert.AreEqual(false, filter.Passes(new DomainIntentContextFeature("domain1", "intent1", new string[0])));
            Assert.AreEqual(true, filter.Passes(new DomainIntentContextFeature("domain1", "intent2", new string[0])));
            Assert.AreEqual(true, filter.Passes(new DomainIntentContextFeature("domain2", "intent1", new string[0])));
            Assert.AreEqual(true, filter.Passes(new DomainIntentContextFeature("domain2", "intent2", new string[0])));

            filter = new IntentCrossTrainFilter("domain2", "intent2", rules);

            Assert.AreEqual(true, filter.Passes(new DomainIntentContextFeature("domain1", "intent1", new string[0])));
            Assert.AreEqual(true, filter.Passes(new DomainIntentContextFeature("domain1", "intent2", new string[0])));
            Assert.AreEqual(true, filter.Passes(new DomainIntentContextFeature("domain2", "intent1", new string[0])));
            Assert.AreEqual(false, filter.Passes(new DomainIntentContextFeature("domain2", "intent2", new string[0])));
        }

        /// <summary>
        /// Pipeline with full crosstraining and private intent
        /// </summary>
        [TestMethod]
        public void TestCDRIntentPipeline2()
        {
            List<CrossTrainingRule> rules = new List<CrossTrainingRule>();
            rules.Add(CrossTrainingRule.Parse("*:*"));
            rules.Add(CrossTrainingRule.Parse("~domain1/private_intent:*"));
            rules.Add(CrossTrainingRule.Parse("domain1/private_intent:domain1"));
            IntentCrossTrainFilter filter = new IntentCrossTrainFilter("domain1", "intent1", rules);

            Assert.AreEqual(false, filter.Passes(new DomainIntentContextFeature("domain1", "intent1", new string[0])));
            Assert.AreEqual(true, filter.Passes(new DomainIntentContextFeature("domain1", "private_intent", new string[0])));
            Assert.AreEqual(true, filter.Passes(new DomainIntentContextFeature("domain2", "intent1", new string[0])));
            Assert.AreEqual(true, filter.Passes(new DomainIntentContextFeature("domain2", "intent2", new string[0])));

            filter = new IntentCrossTrainFilter("domain2", "intent2", rules);

            Assert.AreEqual(true, filter.Passes(new DomainIntentContextFeature("domain1", "intent1", new string[0])));
            Assert.AreEqual(false, filter.Passes(new DomainIntentContextFeature("domain1", "private_intent", new string[0])));
            Assert.AreEqual(true, filter.Passes(new DomainIntentContextFeature("domain2", "intent1", new string[0])));
            Assert.AreEqual(false, filter.Passes(new DomainIntentContextFeature("domain2", "intent2", new string[0])));

            filter = new IntentCrossTrainFilter("domain1", "private_intent", rules);

            Assert.AreEqual(true, filter.Passes(new DomainIntentContextFeature("domain1", "intent1", new string[0])));
            Assert.AreEqual(false, filter.Passes(new DomainIntentContextFeature("domain1", "private_intent", new string[0])));
            Assert.AreEqual(true, filter.Passes(new DomainIntentContextFeature("domain2", "intent1", new string[0])));
            Assert.AreEqual(true, filter.Passes(new DomainIntentContextFeature("domain2", "intent2", new string[0])));
        }

        /// <summary>
        /// Pipeline with no crosstraining between domains
        /// </summary>
        [TestMethod]
        public void TestCDRIntentPipeline3()
        {
            List<CrossTrainingRule> rules = new List<CrossTrainingRule>();
            rules.Add(CrossTrainingRule.Parse("domain1:domain1"));
            rules.Add(CrossTrainingRule.Parse("domain2:domain2"));
            IntentCrossTrainFilter filter = new IntentCrossTrainFilter("domain1", "intent1", rules);

            Assert.AreEqual(false, filter.Passes(new DomainIntentContextFeature("domain1", "intent1", new string[0])));
            Assert.AreEqual(true, filter.Passes(new DomainIntentContextFeature("domain1", "intent2", new string[0])));
            Assert.AreEqual(false, filter.Passes(new DomainIntentContextFeature("domain2", "intent1", new string[0])));
            Assert.AreEqual(false, filter.Passes(new DomainIntentContextFeature("domain2", "intent2", new string[0])));

            filter = new IntentCrossTrainFilter("domain2", "intent2", rules);

            Assert.AreEqual(false, filter.Passes(new DomainIntentContextFeature("domain1", "intent1", new string[0])));
            Assert.AreEqual(false, filter.Passes(new DomainIntentContextFeature("domain1", "intent2", new string[0])));
            Assert.AreEqual(true, filter.Passes(new DomainIntentContextFeature("domain2", "intent1", new string[0])));
            Assert.AreEqual(false, filter.Passes(new DomainIntentContextFeature("domain2", "intent2", new string[0])));
        }
    }
}
