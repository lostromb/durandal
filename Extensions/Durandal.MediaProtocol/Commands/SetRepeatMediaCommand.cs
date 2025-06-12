using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.MediaProtocol
{
    /// <summary>
    /// Configures the Repeat property of the media server which controls how to advance in the playlist
    /// </summary>
    public class SetRepeatMediaCommand : MediaCommand
    {
        public override string Action
        {
            get
            {
                return "SetRepeat";
            }
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public RepeatMode Mode { get; set; }
    }
}
