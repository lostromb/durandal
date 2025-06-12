using Bond;
using Bond.IO.Safe;
using Bond.Protocols;
using Durandal.API;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Runtime;
using Durandal.Common.File;
using Durandal.Common.IO;
using Durandal.Common.Utils;
using Durandal.Common.Logger;
using Durandal.Common.Remoting;
using Durandal.Common.Collections;
using Durandal.Common.Tasks;
using System;
using System.Collections.Generic;
using System.Text;

using BondRemoting = Durandal.Extensions.BondProtocol.Remoting;
using DurandalRemoting = Durandal.Common.Remoting.Protocol;

namespace Durandal.Extensions.BondProtocol
{
    public class BondRemoteDialogProtocol : IRemoteDialogProtocol
    {
        public const uint PROTOCOL_ID = 2;
        public uint ProtocolId => PROTOCOL_ID;
        
        static BondRemoteDialogProtocol()
        {
            DurandalTaskExtensions.LongRunningTaskFactory.StartNew(() =>
            {
                // This can safely be done on a background thread because the bond converter uses concurrent pools internally
                BondConverter.PrecacheSerializers<BondRemoting.KeepAliveRequest>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteBlobResponse>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteBoolResponse>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteInt64Response>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteCachedWebDataResponse>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteCreateOAuthUriRequest>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteCrossDomainRequestDataResponse>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteCrossDomainRequestRequest>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteCrossDomainResponseRequest>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteCrossDomainResponseResponseResponse>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteDeleteOAuthTokenRequest>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteDialogProcessingResponse>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteException>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteExecutePluginRequest>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteFetchPluginViewDataRequest>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteFileCreateDirectoryRequest>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteFileDeleteRequest>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteFileListRequest>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteFileMoveRequest>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteFileReadContentsRequest>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteFileStatRequest>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteFileStreamAccessMode>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteFileStreamCloseRequest>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteFileStreamOpenMode>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteFileStreamOpenRequest>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteFileStreamOpenResult>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteFileStreamReadRequest>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteFileStreamSeekOrigin>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteFileStreamSeekRequest>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteFileStreamSetLengthRequest>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteFileStreamShareMode>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteFileStreamWriteRequest>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteFileWriteStatRequest>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteFileStatResponse>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteFileWriteContentsRequest>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteGetOAuthTokenRequest>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteListPluginStrongNameResponse>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteLoadPluginRequest>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteLoadPluginResponse>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteLogMessageRequest>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteMessage>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteMessageType>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteOAuthTokenResponse>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteProcedureRequest>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteProcedureResponse>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteRecognizeSpeechRequest>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteResolveEntityRequest>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteResolveEntityResponse>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteResolveEntityResponseResponse>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteSpeechRecognitionResultResponse>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteStringListResponse>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteStringResponse>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteSynthesizedSpeechResponse>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteSynthesizeSpeechRequest>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteTriggerPluginRequest>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteTriggerProcessingResponse>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteUnloadPluginRequest>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteCrashContainerRequest>();
                BondConverter.PrecacheSerializers<BondRemoting.RemoteUploadMetricsRequest>();
            });
        }

        public Tuple<object, Type> Parse(PooledBuffer<byte> data, ILogger queryLogger)
        {
            try
            {
                // First try to parse as a RemoteMessage to see the type
                BondRemoting.RemoteMessage messageHeader1;
                if (!BondConverter.DeserializeBond(data, 0, data.Length, out messageHeader1))
                {
                    queryLogger.Log("Can't parse bond remoting message: No message type header", LogLevel.Err);
                    LogBondErrorPayload(data, queryLogger);
                    return null;
                }

                BondRemoting.RemoteMessageType messageType = messageHeader1.MessageType;
                if (messageType == BondRemoting.RemoteMessageType.Request)
                {
                    BondRemoting.RemoteProcedureRequest request;
                    if (!BondConverter.DeserializeBond(data, 0, data.Length, out request))
                    {
                        queryLogger.Log("Can't parse bond remoting message: Message has type \"request\" but doesn't look like a request", LogLevel.Err);
                        LogBondErrorPayload(data, queryLogger);
                        return null;
                    }

                    if (string.Equals(DurandalRemoting.KeepAliveRequest.METHOD_NAME, request.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.KeepAliveRequest keepAliveRequest;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out keepAliveRequest))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize KeepAliveRequest", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.KeepAliveRequest converted = BondTypeConverters.Convert(keepAliveRequest);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteExecutePluginRequest.METHOD_NAME, request.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteExecutePluginRequest executePluginRequest;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out executePluginRequest))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteExecutePluginRequest", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteExecutePluginRequest converted = BondTypeConverters.Convert(executePluginRequest);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteLogMessageRequest.METHOD_NAME, request.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteLogMessageRequest logMessageRequest;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out logMessageRequest))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteLogMessageRequest", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteLogMessageRequest converted = BondTypeConverters.Convert(logMessageRequest);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteLoadPluginRequest.METHOD_NAME, request.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteLoadPluginRequest loadPluginRequest;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out loadPluginRequest))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteLoadPluginRequest", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteLoadPluginRequest converted = BondTypeConverters.Convert(loadPluginRequest);
                        return new Tuple<object, Type>(converted, typeof(DurandalRemoting.RemoteLoadPluginRequest));
                    }
                    else if (string.Equals(DurandalRemoting.RemoteUnloadPluginRequest.METHOD_NAME, request.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteUnloadPluginRequest unloadPluginRequest;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out unloadPluginRequest))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteUnloadPluginRequest", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteUnloadPluginRequest converted = BondTypeConverters.Convert(unloadPluginRequest);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteGetAvailablePluginsRequest.METHOD_NAME, request.MethodName, StringComparison.Ordinal))
                    {
                        // no special method body here so there is no need to actually deserialize the request
                        DurandalRemoting.RemoteGetAvailablePluginsRequest converted = new DurandalRemoting.RemoteGetAvailablePluginsRequest();
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteTriggerPluginRequest.METHOD_NAME, request.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteTriggerPluginRequest triggerPluginRequest;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out triggerPluginRequest))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteTriggerPluginRequest", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteTriggerPluginRequest converted = BondTypeConverters.Convert(triggerPluginRequest);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteCrossDomainRequestRequest.METHOD_NAME, request.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteCrossDomainRequestRequest crossDomainRequestRequest;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out crossDomainRequestRequest))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteCrossDomainRequestRequest", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteCrossDomainRequestRequest converted = BondTypeConverters.Convert(crossDomainRequestRequest);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteCrossDomainResponseRequest.METHOD_NAME, request.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteCrossDomainResponseRequest crossDomainResponseRequest;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out crossDomainResponseRequest))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteCrossDomainRequestRequest", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteCrossDomainResponseRequest converted = BondTypeConverters.Convert(crossDomainResponseRequest);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteSynthesizeSpeechRequest.METHOD_NAME, request.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteSynthesizeSpeechRequest synthSpeechRequest;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out synthSpeechRequest))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteSynthesizeSpeechRequest", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteSynthesizeSpeechRequest converted = BondTypeConverters.Convert(synthSpeechRequest);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteRecognizeSpeechRequest.METHOD_NAME, request.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteRecognizeSpeechRequest recognizeSpeechRequest;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out recognizeSpeechRequest))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteRecognizeSpeechRequest", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteRecognizeSpeechRequest converted = BondTypeConverters.Convert(recognizeSpeechRequest);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteGetOAuthTokenRequest.METHOD_NAME, request.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteGetOAuthTokenRequest convertedRequest;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out convertedRequest))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteGetOAuthTokenRequest", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteGetOAuthTokenRequest converted = BondTypeConverters.Convert(convertedRequest);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteDeleteOAuthTokenRequest.METHOD_NAME, request.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteDeleteOAuthTokenRequest convertedRequest;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out convertedRequest))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteDeleteOAuthTokenRequest", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteDeleteOAuthTokenRequest converted = BondTypeConverters.Convert(convertedRequest);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteCreateOAuthUriRequest.METHOD_NAME, request.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteCreateOAuthUriRequest convertedRequest;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out convertedRequest))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteCreateOAuthUriRequest", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteCreateOAuthUriRequest converted = BondTypeConverters.Convert(convertedRequest);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteFetchPluginViewDataRequest.METHOD_NAME, request.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteFetchPluginViewDataRequest convertedRequest;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out convertedRequest))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteFetchPluginViewDataRequest", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteFetchPluginViewDataRequest converted = BondTypeConverters.Convert(convertedRequest);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteResolveEntityRequest.METHOD_NAME, request.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteResolveEntityRequest convertedRequest;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out convertedRequest))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteResolveEntityRequest", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteResolveEntityRequest converted = BondTypeConverters.Convert(convertedRequest);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteFileCreateDirectoryRequest.METHOD_NAME, request.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteFileCreateDirectoryRequest convertedRequest;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out convertedRequest))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteFileCreateDirectoryRequest", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteFileCreateDirectoryRequest converted = BondTypeConverters.Convert(convertedRequest);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteFileDeleteRequest.METHOD_NAME, request.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteFileDeleteRequest convertedRequest;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out convertedRequest))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteFileDeleteRequest", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteFileDeleteRequest converted = BondTypeConverters.Convert(convertedRequest);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteFileListRequest.METHOD_NAME, request.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteFileListRequest convertedRequest;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out convertedRequest))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteFileListRequest", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteFileListRequest converted = BondTypeConverters.Convert(convertedRequest);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteFileMoveRequest.METHOD_NAME, request.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteFileMoveRequest convertedRequest;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out convertedRequest))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteFileMoveRequest", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteFileMoveRequest converted = BondTypeConverters.Convert(convertedRequest);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteFileReadContentsRequest.METHOD_NAME, request.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteFileReadContentsRequest convertedRequest;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out convertedRequest))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteFileReadContentsRequest", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteFileReadContentsRequest converted = BondTypeConverters.Convert(convertedRequest);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteFileStatRequest.METHOD_NAME, request.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteFileStatRequest convertedRequest;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out convertedRequest))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteFileStatRequest", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteFileStatRequest converted = BondTypeConverters.Convert(convertedRequest);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteFileStreamCloseRequest.METHOD_NAME, request.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteFileStreamCloseRequest convertedRequest;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out convertedRequest))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteFileStreamCloseRequest", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteFileStreamCloseRequest converted = BondTypeConverters.Convert(convertedRequest);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteFileStreamOpenRequest.METHOD_NAME, request.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteFileStreamOpenRequest convertedRequest;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out convertedRequest))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteFileStreamOpenRequest", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteFileStreamOpenRequest converted = BondTypeConverters.Convert(convertedRequest);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteFileStreamReadRequest.METHOD_NAME, request.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteFileStreamReadRequest convertedRequest;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out convertedRequest))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteFileStreamReadRequest", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteFileStreamReadRequest converted = BondTypeConverters.Convert(convertedRequest);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteFileStreamSeekRequest.METHOD_NAME, request.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteFileStreamSeekRequest convertedRequest;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out convertedRequest))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteFileStreamSeekRequest", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteFileStreamSeekRequest converted = BondTypeConverters.Convert(convertedRequest);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteFileStreamSetLengthRequest.METHOD_NAME, request.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteFileStreamSetLengthRequest convertedRequest;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out convertedRequest))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteFileStreamSetLengthRequest", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteFileStreamSetLengthRequest converted = BondTypeConverters.Convert(convertedRequest);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteFileStreamWriteRequest.METHOD_NAME, request.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteFileStreamWriteRequest convertedRequest;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out convertedRequest))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteFileStreamWriteRequest", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteFileStreamWriteRequest converted = BondTypeConverters.Convert(convertedRequest);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteFileWriteStatRequest.METHOD_NAME, request.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteFileWriteStatRequest convertedRequest;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out convertedRequest))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteFileWriteStatRequest", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteFileWriteStatRequest converted = BondTypeConverters.Convert(convertedRequest);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteFileWriteContentsRequest.METHOD_NAME, request.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteFileWriteContentsRequest convertedRequest;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out convertedRequest))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteFileWriteContentsRequest", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteFileWriteContentsRequest converted = BondTypeConverters.Convert(convertedRequest);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteHttpRequest.METHOD_NAME, request.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteHttpRequest convertedRequest;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out convertedRequest))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteHttpRequest", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteHttpRequest converted = BondTypeConverters.Convert(convertedRequest);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteUploadMetricsRequest.METHOD_NAME, request.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteUploadMetricsRequest convertedRequest;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out convertedRequest))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteUploadMetricsRequest", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteUploadMetricsRequest converted = BondTypeConverters.Convert(convertedRequest);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteCrashContainerRequest.METHOD_NAME, request.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteCrashContainerRequest convertedRequest;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out convertedRequest))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteCrashContainerRequest", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteCrashContainerRequest converted = BondTypeConverters.Convert(convertedRequest);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else
                    {
                        queryLogger.Log("Can't parse bond remoting message: Unknown request method \"" + request.MethodName + "\"", LogLevel.Err);
                        LogBondErrorPayload(data, queryLogger);
                        return null;
                    }
                }
                else if (messageType == BondRemoting.RemoteMessageType.Response)
                {
                    BondRemoting.RemoteProcedureResponse response;
                    if (!BondConverter.DeserializeBond(data, 0, data.Length, out response))
                    {
                        queryLogger.Log("Can't parse bond remoting message: Message has type \"response\" but doesn't look like a response", LogLevel.Err);
                        LogBondErrorPayload(data, queryLogger);
                        return null;
                    }

                    if (string.Equals(DurandalRemoting.KeepAliveRequest.METHOD_NAME, response.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteInt64Response keepAliveResponse;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out keepAliveResponse))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteBoolResponse", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteProcedureResponse<long> converted = BondTypeConverters.Convert(keepAliveResponse);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteExecutePluginRequest.METHOD_NAME, response.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteDialogProcessingResponse dialogProcessingResponse;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out dialogProcessingResponse))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteDialogProcessingResponse", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteProcedureResponse<DialogProcessingResponse> converted = BondTypeConverters.Convert(dialogProcessingResponse);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteLoadPluginRequest.METHOD_NAME, response.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteLoadPluginResponse loadPluginResponse;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out loadPluginResponse))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteLoadPluginResponse", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteProcedureResponse<LoadedPluginInformation> converted = BondTypeConverters.Convert(loadPluginResponse);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteUnloadPluginRequest.METHOD_NAME, response.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteBoolResponse unloadPluginResponse;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out unloadPluginResponse))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteBoolResponse", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteProcedureResponse<bool> converted = BondTypeConverters.Convert(unloadPluginResponse);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteGetAvailablePluginsRequest.METHOD_NAME, response.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteListPluginStrongNameResponse getAvailablePluginsResponse;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out getAvailablePluginsResponse))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteGetAvailablePluginsResponse", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteProcedureResponse<List<PluginStrongName>> converted = BondTypeConverters.Convert(getAvailablePluginsResponse);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteTriggerPluginRequest.METHOD_NAME, response.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteTriggerProcessingResponse triggerPluginResponse;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out triggerPluginResponse))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteTriggerPluginResponse", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteProcedureResponse<TriggerProcessingResponse> converted = BondTypeConverters.Convert(triggerPluginResponse);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteCrossDomainRequestRequest.METHOD_NAME, response.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteCrossDomainRequestDataResponse crossDomainRequestResponse;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out crossDomainRequestResponse))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteCrossDomainRequestResponse", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteProcedureResponse<CrossDomainRequestData> converted = BondTypeConverters.Convert(crossDomainRequestResponse);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteCrossDomainResponseRequest.METHOD_NAME, response.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteCrossDomainResponseResponseResponse crossDomainResponseResponse;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out crossDomainResponseResponse))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteCrossDomainResponseResponseResponse", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteProcedureResponse<CrossDomainResponseResponse> converted = BondTypeConverters.Convert(crossDomainResponseResponse);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteSynthesizeSpeechRequest.METHOD_NAME, response.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteSynthesizedSpeechResponse synthSpeechResponse;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out synthSpeechResponse))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteSynthesizedSpeechResponse", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteProcedureResponse<SynthesizedSpeech> converted = BondTypeConverters.Convert(synthSpeechResponse);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteRecognizeSpeechRequest.METHOD_NAME, response.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteSpeechRecognitionResultResponse synthSpeechResponse;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out synthSpeechResponse))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteSpeechRecognitionResultResponse", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteProcedureResponse<SpeechRecognitionResult> converted = BondTypeConverters.Convert(synthSpeechResponse);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteGetOAuthTokenRequest.METHOD_NAME, response.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteOAuthTokenResponse parsedResponse;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out parsedResponse))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteOAuthTokenResponse", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteProcedureResponse<OAuthToken> converted = BondTypeConverters.Convert(parsedResponse);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteCreateOAuthUriRequest.METHOD_NAME, response.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteStringResponse parsedResponse;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out parsedResponse))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteStringResponse", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteProcedureResponse<string> converted = BondTypeConverters.Convert(parsedResponse);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteDeleteOAuthTokenRequest.METHOD_NAME, response.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteBoolResponse parsedResponse;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out parsedResponse))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteBoolResponse", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteProcedureResponse<bool> converted = BondTypeConverters.Convert(parsedResponse);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteFetchPluginViewDataRequest.METHOD_NAME, response.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteCachedWebDataResponse parsedResponse;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out parsedResponse))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteCachedWebDataResponse", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteProcedureResponse<CachedWebData> converted = BondTypeConverters.Convert(parsedResponse);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteResolveEntityRequest.METHOD_NAME, response.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteResolveEntityResponseResponse parsedResponse;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out parsedResponse))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteResolveEntityResponseResponse", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteProcedureResponse<DurandalRemoting.RemoteResolveEntityResponse> converted = BondTypeConverters.Convert(parsedResponse);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteFileCreateDirectoryRequest.METHOD_NAME, response.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteBoolResponse parsedResponse;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out parsedResponse))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteBoolResponse", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteProcedureResponse<bool> converted = BondTypeConverters.Convert(parsedResponse);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteFileDeleteRequest.METHOD_NAME, response.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteBoolResponse parsedResponse;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out parsedResponse))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteBoolResponse", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteProcedureResponse<bool> converted = BondTypeConverters.Convert(parsedResponse);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteFileListRequest.METHOD_NAME, response.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteStringListResponse parsedResponse;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out parsedResponse))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteStringListResponse", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteProcedureResponse<List<string>> converted = BondTypeConverters.Convert(parsedResponse);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteFileMoveRequest.METHOD_NAME, response.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteBoolResponse parsedResponse;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out parsedResponse))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteBoolResponse", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteProcedureResponse<bool> converted = BondTypeConverters.Convert(parsedResponse);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteFileReadContentsRequest.METHOD_NAME, response.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteBlobResponse parsedResponse;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out parsedResponse))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteBlobResponse", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteProcedureResponse<ArraySegment<byte>> converted = BondTypeConverters.Convert(parsedResponse);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteFileStatRequest.METHOD_NAME, response.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteFileStatResponse parsedResponse;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out parsedResponse))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteFileStatResponse", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteProcedureResponse<DurandalRemoting.RemoteFileStat> converted = BondTypeConverters.Convert(parsedResponse);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteFileStreamCloseRequest.METHOD_NAME, response.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteBoolResponse parsedResponse;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out parsedResponse))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteBoolResponse", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteProcedureResponse<bool> converted = BondTypeConverters.Convert(parsedResponse);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteFileStreamOpenRequest.METHOD_NAME, response.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteFileStreamOpenResponse parsedResponse;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out parsedResponse))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteBoolResponse", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteProcedureResponse<DurandalRemoting.RemoteFileStreamOpenResult> converted = BondTypeConverters.Convert(parsedResponse);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteFileStreamReadRequest.METHOD_NAME, response.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteBlobResponse parsedResponse;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out parsedResponse))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteBlobResponse", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteProcedureResponse<ArraySegment<byte>> converted = BondTypeConverters.Convert(parsedResponse);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteFileStreamSeekRequest.METHOD_NAME, response.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteBoolResponse parsedResponse;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out parsedResponse))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteBoolResponse", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteProcedureResponse<bool> converted = BondTypeConverters.Convert(parsedResponse);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteFileStreamSetLengthRequest.METHOD_NAME, response.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteBoolResponse parsedResponse;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out parsedResponse))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteBoolResponse", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteProcedureResponse<bool> converted = BondTypeConverters.Convert(parsedResponse);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteFileStreamWriteRequest.METHOD_NAME, response.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteBoolResponse parsedResponse;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out parsedResponse))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteBoolResponse", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteProcedureResponse<bool> converted = BondTypeConverters.Convert(parsedResponse);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteFileWriteStatRequest.METHOD_NAME, response.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteBoolResponse parsedResponse;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out parsedResponse))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteBoolResponse", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteProcedureResponse<bool> converted = BondTypeConverters.Convert(parsedResponse);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteFileWriteContentsRequest.METHOD_NAME, response.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteBoolResponse parsedResponse;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out parsedResponse))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteBoolResponse", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteProcedureResponse<bool> converted = BondTypeConverters.Convert(parsedResponse);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteHttpRequest.METHOD_NAME, response.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteBlobResponse parsedResponse;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out parsedResponse))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteBlobResponse", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteProcedureResponse<ArraySegment<byte>> converted = BondTypeConverters.Convert(parsedResponse);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteLogMessageRequest.METHOD_NAME, response.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteBoolResponse parsedResponse;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out parsedResponse))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteBoolResponse", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteProcedureResponse<bool> converted = BondTypeConverters.Convert(parsedResponse);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else if (string.Equals(DurandalRemoting.RemoteUploadMetricsRequest.METHOD_NAME, response.MethodName, StringComparison.Ordinal))
                    {
                        BondRemoting.RemoteBoolResponse parsedResponse;
                        if (!BondConverter.DeserializeBond(data, 0, data.Length, out parsedResponse))
                        {
                            queryLogger.Log("Can't parse bond remoting message: Can't deserialize RemoteBoolResponse", LogLevel.Err);
                            LogBondErrorPayload(data, queryLogger);
                            return null;
                        }

                        DurandalRemoting.RemoteProcedureResponse<bool> converted = BondTypeConverters.Convert(parsedResponse);
                        return new Tuple<object, Type>(converted, converted.GetType());
                    }
                    else
                    {
                        queryLogger.Log("Can't parse bond remoting message: Unknown response method \"" + response.MethodName + "\"", LogLevel.Err);
                        LogBondErrorPayload(data, queryLogger);
                        return null;
                    }
                }

                queryLogger.Log("Can't parse bond remoting message: Unknown message type " + messageType, LogLevel.Err);
                LogBondErrorPayload(data, queryLogger);
                return null;
            }
            catch (Exception e)
            {
                queryLogger.Log("Exception while parsing Bond remoting message", LogLevel.Err);
                queryLogger.Log(e, LogLevel.Err);
                LogBondErrorPayload(data, queryLogger);
                return null;
            }
            finally
            {
                data.Dispose();
            }
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteProcedureResponse<DialogProcessingResponse> data, ILogger queryLogger)
        {
            BondRemoting.RemoteDialogProcessingResponse converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteExecutePluginRequest data, ILogger queryLogger)
        {
            BondRemoting.RemoteExecutePluginRequest converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteLogMessageRequest data, ILogger queryLogger)
        {
            BondRemoting.RemoteLogMessageRequest converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteLoadPluginRequest data, ILogger queryLogger)
        {
            BondRemoting.RemoteLoadPluginRequest converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteProcedureResponse<LoadedPluginInformation> data, ILogger queryLogger)
        {
            BondRemoting.RemoteLoadPluginResponse converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteUnloadPluginRequest data, ILogger queryLogger)
        {
            BondRemoting.RemoteUnloadPluginRequest converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteProcedureResponse<bool> data, ILogger queryLogger)
        {
            BondRemoting.RemoteBoolResponse converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteGetAvailablePluginsRequest data, ILogger queryLogger)
        {
            BondRemoting.RemoteProcedureRequest converted = new Remoting.RemoteProcedureRequest()
            {
                MessageType = BondRemoting.RemoteMessageType.Request,
                MethodName = data.MethodName
            };

            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteProcedureResponse<List<PluginStrongName>> data, ILogger queryLogger)
        {
            BondRemoting.RemoteListPluginStrongNameResponse converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteTriggerPluginRequest data, ILogger queryLogger)
        {
            BondRemoting.RemoteTriggerPluginRequest converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteProcedureResponse<TriggerProcessingResponse> data, ILogger queryLogger)
        {
            BondRemoting.RemoteTriggerProcessingResponse converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteCrossDomainRequestRequest data, ILogger queryLogger)
        {
            BondRemoting.RemoteCrossDomainRequestRequest converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteProcedureResponse<CrossDomainRequestData> data, ILogger queryLogger)
        {
            BondRemoting.RemoteCrossDomainRequestDataResponse converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteCrossDomainResponseRequest data, ILogger queryLogger)
        {
            BondRemoting.RemoteCrossDomainResponseRequest converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteProcedureResponse<CrossDomainResponseResponse> data, ILogger queryLogger)
        {
            BondRemoting.RemoteCrossDomainResponseResponseResponse converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteSynthesizeSpeechRequest data, ILogger queryLogger)
        {
            BondRemoting.RemoteSynthesizeSpeechRequest converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteProcedureResponse<SynthesizedSpeech> data, ILogger queryLogger)
        {
            BondRemoting.RemoteSynthesizedSpeechResponse converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteRecognizeSpeechRequest data, ILogger queryLogger)
        {
            BondRemoting.RemoteRecognizeSpeechRequest converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteProcedureResponse<SpeechRecognitionResult> data, ILogger queryLogger)
        {
            BondRemoting.RemoteSpeechRecognitionResultResponse converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteGetOAuthTokenRequest data, ILogger queryLogger)
        {
            BondRemoting.RemoteGetOAuthTokenRequest converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteProcedureResponse<string> data, ILogger queryLogger)
        {
            BondRemoting.RemoteStringResponse converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteProcedureResponse<long> data, ILogger queryLogger)
        {
            BondRemoting.RemoteInt64Response converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteDeleteOAuthTokenRequest data, ILogger queryLogger)
        {
            BondRemoting.RemoteDeleteOAuthTokenRequest converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteCreateOAuthUriRequest data, ILogger queryLogger)
        {
            BondRemoting.RemoteCreateOAuthUriRequest converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteProcedureResponse<OAuthToken> data, ILogger queryLogger)
        {
            BondRemoting.RemoteOAuthTokenResponse converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteFetchPluginViewDataRequest data, ILogger queryLogger)
        {
            BondRemoting.RemoteFetchPluginViewDataRequest converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteProcedureResponse<CachedWebData> data, ILogger queryLogger)
        {
            BondRemoting.RemoteCachedWebDataResponse converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }
        
        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteResolveEntityRequest data, ILogger queryLogger)
        {
            BondRemoting.RemoteResolveEntityRequest converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteProcedureResponse<DurandalRemoting.RemoteResolveEntityResponse> data, ILogger queryLogger)
        {
            BondRemoting.RemoteResolveEntityResponseResponse converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteFileCreateDirectoryRequest data, ILogger queryLogger)
        {
            BondRemoting.RemoteFileCreateDirectoryRequest converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteFileDeleteRequest data, ILogger queryLogger)
        {
            BondRemoting.RemoteFileDeleteRequest converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteFileListRequest data, ILogger queryLogger)
        {
            BondRemoting.RemoteFileListRequest converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteFileMoveRequest data, ILogger queryLogger)
        {
            BondRemoting.RemoteFileMoveRequest converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteFileReadContentsRequest data, ILogger queryLogger)
        {
            BondRemoting.RemoteFileReadContentsRequest converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteFileStatRequest data, ILogger queryLogger)
        {
            BondRemoting.RemoteFileStatRequest converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteFileWriteStatRequest data, ILogger queryLogger)
        {
            BondRemoting.RemoteFileWriteStatRequest converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteFileWriteContentsRequest data, ILogger queryLogger)
        {
            BondRemoting.RemoteFileWriteContentsRequest converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteFileStreamOpenRequest data, ILogger queryLogger)
        {
            BondRemoting.RemoteFileStreamOpenRequest converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteFileStreamCloseRequest data, ILogger queryLogger)
        {
            BondRemoting.RemoteFileStreamCloseRequest converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteFileStreamReadRequest data, ILogger queryLogger)
        {
            BondRemoting.RemoteFileStreamReadRequest converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteFileStreamWriteRequest data, ILogger queryLogger)
        {
            BondRemoting.RemoteFileStreamWriteRequest converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteFileStreamSeekRequest data, ILogger queryLogger)
        {
            BondRemoting.RemoteFileStreamSeekRequest converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteFileStreamSetLengthRequest data, ILogger queryLogger)
        {
            BondRemoting.RemoteFileStreamSetLengthRequest converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteProcedureResponse<ArraySegment<byte>> data, ILogger queryLogger)
        {
            BondRemoting.RemoteBlobResponse converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteProcedureResponse<DurandalRemoting.RemoteFileStat> data, ILogger queryLogger)
        {
            BondRemoting.RemoteFileStatResponse converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteProcedureResponse<DurandalRemoting.RemoteFileStreamOpenResult> data, ILogger queryLogger)
        {
            BondRemoting.RemoteFileStreamOpenResponse converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteProcedureResponse<List<string>> data, ILogger queryLogger)
        {
            BondRemoting.RemoteStringListResponse converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteHttpRequest data, ILogger queryLogger)
        {
            BondRemoting.RemoteHttpRequest converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.KeepAliveRequest data, ILogger queryLogger)
        {
            BondRemoting.KeepAliveRequest converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteUploadMetricsRequest data, ILogger queryLogger)
        {
            BondRemoting.RemoteUploadMetricsRequest converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        public PooledBuffer<byte> Serialize(DurandalRemoting.RemoteCrashContainerRequest data, ILogger queryLogger)
        {
            BondRemoting.RemoteCrashContainerRequest converted = BondTypeConverters.Convert(data);
            return BondConverter.SerializeBondPooled(converted, queryLogger);
        }

        private static void LogBondErrorPayload(PooledBuffer<byte> data, ILogger queryLogger)
        {
            using (PooledStringBuilder sb = StringBuilderPool.Rent())
            {
                sb.Builder.AppendFormat("Raw data (length {0}): ", data.Length);
                BinaryHelpers.ToHexString(data.Buffer, 0, data.Length, sb.Builder);
                queryLogger.Log(sb.Builder.ToString(), LogLevel.Err);
            }
        }
    }
}
