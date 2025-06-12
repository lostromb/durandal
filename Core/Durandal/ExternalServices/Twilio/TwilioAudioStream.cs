using Durandal.Common.Audio.Codecs;
using Durandal.Common.IO;
using Durandal.Common.IO.Json;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.WebSocket;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Components.NetworkAudio
{
    public class TwilioAudioStream : NetworkAudioEndpoint
    {
        private TwilioAudioStream(
            WeakPointer<ISocket> socket,
            ILogger logger,
            NetworkDuplex duplex,
            WeakPointer<IAudioGraph> inputGraph,
            AudioDecoder inputDecoder,
            WeakPointer<IAudioGraph> outputGraph,
            AudioEncoder outputEncoder,
            string nodeCustomNameBase) : base(socket, logger, duplex, inputGraph, inputDecoder, outputGraph, outputEncoder, nodeCustomNameBase)
        {
        }

        public static async Task<NetworkAudioEndpoint> CreateReadWriteEndpoint(
            WeakPointer<IAudioGraph> incomingGraph,
            WeakPointer<IAudioGraph> outgoingGraph,
            string nodeCustomName,
            WeakPointer<IWebSocket> webSocket,
            ILogger logger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            HandshakeReturnVal handshakeResult = await Handshake(
                webSocket,
                logger,
                NetworkDuplex.ReadWrite,
                incomingGraph,
                outgoingGraph,
                cancelToken,
                realTime).ConfigureAwait(false);

            return new TwilioAudioStream(
                new WeakPointer<ISocket>(new SocketOverWebSocketForTwilio(webSocket)),
                logger,
                handshakeResult.EstablishedDuplex,
                incomingGraph,
                handshakeResult.Decoder,
                outgoingGraph,
                handshakeResult.Encoder,
                nodeCustomName);
        }

        private class HandshakeReturnVal
        {
            public AudioEncoder Encoder { get; set; }
            public AudioDecoder Decoder { get; set; }
            public NetworkDuplex EstablishedDuplex { get; set; }
        }

        private static async Task<HandshakeReturnVal> Handshake(
            WeakPointer<IWebSocket> socket,
            ILogger logger,
            NetworkDuplex requestedDuplex,
            WeakPointer<IAudioGraph> incomingAudioGraph,
            WeakPointer<IAudioGraph> outgoingAudioGraph,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            TwilioSocketMessage connectedMessage = await GetRemoteMessage(socket, cancelToken, realTime).ConfigureAwait(false);
            if (!(connectedMessage is TwilioConnectedMessage))
            {
                // fail
            }

            TwilioSocketMessage startMessage = await GetRemoteMessage(socket, cancelToken, realTime).ConfigureAwait(false);
            if (!(connectedMessage is TwilioStartMessage))
            {
                // fail
            }

            // Create decoder and encoder
            AudioDecoder decoder = new ULawDecoder(incomingAudioGraph, AudioSampleFormat.Mono(8000), "SomeNodeCustomName");
            AudioEncoder encoder = new ULawEncoder(outgoingAudioGraph, AudioSampleFormat.Mono(8000), "SomeNodeCustomName");

            return new HandshakeReturnVal()
            {
                Encoder = encoder,
                Decoder = decoder,
                EstablishedDuplex = NetworkDuplex.ReadWrite
            };
        }
        
        private static async Task<TwilioSocketMessage> GetRemoteMessage(
            WeakPointer<IWebSocket> socket,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            WebSocketBufferResult remoteMessage = await socket.Value.ReceiveAsBufferAsync(cancelToken, realTime).ConfigureAwait(false);
            JsonSerializer serializer = new JsonSerializer();
            using (PooledBufferMemoryStream messageStream = new PooledBufferMemoryStream(remoteMessage.Result))
            using (TextReader textReader = new StreamReader(messageStream, StringUtils.UTF8_WITHOUT_BOM))
            using (JsonTextReader jsonReader = new JsonTextReader(textReader))
            {
                JObject rawObj = serializer.Deserialize<JObject>(jsonReader);

                string eventType = rawObj.Value<string>("event");
                if (string.Equals("connected", eventType, StringComparison.Ordinal))
                {
                    return rawObj.ToObject<TwilioConnectedMessage>(serializer);
                }
                else if (string.Equals("start", eventType, StringComparison.Ordinal))
                {
                    return rawObj.ToObject<TwilioStartMessage>(serializer);
                }
                else if (string.Equals("media", eventType, StringComparison.Ordinal))
                {
                    return rawObj.ToObject<TwilioMediaMessage>(serializer);
                }
                else if (string.Equals("stop", eventType, StringComparison.Ordinal))
                {
                    throw new NotImplementedException();
                }
                else if (string.Equals("mark", eventType, StringComparison.Ordinal))
                {
                    throw new NotImplementedException();
                }
                else
                {
                    throw new InvalidDataException();
                }
            }
        }

        public class TwilioSocketMessage
        {
            [JsonProperty("event")]
            public string Event { get; set; }
        }

        public class TwilioConnectedMessage : TwilioSocketMessage
        {
            [JsonProperty("protocol")]
            public string Protocol { get; set; }

            [JsonProperty("version")]
            public string Version { get; set; }
        }

        public class TwilioStartMessage : TwilioSocketMessage
        {
            [JsonProperty("sequenceNumber")]
            public string SequenceNumber { get; set; }

            [JsonProperty("streamSid")]
            public string StreamSid { get; set; }

            [JsonProperty("start")]
            public StreamStartParameters Start { get; set; }
        }

        public class TwilioMediaMessage : TwilioSocketMessage
        {
            [JsonProperty("sequenceNumber")]
            public string SequenceNumber { get; set; }

            [JsonProperty("streamSid")]
            public string StreamSid { get; set; }

            [JsonProperty("media")]
            public MediaFrame Media { get; set; }
        }

        public class MediaFrame
        {
            [JsonProperty("track")]
            public string Track { get; set; }

            [JsonProperty("chunk")]
            public string Chunk { get; set; }

            [JsonProperty("timestamp")]
            public string Timestamp { get; set; }

            [JsonProperty("payload")]
            [JsonConverter(typeof(JsonByteArrayConverter))]
            public byte[] Payload { get; set; }
        }

        public class StreamStartParameters
        {
            [JsonProperty("streamSid")]
            public string StreamSid { get; set; }

            [JsonProperty("accountSid")]
            public string AccountSid { get; set; }

            [JsonProperty("callSid")]
            public string CallSid { get; set; }

            [JsonProperty("tracks")]
            public List<string> Tracks { get; set; }

            [JsonProperty("customParameters")]
            public IDictionary<string, string> CustomParameters { get; set; }

            [JsonProperty("mediaFormat")]
            public MediaFormatParameters MediaFormat { get; set; }
        }

        public class MediaFormatParameters
        {
            [JsonProperty("encoding")]
            public string Encoding { get; set; }

            [JsonProperty("sampleRate")]
            public int SampleRate { get; set; }

            [JsonProperty("channels")]
            public int Channels { get; set; }
        }
        private class SocketOverWebSocketForTwilio : ISocket
        {
            private WeakPointer<IWebSocket> _webSocket;

            public SocketOverWebSocketForTwilio(WeakPointer<IWebSocket> innerSocket)
            {
                _webSocket = innerSocket;
            }

            public int ReceiveTimeout { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public string RemoteEndpointString => throw new NotImplementedException();

            public IReadOnlyDictionary<SocketFeature, object> Features => throw new NotImplementedException();

            public async Task Disconnect(CancellationToken cancelToken, IRealTimeProvider realTime, NetworkDuplex which = NetworkDuplex.ReadWrite, bool allowLinger = false)
            {
                if (which.HasFlag(NetworkDuplex.Write))
                {
                    await _webSocket.Value.CloseWrite(cancelToken, realTime).ConfigureAwait(false);
                }
            }

            public void Dispose()
            {
                throw new NotImplementedException();
            }

            public Task FlushAsync(CancellationToken cancelToken, IRealTimeProvider waitProvider)
            {
                throw new NotImplementedException();
            }

            public Task<int> ReadAnyAsync(byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider waitProvider)
            {
                throw new NotImplementedException();
            }

            public Task<int> ReadAsync(byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider waitProvider)
            {
                throw new NotImplementedException();
            }

            public void Unread(byte[] data, int offset, int count)
            {
                throw new NotImplementedException();
            }

            public Task WriteAsync(byte[] data, int offset, int count, CancellationToken cancelToken, IRealTimeProvider waitProvider)
            {
                throw new NotImplementedException();
            }
        }
    }
}
