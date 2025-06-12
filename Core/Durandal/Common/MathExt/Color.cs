using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Durandal.Common.MathExt
{
    /// <summary>
    /// Represents a color as a set of 4 8-bit components for RGBA
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Color : IEquatable<Color>
    {
        private static readonly Vector3f RGB_UNIT_VECTOR = new Vector3f(255, 255, 255);

        // Raw color components
        private byte _r;
        private byte _g;
        private byte _b;
        private byte _a;
        
        // Views over raw components
        public byte Red => _r;
        public byte Green => _g;
        public byte Blue => _b;
        public byte Alpha => _a;

        [JsonIgnore]
        public float RedF => ((float)_r) / 255f;

        [JsonIgnore]
        public float GreenF => ((float)_g) / 255f;

        [JsonIgnore]
        public float BlueF => ((float)_b) / 255f;

        [JsonIgnore]
        public float AlphaF => ((float)_a) / 255f;

        public Color(byte r, byte g, byte b)
        {
            _r = r;
            _g = g;
            _b = b;
            _a = 255;
        }

        public Color(byte r, byte g, byte b, byte a)
        {
            _r = r;
            _g = g;
            _b = b;
            _a = a;
        }

        public Vector3f GetVector()
        {
            return new Vector3f(RedF, GreenF, BlueF);
        }

        public string ToRGBHex()
        {
            return string.Format("{0:X2}{1:X2}{2:X2}", _r, _g, _b);
        }

        public string ToRGBAHex()
        {
            return string.Format("{0:X2}{1:X2}{2:X2}{3:X2}", _r, _g, _b, _a);
        }

        public string ToARGBHex()
        {
            return string.Format("{0:X2}{1:X2}{2:X2}{3:X2}", _a, _r, _g, _b);
        }

        /// <summary>
        /// Calculates the saturation value of this color as a range from 0.0 to 1.0
        /// </summary>
        /// <returns></returns>
        public float GetSaturation()
        {
            // projection of this rgb vector onto the rgb unit vector
            Vector3f rgbVector = GetVector();
            float adj = rgbVector.Magnitude * (rgbVector.DotProduct(RGB_UNIT_VECTOR) / (RGB_UNIT_VECTOR.Magnitude * rgbVector.Magnitude));

            // orthogonal distance from the unit vector - used here as a measure of saturation
            float opp = (float)Math.Sqrt(Math.Max(0, (rgbVector.Magnitude * rgbVector.Magnitude) - (adj * adj)));

            float rawSaturation = opp / 209f;
            //float saturation_X_magnitude = rawSaturation * rgbVector.Magnitude;
            return rawSaturation;
        }

        /// <summary>
        /// Calculates the brightness of this color as a range from 0.0 to 1.0
        /// </summary>
        /// <returns></returns>
        public float GetBrightness()
        {
            Vector3f rgbVector = GetVector();
            return rgbVector.Magnitude / RGB_UNIT_VECTOR.Magnitude;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is Color))
            {
                return false;
            }

            return Equals((Color)obj);
        }

        public bool Equals(Color other)
        {
            return _r == other._r &&
                   _g == other._g &&
                   _b == other._b &&
                   _a == other._a;
        }

        public override int GetHashCode()
        {
            var hashCode = 1273943280;
            hashCode = hashCode * -1121134291 + _r.GetHashCode();
            hashCode = hashCode * -1221134292 + _g.GetHashCode();
            hashCode = hashCode * -1321134293 + _b.GetHashCode();
            hashCode = hashCode * -1421134294 + _a.GetHashCode();
            return hashCode;
        }

        public static bool operator ==(Color left, Color right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Color left, Color right)
        {
            return !(left == right);
        }
    }
}
