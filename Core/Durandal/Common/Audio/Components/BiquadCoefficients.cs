using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Audio.Components
{
    /// <summary>
    /// Coefficients used in biquad filtering.
    /// </summary>
    public struct BiquadCoefficients : IEquatable<BiquadCoefficients>
    {
        public float A0;
        public float A1;
        public float A2;
        public float B1;
        public float B2;

        public override bool Equals(object obj)
        {
            if (obj is BiquadCoefficients)
            {
                BiquadCoefficients other = (BiquadCoefficients)obj;
                return 
                    A0 == other.A0 &&
                    A1 == other.A1 &&
                    A2 == other.A2 &&
                    B1 == other.B1 &&
                    B2 == other.B2;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return (int)(
                (A0 * 120730) +
                (A1 * -63494) +
                (A2 * 56417) +
                (B1 * -2036) +
                (B2 * 999320)) ^ 0x13A56B06;
        }

        public static bool operator ==(BiquadCoefficients left, BiquadCoefficients right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BiquadCoefficients left, BiquadCoefficients right)
        {
            return !(left == right);
        }

        public bool Equals(BiquadCoefficients other)
        {
            return
                A0 == other.A0 &&
                A1 == other.A1 &&
                A2 == other.A2 &&
                B1 == other.B1 &&
                B2 == other.B2;
        }
    }
}
