using Durandal.API;
using Durandal.Common.Utils;
using Durandal.Common.IO;
using Durandal.CommonViews;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Services;
using Durandal.Common.Tasks;
using Durandal.Common.File;
using Durandal.Common.MathExt;
using Durandal.Common.Audio;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.Client;

namespace Durandal.Plugins.SideSpeech
{
    public class CustomResponseGenerator : ICustomCodeProvider
    {
        private IRandom _rand;

        public CustomResponseGenerator(IRandom rand = null)
        {
            _rand = rand;
        }

        public PluginContinuation GetFunction(string functionName)
        {
            if (functionName.Equals("WhoBuiltMe"))
                return WhoBuiltMe;
            if (functionName.Equals("PersonalizedHello"))
                return PersonalizedHello;
            if (functionName.Equals("TeamRocket1"))
                return TeamRocket1;
            if (functionName.Equals("TeamRocket2"))
                return TeamRocket2;
            if (functionName.Equals("TeamRocket3"))
                return TeamRocket3;
            if (functionName.Equals("TeamRocket4"))
                return TeamRocket4;
            if (functionName.Equals("TeamRocket5"))
                return TeamRocket5;
            return null;
        }

        /// <summary>
        /// Someday this will all be generic
        /// </summary>
        /// <param name="queryWithContext"></param>
        /// <param name="services"></param>
        /// <returns></returns>
        public static async Task<PluginResult> WhoBuiltMe(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            PluginResult returnVal = new PluginResult(Result.Success)
            {
                ResponseText = "Logan Stromberg made this.",
                ResponseHtml = "<html><body style=\"padding:0px; margin:0px;\" ><img src=\"/views/side_speech/logan.jpg\" height=\"100%\" width=\"100%\" /></body></html>"
            };

            VirtualPath audioFile = services.PluginDataDirectory + "\\en-US\\Imadethis.raw";
            if (services.FileSystem.Exists(audioFile))
            {
                using (MemoryStream buffer = new MemoryStream())
                {
                    using (Stream audioFileIn = services.FileSystem.OpenStream(audioFile, FileOpenMode.Open, FileAccessMode.Read))
                    {
                        audioFileIn.CopyTo(buffer);
                        audioFileIn.Close();
                    }

                    returnVal.ResponseAudio = new AudioResponse(new AudioData()
                        {
                            Codec = RawPcmCodecFactory.CODEC_NAME,
                            CodecParams = CommonCodecParamHelper.CreateCodecParams(AudioSampleFormat.Mono(16000)),
                            Data = new ArraySegment<byte>(buffer.ToArray())
                        }, AudioOrdering.AfterSpeech);

                    buffer.Close();
                }
            }

            return returnVal;
        }

        public async Task<PluginResult> PersonalizedHello(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            int phraseId;
            if (services.SessionStore.ContainsKey("chat_phrase_id"))
            {
                phraseId = services.SessionStore.GetInt("chat_phrase_id") + 1;
            }
            else
            {
                phraseId = _rand.NextInt(0, 1000);
            }

            ILGPattern helloPattern = services.LanguageGenerator.GetPattern("PersonalizedHello", queryWithContext.ClientContext, services.Logger, false, phraseId);
            string userName = string.Empty;

            // Look in global user profile for the user's name
            if (services.GlobalUserProfile != null &&
                !services.GlobalUserProfile.TryGetString(ClientContextField.UserGivenName, out userName))
            {
                userName = string.Empty;
            }

            helloPattern = helloPattern.Sub("user_name", userName);
            RenderedLG renderedLG = await helloPattern.Render();

            PluginResult returnVal = new PluginResult(Result.Success);
            returnVal.ResponseText = renderedLG.Text;
            returnVal.ResponseSsml = renderedLG.Spoken;
            returnVal.ResponseHtml = new MessageView()
            {
                Content = renderedLG.Text,
                UseHtml5 = queryWithContext.ClientContext.Capabilities.HasFlag(ClientCapabilities.DisplayHtml5),
                ClientContextData = queryWithContext.ClientContext.ExtraClientContext
            }.Render();

            return returnVal;
        }

        // Team rocket go!
        public static async Task<PluginResult> TeamRocket1(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            List<TriggerKeyword> spotterPhrases = new List<TriggerKeyword>();
            spotterPhrases.Add(new TriggerKeyword()
            {
                TriggerPhrase = "devastation",
                AllowBargeIn = false,
                ExpireTimeSeconds = 15
            });

            string text = "Make it double!";
            return new PluginResult(Result.Success)
            {
                ResponseText = text,
                ResponseSsml = text,
                ResponseHtml = new MessageView()
                {
                    Content = text,
                    ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                }.Render(),
                MultiTurnResult = MultiTurnBehavior.ContinuePassively,
                TriggerKeywords = spotterPhrases,
                AugmentedQuery = "Prepare for trouble!"
            };
        }

        // After this point we are locking the user into the experience

        public static async Task<PluginResult> TeamRocket2(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            List<TriggerKeyword> spotterPhrases = new List<TriggerKeyword>();
            spotterPhrases.Add(new TriggerKeyword()
            {
                TriggerPhrase = "truth and love",
                AllowBargeIn = false,
                ExpireTimeSeconds = 15
            });

            string text = "To unite all people within our nation";
            return new PluginResult(Result.Success)
            {
                ResponseText = text,
                ResponseSsml = text,
                ResponseHtml = new MessageView()
                {
                    Content = text,
                    ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                }.Render(),
                MultiTurnResult = new MultiTurnBehavior()
                {
                    Continues = true,
                    FullConversationControl = true,
                    IsImmediate = false,
                    ConversationTimeoutSeconds = 15
                },
                TriggerKeywords = spotterPhrases,
                AugmentedQuery = "To protect the world from devastation!"
            };
        }

        public static async Task<PluginResult> TeamRocket3(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            List<TriggerKeyword> spotterPhrases = new List<TriggerKeyword>();
            spotterPhrases.Add(new TriggerKeyword()
            {
                TriggerPhrase = "jessie",
                AllowBargeIn = false,
                ExpireTimeSeconds = 15
            });

            string text = "To extend our reach to the stars above!";
            return new PluginResult(Result.Success)
            {
                ResponseText = text,
                ResponseSsml = text,
                ResponseHtml = new MessageView()
                {
                    Content = text,
                    ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                }.Render(),
                MultiTurnResult = new MultiTurnBehavior()
                {
                    Continues = true,
                    FullConversationControl = true,
                    IsImmediate = false,
                    ConversationTimeoutSeconds = 15
                },
                TriggerKeywords = spotterPhrases,
                AugmentedQuery = "To denounce the evils of truth and love!"
            };
        }

        public static async Task<PluginResult> TeamRocket4(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            List<TriggerKeyword> spotterPhrases = new List<TriggerKeyword>();
            spotterPhrases.Add(new TriggerKeyword()
            {
                TriggerPhrase = "speed of light",
                AllowBargeIn = false,
                ExpireTimeSeconds = 15
            });

            string text = "James!";
            return new PluginResult(Result.Success)
            {
                ResponseText = text,
                ResponseSsml = text,
                ResponseHtml = new MessageView()
                {
                    Content = text,
                    ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                }.Render(),
                MultiTurnResult = new MultiTurnBehavior()
                {
                    Continues  = true,
                    FullConversationControl = true,
                    IsImmediate = false,
                    ConversationTimeoutSeconds = 15
                },
                TriggerKeywords = spotterPhrases,
                AugmentedQuery = "Jessie!"
            };
        }

        public static async Task<PluginResult> TeamRocket5(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            string text = "Surrender now or prepare to fight!";
            return new PluginResult(Result.Success)
            {
                ResponseText = text,
                ResponseSsml = text,
                ResponseHtml = new MessageView()
                {
                    Content = text,
                    ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                }.Render(),
                AugmentedQuery = "Team Rocket blasts off at the speed of light!"
            };
        }
    }
}
