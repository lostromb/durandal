using Durandal.API;
using Durandal.Common.Instrumentation;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Durandal.Common.Dialog.Web
{
    public class DialogJsonTransportProtocol : IDialogTransportProtocol
    {
        private static readonly int EXPECTED_PROTOCOL_VERSION = new DialogRequest().ProtocolVersion;

        private static readonly JsonSerializerSettings JSON_SERIALIZE_SETTINGS = new JsonSerializerSettings()
        {
            Formatting = Formatting.None,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            DateParseHandling = DateParseHandling.None,
            DateFormatString = "yyyy-MM-ddTHH:mm:ss",
            NullValueHandling = NullValueHandling.Ignore,
        };

        private static readonly JsonSerializer JSON_SERIALIZER = new JsonSerializer()
        {
            Formatting = Formatting.None,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            DateParseHandling = DateParseHandling.None,
            DateFormatString = "yyyy-MM-ddTHH:mm:ss",
            NullValueHandling = NullValueHandling.Ignore,
        };

        public string ContentEncoding => string.Empty;

        public string MimeType => "application/json";

        public string ProtocolName => "json";

        public DialogRequest ParseClientRequest(PooledBuffer<byte> input, ILogger logger)
        {
            using (input)
            {
                DialogRequest returnVal = ParseClientRequest(input.AsArraySegment, logger);
                return returnVal;
            }
        }

        private class ProtocolVersionHeader
        {
            [JsonProperty("ProtocolVersion")]
            public int? ProtocolVersion { get; set; }

            [JsonProperty("TraceId")]
            public string TraceId { get; set; }
        }

        public DialogRequest ParseClientRequest(ArraySegment<byte> input, ILogger logger)
        {
            try
            {
                ProtocolVersionHeader requestHeader;
                using (MemoryStream memStream = new MemoryStream(input.Array, input.Offset, input.Count, false))
                using (StreamReader streamReader = new StreamReader(memStream, StringUtils.UTF8_WITHOUT_BOM))
                using (JsonReader jsonReader = new JsonTextReader(streamReader))
                {
                    // Originally I used JObject.Load to read the header data, but it turns out
                    // that's wasteful because it creates copies of every field in the incoming
                    // request. So it's actually more performant to parse the full stream twice.
                    requestHeader = JSON_SERIALIZER.Deserialize<ProtocolVersionHeader>(jsonReader);
                }

                // Inspect the protocol version first
                if (requestHeader == null || !requestHeader.ProtocolVersion.HasValue)
                {
                    throw new FormatException("Incoming JSON request is null or has an invalid protocol version");
                }

                int incomingProtocolVersion = requestHeader.ProtocolVersion.Value;
                Guid? parsedIncomingTraceId = CommonInstrumentation.TryParseTraceIdGuid(requestHeader.TraceId);

                if (incomingProtocolVersion < EXPECTED_PROTOCOL_VERSION)
                {
                    logger.Log("Incoming JSON request is using an older protocol version (" + incomingProtocolVersion + "), this may not work", LogLevel.Wrn, parsedIncomingTraceId);
                }
                else if (incomingProtocolVersion > EXPECTED_PROTOCOL_VERSION)
                {
                    logger.Log("Incoming request is using a protocol version from the future (" + incomingProtocolVersion + "), this may not work", LogLevel.Wrn, parsedIncomingTraceId);
                }

                using (MemoryStream memStream = new MemoryStream(input.Array, input.Offset, input.Count, false))
                using (StreamReader streamReader = new StreamReader(memStream, StringUtils.UTF8_WITHOUT_BOM))
                using (JsonReader jsonReader = new JsonTextReader(streamReader))
                {
                    DialogRequest returnVal;

                    if (incomingProtocolVersion == 15)
                    {
                        ClientRequest_v15 v15Request = JSON_SERIALIZER.Deserialize<ClientRequest_v15 >(jsonReader);
                        ClientRequest_v16 v16Request = ConvertV15ToV16Request(v15Request);
                        ClientRequest_v17 v17Request = ConvertV16ToV17Request(v16Request);
                        returnVal = ConvertV17ToV18Request(v17Request);
                    }
                    else if (incomingProtocolVersion == 16)
                    {
                        ClientRequest_v16 v16Request = JSON_SERIALIZER.Deserialize<ClientRequest_v16>(jsonReader);
                        ClientRequest_v17 v17Request = ConvertV16ToV17Request(v16Request);
                        returnVal = ConvertV17ToV18Request(v17Request);
                    }
                    else if (incomingProtocolVersion == 17)
                    {
                        ClientRequest_v17 v17Request = JSON_SERIALIZER.Deserialize<ClientRequest_v17>(jsonReader);
                        returnVal = ConvertV17ToV18Request(v17Request);
                    }
                    else
                    {
                        returnVal = JSON_SERIALIZER.Deserialize<DialogRequest>(jsonReader);
                    }

                    // Check required fields
                    if (returnVal.ClientContext == null)
                    {
                        throw new FormatException("Missing ClientContext");
                    }
                    if (string.IsNullOrEmpty(returnVal.ClientContext.ClientId))
                    {
                        throw new FormatException("Missing ClientContext.ClientId");
                    }
                    if (string.IsNullOrEmpty(returnVal.ClientContext.UserId))
                    {
                        throw new FormatException("Missing ClientContext.UserId");
                    }
                    if (returnVal.ClientContext.Capabilities == 0)
                    {
                        throw new FormatException("Missing ClientContext.Capabilities");
                    }
                    if (returnVal.ClientContext.Locale == null)
                    {
                        throw new FormatException("Missing ClientContext.Locale");
                    }

                    // Validate trace ID
                    Guid g;
                    if (string.IsNullOrEmpty(returnVal.TraceId) || !Guid.TryParse(returnVal.TraceId, out g))
                    {
                        returnVal.TraceId = CommonInstrumentation.FormatTraceId(Guid.NewGuid());
                    }

                    return returnVal;
                }
            }
            catch (JsonException e)
            {
                throw new FormatException(e.Message, e);
            }
        }

        public PooledBuffer<byte> WriteClientRequest(DialogRequest input, ILogger logger)
        {
            using (RecyclableMemoryStream memoryStream = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
            using (Utf8StreamWriter writer = new Utf8StreamWriter(memoryStream))
            using (JsonTextWriter jsonWriter = new JsonTextWriter(writer))
            {
                JSON_SERIALIZER.Serialize(jsonWriter, input);
                jsonWriter.Flush();
                return memoryStream.ToPooledBuffer();
            }
        }

        public DialogResponse ParseClientResponse(PooledBuffer<byte> input, ILogger logger)
        {
            DialogResponse returnVal = ParseClientResponse(input.AsArraySegment, logger);
            input.Dispose();
            return returnVal;
        }

        public DialogResponse ParseClientResponse(ArraySegment<byte> input, ILogger logger)
        {
            try
            {
                DialogResponse returnVal;
                using (MemoryStream memStream = new MemoryStream(input.Array, input.Offset, input.Count, false))
                using (StreamReader streamReader = new StreamReader(memStream, StringUtils.UTF8_WITHOUT_BOM))
                using (JsonReader jsonReader = new JsonTextReader(streamReader))
                {
                    returnVal = JSON_SERIALIZER.Deserialize<DialogResponse>(jsonReader);
                }
                
                if (returnVal.ProtocolVersion < EXPECTED_PROTOCOL_VERSION)
                {
                    logger.Log("Response is using an older protocol version (" + returnVal.ProtocolVersion + "), this may not work", LogLevel.Wrn, CommonInstrumentation.TryParseTraceIdGuid(returnVal.TraceId));
                }
                else if (returnVal.ProtocolVersion > EXPECTED_PROTOCOL_VERSION)
                {
                    logger.Log("Response is using a protocol version from the future (" + returnVal.ProtocolVersion + "), this may not work", LogLevel.Wrn, CommonInstrumentation.TryParseTraceIdGuid(returnVal.TraceId));
                }

                return returnVal;
            }
            catch (JsonException e)
            {
                throw new FormatException(e.Message, e);
            }
        }

        public PooledBuffer<byte> WriteClientResponse(DialogResponse input, ILogger logger)
        {
            using (RecyclableMemoryStream memoryStream = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
            using (Utf8StreamWriter writer = new Utf8StreamWriter(memoryStream))
            using (JsonTextWriter jsonWriter = new JsonTextWriter(writer))
            {
                JSON_SERIALIZER.Serialize(jsonWriter, input);
                jsonWriter.Flush();
                return memoryStream.ToPooledBuffer();
            }
        }

        private ClientRequest_v16 ConvertV15ToV16Request(ClientRequest_v15 v15Request)
        {
            ClientRequest_v16 returnVal = new ClientRequest_v16();
            returnVal.AuthTokens = v15Request.AuthTokens;
            returnVal.ClientAudioPlaybackTimeMs = v15Request.ClientAudioPlaybackTimeMs;
            returnVal.ClientContext = v15Request.ClientContext;
            returnVal.DomainScope = v15Request.DomainScope;
            returnVal.InputType = v15Request.InputType;
            returnVal.LanguageUnderstanding = null;
            if (v15Request.UnderstandingData != null &&
                v15Request.UnderstandingData.Count > 0)
            {
                returnVal.LanguageUnderstanding = new List<RecognizedPhrase>();
                returnVal.LanguageUnderstanding.Add(new RecognizedPhrase()
                {
                    Recognition = v15Request.UnderstandingData
                });
            }
            returnVal.PreferredAudioCodec = v15Request.PreferredAudioCodec;
            returnVal.Utterances = v15Request.Queries;
            returnVal.InputAudio = v15Request.QueryAudio;
            returnVal.RequestFlags = v15Request.RequestFlags;
            returnVal.TraceId = v15Request.TraceId;
            return returnVal;
        }

        private ClientRequest_v17 ConvertV16ToV17Request(ClientRequest_v16 v16Request)
        {
            ClientRequest_v17 returnVal = new ClientRequest_v17();
            returnVal.AuthTokens = v16Request.AuthTokens;
            returnVal.ClientAudioPlaybackTimeMs = v16Request.ClientAudioPlaybackTimeMs;
            returnVal.ClientContext = v16Request.ClientContext;
            returnVal.DomainScope = v16Request.DomainScope;
            returnVal.InputType = v16Request.InputType;
            returnVal.LanguageUnderstanding = v16Request.LanguageUnderstanding;
            returnVal.PreferredAudioCodec = v16Request.PreferredAudioCodec;
            if (v16Request.InputType == InputMethod.Spoken)
            {
                returnVal.SpeechInput = new SpeechRecognitionResult()
                {
                    RecognitionStatus = SpeechRecognitionStatus.Success,
                    RecognizedPhrases = new List<SpeechRecognizedPhrase>()
                };

                foreach (var hyp in v16Request.Utterances)
                {
                    returnVal.SpeechInput.RecognizedPhrases.Add(new SpeechRecognizedPhrase()
                    {
                        DisplayText = hyp.Utterance,
                        IPASyllables = hyp.LexicalForm,
                        SREngineConfidence = hyp.Confidence,
                        InverseTextNormalizationResults = new List<string>(new string[] { hyp.Utterance })
                    });
                }
            }
            else if (v16Request.Utterances.Count > 0)
            {
                returnVal.TextInput = v16Request.Utterances[0].Utterance;
            }

            returnVal.InputAudio = v16Request.InputAudio;
            returnVal.RequestFlags = v16Request.RequestFlags;
            returnVal.TraceId = v16Request.TraceId;
            return returnVal;
        }

        private DialogRequest ConvertV17ToV18Request(ClientRequest_v17 v17Request)
        {
            DialogRequest v18Request = new DialogRequest();

            v18Request.AuthTokens = v17Request.AuthTokens;
            v18Request.ClientAudioPlaybackTimeMs = v17Request.ClientAudioPlaybackTimeMs;
            v18Request.ClientContext = v17Request.ClientContext;
            v18Request.DomainScope = v17Request.DomainScope;
            v18Request.AudioInput = v17Request.InputAudio;
            v18Request.InteractionType = v17Request.InputType;
            v18Request.LanguageUnderstanding = v17Request.LanguageUnderstanding;
            v18Request.PreferredAudioCodec = v17Request.PreferredAudioCodec;
            v18Request.RequestFlags = v17Request.RequestFlags;
            v18Request.SpeechInput = v17Request.SpeechInput;
            v18Request.TextInput = v17Request.TextInput;
            v18Request.TraceId = v17Request.TraceId;

            v18Request.EntityContext = new ArraySegment<byte>(BinaryHelpers.EMPTY_BYTE_ARRAY);
            v18Request.EntityInput = new List<EntityReference>();

            return v18Request;
        }
    }
}
