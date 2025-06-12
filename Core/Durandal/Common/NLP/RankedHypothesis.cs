using Durandal.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.NLP
{
    public class RankedHypothesis : IComparable<RankedHypothesis>, IEquatable<RankedHypothesis>
    {
        private RecoResult _result;

        private float _confidenceCap = 1.0f;

        public RankedHypothesis(RecoResult result)
        {
            _result = result;
            if (result == null)
            {
                throw new ArgumentNullException("RankedHypothesis result object must never be null");
            }
        }

        /// <summary>
        /// The reco result that is being ranked
        /// </summary>
        public RecoResult Result
        {
            get
            {
                return _result;
            }
        }

        /// <summary>
        /// The actual raw confidence value passed from LU
        /// </summary>
        public float ActualLuConfidence
        {
            get
            {
                return _result.Confidence;
            }
        }

        /// <summary>
        /// The confidence value used for sorting, which is the raw value plus any min/max constraints
        /// </summary>
        public float EffectiveLuConfidence
        {
            get
            {
                return Math.Min(_confidenceCap, _result.Confidence);
            }
        }

        /// <summary>
        /// The "class" of ranking this object falls into inside dialog. Higher is better.
        /// Dialog priorities are as follows:
        /// 3 - Boosted hypos
        /// 2 - Regular hypos
        /// 1 - Suppressed hypos
        /// 0 - Dialog processing entities such as noreco or side speech
        /// </summary>
        public int DialogPriority
        {
            get;
            set;
        }

        /// <summary>
        /// Limits this hypothesis to have a maximum effective LU confidence of the given value between 0.0 and 1.0
        /// </summary>
        /// <param name="max"></param>
        public void CapConfidence(float max)
        {
            _confidenceCap = max;
        }

        public static List<RankedHypothesis> ConvertRecoResultList(IList<RecoResult> list)
        {
            List<RankedHypothesis> returnVal = new List<RankedHypothesis>();
            foreach (RecoResult r in list)
            {
                returnVal.Add(new RankedHypothesis(r));
            }

            return returnVal;
        }

        public int CompareTo(RankedHypothesis other)
        {
            if (other.DialogPriority != this.DialogPriority)
            {
                return other.DialogPriority.CompareTo(this.DialogPriority);
            }

            return other.EffectiveLuConfidence.CompareTo(this.EffectiveLuConfidence);
        }

        public override string ToString()
        {
            if (Result == null)
            {
                return string.Format("Null hypothesis/{0}/{1}/{2}", DialogPriority, ActualLuConfidence, EffectiveLuConfidence);
            }

            return string.Format("{0}/{1}/{2}/{3}/{4}", Result.Domain, Result.Intent, DialogPriority, ActualLuConfidence, EffectiveLuConfidence);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RankedHypothesis);
        }

        public bool Equals(RankedHypothesis other)
        {
            if (ReferenceEquals(other, null))
            {
                return false;
            }

            return Result.Equals(other.Result) &&
                   DialogPriority == other.DialogPriority;
        }

        public override int GetHashCode()
        {
            var hashCode = 937201345;
            hashCode = hashCode * -1521134295 + EqualityComparer<RecoResult>.Default.GetHashCode(Result);
            hashCode = hashCode * -1521134295 + DialogPriority.GetHashCode();
            return hashCode;
        }

        public static bool operator ==(RankedHypothesis left, RankedHypothesis right)
        {
            if (ReferenceEquals(left, null))
            {
                return ReferenceEquals(right, null);
            }

            return left.Equals(right);
        }

        public static bool operator !=(RankedHypothesis left, RankedHypothesis right)
        {
            return !(left == right);
        }

        public static bool operator <(RankedHypothesis left, RankedHypothesis right)
        {
            return ReferenceEquals(left, null) ? !ReferenceEquals(right, null) : left.CompareTo(right) < 0;
        }

        public static bool operator <=(RankedHypothesis left, RankedHypothesis right)
        {
            return ReferenceEquals(left, null) || left.CompareTo(right) <= 0;
        }

        public static bool operator >(RankedHypothesis left, RankedHypothesis right)
        {
            return !ReferenceEquals(left, null) && left.CompareTo(right) > 0;
        }

        public static bool operator >=(RankedHypothesis left, RankedHypothesis right)
        {
            return ReferenceEquals(left, null) ? ReferenceEquals(right, null) : left.CompareTo(right) >= 0;
        }
    }
}
