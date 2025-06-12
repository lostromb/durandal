namespace Durandal.Tests.Common.Dialog
{
    using Durandal.API;
    using Durandal.Common.Audio;
    using Durandal.Common.Audio.Codecs;
    using Durandal.Common.Collections;
    using Durandal.Common.Dialog.Web;
    using Durandal.Common.IO;
    using Durandal.Common.Logger;
    using Durandal.Common.MathExt;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.Test;
    using Durandal.Extensions.BondProtocol;
    using Durandal.Extensions.Compression.ZStandard.Dialog;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Text;

    [TestClass]
    public class ProtocolTests
    {
        [TestMethod]
        public void TestProtocolDialogJson()
        {
            TestDialogProtocol(new DialogJsonTransportProtocol());
        }

        [TestMethod]
        public void TestProtocolDialogLZ4Json()
        {
            TestDialogProtocol(new DialogLZ4JsonTransportProtocol());
        }

        [TestMethod]
        public void TestProtocolDialogZStdJson()
        {
            TestDialogProtocol(new DialogZstdJsonTransportProtocol());
        }

        [TestMethod]
        public void TestProtocolDialogZStdDictJsonV1()
        {
            TestDialogProtocol(new DialogZstdDictJsonTransportProtocolV1());
        }

        [TestMethod]
        public void TestProtocolDialogBond()
        {
            TestDialogProtocol(new DialogBondTransportProtocol());
        }

        [TestMethod]
        public void TestProtocolDialogLZ4Bond()
        {
            TestDialogProtocol(new DialogLZ4BondTransportProtocol());
        }

        [TestMethod]
        public void TestProtocolDialogZstdBond()
        {
            TestDialogProtocol(new DialogZstdBondTransportProtocol());
        }

        [TestMethod]
        public void TestProtocolDialogZstdDictBondV1()
        {
            TestDialogProtocol(new DialogZstdDictBondTransportProtocolV1());
        }

        private static void TestDialogProtocol(IDialogTransportProtocol protocol)
        {
            ILogger logger = new ConsoleLogger();
            DialogRequest inputRequest = new DialogRequest()
            {
                AudioInput = new AudioData()
                {
                    Codec = "opus",
                    CodecParams = "framesize=20",
                    Data = new ArraySegment<byte>(RandomByteArray())
                },
                AuthTokens = new List<SecurityToken>()
                {
                    new SecurityToken()
                    {
                        Blue = "blue",
                        Red = "red",
                        Scope  = ClientAuthenticationScope.UserClient
                    }
                },
                ClientContext = new ClientContext()
                {
                    Capabilities = ClientCapabilities.DisplayBasicHtml | ClientCapabilities.DisplayBasicText | ClientCapabilities.HasInternetConnection,
                    ClientId = "TestClient",
                    ClientName = "Test client name",
                    ExtraClientContext = new Dictionary<string, string>()
                    {
                        { "Dark", "Yes" },
                        { "Sunglasses", "On" }
                    },
                    Latitude = 50,
                    Longitude = -100,
                    Locale = LanguageCode.Parse("en-gb"),
                    UserId = "TestUser",
                    UserTimeZone = "GMT",
                    UTCOffset = 0
                },
                EntityContext = new ArraySegment<byte>(RandomByteArray()),
                EntityInput = new List<EntityReference>()
                {
                    new EntityReference()
                    {
                        EntityId = "entityId",
                        Relevance = 0.95f
                    }
                },
                InteractionType = InputMethod.Spoken,
                PreferredAudioCodec = "opus",
                PreferredAudioFormat = CommonCodecParamHelper.CreateCodecParams(AudioSampleFormat.Stereo(48000)),
                TextInput = "This is a test",
                TraceId = Guid.NewGuid().ToString("N"),
            };

            PooledBuffer<byte> serializedData = protocol.WriteClientRequest(inputRequest, logger);
            Assert.IsNotNull(serializedData);
            Assert.AreNotEqual(0, serializedData.Length);
            Console.WriteLine("Request compressed size is " + serializedData.Length);
            int dataLength = serializedData.Length;
            byte[] randomField = new byte[dataLength + 100];
            ArrayExtensions.MemCopy(serializedData.Buffer, 0, randomField, 100, serializedData.Length);
            DialogRequest parsedRequest = protocol.ParseClientRequest(serializedData, logger);
            BufferPool<byte>.Shred();
            Assert.IsTrue(DeepEquals(inputRequest, parsedRequest));
            parsedRequest = protocol.ParseClientRequest(new ArraySegment<byte>(randomField, 100, dataLength), logger);
            Assert.IsTrue(DeepEquals(inputRequest, parsedRequest));

            DialogResponse inputResponse = new DialogResponse()
            {
                AugmentedFinalQuery = "augmented stuff",
                ContinueImmediately = true,
                ConversationLifetimeSeconds = 240,
                CustomAudioOrdering = AudioOrdering.BeforeSpeech,
                ExecutedPlugin = new PluginStrongName("SomePlugin", 2, 1),
                ExecutionResult = Result.Success,
                IsRetrying = false,
                ResponseAudio = new AudioData()
                {
                    Codec = "opus",
                    CodecParams = "framesize=20",
                    Data = new ArraySegment<byte>(RandomByteArray())
                },
                ResponseData = new Dictionary<string, string>()
                {
                    { "Dark", "Yes" },
                    { "Sunglasses", "On" }
                },
                ResponseAction = "Response action",
                ResponseHtml = "Response html",
                ResponseSsml = "Response SSML",
                ResponseText = "Response text",
                ResponseUrl = "http://response.audio",
                SelectedRecoResult = DialogTestHelpers.GetSimpleRecoResult("common", "side_speech", 0.94f),
                StreamingAudioUrl = "http://streaming.audio",
                SuggestedQueries = new List<string>()
                {
                    "query 1", "query 2", "query 3"
                },
                TraceId = Guid.NewGuid().ToString("N"),
                TriggerKeywords = new List<TriggerKeyword>()
                {
                    new TriggerKeyword()
                    {
                        TriggerPhrase = "be quiet",
                        AllowBargeIn = true,
                        ExpireTimeSeconds = 30
                    }
                },
                UrlScope = UrlScope.Local,
            };

            serializedData = protocol.WriteClientResponse(inputResponse, logger);
            Assert.IsNotNull(serializedData);
            Assert.AreNotEqual(0, serializedData.Length);
            Console.WriteLine("Response compressed size is " + serializedData.Length);
            dataLength = serializedData.Length;
            randomField = new byte[dataLength + 100];
            ArrayExtensions.MemCopy(serializedData.Buffer, 0, randomField, 100, serializedData.Length);
            DialogResponse parsedResponse = protocol.ParseClientResponse(serializedData, logger);
            BufferPool<byte>.Shred();
            Assert.IsTrue(DeepEquals(inputResponse, parsedResponse));
            parsedResponse = protocol.ParseClientResponse(new ArraySegment<byte>(randomField, 100, dataLength), logger);
            Assert.IsTrue(DeepEquals(inputResponse, parsedResponse));
        }

        [TestMethod]
        public void TestProtocolJsonHasNoByteOrderMarks()
        {
            IDialogTransportProtocol protocol = new DialogJsonTransportProtocol();
            DialogRequest inputRequest = new DialogRequest()
            {
                InteractionType = InputMethod.Spoken,
                PreferredAudioCodec = "opus",
                PreferredAudioFormat = CommonCodecParamHelper.CreateCodecParams(AudioSampleFormat.Stereo(48000)),
                TextInput = "This is a test",
                TraceId = Guid.NewGuid().ToString("N"),
            };

            using (PooledBuffer<byte> buf = protocol.WriteClientRequest(inputRequest, new ConsoleLogger()))
            {
                Assert.AreEqual(0x7B, buf.Buffer[0]);
                string jsonString = Encoding.UTF8.GetString(buf.Buffer, 0, buf.Length);
                DialogRequest parsedRequest = JsonConvert.DeserializeObject<DialogRequest>(jsonString);
                Assert.IsTrue(DeepEquals(inputRequest, parsedRequest));
            }
        }

        private static bool DeepEquals(object a, object b)
        {
            if (a == null || b == null)
            {
                return false;
            }

            JObject objA = JObject.FromObject(a);
            JObject objB = JObject.FromObject(b);
            return JToken.DeepEquals(objA, objB);
        }

        private static byte[] RandomByteArray()
        {
            byte[] returnVal = new byte[1024];
            new FastRandom().NextBytes(returnVal);
            return returnVal;
        }
    }
}
