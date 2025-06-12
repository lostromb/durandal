using Durandal.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Durandal.Answers.SmartThingsAnswer
{
    using Common.Client.Actions;
    using Durandal.Answers.SmartThingsAnswer.Devices;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.File;
    using Durandal.Common.IO;
    using Durandal.Common.Logger;
    using Durandal.Common.Security.OAuth;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using Durandal.Common.Utils;
    using Durandal.CommonViews;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    public class SmartThingsAnswer : DurandalPlugin
    {
        private IDictionary<string, string> _clientIdDefaultDeviceMapping = new Dictionary<string, string>();

        private OAuthConfig _oauthConfig;

        public SmartThingsAnswer() : base("smartthings") { }

        public override async Task OnLoad(IPluginServices services)
        {
            // Read the oauth config + secrets from file
            VirtualPath oauthConfigFile = services.PluginDataDirectory + "\\oauth.json";
            if (await services.FileSystem.ExistsAsync(oauthConfigFile))
            {
                using (Stream readStream = await services.FileSystem.OpenStreamAsync(oauthConfigFile, FileOpenMode.Open, FileAccessMode.Read))
                {
                    JsonReader reader = new JsonTextReader(new StreamReader(readStream));
                    JsonSerializer serializer = new JsonSerializer();
                    _oauthConfig = serializer.Deserialize<OAuthConfig>(reader);
                }
            }

            VirtualPath clientDefaults = services.PluginDataDirectory + "\\default_devices.tsv";
            if (await services.FileSystem.ExistsAsync(clientDefaults))
            {
                foreach (string line in await services.FileSystem.ReadLinesAsync(clientDefaults))
                {
                    string[] parts = line.Split('\t');
                    if (parts.Length != 2)
                    {
                        continue;
                    }

                    _clientIdDefaultDeviceMapping[parts[0]] = parts[1];
                }

                services.Logger.Log("Loaded " + _clientIdDefaultDeviceMapping.Count + " default client device mappings");
            }
        }

        public override Task OnUnload(IPluginServices services)
        {
            _clientIdDefaultDeviceMapping.Clear();
            return DurandalTaskExtensions.NoOpTask;
        }

        protected override IConversationTree BuildConversationTree(IConversationTree tree, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
        {
            tree.AddStartState("change_state", ChangeState);
            tree.AddStartState("query_state", QueryState);

            return tree;
        }

        public async Task<PluginResult> UnauthorizedResponse(QueryWithContext queryWithContext, IPluginServices services)
        {
            ILGPattern pattern = services.LanguageGenerator.GetPattern("Unauthorized", queryWithContext.ClientContext, services.Logger);
            return await pattern.ApplyToDialogResult(new PluginResult(Result.Success)
            {
                ResponseHtml = new MessageView()
                {
                    Content = (await pattern.Render()).Text,
                    ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                }.Render()
            });
        }

        public async Task<PluginResult> CreateAuth(QueryWithContext queryWithContext, IPluginServices services)
        {
            if (queryWithContext.ClientContext.SupportedClientActions.Contains(OAuthLoginAction.ActionName))
            {
                Uri authUri = await services.CreateOAuthUri(_oauthConfig, queryWithContext.ClientContext.UserId, CancellationToken.None, DefaultRealTimeProvider.Singleton);

                string responseMessage = "Before doing that, you will need to log in to SmartThings";
                PluginResult returnVal = new PluginResult(Result.Success)
                {
                    ResponseHtml = new MessageView()
                    {
                        Content = responseMessage,
                        ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                    }.Render(),
                    ResponseText = responseMessage,
                    ResponseSsml = responseMessage
                };

                returnVal.ClientAction = JsonConvert.SerializeObject(new OAuthLoginAction()
                {
                    ServiceName = "SmartThings",
                    LoginUrl = authUri.AbsoluteUri
                });

                return returnVal;
            }
            else
            {
                string responseMessage = "That action requires you to login to SmartThings. Unfortunately, you cannot do that from this device.";
                return new PluginResult(Result.Success)
                {
                    ResponseHtml = new MessageView()
                    {
                        Content = responseMessage,
                        ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                    }.Render(),
                    ResponseText = responseMessage,
                    ResponseSsml = responseMessage
                };
            }
        }

        public async Task<PluginResult> ChangeState(QueryWithContext queryWithContext, IPluginServices services)
        {
            //if (queryWithContext.AuthenticationLevel != ClientAuthenticationLevel.Authorized)
            //{
            //    return await UnauthorizedResponse(queryWithContext, services);
            //}

            // Try and get an auth token first. If not found, prompt for a login
            OAuthToken userAuthToken = await services.TryGetOAuthToken(_oauthConfig, queryWithContext.ClientContext.UserId, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            if (userAuthToken == null)
            {
                return await CreateAuth(queryWithContext, services);
            }

            // Build context
            SmartThingsContext context = new SmartThingsContext(services.Logger, userAuthToken);
            await context.Initialize(services.LocalUserProfile, _oauthConfig.ClientId);

            LexicalString device_name = DialogHelpers.TryGetLexicalSlotValue(queryWithContext.Understanding, "device_name");
            string device_type = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "device_type");
            string state = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "state");
            string location = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "location");
            string scope = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "scope");
            SlotValue valueSlot = DialogHelpers.TryGetSlot(queryWithContext.Understanding, "value");

            // From the pile of slots that we got, decide what to do
            if ((!string.IsNullOrEmpty(state) || valueSlot != null) && device_name != null && !string.IsNullOrEmpty(device_name.WrittenForm))
            {
                // Used mentioned an action on a specific device. Do it
                // Match the name to the device and then change its state
                SmartDevice device = await context.GetDeviceByName(device_name, services);
                if (device != null)
                {
                    device_name = new LexicalString(device.Name);
                    if (!string.IsNullOrEmpty(state) && (device.Capabilities.HasFlag(DeviceCapability.Switch)))
                    {
                        List<TriggerKeyword> spotterPhrases = new List<TriggerKeyword>();
                        spotterPhrases.Add(new TriggerKeyword()
                        {
                            TriggerPhrase = "thank you",
                            AllowBargeIn = false,
                            ExpireTimeSeconds = 15
                        });

                        if (state.Equals("ON"))
                        {
                            await device.On(context);
                            spotterPhrases.Add(new TriggerKeyword()
                            {
                                TriggerPhrase = "turn it off",
                                AllowBargeIn = false,
                                ExpireTimeSeconds = 15
                            });
                            spotterPhrases.Add(new TriggerKeyword()
                            {
                                TriggerPhrase = "lights off",
                                AllowBargeIn = false,
                                ExpireTimeSeconds = 15
                            });
                        }
                        else
                        {
                            await device.Off(context);
                            spotterPhrases.Add(new TriggerKeyword()
                            {
                                TriggerPhrase = "turn it on",
                                AllowBargeIn = false,
                                ExpireTimeSeconds = 15
                            });
                            spotterPhrases.Add(new TriggerKeyword()
                            {
                                TriggerPhrase = "lights on",
                                AllowBargeIn = false,
                                ExpireTimeSeconds = 15
                            });
                        }
                        
                        ILGPattern pattern = services.LanguageGenerator.GetPattern("StateChangedOnOff", queryWithContext.ClientContext, services.Logger)
                            .Sub("device", device_name)
                            .Sub("state", state);

                        return await pattern.ApplyToDialogResult(new PluginResult(Result.Success)
                            {
                                TriggerKeywords = spotterPhrases,
                                ResponseHtml = new MessageView()
                                {
                                    Content = (await pattern.Render()).Text,
                                    ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                                }.Render()
                            });
                    }
                    else if (valueSlot != null && valueSlot.GetNumber().HasValue && device.Capabilities.HasFlag(DeviceCapability.SwitchLevel))
                    {
                        decimal? dimmerValue = valueSlot.GetNumber();
                        await device.SetLevel((int)Math.Floor(dimmerValue.Value), context);

                        List<TriggerKeyword> spotterPhrases = new List<TriggerKeyword>();
                        spotterPhrases.Add(new TriggerKeyword()
                        {
                            TriggerPhrase = "thank you",
                            AllowBargeIn = false,
                            ExpireTimeSeconds = 15
                        });

                        ILGPattern pattern = services.LanguageGenerator.GetPattern("StateChangedValue", queryWithContext.ClientContext, services.Logger)
                            .Sub("device", device_name.WrittenForm.ToLowerInvariant()) //hackhack
                            .Sub("value", dimmerValue.Value);

                       return await pattern.ApplyToDialogResult(new PluginResult(Result.Success)
                            {
                                TriggerKeywords = spotterPhrases,
                                ResponseHtml = new MessageView()
                                {
                                    Content = (await pattern.Render()).Text,
                                    ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                                }.Render()
                            });
                    }
                    else
                    {
                        // Something else?
                        return new PluginResult(Result.Failure)
                        {
                            ErrorMessage = "The requested state change is invalid for the given device."
                        };
                    }
                }
                else
                {
                    // No device was found that matched the user query
                    services.Logger.Log("Could not find a device named " + device_name, LogLevel.Wrn);
                    return new PluginResult(Result.Skip);
                }
            }
            else if ((device_name == null || string.IsNullOrEmpty(device_name.WrittenForm)) && !string.IsNullOrEmpty(device_type) && !string.IsNullOrEmpty(state))
            {
                // No specific device mentioned, but trigger the default device for the client if available
                if (_clientIdDefaultDeviceMapping.ContainsKey(queryWithContext.ClientContext.ClientId))
                {
                    SmartDevice device = context.GetDeviceById(_clientIdDefaultDeviceMapping[queryWithContext.ClientContext.ClientId]);
                    device_name = new LexicalString(device.Name);

                    if (state.Equals("ON"))
                        await device.On(context);
                    else
                        await device.Off(context);

                    List<TriggerKeyword> spotterPhrases = new List<TriggerKeyword>();
                    spotterPhrases.Add(new TriggerKeyword()
                    {
                        TriggerPhrase = "thank you",
                        AllowBargeIn = false,
                        ExpireTimeSeconds = 15
                    });

                    // Render the state string in the user's language
                    string nlState = await services.LanguageGenerator.GetText("State-" + state, queryWithContext.ClientContext, services.Logger);

                    ILGPattern pattern = services.LanguageGenerator.GetPattern("StateChangedOnOff", queryWithContext.ClientContext, services.Logger)
                        .Sub("device", device_name.WrittenForm.ToLowerInvariant()) // hackish ToLower call until I can get LG to apply rules automatically
                        .Sub("state", nlState);
                    return await pattern.ApplyToDialogResult(new PluginResult(Result.Success)
                        {
                            TriggerKeywords = spotterPhrases,
                            ResponseHtml = new MessageView()
                            {
                                Content = (await pattern.Render()).Text,
                                ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                            }.Render()
                        });
                }
                else
                {
                    services.Logger.Log("User didn't mention a device and no default is set, so skipping");
                    return new PluginResult(Result.Skip);
                }
            }
            else
            {
                // No state, so nothing to do
                services.Logger.Log("User didn't mention a state change, so I will skip this intent");
                return new PluginResult(Result.Skip);
            }
        }

        public async Task<PluginResult> QueryState(QueryWithContext queryWithContext, IPluginServices services)
        {
            string device = DialogHelpers.TryGetSlotValue(queryWithContext.Understanding, "device");
            if (string.IsNullOrEmpty(device))
            {
                device = "thing";
            }

            ILGPattern pattern = services.LanguageGenerator.GetPattern("QueryState", queryWithContext.ClientContext, services.Logger)
                .Sub("device", device);

            return await pattern.ApplyToDialogResult(new PluginResult(Result.Success)
            {
                ResponseHtml = new MessageView()
                {
                    Content = (await pattern.Render()).Text,
                    ClientContextData = queryWithContext.ClientContext.ExtraClientContext
                }.Render()
            });
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
                InternalName = "SmartThings",
                Creator = "Logan Stromberg",
                MajorVersion = 1,
                MinorVersion = 0,
                IconPngData = new ArraySegment<byte>(pngStream.ToArray())
            };

            returnVal.LocalizedInfo.Add("en-US", new LocalizedInformation()
            {
                DisplayName = "SmartThings",
                ShortDescription = "Your home, smarter",
                SampleQueries = new List<string>()
            });

            returnVal.LocalizedInfo["en-US"].SampleQueries.Add("Turn the porch light off");
            returnVal.LocalizedInfo["en-US"].SampleQueries.Add("Is the kitchen light on?");
            returnVal.LocalizedInfo["en-US"].SampleQueries.Add("Turn on the living room fan");

            return returnVal;
        }
    }
}
