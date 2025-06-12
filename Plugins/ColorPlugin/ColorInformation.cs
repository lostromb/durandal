using Durandal.Common.IO.Json;
using Durandal.Common.MathExt;
using Durandal.Common.NLP.Language;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Color
{
    internal class ColorInformation
    {
        private static readonly Vector3f RGB_UNIT_VECTOR = new Vector3f(255, 255, 255);

        public ColorInformation(SerializedColorInformation serializedForm)
        {
            Hex = serializedForm.Hex;
            HSL = serializedForm.HSL;
            RGB = serializedForm.RGB;
            if (serializedForm.Name != null)
            {
                Name = new Dictionary<LanguageCode, List<string>>();
                foreach (var kvp in serializedForm.Name)
                {
                    Name.Add(LanguageCode.Parse(kvp.Key), kvp.Value);
                }
            }

            if (serializedForm.ColorOf != null)
            {
                ColorOf = new Dictionary<LanguageCode, List<string>>();
                foreach (var kvp in serializedForm.ColorOf)
                {
                    ColorOf.Add(LanguageCode.Parse(kvp.Key), kvp.Value);
                }
            }
        }
        
        /// <summary>
        /// Hex form of a color e.g. AE01FB
        /// </summary>
        public string Hex;

        /// <summary>
        /// Hue-Saturation-Lightness form of a color e.g. 233,10,85
        /// </summary>
        public string HSL;

        /// <summary>
        /// Red-Green-Blue form of a color e.g. 12,44,255
        /// </summary>
        public string RGB;

        /// <summary>
        /// Localized dictionary of names given to this color
        /// </summary>
        public Dictionary<LanguageCode, List<string>> Name;

        /// <summary>
        /// Localized dictionary of things that possess this color (as a way of describing it without referring to other colors)
        /// </summary>
        public Dictionary<LanguageCode, List<string>> ColorOf;

        /// <summary>
        /// RGB values as a vector for similarity comparison
        /// </summary>
        public Vector3f RGBVector;

        /// <summary>
        /// Red component value 0-255
        /// </summary>
        public int R;

        /// <summary>
        /// Green component value 0-255
        /// </summary>
        public int G;

        /// <summary>
        /// Blue component value 0-255
        /// </summary>
        public int B;

        /// <summary>
        /// Hue angle 0-360
        /// </summary>
        public int H;

        /// <summary>
        /// Saturation value 0-100
        /// </summary>
        public int S;

        /// <summary>
        /// Lightness value 0-100
        /// </summary>
        public int L;

        /// <summary>
        /// This color's saturation as a range from 0...1
        /// </summary>
        public float SaturationLevelRaw
        {
            get
            {
                return (float)S / 100f;
            }
        }

        public SaturationLevel SaturationLevelEnum
        {
            get
            {
                // projection of this rgb vector onto the rgb unit vector
                float adj = RGBVector.Magnitude * (RGBVector.DotProduct(RGB_UNIT_VECTOR) / (RGB_UNIT_VECTOR.Magnitude * RGBVector.Magnitude));

                // orthogonal distance from the unit vector - used here as a measure of saturation
                float opp = (float)Math.Sqrt(Math.Max(0, (RGBVector.Magnitude * RGBVector.Magnitude) - (adj * adj)));

                float rawSaturation = opp / 209f;
                float saturation_X_magnitude = rawSaturation * RGBVector.Magnitude;
                if (rawSaturation < 0.1f || saturation_X_magnitude < 0.12f)
                {
                    return SaturationLevel.Dull;
                }
                else if (rawSaturation < 0.5f)
                {
                    return SaturationLevel.Medium;
                }
                else
                {
                    return SaturationLevel.Vivid;
                }
            }
        }

        /// <summary>
        /// This color's brightness as a range from 0...1
        /// </summary>
        public float BrightnessLevelRaw
        {
            get
            {
                return (float)L / 100f;
            }
        }

        public BrightnessLevel BrightnessLevelEnum
        {
            get
            {
                float rawBrightness = RGBVector.Magnitude / RGB_UNIT_VECTOR.Magnitude;
                if (rawBrightness < 0.45f)
                {
                    return BrightnessLevel.Dark;
                }
                else if (rawBrightness < 0.65f)
                {
                    return BrightnessLevel.Medium;
                }
                else
                {
                    return BrightnessLevel.Light;
                }
            }
        }
    }
}
