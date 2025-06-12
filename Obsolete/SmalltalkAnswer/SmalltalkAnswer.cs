namespace Durandal.Answers.StandardAnswers.Smalltalk
{
    using System;

    using Durandal.API;
    using Durandal.API.Utils;
    using Stromberg.Utils.IO;
    using System.IO;
    using Durandal.CommonViews;
    using System.Collections.Generic;
    using System.Drawing.Imaging;

    public class SmalltalkAnswer : Answer
    {
        private ChatterBot _chatterBot = null;
        private ChatterBotSession _chatterSession = null;
        
        public SmalltalkAnswer()
            : base("smalltalk")
        {
        }

        protected override ConversationTree BuildConversationTree(ConversationTree returnVal)
        {
            //ConversationNode startConversationNode = returnVal.CreateNode(StartConversation);
            //ConversationNode continueConversationNode = returnVal.CreateNode(ContinueConversation);
            //startConversationNode.CreateCommonEdge(DialogConstants.SIDE_SPEECH_INTENT, continueConversationNode);
            //continueConversationNode.CreateCommonEdge(DialogConstants.SIDE_SPEECH_INTENT, continueConversationNode);
            //returnVal.AddStartState("start_conversation", startConversationNode);

            ConversationNode yourNameNode = returnVal.CreateNode(WhatsMyName);
            ConversationNode montyPythonQuestNode = returnVal.CreateNode(MyQuest);
            ConversationNode montyPythonColorNode = returnVal.CreateNode(MyFavoriteColor);
            ConversationNode montyPythonSwallowNode = returnVal.CreateNode(UnladenSwallow);
            yourNameNode.CreateNormalEdge("yourquest", montyPythonQuestNode);
            montyPythonQuestNode.CreateNormalEdge("yourcolor", montyPythonColorNode);
            montyPythonQuestNode.CreateNormalEdge("unladenswallow", montyPythonSwallowNode);
            returnVal.AddStartState("yourname", yourNameNode);
            //returnVal.AddStartState("start_conversation", StartConversation);

            return returnVal;
        }

        public DialogResult WhatsMyName(QueryWithContext queryWithContext, AnswerServices services)
        {
            return services.LanguageTemplate.GetPattern("MyName", queryWithContext.ClientContext, services.TraceId)
                .ApplyToDialogResult(new DialogResult(Result.Success) { MultiTurnResult = MultiTurnBehavior.ContinuePassively });
        }

        public DialogResult MyQuest(QueryWithContext queryWithContext, AnswerServices services)
        {
            return services.LanguageTemplate.GetPattern("MyQuest", queryWithContext.ClientContext, services.TraceId)
                .ApplyToDialogResult(new DialogResult(Result.Success) { MultiTurnResult = MultiTurnBehavior.ContinuePassively } );
        }

        public DialogResult UnladenSwallow(QueryWithContext queryWithContext, AnswerServices services)
        {
            return BridgeResponse(services, services.LanguageTemplate.GetPattern("IDontKnowThat", queryWithContext.ClientContext, services.TraceId));
        }

        public DialogResult MyFavoriteColor(QueryWithContext queryWithContext, AnswerServices services)
        {
            return BridgeResponse(services, services.LanguageTemplate.GetPattern("MyFavoriteColor", queryWithContext.ClientContext, services.TraceId));
        }

        /// <summary>
        /// Generates a .gif view and audio of the guy getting thrown off the bridge, preceeded by a specified LG response pattern
        /// </summary>
        /// <param name="services"></param>
        /// <param name="basePattern"></param>
        /// <returns></returns>
        private DialogResult BridgeResponse(AnswerServices services, LGPattern basePattern)
        {
            DialogResult returnVal = new DialogResult(Result.Success);

            ResourceName yellAudioFile = services.GetPluginDataDirectory() + "\\yell.raw";
            if (services.ResourceManager.Exists(yellAudioFile))
            {
                using (MemoryStream buffer = new MemoryStream())
                {
                    using (Stream audioFileIn = services.ResourceManager.ReadStream(yellAudioFile))
                    {
                        audioFileIn.CopyTo(buffer);
                        audioFileIn.Close();
                    }

                    returnVal.ResponseAudio = new AudioResponse(buffer.ToArray(), 16000, AudioOrdering.AfterSpeech);

                    buffer.Close();
                }
            }

            returnVal = basePattern.ApplyToDialogResult(returnVal);

            MessageView responseHtml = new MessageView()
            {
                Content = returnVal.ResponseText,
                Image = "/views/smalltalk/bridge.gif"
            };
            returnVal.ResponseHtml = responseHtml.Render();

            return returnVal;
        }

        #region Untested chatterbot routines

        private string TalkToBot(string inputText)
        {
            ChatterBotThought input = new ChatterBotThought()
            {
                Text = inputText
            };

            ChatterBotThought output = this._chatterSession.Think(input);
            return output.Text;
        }

        private MultiTurnBehavior GetDefaultMultiturnBehavior()
        {
            return new MultiTurnBehavior()
                {
                    Continues = true,
                    IsImmediate = true,
                    SuggestedPauseDelay = -1,
                    ConversationTimeoutSeconds = 30,
                };
        }

        public DialogResult StartConversation(QueryWithContext queryWithContext, AnswerServices services)
        {
            // Initialize the bot and make the first turn
            ChatterBotFactory factory = new ChatterBotFactory();
            this._chatterBot = factory.Create(ChatterBotType.CLEVERBOT);
            this._chatterSession = this._chatterBot.CreateSession();

            string botResponse = this.TalkToBot(queryWithContext.Result.Utterance.OriginalText);

            return new DialogResult(Result.Success)
                {
                    ResponseSSML = botResponse,
                    ResponseText = botResponse,
                    MultiTurnResult = this.GetDefaultMultiturnBehavior()
                };
        }

        public DialogResult ContinueConversationProxied(QueryWithContext queryWithContext, AnswerServices services)
        {
            string botResponse = this.TalkToBot(queryWithContext.Result.Utterance.OriginalText);

            return new DialogResult(Result.Success)
                {
                    ResponseSSML = botResponse,
                    ResponseText = botResponse,
                    MultiTurnResult = this.GetDefaultMultiturnBehavior()
                };
        }

        #endregion

        protected override AnswerPluginInformation GetInformation()
        {
            MemoryStream pngStream = new MemoryStream();
            AssemblyResources.Icon_smalltalk.Save(pngStream, ImageFormat.Png);

            AnswerPluginInformation returnVal = new AnswerPluginInformation()
            {
                InternalName = "smalltalk",
                Creator = "Logan Stromberg",
                MajorVersion = 1,
                MinorVersion = 0,
                IconPngData = new ArraySegment<byte>(pngStream.ToArray())
            };

            returnVal.LocalizedInfo.Add("en-us", new LocalizedInformation()
            {
                DisplayName = "Chitchat",
                ShortDescription = "Cleverbot",
                SampleQueries = new List<string>()
            });

            returnVal.LocalizedInfo["en-us"].SampleQueries.Add("What is your name?");
            returnVal.LocalizedInfo["en-us"].SampleQueries.Add("How are you doing?");
            returnVal.LocalizedInfo["en-us"].SampleQueries.Add("What's up?");

            return returnVal;
        }
    }
}
