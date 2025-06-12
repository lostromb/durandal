using Durandal.Common.NLP.Language;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.AnimalSounds
{
    /// <summary>
    /// Schema for json animal database
    /// </summary>
    internal class SerializedAnimalEntry
    {
        /// <summary>
        /// Internal canonical name
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// The "common" name of this animal, localized
        /// </summary>
        [JsonProperty("properName")]
        public IDictionary<string, string> ProperName { get; set; }

        /// <summary>
        /// Mapping from locale -> list of things this animal may be called
        /// </summary>
        [JsonProperty("knownAs")]
        public IDictionary<string, IList<string>> KnownAs { get; set; }

        /// <summary>
        /// Mapping from locale -> the onomatopaea for this animal's sound
        /// </summary>
        [JsonProperty("sound")]
        public IDictionary<string, string> Sound { get; set; }

        /// <summary>
        /// The name of the sound file for this animal, if any (in the plugindata directory)
        /// </summary>
        [JsonProperty("soundFile")]
        public string SoundFile { get; set; }

        /// <summary>
        /// The name of the image for this animal, if any (in the views directory)
        /// </summary>
        [JsonProperty("image")]
        public string Image { get; set; }
    }
}
