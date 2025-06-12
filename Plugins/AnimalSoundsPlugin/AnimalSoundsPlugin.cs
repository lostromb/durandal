
namespace Durandal.Plugins.AnimalSounds
{
    using Durandal.API;
    using Durandal.Common.Audio;
    using Durandal.Common.Audio.Codecs;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.File;
    using Durandal.Common.IO;
    using Durandal.Common.IO.Json;
    using Durandal.Common.Logger;
    using Durandal.Common.NLP.ApproxString;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.NLP.Language.English;
    using Durandal.Common.NLP.Search;
    using Durandal.Common.Statistics;
    using Durandal.Common.Tasks;
    using Durandal.Common.Utils;
    using Durandal.CommonViews;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    public class AnimalSoundsPlugin : DurandalPlugin
    {
        private const string DOMAIN_NAME = "animalsounds";

        private IDictionary<string, AnimalEntry> _knownAnimals;
        private IDictionary<LanguageCode, StringFeatureSearchIndex<string>> _animalNameIndex;
        private IDictionary<string, AudioData> _knownSounds = new Dictionary<string, AudioData>();

        public AnimalSoundsPlugin() : base(DOMAIN_NAME) { }

        protected override IConversationTree BuildConversationTree(IConversationTree returnVal, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            IConversationNode makeSoundNode = returnVal.CreateNode(GetSound);
            returnVal.AddStartState("get_animal_sound", makeSoundNode);
            makeSoundNode.CreateNormalEdge("get_animal_sound_multiturn", makeSoundNode);
            return returnVal;
        }

        public override async Task OnLoad(IPluginServices services)
        {
            _knownAnimals = new Dictionary<string, AnimalEntry>();
            _animalNameIndex = new Dictionary<LanguageCode, StringFeatureSearchIndex<string>>();
            VirtualPath indexFile = services.PluginDataDirectory + "\\animal_index.json";
            if (await services.FileSystem.ExistsAsync(indexFile).ConfigureAwait(false))
            {
                using (Stream readStream = await services.FileSystem.OpenStreamAsync(indexFile, FileOpenMode.Open, FileAccessMode.Read).ConfigureAwait(false))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    using (JsonReader reader = new JsonTextReader(new StreamReader(readStream)))
                    {
                        IList<SerializedAnimalEntry> entries = serializer.Deserialize<IList<SerializedAnimalEntry>>(reader);
                        foreach (SerializedAnimalEntry entry in entries)
                        {
                            AnimalEntry convertedEntry = new AnimalEntry(entry);
                            _knownAnimals[entry.Name] = convertedEntry;

                            // Build a search index for this animal name
                            if (entry.KnownAs != null)
                            {
                                foreach (LanguageCode locale in convertedEntry.KnownAs.Keys)
                                {
                                    if (!_animalNameIndex.ContainsKey(locale))
                                    {
                                        _animalNameIndex[locale] = new StringFeatureSearchIndex<string>(new EnglishNgramApproxStringFeatureExtractor(), services.Logger);
                                    }

                                    foreach (string knownAs in convertedEntry.KnownAs[locale])
                                    {
                                        _animalNameIndex[locale].Index(knownAs, entry.Name);
                                    }
                                }
                            }

                            // Preload the sound file into memory
                            if (!string.IsNullOrEmpty(entry.SoundFile))
                            {
                                VirtualPath soundFile = services.PluginDataDirectory.Combine(entry.SoundFile);
                                if (!(await services.FileSystem.ExistsAsync(soundFile).ConfigureAwait(false)))
                                {
                                    services.Logger.Log("The sound file \"" + soundFile.FullName + "\" could not be found", LogLevel.Wrn);
                                }
                                else
                                {
                                    using (MemoryStream soundBuf = new MemoryStream())
                                    {
                                        using (Stream soundStream = await services.FileSystem.OpenStreamAsync(soundFile, FileOpenMode.Open, FileAccessMode.Read).ConfigureAwait(false))
                                        {
                                            soundStream.CopyTo(soundBuf);
                                            soundStream.Dispose();
                                        }

                                        _knownSounds.Add(entry.Name,
                                            new AudioData()
                                            {
                                                Codec = RawPcmCodecFactory.CODEC_NAME,
                                                CodecParams = CommonCodecParamHelper.CreateCodecParams(AudioSampleFormat.Mono(16000)),
                                                Data = new ArraySegment<byte>(soundBuf.ToArray())
                                            });
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public override async Task OnUnload(IPluginServices services)
        {
            _knownAnimals.Clear();
            _animalNameIndex.Clear();
            _knownSounds.Clear();
            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
        }

        public override Task<TriggerResult> Trigger(QueryWithContext queryWithContext, IPluginServices services)
        {
            // Look for an animal slot
            // Canonicalize and see if we know that animal
            return base.Trigger(queryWithContext, services);
        }

        public async Task<PluginResult> GetSound(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            SlotValue animalSlot = DialogHelpers.TryGetSlot(queryWithContext.Understanding, "animal");
            if (animalSlot == null)
            {
                services.Logger.Log("No animal slot; skipping", LogLevel.Wrn);
                return new PluginResult(Result.Skip);
            }

            // Canonicalize           
            string canonicalAnimal = CanonicalizeAnimalName(animalSlot.Value, queryWithContext.ClientContext.Locale);

            // See if we have a record for this animal
            if (string.IsNullOrEmpty(canonicalAnimal) || !_knownAnimals.ContainsKey(canonicalAnimal))
            {
                services.Logger.Log("The animal \"" + canonicalAnimal + "\" is not known; skipping", LogLevel.Wrn);
                return new PluginResult(Result.Skip);
            }

            AnimalEntry entry = _knownAnimals[canonicalAnimal];

            // Make sure we have a sound for this animal in the desired locale
            if (entry.Sound == null || !entry.Sound.ContainsKey(queryWithContext.ClientContext.Locale))
            {
                services.Logger.Log("The animal \"" + canonicalAnimal + "\" does not have a sound localized to " + queryWithContext.ClientContext.Locale + "; skipping", LogLevel.Wrn);
                return new PluginResult(Result.Skip);
            }

            if (entry.ProperName == null || !entry.ProperName.ContainsKey(queryWithContext.ClientContext.Locale))
            {
                services.Logger.Log("The animal \"" + canonicalAnimal + "\" does not have a proper name localized to " + queryWithContext.ClientContext.Locale + "; skipping", LogLevel.Wrn);
                return new PluginResult(Result.Skip);
            }

            string localizedAnimalName = entry.ProperName[queryWithContext.ClientContext.Locale];
            string localizedAnimalSaying = entry.Sound[queryWithContext.ClientContext.Locale];

            PluginResult returnVal = new PluginResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinuePassively
            };
            
            if (_knownSounds.ContainsKey(canonicalAnimal))
            {
                returnVal.ResponseAudio = new AudioResponse(_knownSounds[canonicalAnimal], AudioOrdering.AfterSpeech);
            }

            ILGPattern lg = services.LanguageGenerator.GetPattern("TheCowSays", queryWithContext.ClientContext, services.Logger)
                .Sub("animal", localizedAnimalName)
                .Sub("saying", localizedAnimalSaying);

            MessageView html = new MessageView()
            {
                Content = (await lg.Render().ConfigureAwait(false)).Text,
                ClientContextData = queryWithContext.ClientContext.ExtraClientContext
            };

            if (!string.IsNullOrEmpty(entry.Image))
            {
                html.Image = string.Format("/views/{0}/{1}", DOMAIN_NAME, entry.Image);
            }

            returnVal.ResponseHtml = html.Render();

            return await lg.ApplyToDialogResult(returnVal).ConfigureAwait(false);
        }

        private string CanonicalizeAnimalName(string name, LanguageCode locale)
        {
            if (!_animalNameIndex.ContainsKey(locale))
            {
                return null;
            }

            IList<Hypothesis<string>> results = _animalNameIndex[locale].Search(name);
            if (results == null || results.Count == 0)
            {
                return null;
            }

            if (results[0].Conf > 0.9f)
            {
                return results[0].Value;
            }

            return null;
        }

        protected override PluginInformation GetInformation(IFileSystem pluginDataManager, VirtualPath pluginDataDirectory)
        {
            byte[] iconData;
            if (pluginDataDirectory != null && pluginDataManager != null)
            {
                using (MemoryStream pngStream = new MemoryStream())
                {
                    VirtualPath iconFile = pluginDataDirectory + "\\icon.png";
                    if (pluginDataManager.Exists(iconFile))
                    {
                        using (Stream iconStream = pluginDataManager.OpenStream(iconFile, FileOpenMode.Open, FileAccessMode.Read))
                        {
                            iconStream.CopyTo(pngStream);
                        }
                    }
                    iconData = pngStream.ToArray();
                }
            }
            else
            {
                iconData = new byte[0];
            }

            PluginInformation returnVal = new PluginInformation()
            {
                InternalName = "AnimalSounds",
                Creator = "Logan Stromberg",
                MajorVersion = 2,
                MinorVersion = 0,
                IconPngData = new ArraySegment<byte>(iconData)
            };

            returnVal.LocalizedInfo.Add("en-US", new LocalizedInformation()
            {
                DisplayName = "Animal Sounds",
                ShortDescription = "What does the cow say?",
                SampleQueries = new List<string>()
                {
                    "What does the cow say?",
                    "What sound does a horse make?",
                    "What do sheep sound like?"
                }
            });

            return returnVal;
        }
    }
}
