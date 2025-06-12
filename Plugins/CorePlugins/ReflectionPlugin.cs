
namespace Durandal.Plugins.Reflection
{
    using Durandal;
    using Durandal.API;
    using Durandal.Common.Client;
    using Durandal.Common.Client.Actions;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.File;
    using Durandal.Common.IO.Json;
    using Durandal.Common.MathExt;
    using Durandal.Common.Net.Http;
    using Durandal.Common.NLP;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.Statistics;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using Durandal.CommonViews;
    using Durandal.ExternalServices;
    using Durandal.ExternalServices.Twilio;
    using Durandal.Plugins.Reflection.Views;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    public class ReflectionPlugin : DurandalPlugin
    {
        private readonly IRandom _rand;
        private TwilioInterface _phoneLoginInterface;
        private TinyUrl _urlShortener;

        public ReflectionPlugin() : base(DialogConstants.REFLECTION_DOMAIN)
        {
            _rand = new FastRandom();
        }

        public ReflectionPlugin(IRandom random) : base(DialogConstants.REFLECTION_DOMAIN)
        {
            _rand = random;
        }

        public override async Task OnLoad(IPluginServices services)
        {
            _phoneLoginInterface = new TwilioInterface("7ed9f49f5a70501b36601b56e80e8e79", "ACfff69aa5bafa82e493afccf48d7ca380", services.HttpClientFactory, services.Logger);
            _urlShortener = new TinyUrl(services.HttpClientFactory, services.Logger);
            await DurandalTaskExtensions.NoOpTask;
        }

        protected override IConversationTree BuildConversationTree(IConversationTree returnVal, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            IConversationNode listPluginsNode = returnVal.CreateNode(this.ListAllPlugins);
            IConversationNode pluginDetailNode = returnVal.CreateNode(this.GetPluginDetail);

            listPluginsNode.CreateNormalEdge("get_plugin_details", pluginDetailNode);
            pluginDetailNode.CreateNormalEdge("list_plugins", listPluginsNode);

            returnVal.AddStartState("list_plugins", listPluginsNode);
            
            returnVal.AddStartState("error", this.Error);
            returnVal.AddStartState("greet", this.Greet);

            IConversationNode disambiguateNode = returnVal.CreateNode(DisambiguateStep1);
            IConversationNode selectionNode = returnVal.CreateNode(DisambiguationSelection);
            disambiguateNode.CreateCommonEdge("side_speech", selectionNode);
            disambiguateNode.CreateNormalEdge("direct_selection", selectionNode);

            returnVal.AddStartState("disambiguate", disambiguateNode);

            IConversationNode startDeleteUserDataNode = returnVal.CreateNode(StartDeleteUserData);
            IConversationNode confirmDeleteUserDataNode = returnVal.CreateNode(ConfirmDeleteUserData);
            IConversationNode cancelDeleteUserDataNode = returnVal.CreateNode(CancelDeleteUserData);
            
            startDeleteUserDataNode.CreateCommonEdge("confirm", confirmDeleteUserDataNode);
            startDeleteUserDataNode.CreateCommonEdge("deny", cancelDeleteUserDataNode);
            startDeleteUserDataNode.CreateCommonEdge("side_speech", cancelDeleteUserDataNode);
            returnVal.AddStartState("delete_user_data", startDeleteUserDataNode);

            IConversationNode loginTemporaryActionNode = returnVal.CreateNode(LoginTemporary1);
            IConversationNode loginTemporaryActionNode2 = returnVal.CreateNode(LoginTemporary2);
            loginTemporaryActionNode.CreateCommonEdge(DialogConstants.SIDE_SPEECH_INTENT, loginTemporaryActionNode2);
            returnVal.AddStartState("login", loginTemporaryActionNode);

            returnVal.AddStartState("login_success", LoginTemporary3);

            return returnVal;
        }

        private IDictionary<string, PluginInformation> GetPluginInfoFromSession(IPluginServices services)
        {
            // Check that the results are in the store
            string serializedInfo;
            if (!services.SessionStore.TryGetString("pluginInformation", out serializedInfo))
            {
                return null;
            }

            Dictionary<string, PluginInformation> pluginInfo = JsonConvert.DeserializeObject<Dictionary<string, PluginInformation>>(serializedInfo, new JsonByteArrayConverter());

            // Remove the data from the session immediately once we've got it because we don't want to keep this huge object in our serialized session
            services.SessionStore.Remove("pluginInformation");

            return pluginInfo;
        }

        public async Task<PluginResult> ListAllPlugins(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            // Make sure the client can handle HTML
            if (!queryWithContext.ClientContext.GetCapabilities().HasFlag(ClientCapabilities.DisplayBasicHtml) &&
                !queryWithContext.ClientContext.GetCapabilities().HasFlag(ClientCapabilities.DisplayHtml5))
            {
                return await services.LanguageGenerator.GetPattern("NoDisplayMessage", queryWithContext.ClientContext, services.Logger)
                               .ApplyToDialogResult(new PluginResult(Result.Success));
            }

            IDictionary<string, PluginInformation> pluginInfo = GetPluginInfoFromSession(services);
            if (pluginInfo == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Dialog engine failure: did not pass PluginInformation to object store"
                };
            }

            // Display all information about installed plugins
            if (pluginInfo.ContainsKey("reflection"))
            {
                pluginInfo.Remove("reflection");
            }

            return new PluginResult(Result.Success)
            {
                ResponseText = string.Format("I have {0} plugins installed", pluginInfo.Count),
                ResponseSsml = "This is what I can do for you",
                ResponseHtml = this.GeneratePluginListHtml(pluginInfo, queryWithContext.ClientContext.ClientId, services, queryWithContext.ClientContext.Locale),
                MultiTurnResult = new MultiTurnBehavior()
                {
                    Continues = true,
                    ConversationTimeoutSeconds = 3600,
                    IsImmediate = false
                }
            };
        }

        // Display information about one plugin
        public async Task<PluginResult> GetPluginDetail(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            // Get the plugin name slot
            string pluginName = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "plugin_name");

            IDictionary<string, PluginInformation> pluginInfo = GetPluginInfoFromSession(services);
            if (pluginInfo == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Dialog engine failure: did not pass PluginInformation to object store"
                };
            }

            if (!pluginInfo.ContainsKey(pluginName))
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Reflection: Could not find answer plugin registered to domain \"" + pluginName + "\""
                };
            }
            return new PluginResult(Result.Success)
            {
                ResponseHtml = this.GeneratePluginDetailHtml(
                        pluginName,
                        pluginInfo[pluginName],
                        queryWithContext.ClientContext.ClientId,
                        services,
                        queryWithContext.ClientContext.Locale),
                MultiTurnResult = new MultiTurnBehavior()
                {
                    Continues = true,
                    ConversationTimeoutSeconds = 3600,
                    IsImmediate = false
                }
            };
        }

        // Display a server-generated error message to the user
        public async Task<PluginResult> Error(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            string errorMessage = queryWithContext.Understanding.Utterance.OriginalText;

            ILGPattern lg = services.LanguageGenerator.GetPattern("Error", queryWithContext.ClientContext, services.Logger);

            MessageView responseHtml = new MessageView()
            {
                Content = (await lg.Render()).Text,
                Subscript = errorMessage,
                UseHtml5 = true,
                ClientContextData = queryWithContext.ClientContext.ExtraClientContext
            };

            return new PluginResult(Result.Success)
            {
                ResponseHtml = responseHtml.Render()
            };
        }

        public async Task<PluginResult> Greet(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            bool clientIsFullProfile = queryWithContext.ClientContext.GetCapabilities().HasFlag(ClientCapabilities.DisplayHtml5);
            LanguageCode locale = queryWithContext.ClientContext.Locale;

            // Get the installed plugin info
            IDictionary<string, PluginInformation> pluginInfo = GetPluginInfoFromSession(services);
            if (pluginInfo == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Dialog engine failure: did not pass PluginInformation to object store"
                };
            }

            // Display all information about installed plugins
            // Build sample queries from the reflected plugin info
            ISet<string> sampleQueries = GetSampleQueries(pluginInfo, 12, locale);

            // Create a link to the "list plugins" page when you click on a sample query
            string listPluginsLink = services.RegisterDialogActionUrl(new DialogAction()
            {
                Domain = LUDomain,
                Intent = "list_plugins"
            },
                queryWithContext.ClientContext.ClientId);

            // A client is requesting the start page.
            // This is a Razor view, so render it now
            StartPage html = new StartPage
            {
                Date = SVNVersionInfo.BuildDate,
                Revision = SVNVersionInfo.VersionString,
                UseHtml5 = clientIsFullProfile,
                SampleQueries = sampleQueries,
                ListPluginsLink = listPluginsLink,
                AuthLevel = queryWithContext.AuthenticationLevel
            };

            string responseText = "Hello, I am Durandal. What can I help you with?";

            PluginResult result = new PluginResult(Result.Success)
            {
                ResponseHtml = html.Render(),
                ResponseText = responseText
            };

            //if (queryWithContext.ClientContext.ExtraClientContext.ContainsKey(ClientContextField.ClientType) &&
            //    "TELEGRAM".Equals(queryWithContext.ClientContext.ExtraClientContext[ClientContextField.ClientType]))
            //{
            //    result.ResponseText = responseText;
            //}

            return result;
        }

        private PluginRenderingInfo GenerateRenderingInfo(
            PluginInformation plugin,
            string pluginDomain,
            IPluginServices services,
            LanguageCode locale,
            Dictionary<string, string> iconCache,
            string clientId)
        {
            PluginRenderingInfo returnVal = new PluginRenderingInfo();

            returnVal.IconUrl = GetIconForPlugin(plugin, pluginDomain, services, iconCache);

            if (PluginHasInfo(plugin))
            {
                // Try and get localized info for the current locale
                LocalizedInformation info;
                if (plugin.LocalizedInfo.TryGetValue(locale.ToBcp47Alpha2String(), out info))
                {
                    returnVal.Name = info.DisplayName;
                    returnVal.Subtitle = plugin.MajorVersion + "." + plugin.MinorVersion + " by " + plugin.Creator;
                    returnVal.ShortDescription = string.Format("{0} v{1}", info.DisplayName, plugin.MajorVersion + "." + plugin.MinorVersion);
                    returnVal.LongDescription = info.ShortDescription;
                    returnVal.InfoLink = string.Empty;
                    if (info.SampleQueries != null)
                    {
                        returnVal.SampleQueries = new List<PluginSampleQuery>();
                        foreach (string query in info.SampleQueries)
                        {
                            returnVal.SampleQueries.Add(new PluginSampleQuery()
                            {
                                Utterance = query,
                                Deeplink = "/query?q=" + WebUtility.UrlEncode(query) + "&client=" + WebUtility.UrlEncode(clientId)
                            });
                        }
                    }
                }
                else
                {
                    returnVal.Name = plugin.InternalName;
                    returnVal.Subtitle = plugin.MajorVersion + "." + plugin.MinorVersion + " by " + plugin.Creator;
                    returnVal.ShortDescription = string.Format("{0} v{1}", plugin.InternalName, plugin.MajorVersion + "." + plugin.MinorVersion);
                    returnVal.LongDescription = string.Empty;
                    returnVal.InfoLink = string.Empty;
                }
            }
            else
            {
                returnVal.Name = "Unknown Plugin (" + pluginDomain + ")";
                returnVal.Subtitle = string.Empty;
                returnVal.ShortDescription = "Unknown Plugin (" + pluginDomain + ")";
                returnVal.LongDescription = "This plugin is not reporting any description about itself.";
                returnVal.InfoLink = string.Empty;
                returnVal.SampleQueries = null;
            }

            return returnVal;
        }

        private string GeneratePluginDetailHtml(string pluginDomain, PluginInformation pluginInfo, string clientId, IPluginServices services, LanguageCode locale)
        {
            Dictionary<string, string> iconCache;
            if (services.SessionStore.ContainsKey("_icon_cache"))
            {
                iconCache = services.SessionStore.GetObject<Dictionary<string, string>>("_icon_cache");
            }
            else
            {
                iconCache = new Dictionary<string, string>();
            }

            PluginDetailView page = new PluginDetailView();
            page.Plugin = GenerateRenderingInfo(pluginInfo, pluginDomain, services, locale, iconCache, clientId);
            page.Backlink = services.RegisterDialogActionUrl(new DialogAction() { Domain = LUDomain, Intent = "list_plugins" }, clientId);

            services.SessionStore.Put("_icon_cache", iconCache);
            return page.Render();
        }

        private string GeneratePluginListHtml(IDictionary<string, PluginInformation> pluginInfo, string clientId, IPluginServices services, LanguageCode locale)
        {
            IList<PluginRenderingInfo> renderingInfo = new List<PluginRenderingInfo>();
            Dictionary<string, string> iconCache;
            if (services.SessionStore.ContainsKey("_icon_cache"))
            {
                iconCache = services.SessionStore.GetObject<Dictionary<string, string>>("_icon_cache");
            }
            else
            {
                iconCache = new Dictionary<string, string>();
            }

            foreach (var plugin in pluginInfo)
            {
                // Skip hidden plugins
                if (plugin.Value.Hidden)
                    continue;

                PluginRenderingInfo info = GenerateRenderingInfo(plugin.Value, plugin.Key, services, locale, iconCache, clientId);
                // Register a dialog action to link to the plugin detail page
                DialogAction pluginDetailAction = new DialogAction() { Domain = "reflection", Intent = "get_plugin_details" };
                pluginDetailAction.Slots.Add(new SlotValue()
                {
                    Name = "plugin_name",
                    Value = plugin.Key,
                    Format = SlotValueFormat.DialogActionParameter
                });
                info.InfoLink = services.RegisterDialogActionUrl(pluginDetailAction, clientId);
                renderingInfo.Add(info);
            }

            services.SessionStore.Put("_icon_cache", iconCache);

            PluginListView page = new PluginListView();
            page.Plugins = renderingInfo;

            return page.Render();
        }

        private static string GetDefaultPluginIconUrl()
        {
            return "/views/reflection/defaultPlugin.png";
        }

        private static string GetUnknownPluginIconUrl()
        {
            return "/views/reflection/unknownPlugin.png";
        }

        private static bool PluginHasInfo(PluginInformation info)
        {
            return info.MajorVersion > 0 || info.MinorVersion > 0;
        }
        
        private string GetPluginIconUrl(string domainName, ArraySegment<byte> pngImageData, IPluginServices services, Dictionary<string, string> iconCache)
        {
            if (!iconCache.ContainsKey(domainName))
            {
                // Add it if it's not
                iconCache.Add(domainName, services.CreateTemporaryWebResource(pngImageData, "image/png"));
            }

            return iconCache[domainName];
        }

        public ISet<string> GetSampleQueries(IDictionary<string, PluginInformation> pluginInfo, int count, LanguageCode locale)
        {
            HashSet<string> returnVal = new HashSet<string>();
            IRandom selector = new FastRandom();

            // Get all samples
            List<string> allSamples = new List<string>();
            foreach (PluginInformation info in pluginInfo.Values)
            {
                if (info == null || !info.LocalizedInfo.ContainsKey(locale.ToBcp47Alpha2String()))
                {
                    continue;
                }

                LocalizedInformation localInfo = info.LocalizedInfo[locale.ToBcp47Alpha2String()];
                if (localInfo.SampleQueries != null)
                {
                    allSamples.AddRange(localInfo.SampleQueries);
                }
            }

            // Select some
            if (allSamples.Count > 0)
            {
                for (int c = 0; c < count; c++)
                {
                    string toAdd = allSamples[selector.NextInt(allSamples.Count)];
                    if (!returnVal.Contains(toAdd))
                    {
                        returnVal.Add(toAdd);
                    }
                }
            }

            return returnVal;
        }

        public string GetIconForPlugin(PluginInformation info, string pluginDomain, IPluginServices services, Dictionary<string, string> iconCache)
        {
            if (info.IconPngData != null && info.IconPngData.Count > 0)
            {
                // Use the plugin-provided icon if one exists
                return GetPluginIconUrl(pluginDomain, info.IconPngData, services, iconCache);
            }
            else
            {
                // Otherwise use a default icon or a question mark
                return PluginHasInfo(info) ? GetDefaultPluginIconUrl() : GetUnknownPluginIconUrl();
            }
        }

        public async Task<PluginResult> DisambiguateStep1(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            if (!services.SessionStore.ContainsKey("triggerResults"))
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Trigger results not in object store"
                };
            }

            IDictionary<string, TriggerResult> triggerResults = JsonConvert.DeserializeObject<IDictionary<string, TriggerResult>>(services.SessionStore.GetString("triggerResults"));

            IDictionary<string, PluginInformation> pluginInfo = GetPluginInfoFromSession(services);
            if (pluginInfo == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "PluginInformation not in object store"
                };
            }

            Dictionary<string, string> iconCache;
            if (services.SessionStore.ContainsKey("_icon_cache"))
            {
                iconCache = services.SessionStore.GetObject<Dictionary<string, string>>("_icon_cache");
            }
            else
            {
                iconCache = new Dictionary<string, string>();
            }

            List<DisambiguationRenderItem> renderingInfo = new List<DisambiguationRenderItem>();
            foreach (var result in triggerResults)
            {
                string domain = result.Key.Substring(0, result.Key.IndexOf("/"));
                DialogAction selectionAction = new DialogAction()
                {
                    Domain = LUDomain,
                    Intent = "direct_selection",
                    Slots = new List<SlotValue>()
                };
                selectionAction.Slots.Add(new SlotValue("selection", result.Key, SlotValueFormat.DialogActionParameter));

                PluginInformation associatedPluginInfo = pluginInfo.ContainsKey(domain) ? pluginInfo[domain] : new PluginInformation();

                renderingInfo.Add(new DisambiguationRenderItem()
                {
                    ActionName = result.Value.ActionName,
                    Description = result.Value.ActionDescription,
                    PluginIconUrl = GetIconForPlugin(associatedPluginInfo, domain, services, iconCache),
                    SelectionUrl = services.RegisterDialogActionUrl(selectionAction, queryWithContext.ClientContext.ClientId)
                });
            }

            services.SessionStore.Put("_icon_cache", iconCache);

            string message = "Did you mean " + renderingInfo[0].ActionName + " or " + renderingInfo[1].ActionName + "?";

            DisambiguationView view = new DisambiguationView();
            view.Header = "Which one did you mean?";
            view.Results = renderingInfo;

            return new PluginResult(Result.Success)
            {
                ResponseText = message,
                ResponseSsml = message,
                ResponseHtml = view.Render(),
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public async Task<PluginResult> DisambiguationSelection(QueryWithContext queryWithContext, IPluginServices services)
        {
            if (!services.SessionStore.ContainsKey("triggerResults"))
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Trigger results not in session store"
                };
            }

            IDictionary<string, TriggerResult> triggerResults = JsonConvert.DeserializeObject<IDictionary<string, TriggerResult>>(services.SessionStore.GetString("triggerResults"));
            
            DialogAction callbackAction = new DialogAction()
            {
                Domain = LUDomain,
                Intent = "disambiguation_callback",
                Slots = new List<SlotValue>()
            };

            string boostedDomainIntent = "";

            // Did the user click on a specific choice (in which case the selection slot will be populated with domain/intent)
            string directSelection = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "selection");
            if (!string.IsNullOrEmpty(directSelection))
            {
                boostedDomainIntent = directSelection;
            }
            else
            {
                // Resolve the user's input to match one of the disambiguation prompts
                IList<NamedEntity<string>> disambiguationChoices = new List<NamedEntity<string>>();
                foreach (var triggerResult in triggerResults)
                {
                    disambiguationChoices.Add(new NamedEntity<string>(triggerResult.Key, triggerResult.Value.ActionKnownAs));
                }

                IList<Hypothesis<string>> hyps = await services.EntityResolver.ResolveEntity<string>(
                    new LexicalString(queryWithContext.Understanding.Utterance.OriginalText), // FIXME plumb through lexical pronunciation
                    disambiguationChoices,
                    queryWithContext.ClientContext.Locale,
                    services.Logger);
                if (hyps.Count > 0)
                {
                    boostedDomainIntent = hyps[0].Value;
                }
            }

            callbackAction.Slots.Add(new SlotValue("disambiguated_domain_intent", boostedDomainIntent, SlotValueFormat.StructuredData));

            return new PluginResult(Result.Success)
            {
                InvokedDialogAction = callbackAction,
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        /// <summary>
        /// GDPR deletion path
        /// </summary>
        /// <param name="queryWithContext"></param>
        /// <param name="services"></param>
        /// <returns></returns>
        public async Task<PluginResult> StartDeleteUserData(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            string responseMessage = "If you desire, I can clear all personal information that I have remembered about you. This action is not reversible. Should I clear all of your stored user information?";
            return new PluginResult(Result.Success)
            {
                ResponseText = responseMessage,
                ResponseSsml = responseMessage,
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public async Task<PluginResult> ConfirmDeleteUserData(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            string responseMessage = "Your personal information is now cleared.";
            
            return new PluginResult(Result.Success)
            {
                ResponseText = responseMessage,
                ResponseSsml = responseMessage,
                MultiTurnResult = MultiTurnBehavior.None,
                ClientAction = JsonConvert.SerializeObject(new ClearPrivateDataAction())
            };
        }

        public async Task<PluginResult> CancelDeleteUserData(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            string responseMessage = "Okay. I did not delete anything.";
            return new PluginResult(Result.Success)
            {
                ResponseText = responseMessage,
                ResponseSsml = responseMessage,
                MultiTurnResult = MultiTurnBehavior.None
            };
        }

        public async Task<PluginResult> LoginTemporary1(QueryWithContext queryWithContext, IPluginServices services)
        {
            // Ensure that the client can handle the portable login action
            if (!queryWithContext.ClientContext.SupportedClientActions.Contains(MSAPortableLoginAction.ActionName))
            {
                return new PluginResult(Result.Skip);
            }

            string message = "I can help you log in using a Microsoft account. All I need from you is your phone number.";
            await DurandalTaskExtensions.NoOpTask;
            return new PluginResult(Result.Success)
            {
                ResponseText = message,
                ResponseSsml = message,
                MultiTurnResult = MultiTurnBehavior.ContinueBasic
            };
        }

        public async Task<PluginResult> LoginTemporary2(QueryWithContext queryWithContext, IPluginServices services)
        {
            // Generate the login URL
            string state = Guid.NewGuid().ToString("N");
            HttpRequest loginRequestBuilder = HttpRequest.CreateOutgoing("/common/oauth2/v2.0/authorize");
            loginRequestBuilder.GetParameters["client_id"] = "0359c040-e829-4472-843b-122ec590e75d";
            loginRequestBuilder.GetParameters["response_type"] = "code";
            loginRequestBuilder.GetParameters["redirect_uri"] = "https://durandal-ai.net/auth/login/oauth/msa-portable";
            loginRequestBuilder.GetParameters["response_mode"] = "query";
            loginRequestBuilder.GetParameters["scope"] = "User.Read";
            loginRequestBuilder.GetParameters["prompt"] = "login";
            loginRequestBuilder.GetParameters["state"] = state;
            Uri loginUrl = new Uri("https://login.microsoftonline.com" + loginRequestBuilder.BuildUri());
            Uri shortenedUri = await _urlShortener.ShortenUrl(loginUrl, services.Logger, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            if (shortenedUri == null)
            {
                return new PluginResult(Result.Failure)
                {
                    ErrorMessage = "Error came back from URL shortening service"
                };
            }

            // Generate a PIN (for user peace of mind, not anything actually super secure)
            string pin = _rand.NextInt(0, 1000).ToString().PadLeft(3, '0');

            // Actually send the message
            string sourceNumber = "+12138057490";
            string targetNumber = "+14254636482";
            string textMessage = "Login to Durandal at: " + shortenedUri.AbsoluteUri + ". The PIN number is " + pin;
            await _phoneLoginInterface.SendSMS(sourceNumber, targetNumber, textMessage, services.Logger, CancellationToken.None, DefaultRealTimeProvider.Singleton);

            string pinWithSpaces = pin[0] + " " + pin[1] + " " + pin[2];
            string responseMessage = "I sent a message to your phone where you can complete the login process. Just so you know it's from me, I included the PIN number of " + pinWithSpaces + ".";

            InputMethod callbackInteractionMethod = queryWithContext.Source == InputMethod.Spoken ? InputMethod.TactileWithAudio : InputMethod.Tactile;
            DialogAction callbackAction = new DialogAction()
            {
                Domain = LUDomain,
                Intent = "login_success",
                InteractionMethod = callbackInteractionMethod,
                Slots = new List<SlotValue>()
            };

            MSAPortableLoginAction loginAction = new MSAPortableLoginAction()
            {
                ExternalToken = state,
                SuccessActionId = services.RegisterDialogAction(callbackAction),
                IsSpeechEnabled = callbackInteractionMethod == InputMethod.TactileWithAudio
            };
            
            return new PluginResult(Result.Success)
            {
                ResponseText = responseMessage,
                ResponseSsml = responseMessage,
                ClientAction = JsonConvert.SerializeObject(loginAction)
            };
        }

        public async Task<PluginResult> LoginTemporary3(QueryWithContext queryWithContext, IPluginServices services)
        {
            await DurandalTaskExtensions.NoOpTask;
            string userName = string.Empty;

            // Look in global user profile for the user's name
            if (services.GlobalUserProfile != null &&
                !services.GlobalUserProfile.TryGetString(ClientContextField.UserGivenName, out userName))
            {
                userName = string.Empty;
            }

            string responseMessage;
            if (string.IsNullOrEmpty(userName))
            {
                responseMessage = "You are now logged in.";
            }
            else
            {
                responseMessage = "All right " + userName + ", you are now logged in.";
            }

            return new PluginResult(Result.Success)
            {
                ResponseText = responseMessage,
                ResponseSsml = responseMessage
            };
        }

        protected override PluginInformation GetInformation(IFileSystem pluginDataManager, VirtualPath pluginDataDirectory)
        {
            PluginInformation returnVal = new PluginInformation()
            {
                InternalName = "Reflection",
                Creator = "Logan Stromberg",
                MajorVersion = 1,
                MinorVersion = 0,
                Hidden = true
            };

            returnVal.LocalizedInfo.Add("en-US", new LocalizedInformation()
            {
                DisplayName = "Durandal Reflection",
                ShortDescription = "Internal plugin for viewing and configuring details about the Durandal dialog engine",
                SampleQueries = new List<string>()
            });

            returnVal.LocalizedInfo["en-US"].SampleQueries.Add("What can you help me with?");
            returnVal.LocalizedInfo["en-US"].SampleQueries.Add("What can you do?");

            return returnVal;
        }
    }
}
