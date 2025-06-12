using Durandal.Common.Dialog;
using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Text;
using Durandal.API;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Durandal.Common.Utils;
using Durandal.Common.Logger;
using Durandal.Common.Dialog.Runtime;
using Durandal.Common.File;
using Durandal.Common.IO.Json;
using Durandal.Common.IO;
using System.IO;

namespace Durandal.Common.Remoting.Protocol
{
    public class JsonRemoteDialogProtocol : IRemoteDialogProtocol
    {
        public const uint PROTOCOL_ID = 1;
        public uint ProtocolId => PROTOCOL_ID;

        private static readonly JsonSerializerSettings JSON_SERIALIZE_SETTINGS = new JsonSerializerSettings()
        {
            Formatting = Formatting.None,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            DateParseHandling = DateParseHandling.None,
            DateFormatString = "yyyy-MM-ddTHH:mm:ss.fffff",
            NullValueHandling = NullValueHandling.Ignore,
        };

        private static readonly JsonSerializer JSON_SERIALIZER = new JsonSerializer()
        {
            Formatting = Formatting.None,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            DateParseHandling = DateParseHandling.None,
            DateFormatString = "yyyy-MM-ddTHH:mm:ss.fffff",
            NullValueHandling = NullValueHandling.Ignore,
        };

        static JsonRemoteDialogProtocol()
        {
            JSON_SERIALIZE_SETTINGS.Converters.Add(new JsonByteArrayConverter());
            JSON_SERIALIZER.Converters.Add(new JsonByteArrayConverter());
        }

        public PooledBuffer<byte> Serialize(KeepAliveRequest data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteProcedureResponse<DialogProcessingResponse> data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteProcedureResponse<LoadedPluginInformation> data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteProcedureResponse<bool> data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteProcedureResponse<long> data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteLogMessageRequest data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteLoadPluginRequest data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteUnloadPluginRequest data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteExecutePluginRequest data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteGetAvailablePluginsRequest data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteProcedureResponse<List<PluginStrongName>> data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteTriggerPluginRequest data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteProcedureResponse<TriggerProcessingResponse> data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }
        
        public PooledBuffer<byte> Serialize(RemoteCrossDomainRequestRequest data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteProcedureResponse<CrossDomainRequestData> data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteCrossDomainResponseRequest data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteProcedureResponse<CrossDomainResponseResponse> data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }
        
        public PooledBuffer<byte> Serialize(RemoteSynthesizeSpeechRequest data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteProcedureResponse<SynthesizedSpeech> data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteRecognizeSpeechRequest data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteProcedureResponse<SpeechRecognitionResult> data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteGetOAuthTokenRequest data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteProcedureResponse<string> data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteDeleteOAuthTokenRequest data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteCreateOAuthUriRequest data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteProcedureResponse<OAuthToken> data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteFetchPluginViewDataRequest data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteProcedureResponse<CachedWebData> data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteResolveEntityRequest data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteProcedureResponse<RemoteResolveEntityResponse> data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteFileStatRequest data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteFileWriteStatRequest data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteFileListRequest data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteFileReadContentsRequest data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteProcedureResponse<ArraySegment<byte>> data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteProcedureResponse<RemoteFileStat> data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteProcedureResponse<RemoteFileStreamOpenResult> data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteProcedureResponse<List<string>> data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteFileMoveRequest data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteFileCreateDirectoryRequest data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteFileDeleteRequest data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteFileWriteContentsRequest data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteFileStreamOpenRequest data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteFileStreamReadRequest data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteFileStreamWriteRequest data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteFileStreamCloseRequest data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteFileStreamSeekRequest data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteFileStreamSetLengthRequest data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteHttpRequest data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteUploadMetricsRequest data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public PooledBuffer<byte> Serialize(RemoteCrashContainerRequest data, ILogger queryLogger)
        {
            return SerializeInternal(data, queryLogger);
        }

        public Tuple<object, Type> Parse(PooledBuffer<byte> data, ILogger queryLogger)
        {
            try
            {
                JObject parsedObj;
                using (MemoryStream memoryStream = new MemoryStream(data.Buffer, 0, data.Length, false))
                using (StreamReader reader = new StreamReader(memoryStream, StringUtils.UTF8_WITHOUT_BOM))
                using (JsonTextReader jsonReader = new JsonTextReader(reader))
                {
                    parsedObj = JToken.ReadFrom(jsonReader) as JObject;
                }

                if (parsedObj == null)
                {
                    queryLogger.Log("Can't parse json remoting message: Invalid JSON object", LogLevel.Err);
                    LogJsonErrorPayload(data, queryLogger);
                    return null;
                }

                if (parsedObj["MessageType"] == null ||
                    parsedObj["MethodName"] == null)
                {
                    // No idea what this is...
                    queryLogger.Log("Can't parse json remoting message: No MessageType header", LogLevel.Err);
                    LogJsonErrorPayload(data, queryLogger);
                    return null;
                }

                RemoteMessageType messageType = (RemoteMessageType)parsedObj["MessageType"].Value<int>();
                string methodName = parsedObj["MethodName"].Value<string>();

                if (messageType == RemoteProcedureRequest.REQUEST_MESSAGE_TYPE)
                {
                    // It's a request of some kind
                    if (string.Equals(methodName, KeepAliveRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        KeepAliveRequest returnVal = parsedObj.ToObject<KeepAliveRequest>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteExecutePluginRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteExecutePluginRequest returnVal = parsedObj.ToObject<RemoteExecutePluginRequest>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteLoadPluginRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteLoadPluginRequest returnVal = parsedObj.ToObject<RemoteLoadPluginRequest>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteUnloadPluginRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteUnloadPluginRequest returnVal = parsedObj.ToObject<RemoteUnloadPluginRequest>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteGetAvailablePluginsRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteGetAvailablePluginsRequest returnVal = parsedObj.ToObject<RemoteGetAvailablePluginsRequest>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteTriggerPluginRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteTriggerPluginRequest returnVal = parsedObj.ToObject<RemoteTriggerPluginRequest>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteCrossDomainRequestRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteCrossDomainRequestRequest returnVal = parsedObj.ToObject<RemoteCrossDomainRequestRequest>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteCrossDomainResponseRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteCrossDomainResponseRequest returnVal = parsedObj.ToObject<RemoteCrossDomainResponseRequest>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteLogMessageRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteLogMessageRequest returnVal = parsedObj.ToObject<RemoteLogMessageRequest>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteSynthesizeSpeechRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteSynthesizeSpeechRequest returnVal = parsedObj.ToObject<RemoteSynthesizeSpeechRequest>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteRecognizeSpeechRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteRecognizeSpeechRequest returnVal = parsedObj.ToObject<RemoteRecognizeSpeechRequest>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteGetOAuthTokenRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteGetOAuthTokenRequest returnVal = parsedObj.ToObject<RemoteGetOAuthTokenRequest>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteDeleteOAuthTokenRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteDeleteOAuthTokenRequest returnVal = parsedObj.ToObject<RemoteDeleteOAuthTokenRequest>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteCreateOAuthUriRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteCreateOAuthUriRequest returnVal = parsedObj.ToObject<RemoteCreateOAuthUriRequest>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteFetchPluginViewDataRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteFetchPluginViewDataRequest returnVal = parsedObj.ToObject<RemoteFetchPluginViewDataRequest>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteResolveEntityRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteResolveEntityRequest returnVal = parsedObj.ToObject<RemoteResolveEntityRequest>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteFileListRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteFileListRequest returnVal = parsedObj.ToObject<RemoteFileListRequest>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteFileReadContentsRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteFileReadContentsRequest returnVal = parsedObj.ToObject<RemoteFileReadContentsRequest>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteFileStatRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteFileStatRequest returnVal = parsedObj.ToObject<RemoteFileStatRequest>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteFileWriteStatRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteFileWriteStatRequest returnVal = parsedObj.ToObject<RemoteFileWriteStatRequest>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteFileCreateDirectoryRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteFileCreateDirectoryRequest returnVal = parsedObj.ToObject<RemoteFileCreateDirectoryRequest>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteFileDeleteRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteFileDeleteRequest returnVal = parsedObj.ToObject<RemoteFileDeleteRequest>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteFileMoveRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteFileMoveRequest returnVal = parsedObj.ToObject<RemoteFileMoveRequest>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteFileWriteContentsRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteFileWriteContentsRequest returnVal = parsedObj.ToObject<RemoteFileWriteContentsRequest>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteFileStreamOpenRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteFileStreamOpenRequest returnVal = parsedObj.ToObject<RemoteFileStreamOpenRequest>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteFileStreamReadRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteFileStreamReadRequest returnVal = parsedObj.ToObject<RemoteFileStreamReadRequest>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteFileStreamWriteRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteFileStreamWriteRequest returnVal = parsedObj.ToObject<RemoteFileStreamWriteRequest>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteFileStreamCloseRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteFileStreamCloseRequest returnVal = parsedObj.ToObject<RemoteFileStreamCloseRequest>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteFileStreamSeekRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteFileStreamSeekRequest returnVal = parsedObj.ToObject<RemoteFileStreamSeekRequest>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteFileStreamSetLengthRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteFileStreamSetLengthRequest returnVal = parsedObj.ToObject<RemoteFileStreamSetLengthRequest>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteHttpRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteHttpRequest returnVal = parsedObj.ToObject<RemoteHttpRequest>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteUploadMetricsRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteUploadMetricsRequest returnVal = parsedObj.ToObject<RemoteUploadMetricsRequest>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteCrashContainerRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteCrashContainerRequest returnVal = parsedObj.ToObject<RemoteCrashContainerRequest>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else
                    {
                        queryLogger.Log("Can't parse json remoting message: Unknown request method \"" + methodName + "\"", LogLevel.Err);
                        LogJsonErrorPayload(data, queryLogger);
                        return null;
                    }
                }
                else if (messageType == RemoteProcedureResponse<object>.RESPONSE_MESSAGE_TYPE)
                {
                    // It's a response of some kind
                    if (string.Equals(methodName, KeepAliveRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteProcedureResponse<long> returnVal = parsedObj.ToObject<RemoteProcedureResponse<long>>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteExecutePluginRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteProcedureResponse<DialogProcessingResponse> returnVal = parsedObj.ToObject<RemoteProcedureResponse<DialogProcessingResponse>>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteLoadPluginRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteProcedureResponse<LoadedPluginInformation> returnVal = parsedObj.ToObject<RemoteProcedureResponse<LoadedPluginInformation>>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteUnloadPluginRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteProcedureResponse<bool> returnVal = parsedObj.ToObject<RemoteProcedureResponse<bool>>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteGetAvailablePluginsRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteProcedureResponse<List<PluginStrongName>> returnVal = parsedObj.ToObject<RemoteProcedureResponse<List<PluginStrongName>>>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteTriggerPluginRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteProcedureResponse<TriggerProcessingResponse> returnVal = parsedObj.ToObject<RemoteProcedureResponse<TriggerProcessingResponse>>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteCrossDomainRequestRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteProcedureResponse<CrossDomainRequestData> returnVal = parsedObj.ToObject<RemoteProcedureResponse<CrossDomainRequestData>>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteCrossDomainResponseRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteProcedureResponse<CrossDomainResponseResponse> returnVal = parsedObj.ToObject<RemoteProcedureResponse<CrossDomainResponseResponse>>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteSynthesizeSpeechRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteProcedureResponse<SynthesizedSpeech> returnVal = parsedObj.ToObject<RemoteProcedureResponse<SynthesizedSpeech>>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteRecognizeSpeechRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteProcedureResponse<SpeechRecognitionResult> returnVal = parsedObj.ToObject<RemoteProcedureResponse<SpeechRecognitionResult>>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteGetOAuthTokenRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteProcedureResponse<OAuthToken> returnVal = parsedObj.ToObject<RemoteProcedureResponse<OAuthToken>>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteCreateOAuthUriRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteProcedureResponse<string> returnVal = parsedObj.ToObject<RemoteProcedureResponse<string>>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteDeleteOAuthTokenRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteProcedureResponse<bool> returnVal = parsedObj.ToObject<RemoteProcedureResponse<bool>>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteFetchPluginViewDataRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteProcedureResponse<CachedWebData> returnVal = parsedObj.ToObject<RemoteProcedureResponse<CachedWebData>>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteResolveEntityRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteProcedureResponse<RemoteResolveEntityResponse> returnVal = parsedObj.ToObject<RemoteProcedureResponse<RemoteResolveEntityResponse>>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteFileListRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteProcedureResponse<List<string>> returnVal = parsedObj.ToObject<RemoteProcedureResponse<List<string>>>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteFileReadContentsRequest.METHOD_NAME, StringComparison.Ordinal) ||
                        string.Equals(methodName, RemoteFileStreamReadRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteProcedureResponse<ArraySegment<byte>> returnVal = parsedObj.ToObject<RemoteProcedureResponse<ArraySegment<byte>>>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteFileStatRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteProcedureResponse<RemoteFileStat> returnVal = parsedObj.ToObject<RemoteProcedureResponse<RemoteFileStat>>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteFileStreamOpenRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteProcedureResponse<RemoteFileStreamOpenResult> returnVal = parsedObj.ToObject<RemoteProcedureResponse<RemoteFileStreamOpenResult>>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteFileCreateDirectoryRequest.METHOD_NAME, StringComparison.Ordinal) ||
                        string.Equals(methodName, RemoteFileDeleteRequest.METHOD_NAME, StringComparison.Ordinal) ||
                        string.Equals(methodName, RemoteFileWriteStatRequest.METHOD_NAME, StringComparison.Ordinal) ||
                        string.Equals(methodName, RemoteFileWriteContentsRequest.METHOD_NAME, StringComparison.Ordinal) ||
                        string.Equals(methodName, RemoteFileStreamSeekRequest.METHOD_NAME, StringComparison.Ordinal) ||
                        string.Equals(methodName, RemoteFileStreamSetLengthRequest.METHOD_NAME, StringComparison.Ordinal) ||
                        string.Equals(methodName, RemoteFileStreamWriteRequest.METHOD_NAME, StringComparison.Ordinal) ||
                        string.Equals(methodName, RemoteFileStreamCloseRequest.METHOD_NAME, StringComparison.Ordinal) ||
                        string.Equals(methodName, RemoteFileMoveRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteProcedureResponse<bool> returnVal = parsedObj.ToObject<RemoteProcedureResponse<bool>>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteHttpRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteProcedureResponse<ArraySegment<byte>> returnVal = parsedObj.ToObject<RemoteProcedureResponse<ArraySegment<byte>>>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteLogMessageRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteProcedureResponse<bool> returnVal = parsedObj.ToObject<RemoteProcedureResponse<bool>>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else if (string.Equals(methodName, RemoteUploadMetricsRequest.METHOD_NAME, StringComparison.Ordinal))
                    {
                        RemoteProcedureResponse<bool> returnVal = parsedObj.ToObject<RemoteProcedureResponse<bool>>(JSON_SERIALIZER);
                        return new Tuple<object, Type>(returnVal, returnVal.GetType());
                    }
                    else
                    {
                        queryLogger.Log("Can't parse json remoting message: Unknown response method \"" + methodName + "\"", LogLevel.Err);
                        LogJsonErrorPayload(data, queryLogger);
                        return null;
                    }
                }
                else
                {
                    queryLogger.Log("Can't parse json remoting message: Unknown message type " + messageType, LogLevel.Err);
                    LogJsonErrorPayload(data, queryLogger);
                    return null;
                }
            }
            catch (Exception e)
            {
                queryLogger.Log("Exception while parsing Json remoting message", LogLevel.Err);
                queryLogger.Log(e, LogLevel.Err);
                LogJsonErrorPayload(data, queryLogger);
                return null;
            }
            finally
            {
                data.Dispose();
            }
        }

        private static void LogJsonErrorPayload(PooledBuffer<byte> data, ILogger queryLogger)
        {
            using (PooledStringBuilder sb = StringBuilderPool.Rent())
            {
                sb.Builder.AppendFormat("Raw data (length {0}): ", data.Length);
                sb.Builder.Append(Encoding.UTF8.GetString(data.Buffer, 0, data.Length));
                queryLogger.Log(sb.Builder.ToString(), LogLevel.Err);
            }
        }

        private PooledBuffer<byte> SerializeInternal(object data, ILogger queryLogger)
        {
            using (RecyclableMemoryStream memoryStream = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
            //using (StreamWriter writer = new StreamWriter(memoryStream, StringUtils.UTF8_WITHOUT_BOM))
            using (Utf8StreamWriter writer = new Utf8StreamWriter(memoryStream))
            using (JsonTextWriter jsonWriter = new JsonTextWriter(writer))
            {
                JSON_SERIALIZER.Serialize(jsonWriter, data);
                jsonWriter.Flush();
                return memoryStream.ToPooledBuffer();
            }
        }
    }
}
