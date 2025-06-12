using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Test.FVT
{
    /// <summary>
    /// Represents a set of inclusive / exclusive constraints to be applied to test user identities.
    /// For example, if your test requires a user that has certain privileges or a certain kind of login, these constraints can be used
    /// to find identities which satisfy your requirements.
    /// </summary>
    public class FunctionalTestFeatureConstraints
    {
        /// <summary>
        /// The default set of empty constraints.
        /// </summary>
        public static readonly FunctionalTestFeatureConstraints EmptyConstraints = new FunctionalTestFeatureConstraints();

        public FunctionalTestFeatureConstraints()
        {
            Require = new HashSet<string>();
            Exclude = new HashSet<string>();
        }

        public bool IsEmpty
        {
            get
            {
                return Require.Count == 0 &&
                    Exclude.Count == 0;
            }
        }

        [JsonProperty("Require")]
        public HashSet<string> Require { get; private set; }

        [JsonProperty("Exclude")]
        public HashSet<string> Exclude { get; private set; }

        public override bool Equals(object obj)
        {
            if (obj == null || obj.GetType() != GetType())
            {
                return false;
            }

            FunctionalTestFeatureConstraints other = obj as FunctionalTestFeatureConstraints;
            return Require.SetEquals(other.Require) &&
                Exclude.SetEquals(other.Exclude);
        }

        public override int GetHashCode()
        {
            int returnVal = 0;
            // use summation to ensure the hash code is the same even if the order of elements is different
            foreach (string r in Require)
            {
                returnVal += r.GetHashCode();
            }

            foreach (string r in Exclude)
            {
                returnVal += r.GetHashCode();
            }

            return returnVal;
        }

        public override string ToString()
        {
            return "Require: " + string.Join(",", Require) + " Exclude: " + string.Join(",", Exclude);
        }
    }
}
