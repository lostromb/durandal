
namespace Durandal.Plugins.Joke
{
        using Durandal.API;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.File;
    using Durandal.Common.IO;
    using Durandal.Common.Tasks;
    using Durandal.CommonViews;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    public class JokePlugin : DurandalPlugin
    {
        public JokePlugin() : base("joke") { }

        protected override IConversationTree BuildConversationTree(IConversationTree tree, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            IConversationNode turn1 = tree.CreateNode(this.TellAJoke, "TellJoke");
            IConversationNode positiveFeedback = tree.CreateNode(this.FeedbackPositive);
            IConversationNode negativeFeedback = tree.CreateNode(this.FeedbackNegative);
            turn1.CreateCommonEdge("elaboration", turn1);
            turn1.CreateCommonEdge("repeat", turn1);
            turn1.CreateNormalEdge("tell_another", turn1);
            turn1.CreateNormalEdge("feedback_good", positiveFeedback);
            turn1.CreateNormalEdge("feedback_bad", negativeFeedback);

            IConversationNode stopTalking = tree.CreateNode(StopTalking);
            turn1.CreateCommonEdge("stop_talking", stopTalking);

            positiveFeedback.CreateCommonEdge("elaboration", turn1);
            positiveFeedback.CreateCommonEdge("repeat", turn1);
            positiveFeedback.CreateNormalEdge("tell_another", turn1);

            negativeFeedback.CreateCommonEdge("elaboration", turn1);
            negativeFeedback.CreateCommonEdge("repeat", turn1);
            negativeFeedback.CreateNormalEdge("tell_another", turn1);

            tree.AddStartState("tell_a_joke", turn1);

            IConversationNode knockKnock1 = tree.CreateNode(KnockKnock1);
            IConversationNode knockKnock2 = tree.CreateNode(KnockKnock2);
            IConversationNode knockKnock3 = tree.CreateNode(KnockKnock3);
            knockKnock1.CreateCommonEdge("side_speech", knockKnock2);
            knockKnock2.CreateCommonEdge("side_speech", knockKnock3);
            tree.AddStartState("knock_knock", knockKnock1);

            return tree;
        }

        public async Task<PluginResult> TellAJoke(QueryWithContext queryWithContext, IPluginServices services)
        {
            List<TriggerKeyword> spotterPhrases = new List<TriggerKeyword>();
            spotterPhrases.Add(new TriggerKeyword()
            {
                TriggerPhrase = "another one",
                AllowBargeIn = false,
                ExpireTimeSeconds = 15
            });
            spotterPhrases.Add(new TriggerKeyword()
            {
                TriggerPhrase = "stop talking",
                AllowBargeIn = true,
                ExpireTimeSeconds = 0
            });
            spotterPhrases.Add(new TriggerKeyword()
            {
                TriggerPhrase = "shut up",
                AllowBargeIn = true,
                ExpireTimeSeconds = 0
            });
            
            ILGPattern pattern = services.LanguageGenerator.GetPattern("TellAJoke", queryWithContext.ClientContext, services.Logger);
            return await pattern.ApplyToDialogResult(new PluginResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.ContinuePassively,
                    TriggerKeywords = spotterPhrases,
                    ResponseHtml = new MessageView()
                    {
                        Content = (await pattern.Render().ConfigureAwait(false)).Text,
                        ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                    }.Render()
                }).ConfigureAwait(false);
        }

        public async Task<PluginResult> FeedbackPositive(QueryWithContext queryWithContext, IPluginServices services)
        {
            ILGPattern pattern = services.LanguageGenerator.GetPattern("FeedbackPositive", queryWithContext.ClientContext, services.Logger);
            return await pattern.ApplyToDialogResult(new PluginResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.ContinuePassively,
                    ResponseHtml = new MessageView()
                    {
                        Content = (await pattern.Render().ConfigureAwait(false)).Text,
                        ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                    }.Render()
                }).ConfigureAwait(false);
        }

        public async Task<PluginResult> StopTalking(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            return new PluginResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinuePassively,
                ResultConversationNode = "TellJoke"
            };
        }

        public async Task<PluginResult> FeedbackNegative(QueryWithContext queryWithContext, IPluginServices services)
        {
            ILGPattern pattern = services.LanguageGenerator.GetPattern("FeedbackNegative", queryWithContext.ClientContext, services.Logger);
            return await pattern.ApplyToDialogResult(new PluginResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.ContinuePassively,
                    ResponseHtml = new MessageView()
                    {
                        Content = (await pattern.Render().ConfigureAwait(false)).Text,
                        ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                    }.Render()
                }).ConfigureAwait(false);
        }

        public async Task<PluginResult> KnockKnock1(QueryWithContext queryWithContext, IPluginServices services)
        {
            ILGPattern pattern = services.LanguageGenerator.GetPattern("KnockKnock1", queryWithContext.ClientContext, services.Logger);
            return await pattern.ApplyToDialogResult(new PluginResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.ContinueBasic,
                    ResponseHtml = new MessageView()
                    {
                        Content = (await pattern.Render().ConfigureAwait(false)).Text,
                        ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                    }.Render()
                }).ConfigureAwait(false);
        }

        public async Task<PluginResult> KnockKnock2(QueryWithContext queryWithContext, IPluginServices services)
        {
            ILGPattern pattern = services.LanguageGenerator.GetPattern("KnockKnock2", queryWithContext.ClientContext, services.Logger)
                .Sub("text", queryWithContext.Understanding.Utterance.OriginalText);
            return await pattern.ApplyToDialogResult(new PluginResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.ContinueBasic,
                    ResponseHtml = new MessageView()
                    {
                        Content = (await pattern.Render().ConfigureAwait(false)).Text,
                        ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                    }.Render()
                }).ConfigureAwait(false);
        }

        public async Task<PluginResult> KnockKnock3(QueryWithContext queryWithContext, IPluginServices services)
        {
            ILGPattern pattern = services.LanguageGenerator.GetPattern("KnockKnock3", queryWithContext.ClientContext, services.Logger);
            return await pattern.ApplyToDialogResult(new PluginResult(Result.Success)
            {
                ResponseHtml = new MessageView()
                {
                    Content = (await pattern.Render().ConfigureAwait(false)).Text,
                    ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                }.Render()
            }).ConfigureAwait(false);
        }

        protected override PluginInformation GetInformation(IFileSystem pluginDataManager, VirtualPath pluginDataDirectory)
        {
            using (MemoryStream pngStream = new MemoryStream())
            {
                if (pluginDataDirectory != null && pluginDataManager != null)
                {
                    VirtualPath iconFile = pluginDataDirectory + "\\icon.png";
                    if (pluginDataManager.Exists(iconFile))
                    {
                        using (Stream iconStream = pluginDataManager.OpenStream(iconFile, FileOpenMode.Open, FileAccessMode.Read))
                        {
                            iconStream.CopyTo(pngStream);
                        }
                    }
                }

                PluginInformation returnVal = new PluginInformation()
                {
                    InternalName = "Joke",
                    Creator = "Logan Stromberg",
                    MajorVersion = 1,
                    MinorVersion = 0,
                    IconPngData = new ArraySegment<byte>(pngStream.ToArray())
                };

                returnVal.LocalizedInfo.Add("en-US", new LocalizedInformation()
                {
                    DisplayName = "Jokes",
                    ShortDescription = "Tells you a joke when you ask for one",
                    SampleQueries = new List<string>()
                });

                returnVal.LocalizedInfo["en-US"].SampleQueries.Add("Tell me a joke");

                return returnVal;
            }
        }
    }
}
