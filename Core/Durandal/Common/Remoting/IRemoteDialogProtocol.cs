using Durandal.API;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Runtime;
using Durandal.Common.File;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.Remoting.Protocol;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Remoting
{
    public interface IRemoteDialogProtocol
    {
        /// <summary>
        /// A constant unique ID which identifies this protocol.
        /// </summary>
        uint ProtocolId { get; }

        Tuple<object, Type> Parse(PooledBuffer<byte> data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(KeepAliveRequest data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteLoadPluginRequest data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteUnloadPluginRequest data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteExecutePluginRequest data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteGetAvailablePluginsRequest data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteTriggerPluginRequest data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteLogMessageRequest data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteSynthesizeSpeechRequest data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteRecognizeSpeechRequest data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteCrossDomainRequestRequest data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteCrossDomainResponseRequest data, ILogger queryLogger);
        
        PooledBuffer<byte> Serialize(RemoteGetOAuthTokenRequest data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteDeleteOAuthTokenRequest data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteCreateOAuthUriRequest data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteFetchPluginViewDataRequest data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteResolveEntityRequest data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteFileStatRequest data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteFileWriteStatRequest data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteFileListRequest data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteFileReadContentsRequest data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteFileMoveRequest data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteFileCreateDirectoryRequest data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteFileDeleteRequest data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteFileWriteContentsRequest data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteFileStreamOpenRequest data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteFileStreamReadRequest data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteFileStreamWriteRequest data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteFileStreamCloseRequest data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteFileStreamSeekRequest data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteFileStreamSetLengthRequest data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteHttpRequest data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteUploadMetricsRequest data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteCrashContainerRequest data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteProcedureResponse<string> data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteProcedureResponse<bool> data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteProcedureResponse<long> data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteProcedureResponse<ArraySegment<byte>> data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteProcedureResponse<RemoteFileStat> data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteProcedureResponse<RemoteFileStreamOpenResult> data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteProcedureResponse<List<string>> data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteProcedureResponse<SynthesizedSpeech> data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteProcedureResponse<SpeechRecognitionResult> data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteProcedureResponse<CrossDomainRequestData> data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteProcedureResponse<CrossDomainResponseResponse> data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteProcedureResponse<OAuthToken> data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteProcedureResponse<CachedWebData> data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteProcedureResponse<RemoteResolveEntityResponse> data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteProcedureResponse<DialogProcessingResponse> data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteProcedureResponse<LoadedPluginInformation> data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteProcedureResponse<List<PluginStrongName>> data, ILogger queryLogger);

        PooledBuffer<byte> Serialize(RemoteProcedureResponse<TriggerProcessingResponse> data, ILogger queryLogger);
    }
}
