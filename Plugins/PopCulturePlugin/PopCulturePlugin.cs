
namespace Durandal.Plugins.PopCulture
{
    using Durandal.API;
    using Durandal.Common.Audio;
    using Durandal.Common.Audio.Codecs;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.File;
    using Durandal.Common.IO;
    using Durandal.Common.Logger;
    using Durandal.Common.Ontology;
    using Durandal.Common.Tasks;
    using Durandal.Common.Utils;
    using Durandal.CommonViews;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    public class PopCultureAnswer : DurandalPlugin
    {
        public PopCultureAnswer() : base("popculture") { }

        protected override IConversationTree BuildConversationTree(IConversationTree returnVal, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            returnVal.AddStartState("insult", Retort);
            //returnVal.AddStartState("people_opinions", PeopleOpinions);
            //returnVal.AddStartState("smiley", Smiley);

            return returnVal;
        }
        
        public override async Task<TriggerResult> Trigger(QueryWithContext queryWithContext, IPluginServices services)
        {
            switch (queryWithContext.Understanding.Intent)
            {
                case "people_opinions":
                    // Suppress this answer if the person slot is not found or is not resolved
                    SlotValue personSlot = DialogHelpers.TryGetSlot(queryWithContext.Understanding, "person");
                    if (personSlot == null || personSlot.GetEntities(services.EntityContext).Count == 0)
                    {
                        return new TriggerResult(BoostingOption.Suppress);
                    }
                    break;
            }

            return await base.Trigger(queryWithContext, services).ConfigureAwait(false);
        }
        
        public async Task<PluginResult> Smiley(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            return new PluginResult(Result.Success)
                {
                    ResponseText = ";-)",
                    ResponseHtml = new MessageView()
                    {
                        Content = ";-)",
                        ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                    }.Render()
                };
        }
        
        public async Task<PluginResult> PeopleOpinions(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            // Find the person slot. At this point this slot should always be present
            SlotValue personSlot = DialogHelpers.TryGetSlot(queryWithContext.Understanding, "person");

            SchemaDotOrg.Person resolvedPerson = personSlot.GetEntities(services.EntityContext)[0].Entity.As<SchemaDotOrg.Person>();

            string gender = null;
            SchemaDotOrg.GenderType entityGenderType = await resolvedPerson.Gender_as_GenderType.GetValue().ConfigureAwait(false);
            if (entityGenderType == null)
            { 
                services.Logger.Log("Resolved person \"" + resolvedPerson.Name.Value + "\" does not have a parsed gender value; cannot proceed", LogLevel.Wrn);
                return new PluginResult(Result.Skip);
            }

            if (entityGenderType.IsA<SchemaDotOrg.Male>())
            {
                gender = "Male";
            }
            else if (entityGenderType.IsA<SchemaDotOrg.Female>())
            {
                gender = "Female";
            }

            string category = "General";
            List<string> professions = new List<string>(resolvedPerson.JobTitle.List);

            if (professions.Contains("Politician"))
            {
                category = "Politician";
            }
            else if (professions.Contains("Singer") || professions.Contains("Musician"))
            {
                category = "Musician";
            }
            else if (professions.Contains("Actor"))
            {
                category = "Actor";
            }
            else if (professions.Contains("Novelist") || professions.Contains("Writer") || professions.Contains("Author"))
            {
                category = "Writer";
            }

            string lgKey = "Opinion-" + category + gender;

            ILGPattern pattern = services.LanguageGenerator.GetPattern(lgKey, queryWithContext.ClientContext, services.Logger);
            return await pattern.ApplyToDialogResult(new PluginResult(Result.Success)
            {
                ResponseHtml = new MessageView()
                {
                    Content = (await pattern.Render().ConfigureAwait(false)).Text,
                    ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                }.Render()
            }).ConfigureAwait(false);
        }

        public async Task<PluginResult> Retort(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            PluginResult returnVal = new PluginResult(Result.Success)
                {
                    ResponseText = "Registering insult... Retort: You are short and your hair line is receding",
                    ResponseHtml = new MessageView()
                    {
                        Content = "Registering insult... Retort: You are short and your hair line is receding",
                        ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                    }.Render()
                };
            
            VirtualPath retortAudioFile = services.PluginDataDirectory + "\\retort.raw";
            if (services.FileSystem.Exists(retortAudioFile))
            {
                using (MemoryStream buffer = new MemoryStream())
                {
                    using (Stream audioFileIn = services.FileSystem.OpenStream(retortAudioFile, FileOpenMode.Open, FileAccessMode.Read))
                    {
                        audioFileIn.CopyTo(buffer);
                        audioFileIn.Dispose();
                    }

                    returnVal.ResponseAudio = new AudioResponse(new AudioData()
                        {
                            Codec = RawPcmCodecFactory.CODEC_NAME,
                            CodecParams = CommonCodecParamHelper.CreateCodecParams(AudioSampleFormat.Mono(16000)),
                            Data = new ArraySegment<byte>(buffer.ToArray())
                        }, AudioOrdering.AfterSpeech);

                    buffer.Dispose();
                }
            }

            return returnVal;
        }

        protected override PluginInformation GetInformation(IFileSystem pluginDataManager, VirtualPath pluginDataDirectory)
        {
            PluginInformation returnVal = new PluginInformation()
            {
                InternalName = "popculture",
                Creator = "Logan Stromberg",
                MajorVersion = 1,
                MinorVersion = 0
            };

            returnVal.LocalizedInfo.Add("en-US", new LocalizedInformation()
            {
                DisplayName = "Pop Culture",
                ShortDescription = "I can be hip and cool too",
                SampleQueries = new List<string>()
            });

            return returnVal;
        }
    }
}
