using Durandal.Common.Tasks;

namespace Durandal.Common.Speech.SR.Remote
{
    using Durandal.Common.Audio;
    using Durandal.Common.Audio.Codecs;
    using Durandal.Common.Logger;
    using Durandal.Common.Net;
    using Durandal.Common.Speech;
    using Durandal.Common.Speech.SR;
    using Durandal.Common.Utils;
    using Durandal.Common.File;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using System.Threading;
    using Durandal.API;
    using Remoting;
    using Durandal.Common.Time;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.IO;
    using System.Linq;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.Collections;
    using Durandal.Common.ServiceMgmt;

    /// <summary>
    /// Socket server that handles remote clients that want to do streaming speech recognition but can't implement proper speech reco natively.
    /// </summary>
    public class SRProxyServer : IServer, ISocketServerDelegate
    {
        private const int MAX_CONNECTIONS = 100;

        private static readonly JsonSerializer JSON_SERIALIZER = new JsonSerializer();

        private ILogger _logger;
        private ISocketServer _serverBase;
        private IAudioCodecFactory _codecFactory;
        private ISpeechRecognizerFactory _speechRecoFactory;
        private int _disposed = 0;

        public SRProxyServer(
            ISocketServer serverImpl,
            ISpeechRecognizerFactory proxyTo,
            IAudioCodecFactory codecs,
            ILogger logger,
            DimensionSet dimensions)
        {
            _serverBase = serverImpl;
            _serverBase.RegisterSubclass(this);
            _logger = logger;
            _codecFactory = codecs;
            _speechRecoFactory = proxyTo;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~SRProxyServer()
        {
            Dispose(false);
        }
#endif

        public IEnumerable<ServerBindingInfo> Endpoints
        {
            get
            {
                return _serverBase.Endpoints;
            }
        }

        public bool Running
        {
            get
            {
                return _serverBase.Running;
            }
        }

        public async Task HandleSocketConnection(ISocket clientSocket, ServerBindingInfo socketBinding, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (clientSocket == null)
            {
                _logger.Log("SR server encountered a closed or erroneous connection (AcceptSocket is null)", LogLevel.Err);
                return;
            }

            clientSocket.ReceiveTimeout = 10000;
            _logger.Log("Got a new connection from " + clientSocket.RemoteEndpointString);

            try
            {
                PostOffice serverPostOffice = new PostOffice(
                    clientSocket,
                    _logger,
                    TimeSpan.FromSeconds(30),
                    isServer: true,
                    realTime: realTime,
                    metrics: default(WeakPointer<IMetricCollector>),
                    metricDimensions: null,
                    useDedicatedThread: false);

                ISpeechRecognizer recognizer = null;
                AudioDecoder inputDecoder = null;
                int bytesRemainingToReadFromSocket = 600;
                using (IAudioGraph audioGraph = new AudioGraph(AudioGraphCapabilities.None))
                using (PipeStream encodedAudioPipe = new PipeStream())
                using (NonRealTimeStream pipeWriteStream = encodedAudioPipe.GetWriteStream())
                {
                    try
                    {
                        bool running = true;

                        DateTimeOffset endTime = realTime.Time.AddSeconds(60);
                        MailboxId mailboxId = await serverPostOffice.WaitForMessagesOnNewMailbox(cancelToken, realTime).ConfigureAwait(false);

                        while (running)
                        {
                            RetrieveResult<MailboxMessage> anyMessage = await serverPostOffice.TryReceiveMessage(
                                mailboxId, cancelToken, TimeSpan.FromSeconds(30), realTime).ConfigureAwait(false);

                            if (!anyMessage.Success)
                            {
                                _logger.Log("SR socket read timed out", LogLevel.Err);
                                return;
                            }

                            MailboxMessage mailboxMessage = anyMessage.Result;
                            //_logger.Log("Got a message of size " + mailboxMessage.Payload.Length + " with type " + mailboxMessage.ProtocolId, LogLevel.Vrb);

                            if (mailboxMessage.ProtocolId == SRMessageType.CLOSE_SOCKET)
                            {
                                running = false;
                            }
                            else if (mailboxMessage.ProtocolId == SRMessageType.SR_START)
                            {
                                LanguageCode locale = LanguageCode.TryParse(Encoding.UTF8.GetString(mailboxMessage.Buffer.Buffer, 0, mailboxMessage.Buffer.Length));
                                recognizer = await _speechRecoFactory.CreateRecognitionStream(new WeakPointer<IAudioGraph>(audioGraph), "ProxiedSRImplementation", locale, _logger, cancelToken, realTime).ConfigureAwait(false);
                            }
                            else if (mailboxMessage.ProtocolId == SRMessageType.SR_SEND_AUDIOHEADER)
                            {
                                string headerString = Encoding.UTF8.GetString(mailboxMessage.Buffer.Buffer, 0, mailboxMessage.Buffer.Length);
                                _logger.Log("The audio header is " + headerString, LogLevel.Vrb);
                                int separator = headerString.IndexOf('|');
                                string codec = headerString.Substring(0, separator);
                                string encodeParams = headerString.Substring(separator);
                                if (!_codecFactory.CanDecode(codec))
                                {
                                    _logger.Log("The audio format \"" + codec + "\" is unsupported", LogLevel.Wrn);
                                    break;
                                }
                                else
                                {
                                    inputDecoder = _codecFactory.CreateDecoder(codec, encodeParams, new WeakPointer<IAudioGraph>(audioGraph), _logger, "SRProxyInputDecoder");
                                }
                            }
                            else if (mailboxMessage.ProtocolId == SRMessageType.SR_SEND_AUDIO)
                            {
                                // Decode the audio and send to SR
                                await pipeWriteStream.WriteAsync(mailboxMessage.Buffer.Buffer, 0, mailboxMessage.Buffer.Length).ConfigureAwait(false);
                                if (bytesRemainingToReadFromSocket > 0)
                                {
                                    bytesRemainingToReadFromSocket -= mailboxMessage.Buffer.Length;
                                    if (bytesRemainingToReadFromSocket <= 0)
                                    {
                                        await inputDecoder.Initialize(encodedAudioPipe.GetReadStream(), true, cancelToken, realTime).ConfigureAwait(false);
                                        inputDecoder.ConnectOutput(recognizer);
                                        recognizer.IntermediateResultEvent.Subscribe(async (source, args, time) =>
                                        {
                                            string partialResult = args.Text;
                                            if (!string.IsNullOrEmpty(partialResult))
                                            {
                                                byte[] partialResultBytes = Encoding.UTF8.GetBytes(partialResult);
                                                PooledBuffer<byte> smallBuf = BufferPool<byte>.Rent(partialResultBytes.Length);
                                                ArrayExtensions.MemCopy(partialResultBytes, 0, smallBuf.Buffer, 0, partialResultBytes.Length);
                                                // Send the partial result back to the client
                                                //_logger.Log("Sending SR_PARTIALRESULT with length " + partialResultBytes.Length);
                                                MailboxMessage message = new MailboxMessage(mailboxId, SRMessageType.SR_PARTIALRESULT, smallBuf, serverPostOffice.GenerateMessageId(), mailboxMessage.MessageId);
                                                await serverPostOffice.SendMessage(message, cancelToken, realTime).ConfigureAwait(false);
                                            }
                                        });
                                    }
                                }

                                // pipe data from the decoder to the recognizer
                                using (PooledBuffer<float> pooledBuf = BufferPool<float>.Rent(65536 * recognizer.InputFormat.NumChannels))
                                {
                                    int amountRead = await inputDecoder.ReadAsync(pooledBuf.Buffer, 0, 65536, cancelToken, realTime).ConfigureAwait(false);
                                    if (amountRead > 0)
                                    {
                                        await recognizer.WriteAsync(pooledBuf.Buffer, 0, amountRead, cancelToken, realTime).ConfigureAwait(false);
                                    }
                                }
                            }
                            else if (mailboxMessage.ProtocolId == SRMessageType.SR_SEND_FINALAUDIO)
                            {
                                if (mailboxMessage.Buffer == null || mailboxMessage.Buffer.Length == 0)
                                {
                                    _logger.Log("Final audio is null; just closing the stream as-is");
                                }
                                else
                                {
                                    await pipeWriteStream.WriteAsync(mailboxMessage.Buffer.Buffer, 0, mailboxMessage.Buffer.Length).ConfigureAwait(false);
                                }

                                pipeWriteStream.Dispose();
                                await inputDecoder.ReadFully(cancelToken, realTime).ConfigureAwait(false);

                                //_logger.Log("Total audio bytes received was " + thisContext.InputByteCount, LogLevel.Vrb);

                                SpeechRecognitionResult finalResults = await recognizer.FinishUnderstandSpeech(cancelToken, realTime).ConfigureAwait(false);

                                if (finalResults != null && finalResults.RecognizedPhrases != null && finalResults.RecognizedPhrases.Count > 0)
                                {
                                    _logger.Log("Final reco result: " + finalResults.RecognizedPhrases[0].DisplayText);
                                }

                                using (RecyclableMemoryStream memoryStream = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
                                using (Utf8StreamWriter writer = new Utf8StreamWriter(memoryStream))
                                using (JsonTextWriter jsonWriter = new JsonTextWriter(writer))
                                {
                                    JSON_SERIALIZER.Serialize(jsonWriter, finalResults);
                                    PooledBuffer<byte> serializedJsonResponse = memoryStream.ToPooledBuffer();

                                    // Send final result back to the client
                                    MailboxMessage message = new MailboxMessage(mailboxId, SRMessageType.SR_FINALRESULT, serializedJsonResponse, serverPostOffice.GenerateMessageId(), mailboxMessage.MessageId);
                                    await serverPostOffice.SendMessage(message, cancelToken, realTime).ConfigureAwait(false);
                                }
                            }
                            else
                            {
                                _logger.Log("Unrecognized message of type " + mailboxMessage.ProtocolId, LogLevel.Wrn);
                            }

                            mailboxMessage.DisposeOfBuffer();
                        }
                    }
                    finally
                    {
                        recognizer?.Dispose();
                        serverPostOffice.Dispose();
                        _logger.Log("Closing connection to " + clientSocket.RemoteEndpointString);
                        await clientSocket.Disconnect(cancelToken, realTime, NetworkDuplex.ReadWrite, allowLinger: false).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.Log(e, LogLevel.Err);
            }
        }

        public Task<bool> StartServer(string serverName, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _serverBase.StartServer(serverName, cancelToken, realTime);
        }

        public Task StopServer(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _serverBase.StopServer(cancelToken, realTime);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                _serverBase.Dispose();
            }
        }
    }
}
