using Durandal.Common.Utils;
using Durandal.Common.Config;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.Config
{
    [TestClass]
    public class VariantConfigTests
    {
        private static readonly string ConfigName = "SampleConfig";

        [TestMethod]
        public void TestEmptyVariantConfig()
        {
            VariantConfig config = new VariantConfig(ConfigName);
            Assert.AreEqual(ConfigName, config.Name);
            Assert.AreEqual(0, config.Variants.Count);

            Assert.IsTrue(config.MatchesVariantConstraints(new Dictionary<string, string>()));
            Assert.IsTrue(config.MatchesVariantConstraints(new Dictionary<string, string>
                {
                    { "locale", "en-US" }
                }));
            Assert.IsTrue(config.MatchesVariantConstraints(new Dictionary<string, string>
                {
                    { "locale", "en-US" },
                    { "canvas", "chat" }
                }));
        }

        [TestMethod]
        public void TestBasicVariantConfig()
        {
            VariantConfig config = new VariantConfig(ConfigName, new Dictionary<string, string>
                {
                    { "locale", "de-de" },
                    { "canvas", "speak" }
                });
            Assert.IsFalse(config.MatchesVariantConstraints(new Dictionary<string, string>()));
            Assert.IsFalse(config.MatchesVariantConstraints(new Dictionary<string, string>
                {
                    { "locale", "en-US" }
                }));
            Assert.IsTrue(config.MatchesVariantConstraints(new Dictionary<string, string>
                {
                    { "locale", "de-de" },
                    { "canvas", "speak" }
                }));
            Assert.IsFalse(config.MatchesVariantConstraints(new Dictionary<string, string>
                {
                    { "locale", "en-US" },
                    { "canvas", "speak" }
                }));
            Assert.IsTrue(config.MatchesVariantConstraints(new Dictionary<string, string>
                {
                    { "locale", "de-de" },
                    { "canvas", "speak" },
                    { "cabeza", "nieve" },
                    { "cerveza", "bueno" }
                }));
            Assert.IsFalse(config.MatchesVariantConstraints(new Dictionary<string, string>
                {
                    { "locale", "de-de" },
                    { "canvas", "chat" }
                }));
        }

        [TestMethod]
        public void TestVariantConfigResolution()
        {
            Dictionary<VariantConfig, int> allConfigs = new Dictionary<VariantConfig, int>();
            allConfigs[new VariantConfig(ConfigName)] = -1;
            allConfigs[new VariantConfig(ConfigName, new Dictionary<string, string>
                {
                    { "locale", "en-US" }
                })] = 1;
            allConfigs[new VariantConfig(ConfigName, new Dictionary<string, string>
                {
                    { "locale", "de-de" }
                })] = 2;
            allConfigs[new VariantConfig(ConfigName, new Dictionary<string, string>
                {
                    { "locale", "de-de" },
                    { "canvas", "speak" }
                })] = 3;
            allConfigs[new VariantConfig(ConfigName, new Dictionary<string, string>
                {
                    { "locale", "de-de" },
                    { "canvas", "chat" }
                })] = 4;
            allConfigs[new VariantConfig(ConfigName, new Dictionary<string, string>
                {
                    { "locale", "de-de" },
                    { "canvas", "chat" },
                    { "hint", "yes" }
                })] = 5;

            Assert.AreEqual(-1, VariantConfig.SelectByVariants(allConfigs, ConfigName, new Dictionary<string, string>()));
            Assert.AreEqual(0, VariantConfig.SelectByVariants(allConfigs, "NotHere", new Dictionary<string, string>()));
            Assert.AreEqual(1, VariantConfig.SelectByVariants(allConfigs, ConfigName, new Dictionary<string, string>
                {
                    { "locale", "en-US" },
                    { "hint", "no" }
                }));
            Assert.AreEqual(1, VariantConfig.SelectByVariants(allConfigs, ConfigName, new Dictionary<string, string>
                {
                    { "locale", "en-US" },
                    { "canvas", "chat" },
                    { "hint", "no" }
                }));
            Assert.AreEqual(2, VariantConfig.SelectByVariants(allConfigs, ConfigName, new Dictionary<string, string>
                {
                    { "locale", "de-de" },
                    { "canvas", "somethingelse" },
                    { "hint", "no" }
                }));
            Assert.AreEqual(4, VariantConfig.SelectByVariants(allConfigs, ConfigName, new Dictionary<string, string>
                {
                    { "locale", "de-de" },
                    { "canvas", "chat" },
                    { "hint", "no" }
                }));
            Assert.AreEqual(3, VariantConfig.SelectByVariants(allConfigs, ConfigName, new Dictionary<string, string>
                {
                    { "locale", "de-de" },
                    { "canvas", "speak" },
                    { "hint", "no" }
                }));
            Assert.AreEqual(3, VariantConfig.SelectByVariants(allConfigs, ConfigName, new Dictionary<string, string>
                {
                    { "locale", "de-de" },
                    { "canvas", "speak" },
                    { "hint", "yes" }
                }));
            Assert.AreEqual(5, VariantConfig.SelectByVariants(allConfigs, ConfigName, new Dictionary<string, string>
                {
                    { "locale", "de-de" },
                    { "canvas", "chat" },
                    { "hint", "yes" }
                }));
        }

        [TestMethod]
        public void TestVariantConfigNondeterminism()
        {
            Dictionary<VariantConfig, int> allConfigs = new Dictionary<VariantConfig, int>();
            allConfigs[new VariantConfig(ConfigName, new Dictionary<string, string>
                {
                    { "variantA", "yes" }
                })] = 1;
            allConfigs[new VariantConfig(ConfigName, new Dictionary<string, string>
                {
                    { "variantB", "yes" }
                })] = 2;

            HashSet<int> results = new HashSet<int>();
            for (int c = 0; c < 100; c++)
            {
                int selection = VariantConfig.SelectByVariants(allConfigs, ConfigName, new Dictionary<string, string>
                {
                    { "variantA", "yes" },
                    { "variantB", "yes" },
                });

                if (!results.Contains(selection))
                {
                    results.Add(selection);
                }
            }

            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.Contains(1));
            Assert.IsTrue(results.Contains(2));
        }
    }
}
