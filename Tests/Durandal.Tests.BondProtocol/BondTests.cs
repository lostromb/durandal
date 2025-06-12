using Bond;
using Bond.IO.Safe;
using Bond.Protocols;
using Durandal.Extensions.BondProtocol;
using Durandal.Extensions.BondProtocol.API;
using Durandal.Common.Compression;
using Durandal.Common.Compression.LZ4;
using Durandal.Common.Dialog.Web;
using Durandal.Common.File;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Test;
using Durandal.Common.Utils;
using Durandal.Common.IO;
using Durandal.Common.MathExt;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Durandal.Extensions.BondProtocol.Remoting;
using Durandal.Common.NLP.Language;
using Durandal.Common.Collections;

namespace Durandal.Tests.BondProtocol
{
    [TestClass]
    public class BondTests
    {
        [TestMethod]
        public void TestBondBasicSerialize()
        {
            ILogger logger = new ConsoleLogger();
            AudioData toSerialize = new AudioData()
            {
                Codec = "alaw",
                CodecParams = "params",
                Data = new ArraySegment<byte>(new byte[100])
            };
            
            byte[] output = BondConverter.SerializeBond<AudioData>(toSerialize, logger);
            Assert.IsNotNull(output);
            Assert.AreEqual(118, output.Length);
        }

        [TestMethod]
        public void TestBondBasicSerializeNullArray()
        {
            ILogger logger = new ConsoleLogger();
            AudioData toSerialize = new AudioData()
            {
                Codec = "alaw",
                CodecParams = "params",
                Data = new ArraySegment<byte>()
            };

            byte[] output = BondConverter.SerializeBond<AudioData>(toSerialize, logger);
            Assert.IsNotNull(output);
            Assert.AreEqual(18, output.Length);
        }

        [TestMethod]
        public void TestBondBasicSerializeVarInt64()
        {
            ILogger logger = new ConsoleLogger();
            RemoteFileStat toSerialize = new RemoteFileStat()
            {
                CreationTime = long.MaxValue,
                Exists = true,
                IsDirectory = false,
                LastAccessTime = 10000000000L,
                LastWriteTime = long.MinValue,
                Size = 10
            };

            byte[] output = BondConverter.SerializeBond<RemoteFileStat>(toSerialize, logger);
            Assert.IsNotNull(output);
            Assert.AreEqual(42, output.Length);
        }

        [TestMethod]
        public void TestBondBasicSerializeDeserialize()
        {
            ILogger logger = new ConsoleLogger();
            AudioData toSerialize = new AudioData()
            {
                Codec = "alaw",
                CodecParams = "params",
                Data = new ArraySegment<byte>(new byte[100])
            };
            
            byte[] output = BondConverter.SerializeBond<AudioData>(toSerialize, logger);
            Assert.IsNotNull(output);
            Assert.AreEqual(118, output.Length);

            AudioData deserialized;
            Assert.IsTrue(BondConverter.DeserializeBond<AudioData>(output, 0, output.Length, out deserialized, logger));
            Assert.AreEqual(toSerialize.Codec, deserialized.Codec);
            Assert.AreEqual(toSerialize.CodecParams, deserialized.CodecParams);
        }

        [TestMethod]
        public void TestBondSerializerThreadSafety()
        {
            IRandom rand = new FastRandom();
            byte[] randomBytes = new byte[100];
            rand.NextBytes(randomBytes);
            ILogger logger = new ConsoleLogger();
            AudioData toSerialize = new AudioData()
            {
                Codec = "alaw",
                CodecParams = "params",
                Data = new ArraySegment<byte>(randomBytes)
            };
            
            byte[] gold = BondConverter.SerializeBond<AudioData>(toSerialize, logger);
            Assert.IsNotNull(gold);
            Assert.AreEqual(118, gold.Length);

            Task<byte[]>[] subTasks = new Task<byte[]>[10000];
            for (int c = 0; c < subTasks.Length; c++)
            {
                subTasks[c] = new Task<byte[]>(() =>
                    {
                        return BondConverter.SerializeBond<AudioData>(toSerialize, logger);
                    });
            }

            foreach (var subTask in subTasks)
            {
                subTask.Start();
            }

            Task.WaitAll(subTasks);

            foreach (var subTask in subTasks)
            {
                Assert.AreEqual(gold.Length, subTask.Result.Length);
                for (int c = 0; c < gold.Length; c++)
                {
                    Assert.AreEqual(gold[c], subTask.Result[c]);
                }
            }
        }

        [TestMethod]
        public void TestBondDialogProtocol()
        {
            IDialogTransportProtocol protocol = new DialogBondTransportProtocol();
            ILogger logger = new ConsoleLogger();

            Durandal.API.DialogRequest request = new Durandal.API.DialogRequest()
            {
                AuthTokens = new List<Durandal.API.SecurityToken>()
                {
                    new Durandal.API.SecurityToken()
                    {
                        Blue = "blue",
                        Red = "red",
                        Scope = Durandal.API.ClientAuthenticationScope.Client
                    }
                },
                ClientContext = new Durandal.API.ClientContext()
                {
                    Capabilities = Durandal.API.ClientCapabilities.DisplayBasicText | Durandal.API.ClientCapabilities.CanSynthesizeSpeech,
                    ClientId = "clientId",
                    ClientName = "clientName",
                    UserId = "userId",
                    UTCOffset = -420,
                    Locale = LanguageCode.EN_US,
                },
                RequestFlags = Durandal.API.QueryFlags.Debug,
                SpeechInput = new Durandal.API.SpeechRecognitionResult
                {
                    RecognizedPhrases = new List<Durandal.API.SpeechRecognizedPhrase>()
                    {
                        new Durandal.API.SpeechRecognizedPhrase()
                        {
                            SREngineConfidence = 1.0f,
                            DisplayText = "This is a test",
                            IPASyllables = "This is a test",
                        }
                    }
                }
            };

            PooledBuffer<byte> payload = protocol.WriteClientRequest(request, logger);
            Durandal.API.DialogRequest parsedRequest = protocol.ParseClientRequest(payload.AsArraySegment, logger);
            PooledBuffer<byte> payload2 = protocol.WriteClientRequest(parsedRequest, logger);
            Assert.IsTrue(ArrayExtensions.ArrayEquals(payload, payload2));

            Durandal.API.DialogResponse response = new Durandal.API.DialogResponse()
            {
                AugmentedFinalQuery = "This is a test",
                ContinueImmediately = false,
                ConversationLifetimeSeconds = 60,
                ExecutionResult = Durandal.API.Result.Success,
                ResponseText = "Apparently, it works",
                ResponseAudio = new Durandal.API.AudioData()
                {
                    Codec = "opus",
                    CodecParams = "16000",
                    Data = new ArraySegment<byte>(new byte[100])
                },
                SuggestedQueries = new List<string>() { "one", "two", "three" },
                TraceId = CommonInstrumentation.FormatTraceId(Guid.NewGuid()),
                UrlScope = Durandal.API.UrlScope.Local,
                ResponseHtml = "<html></html>"
            };

            payload = protocol.WriteClientResponse(response, logger);
            Durandal.API.DialogResponse parsedResponse = protocol.ParseClientResponse(payload.AsArraySegment, logger);
            payload2 = protocol.WriteClientResponse(parsedResponse, logger);
            Assert.IsTrue(ArrayExtensions.ArrayEquals(payload, payload2));
        }

        [TestMethod]
        public void TestBondSerializeInstrumentationBondBlob()
        {
            IByteConverter<Durandal.API.InstrumentationEventList> serializer = new BondByteConverterInstrumentationEventList();

            Durandal.API.InstrumentationEventList orig = new Durandal.API.InstrumentationEventList()
            {
                Events = new List<Durandal.API.InstrumentationEvent>()
            };

            orig.Events.Add(new Durandal.API.InstrumentationEvent()
            {
                Component = "Component 1",
                Level = 3,
                Message = "Log 1",
                Timestamp = 1234L,
                TraceId = "Trace 1"
            });

            orig.Events.Add(new Durandal.API.InstrumentationEvent()
            {
                Component = "Component 2",
                Level = 5,
                Message = "Log 2",
                Timestamp = 5678L,
                TraceId = "Trace 2"
            });

            byte[] encoded = serializer.Encode(orig);
            Durandal.API.InstrumentationEventList remade = serializer.Decode(encoded, 0, encoded.Length);

            Assert.AreEqual(2, remade.Events.Count);
            Assert.AreEqual(orig.Events[0].Component, remade.Events[0].Component);
            Assert.AreEqual(orig.Events[0].Level, remade.Events[0].Level);
            Assert.AreEqual(orig.Events[0].Message, remade.Events[0].Message);
            Assert.AreEqual(orig.Events[0].Timestamp, remade.Events[0].Timestamp);
            Assert.AreEqual(orig.Events[0].TraceId, remade.Events[0].TraceId);

            Assert.AreEqual(orig.Events[1].Component, remade.Events[1].Component);
            Assert.AreEqual(orig.Events[1].Level, remade.Events[1].Level);
            Assert.AreEqual(orig.Events[1].Message, remade.Events[1].Message);
            Assert.AreEqual(orig.Events[1].Timestamp, remade.Events[1].Timestamp);
            Assert.AreEqual(orig.Events[1].TraceId, remade.Events[1].TraceId);
        }

        [TestMethod]
        public void TestBondSerializeInstrumentationBlob()
        {
            IByteConverter<Durandal.API.InstrumentationEventList> serializer = new InstrumentationBlobSerializer();

            Durandal.API.InstrumentationEventList orig = new Durandal.API.InstrumentationEventList()
            {
                Events = new List<Durandal.API.InstrumentationEvent>()
            };

            orig.Events.Add(new Durandal.API.InstrumentationEvent()
            {
                Component = "Component 1",
                Level = 3,
                Message = "Log 1",
                Timestamp = 1234L,
                TraceId = "Trace 1"
            });

            orig.Events.Add(new Durandal.API.InstrumentationEvent()
            {
                Component = "Component 2",
                Level = 5,
                Message = "Log 2",
                Timestamp = 5678L,
                TraceId = "Trace 2"
            });

            byte[] encoded = serializer.Encode(orig);
            Durandal.API.InstrumentationEventList remade = serializer.Decode(encoded, 0, encoded.Length);

            Assert.AreEqual(2, remade.Events.Count);
            Assert.AreEqual(orig.Events[0].Component, remade.Events[0].Component);
            Assert.AreEqual(orig.Events[0].Level, remade.Events[0].Level);
            Assert.AreEqual(orig.Events[0].Message, remade.Events[0].Message);
            Assert.AreEqual(orig.Events[0].Timestamp, remade.Events[0].Timestamp);
            Assert.AreEqual(orig.Events[0].TraceId, remade.Events[0].TraceId);

            Assert.AreEqual(orig.Events[1].Component, remade.Events[1].Component);
            Assert.AreEqual(orig.Events[1].Level, remade.Events[1].Level);
            Assert.AreEqual(orig.Events[1].Message, remade.Events[1].Message);
            Assert.AreEqual(orig.Events[1].Timestamp, remade.Events[1].Timestamp);
            Assert.AreEqual(orig.Events[1].TraceId, remade.Events[1].TraceId);
        }

        [TestMethod]
        public void TestBondSerializeLZ4InstrumentationBlob()
        {
            IByteConverter<Durandal.API.InstrumentationEventList> serializer = new LZ4ByteConverter<Durandal.API.InstrumentationEventList>(new InstrumentationBlobSerializer());

            Durandal.API.InstrumentationEventList orig = new Durandal.API.InstrumentationEventList()
            {
                Events = new List<Durandal.API.InstrumentationEvent>()
            };

            orig.Events.Add(new Durandal.API.InstrumentationEvent()
            {
                Component = "Component 1",
                Level = 3,
                Message = "Log 1",
                Timestamp = 1234L,
                TraceId = "Trace 1"
            });

            orig.Events.Add(new Durandal.API.InstrumentationEvent()
            {
                Component = "Component 2",
                Level = 5,
                Message = "Log 2",
                Timestamp = 5678L,
                TraceId = "Trace 2"
            });

            byte[] encoded = serializer.Encode(orig);
            Durandal.API.InstrumentationEventList remade = serializer.Decode(encoded, 0, encoded.Length);

            Assert.AreEqual(2, remade.Events.Count);
            Assert.AreEqual(orig.Events[0].Component, remade.Events[0].Component);
            Assert.AreEqual(orig.Events[0].Level, remade.Events[0].Level);
            Assert.AreEqual(orig.Events[0].Message, remade.Events[0].Message);
            Assert.AreEqual(orig.Events[0].Timestamp, remade.Events[0].Timestamp);
            Assert.AreEqual(orig.Events[0].TraceId, remade.Events[0].TraceId);

            Assert.AreEqual(orig.Events[1].Component, remade.Events[1].Component);
            Assert.AreEqual(orig.Events[1].Level, remade.Events[1].Level);
            Assert.AreEqual(orig.Events[1].Message, remade.Events[1].Message);
            Assert.AreEqual(orig.Events[1].Timestamp, remade.Events[1].Timestamp);
            Assert.AreEqual(orig.Events[1].TraceId, remade.Events[1].TraceId);
        }

        [TestMethod]
        public void TestBondSerializeRequestScenario()
        {
            ILogger logger = new ConsoleLogger();
            Durandal.API.DialogRequest req = new Durandal.API.DialogRequest()
            {
                ClientContext = new Durandal.API.ClientContext()
                {
                    ClientId = "44",
                    Capabilities = Durandal.API.ClientCapabilities.DisplayHtml5,
                    Locale = LanguageCode.EN_US,
                    UserId = "22",
                    ClientName = "Sword of Mercy",
                    ReferenceDateTime = "2018-11-08T17:26:06",
                    UTCOffset = -480,
                    Latitude = 47,
                    Longitude = -122,
                    ExtraClientContext = new Dictionary<string, string>()
                    {
                        { "FormFactor", "Portable" }
                    },
                    SupportedClientActions = new HashSet<string>()
                    {
                        "StopListening"
                    },
                    UserTimeZone = null,
                    LocationAccuracy = 7
                },
                InteractionType = Durandal.API.InputMethod.Spoken,
                TextInput = null,
                SpeechInput = new Durandal.API.SpeechRecognitionResult()
                {
                    RecognitionStatus = Durandal.API.SpeechRecognitionStatus.Success,
                    RecognizedPhrases = new List<Durandal.API.SpeechRecognizedPhrase>()
                    {
                        new Durandal.API.SpeechRecognizedPhrase()
                        {
                            DisplayText = "Hello.",
                            IPASyllables = "hello",
                            SREngineConfidence = 1.0f,
                            PhraseElements = new List<Durandal.API.SpeechPhraseElement>()
                            {
                                new Durandal.API.SpeechPhraseElement()
                                {
                                    SREngineConfidence = 0.9594f,
                                    IPASyllables = "hello",
                                    DisplayText = "hello",
                                    Pronunciation = null,
                                    AudioTimeLength = TimeSpan.Zero,
                                    AudioTimeOffset = TimeSpan.Zero
                                }
                            },
                            Locale = "en-US",
                            AudioTimeOffset = TimeSpan.Zero,
                            AudioTimeLength = TimeSpan.FromMilliseconds(570),
                            InverseTextNormalizationResults = new List<string>()
                            {
                                "hello"
                            },
                            ProfanityTags = null,
                            MaskedInverseTextNormalizationResults = new List<string>()
                            {
                                "hello"
                            }
                        }
                    },
                    ConfusionNetworkData = null
                },
                AuthTokens = new List<Durandal.API.SecurityToken>()
                {
                    new Durandal.API.SecurityToken()
                    {
                        Red = "red",
                        Blue = "blue",
                        Scope = Durandal.API.ClientAuthenticationScope.User
                    }
                },
                AudioInput = null,
                LanguageUnderstanding = null,
                PreferredAudioCodec = "opus",
                TraceId = "trace",
                DomainScope = null,
                ClientAudioPlaybackTimeMs = null,
                RequestFlags = Durandal.API.QueryFlags.Debug,
                EntityContext = new ArraySegment<byte>(),
                EntityInput = null
            };

            DialogBondTransportProtocol prot = new DialogBondTransportProtocol();
            PooledBuffer<byte> serialized = prot.WriteClientRequest(req, logger);
            Assert.IsNotNull(serialized.Buffer);
            Assert.AreNotEqual(0, serialized.Length);
        }

        [TestMethod]
        public void TestBondDiagnoseNullReferencesGood()
        {
            Durandal.API.QueryWithContext qc = new Durandal.API.QueryWithContext()
            {
                ClientContext = DialogTestHelpers.GetTestClientContextTextQuery(),
                Understanding = DialogTestHelpers.GetSimpleRecoResult("my_domain", "my_intent", 1.0f, "hello"),
            };

            var converted = BondTypeConverters.Convert(qc);

            Assert.IsTrue(string.IsNullOrEmpty(BondConverter.WhichFieldIsThrowingNullExceptions(converted)));
        }

        [TestMethod]
        public void TestBondDiagnoseNullReferencesError()
        {
            Durandal.API.QueryWithContext qc = new Durandal.API.QueryWithContext()
            {
                ClientContext = DialogTestHelpers.GetTestClientContextTextQuery(),
                Understanding = DialogTestHelpers.GetSimpleRecoResult("my_domain", "my_intent", 1.0f, "hello"),
            };

            var converted = BondTypeConverters.Convert(qc);
            converted.ClientContext.UserId = null;

            Assert.AreEqual("ClientContext.UserId", BondConverter.WhichFieldIsThrowingNullExceptions(converted));
        }

        /// <summary>
        /// Tests that when we deserialize bond from a pooled byte buffer, the resulting object does
        /// not retain any pointers into the pooled buffer as that array is inherently volatile after it is reclaimed.
        /// </summary>
        [TestMethod]
        public void TestBondCanDeserializeBlobsFromPooledBuffers()
        {
            IRandom rand = new FastRandom();
            byte[] inputBuffer = new byte[100];
            rand.NextBytes(inputBuffer);
            ILogger logger = new ConsoleLogger();
            Durandal.API.CachedWebData capiData = new Durandal.API.CachedWebData()
            {
                Data = new ArraySegment<byte>(inputBuffer),
                LifetimeSeconds = 10,
                MimeType = "text/html",
            };

            Durandal.Extensions.BondProtocol.API.CachedWebData bondData = BondTypeConverters.Convert(capiData);
            PooledBuffer<byte> pooledOutBuffer = BondConverter.SerializeBondPooled(bondData, logger);

            // Deserialize from a pooled buffer
            Assert.IsTrue(BondConverter.DeserializeBond<Durandal.Extensions.BondProtocol.API.CachedWebData>(pooledOutBuffer, 0, pooledOutBuffer.Length, out bondData, logger));

            // Assert the data is correct initially
            Assert.IsTrue(ArrayExtensions.ArrayEquals(bondData.Data, new ArraySegment<byte>(inputBuffer)));

            // Simulate reclaiming the pooled buffer and writing other junk to it
            rand.NextBytes(pooledOutBuffer.Buffer);

            // The object data should not hold any references to the pooled buffer and its data should appear unmodified
            Assert.IsTrue(ArrayExtensions.ArrayEquals(bondData.Data, new ArraySegment<byte>(inputBuffer)));
        }

        /*[TestMethod]
        public void TestSerializeStream()
        {
            ILogger logger = new ConsoleLogger();
            ClientResponse toSerialize = new ClientResponse()
            {
                AugmentedFinalQuery = "test",
                CustomAudioOrdering = AudioOrdering.AfterSpeech,
                ExecutionResult = Result.Success,
                ProtocolVersion = 99,
                ContinueImmediately = true,
                AudioToPlay = new AudioData()
                    {
                        Codec = "alaw",
                        CodecParams = "params",
                        SampleRate = 16000,
                        Data = new ArraySegment<byte>(new byte[100])
                    }
            };

            MemoryStream outStream = new MemoryStream();
            Serializer<CompactBinaryWriter<Bond.IO.Unsafe.OutputStream>> ser = new Serializer<CompactBinaryWriter<Bond.IO.Unsafe.OutputStream>>(typeof(ClientResponse));
            Bond.IO.Unsafe.OutputStream outBuf = new Bond.IO.Unsafe.OutputStream(outStream);
            var writer = new CompactBinaryWriter<Bond.IO.Unsafe.OutputStream>(outBuf);
            ser.Serialize(toSerialize, writer);
            outStream.Close();

            byte[] output = outStream.ToArray();

            MemoryStream inStream = new MemoryStream(output);
            Deserializer<CompactBinaryReader<Bond.IO.Unsafe.InputStream>> dser = new Deserializer<CompactBinaryReader<Bond.IO.Unsafe.InputStream>>(typeof(ClientResponse));
            Bond.IO.Unsafe.InputStream inBuf = new Bond.IO.Unsafe.InputStream(inStream);
            var reader = new CompactBinaryReader<Bond.IO.Unsafe.InputStream>(inBuf);
            ClientResponse deserialized = dser.Deserialize<ClientResponse>(reader);
            inStream.Close();

            Assert.AreEqual(toSerialize.AugmentedFinalQuery, deserialized.AugmentedFinalQuery);
            Assert.AreEqual(toSerialize.CustomAudioOrdering, deserialized.CustomAudioOrdering);
            Assert.AreEqual(toSerialize.ExecutionResult, deserialized.ExecutionResult);
            Assert.AreEqual(toSerialize.ProtocolVersion, deserialized.ProtocolVersion);
            Assert.AreEqual(toSerialize.ContinueImmediately, deserialized.ContinueImmediately);
        }*/
    }
}
