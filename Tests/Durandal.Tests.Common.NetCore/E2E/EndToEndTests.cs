namespace Durandal.Tests.E2E
{
    using Durandal;
    using Durandal.API;
    using Durandal.Common.Audio;
    using Durandal.Common.Audio.Codecs;
    using Durandal.Common.Client;
    using Durandal.Common.Client.Actions;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.Events;
    using Durandal.Common.File;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.Logger;
    using Durandal.Common.Net;
    using Durandal.Common.Net.Http;
    using Durandal.Common.Ontology;
    using Durandal.Common.Tasks;
    using Durandal.Common.Test;
    using Durandal.Common.Time;
    using Durandal.Common.Utils;
    using Durandal.Extensions.BondProtocol;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Durandal.Tests.Common.Audio;
    using Durandal.Tests.Common.Client;
    using Durandal.Tests.E2E;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.Audio.Codecs.Opus;

    [TestClass]
    public class EndToEndTests
    {
        private ILogger _logger;
        private EventOnlyLogger _eventLogger;
        private InqueTestDriver _testDriver;
        private LockStepRealTimeProvider _realTime;

        [TestInitialize]
        public void InitializeTest()
        {
            _eventLogger = new EventOnlyLogger("EndToEndTest", LogLevel.All);
            _logger = new AggregateLogger("EndToEndTest", null,
                new ConsoleLogger("EndToEndTest", LogLevel.All),
                new DebugLogger("EndToEndTest", LogLevel.All),
                _eventLogger);

            FakeLUModel model = new FakeLUModel();
            model.Domain = "test";
            model.AddRegex("vader", "darth vader");
            model.AddRegex("check_auth", "authentication");
            model.AddRegex("client_action", "execute an action");
            model.AddRegex("client_action", "execute (?<count>several) actions");
            model.AddRegex("spa_start", "start SPA");
            model.AddRegex("client_entities", "client entities");
            model.AddRegex("custom_audio", "custom audio");
            model.AddRegex("client_resolution", "do client resolution");

            _realTime = new LockStepRealTimeProvider(_logger.Clone("LockStepTime"));

            IList<FakeLUModel> models = new List<FakeLUModel>();
            models.Add(model);

            InqueTestParameters testConfig = new InqueTestParameters()
            {
                Logger = _logger.Clone("DialogDriver"),
                Plugins = new List<DurandalPlugin>() { new TestDomainPlugin() },
                TimeProvider = _realTime,
                FakeLUModels = models,
                DialogTransportProtocol = new DialogBondTransportProtocol(),
                LUTransportProtocol = new LUBondTransportProtocol(),
                PluginProviderFactory = InqueTestDriver.BuildDefaultPluginProvider
            };

            _testDriver = new InqueTestDriver(testConfig);
            _testDriver.Initialize().Await();
        }

        [TestCleanup]
        public void CleanupTest()
        {
            _testDriver.Dispose();
        }

        private class TestDomainPlugin : DurandalPlugin
        {
            public TestDomainPlugin() : base("test")
            {
            }

            protected override IConversationTree BuildConversationTree(IConversationTree tree, IFileSystem pluginFileSystem, VirtualPath pluginDataDirectory)
            {
                tree.AddStartState("vader", Vader);
                tree.AddStartState("check_auth", CheckAuth);
                tree.AddStartState("client_action", ClientAction);
                tree.AddStartState("client_entities", ClientEntities);
                tree.AddStartState("custom_audio", CustomAudio);

                IConversationNode clientResolution1 = tree.CreateNode(ClientResolution1, "ClientResolution1");
                IConversationNode clientResolution2 = tree.CreateNode(ClientResolution2, "ClientResolution2");
                clientResolution1.CreateNormalEdge("client_resolution_callback", clientResolution2);
                tree.AddStartState("client_resolution", clientResolution1);

                IConversationNode spaStart = tree.CreateNode(SpaStart, "SpaStart");
                IConversationNode spaContinue = tree.CreateNode(SpaContinue, "SpaContinue");
                spaStart.CreateNormalEdge("spa_continue", spaContinue);
                tree.AddStartState("spa_start", spaStart);
                return tree;
            }

            public async Task<PluginResult> Vader(QueryWithContext queryWithContext, IPluginServices services)
            {
                await DurandalTaskExtensions.NoOpTask;

                return new PluginResult(Result.Success)
                {
                    ResponseText = "Success",
                    ResponseSsml = "Success",
                    ResponseHtml = "<html>"
                };
            }

            public async Task<PluginResult> CustomAudio(QueryWithContext queryWithContext, IPluginServices services)
            {
                await DurandalTaskExtensions.NoOpTask;

                float[] wavData = new float[16000];
                AudioTestHelpers.GenerateSineWave(wavData, 16000, 440, 16000, 0, 0, 1, 0.8f);
                AudioSample sample = new AudioSample(wavData, AudioSampleFormat.Mono(16000));
                AudioData encoded = await AudioHelpers.EncodeAudioSampleUsingCodec(sample, new OpusRawCodecFactory(NullLogger.Singleton), OpusRawCodecFactory.CODEC_NAME, services.Logger);

                return new PluginResult(Result.Success)
                {
                    ResponseAudio = new AudioResponse(encoded, AudioOrdering.AfterSpeech)
                };
            }

            public async Task<PluginResult> CheckAuth(QueryWithContext queryWithContext, IPluginServices services)
            {
                await DurandalTaskExtensions.NoOpTask;

                if (queryWithContext.AuthenticationLevel.HasFlag(ClientAuthenticationLevel.UserAuthorized))
                {
                    return new PluginResult(Result.Success);
                }

                return new PluginResult(Result.Failure);
            }

            public async Task<PluginResult> ClientAction(QueryWithContext queryWithContext, IPluginServices services)
            {
                await DurandalTaskExtensions.NoOpTask;

                if (queryWithContext.ClientContext.SupportedClientActions == null ||
                    !queryWithContext.ClientContext.SupportedClientActions.Contains("TestAction"))
                {
                    return new PluginResult(Result.Failure);
                }
                if (DialogHelpers.TryGetSlot(queryWithContext.Understanding, "count") == null)
                {
                    // return a single action
                    return new PluginResult(Result.Success)
                    {
                        ClientAction = "{ \"Name\": \"TestAction\" }"
                    };
                }
                else
                {
                    // return an array
                    return new PluginResult(Result.Success)
                    {
                        ClientAction = "[{ \"Name\": \"TestAction\" }, { \"Name\": \"TestAction\" }]"
                    };
                }
            }

            public async Task<PluginResult> ClientEntities(QueryWithContext queryWithContext, IPluginServices services)
            {
                await DurandalTaskExtensions.NoOpTask;

                if (services.ContextualEntities == null || services.ContextualEntities.Count == 0)
                {
                    return new PluginResult(Result.Failure);
                }

                foreach (var entity in services.ContextualEntities)
                {
                    services.Logger.Log(entity.Entity.ToDebugJson());
                }

                return new PluginResult(Result.Success)
                {
                    ResponseText = "You passed " + services.ContextualEntities.Count + " entities!",
                    ResponseSsml = "You passed " + services.ContextualEntities.Count + " entities!"
                };
            }

            public async Task<PluginResult> SpaStart(QueryWithContext queryWithContext, IPluginServices services)
            {
                await DurandalTaskExtensions.NoOpTask;

                string actionUrl = services.RegisterDialogActionUrl(new DialogAction()
                {
                    Domain = LUDomain,
                    Intent = "spa_continue",
                    InteractionMethod = InputMethod.Tactile
                }, queryWithContext.ClientContext.ClientId);

                return new PluginResult(Result.Success)
                {
                    ResponseText = actionUrl,
                    ResponseHtml = "<html></html>",
                    MultiTurnResult = MultiTurnBehavior.ContinuePassively
                };
            }

            public async Task<PluginResult> SpaContinue(QueryWithContext queryWithContext, IPluginServices services)
            {
                await DurandalTaskExtensions.NoOpTask;

                return new PluginResult(Result.Success)
                {
                    ResponseData = queryWithContext.RequestData,
                    ResponseText = "turn" + queryWithContext.TurnNum,
                    ResponseHtml = "<html></html>",
                    MultiTurnResult = MultiTurnBehavior.ContinuePassively
                };
            }

            public async Task<PluginResult> ClientResolution1(QueryWithContext queryWithContext, IPluginServices services)
            {
                if (!queryWithContext.ClientContext.SupportedClientActions.Contains(CustomClientResolutionAction.ActionName))
                {
                    return new PluginResult(Result.Failure)
                    {
                        ErrorMessage = "Client does not support client resolution action"
                    };
                }

                await DurandalTaskExtensions.NoOpTask;

                DialogAction callbackAction = new DialogAction()
                {
                    Domain = LUDomain,
                    Intent = "client_resolution_callback",
                    InteractionMethod = queryWithContext.Source,
                };

                CustomClientResolutionAction responseAction = new CustomClientResolutionAction();
                responseAction.CallbackActionId = services.RegisterDialogAction(callbackAction);
                responseAction.InteractionMethod = queryWithContext.Source;

                return new PluginResult(Result.Success)
                {
                    MultiTurnResult = MultiTurnBehavior.ContinuePassively,
                    ClientAction = JsonConvert.SerializeObject(responseAction)
                };
            }

            public async Task<PluginResult> ClientResolution2(QueryWithContext queryWithContext, IPluginServices services)
            {
                await DurandalTaskExtensions.NoOpTask;

                if (!queryWithContext.RequestData.ContainsKey("ClientResolutionData"))
                {
                    return new PluginResult(Result.Failure)
                    {
                        ErrorMessage = "No client resolution data received"
                    };
                }

                if (queryWithContext.Source == InputMethod.Programmatic)
                {
                    return new PluginResult(Result.Failure)
                    {
                        ErrorMessage = "The input method should be something other than Programmatic"
                    };
                }

                string resolutionData = queryWithContext.RequestData["ClientResolutionData"];

                return new PluginResult(Result.Success)
                {
                    ResponseText = "Client resolution data was " + resolutionData,
                    ResponseSsml = "Client resolution data was " + resolutionData
                };
            }
        }

        [TestMethod]
        public async Task TestEndToEndTextQuery()
        {
            using (ClientCoreTestWrapper client = new ClientCoreTestWrapper(_logger, false, _realTime, _testDriver.PublicKeyStorage))
            {
                IDialogClient dialog = _testDriver.Client;
                await client.Initialize(dialog);
                _testDriver.SetClientAsTrusted(client.ClientConfig.ClientId);

                for (int c = 0; c < 10; c++)
                {
                    client.ResetEvents();
                    Assert.IsTrue(await client.Core.TryMakeTextRequest("my name is darth vader", realTime: _realTime));
                    _realTime.Step(TimeSpan.FromMilliseconds(100));

                    // Ensure that the core emitted the proper events
                    Assert.IsTrue((await client.SuccessEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero)).Success);
                    RetrieveResult<CapturedEvent<TextEventArgs>> textRr = await client.DisplayTextEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                    Assert.IsTrue(textRr.Success);
                    Assert.AreEqual("Success", textRr.Result.Args.Text);
                }
            }
        }

        [TestMethod]
        public async Task TestEndToEndAudioQuery()
        {
            using (ClientCoreTestWrapper client = new ClientCoreTestWrapper(_logger, true, _realTime, _testDriver.PublicKeyStorage))
            {
                IDialogClient dialog = _testDriver.Client;
                await client.Initialize(dialog);
                _testDriver.SetClientAsTrusted(client.ClientConfig.ClientId);
                client.SpeechReco.SetRecoResult("en-US", "my name is darth vader");

                client.ResetEvents();
                Assert.IsTrue(await client.Core.TryMakeAudioRequest(realTime: _realTime));
                AudioSample mockUserSpeech = DialogTestHelpers.GenerateUtterance(client.Microphone.OutputFormat, 2000);
                client.Microphone.AddInput(mockUserSpeech);

                // Advance enough time to pipe the speech input. The client core uses a static utterance recorder with a 2 second length
                _realTime.Step(TimeSpan.FromMilliseconds(2000));
                _realTime.Step(TimeSpan.FromMilliseconds(100), 20);

                // Ensure that the core emitted the proper events
                Assert.IsTrue((await client.SuccessEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero)).Success);
                Assert.IsTrue((await client.PlayAudioEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero)).Success);
            }
        }

        [TestMethod]
        public async Task TestEndToEndAudioQueryHttp()
        {
            using (ClientCoreTestWrapper client = new ClientCoreTestWrapper(_logger, true, _realTime, _testDriver.PublicKeyStorage))
            {
                IDialogClient dialog = _testDriver.Client;
                await client.Initialize(dialog);
                _testDriver.SetClientAsTrusted(client.ClientConfig.ClientId);
                client.SpeechReco.SetRecoResult("en-US", "my name is darth vader");

                client.ResetEvents();
                Assert.IsTrue(await client.Core.TryMakeAudioRequest(realTime: _realTime));
                AudioSample mockUserSpeech = DialogTestHelpers.GenerateUtterance(client.Microphone.OutputFormat, 3000);
                client.Microphone.AddInput(mockUserSpeech);

                // Advance enough time to pipe the speech input. The client core uses a static utterance recorder with a 2 second length
                _realTime.Step(TimeSpan.FromMilliseconds(2000));
                _realTime.Step(TimeSpan.FromMilliseconds(100), 20);

                // Ensure that the core emitted the proper events
                Assert.IsTrue((await client.SuccessEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero)).Success);
                Assert.IsTrue((await client.PlayAudioEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero)).Success);
            }
        }

        [TestMethod]
        public async Task TestEndToEndClientActions()
        {
            JsonClientActionDispatcher actionDispatcher = new JsonClientActionDispatcher();
            FakeClientActionHandler actionHandler = new FakeClientActionHandler("TestAction");
            actionDispatcher.AddHandler(actionHandler);

            using (ClientCoreTestWrapper client = new ClientCoreTestWrapper(_logger, false, _realTime, _testDriver.PublicKeyStorage))
            {
                client.ActionDispatcher = actionDispatcher;
                IDialogClient dialog = _testDriver.Client;
                await client.Initialize(dialog);

                Assert.IsTrue(await client.Core.TryMakeTextRequest("execute an action", realTime: _realTime));
                _realTime.Step(TimeSpan.FromMilliseconds(100));

                // Ensure that the action handler triggered, which means the action was parsed properly
                Assert.AreEqual(1, actionHandler.TriggerCount);

                Assert.IsTrue(await client.Core.TryMakeTextRequest("execute several actions", realTime: _realTime));
                _realTime.Step(TimeSpan.FromMilliseconds(100));

                Assert.AreEqual(3, actionHandler.TriggerCount);
            }
        }

        private class CustomClientResolutionActionHandler : IJsonClientActionHandler
        {
            public ISet<string> GetSupportedClientActions()
            {
                return new HashSet<string>() { CustomClientResolutionAction.ActionName };
            }

            public async Task HandleAction(string actionName, JObject action, ILogger queryLogger, ClientCore source, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                // Parse incoming action
                CustomClientResolutionAction parsedAction = action.ToObject<CustomClientResolutionAction>();
                queryLogger.Log("Parsed client resolution action, action ID is " + parsedAction.CallbackActionId);
                Dictionary<string, string> requestData = new Dictionary<string, string>()
                {
                    { "ClientResolutionData", "Awesome" }
                };

                await realTime.WaitAsync(TimeSpan.FromMilliseconds(50), cancelToken);
                queryLogger.Log("Waited 50ms, beginning callback...");
                bool requestSuccess = await source.TryMakeDialogActionRequest(
                    parsedAction.CallbackActionId,
                    parsedAction.InteractionMethod,
                    context: null,
                    flags: QueryFlags.None,
                    inputEntities: null,
                    entityContext: null,
                    realTime: realTime,
                    requestData: requestData);

                if (requestSuccess)
                {
                    queryLogger.Log("Invoking callback succeeded");
                }
                else
                {
                    queryLogger.Log("Invoking callback FAILED", LogLevel.Err);
                }
            }
        }

        private class CustomClientResolutionAction : IJsonClientAction
        {
            public static string ActionName = "CustomClientResolutionAction";

            public string Name => ActionName;

            public string CallbackActionId { get; set; }

            [JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
            public InputMethod InteractionMethod { get; set; }
        }

        [TestMethod]
        public async Task TestEndToEndClientResolution()
        {
            JsonClientActionDispatcher actionDispatcher = new JsonClientActionDispatcher();
            CustomClientResolutionActionHandler actionHandler = new CustomClientResolutionActionHandler();
            actionDispatcher.AddHandler(actionHandler);

            using (ClientCoreTestWrapper client = new ClientCoreTestWrapper(_logger, false, _realTime, _testDriver.PublicKeyStorage))
            {
                client.ActionDispatcher = actionDispatcher;
                IDialogClient dialog = _testDriver.Client;
                await client.Initialize(dialog);

                Assert.IsTrue(await client.Core.TryMakeTextRequest("do client resolution", realTime: _realTime));
                _realTime.Step(TimeSpan.FromMilliseconds(100), 10);

                RetrieveResult<CapturedEvent<TextEventArgs>> responseTextEvent = await client.DisplayTextEvent.WaitForEvent(
                    CancellationToken.None,
                    DefaultRealTimeProvider.Singleton,
                    TimeSpan.Zero);

                Assert.IsTrue(responseTextEvent.Success);
                Assert.IsNotNull(responseTextEvent.Result);
                Assert.IsNotNull(responseTextEvent.Result.Args);
                Assert.AreEqual("Client resolution data was Awesome", responseTextEvent.Result.Args.Text);
            }
        }

        /// <summary>
        /// Tests that when the test driver marks the user's public key as trusted, then skills will treat the user as authenticated.
        /// This test has kind of been made obsolete with the recent changes to public key infrastructure and client-side authentication providers
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestAuthHello()
        {
            using (ClientCoreTestWrapper client = new ClientCoreTestWrapper(_logger, false, _realTime, _testDriver.PublicKeyStorage))
            {
                IDialogClient dialog = _testDriver.Client;
                await client.Initialize(dialog);
                _testDriver.SetUserAsTrusted(client.ClientConfig.UserId);

                Assert.IsTrue(await client.Core.TryMakeTextRequest("authentication", realTime: _realTime));
                _realTime.Step(TimeSpan.FromMilliseconds(100));

                // Ensure that the core emitted the proper events
                Assert.IsTrue((await client.SuccessEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero)).Success);
            }
        }

        [TestMethod]
        public async Task TestEndToEndTextQueryHttp()
        {
            using (ClientCoreTestWrapper client = new ClientCoreTestWrapper(_logger, false, _realTime, _testDriver.PublicKeyStorage))
            {
                IDialogClient dialog = _testDriver.Client;
                await client.Initialize(dialog);
                _testDriver.SetClientAsTrusted(client.ClientConfig.ClientId);

                for (int c = 0; c < 10; c++)
                {
                    client.ResetEvents();
                    Assert.IsTrue(await client.Core.TryMakeTextRequest("my name is darth vader", realTime: _realTime));
                    _realTime.Step(TimeSpan.FromMilliseconds(100));

                    // Ensure that the core emitted the proper events
                    Assert.IsTrue((await client.SuccessEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero)).Success);
                    RetrieveResult<CapturedEvent<TextEventArgs>> textRr = await client.DisplayTextEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                    Assert.IsTrue(textRr.Success);
                    Assert.AreEqual("Success", textRr.Result.Args.Text);
                }
            }
        }

        [TestMethod]
        public async Task TestEndToEndDialogActionsHttpProxied()
        {
            using (ClientCoreTestWrapper client = new ClientCoreTestWrapper(_logger, false, _realTime, _testDriver.PublicKeyStorage))
            {
                IDialogClient dialog = _testDriver.Client;
                await client.Initialize(dialog);
                _testDriver.SetClientAsTrusted(client.ClientConfig.ClientId);

                for (int c = 0; c < 10; c++)
                {
                    client.ResetEvents();
                    Assert.IsTrue(await client.Core.TryMakeTextRequest("start SPA", realTime: _realTime));
                    _realTime.Step(TimeSpan.FromMilliseconds(100));

                    // Ensure that the core emitted the proper events
                    Assert.IsTrue((await client.SuccessEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero)).Success);
                    RetrieveResult<CapturedEvent<TextEventArgs>> textRr = await client.DisplayTextEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                    Assert.IsTrue(textRr.Success);
                    string actionUrl = textRr.Result.Args.Text;

                    _realTime.Step(TimeSpan.FromMilliseconds(1000), 100);

                    // Make an HTTP GET to the client-local presentation layer to simulate a proxied dialog action request
                    HttpRequest fakeClientRequest = HttpRequest.CreateOutgoing(actionUrl, "GET");
                    DirectHttpClient httpClient = new DirectHttpClient(client.PresentationWebServer);
                    HttpResponse clientResponse = await httpClient.SendRequestAsync(fakeClientRequest, CancellationToken.None, _realTime).ConfigureAwait(false);
                    Assert.IsNotNull(clientResponse);
                    Assert.AreEqual(303, clientResponse.ResponseCode);
                    string redirectLocation = clientResponse.ResponseHeaders["Location"];
                    Assert.IsTrue(redirectLocation.Contains("page="));
                    string pageKey = redirectLocation.Substring(redirectLocation.IndexOf("page=") + 5, 32);
                    string newHtml = await client.PresentationWebServer.GetCachedPage(pageKey, _realTime);
                    Assert.AreEqual("<html></html>", newHtml);
                }
            }
        }

        [TestMethod]
        public async Task TestEndToEndDialogActionsHttpGet()
        {
            using (ClientCoreTestWrapper client = new ClientCoreTestWrapper(_logger, false, _realTime, _testDriver.PublicKeyStorage, false))
            {
                IDialogClient dialog = _testDriver.Client;
                await client.Initialize(dialog);
                _testDriver.SetClientAsTrusted(client.ClientConfig.ClientId);
                IHttpClient dialogHttpClient = _testDriver.HttpClient;

                for (int c = 0; c < 10; c++)
                {
                    client.ResetEvents();
                    Assert.IsTrue(await client.Core.TryMakeTextRequest("start SPA", realTime: _realTime));
                    _realTime.Step(TimeSpan.FromMilliseconds(100));

                    // Ensure that the core emitted the proper events
                    Assert.IsTrue((await client.SuccessEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero)).Success);
                    RetrieveResult<CapturedEvent<TextEventArgs>> textRr = await client.DisplayTextEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                    Assert.IsTrue(textRr.Success);
                    string actionUrl = textRr.Result.Args.Text;

                    // Make an HTTP GET request to localhost to execute the action, simulating interaction from the client device
                    HttpRequest httpReq = HttpRequest.CreateOutgoing(actionUrl, "GET");
                    NetworkResponseInstrumented<HttpResponse> netResp = await dialogHttpClient.SendInstrumentedRequestAsync(httpReq, CancellationToken.None, _realTime);
                    Assert.IsTrue(netResp.Success);
                    Assert.IsNotNull(netResp.Response);

                    // Follow the redirect
                    Assert.AreEqual(303, netResp.Response.ResponseCode);
                    string redirectLocation = netResp.Response.ResponseHeaders["Location"];
                    httpReq = HttpRequest.CreateOutgoing(redirectLocation, "GET");
                    netResp = await dialogHttpClient.SendInstrumentedRequestAsync(httpReq, CancellationToken.None, _realTime);
                    Assert.IsTrue(netResp.Success);
                    Assert.IsNotNull(netResp.Response);

                    string responseString = await netResp.Response.ReadContentAsStringAsync(CancellationToken.None, _realTime).ConfigureAwait(false);
                    Assert.IsNotNull(responseString);
                    Assert.AreEqual("<html></html>", responseString);
                }
            }
        }

        [TestMethod]
        public async Task TestEndToEndDialogActionsHttpSPA()
        {
            using (ClientCoreTestWrapper client = new ClientCoreTestWrapper(_logger, false, _realTime, _testDriver.PublicKeyStorage))
            {
                IDialogClient dialog = _testDriver.Client;
                await client.Initialize(dialog);
                _testDriver.SetClientAsTrusted(client.ClientConfig.ClientId);
                IHttpClient dialogHttpClient = _testDriver.HttpClient;

                for (int c = 0; c < 10; c++)
                {
                    client.ResetEvents();
                    Assert.IsTrue(await client.Core.TryMakeTextRequest("start SPA", realTime: _realTime));
                    _realTime.Step(TimeSpan.FromMilliseconds(100));

                    // Ensure that the core emitted the proper events
                    Assert.IsTrue((await client.SuccessEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero)).Success);
                    RetrieveResult<CapturedEvent<TextEventArgs>> textRr = await client.DisplayTextEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                    Assert.IsTrue(textRr.Success);
                    string actionUrl = textRr.Result.Args.Text;

                    // Make an HTTP PUT request to localhost to execute the action, simulating interaction from the client device from an SPA
                    Dictionary<string, string> requestData = new Dictionary<string, string>();
                    requestData[Guid.NewGuid().ToString("N")] = Guid.NewGuid().ToString("N");
                    requestData[Guid.NewGuid().ToString("N")] = Guid.NewGuid().ToString("N");

                    // Make an HTTP GET request to localhost to execute the action, simulating interaction from the client device
                    HttpRequest httpReq = HttpRequest.CreateOutgoing(actionUrl, "PUT");
                    httpReq.SetContent(JsonConvert.SerializeObject(requestData), "application/json");
                    NetworkResponseInstrumented<HttpResponse> netResp = await dialogHttpClient.SendInstrumentedRequestAsync(httpReq, CancellationToken.None, _realTime);
                    Assert.IsTrue(netResp.Success);
                    Assert.IsNotNull(netResp.Response);
                    string responseString = await netResp.Response.ReadContentAsStringAsync(CancellationToken.None, _realTime).ConfigureAwait(false);
                    DialogActionSpaResponse spaResponse = JsonConvert.DeserializeObject<DialogActionSpaResponse>(responseString);
                    Assert.IsTrue(spaResponse.Success);
                    foreach (var kvp in requestData)
                    {
                        Assert.IsTrue(spaResponse.Data.ContainsKey(kvp.Key));
                        Assert.AreEqual(kvp.Value, spaResponse.Data[kvp.Key]);
                    }
                }
            }
        }

        [TestMethod]
        public async Task TestEndToEndTextQueryWithClientEntities()
        {
            using (ClientCoreTestWrapper client = new ClientCoreTestWrapper(_logger, false, _realTime, _testDriver.PublicKeyStorage))
            {
                IDialogClient dialog = _testDriver.Client;
                await client.Initialize(dialog);
                _testDriver.SetClientAsTrusted(client.ClientConfig.ClientId);

                KnowledgeContext entityContext = new KnowledgeContext();

                List<ContextualEntity> inputEntities = new List<ContextualEntity>();
                Durandal.Tests.EntitySchemas.Person entityOne = new Durandal.Tests.EntitySchemas.Person(entityContext);
                entityOne.Name.Value = "Ralsei";
                inputEntities.Add(new ContextualEntity(entityOne, ContextualEntitySource.ClientInput, 1.0f));

                Durandal.Tests.EntitySchemas.Person entityTwo = new Durandal.Tests.EntitySchemas.Person(entityContext);
                entityTwo.Name.Value = "Kris";
                inputEntities.Add(new ContextualEntity(entityTwo, ContextualEntitySource.ClientInput, 1.0f));

                for (int c = 0; c < 10; c++)
                {
                    client.ResetEvents();
                    Assert.IsTrue(await client.Core.TryMakeTextRequest("here are some client entities", null, QueryFlags.None, inputEntities, entityContext, realTime: _realTime));
                    _realTime.Step(TimeSpan.FromMilliseconds(100));

                    // Ensure that the core emitted the proper events
                    Assert.IsTrue((await client.SuccessEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero)).Success);
                    RetrieveResult<CapturedEvent<TextEventArgs>> textRr = await client.DisplayTextEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                    Assert.IsTrue(textRr.Success);
                    Assert.AreEqual("You passed 2 entities!", textRr.Result.Args.Text);
                }
            }
        }

        [TestMethod]
        public async Task TestEndToEndAudioQueryWithClientEntities()
        {
            using (ClientCoreTestWrapper client = new ClientCoreTestWrapper(_logger, true, _realTime, _testDriver.PublicKeyStorage))
            {
                IDialogClient dialog = _testDriver.Client;
                await client.Initialize(dialog);
                _testDriver.SetClientAsTrusted(client.ClientConfig.ClientId);
                client.SpeechReco.SetRecoResult("en-US", "here are some client entities");

                KnowledgeContext entityContext = new KnowledgeContext();

                List<ContextualEntity> inputEntities = new List<ContextualEntity>();
                Durandal.Tests.EntitySchemas.Person entityOne = new Durandal.Tests.EntitySchemas.Person(entityContext);
                entityOne.Name.Value = "Susie";
                inputEntities.Add(new ContextualEntity(entityOne, ContextualEntitySource.ClientInput, 1.0f));

                Durandal.Tests.EntitySchemas.Person entityTwo = new Durandal.Tests.EntitySchemas.Person(entityContext);
                entityTwo.Name.Value = "Lancer";
                inputEntities.Add(new ContextualEntity(entityTwo, ContextualEntitySource.ClientInput, 1.0f));

                ClientContext context = new ClientContext()
                {
                    UserId = client.ClientConfig.UserId,
                    ClientId = client.ClientConfig.ClientId,
                    Capabilities = ClientCapabilities.DisplayUnlimitedText | ClientCapabilities.HasMicrophone | ClientCapabilities.HasSpeakers,
                    Locale = LanguageCode.EN_US
                };

                client.ResetEvents();
                Assert.IsTrue(await client.Core.TryMakeAudioRequest(context: context, flags: QueryFlags.None, inputEntities: inputEntities, entityContext: entityContext, realTime: _realTime));
                AudioSample mockUserSpeech = DialogTestHelpers.GenerateUtterance(client.Microphone.OutputFormat, 3000);
                client.Microphone.AddInput(mockUserSpeech);

                // Advance enough time to pipe the speech input. The client core uses a static utterance recorder with a 2 second length
                _realTime.Step(TimeSpan.FromMilliseconds(2000));
                _realTime.Step(TimeSpan.FromMilliseconds(100), 20);

                // Ensure that the core emitted the proper events
                Assert.IsTrue((await client.SuccessEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero)).Success);
                Assert.IsTrue((await client.PlayAudioEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero)).Success);
                RetrieveResult<CapturedEvent<TextEventArgs>> textRr = await client.DisplayTextEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                Assert.IsTrue(textRr.Success);
                Assert.AreEqual("You passed 2 entities!", textRr.Result.Args.Text);
            }
        }

        [TestMethod]
        public async Task TestEndToEndAudioQueryCustomAudio()
        {
            using (ClientCoreTestWrapper client = new ClientCoreTestWrapper(_logger, true, _realTime, _testDriver.PublicKeyStorage))
            {
                IDialogClient dialog = _testDriver.Client;
                await client.Initialize(dialog);
                _testDriver.SetClientAsTrusted(client.ClientConfig.ClientId);
                client.SpeechReco.SetRecoResult("en-US", "custom audio");

                client.ResetEvents();
                Assert.IsTrue(await client.Core.TryMakeAudioRequest(realTime: _realTime));
                AudioSample mockUserSpeech = DialogTestHelpers.GenerateUtterance(client.Microphone.OutputFormat, 3000);
                client.Microphone.AddInput(mockUserSpeech);

                // Advance enough time to pipe the speech input. The client core uses a static utterance recorder with a 2 second length
                _realTime.Step(TimeSpan.FromMilliseconds(2000));
                _realTime.Step(TimeSpan.FromMilliseconds(100), 20);

                // Ensure that the core emitted the proper events
                Assert.IsTrue((await client.SuccessEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero)).Success);
                Assert.IsTrue((await client.PlayAudioEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero)).Success);
            }
        }

        [TestMethod]
        public async Task TestEndToEndTextQueryInstrumentation()
        {
            MetricCollector metrics = _testDriver.MetricCollector;
            FakeMetricOutput metricInterceptor = new FakeMetricOutput();
            metrics.AddMetricOutput(metricInterceptor);
            ILogger metaTraceLogger = new ConsoleLogger(); // used when we are creating traces from the other main logger
            IStringDecrypterPii piiDecrypter = new NullStringEncrypter();
            using (ClientCoreTestWrapper client = new ClientCoreTestWrapper(_logger, false, _realTime, _testDriver.PublicKeyStorage))
            {
                IDialogClient dialog = _testDriver.Client;
                await client.Initialize(dialog);
                _testDriver.SetClientAsTrusted(client.ClientConfig.ClientId);

                client.ResetEvents();
                Assert.IsTrue(await client.Core.TryMakeTextRequest("my name is darth vader", realTime: _realTime));
                _realTime.Step(TimeSpan.FromMilliseconds(100));

                // Ensure that the core emitted the proper events
                Assert.IsTrue((await client.SuccessEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero)).Success);

                RetrieveResult<CapturedEvent<TextEventArgs>> textRr = await client.DisplayTextEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                Assert.IsTrue(textRr.Success);
                Assert.AreEqual("Success", textRr.Result.Args.Text);

                await _logger.Flush(CancellationToken.None, _realTime, true);

                // Find the most recent traceid and use that to pull a trace
                ILoggingHistory history = _eventLogger.History;
                LogEvent mostRecentEvent = history.FilterByCriteria(new FilterCriteria() { SearchTerm = "Dialog engine top input" }, true).FirstOrDefault();

                Assert.IsNotNull(mostRecentEvent);
                Assert.IsTrue(mostRecentEvent.TraceId.HasValue);
                Guid traceId = mostRecentEvent.TraceId.Value;

                UnifiedTrace trace = UnifiedTrace.CreateFromLogData(
                    traceId,
                    history.FilterByCriteria(new FilterCriteria()
                    {
                        TraceId = traceId
                    }),
                    metaTraceLogger,
                    piiDecrypter);

                Assert.IsNotNull(trace);
                Assert.IsTrue(trace.TraceDuration > 0);
                Assert.AreEqual("UnitTestClient", trace.ClientId);
                Assert.AreEqual("UnitTestUser", trace.UserId);
                Assert.AreEqual(0, trace.ErrorLogCount);
                Assert.IsTrue(string.IsNullOrEmpty(trace.ErrorMessage));
                Assert.AreEqual("my name is darth vader", trace.InputString);
                Assert.AreEqual(InputMethod.Typed, trace.InteractionType);
                Assert.IsTrue(trace.LogCount > 0);
                Assert.AreEqual("Success", trace.ResponseText);
                Assert.AreEqual("test", trace.TriggeredDomain);
                Assert.AreEqual("vader", trace.TriggeredIntent);
                Assert.AreEqual("InqueTest", trace.DialogHost);
                Assert.AreEqual("bond", trace.DialogProtocol);
                Assert.AreEqual("InqueTest", trace.LUHost);
                Assert.AreEqual("bond", trace.LUProtocol);
                Assert.AreEqual(DialogEventType.Query, trace.DialogEventType);
                Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Dialog_E2E));
                Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Client_E2E));
                Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_LU_E2E));
                Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Dialog_Core));
                Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Dialog_LUCall));
                Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Dialog_Triggers));
                Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Client_GenerateRequestToken));
                Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Plugin_Execute));
                Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Plugin_Trigger));
                Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Store_SessionRead));
                Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Store_SessionClear));
                Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Store_UserProfileRead));
                Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Store_UserProfileRead));
                Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Store_UserProfileRead));
                //Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Store_CacheWrite));
                //Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Store_WebCacheRead));

                Assert.IsTrue(trace.Sizes.ContainsKey(CommonInstrumentation.Key_Size_Client_Request));
                Assert.IsTrue(trace.Sizes.ContainsKey(CommonInstrumentation.Key_Size_Client_Response));
                Assert.IsTrue(trace.Sizes.ContainsKey(CommonInstrumentation.Key_Size_Dialog_InputPayload));
                Assert.IsTrue(trace.Sizes.ContainsKey(CommonInstrumentation.Key_Size_Dialog_OutputPayload));
                Assert.IsTrue(trace.Sizes.ContainsKey(CommonInstrumentation.Key_Size_LU_InputPayload));
                Assert.IsTrue(trace.Sizes.ContainsKey(CommonInstrumentation.Key_Size_LU_OutputPayload));

                // Fast-forward time so we can ensure that metrics are being reported correctly
                for (int loop = 0; loop < 10; loop++)
                {
                    _realTime.Step(TimeSpan.FromSeconds(1));
                    if (metricInterceptor.MetricHasValue(CommonInstrumentation.Key_Counter_LU_WebRequestCount))
                    {
                        break;
                    }
                }

                Assert.IsTrue(metricInterceptor.MetricHasValue(CommonInstrumentation.Key_Counter_LU_WebRequestCount));
                Assert.IsTrue(metricInterceptor.MetricHasValue(CommonInstrumentation.Key_Counter_Dialog_WebRequestCount));
                Assert.IsTrue(metricInterceptor.MetricHasValue(CommonInstrumentation.Key_Counter_Dialog_CoreLatency + "_p0.99"));
            }
        }

        [TestMethod]
        public async Task TestEndToEndAudioQueryInstrumentationSyncAudio()
        {
            using (MetricCollector metrics = _testDriver.MetricCollector)
            {
                FakeMetricOutput metricInterceptor = new FakeMetricOutput();
                metrics.AddMetricOutput(metricInterceptor);
                ILogger metaTraceLogger = new ConsoleLogger(); // used when we are creating traces from the other main logger
                IStringDecrypterPii piiDecrypter = new NullStringEncrypter();
                using (ClientCoreTestWrapper client = new ClientCoreTestWrapper(_logger, true, _realTime, _testDriver.PublicKeyStorage))
                {
                    IDialogClient dialog = _testDriver.Client;
                    await client.Initialize(dialog);
                    _testDriver.SetClientAsTrusted(client.ClientConfig.ClientId);
                    client.SpeechReco.SetRecoResult("en-US", "my name is darth vader");

                    client.ResetEvents();
                    Assert.IsTrue(await client.Core.TryMakeAudioRequest(realTime: _realTime));
                    AudioSample mockUserSpeech = DialogTestHelpers.GenerateUtterance(client.Microphone.OutputFormat, 3000);
                    client.Microphone.AddInput(mockUserSpeech);

                    // Advance enough time to pipe the speech input. The client core uses a static utterance recorder with a 2 second length
                    _realTime.Step(TimeSpan.FromMilliseconds(2000));
                    _realTime.Step(TimeSpan.FromMilliseconds(100), 20);

                    // Ensure that the core emitted the proper events
                    Assert.IsTrue((await client.SuccessEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero)).Success);
                    Assert.IsTrue((await client.PlayAudioEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero)).Success);

                    await _logger.Flush(CancellationToken.None, _realTime, true);

                    // Find the most recent traceid and use that to pull a trace
                    ILoggingHistory history = _eventLogger.History;
                    LogEvent mostRecentEvent = history.FilterByCriteria(new FilterCriteria() { SearchTerm = "Dialog engine top input" }, true).FirstOrDefault();

                    Assert.IsNotNull(mostRecentEvent);
                    Assert.IsTrue(mostRecentEvent.TraceId.HasValue);
                    Guid traceId = mostRecentEvent.TraceId.Value;

                    UnifiedTrace trace = UnifiedTrace.CreateFromLogData(
                        traceId,
                        history.FilterByCriteria(new FilterCriteria()
                        {
                            TraceId = traceId
                        }),
                        metaTraceLogger,
                        piiDecrypter);

                    Assert.IsNotNull(trace);
                    Assert.IsTrue(trace.TraceDuration > 0);
                    Assert.AreEqual("UnitTestClient", trace.ClientId);
                    Assert.AreEqual("UnitTestUser", trace.UserId);
                    Assert.AreEqual(0, trace.ErrorLogCount);
                    Assert.IsTrue(string.IsNullOrEmpty(trace.ErrorMessage));
                    Assert.AreEqual("my name is darth vader", trace.InputString);
                    Assert.AreEqual(InputMethod.Spoken, trace.InteractionType);
                    Assert.IsTrue(trace.LogCount > 0);
                    Assert.AreEqual("Success", trace.ResponseText);
                    Assert.AreEqual("test", trace.TriggeredDomain);
                    Assert.AreEqual("vader", trace.TriggeredIntent);
                    Assert.AreEqual("InqueTest", trace.DialogHost);
                    Assert.AreEqual("bond", trace.DialogProtocol);
                    Assert.AreEqual("InqueTest", trace.LUHost);
                    Assert.AreEqual("bond", trace.LUProtocol);
                    Assert.AreEqual(DialogEventType.Query, trace.DialogEventType);
                    Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Dialog_E2E));
                    Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Client_E2E));
                    Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_LU_E2E));
                    Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Dialog_Core));
                    Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Dialog_LUCall));
                    Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Dialog_Triggers));
                    Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Plugin_Execute));
                    Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Plugin_Trigger));
                    Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Client_GenerateRequestToken));
                    Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Store_SessionRead));
                    Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Store_SessionClear));
                    Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Store_UserProfileRead));
                    Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Dialog_ProcessSyncAudio));
                    Assert.IsTrue(trace.Sizes.ContainsKey(CommonInstrumentation.Key_Size_Client_Request));
                    Assert.IsTrue(trace.Sizes.ContainsKey(CommonInstrumentation.Key_Size_Client_Response));
                    Assert.IsTrue(trace.Sizes.ContainsKey(CommonInstrumentation.Key_Size_Dialog_InputPayload));
                    Assert.IsTrue(trace.Sizes.ContainsKey(CommonInstrumentation.Key_Size_Dialog_OutputPayload));
                    Assert.IsTrue(trace.Sizes.ContainsKey(CommonInstrumentation.Key_Size_LU_InputPayload));
                    Assert.IsTrue(trace.Sizes.ContainsKey(CommonInstrumentation.Key_Size_LU_OutputPayload));

                    // Fast-forward time so we can ensure that metrics are being reported correctly
                    for (int loop = 0; loop < 10; loop++)
                    {
                        _realTime.Step(TimeSpan.FromSeconds(1));
                        if (metricInterceptor.MetricHasValue(CommonInstrumentation.Key_Counter_LU_WebRequestCount))
                        {
                            break;
                        }
                    }

                    Assert.IsTrue(metricInterceptor.MetricHasValue(CommonInstrumentation.Key_Counter_LU_WebRequestCount));
                    Assert.IsTrue(metricInterceptor.MetricHasValue(CommonInstrumentation.Key_Counter_Dialog_WebRequestCount));
                    Assert.IsTrue(metricInterceptor.MetricHasValue(CommonInstrumentation.Key_Counter_Dialog_CoreLatency + "_p0.99"));
                }
            }
        }

        [Ignore] // Client async audio is still broken
        [TestMethod]
        public async Task TestEndToEndAudioQueryInstrumentationAsyncAudio()
        {
            using (MetricCollector metrics = _testDriver.MetricCollector)
            {
                FakeMetricOutput metricInterceptor = new FakeMetricOutput();
                metrics.AddMetricOutput(metricInterceptor);
                ILogger metaTraceLogger = new ConsoleLogger(); // used when we are creating traces from the other main logger
                IStringDecrypterPii piiDecrypter = new NullStringEncrypter();
                using (ClientCoreTestWrapper client = new ClientCoreTestWrapper(_logger, true, _realTime, _testDriver.PublicKeyStorage))
                {
                    IDialogClient dialog = _testDriver.Client;
                    await client.Initialize(dialog);
                    client.EnableStreamingAudio = true;
                    _testDriver.SetClientAsTrusted(client.ClientConfig.ClientId);
                    client.SpeechReco.SetRecoResult("en-US", "my name is darth vader");

                    client.ResetEvents();
                    Assert.IsTrue(await client.Core.TryMakeAudioRequest(realTime: _realTime));
                    AudioSample mockUserSpeech = DialogTestHelpers.GenerateUtterance(client.Microphone.OutputFormat, 3000);
                    client.Microphone.AddInput(mockUserSpeech);

                    // Advance enough time to pipe the speech input. The client core uses a static utterance recorder with a 2 second length
                    _realTime.Step(TimeSpan.FromMilliseconds(2000));
                    _realTime.Step(TimeSpan.FromMilliseconds(100), 20);

                    // Ensure that the core emitted the proper events
                    Assert.IsTrue((await client.SuccessEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero)).Success);
                    Assert.IsTrue((await client.PlayAudioEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero)).Success);

                    // Give time for the client to play the response
                    _realTime.Step(TimeSpan.FromMilliseconds(3000));

                    await _logger.Flush(CancellationToken.None, _realTime, true);

                    // Find the most recent traceid and use that to pull a trace
                    ILoggingHistory history = _eventLogger.History;
                    LogEvent mostRecentEvent = history.FilterByCriteria(new FilterCriteria() { SearchTerm = "Dialog engine top input" }, true).FirstOrDefault();

                    Assert.IsNotNull(mostRecentEvent);
                    Assert.IsTrue(mostRecentEvent.TraceId.HasValue);
                    Guid traceId = mostRecentEvent.TraceId.Value;

                    UnifiedTrace trace = UnifiedTrace.CreateFromLogData(
                        traceId,
                        history.FilterByCriteria(new FilterCriteria()
                        {
                            TraceId = traceId
                        }),
                        metaTraceLogger,
                        piiDecrypter);

                    Assert.IsNotNull(trace);
                    Assert.IsTrue(trace.TraceDuration > 0);
                    Assert.AreEqual("UnitTestClient", trace.ClientId);
                    Assert.AreEqual("UnitTestUser", trace.UserId);
                    Assert.AreEqual(0, trace.ErrorLogCount);
                    Assert.IsTrue(string.IsNullOrEmpty(trace.ErrorMessage));
                    Assert.AreEqual("my name is darth vader", trace.InputString);
                    Assert.AreEqual(InputMethod.Spoken, trace.InteractionType);
                    Assert.IsTrue(trace.LogCount > 0);
                    Assert.AreEqual("Success", trace.ResponseText);
                    Assert.AreEqual("test", trace.TriggeredDomain);
                    Assert.AreEqual("vader", trace.TriggeredIntent);
                    Assert.AreEqual("InqueTest", trace.DialogHost);
                    Assert.AreEqual("bond", trace.DialogProtocol);
                    Assert.AreEqual("InqueTest", trace.LUHost);
                    Assert.AreEqual("bond", trace.LUProtocol);
                    Assert.AreEqual(DialogEventType.Query, trace.DialogEventType);
                    Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Dialog_E2E));
                    Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Client_E2E));
                    Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_LU_E2E));
                    Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Dialog_Core));
                    Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Dialog_LUCall));
                    Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Dialog_Triggers));
                    Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Plugin_Execute));
                    Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Plugin_Trigger));
                    Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Client_GenerateRequestToken));
                    Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Store_SessionRead));
                    Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Store_SessionClear));
                    Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Store_UserProfileRead));
                    Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Dialog_ProcessAsyncAudio));
                    Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Dialog_StreamingAudioBeginWrite));
                    Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Dialog_StreamingAudioWrite));
                    Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Dialog_StreamingAudioTimeInCache));
                    Assert.IsTrue(trace.Sizes.ContainsKey(CommonInstrumentation.Key_Size_Client_Request));
                    Assert.IsTrue(trace.Sizes.ContainsKey(CommonInstrumentation.Key_Size_Client_Response));
                    Assert.IsTrue(trace.Sizes.ContainsKey(CommonInstrumentation.Key_Size_Dialog_InputPayload));
                    Assert.IsTrue(trace.Sizes.ContainsKey(CommonInstrumentation.Key_Size_Dialog_OutputPayload));
                    Assert.IsTrue(trace.Sizes.ContainsKey(CommonInstrumentation.Key_Size_LU_InputPayload));
                    Assert.IsTrue(trace.Sizes.ContainsKey(CommonInstrumentation.Key_Size_LU_OutputPayload));

                    // Fast-forward time so we can ensure that metrics are being reported correctly
                    for (int loop = 0; loop < 10; loop++)
                    {
                        _realTime.Step(TimeSpan.FromSeconds(1));
                        if (metricInterceptor.MetricHasValue(CommonInstrumentation.Key_Counter_LU_WebRequestCount))
                        {
                            break;
                        }
                    }

                    Assert.IsTrue(metricInterceptor.MetricHasValue(CommonInstrumentation.Key_Counter_LU_WebRequestCount));
                    Assert.IsTrue(metricInterceptor.MetricHasValue(CommonInstrumentation.Key_Counter_Dialog_WebRequestCount));
                    Assert.IsTrue(metricInterceptor.MetricHasValue(CommonInstrumentation.Key_Counter_Dialog_CoreLatency + "_p0.99"));
                }
            }
        }

        [TestMethod]
        public async Task TestEndToEndDialogActionHttpProxiedInstrumentation()
        {
            ILogger metaTraceLogger = new ConsoleLogger(); // used when we are creating traces from the other main logger
            IStringDecrypterPii piiDecrypter = new NullStringEncrypter();
            using (ClientCoreTestWrapper client = new ClientCoreTestWrapper(_logger, false, _realTime, _testDriver.PublicKeyStorage))
            {
                IDialogClient dialog = _testDriver.Client;
                await client.Initialize(dialog);
                _testDriver.SetClientAsTrusted(client.ClientConfig.ClientId);

                client.ResetEvents();
                Assert.IsTrue(await client.Core.TryMakeTextRequest("start SPA", realTime: _realTime));
                _realTime.Step(TimeSpan.FromMilliseconds(100));

                await _logger.Flush(CancellationToken.None, _realTime, true);

                // Get the trace ID of the first turn
                ILoggingHistory history = _eventLogger.History;
                LogEvent mostRecentEvent = history.FilterByCriteria(new FilterCriteria() { SearchTerm = "Query hypothesis for \"start SPA\"" }, true).FirstOrDefault();

                Assert.IsNotNull(mostRecentEvent);
                Assert.IsTrue(mostRecentEvent.TraceId.HasValue);
                Guid firstTurnTraceId = mostRecentEvent.TraceId.Value;

                // Ensure that the core emitted the proper events
                Assert.IsTrue((await client.SuccessEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero)).Success);
                RetrieveResult<CapturedEvent<TextEventArgs>> textRr = await client.DisplayTextEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                Assert.IsTrue(textRr.Success);
                string actionUrl = textRr.Result.Args.Text;

                _realTime.Step(TimeSpan.FromMilliseconds(1000), 100);

                // Make an HTTP GET to the client-local presentation layer to simulate a proxied dialog action request
                HttpRequest fakeClientRequest = HttpRequest.CreateOutgoing(actionUrl, "GET");
                DirectHttpClient directClient = new DirectHttpClient(client.PresentationWebServer);
                HttpResponse clientResponse = await directClient.SendRequestAsync(fakeClientRequest, CancellationToken.None, _realTime).ConfigureAwait(false);
                Assert.IsNotNull(clientResponse);
                Assert.AreEqual(303, clientResponse.ResponseCode);
                string redirectLocation = clientResponse.ResponseHeaders["Location"];
                Assert.IsTrue(redirectLocation.Contains("page="));
                string pageKey = redirectLocation.Substring(redirectLocation.IndexOf("page=") + 5, 32);
                string newHtml = await client.PresentationWebServer.GetCachedPage(pageKey, _realTime);
                Assert.AreEqual("<html></html>", newHtml);

                await _logger.Flush(CancellationToken.None, _realTime, true);

                // Now pull a trace of the SPA turn
                history = _eventLogger.History;
                mostRecentEvent = history.FilterByCriteria(new FilterCriteria() { SearchTerm = "Triggering custom dialog action" }, true).FirstOrDefault();

                Assert.IsNotNull(mostRecentEvent);
                Assert.IsTrue(mostRecentEvent.TraceId.HasValue);
                Guid spaActionTraceId = mostRecentEvent.TraceId.Value;

                Assert.AreNotEqual(firstTurnTraceId, spaActionTraceId);

                UnifiedTrace trace = UnifiedTrace.CreateFromLogData(
                    spaActionTraceId,
                    history.FilterByCriteria(new FilterCriteria()
                    {
                        TraceId = spaActionTraceId
                    }),
                    metaTraceLogger,
                    piiDecrypter);

                Assert.IsNotNull(trace);
                Assert.IsTrue(trace.TraceDuration > 0);
                Assert.AreEqual("UnitTestClient", trace.ClientId);
                Assert.AreEqual("UnitTestUser", trace.UserId);
                Assert.AreEqual(0, trace.ErrorLogCount);
                Assert.IsTrue(string.IsNullOrEmpty(trace.ErrorMessage));
                Assert.AreEqual(InputMethod.Tactile, trace.InteractionType);
                Assert.IsTrue(trace.LogCount > 0);
                Assert.AreEqual("turn1", trace.ResponseText);
                Assert.AreEqual("test", trace.TriggeredDomain);
                Assert.AreEqual("spa_continue", trace.TriggeredIntent);
                Assert.AreEqual(DialogEventType.DialogAction, trace.DialogEventType);

                Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Dialog_E2E));
                Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Client_E2E));
                Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Dialog_Core));
                Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Dialog_Triggers));
                Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Plugin_Execute));
                Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Plugin_Trigger));
                Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Store_SessionRead));
                Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Store_SessionWriteClientState));
                Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Store_UserProfileRead));
                //Assert.IsTrue(trace.Latencies.ContainsKey("SessionStoreRoamingState"));
                //Assert.IsTrue(trace.Latencies.ContainsKey("Client_GenerateRequestToken"));
                Assert.IsTrue(trace.Sizes.ContainsKey(CommonInstrumentation.Key_Size_Client_Request));
                Assert.IsTrue(trace.Sizes.ContainsKey(CommonInstrumentation.Key_Size_Client_Response));
                Assert.IsTrue(trace.Sizes.ContainsKey(CommonInstrumentation.Key_Size_Dialog_InputPayload));
                Assert.IsTrue(trace.Sizes.ContainsKey(CommonInstrumentation.Key_Size_Dialog_OutputPayload));
            }
        }

        [TestMethod]
        public async Task TestEndToEndDialogActionHttpGetInstrumentation()
        {
            ILogger metaTraceLogger = new ConsoleLogger(); // used when we are creating traces from the other main logger
            IStringDecrypterPii piiDecrypter = new NullStringEncrypter();
            using (ClientCoreTestWrapper client = new ClientCoreTestWrapper(_logger, false, _realTime, _testDriver.PublicKeyStorage, false))
            {
                IDialogClient dialog = _testDriver.Client;
                await client.Initialize(dialog);
                _testDriver.SetClientAsTrusted(client.ClientConfig.ClientId);

                client.ResetEvents();
                Assert.IsTrue(await client.Core.TryMakeTextRequest("start SPA", realTime: _realTime));
                _realTime.Step(TimeSpan.FromMilliseconds(100));

                await _logger.Flush(CancellationToken.None, _realTime, true);

                // Get the trace ID of the first turn
                ILoggingHistory history = _eventLogger.History;
                LogEvent mostRecentEvent = history.FilterByCriteria(new FilterCriteria() { SearchTerm = "Query hypothesis for \"start SPA\"" }, true).FirstOrDefault();

                Assert.IsNotNull(mostRecentEvent);
                Assert.IsTrue(mostRecentEvent.TraceId.HasValue);
                Guid firstTurnTraceId = mostRecentEvent.TraceId.Value;

                // Ensure that the core emitted the proper events
                Assert.IsTrue((await client.SuccessEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero)).Success);
                RetrieveResult<CapturedEvent<TextEventArgs>> textRr = await client.DisplayTextEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                Assert.IsTrue(textRr.Success);
                string actionUrl = textRr.Result.Args.Text;

                _realTime.Step(TimeSpan.FromMilliseconds(1000), 100);

                // Make an HTTP GET to the client-local presentation layer to simulate a proxied dialog action request
                HttpRequest fakeClientRequest = HttpRequest.CreateOutgoing(actionUrl, "GET");
                NetworkResponseInstrumented<HttpResponse> netResp = await _testDriver.HttpClient.SendInstrumentedRequestAsync(fakeClientRequest, CancellationToken.None, _realTime, _logger);
                HttpResponse clientResponse = netResp.Response;
                Assert.IsNotNull(clientResponse);
                Assert.AreEqual(303, clientResponse.ResponseCode);
                string redirectLocation = clientResponse.ResponseHeaders["Location"];
                HttpRequest httpReq = HttpRequest.CreateOutgoing(redirectLocation, "GET");
                await netResp.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                netResp.Dispose();
                netResp = await _testDriver.HttpClient.SendInstrumentedRequestAsync(httpReq, CancellationToken.None, _realTime);
                httpReq.Dispose();
                Assert.IsTrue(netResp.Success);
                Assert.IsNotNull(netResp.Response);
                await netResp.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                netResp.Dispose();

                await _logger.Flush(CancellationToken.None, _realTime, true);

                // Now pull a trace of the SPA turn
                history = _eventLogger.History;
                mostRecentEvent = history.FilterByCriteria(new FilterCriteria() { SearchTerm = "Triggering custom dialog action" }, true).FirstOrDefault();

                Assert.IsNotNull(mostRecentEvent);
                Assert.IsTrue(mostRecentEvent.TraceId.HasValue);
                Guid spaActionTraceId = mostRecentEvent.TraceId.Value;

                Assert.AreNotEqual(firstTurnTraceId, spaActionTraceId);

                UnifiedTrace trace = UnifiedTrace.CreateFromLogData(
                    spaActionTraceId,
                    history.FilterByCriteria(new FilterCriteria()
                    {
                        TraceId = spaActionTraceId
                    }),
                    metaTraceLogger,
                    piiDecrypter);

                Assert.IsNotNull(trace);
                Assert.IsTrue(trace.TraceDuration > 0);
                Assert.AreEqual("UnitTestClient", trace.ClientId);
                Assert.AreEqual("UnitTestUser", trace.UserId);
                Assert.AreEqual(0, trace.ErrorLogCount);
                Assert.AreEqual(string.Empty, trace.ErrorMessage);
                Assert.AreEqual(InputMethod.Tactile, trace.InteractionType);
                Assert.IsTrue(trace.LogCount > 0);
                Assert.AreEqual("turn1", trace.ResponseText);
                Assert.AreEqual("test", trace.TriggeredDomain);
                Assert.AreEqual("spa_continue", trace.TriggeredIntent);
                Assert.AreEqual(DialogEventType.DialogAction, trace.DialogEventType);
                Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Dialog_E2E));
                // Assert.IsTrue(trace.Latencies.ContainsKey("Client_E2E"));
                Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Dialog_Core));
                Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Dialog_Triggers));
                Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Plugin_Execute));
                Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Plugin_Trigger));
                Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Store_SessionWriteClientState));
                //Assert.IsTrue(trace.Latencies.ContainsKey("SessionStoreRoamingState"));
                Assert.IsTrue(trace.Latencies.ContainsKey(CommonInstrumentation.Key_Latency_Store_UserProfileRead));
                // Assert.IsTrue(trace.Latencies.ContainsKey("Client_GenerateRequestToken"));
                //Assert.IsTrue(trace.Sizes.ContainsKey(CommonInstrumentation.Key_Size_Client_Request));
                //Assert.IsTrue(trace.Sizes.ContainsKey(CommonInstrumentation.Key_Size_Client_Response));
                Assert.IsTrue(trace.Sizes.ContainsKey(CommonInstrumentation.Key_Size_Dialog_InputPayload));
                Assert.IsTrue(trace.Sizes.ContainsKey(CommonInstrumentation.Key_Size_Dialog_OutputPayload));
            }
        }

        [TestMethod]
        public async Task TestEndToEndAudioMultipleSilenceInARow()
        {
            using (ClientCoreTestWrapper client = new ClientCoreTestWrapper(_logger, true, _realTime, _testDriver.PublicKeyStorage))
            {
                IDialogClient dialog = _testDriver.Client;
                await client.Initialize(dialog);
                _testDriver.SetClientAsTrusted(client.ClientConfig.ClientId);
                client.SpeechReco.SetRecoResult("en-US", "this speech recognition should fail because the input audio is silent");

                for (int loop = 0; loop < 2; loop++)
                {
                    client.ResetEvents();
                    Assert.IsTrue(await client.Core.TryMakeAudioRequest(realTime: _realTime));

                    // Advance enough time to pipe the speech input. The client core uses a static utterance recorder with a 2 second length
                    _realTime.Step(TimeSpan.FromMilliseconds(100), 20);
                    Assert.IsTrue((await client.AudioPromptEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero)).Success);
                    _realTime.Step(TimeSpan.FromMilliseconds(2000));
                    _realTime.Step(TimeSpan.FromMilliseconds(100), 20);

                    // Should have fired an event for "nothing heard" and then reverted to listening state
                    RetrieveResult<CapturedEvent<SpeechCaptureEventArgs>> rr = await client.SpeechCaptureFinishedEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                    Assert.IsTrue(rr.Success);
                    Assert.IsFalse(rr.Result.Args.Success);
                }
            }
        }

        [TestMethod]
        public async Task TestEndToEndAudioHandleSpeechRecoThrowsIntermediateError()
        {
            using (ClientCoreTestWrapper client = new ClientCoreTestWrapper(_logger, true, _realTime, _testDriver.PublicKeyStorage))
            {
                IDialogClient dialog = _testDriver.Client;
                await client.Initialize(dialog);
                _testDriver.SetClientAsTrusted(client.ClientConfig.ClientId);
                client.SpeechReco.SetRecoResult("en-US", "speech reco should throw an exception before it gets here");
                client.SpeechReco.ShouldThrowExceptionOnIntermediateRecognize = true;

                for (int loop = 0; loop < 2; loop++)
                {
                    client.ResetEvents();
                    Assert.IsTrue(await client.Core.TryMakeAudioRequest(realTime: _realTime));
                    AudioSample mockUserSpeech = DialogTestHelpers.GenerateUtterance(client.Microphone.OutputFormat, 2000);
                    client.Microphone.AddInput(mockUserSpeech);
                    _realTime.Step(TimeSpan.FromMilliseconds(2000), 100);
                    _realTime.Step(TimeSpan.FromMilliseconds(100), 10);

                    // Should have fired an event for "speech reco failed" and then reverted to listening state
                    RetrieveResult<CapturedEvent<SpeechCaptureEventArgs>> rr = await client.SpeechCaptureFinishedEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromMilliseconds(1000));
                    Assert.IsTrue(rr.Success);
                    Assert.IsFalse(rr.Result.Args.Success);
                }
            }
        }

        [TestMethod]
        public async Task TestEndToEndAudioHandleSpeechRecoThrowsErrorOnFinish()
        {
            using (ClientCoreTestWrapper client = new ClientCoreTestWrapper(_logger, true, _realTime, _testDriver.PublicKeyStorage))
            {
                IDialogClient dialog = _testDriver.Client;
                await client.Initialize(dialog);
                _testDriver.SetClientAsTrusted(client.ClientConfig.ClientId);
                client.SpeechReco.SetRecoResult("en-US", "speech reco should throw an exception before it gets here");
                client.SpeechReco.ShouldThrowExceptionOnFinishRecognize = true;

                for (int loop = 0; loop < 2; loop++)
                {
                    client.ResetEvents();
                    Assert.IsTrue(await client.Core.TryMakeAudioRequest(realTime: _realTime));
                    AudioSample mockUserSpeech = DialogTestHelpers.GenerateUtterance(client.Microphone.OutputFormat, 2000);
                    client.Microphone.AddInput(mockUserSpeech);
                    _realTime.Step(TimeSpan.FromMilliseconds(2000), 100);
                    _realTime.Step(TimeSpan.FromMilliseconds(100), 10);

                    // Should have fired an event for "speech reco failed" and then reverted to listening state
                    RetrieveResult<CapturedEvent<SpeechCaptureEventArgs>> rr = await client.SpeechCaptureFinishedEvent.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                    Assert.IsTrue(rr.Success);
                    Assert.IsFalse(rr.Result.Args.Success);
                }
            }
        }

        // Todo: Add a test for pressing the button while the previous turn audio is still playing
    }
}
