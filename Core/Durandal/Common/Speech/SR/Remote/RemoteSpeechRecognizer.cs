using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Durandal.Common.Speech.SR.Remote
{
    using Durandal.Common.Audio;
    using Newtonsoft.Json;
    using Durandal.Common.Logger;
    using Durandal.Common.Net;
    using Durandal.Common.File;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using Durandal.API;
    using Remoting;
    using Durandal.Common.Utils;
    using Durandal.Common.Audio.Components;
    using System.IO;
    using Durandal.Common.Speech.SR.Azure;
    using Durandal.Common.Client;
    using Durandal.Common.Collections;
    using Durandal.Common.IO;
    using Durandal.Common.Events;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.ServiceMgmt;

    public sealed class RemoteSpeechRecognizer : AbstractAudioSampleTarget, ISpeechRecognizer
    {
        private readonly ILogger _logger;
        private readonly WeakPointer<ISocketFactory> _socketProvider;
        private readonly IAudioCodecFactory _codecFactory;
        private readonly int _readTimeoutMs;
        private readonly string _remoteHost;
        private readonly int _remotePort;
        private readonly string _codecToUse;

        private AudioEncoder _encoder;
        private AudioConformer _conformer;
        private RemoteSpeechRecognitionStream _outputStream;
        private int _disposed = 0;

        public AsyncEvent<TextEventArgs> IntermediateResultEvent
        {
            get;
            private set;
        }

        public RemoteSpeechRecognizer(
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat inputAudioFormat,
            string nodeCustomName,
            IAudioCodecFactory codecFactory,
            string codecToUse,
            WeakPointer<ISocketFactory> socketProvider,
            string remoteHost,
            int remotePort,
            ILogger logger,
            int timeoutMs = 3000) : base(graph, nameof(RemoteSpeechRecognizer), nodeCustomName)
        {
            _logger = logger;
            _socketProvider = socketProvider;
            _codecFactory = codecFactory;
            _codecToUse = codecToUse;
            _readTimeoutMs = timeoutMs;
            _remoteHost = remoteHost;
            _remotePort = remotePort;
            InputFormat = inputAudioFormat;
            IntermediateResultEvent = new AsyncEvent<TextEventArgs>();
        }

        public async Task Initialize(LanguageCode locale, WeakPointer<IAudioGraph> audioGraph, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            ISocket webSocket = null;
            PostOffice postOffice = null;
            _logger.Log("Connecting to remote SR service at " + _remoteHost + ":" + _remotePort, LogLevel.Vrb);

            try
            {
                webSocket = await _socketProvider.Value.Connect(_remoteHost, _remotePort, false, _logger, cancelToken, realTime).ConfigureAwait(false);
                if (webSocket == null)
                {
                    _logger.Log("Remote SR connection failed", LogLevel.Err);
                    return;
                }

                postOffice = new PostOffice(
                    webSocket,
                    _logger,
                    TimeSpan.FromSeconds(30),
                    isServer: false, 
                    realTime: realTime,
                    metrics: default(WeakPointer<IMetricCollector>),
                    metricDimensions: null,
                    useDedicatedThread: false);
                MailboxId mailboxId = postOffice.CreateTransientMailbox(realTime);

                //_logger.Log("Sending SR_START", LogLevel.Vrb);
                byte[] localeField = Encoding.UTF8.GetBytes(locale.ToBcp47Alpha2String());
                PooledBuffer<byte> smallBuf = BufferPool<byte>.Rent(localeField.Length);
                ArrayExtensions.MemCopy(localeField, 0, smallBuf.Buffer, 0, localeField.Length);
                MailboxMessage message = new MailboxMessage(mailboxId, SRMessageType.SR_START, smallBuf, postOffice.GenerateMessageId());
                await postOffice.SendMessage(message, cancelToken, realTime).ConfigureAwait(false);

                _encoder = _codecFactory.CreateEncoder(_codecToUse, audioGraph, InputFormat, _logger, "RemoteSpeechRecoEncoder");
                await _encoder.Initialize(_outputStream, false, cancelToken, realTime).ConfigureAwait(false);
                _conformer = new AudioConformer(audioGraph, InputFormat, _encoder.InputFormat, "RemoteSpeechRecoConformer", _logger.Clone("RemoteSRConformer"), resamplerQuality: AudioProcessingQuality.Balanced);
                _conformer.ConnectOutput(_encoder);

                _outputStream = new RemoteSpeechRecognitionStream(
                    _encoder,
                    webSocket,
                    postOffice,
                    mailboxId,
                    _logger,
                    _readTimeoutMs,
                    IntermediateResultEvent);

                webSocket = null;
                postOffice = null;
            }
            finally
            {
                webSocket?.Dispose();
                postOffice?.Dispose();
            }
        }

        protected override ValueTask WriteAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _conformer.WriteAsync(buffer, bufferOffset, numSamplesPerChannel, cancelToken, realTime);
        }

        protected override ValueTask FlushAsyncInternal(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _conformer.FlushAsync(cancelToken, realTime);
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
                    _outputStream?.Dispose();
                    _conformer?.Dispose();
                    _encoder?.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        public Task<SpeechRecognitionResult> FinishUnderstandSpeech(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _outputStream.FinishSpeechRecognition(cancelToken, realTime);
        }

        private class RemoteSpeechRecognitionStream : NonRealTimeStream
        {
            private readonly AudioEncoder _encoder;
            private readonly ISocket _webSocket;
            private readonly PostOffice _postOffice;
            private readonly MailboxId _mailboxId;
            private readonly ILogger _logger;
            private readonly int _readTimeoutMs;
            private readonly AsyncEvent<TextEventArgs> _partialResultAvailableEvent;
            private bool _sentAudioHeader = false;
            private int _disposed = 0;

            public RemoteSpeechRecognitionStream(
                AudioEncoder encoder,
                ISocket webSocket,
                PostOffice postOffice,
                MailboxId mailboxId,
                ILogger logger,
                int readTimeoutMs,
                AsyncEvent<TextEventArgs> partialResultAvailableEvent)
            {
                _encoder = encoder;
                _webSocket = webSocket;
                _postOffice = postOffice;
                _mailboxId = mailboxId;
                _logger = logger;
                _readTimeoutMs = readTimeoutMs;
                _partialResultAvailableEvent = partialResultAvailableEvent;
                DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
            }

            public override bool CanRead => false;

            public override bool CanSeek => false;

            public override bool CanWrite => true;

            public override long Length => 0L;

            public override long Position
            {
                get
                {
                    return 0L;
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            public override int Read(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                throw new NotImplementedException();
            }

            public override Task<int> ReadAsync(byte[] targetBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                throw new NotImplementedException();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotImplementedException();
            }

            public override void SetLength(long value)
            {
                throw new NotImplementedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                WriteAsync(buffer, offset, count).Await();
            }

            public override void Write(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                WriteAsync(sourceBuffer, offset, count, cancelToken, realTime).Await();
            }

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancelToken)
            {
                return WriteAsync(buffer, offset, count, cancelToken, DefaultRealTimeProvider.Singleton);
            }

            public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                try
                {
                    // Send the audio header if this is the first message
                    if (!_sentAudioHeader)
                    {
                        string header = _encoder.Codec + "|" + _encoder.CodecParams;
                        byte[] encodeParamsField = Encoding.UTF8.GetBytes(header);
                        PooledBuffer<byte> smallBuf = BufferPool<byte>.Rent(encodeParamsField.Length);
                        ArrayExtensions.MemCopy(encodeParamsField, 0, smallBuf.Buffer, 0, encodeParamsField.Length);
                        //_logger.Log("Sending SR_SEND_AUDIOHEADER with length " + encodeParamsField.Length, LogLevel.Vrb);
                        MailboxMessage message = new MailboxMessage(_mailboxId, SRMessageType.SR_SEND_AUDIOHEADER, smallBuf, _postOffice.GenerateMessageId());
                        await _postOffice.SendMessage(message, cancelToken, realTime).ConfigureAwait(false);
                        _sentAudioHeader = true;
                    }

                    if (count > 0)
                    {
                        PooledBuffer<byte> payload = BufferPool<byte>.Rent(count);
                        ArrayExtensions.MemCopy(buffer, offset, payload.Buffer, 0, count);
                        //_logger.Log("Sending SR_SEND_AUDIO with length " + payload.Length, LogLevel.Vrb);
                        MailboxMessage message = new MailboxMessage(_mailboxId, SRMessageType.SR_SEND_AUDIO, payload, _postOffice.GenerateMessageId());
                        await _postOffice.SendMessage(message, cancelToken, realTime).ConfigureAwait(false);
                    }

                    //_logger.Log("Checking tentatively for any responses", LogLevel.Vrb);

                    RetrieveResult<MailboxMessage> anyResponse = await _postOffice.TryReceiveMessage(_mailboxId, cancelToken, TimeSpan.Zero, realTime).ConfigureAwait(false);
                    if (anyResponse.Success)
                    {
                        //_logger.Log("Got interstitial response", LogLevel.Vrb);
                        MailboxMessage gotMessage = anyResponse.Result;
                        if (gotMessage.ProtocolId == SRMessageType.SR_PARTIALRESULT && gotMessage.Buffer != null && gotMessage.Buffer.Length > 0)
                        {
                            string partialResult = Encoding.UTF8.GetString(gotMessage.Buffer.Buffer, 0, gotMessage.Buffer.Length);
                            //_logger.Log("Got partial result (a) " + _partialResult, LogLevel.Vrb);
                            _partialResultAvailableEvent.FireInBackground(this, new TextEventArgs(partialResult), _logger, realTime);
                        }

                        gotMessage.DisposeOfBuffer();
                    }
                }
                catch (Exception e)
                {
                    _logger.Log("Error while sending SR data: " + e.Message, LogLevel.Err);
                    _logger.Log(e.StackTrace, LogLevel.Err);
                }
            }

            public async Task<SpeechRecognitionResult> FinishSpeechRecognition(CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                if (_webSocket == null)
                {
                    _logger.Log("No remote SR connection; speech reco results are null", LogLevel.Err);
                    return new SpeechRecognitionResult()
                    {
                        RecognitionStatus = SpeechRecognitionStatus.Error
                    };
                }

                try
                {
                    //_logger.Log("Sending SR_SEND_FINALAUDIO of size " + finalPayload.Length, LogLevel.Vrb);
                    MailboxMessage message = new MailboxMessage(_mailboxId, SRMessageType.SR_SEND_FINALAUDIO, BufferPool<byte>.Rent(0), _postOffice.GenerateMessageId());
                    await _postOffice.SendMessage(message, cancelToken, realTime).ConfigureAwait(false);

                    // Send the close signal
                    //_logger.Log("Sending CLOSE_SOCKET", LogLevel.Vrb);
                    message = new MailboxMessage(_mailboxId, SRMessageType.CLOSE_SOCKET, BufferPool<byte>.Rent(0), _postOffice.GenerateMessageId());
                    await _postOffice.SendMessage(message, cancelToken, realTime).ConfigureAwait(false);

                    // And wait for the final response
                    //_logger.Log("Waiting for final remote SR response...", LogLevel.Vrb);
                    long startTime = realTime.TimestampMilliseconds;
                    try
                    {
                        while (realTime.TimestampMilliseconds < startTime + _readTimeoutMs)
                        {
                            RetrieveResult<MailboxMessage> anyResponse = await _postOffice.TryReceiveMessage(
                                _mailboxId,
                                cancelToken,
                                TimeSpan.FromMilliseconds(_readTimeoutMs),
                                realTime).ConfigureAwait(false);
                            if (anyResponse.Success)
                            {
                                MailboxMessage gotMessage = anyResponse.Result;
                                try
                                {
                                    //_logger.Log("Got message " + gotMessage.ProtocolId + " with payload " + (gotMessage.Payload != null ? gotMessage.Payload.Length : 0));
                                    if (gotMessage.ProtocolId == SRMessageType.SR_PARTIALRESULT)
                                    {
                                        if (gotMessage.Buffer != null && gotMessage.Buffer.Length > 0)
                                        {
                                            string partialResult = Encoding.UTF8.GetString(gotMessage.Buffer.Buffer, 0, gotMessage.Buffer.Length);
                                            //_logger.Log("Got partial result (b) " + _partialResult, LogLevel.Vrb);
                                            _partialResultAvailableEvent.FireInBackground(this, new TextEventArgs(partialResult), _logger, realTime);
                                        }
                                    }
                                    else if (gotMessage.ProtocolId == SRMessageType.SR_FINALRESULT)
                                    {
                                        string responseJson = Encoding.UTF8.GetString(gotMessage.Buffer.Buffer, 0, gotMessage.Buffer.Length);
                                        //_logger.Log("Got final result " + responseJson, LogLevel.Vrb);
                                        SpeechRecognitionResult results = JsonConvert.DeserializeObject<SpeechRecognitionResult>(responseJson);
                                        //_logger.Log("Parsed " + results.RecognizedPhrases.Count + " SR results", LogLevel.Vrb);
                                        return results;
                                    }
                                    else
                                    {
                                        _logger.Log("Unknown SR message type " + gotMessage.ProtocolId, LogLevel.Err);
                                    }
                                }
                                finally
                                {
                                    gotMessage.DisposeOfBuffer();
                                }
                            }
                            else
                            {
                                await realTime.WaitAsync(TimeSpan.FromMilliseconds(5), cancelToken).ConfigureAwait(false);
                            }
                        }

                        _logger.Log("SR timed out while waiting for the final response", LogLevel.Err);
                    }
                    catch (Exception e)
                    {
                        _logger.Log(e, LogLevel.Err);
                        return new SpeechRecognitionResult()
                        {
                            RecognitionStatus = SpeechRecognitionStatus.Error
                        };
                    }
                }
                catch (Exception e)
                {
                    _logger.Log("Error while connecting to SR: " + e.GetType().Name + " " + e.Message, LogLevel.Err);
                    _logger.Log(e.StackTrace, LogLevel.Err);
                }

                return new SpeechRecognitionResult()
                {
                    RecognitionStatus = SpeechRecognitionStatus.Error
                };
            }

            protected override void Dispose(bool disposing)
            {
                if (!AtomicOperations.ExecuteOnce(ref _disposed))
                {
                    return;
                }

                try
                {
                    DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

                    if (disposing)
                    {
                        _postOffice?.Dispose();
                        _logger.Log("Disconnecting SR socket");
                        _webSocket?.Disconnect(CancellationToken.None, DefaultRealTimeProvider.Singleton, NetworkDuplex.ReadWrite, allowLinger: false).Await();
                    }
                }
                finally
                {
                    base.Dispose(disposing);
                }
            }
        }
    }
}
