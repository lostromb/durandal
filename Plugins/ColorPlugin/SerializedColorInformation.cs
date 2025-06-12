using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Color
{
#pragma warning disable CS0649 // Values defined but not used - they are assigned to via deserialization
    internal class SerializedColorInformation
    {
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
        public Dictionary<string, List<string>> Name;

        /// <summary>
        /// Localized dictionary of things that possess this color (as a way of describing it without referring to other colors)
        /// </summary>
        public Dictionary<string, List<string>> ColorOf;
    }
#pragma warning restore CS0649
}
