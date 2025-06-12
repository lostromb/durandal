using Durandal.Common.Collections;
using Durandal.Common.MathExt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Config
{
    /// <summary>
    /// Represents a set of "variant" constraints, which are lists of key-value pairs
    /// that can be evaluated to determine whether a certain object is valid in context.
    /// Used for when you want the behavior of some component to vary based on
    /// dynamic context such as locale, platform, or user preferences.
    /// </summary>
    public class VariantConfig
    {
        private SmallDictionary<string, string> _variants;
        private static IRandom _rand = new FastRandom();

        /// <summary>
        /// Constructs a new readonly variant config
        /// </summary>
        /// <param name="name">The name of this config</param>
        /// <param name="requiredVariants">The set of variants that must be present for this config to validate</param>
        public VariantConfig(string name, IDictionary<string, string> requiredVariants = null)
        {
            if (name == null)
            {
                throw new ArgumentNullException("name");
            }

            Name = name;
            if (requiredVariants == null)
            {
                _variants = new SmallDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                _variants = new SmallDictionary<string, string>(StringComparer.OrdinalIgnoreCase, requiredVariants);
            }
        }

        /// <summary>
        /// Gets the list of variants that must be satisfied by this config
        /// </summary>
        public IReadOnlyDictionary<string, string> Variants
        {
            get
            {
                return _variants;
            }
        }

        /// <summary>
        /// Gets the name of this config
        /// </summary>
        public string Name
        {
            get;
            private set;
        }
        
        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            VariantConfig other = (VariantConfig)obj;
            if (!string.Equals(Name, other.Name))
            {
                return false;
            }

            if (Variants.Count != other.Variants.Count)
            {
                return false;
            }

            foreach (var kvp in _variants)
            {
                string otherValue;
                if (!other.Variants.TryGetValue(kvp.Key, out otherValue) ||
                    !string.Equals(kvp.Value, otherValue))
                {
                    return false;
                }
            }

            return true;
        }
        
        public override int GetHashCode()
        {
            int hash = Name.GetHashCode();

            foreach (var kvp in _variants)
            {
                hash += kvp.Key.GetHashCode() + kvp.Value.GetHashCode();
            }

            return hash;
        }

        /// <summary>
        /// Verifies that this object's set of variants is satisfied by the given dictionary of constraints.
        /// </summary>
        /// <param name="variantConstraints">The constraints in the current context</param>
        /// <returns>True if the given variants satisfy all the constraints in this object</returns>
        public bool MatchesVariantConstraints(IDictionary<string, string> variantConstraints)
        {
            foreach (KeyValuePair<string, string> variant in _variants)
            {
                string otherVar;
                if (!variantConstraints.TryGetValue(variant.Key, out otherVar) ||
                    !string.Equals(variant.Value, otherVar))
                {
                    return false;
                }
            }

            return true;
        }

        public bool EqualsVariantConstraints(IDictionary<string, string> variantConstraints)
        {
            return MatchesVariantConstraints(variantConstraints) && variantConstraints.Count == _variants.Count;
        }

        /// <summary>
        /// Attempts to select a configuration based on a set of variant constraints that apply to each candidate.
        /// The candidate that satisfies the most constraints will be returned. If multiple candidates have the same
        /// number of constraints, a random one will be selected.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="candidates"></param>
        /// <param name="key"></param>
        /// <param name="variantsToApply"></param>
        /// <returns></returns>
        public static T SelectByVariants<T>(IEnumerable<KeyValuePair<VariantConfig, T>> candidates, string key, IDictionary<string, string> variantsToApply)
        {
            int mostMatches = -1;
            T bestCandidate = default(T);

            // Simply find the variant config which has the most constraints satisfied
            foreach (KeyValuePair<VariantConfig, T> candidate in candidates)
            {
                // Make sure key matches
                if (!string.Equals(key, candidate.Key.Name))
                {
                    continue;
                }

                // Now verify all constraints
                bool valid = candidate.Key.MatchesVariantConstraints(variantsToApply);
                int thisMatchCount = candidate.Key.Variants.Count;

                // Select this candidate if it's valid AND either it has a higher matched variant count, or it has an equal variant count
                // with the current top candidate and random selection decided to change it. This is intended to expose nondeterministic
                // behavior when variants are underspecified, which should hopefully reveal configuration bugs if any are present
                if (valid &&
                    (thisMatchCount > mostMatches || (thisMatchCount == mostMatches && _rand.NextBool())))
                {
                    mostMatches = thisMatchCount;
                    bestCandidate = candidate.Value;
                }
            }

            return bestCandidate;
        }
    }
}
