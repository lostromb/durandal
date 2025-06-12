using Durandal.API;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Components.NetworkAudio
{
    public class SocketAudioEndpoint : NetworkAudioEndpoint
    {
        private static readonly int MAX_HEADER_SIZE = 1024;
        private static readonly Regex DUPLEX_PARSER = new Regex("Duplex=(\\d+)\n");
        private static readonly Regex CODEC_PARSER = new Regex("Codec=(.+?)\n");
        private static readonly Regex CODEC_PARAMS_PARSER = new Regex("CodecParams=(.+?)\n");

        private static readonly byte[] SocketHandshakeHeader = StringUtils.ASCII_ENCODING.GetBytes("DurandalNetworkAudio");

        private SocketAudioEndpoint(
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

        public static async Task<NetworkAudioEndpoint> CreateReadOnlyEndpoint(
            IAudioCodecFactory codecFactory,
            WeakPointer<IAudioGraph> graph,
            string nodeCustomName,
            WeakPointer<ISocket> socket,
            ILogger logger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            HandshakeReturnVal handshakeResult = await Handshake(
                socket,
                logger,
                NetworkDuplex.Read,
                codecFactory,
                graph,
                WeakPointer<IAudioGraph>.Null,
                null,
                null,
                cancelToken,
                realTime).ConfigureAwait(false);

            return new SocketAudioEndpoint(socket, logger, handshakeResult.EstablishedDuplex, graph, handshakeResult.Decoder, WeakPointer<IAudioGraph>.Null, null, nodeCustomName);
        }

        public static async Task<NetworkAudioEndpoint> CreateWriteOnlyEndpoint(
            IAudioCodecFactory codecFactory,
            WeakPointer<IAudioGraph> graph,
            string outgoingCodec,
            AudioSampleFormat outgoingFormat,
            string nodeCustomName,
            WeakPointer<ISocket> socket,
            ILogger logger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            HandshakeReturnVal handshakeResult = await Handshake(
                socket,
                logger,
                NetworkDuplex.Write,
                codecFactory,
                WeakPointer<IAudioGraph>.Null,
                graph,
                outgoingFormat,
                outgoingCodec,
                cancelToken,
                realTime).ConfigureAwait(false);

            return new SocketAudioEndpoint(socket, logger, handshakeResult.EstablishedDuplex, WeakPointer<IAudioGraph>.Null, null, graph, handshakeResult.Encoder, nodeCustomName);
        }

        public static async Task<NetworkAudioEndpoint> CreateReadWriteEndpoint(
            IAudioCodecFactory codecFactory,
            WeakPointer<IAudioGraph> incomingGraph,
            WeakPointer<IAudioGraph> outgoingGraph,
            string outgoingCodec,
            AudioSampleFormat outgoingFormat,
            string nodeCustomName,
            WeakPointer<ISocket> socket,
            ILogger logger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            HandshakeReturnVal handshakeResult = await Handshake(
                socket,
                logger,
                NetworkDuplex.ReadWrite,
                codecFactory,
                incomingGraph,
                outgoingGraph,
                outgoingFormat,
                outgoingCodec,
                cancelToken,
                realTime).ConfigureAwait(false);

            return new SocketAudioEndpoint(
                socket,
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
            WeakPointer<ISocket> socket,
            ILogger logger,
            NetworkDuplex requestedDuplex,
            IAudioCodecFactory codecFactory,
            WeakPointer<IAudioGraph> incomingAudioGraph,
            WeakPointer<IAudioGraph> outgoingAudioGraph,
            AudioSampleFormat outgoingSampleFormat,
            string outgoingCodec,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            AudioEncoder return_encoder = null;
            AudioDecoder return_decoder = null;

            // Send init message on socket
            using (PooledBuffer<byte> scratch = BufferPool<byte>.Rent(MAX_HEADER_SIZE))
            {
                StringBuilder initMessageStringBuilder = new StringBuilder();

                initMessageStringBuilder.Append("Duplex=");
                initMessageStringBuilder.Append((int)requestedDuplex);
                initMessageStringBuilder.Append("\n");
                if ((requestedDuplex & NetworkDuplex.Write) != 0)
                {
                    if (!codecFactory.CanEncode(outgoingCodec))
                    {
                        throw new NotSupportedException($"Local audio endpoint requested codec {outgoingCodec} which is not supported");
                    }

                    return_encoder = codecFactory.CreateEncoder(outgoingCodec, outgoingAudioGraph, outgoingSampleFormat, logger, "SocketAudioEncoder");
                    initMessageStringBuilder.Append("Codec=");
                    initMessageStringBuilder.Append(outgoingCodec);
                    initMessageStringBuilder.Append("\n");
                    initMessageStringBuilder.Append("CodecParams=");
                    initMessageStringBuilder.Append(return_encoder.CodecParams);
                    initMessageStringBuilder.Append("\n");
                }

                byte[] outgoingParams = StringUtils.UTF8_WITHOUT_BOM.GetBytes(initMessageStringBuilder.ToString());
                await socket.Value.WriteAsync(SocketHandshakeHeader, 0, SocketHandshakeHeader.Length, cancelToken, realTime).ConfigureAwait(false);
                BinaryHelpers.Int32ToByteArrayLittleEndian(outgoingParams.Length, scratch.Buffer, 0);
                await socket.Value.WriteAsync(scratch.Buffer, 0, 4, cancelToken, realTime).ConfigureAwait(false);
                await socket.Value.WriteAsync(outgoingParams, 0, outgoingParams.Length, cancelToken, realTime).ConfigureAwait(false);
                await socket.Value.FlushAsync(cancelToken, realTime).ConfigureAwait(false);

                // And read incoming init message from other end
                int bytesRead = await socket.Value.ReadAsync(scratch.Buffer, 0, SocketHandshakeHeader.Length, cancelToken, realTime).ConfigureAwait(false);
                if (!scratch.Buffer.AsSpan(0, bytesRead).SequenceEqual(SocketHandshakeHeader.AsSpan(0, bytesRead)))
                {
                    throw new InvalidDataException("Socket audio endpoint did not receive correct handshake");
                }

                bytesRead = await socket.Value.ReadAsync(scratch.Buffer, 0, 4, cancelToken, realTime).ConfigureAwait(false);
                int headerLength = BinaryHelpers.ByteArrayToInt32LittleEndian(scratch.Buffer, 0);
                if (headerLength > MAX_HEADER_SIZE)
                {
                    throw new InvalidDataException($"Not enough buffer to receive {headerLength} socket audio header bytes");
                }

                bytesRead = await socket.Value.ReadAsync(scratch.Buffer, 0, headerLength, cancelToken, realTime).ConfigureAwait(false);
                string remoteParams = StringUtils.UTF8_WITHOUT_BOM.GetString(scratch.Buffer, 0, headerLength);
                string remoteDuplexString = StringUtils.RegexRip(DUPLEX_PARSER, remoteParams, 1, logger);
                int remoteDuplexInt;
                if (string.IsNullOrEmpty(remoteDuplexString) || !int.TryParse(remoteDuplexString, out remoteDuplexInt))
                {
                    throw new FormatException($"Remote audio endpoint did not send correct duplex: got {remoteParams}");
                }

                NetworkDuplex remoteDuplex = (NetworkDuplex)remoteDuplexInt;
                bool incomingEstablished =
                    ((requestedDuplex & NetworkDuplex.Read) != 0) &&
                    ((remoteDuplex & NetworkDuplex.Write) != 0);
                bool outgoingEstablished =
                    ((requestedDuplex & NetworkDuplex.Write) != 0) &&
                    ((remoteDuplex & NetworkDuplex.Read) != 0);

                if (return_encoder != null)
                {
                    if (!outgoingEstablished)
                    {
                        logger.Log("SocketAudio requested write duplex but remote audio endpoint does not accept audio", LogLevel.Wrn);
                        return_encoder.Dispose();
                        return_encoder = null;
                    }
                    else
                    {
                        logger.Log($"Initializing socket audio output with codec {outgoingCodec}");
                        await return_encoder.Initialize(
                            new SocketStream(socket, logger.Clone("SocketAudioOut"), ownsSocket: false),
                            true,
                            cancelToken,
                            realTime).ConfigureAwait(false);
                    }
                }

                if (((requestedDuplex & NetworkDuplex.Read) != 0) &&
                    ((remoteDuplex & NetworkDuplex.Write) == 0))
                {
                    logger.Log("SocketAudio requested read duplex but remote audio endpoint does not send audio", LogLevel.Wrn);
                }

                if (incomingEstablished)
                {
                    string remoteCodec = StringUtils.RegexRip(CODEC_PARSER, remoteParams, 1, logger);
                    string remoteCodecParams = StringUtils.RegexRip(CODEC_PARAMS_PARSER, remoteParams, 1, logger);
                    if (string.IsNullOrEmpty(remoteCodec) || string.IsNullOrEmpty(remoteCodecParams))
                    {
                        throw new FormatException($"Remote audio endpoint did not send codec or codec params: got {remoteParams}");
                    }

                    if (!codecFactory.CanDecode(remoteCodec))
                    {
                        throw new NotSupportedException($"Remote audio endpoint uses codec {remoteCodec} which is not supported");
                    }

                    return_decoder = codecFactory.CreateDecoder(remoteCodec, remoteCodecParams, incomingAudioGraph, logger, "SocketAudioDecoder");

                    logger.Log($"Initializing socket audio input with codec {remoteCodec}");
                    await return_decoder.Initialize(
                        new SocketStream(socket, logger.Clone("SocketAudioIn"), ownsSocket: false),
                        true,
                        cancelToken,
                        realTime).ConfigureAwait(false);
                }

                NetworkDuplex return_duplex = (NetworkDuplex)(
                    (incomingEstablished ? (int)NetworkDuplex.Read : 0) |
                    (outgoingEstablished ? (int)NetworkDuplex.Write : 0));

                if (return_duplex == NetworkDuplex.Unknown)
                {
                    throw new InvalidOperationException("The negotiated audio endpoint duplex would not allow any audio to pass in either direction.");
                }

                return new HandshakeReturnVal()
                {
                    Encoder = return_encoder,
                    Decoder = return_decoder,
                    EstablishedDuplex = return_duplex,
                };
            }
        }
    }
}
