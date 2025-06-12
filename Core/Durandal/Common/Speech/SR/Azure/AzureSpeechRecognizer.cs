using Durandal.Common.Audio;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Net;
using Durandal.Common.Logger;
using Durandal.Common.Tasks;
using System.Threading;
using Durandal.Common.Utils;
using Newtonsoft.Json;
using Durandal.Common.Time;
using Durandal.API;
using Durandal.Common.Audio.Components;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.Events;
using Durandal.Common.NLP.Language;
using Durandal.Common.Net.WebSocket;
using Durandal.Common.Net.Http;
using Durandal.Common.IO;
using Durandal.Common.Collections;
using Durandal.Common.Cache;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Speech.SR.Azure
{
    public class AzureSpeechRecognizer : AbstractAudioSampleTarget, ISpeechRecognizer
    {
        private static readonly TimeSpan FINAL_READ_TIMEOUT = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan AUDIO_SLICE_SIZE = TimeSpan.FromMilliseconds(50);
        private static readonly IReadThroughCache<ByteArraySegment, string> HEADER_CACHE = new MFUStringCache(StringUtils.ASCII_ENCODING, 16);

        private readonly ILogger _logger;
        private readonly LanguageCode _locale;
        private readonly SemaphoreSlim _finalResponseSignal = new SemaphoreSlim(0, 1); // this is used like a manualreseteventasync, except we want a timeout

        // Used if you want to enforce slices of an exact length to send to the service
        // The official client uses 100ms blocks, but we use 50ms to try and get better latency
        private readonly AudioBlockRectifierBuffer _rectifier;
        private readonly WeakPointer<IAudioGraph> _audioGraph;
        private readonly string _wsConnectionId = Guid.NewGuid().ToString("N");
        private readonly string _audioRequestId = Guid.NewGuid().ToString("N");
        private readonly IWebSocketClientFactory _webSocketFactory;
        private readonly string _authToken;
        private readonly TcpConnectionConfiguration _remoteServerConfig;
        private readonly PipeStream _pcmPipe;
        private readonly PipeStream.PipeReadStream _encodedPcmStream;
        private readonly CancellationTokenSource _readThreadCancel = new CancellationTokenSource();
        private AudioEncoder _pcmEncoder;
        private IWebSocket _webSocket = null;
        private SpeechResponse _finalResponse;
        private int _disposed = 0;

        public AsyncEvent<TextEventArgs> IntermediateResultEvent { get; private set; }

        private AzureSpeechRecognizer(
            IWebSocketClientFactory webSocketFactory,
            WeakPointer<IAudioGraph> audioGraph,
            TcpConnectionConfiguration remoteServerConfig,
            LanguageCode locale,
            ILogger logger,
            string authToken,
            string hostname,
            IRealTimeProvider realTime) : base(audioGraph, nameof(AzureSpeechRecognizer), nodeCustomName: null)
        {
            _webSocketFactory = webSocketFactory.AssertNonNull(nameof(webSocketFactory));
            _audioGraph = audioGraph.AssertNonNull(nameof(audioGraph));
            _logger = logger.AssertNonNull(nameof(logger));
            _locale = locale.AssertNonNull(nameof(locale));
            _authToken = authToken.AssertNonNullOrEmpty(nameof(authToken));
            _remoteServerConfig = remoteServerConfig.AssertNonNull(nameof(remoteServerConfig));
            InputFormat = AudioSampleFormat.Mono(16000);
            _rectifier = new AudioBlockRectifierBuffer(_audioGraph, InputFormat, "AzureSpeechRecoBlockRectifier", AUDIO_SLICE_SIZE);
            _pcmPipe = new PipeStream();
            _encodedPcmStream = _pcmPipe.GetReadStream();
            IntermediateResultEvent = new AsyncEvent<TextEventArgs>();
        }

        private async Task<bool> Initialize(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            string requestUri = "/speech/recognition/interactive/cognitiveservices/v1?format=detailed&language=" + _locale.ToBcp47Alpha2String();
            WebSocketConnectionParams connectionParams = new WebSocketConnectionParams();
            connectionParams.AvailableProtocols = new string[] { "USP" };
            connectionParams.AdditionalHeaders = new HttpHeaders();
            connectionParams.AdditionalHeaders.Add("Authorization", "Bearer " + _authToken);
            //connectionParams.AdditionalHeaders.Add("User-Agent", "Durandal-" + SVNVersionInfo.MajorVersion + "." + SVNVersionInfo.MinorVersion);
            connectionParams.AdditionalHeaders.Add("X-ConnectionId", _wsConnectionId);
            connectionParams.AdditionalHeaders.Add("X-Shoutouts-To", "shortskirts, inque, gargaj, smash, asd, jmspeex");
            _webSocket = await _webSocketFactory.OpenWebSocketConnection(_logger.Clone("SpeechWS"), _remoteServerConfig, requestUri, cancelToken, realTime, connectionParams);

            // Send the context packet over websocket
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            using (PooledBuffer<byte> scratchBuf = BufferPool<byte>.Rent(65536))
            {
                pooledSb.Builder.Append("X-Timestamp:");
                pooledSb.Builder.Append(realTime.Time.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
                pooledSb.Builder.Append("\r\n");
                pooledSb.Builder.Append("Path:speech.config\r\nContent-Type:application/json\r\n\r\n");
                pooledSb.Builder.Append("{\"context\":{\"os\":{\"name\":\"Client\",\"platform\":\"Windows\",\"version\":\"8\"},\"system\":{\"build\":\"Windows-x64\",\"lang\":\"C#\",\"name\":\"SpeechSDK\",\"version\":\"1.2.0\"}}}");

                using (PooledStringBuilderStream stringStream = new PooledStringBuilderStream(pooledSb, StringUtils.UTF8_WITHOUT_BOM, leaveOpen: true))
                {
                    await _webSocket.SendAsync(stringStream, WebSocketMessageType.Text, cancelToken, realTime).ConfigureAwait(false);
                }

                // And also manually format the RIFF header packet
                // We could avoid this by using a RiffWaveEncoder instead of a RawPcmEncoder.
                // However, then because the RIFF header gets mixed in with PCM data, we can't
                // tell exactly the byte alignment of the audio samples, which can cause problem.
                // Plus we know for sure this RIFF header works.
                pooledSb.Builder.Length = 0;
                pooledSb.Builder.Append("X-Timestamp:");
                pooledSb.Builder.Append(realTime.Time.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
                pooledSb.Builder.Append("\r\n");
                pooledSb.Builder.Append("Path:audio\r\nX-StreamId:1\r\nX-RequestId:");
                pooledSb.Builder.Append(_audioRequestId);
                pooledSb.Builder.Append("\r\n");
                string headerString = pooledSb.Builder.ToString();
                ushort headerByteLength = (ushort)StringUtils.UTF8_WITHOUT_BOM.GetBytes(headerString, 0, headerString.Length, scratchBuf.Buffer, 2);
                BinaryHelpers.UInt16ToByteArrayBigEndian(headerByteLength, scratchBuf.Buffer, 0);
                ArrayExtensions.MemCopy(STANDARD_RIFF_HEADER, 0, scratchBuf.Buffer, headerByteLength + 2, STANDARD_RIFF_HEADER.Length);
                await _webSocket.SendAsync(
                    new ArraySegment<byte>(scratchBuf.Buffer, 0, headerByteLength + 2 + STANDARD_RIFF_HEADER.Length),
                    WebSocketMessageType.Binary,
                    cancelToken,
                    realTime).ConfigureAwait(false);
            }

            // Start the read thread
            IRealTimeProvider readThreadTime = realTime.Fork("AzureSRReadThread");
            Task.Run(() => RunReadThread(_readThreadCancel.Token, readThreadTime)).Forget(_logger.Clone("AzureSRReadThread"));

            // And set up the audio pipeline
            _pcmEncoder = new RawPcmEncoder(_audioGraph, InputFormat, "AzureSpeechRecoPcmEncoder");
            AudioInitializationResult initResult = await _pcmEncoder.Initialize(_pcmPipe.GetWriteStream(), false, cancelToken, realTime).ConfigureAwait(false);
            if (initResult == AudioInitializationResult.Success)
            {
                _rectifier.ConnectOutput(_pcmEncoder);
                return true;
            }
            else
            {
                return false;
            }
        }

        private static readonly byte[] STANDARD_RIFF_HEADER = {
            0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45, 0x66, 0x6D, 0x74, 0x20,
            0x10, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x80, 0x3E, 0x00, 0x00, 0x00, 0x7D, 0x00, 0x00,
            0x02, 0x00, 0x10, 0x00, 0x64, 0x61, 0x74, 0x61, 0x00, 0x00, 0x00, 0x00 };

        public static async Task<AzureSpeechRecognizer> OpenConnection(
            WeakPointer<IAudioGraph> audioGraph,
            IWebSocketClientFactory webSocketFactory,
            TcpConnectionConfiguration connectionConfig,
            LanguageCode locale,
            string authToken,
            ILogger logger,
            string hostname,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            AzureSpeechRecognizer speechRecognizer = new AzureSpeechRecognizer(webSocketFactory, audioGraph, connectionConfig, locale, logger, authToken, hostname, realTime);
            try
            {
                bool initializedOk = await speechRecognizer.Initialize(cancelToken, realTime).ConfigureAwait(false);
                if (initializedOk)
                {
                    AzureSpeechRecognizer returnVal = speechRecognizer;
                    speechRecognizer = null;
                    return returnVal;
                }
            }
            finally
            {
                speechRecognizer?.Dispose();
            }

            return null;
        }

        protected override async ValueTask WriteAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            const int REMOTE_ENDPOINT_PACKET_LIMIT = 64000; // Remote endpoint apparently can't handle large single messages, so fragment as needed.
            await _rectifier.WriteAsync(buffer, bufferOffset, numSamplesPerChannel, cancelToken, realTime);
            int bytesCanSendToWebSocket = (int)_encodedPcmStream.Length;
            if (bytesCanSendToWebSocket > 1)
            {
                using (PooledStringBuilder headerBuilder = StringBuilderPool.Rent())
                using (PooledBuffer<byte> scratchBuf = BufferPool<byte>.Rent(65536))
                {
                    do
                    {
                        // Send whatever intermediate audio data we have.
                        headerBuilder.Builder.Length = 0;
                        headerBuilder.Builder.Append("X-Timestamp:");
                        headerBuilder.Builder.Append(realTime.Time.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
                        headerBuilder.Builder.Append("\r\n");
                        headerBuilder.Builder.Append("Path:audio\r\nX-StreamId:1\r\nX-RequestId:");
                        headerBuilder.Builder.Append(_audioRequestId);
                        headerBuilder.Builder.Append("\r\n");
                        string headerString = headerBuilder.Builder.ToString();
                        ushort headerByteLength = (ushort)StringUtils.UTF8_WITHOUT_BOM.GetBytes(headerString, 0, headerString.Length, scratchBuf.Buffer, 2);
                        BinaryHelpers.UInt16ToByteArrayBigEndian(headerByteLength, scratchBuf.Buffer, 0);

                        int audioBytesToSend = Math.Min(bytesCanSendToWebSocket, REMOTE_ENDPOINT_PACKET_LIMIT - headerByteLength - 2);

                        // round down if an uneven number of bytes (so we don't send half of a sample)
                        if ((audioBytesToSend % 2) == 1)
                        {
                            audioBytesToSend--;
                        }

                        int pcmBytesRead = 0;
                        while (pcmBytesRead < audioBytesToSend)
                        {
                            int thisRead = _encodedPcmStream.Read(scratchBuf.Buffer, 2 + headerByteLength + pcmBytesRead, audioBytesToSend - pcmBytesRead);
                            if (thisRead == 0)
                            {
                                // I guess we're done? What can cause this? We just don't want an infinite loop...
                                _logger.Log("Outgoing audio was truncated", LogLevel.Wrn);
                                audioBytesToSend = pcmBytesRead;
                            }

                            pcmBytesRead += thisRead;
                        }

                        await _webSocket.SendAsync(
                            new ArraySegment<byte>(scratchBuf.Buffer, 0, 2 + headerByteLength + audioBytesToSend),
                            WebSocketMessageType.Binary,
                            cancelToken,
                            realTime).ConfigureAwait(false);

                        bytesCanSendToWebSocket = (int)_encodedPcmStream.Length;
                    } while (bytesCanSendToWebSocket > 1) ;
                }
            }
        }
        
        public async Task<SpeechRecognitionResult> FinishUnderstandSpeech(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            try
            {
                // Send an empty audio frame to signal end
                using (PooledStringBuilder headerBuilder = StringBuilderPool.Rent())
                {
                    headerBuilder.Builder.Append("X-Timestamp:");
                    headerBuilder.Builder.Append(realTime.Time.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
                    headerBuilder.Builder.Append("\r\n");
                    headerBuilder.Builder.Append("Path:audio\r\nX-StreamId:1\r\nX-RequestId:");
                    headerBuilder.Builder.Append(_audioRequestId);
                    headerBuilder.Builder.Append("\r\n");
                    string headerString = headerBuilder.Builder.ToString();
                    ushort headerByteLength = (ushort)StringUtils.UTF8_WITHOUT_BOM.GetByteCount(headerString);
                    using (PooledBuffer<byte> scratchBuf = BufferPool<byte>.Rent(2 + headerByteLength))
                    {
                        BinaryHelpers.UInt16ToByteArrayBigEndian(headerByteLength, scratchBuf.Buffer, 0);
                        StringUtils.UTF8_WITHOUT_BOM.GetBytes(headerString, 0, headerString.Length, scratchBuf.Buffer, 2);
                        await _webSocket.SendAsync(scratchBuf.AsArraySegment, WebSocketMessageType.Binary, cancelToken, realTime).ConfigureAwait(false);
                    }
                }

                if (!(await _finalResponseSignal.WaitAsync(FINAL_READ_TIMEOUT).ConfigureAwait(false)))
                {
                    // Final response timed out
                    return new Durandal.API.SpeechRecognitionResult()
                    {
                        RecognitionStatus = API.SpeechRecognitionStatus.BabbleTimeout
                    };
                }

                if (_finalResponse == null || _finalResponse.RecognitionStatus == null)
                {
                    // Reco failed for other reason
                    return new Durandal.API.SpeechRecognitionResult()
                    {
                        RecognitionStatus = API.SpeechRecognitionStatus.Error
                    };
                }

                Durandal.API.SpeechRecognitionResult returnVal = Convert(_finalResponse);

                return returnVal;
            }
            finally
            {
                await _webSocket.CloseWrite(cancelToken, realTime, WebSocketCloseReason.NormalClosure).ConfigureAwait(false);
            }
        }
        
        protected override void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            try
            {
                if (disposing)
                {
                    _webSocket?.Dispose();
                    _finalResponseSignal.Dispose();
                    _readThreadCancel.Cancel();
                    _readThreadCancel.Dispose();
                    _rectifier.Dispose();
                    _pcmEncoder?.Dispose();
                    _pcmPipe?.Dispose();
                    _encodedPcmStream?.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        private async Task RunReadThread(CancellationToken readThreadCancel, IRealTimeProvider readThreadTime)
        {
            try
            {
                bool moreToRead = true;
                while (!readThreadCancel.IsCancellationRequested && moreToRead)
                {
                    using (WebSocketBufferResult bufferResult = await _webSocket.ReceiveAsBufferAsync(readThreadCancel, readThreadTime).ConfigureAwait(false))
                    {
                        moreToRead = bufferResult.Success;

                        if (moreToRead)
                        {
                            //_logger.Log("GOT PACKET: " + Enum.GetName(typeof(WebSocketMessageType), bufferResult.MessageType));
                            int endOfHeaders;
                            HttpHeaders parsedHeaders = HttpHelpers.ParseHttpHeaders(bufferResult.Result.Buffer, bufferResult.Result.Length, 2, out endOfHeaders, HEADER_CACHE);
                            
                            if (parsedHeaders == null)
                            {
                                _logger.Log("Got packet with but could not parse headers", LogLevel.Err);
                                continue;
                            }

                            if (!parsedHeaders.ContainsKey("Path"))
                            {
                                _logger.Log("Got packet with no Path header", LogLevel.Err);
                                continue;
                            }

                            string payload = StringUtils.UTF8_WITHOUT_BOM.GetString(bufferResult.Result.Buffer, endOfHeaders, bufferResult.Result.Length - endOfHeaders);
                            //_logger.Log(payload);

                            string path = parsedHeaders["Path"];
                            if (string.Equals(path, "turn.start", StringComparison.Ordinal) ||
                                string.Equals(path, "speech.startDetected", StringComparison.Ordinal) ||
                                string.Equals(path, "speech.endDetected", StringComparison.Ordinal))
                            {
                                moreToRead = true;
                            }
                            else if (string.Equals(path, "speech.hypothesis", StringComparison.Ordinal))
                            {
                                SpeechHypothesis response = JsonConvert.DeserializeObject<SpeechHypothesis>(payload);
                                if (response != null && !string.IsNullOrEmpty(response.Text))
                                {
                                    IntermediateResultEvent.FireInBackground(this, new TextEventArgs(response.Text), _logger, readThreadTime);
                                }

                                moreToRead = true;
                            }
                            else if (string.Equals(path, "speech.phrase", StringComparison.Ordinal))
                            {
                                SpeechResponse response = JsonConvert.DeserializeObject<SpeechResponse>(payload);
                                _finalResponse = response;
                                _finalResponseSignal.Release();
                                moreToRead = true;
                            }
                            else if (string.Equals(path, "turn.end", StringComparison.Ordinal))
                            {
                                moreToRead = false;
                            }
                            else
                            {
                                _logger.Log("Got unknown service response type " + path, LogLevel.Wrn);
                            }
                        }
                        else
                        {
                            //_logger.Log("GOT CLOSE: " + Enum.GetName(typeof(WebSocketCloseReason), bufferResult.CloseReason.GetValueOrDefault(WebSocketCloseReason.Empty)));
                            //if (bufferResult.CloseMessage != null)
                            //{
                            //    _logger.Log(bufferResult.CloseMessage);
                            //}
                        }
                    }
                }
            }
            finally
            {
                readThreadTime.Merge();
            }
        }

        private Durandal.API.SpeechRecognitionResult Convert(SpeechResponse input)
        {
            Durandal.API.SpeechRecognitionResult returnVal = new Durandal.API.SpeechRecognitionResult();
            
            if (input != null)
            {
                switch (input.RecognitionStatus)
                {
                    case "Success":
                        returnVal.RecognitionStatus = Durandal.API.SpeechRecognitionStatus.Success;
                        break;
                    default:
                        returnVal.RecognitionStatus = Durandal.API.SpeechRecognitionStatus.Error;
                        break;
                }

                if (input.NBest != null)
                {
                    foreach (var inputPhrase in input.NBest)
                    {
                        Durandal.API.SpeechRecognizedPhrase newPhrase = new Durandal.API.SpeechRecognizedPhrase();
                        newPhrase.AudioTimeLength = input.Duration.HasValue ? (TimeSpan?)TimeSpan.FromTicks(input.Duration.Value) : null;
                        newPhrase.AudioTimeOffset = input.Offset.HasValue ? (TimeSpan?)TimeSpan.FromTicks(input.Offset.Value) : null;
                        newPhrase.DisplayText = inputPhrase.Display;
                        newPhrase.InverseTextNormalizationResults = new List<string>() { inputPhrase.ITN };
                        newPhrase.LexicalForm = inputPhrase.Lexical;
                        newPhrase.IPASyllables = string.Empty; // FIXME this is a problem
                        newPhrase.Locale = _locale.ToBcp47Alpha2String(); // FIXME So is this
                        newPhrase.MaskedInverseTextNormalizationResults = new List<string>() { inputPhrase.MaskedITN };
                        newPhrase.ProfanityTags = null;
                        newPhrase.SREngineConfidence = inputPhrase.Confidence;
                        returnVal.RecognizedPhrases.Add(newPhrase);
                    }

                    returnVal.RecognizedPhrases.Sort((a, b) => b.SREngineConfidence.CompareTo(a.SREngineConfidence));
                }
            }

            return returnVal;
        }
    }
}