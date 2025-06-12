using Bond;
using Bond.IO.Safe;
using Bond.Protocols;
using Durandal.Common.Dialog.Web;
using System;
using System.Collections.Generic;
using System.Text;
using BONDAPI = Durandal.Extensions.BondProtocol.API;
using CAPI = Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.IO;

namespace Durandal.Extensions.BondProtocol
{
    public class DialogBondTransportProtocol : IDialogTransportProtocol
    {
        private static readonly int EXPECTED_PROTOCOL_VERSION = new CAPI.DialogRequest().ProtocolVersion;


        public string ContentEncoding => string.Empty;
        public string MimeType => "application/bond; proto=compact-binary";
        public string ProtocolName => "bond";
        
        static DialogBondTransportProtocol()
        {
            BondConverter.PrecacheSerializers<BONDAPI.DialogResponse>();
            BondConverter.PrecacheSerializers<BONDAPI.DialogRequest>();
            BondConverter.PrecacheSerializers<BONDAPI.ClientRequest_v15>();
            BondConverter.PrecacheSerializers<BONDAPI.ClientRequest_v16>();
            BondConverter.PrecacheSerializers<BONDAPI.ClientRequest_v17>();
            BondConverter.PrecacheSerializers<BONDAPI.ProtocolVersionHeader>();
        }

        public CAPI.DialogRequest ParseClientRequest(PooledBuffer<byte> input, ILogger logger)
        {
            try
            {
                BONDAPI.ProtocolVersionHeader header;
                // Peek at the protocol version field first
                BondConverter.DeserializeBond(input, 0, input.Length, out header, logger);

                if (header.ProtocolVersion < EXPECTED_PROTOCOL_VERSION)
                {
                    // Incoming request is legacy.
                    logger.Log("Incoming request is using an older protocol version (" + header.ProtocolVersion + "), this may not work", LogLevel.Wrn);
                }
                else if (header.ProtocolVersion > EXPECTED_PROTOCOL_VERSION)
                {
                    // Incoming request is from the future
                    logger.Log("Incoming request is using a protocol version from the future (" + header.ProtocolVersion + "), this may not work", LogLevel.Wrn);
                }

                BONDAPI.DialogRequest bondParsedRequest;
                if (header.ProtocolVersion == 15)
                {
                    // Interpret a v15 client request
                    BONDAPI.ClientRequest_v15 v15Request;
                    if (!BondConverter.DeserializeBond(input, 0, input.Length, out v15Request, logger))
                    {
                        throw new Exception("Could not deserialize Bond dialog request. It is possible that the server and client are using mismatched protocol versions.");
                    }

                    BONDAPI.ClientRequest_v16 v16Request = Convertv15tov16Request(v15Request);
                    BONDAPI.ClientRequest_v17 v17Request = Convertv16tov17Request(v16Request);
                    bondParsedRequest = Convertv17tov18Request(v17Request);
                }
                else if (header.ProtocolVersion == 16)
                {
                    // Interpret a v16 client request
                    BONDAPI.ClientRequest_v16 v16Request;
                    if (!BondConverter.DeserializeBond(input, 0, input.Length, out v16Request, logger))
                    {
                        throw new Exception("Could not deserialize Bond dialog request. It is possible that the server and client are using mismatched protocol versions.");
                    }

                    BONDAPI.ClientRequest_v17 v17Request = Convertv16tov17Request(v16Request);
                    bondParsedRequest = Convertv17tov18Request(v17Request);
                }
                else if (header.ProtocolVersion == 17)
                {
                    // Interpret a v17 client request
                    BONDAPI.ClientRequest_v17 v17Request;
                    if (!BondConverter.DeserializeBond(input, 0, input.Length, out v17Request, logger))
                    {
                        throw new Exception("Could not deserialize Bond dialog request. It is possible that the server and client are using mismatched protocol versions.");
                    }

                    bondParsedRequest = Convertv17tov18Request(v17Request);
                }
                else if (!BondConverter.DeserializeBond(input, 0, input.Length, out bondParsedRequest, logger))
                {
                    throw new Exception("Could not deserialize Bond dialog request. It is possible that the server and client are using mismatched protocol versions.");
                }

                return BondTypeConverters.Convert(bondParsedRequest);
            }
            finally
            {
                input?.Dispose();
            }
        }

        public CAPI.DialogRequest ParseClientRequest(ArraySegment<byte> input, ILogger logger)
        {
            BONDAPI.ProtocolVersionHeader header;
            // Peek at the protocol version field first
            BondConverter.DeserializeBond(input.Array, input.Offset, input.Count, out header, logger);

            if (header.ProtocolVersion < EXPECTED_PROTOCOL_VERSION)
            {
                // Incoming request is legacy.
                logger.Log("Incoming request is using an older protocol version (" + header.ProtocolVersion + "), this may not work", LogLevel.Wrn);
            }
            else if (header.ProtocolVersion > EXPECTED_PROTOCOL_VERSION)
            {
                // Incoming request is from the future
                logger.Log("Incoming request is using a protocol version from the future (" + header.ProtocolVersion + "), this may not work", LogLevel.Wrn);
            }

            BONDAPI.DialogRequest bondParsedRequest;
            if (header.ProtocolVersion == 15)
            {
                // Interpret a v15 client request
                BONDAPI.ClientRequest_v15 v15Request;
                if (!BondConverter.DeserializeBond(input.Array, input.Offset, input.Count, out v15Request, logger))
                {
                    throw new Exception("Could not deserialize Bond dialog request. It is possible that the server and client are using mismatched protocol versions.");
                }

                BONDAPI.ClientRequest_v16 v16Request = Convertv15tov16Request(v15Request);
                BONDAPI.ClientRequest_v17 v17Request = Convertv16tov17Request(v16Request);
                bondParsedRequest = Convertv17tov18Request(v17Request);
            }
            else if (header.ProtocolVersion == 16)
            {
                // Interpret a v16 client request
                BONDAPI.ClientRequest_v16 v16Request;
                if (!BondConverter.DeserializeBond(input.Array, input.Offset, input.Count, out v16Request, logger))
                {
                    throw new Exception("Could not deserialize Bond dialog request. It is possible that the server and client are using mismatched protocol versions.");
                }

                BONDAPI.ClientRequest_v17 v17Request = Convertv16tov17Request(v16Request);
                bondParsedRequest = Convertv17tov18Request(v17Request);
            }
            else if (header.ProtocolVersion == 17)
            {
                // Interpret a v17 client request
                BONDAPI.ClientRequest_v17 v17Request;
                if (!BondConverter.DeserializeBond(input.Array, input.Offset, input.Count, out v17Request, logger))
                {
                    throw new Exception("Could not deserialize Bond dialog request. It is possible that the server and client are using mismatched protocol versions.");
                }
                    
                bondParsedRequest = Convertv17tov18Request(v17Request);
            }
            else if (!BondConverter.DeserializeBond(input.Array, input.Offset, input.Count, out bondParsedRequest, logger))
            {
                throw new Exception("Could not deserialize Bond dialog request. It is possible that the server and client are using mismatched protocol versions.");
            }

            return BondTypeConverters.Convert(bondParsedRequest);
        }

        public PooledBuffer<byte> WriteClientRequest(CAPI.DialogRequest input, ILogger logger)
        {
            BONDAPI.DialogRequest convertedInput = BondTypeConverters.Convert(input);
            return BondConverter.SerializeBondPooled<BONDAPI.DialogRequest>(convertedInput, logger);
        }

        public CAPI.DialogResponse ParseClientResponse(PooledBuffer<byte> input, ILogger logger)
        {
            try
            {
                BONDAPI.ProtocolVersionHeader header;
                // Peek at the protocol version field first
                BondConverter.DeserializeBond(input, 0, input.Length, out header, logger);

                if (header.ProtocolVersion < EXPECTED_PROTOCOL_VERSION)
                {
                    // Incoming request is legacy.
                    logger.Log("Response is using an older protocol version (" + header.ProtocolVersion + "), this may not work", LogLevel.Wrn);
                }
                else if (header.ProtocolVersion > EXPECTED_PROTOCOL_VERSION)
                {
                    // Incoming request is from the future
                    logger.Log("Response is using a protocol version from the future (" + header.ProtocolVersion + "), this may not work", LogLevel.Wrn);
                }

                BONDAPI.DialogResponse bondParsedRequest;
                if (!BondConverter.DeserializeBond(input, 0, input.Length, out bondParsedRequest, logger))
                {
                    throw new Exception("Could not deserialize Bond LU response. It is possible that the server and client are using mismatched protocol versions.");
                }

                return BondTypeConverters.Convert(bondParsedRequest);
            }
            finally
            {
                input?.Dispose();
            }
        }

        public CAPI.DialogResponse ParseClientResponse(ArraySegment<byte> input, ILogger logger)
        {
            BONDAPI.ProtocolVersionHeader header;
            // Peek at the protocol version field first
            BondConverter.DeserializeBond(input.Array, input.Offset, input.Count, out header, logger);

            if (header.ProtocolVersion < EXPECTED_PROTOCOL_VERSION)
            {
                // Incoming request is legacy.
                logger.Log("Response is using an older protocol version (" + header.ProtocolVersion + "), this may not work", LogLevel.Wrn);
            }
            else if (header.ProtocolVersion > EXPECTED_PROTOCOL_VERSION)
            {
                // Incoming request is from the future
                logger.Log("Response is using a protocol version from the future (" + header.ProtocolVersion + "), this may not work", LogLevel.Wrn);
            }

            BONDAPI.DialogResponse bondParsedRequest;
            if (!BondConverter.DeserializeBond(input.Array, input.Offset, input.Count, out bondParsedRequest, logger))
            {
                throw new Exception("Could not deserialize Bond LU response. It is possible that the server and client are using mismatched protocol versions.");
            }

            return BondTypeConverters.Convert(bondParsedRequest);
        }

        public PooledBuffer<byte> WriteClientResponse(CAPI.DialogResponse input, ILogger logger)
        {
            BONDAPI.DialogResponse convertedInput = BondTypeConverters.Convert(input);
            return BondConverter.SerializeBondPooled<BONDAPI.DialogResponse>(convertedInput, logger);
        }

        private BONDAPI.ClientRequest_v16 Convertv15tov16Request(BONDAPI.ClientRequest_v15 v15Request)
        {
            BONDAPI.ClientRequest_v16 v16Request = new BONDAPI.ClientRequest_v16();
            v16Request.AuthTokens = v15Request.AuthTokens;
            v16Request.ClientAudioPlaybackTimeMs = v15Request.ClientAudioPlaybackTimeMs;
            v16Request.ClientContext = v15Request.ClientContext;
            v16Request.DomainScope = v15Request.DomainScope;
            v16Request.InputType = v15Request.InputType;
            v16Request.LanguageUnderstanding = null;
            if (v15Request.UnderstandingData != null &&
                v15Request.UnderstandingData.Count > 0)
            {
                v16Request.LanguageUnderstanding = new List<BONDAPI.RecognizedPhrase>();
                v16Request.LanguageUnderstanding.Add(new BONDAPI.RecognizedPhrase()
                {
                    Recognition = v15Request.UnderstandingData
                });
            }
            v16Request.PreferredAudioCodec = v15Request.PreferredAudioCodec;
            v16Request.Utterances = v15Request.Queries;
            v16Request.InputAudio = v15Request.QueryAudio;
            v16Request.RequestFlags = v15Request.RequestFlags;
            v16Request.TraceId = v15Request.TraceId;
            return v16Request;
        }

        private BONDAPI.ClientRequest_v17 Convertv16tov17Request(BONDAPI.ClientRequest_v16 v16Request)
        {
            BONDAPI.ClientRequest_v17 v17Request = new BONDAPI.ClientRequest_v17();

            v17Request.AuthTokens = v16Request.AuthTokens;
            v17Request.ClientAudioPlaybackTimeMs = v16Request.ClientAudioPlaybackTimeMs;
            v17Request.ClientContext = v16Request.ClientContext;
            v17Request.DomainScope = v16Request.DomainScope;
            v17Request.InputType = v16Request.InputType;
            v17Request.LanguageUnderstanding = v16Request.LanguageUnderstanding;
            v17Request.PreferredAudioCodec = v16Request.PreferredAudioCodec;
            if (v16Request.InputType == BONDAPI.InputMethod.Spoken)
            {
                v17Request.SpeechInput = new BONDAPI.SpeechRecognitionResult()
                {
                    RecognitionStatus = BONDAPI.SpeechRecognitionStatus.Success,
                    RecognizedPhrases = new List<BONDAPI.SpeechRecognizedPhrase>()
                };

                foreach (var hyp in v16Request.Utterances)
                {
                    v17Request.SpeechInput.RecognizedPhrases.Add(new BONDAPI.SpeechRecognizedPhrase()
                    {
                        DisplayText = hyp.Utterance,
                        LexicalForm = hyp.LexicalForm,
                        SREngineConfidence = hyp.Confidence,
                        InverseTextNormalizationResults = new List<string>(new string[] { hyp.Utterance })
                    });
                }
            }
            else if (v16Request.Utterances.Count > 0)
            {
                v17Request.TextInput = v16Request.Utterances[0].Utterance;
            }

            v17Request.InputAudio = v16Request.InputAudio;
            v17Request.RequestFlags = v16Request.RequestFlags;
            v17Request.TraceId = v16Request.TraceId;

            return v17Request;
        }

        private BONDAPI.DialogRequest Convertv17tov18Request(BONDAPI.ClientRequest_v17 v17Request)
        {
            BONDAPI.DialogRequest v18Request = new BONDAPI.DialogRequest();

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
            
            v18Request.EntityContext = new ArraySegment<byte>();
            v18Request.EntityInput = new List<BONDAPI.EntityReference>();

            return v18Request;
        }
    }
}
