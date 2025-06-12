using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.MediaProtocol
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ValueChangeType
    {
        /// <summary>
        /// Set the value to exactly the input value
        /// </summary>
        Absolute = 0,

        /// <summary>
        /// Add/subtract the input to the current value
        /// </summary>
        RelativeAdd = 1,

        /// <summary>
        /// Multiply the current value by the input
        /// </summary>
        RelativeMultiply = 2
    }
}
