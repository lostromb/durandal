

namespace Durandal.Answers.TranslateAnswer
{
    using Durandal.API;
    using Durandal.Common.Audio;
    using Durandal.Common.Audio.Codecs;
    using Durandal.Common.Audio.Components;
    using Durandal.Common.Client.Actions;
    using Durandal.Common.Config;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.File;
    using Durandal.Common.IO;
    using Durandal.Common.LG;
    using Durandal.Common.Logger;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.Speech.SR;
    using Durandal.Common.Speech.TTS;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using Durandal.CommonViews;
    using Durandal.ExternalServices.Bing;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    public class TranslateAnswer : DurandalPlugin
    {
        private BingTranslator _translator;

        public TranslateAnswer()
            : base("translate")
        {
        }

        protected override IConversationTree BuildConversationTree(IConversationTree tree, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            IConversationNode translatePhraseNode = tree.CreateNode(TranslatePhrase);
            IConversationNode enterTranslationNode = tree.CreateNode(EnterTranslateMode);
            IConversationNode exitTranslationNode = tree.CreateNode(ExitTranslateMode);
            IConversationNode autoTranslateLoop = tree.CreateNode(AutoTranslate);
            enterTranslationNode.CreateCommonEdge("side_speech", autoTranslateLoop);
            enterTranslationNode.CreateCommonEdge("noreco", autoTranslateLoop);
            autoTranslateLoop.CreateCommonEdge("side_speech", autoTranslateLoop);
            autoTranslateLoop.CreateCommonEdge("noreco", autoTranslateLoop);
            enterTranslationNode.CreateNormalEdge("exit_translate_mode", exitTranslationNode);

            tree.AddStartState("translate_phrase", translatePhraseNode);
            tree.AddStartState("enter_translate_mode", enterTranslationNode);
            return tree;
        }

        public override async Task OnLoad(IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            IConfiguration pluginConfig = services.PluginConfiguration;
            string translateApiKey = string.Empty;

            if (pluginConfig.ContainsKey("TranslateApiKey"))
            {
                translateApiKey = pluginConfig.GetString("TranslateApiKey");
            }

            _translator = new BingTranslator(translateApiKey, services.Logger, services.HttpClientFactory, DefaultRealTimeProvider.Singleton);
        }

        public override async Task OnUnload(IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            _translator.Dispose();
        }

        private static MultiTurnBehavior GetLockInBehavior()
        {
            return new MultiTurnBehavior()
            {
                Continues = true,
                ConversationTimeoutSeconds = 30,
                FullConversationControl = true,
                IsImmediate = false
            };
        }

        public async Task<PluginResult> ExitTranslateMode(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            ILGPattern pattern = services.LanguageGenerator.GetPattern("DoneTranslating", queryWithContext.ClientContext, services.Logger);

            MessageView html = new MessageView()
            {
                Title = "Translation",
                Content = (await pattern.Render()).Text,
                UseHtml5 = true,
                ClientContextData = queryWithContext.ClientContext.ExtraClientContext
            };

            return await pattern.ApplyToDialogResult(new PluginResult(Result.Success)
            {
                ResponseHtml = html.Render()
            });
        }

        public async Task<PluginResult> EnterTranslateMode(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            // Start the translate session and create a state
            string language1 = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "language1");
            string language2 = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "language2");

            if (string.IsNullOrEmpty(language1) || string.IsNullOrEmpty(language2))
            {
                return new PluginResult(Result.Skip);
            }

            services.SessionStore.Put("language1", language1);
            services.SessionStore.Put("language2", language2);

            ILGPattern pattern = services.LanguageGenerator.GetPattern("ReadyToTranslate", queryWithContext.ClientContext)
                .Sub("lang1", GetTranslationLanguageName(services.LanguageGenerator, queryWithContext.ClientContext, GetTranslationLanguageCode(language1).Iso639_1))
                .Sub("lang2", GetTranslationLanguageName(services.LanguageGenerator, queryWithContext.ClientContext, GetTranslationLanguageCode(language2).Iso639_1));
            string responseText = (await pattern.Render()).Text;

            return new PluginResult(Result.Success)
            {
                ResponseText = responseText,
                ResponseSsml = responseText,
                MultiTurnResult = GetLockInBehavior(),
                ClientAction = JsonConvert.SerializeObject(new SendNextTurnAudioAction())
            };
        }

        public async Task<PluginResult> AutoTranslate(QueryWithContext queryWithContext, IPluginServices services)
        {
            services.Logger.Log("Locking into translation multiturn");
            // Try and get the session
            string language1, language2;
            if (!services.SessionStore.TryGetString("language1", out language1) ||
                !services.SessionStore.TryGetString("language2", out language2))
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "An error occurred while retrieving translation session state"
                };
            }

            string utterance = string.Empty;
            string sourceLanguage = language1;

            // Is it an audio query?
            if (queryWithContext.InputAudio != null && queryWithContext.InputAudio.Data != null && queryWithContext.InputAudio.Data.Count > 0)
            {
                services.Logger.Log("Client sent audio - Attempting to determine input language");

                ISpeechRecognizerFactory speechReco = services.SpeechRecoEngine;
                if (speechReco == null)
                {
                    services.Logger.Log("No speech reco is available. Cannot continue");
                }

                // Run multiple SR to determine input language
                SpeechRecognitionResult recoResults1;
                SpeechRecognitionResult recoResults2;
                IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.None);
                using (ISpeechRecognizer reco1 = await speechReco.CreateRecognitionStream(graph, "Language1Recognizer", GetTTSLanguageCode(language1), services.Logger, CancellationToken.None, DefaultRealTimeProvider.Singleton))
                using (ISpeechRecognizer reco2 = await speechReco.CreateRecognitionStream(graph, "Language2Recognizer", GetTTSLanguageCode(language2), services.Logger, CancellationToken.None, DefaultRealTimeProvider.Singleton))
                using (MemoryStream audioDataStream = new MemoryStream(queryWithContext.InputAudio.Data.Array, queryWithContext.InputAudio.Data.Offset, queryWithContext.InputAudio.Data.Count, false))
                using (AudioDecoder decoder = new RawPcmDecoder(graph, reco1.InputFormat, null))
                {
                    await decoder.Initialize(new NonRealTimeStreamWrapper(audioDataStream, false), false, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    using (AudioSplitter splitter = new AudioSplitter(graph, decoder.OutputFormat, null))
                    using (ChannelMixer channelMixer = new ChannelMixer(graph, decoder.OutputFormat.SampleRateHz, decoder.OutputFormat.ChannelMapping, reco1.InputFormat.ChannelMapping, null))
                    using (ResamplingFilter resampler = new ResamplingFilter(
                        graph,
                        null,
                        reco1.InputFormat.NumChannels,
                        reco1.InputFormat.ChannelMapping,
                        decoder.OutputFormat.SampleRateHz,
                        reco1.InputFormat.SampleRateHz))
                    {
                        decoder.ConnectOutput(channelMixer);
                        channelMixer.ConnectOutput(resampler);
                        resampler.ConnectOutput(splitter);
                        splitter.AddOutput(reco1);
                        splitter.AddOutput(reco2);

                        await decoder.ReadFully(CancellationToken.None, DefaultRealTimeProvider.Singleton);

                        recoResults1 = await reco1.FinishUnderstandSpeech(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                        recoResults2 = await reco2.FinishUnderstandSpeech(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }
                }

                // Find the highest utterance
                SpeechRecognizedPhrase bestResult = new SpeechRecognizedPhrase()
                {
                    DisplayText = queryWithContext.Understanding.Utterance.OriginalText,
                    SREngineConfidence = 0.0f
                };

                string inputLanguage = language1;
                foreach (SpeechRecognizedPhrase hyp1 in recoResults1.RecognizedPhrases)
                {
                    if (hyp1.SREngineConfidence > bestResult.SREngineConfidence)
                    {
                        bestResult = hyp1;
                        sourceLanguage = language1;
                    }
                }
                foreach (SpeechRecognizedPhrase hyp2 in recoResults2.RecognizedPhrases)
                {
                    if (hyp2.SREngineConfidence > bestResult.SREngineConfidence)
                    {
                        bestResult = hyp2;
                        sourceLanguage = language2;
                    }
                }

                utterance = bestResult.DisplayText;
            }
            else
            {
                utterance = queryWithContext.Understanding.Utterance.OriginalText;

                LanguageCode sourceLanguageCode = await _translator.DetectLanguage(utterance, services.Logger, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                sourceLanguage = language1;

                if (sourceLanguageCode == null)
                {
                    services.Logger.Log("Language detection FAILED, assuming source lang is " + sourceLanguage, LogLevel.Wrn);
                }
                else
                {
                    sourceLanguage = GetTranslationLanguageName(sourceLanguageCode);
                }
            }

            string targetLanguage = language2;

            if (string.Equals(sourceLanguage, language2, StringComparison.OrdinalIgnoreCase))
            {
                targetLanguage = language1;
            }

            services.Logger.Log("Source language is " + sourceLanguage);
            services.Logger.Log("Target language is " + targetLanguage);

            string translatedPhrase = await _translator.TranslateText(
                utterance,
                services.Logger,
                CancellationToken.None,
                DefaultRealTimeProvider.Singleton,
                GetTranslationLanguageCode(targetLanguage),
                GetTranslationLanguageCode(sourceLanguage)).ConfigureAwait(false);

            LanguageCode languageCode = GetTTSLanguageCode(targetLanguage);
            AudioData audioData = null;
            if (services.TTSEngine != null && services.TTSEngine.IsLocaleSupported(languageCode))
            {
                ISpeechSynth synth = services.TTSEngine;
                SpeechSynthesisRequest synthRequest = new SpeechSynthesisRequest()
                {
                    Plaintext = translatedPhrase,
                    Locale = languageCode,
                    VoiceGender = VoiceGender.Unspecified,
                    Ssml = null
                };
                audioData = (await synth.SynthesizeSpeechAsync(synthRequest, CancellationToken.None, DefaultRealTimeProvider.Singleton, services.Logger)).Audio;
            }

            ILGPattern pattern = services.LanguageGenerator.GetPattern("TranslationSubtitleFromTo", queryWithContext.ClientContext)
                .Sub("utterance", utterance)
                .Sub("sourceLang", GetTranslationLanguageName(services.LanguageGenerator, queryWithContext.ClientContext, GetTranslationLanguageCode(sourceLanguage).Iso639_1))
                .Sub("targetLang", GetTranslationLanguageName(services.LanguageGenerator, queryWithContext.ClientContext, GetTranslationLanguageCode(targetLanguage).Iso639_1));
            
            MessageView html = new MessageView()
            {
                Title = "Translation",
                Content = translatedPhrase,
                Subscript = (await pattern.Render()).Text,
                UseHtml5 = true,
                ClientContextData = queryWithContext.ClientContext.ExtraClientContext
            };

            return new PluginResult(Result.Success)
            {
                ResponseText = translatedPhrase,
                ResponseSsml = string.Empty,
                ResponseAudio = audioData == null ? null : new AudioResponse(audioData, AudioOrdering.BeforeSpeech),
                ResponseHtml = html.Render(),
                AugmentedQuery = utterance,
                MultiTurnResult = GetLockInBehavior(),
                ClientAction = JsonConvert.SerializeObject(new SendNextTurnAudioAction())
            };
        }

        public async Task<PluginResult> TranslatePhrase(QueryWithContext queryWithContext, IPluginServices services)
        {
            string sourcePhrase = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "phrase");
            string targetLanguage = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "targetlanguage");

            if (string.IsNullOrEmpty(sourcePhrase))
            {
                services.Logger.Log("No source phrase to translate!", LogLevel.Err);
                return new PluginResult(Result.Skip);
            }
            if (string.IsNullOrEmpty(targetLanguage))
            {
                services.Logger.Log("Translate domain: No target language! (todo: prompt user for target language?)", LogLevel.Err);
                return new PluginResult(Result.Skip);
            }

            LanguageCode sourceLanguage = queryWithContext.ClientContext.Locale;

            LanguageCode targetLanguageCode = GetTranslationLanguageCode(targetLanguage);
            string translatedText = await _translator.TranslateText(
                sourcePhrase,
                services.Logger,
                CancellationToken.None,
                DefaultRealTimeProvider.Singleton,
                targetLanguageCode,
                sourceLanguage);

            AudioData audioData = null;

            LanguageCode languageCode = GetTTSLanguageCode(targetLanguage);
            if (queryWithContext.ClientContext.GetCapabilities().HasFlag(ClientCapabilities.HasSpeakers) &&
                services.TTSEngine != null &&
                services.TTSEngine.IsLocaleSupported(languageCode))
            {
                ISpeechSynth synth = services.TTSEngine;
                SpeechSynthesisRequest synthRequest = new SpeechSynthesisRequest()
                {
                    Plaintext = translatedText,
                    Locale = languageCode,
                    VoiceGender = VoiceGender.Unspecified,
                    Ssml = null
                };
                audioData = (await synth.SynthesizeSpeechAsync(synthRequest, CancellationToken.None, DefaultRealTimeProvider.Singleton, services.Logger)).Audio;
            }

            ILGPattern pattern = services.LanguageGenerator.GetPattern("TranslationSubtitleTo", queryWithContext.ClientContext)
                .Sub("utterance", sourcePhrase)
                .Sub("targetLang", GetTranslationLanguageName(services.LanguageGenerator, queryWithContext.ClientContext, targetLanguageCode.Iso639_1));

            MessageView html = new MessageView()
                {
                    Title = "Translation",
                    Content = translatedText,
                    Subscript = (await pattern.Render()).Text,
                    UseHtml5 = true,
                    ClientContextData = queryWithContext.ClientContext.ExtraClientContext
            };

            return new PluginResult(Result.Success)
            {
                ResponseText = translatedText,
                ResponseSsml = string.Empty,
                ResponseAudio = audioData == null ? null : new AudioResponse(audioData, AudioOrdering.BeforeSpeech),
                ResponseHtml = html.Render()
            };
        }

        private static async Task<string> GetTranslationLanguageName(ILGEngine lgEngine, ClientContext context, string code)
        {
            string patternKey = "lang-" + code;
            ILGPattern pattern = lgEngine.GetPattern(patternKey, context);
            if (pattern == null)
            {
                return "Unknown Language";
            }

            return (await pattern.Render()).Text;
        }

        private static IDictionary<string, LanguageCode> GetTranslationMap()
        {
            IDictionary<string, LanguageCode> returnVal = new Dictionary<string, LanguageCode>();
            returnVal.Add("SPANISH", LanguageCode.Parse("es"));
            returnVal.Add("ENGLISH", LanguageCode.Parse("en"));
            returnVal.Add("GERMAN", LanguageCode.Parse("de"));
            returnVal.Add("FRENCH", LanguageCode.Parse("fr"));
            returnVal.Add("ITALIAN", LanguageCode.Parse("it"));
            returnVal.Add("JAPANESE", LanguageCode.Parse("ja"));
            returnVal.Add("CANTONESE", LanguageCode.Parse("zh-CHT"));
            returnVal.Add("MANDARIN", LanguageCode.Parse("zh-CHS"));
            returnVal.Add("RUSSIAN", LanguageCode.Parse("ru"));
            returnVal.Add("FINNISH", LanguageCode.Parse("fi"));
            returnVal.Add("SWEDISH", LanguageCode.Parse("sv"));
            returnVal.Add("DUTCH", LanguageCode.Parse("pl"));
            return returnVal;
        }

        private static IDictionary<string, LanguageCode> GetTTSMap()
        {
            IDictionary<string, LanguageCode> returnVal = new Dictionary<string, LanguageCode>();
            returnVal.Add("SPANISH", LanguageCode.Parse("es-es"));
            returnVal.Add("ENGLISH", LanguageCode.Parse("en-US"));
            returnVal.Add("GERMAN", LanguageCode.Parse("de-de"));
            returnVal.Add("FRENCH", LanguageCode.Parse("fr-fr"));
            returnVal.Add("ITALIAN", LanguageCode.Parse("it-it"));
            returnVal.Add("JAPANESE", LanguageCode.Parse("ja-jp"));
            returnVal.Add("CANTONESE", LanguageCode.Parse("zh-cn"));
            returnVal.Add("MANDARIN", LanguageCode.Parse("zh-cn"));
            returnVal.Add("RUSSIAN", LanguageCode.Parse("ru-ru"));
            returnVal.Add("FINNISH", LanguageCode.Parse("fi-fl"));
            returnVal.Add("SWEDISH", LanguageCode.Parse("sv-se"));
            returnVal.Add("DUTCH", LanguageCode.Parse("nl-nl"));
            return returnVal;
        }

        private static string GetTranslationLanguageName(LanguageCode languageCode)
        {
            IDictionary<string, LanguageCode> map = GetTranslationMap();
            foreach (KeyValuePair<string, LanguageCode> x in map)
            {
                if (x.Value.Equals(languageCode))
                {
                    return x.Key;
                }
            }
            return "ENGLISH";
        }

        private static LanguageCode GetTranslationLanguageCode(string languageName)
        {
            languageName = languageName.ToUpperInvariant();
            IDictionary<string, LanguageCode> map = GetTranslationMap();
            if (map.ContainsKey(languageName))
            {
                return map[languageName];
            }
            return LanguageCode.ENGLISH;
        }

        private static LanguageCode GetTTSLanguageCode(string languageName)
        {
            languageName = languageName.ToUpperInvariant();
            IDictionary<string, LanguageCode> map = GetTTSMap();
            if (map.ContainsKey(languageName))
            {
                return map[languageName];
            }
            return LanguageCode.Parse("en-US");
        }

        protected override PluginInformation GetInformation(IFileSystem pluginDataManager, VirtualPath pluginDataDirectory)
        {
            MemoryStream pngStream = new MemoryStream();
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
                InternalName = "Translate",
                Creator = "Logan Stromberg",
                MajorVersion = 1,
                MinorVersion = 0,
                IconPngData = new ArraySegment<byte>(pngStream.ToArray())
            };

            returnVal.LocalizedInfo.Add("en-US", new LocalizedInformation()
            {
                DisplayName = "Translation",
                ShortDescription = "Translates phrases and conversations",
                SampleQueries = new List<string>()
            });

            returnVal.LocalizedInfo["en-US"].SampleQueries.Add("How do you say good morning in Japanese?");
            returnVal.LocalizedInfo["en-US"].SampleQueries.Add("Start translating between English and Spanish");

            return returnVal;
        }
    }
}
