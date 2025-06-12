using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Durandal.Common.NLP.Train
{
    public class CrossTrainingRule
    {
        private static readonly Regex RULE_PARSE_REGEX = new Regex(
             // Capture groups:
             // 1
             "^(~)?" +
            //  Domain (w intent)   Intent              Domain Alone        Wildcard
            //  2                   3                   4                   5
            "(?:([a-zA-Z0-9_\\-]+?)/([a-zA-Z0-9_\\-]+?)|([a-zA-Z0-9_\\-]+?)|(\\*))" +
            ":" +
            //  Domain (w intent)   Intent              Domain Alone        Wildcard
            //  6                   7                   8                   9
            "(?:([a-zA-Z0-9_\\-]+?)/([a-zA-Z0-9_\\-]+?)|([a-zA-Z0-9_\\-]+?)|(\\*))$");


        // Null string means wildcard
        private readonly string _lhsDomain;
        private readonly string _lhsIntent;
        private readonly string _rhsDomain;
        private readonly string _rhsIntent;
        private readonly string _originalRule;
        private readonly bool _negate = false;

        private CrossTrainingRule(string lhsDomain, string lhsIntent, string rhsDomain, string rhsIntent, bool negate, string originalRule)
        {
            _lhsDomain = lhsDomain;
            _lhsIntent = lhsIntent;
            _rhsDomain = rhsDomain;
            _rhsIntent = rhsIntent;
            _negate = negate;
            _originalRule = originalRule;
        }

        public bool? Evaluate(string sourceDomain, string sourceIntent, string targetDomain, string targetIntent)
        {
            bool lhsMatches = _lhsDomain == null ||
                (string.Equals(sourceDomain, _lhsDomain, StringComparison.OrdinalIgnoreCase) && (_lhsIntent == null || string.Equals(sourceIntent, _lhsIntent, StringComparison.OrdinalIgnoreCase)));

            bool rhsMatches;
            if (targetIntent == null)
            {
                // When the target is an entire domain with null intent, apply a different rule that fails if the rule has a specific intent
                rhsMatches = _rhsDomain == null ||
                    (string.Equals(targetDomain, _rhsDomain, StringComparison.OrdinalIgnoreCase) && _rhsIntent == null);
            }
            else
            {
                rhsMatches = _rhsDomain == null ||
                    (string.Equals(targetDomain, _rhsDomain, StringComparison.OrdinalIgnoreCase) && (_rhsIntent == null || string.Equals(targetIntent, _rhsIntent, StringComparison.OrdinalIgnoreCase)));
            }

            if (lhsMatches && rhsMatches)
            {
                return !_negate;
            }

            return null;
        }

        public override string ToString()
        {
            return _originalRule;
        }

        public static CrossTrainingRule Parse(string rule, string enforcedDomain = null)
        {
            if (string.IsNullOrEmpty(rule))
            {
                throw new FormatException("Crosstraining rule is null or empty");
            }

            string lhsDomain = null;
            string lhsIntent = null;
            string rhsDomain = null;
            string rhsIntent = null;
            bool negate = false;

            Match parserMatch = RULE_PARSE_REGEX.Match(rule);
            if (!parserMatch.Success)
            {
                throw new FormatException("Badly formatted crosstraining rule: " + rule);
            }

            negate = parserMatch.Groups[1].Success;
            if (parserMatch.Groups[2].Success)
            {
                lhsDomain = parserMatch.Groups[2].Value;
            }
            else if (parserMatch.Groups[4].Success)
            {
                lhsDomain = parserMatch.Groups[4].Value;
            }
            else if (parserMatch.Groups[5].Success)
            {
                lhsDomain = null;
            }
            else
            {
                throw new FormatException("Badly formatted crosstraining rule: " + rule);
            }

            if (parserMatch.Groups[3].Success)
            {
                lhsIntent = parserMatch.Groups[3].Value;
            }

            if (parserMatch.Groups[6].Success)
            {
                rhsDomain = parserMatch.Groups[6].Value;
            }
            else if (parserMatch.Groups[8].Success)
            {
                rhsDomain = parserMatch.Groups[8].Value;
            }
            else if (parserMatch.Groups[9].Success)
            {
                rhsDomain = null;
            }
            else
            {
                throw new FormatException("Badly formatted crosstraining rule: " + rule);
            }

            if (parserMatch.Groups[7].Success)
            {
                rhsIntent = parserMatch.Groups[7].Value;
            }

            if (enforcedDomain != null)
            {
                // Enforce an extra constraint that the rule must have the specified domain on one side of its rule
                if (!string.Equals(enforcedDomain, lhsDomain, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(enforcedDomain, rhsDomain, StringComparison.OrdinalIgnoreCase))
                {
                    throw new FormatException("Crosstraining rule \"" + rule + "\" must be restricted to the \"" + enforcedDomain + "\" domain");
                }
            }

            CrossTrainingRule returnVal = new CrossTrainingRule(lhsDomain, lhsIntent, rhsDomain, rhsIntent, negate, rule);
            return returnVal;
        }
    }
}
