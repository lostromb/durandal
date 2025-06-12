using Durandal.API;
using Durandal.Common.Config;
using Durandal.Common.Logger;
using Durandal.Common.Parsers;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Durandal.Common.NLP.Train
{
    /// <summary>
    /// Helper class to inspect model configurations and determine the set of cross-training rules that they specify.
    /// </summary>
    public static class CrossDomainRuleTrainer
    {
        private static readonly char[] COMMA_SPLIT = new char[] { ',' };
        private static readonly Regex CROSSDOMAIN_RULE_POSITIVE_PARSER = new Regex(@"(.+?):(\*|~?\{.+?\}|.+?)(?:/(\*|~?\{.+?\}|.+?))?(?:\s*,\s*|$)");
        private static readonly Regex CROSSDOMAIN_RULE_NEGATIVE_PARSER = new Regex(@"(\*|~?\{.+?\}|[^\\/]+?)(?:/(.+?))?:(.+?)(?:/(\*|~?\{.+?\}|.+?))?(?:\s*,\s*|$)");

        private static readonly Parser<string> parseIntent = Parse.Regex("[a-zA-Z0-9_\\-]+");
        private static readonly Parser<string> parseDomain = Parse.Regex("[a-zA-Z0-9_\\-]+");
        private static readonly Parser<string> parseDomainWithWildcard = Parse.Regex("(\\*|[a-zA-Z0-9_\\-]+)");
        
        public static IList<CrossTrainingRule> ConstructCrossTrainingRules(
            IEnumerable<string> domains,
            TrainingDataManager training,
            ILogger logger,
            string defaultRules,
            IRealTimeProvider realTime)
        {
            List<CrossTrainingRule> returnVal = new List<CrossTrainingRule>();

            foreach (string domain in domains)
            {
                IConfiguration domainConfig = training.GetDomainConfiguration(domain, realTime);

                // Start with the hardcoded implicit rules that always enable training within the same domain
                returnVal.Add(CrossTrainingRule.Parse(domain + ":" + domain));
            }

            // Then add rules from the global config
            string[] splitRules = defaultRules.Split(COMMA_SPLIT, StringSplitOptions.RemoveEmptyEntries);
            foreach (string rule in splitRules)
            {
                returnVal.Add(CrossTrainingRule.Parse(rule));
            }

            // Iterate through all model configurations for this model
            foreach (string domain in domains)
            {
                try
                {
                    IConfiguration domainConfig = training.GetDomainConfiguration(domain, realTime);

                    // Then factor in multiturn-only intents
                    // The domain config may also specify multiturn-only intents. These should be converted into additional crosstraining constraints
                    if (domainConfig.ContainsKey("MultiturnIntents") && domainConfig.GetStringList("MultiturnIntents") != null)
                    {
                        var intentsList = domainConfig.GetStringList("MultiturnIntents");
                        foreach (string intent in intentsList)
                        {
                            // The constraint is "Multiturn-only intent data should only be used as negative crosstraining within the local domain and the common domain, not any other custom domains"
                            returnVal.Add(CrossTrainingRule.Parse("~" + domain + "/" + intent + ":*"));
                            returnVal.Add(CrossTrainingRule.Parse(domain + "/" + intent + ":" + domain));
                        }
                    }

                    // Then finally, append any model-specific rules
                    if (domainConfig.ContainsKey("CrossTrainingRules"))
                    {
                        string domainSpecificTrainingRules = domainConfig.GetString("CrossTrainingRules", string.Empty);
                        if (string.IsNullOrEmpty(domainSpecificTrainingRules))
                        {
                            splitRules = domainSpecificTrainingRules.Split(COMMA_SPLIT, StringSplitOptions.RemoveEmptyEntries);
                            foreach (string rule in splitRules)
                            {
                                CrossTrainingRule customDomainRule = CrossTrainingRule.Parse(rule, domain);
                                returnVal.Add(customDomainRule);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    logger.Log("Couldn't parse domain configuration for domain " + domain, LogLevel.Err);
                    throw;
                }
            }

            logger.Log("Effective crosstraining rules are: " + string.Join(",", returnVal), LogLevel.Vrb);

            return returnVal;
        }
    }
}
