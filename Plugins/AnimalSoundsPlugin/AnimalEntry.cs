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
    internal class AnimalEntry
    {
        public AnimalEntry(SerializedAnimalEntry serializedForm)
        {
            Name = serializedForm.Name;

            if (serializedForm.ProperName != null)
            {
                ProperName = new Dictionary<LanguageCode, string>();
                foreach (var kvp in serializedForm.ProperName)
                {
                    ProperName.Add(LanguageCode.Parse(kvp.Key), kvp.Value);
                }
            }

            if (serializedForm.KnownAs != null)
            {
                KnownAs = new Dictionary<LanguageCode, IList<string>>();
                foreach (var kvp in serializedForm.KnownAs)
                {
                    KnownAs.Add(LanguageCode.Parse(kvp.Key), kvp.Value);
                }
            }

            if (serializedForm.Sound != null)
            {
                Sound = new Dictionary<LanguageCode, string>();
                foreach (var kvp in serializedForm.Sound)
                {
                    Sound.Add(LanguageCode.Parse(kvp.Key), kvp.Value);
                }
            }

            SoundFile = serializedForm.SoundFile;
            Image = serializedForm.Image;
        }

        /// <summary>
        /// Internal canonical name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The "common" name of this animal, localized
        /// </summary>
        public IDictionary<LanguageCode, string> ProperName { get; set; }

        /// <summary>
        /// Mapping from locale -> list of things this animal may be called
        /// </summary>
        public IDictionary<LanguageCode, IList<string>> KnownAs { get; set; }

        /// <summary>
        /// Mapping from locale -> the onomatopaea for this animal's sound
        /// </summary>
        public IDictionary<LanguageCode, string> Sound { get; set; }

        /// <summary>
        /// The name of the sound file for this animal, if any (in the plugindata directory)
        /// </summary>
        public string SoundFile { get; set; }

        /// <summary>
        /// The name of the image for this animal, if any (in the views directory)
        /// </summary>
        public string Image { get; set; }
    }
}
