
namespace Durandal.Plugins.SideSpeech
{
    using Durandal.API;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.Utils;
    using Durandal.Common.File;
    using Durandal.Common.Logger;
    using Durandal.Common.Speech.TTS;
    using Durandal.Common.IO;
    using Durandal.Common.MathExt;
    using Durandal.Common.Tasks;
    using Durandal.CommonViews;
    using Durandal.ExternalServices.Bing.Search;
    using Durandal.ExternalServices.Bing.Search.Schemas;
    using Durandal.Common.Ontology;
    using Durandal.Plugins.Bing;
    using Durandal.Plugins.Bing.Views;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using SchemaDotOrg = Durandal.Plugins.Basic.SchemaDotOrg;
    using Durandal.Common.Audio;
    using Durandal.Common.Audio.Codecs;
    using Durandal.Common.Statistics;
    using Durandal.Common.NLP.Language;
    using System.Threading;
    using Durandal.Common.Time;

    /// <summary>
    /// This is an answer which catches side speech and either attempts to respond using chit-chat, or just falls back to "I don't know"
    /// </summary>
    public class SideSpeechPlugin : DurandalPlugin
    {
        private IDictionary<LanguageCode, ChitChatEngine> _chitChatEngines = new Dictionary<LanguageCode, ChitChatEngine>();
        private BingSearch _bingSearchApi;
        private IRandom _rand;

        public SideSpeechPlugin() : base(DialogConstants.SIDE_SPEECH_DOMAIN)
        {
            _rand = new FastRandom();
        }

        /// <summary>
        /// Unit test constructor
        /// </summary>
        public SideSpeechPlugin(IRandom rand) : base(DialogConstants.SIDE_SPEECH_DOMAIN)
        {
            _rand = rand;
        }

        protected override IConversationTree BuildConversationTree(IConversationTree tree, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            IConversationNode sideSpeechMultiturnNodeHigh = tree.CreateNode(SideSpeechHigh);
            IConversationNode sideSpeechMultiturnNodeLow = tree.CreateNode(SideSpeechLow);

            sideSpeechMultiturnNodeHigh.CreateNormalEdge(DialogConstants.SIDE_SPEECH_HIGHCONF_INTENT, sideSpeechMultiturnNodeHigh);
            sideSpeechMultiturnNodeHigh.CreateNormalEdge(DialogConstants.SIDE_SPEECH_INTENT, sideSpeechMultiturnNodeLow);
            sideSpeechMultiturnNodeLow.CreateNormalEdge(DialogConstants.SIDE_SPEECH_HIGHCONF_INTENT, sideSpeechMultiturnNodeHigh);
            sideSpeechMultiturnNodeLow.CreateNormalEdge(DialogConstants.SIDE_SPEECH_INTENT, sideSpeechMultiturnNodeLow);

            tree.AddStartState(DialogConstants.SIDE_SPEECH_HIGHCONF_INTENT, sideSpeechMultiturnNodeHigh);
            tree.AddStartState(DialogConstants.SIDE_SPEECH_INTENT, sideSpeechMultiturnNodeLow);

            IConversationNode selectEntityNode = tree.CreateNode(null, "selectEntityNode");
            IConversationNode showSingleEntityNode = tree.CreateNode(HandleEntitySelection);
            selectEntityNode.CreateCommonEdge("select", showSingleEntityNode);

            IConversationNode yourNameNode = tree.CreateNode(WhatsMyName);
            IConversationNode montyPythonQuestNode = tree.CreateNode(MyQuest);
            IConversationNode montyPythonColorNode = tree.CreateNode(MyFavoriteColor);
            IConversationNode montyPythonSwallowNode = tree.CreateNode(UnladenSwallow);
            yourNameNode.CreateNormalEdge("yourquest", montyPythonQuestNode);
            montyPythonQuestNode.CreateNormalEdge("yourcolor", montyPythonColorNode);
            montyPythonQuestNode.CreateNormalEdge("unladenswallow", montyPythonSwallowNode);
            tree.AddStartState("yourname", yourNameNode);

            return tree;
        }

        public override async Task OnLoad(IPluginServices services)
        {
            string searchAppId = null;
            if (services.PluginConfiguration.ContainsKey("SearchAppId"))
            {
                searchAppId = services.PluginConfiguration.GetString("SearchAppId");
            }

            _bingSearchApi = new BingSearch(searchAppId, services.HttpClientFactory, services.Logger, BingApiVersion.V7Internal);

            // Enumerate the possible locales
            VirtualPath mainDataDir = services.PluginDataDirectory;
            if (!services.FileSystem.Exists(mainDataDir) || services.FileSystem.WhatIs(mainDataDir) != ResourceType.Directory)
            {
                services.Logger.Log("No plugin data directory found; side speech will not load any chitchat data", LogLevel.Wrn);
                return;
            }

            foreach (VirtualPath localeFolder in await services.FileSystem.ListDirectoriesAsync(mainDataDir))
            {
                LanguageCode locale = LanguageCode.Parse(localeFolder.Name);
                
                // Load the chitchat definition files
                List<Stream> fileStreams = new List<Stream>();
                foreach (VirtualPath file in await services.FileSystem.ListFilesAsync(localeFolder))
                {
                    if (file.Name.EndsWith(".chat.ini", StringComparison.OrdinalIgnoreCase))
                    {
                        services.Logger.Log("Found chitchat file " + file.Name);
                        fileStreams.Add(await services.FileSystem.OpenStreamAsync(file, FileOpenMode.Open, FileAccessMode.Read));
                    }
                }

                if (fileStreams.Count > 0)
                {
                    services.Logger.Log("Creating a chit-chat engine for locale \"" + locale.ToBcp47Alpha2String() + "\" with " + fileStreams.Count + " input files");
                    ChitChatEngine engine = new ChitChatEngine(locale, services.Logger, _rand, new CustomResponseGenerator(_rand));
                    if (engine.Initialize(fileStreams, services.Logger))
                    {
                        services.Logger.Log("Chit-chat engine looks good");
                    }
                    else
                    {
                        services.Logger.Log("The chitchat engine failed either in loading or validation. There is a good chance that you will hit runtime errors", LogLevel.Err);
                    }

                    _chitChatEngines[locale] = engine;

                    // Create LU training files to improve side speech accuracy in the feedback loop
                    VirtualPath trainingFile = services.PluginDataDirectory + ("\\" + locale.ToBcp47Alpha2String() + "\\training.template");
                    await engine.CreateTrainingFile("common", "side_speech", trainingFile, services.FileSystem, services.Logger);
                }
            }
        }

        /// <summary>
        /// Triggered on high-confidence first-turn side speech, which in this case we usually try and resolve as chit-chat
        /// </summary>
        /// <param name="queryWithContext"></param>
        /// <param name="services"></param>
        /// <returns></returns>
        public async Task<PluginResult> SideSpeechHigh(QueryWithContext queryWithContext, IPluginServices services)
        {
            // Attempt to run chit-chat
            if (_chitChatEngines.ContainsKey(queryWithContext.ClientContext.Locale))
            {
                PluginResult chatResult = await _chitChatEngines[queryWithContext.ClientContext.Locale].AttemptChat(queryWithContext, services);
                if (chatResult != null)
                {
                    services.SessionStore.Remove("fallback_count");
                    return chatResult;
                }
            }

            return new PluginResult(Result.Skip);
        }

        /// <summary>
        /// This is the "capped" side_speech hyp, which usually means that everything else fell through and we need to tell the user we gave up
        /// </summary>
        /// <param name="queryWithContext"></param>
        /// <param name="services"></param>
        /// <returns></returns>
        public async Task<PluginResult> SideSpeechLow(QueryWithContext queryWithContext, IPluginServices services)
        {
            // First try to fallback to Bing facts which can answer lots of open-ended queries
            PluginResult bingResult = await TryReturnBingResults(queryWithContext, services);
            if (bingResult != null)
            {
                return bingResult;
            }
            
            // Then attempt to run chit-chat again
            if (_chitChatEngines.ContainsKey(queryWithContext.ClientContext.Locale))
            {
                PluginResult chatResult = await _chitChatEngines[queryWithContext.ClientContext.Locale].AttemptChat(queryWithContext, services);
                if (chatResult != null)
                {
                    return chatResult;
                }
            }

            // Otherwise give the bad fallback response
            int repeats = 0;
            if (services.SessionStore.ContainsKey("fallback_count"))
            {
                repeats = services.SessionStore.GetInt("fallback_count", 0);
            }

            services.SessionStore.Put("fallback_count", repeats + 1);

            if (repeats > 2)
            {
                ILGPattern pattern = services.LanguageGenerator.GetPattern("DontUnderstandAnything", queryWithContext.ClientContext, services.Logger, false, _rand.NextInt());
                return await pattern.ApplyToDialogResult(new PluginResult(Result.Skip)
                    {
                        MultiTurnResult = MultiTurnBehavior.ContinuePassively,
                        ResponseHtml = new MessageView()
                        {
                            Content = (await pattern.Render()).Text,
                            ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                        }.Render()
                    });
            }
            else
            {
                ILGPattern pattern = services.LanguageGenerator.GetPattern("DontUnderstand", queryWithContext.ClientContext, services.Logger, false, _rand.NextInt());
                return await pattern.ApplyToDialogResult(new PluginResult(Result.Skip)
                    {
                        MultiTurnResult = MultiTurnBehavior.ContinuePassively,
                        ResponseHtml = new MessageView()
                        {
                            Content = (await pattern.Render()).Text,
                            ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                        }.Render()
                    });
            }
        }

        private async Task<PluginResult> TryReturnBingResults(QueryWithContext queryWithContext, IPluginServices services)
        {
            BingResponse bingAnswer = await _bingSearchApi.Query(
                queryWithContext.Understanding.Utterance.OriginalText,
                services.Logger,
                CancellationToken.None,
                DefaultRealTimeProvider.Singleton,
                queryWithContext.ClientContext.Locale);
            if (bingAnswer != null)
            {
                // Show entities
                if (bingAnswer.EntityReferences != null && bingAnswer.EntityReferences.Count > 0)
                {
                    // Are facts also present? Then assume this is "entity card + fact about that entity" scenario, and show them both accordingly.
                    if (bingAnswer.EntityReferences.Count == 1)
                    {
                        if (bingAnswer.Facts != null && bingAnswer.Facts.Count == 1)
                        {
                            // FIXME actually incorporate the entity into this view somehow
                            BingFactResponse factResponse = bingAnswer.Facts[0];
                            string resultText = factResponse.Text;
                            string ssml = factResponse.Text;
                            if (!string.IsNullOrWhiteSpace(factResponse.Subtitle))
                            {
                                resultText += " · " + factResponse.Subtitle;
                            }

                            MessageView html = new MessageView()
                            {
                                Superscript = factResponse.Subtitle,
                                Content = resultText,
                                Subscript = "Answers by Bing™",
                                Title = factResponse.Subtitle,
                                UseHtml5 = true,
                                ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                            };

                            PluginResult bingResult = new PluginResult(Result.Success)
                            {
                                ResponseSsml = ssml,
                                ResponseText = resultText,
                                ResponseHtml = html.Render()
                            };

                            return bingResult;
                        }
                        else
                        {
                            // Show a single entity
                            Entity e = bingAnswer.KnowledgeContext.GetEntityInMemory(bingAnswer.EntityReferences[0]);
                            services.EntityHistory.AddOrUpdateEntity(e);
                            string responseText = EntityRenderer.RenderText(e);
                            return new PluginResult(Result.Success)
                            {
                                ResponseText = responseText,
                                ResponseSsml = responseText,
                                ResponseHtml = EntityRenderer.RenderHtml(e)
                            };
                        }
                    }
                    else
                    {
                        // Multi-entity results. Prompt for selection
                        List<Entity> entities = new List<Entity>();
                        foreach (string entityId in bingAnswer.EntityReferences)
                        {
                            entities.Add(bingAnswer.KnowledgeContext.GetEntityInMemory(entityId));
                        }

                        return ShowMultiEntitySelection(entities, queryWithContext, services);
                    }
                }
                // Show facts on their own as a fallback
                else if (bingAnswer.Facts != null && bingAnswer.Facts.Count > 0)
                {
                    BingFactResponse factResponse = bingAnswer.Facts[0];
                    string resultText = factResponse.Text;
                    string ssml = factResponse.Text;
                    if (!string.IsNullOrWhiteSpace(factResponse.Subtitle))
                    {
                        resultText += " · " + factResponse.Subtitle;
                    }

                    MessageView html = new MessageView()
                    {
                        Superscript = factResponse.Subtitle,
                        Content = resultText,
                        Subscript = "Answers by Bing™",
                        Title = factResponse.Subtitle,
                        UseHtml5 = true,
                        ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                    };

                    PluginResult bingResult = new PluginResult(Result.Success)
                    {
                        ResponseSsml = ssml,
                        ResponseText = resultText,
                        ResponseHtml = html.Render()
                    };

                    return bingResult;
                }
            }

            return null;
        }
        
        private PluginResult ShowMultiEntitySelection(IList<Entity> entities, QueryWithContext queryWithContext, IPluginServices services)
        {
            // Multi entity display with selection
            string prompt = "I found these results";
            StringBuilder responseString = new StringBuilder(prompt + " ");
            foreach (Entity e in entities)
            {
                responseString.Append(EntityRenderer.RenderText(e));
                responseString.Append(", ");
            }

            List<SelectableEntity> entitiesWithSelection = new List<SelectableEntity>();
            Dictionary<int, string> entityOrdinalMap = new Dictionary<int, string>();

            int ordinal = 1;
            foreach (Entity e in entities)
            {
                SlotValue ordinalSlot = new SlotValue("selection", ordinal.ToString(), SlotValueFormat.DialogActionParameter);
                ordinalSlot.Annotations.Add("Ordinal", ordinal.ToString());

                DialogAction selectionAction = new DialogAction()
                {
                    Domain = DialogConstants.COMMON_DOMAIN,
                    Intent = "select",
                    InteractionMethod = InputMethod.Tactile,
                    Slots = new List<SlotValue>()
                };

                selectionAction.Slots.Add(ordinalSlot);

                entitiesWithSelection.Add(new SelectableEntity()
                {
                    HtmlCard = EntityRenderer.RenderHtmlCard(e),
                    SelectActionUrl = services.RegisterDialogActionUrl(selectionAction, queryWithContext.ClientContext.ClientId)
                });

                // Also inject entities into history so they're available later
                services.EntityHistory.AddOrUpdateEntity(e);

                entityOrdinalMap[ordinal] = e.EntityId;
                ordinal++;
            }

            // Stash the ordering of what entities were shown so we can match back up to the ordinal later
            services.SessionStore.Put("entityOrdinalMap", entityOrdinalMap);

            MultiEntityView html = new MultiEntityView()
            {
                Entities = entitiesWithSelection,
                Header = prompt
            };

            PluginResult bingResult = new PluginResult(Result.Success)
            {
                ResponseSsml = prompt,
                ResponseText = responseString.ToString(),
                ResponseHtml = html.Render(),
                ResultConversationNode = "selectEntityNode",
                MultiTurnResult = MultiTurnBehavior.ContinuePassively
            };

            return bingResult;
        }

        public async Task<PluginResult> HandleEntitySelection(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            Entity selectedEntity = FetchEntityPostSelection(queryWithContext, services);

            if (selectedEntity == null || !selectedEntity.IsA<SchemaDotOrg.Thing>())
            {
                return new PluginResult(Result.Skip);
            }

            // TODO: Delete all the other entities that weren't selected?
            services.EntityHistory.AddOrUpdateEntity(selectedEntity);
            string responseText = EntityRenderer.RenderText(selectedEntity);
            return new PluginResult(Result.Success)
            {
                ResponseText = responseText,
                ResponseSsml = responseText,
                ResponseHtml = EntityRenderer.RenderHtml(selectedEntity)
            };
        }

        private static Entity FetchEntityPostSelection(QueryWithContext queryWithContext, IPluginServices services)
        {
            // First, get the ordinal map we stashed earlier
            Dictionary<int, string> entityOrdinalMap = services.SessionStore.GetObject<Dictionary<int, string>>("entityOrdinalMap");

            if (entityOrdinalMap == null)
            {
                services.Logger.Log("No cached ordinal map; cannot perform selection", LogLevel.Err);
                return null;
            }

            // Now get the slot
            SlotValue ordinalSlot = DialogHelpers.TryGetSlot(queryWithContext.Understanding, "selection");
            if (ordinalSlot == null)
            {
                services.Logger.Log("No ordinal slot", LogLevel.Err);
                return null;
            }

            Ordinal ord = ordinalSlot.GetOrdinal();

            if (ord == null)
            {
                services.Logger.Log("No ordinal slot", LogLevel.Err);
                return null;
            }

            if (ord.Type == OrdinalType.Number)
            {
                int ordinal = ord.NumericValue;
                if (!entityOrdinalMap.ContainsKey(ordinal))
                {
                    services.Logger.Log("Ordinal out of range", LogLevel.Err);
                    return null;
                }

                string selectedGuid = entityOrdinalMap[ordinal];
                // Fixme: there should be a function to retrieve a specific entity from history
                Hypothesis<Entity> selectedEntity = services.EntityHistory.FindEntities<Entity>().Where((e) => e.Value.EntityId == selectedGuid).SingleOrDefault();

                if (selectedEntity == null)
                {
                    services.Logger.Log("Selected entity doesn't exist", LogLevel.Err);
                    return null;
                }

                return selectedEntity.Value;
            }
            else
            {
                services.Logger.Log("Positional selection not yet implemented", LogLevel.Err);
                return null;
            }
        }

        // Monty python multiturn segment.
        // Can't be a regular multiturn chitchat because we need the 3rd turn to be reliable
        // Really though this should use the new custom handler script

        public async Task<PluginResult> WhatsMyName(QueryWithContext queryWithContext, IPluginServices services)
        {
            ILGPattern pattern = services.LanguageGenerator.GetPattern("MyName", queryWithContext.ClientContext, services.Logger, false, _rand.NextInt());
            return await pattern.ApplyToDialogResult(new PluginResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinuePassively,
                ResponseHtml = new MessageView()
                {
                    Content = (await pattern.Render()).Text,
                    ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                }.Render()
            });
        }

        public async Task<PluginResult> MyQuest(QueryWithContext queryWithContext, IPluginServices services)
        {
            ILGPattern pattern = services.LanguageGenerator.GetPattern("MyQuest", queryWithContext.ClientContext, services.Logger, false, _rand.NextInt());
            return await pattern.ApplyToDialogResult(new PluginResult(Result.Success)
            {
                MultiTurnResult = MultiTurnBehavior.ContinuePassively,
                ResponseHtml = new MessageView()
                {
                    Content = (await pattern.Render()).Text,
                    ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                }.Render()
            });
        }

        public async Task<PluginResult> UnladenSwallow(QueryWithContext queryWithContext, IPluginServices services)
        {
            return await BridgeResponse(queryWithContext, services, services.LanguageGenerator.GetPattern("IDontKnowThat", queryWithContext.ClientContext, services.Logger, false, _rand.NextInt()));
        }

        public async Task<PluginResult> MyFavoriteColor(QueryWithContext queryWithContext, IPluginServices services)
        {
            return await BridgeResponse(queryWithContext, services, services.LanguageGenerator.GetPattern("MyFavoriteColor", queryWithContext.ClientContext, services.Logger, false, _rand.NextInt()));
        }

        /// <summary>
        /// Generates a .gif view and audio of the guy getting thrown off the bridge, preceeded by a specified LG response pattern
        /// </summary>
        /// <param name="services"></param>
        /// <param name="basePattern"></param>
        /// <returns></returns>
        private async Task<PluginResult> BridgeResponse(QueryWithContext queryWithContext, IPluginServices services, ILGPattern basePattern)
        {
            PluginResult returnVal = new PluginResult(Result.Success);

            VirtualPath yellAudioFile = services.PluginDataDirectory + "\\en-US\\yell.raw";
            if (services.FileSystem.Exists(yellAudioFile))
            {
                using (MemoryStream buffer = new MemoryStream())
                {
                    using (Stream audioFileIn = services.FileSystem.OpenStream(yellAudioFile, FileOpenMode.Open, FileAccessMode.Read))
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

            returnVal = await basePattern.ApplyToDialogResult(returnVal);
            
            returnVal.ResponseHtml = new MessageView()
            {
                Content = returnVal.ResponseText,
                Image = "/views/side_speech/bridge.gif",
                ClientContextData = queryWithContext.ClientContext.ExtraClientContext
            }.Render();

            return returnVal;
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
                InternalName = "SideSpeech",
                Creator = "Logan Stromberg",
                MajorVersion = 1,
                MinorVersion = 0,
                IconPngData = new ArraySegment<byte>(pngStream.ToArray())
            };

            returnVal.LocalizedInfo.Add("en-US", new LocalizedInformation()
            {
                DisplayName = "Chitchat",
                ShortDescription = "Friendly conversations, anytime",
                SampleQueries = new List<string>()
            });

            returnVal.LocalizedInfo["en-US"].SampleQueries.Add("hello");

            return returnVal;
        }
    }
}
