using Durandal.Common.Events;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Components.NetworkAudio
{
    /// <summary>
    /// Represents a network connected endpoint (over socket, WebSocket, HTTP, etc.)
    /// over which encoded audio data may be sent or transmitted.
    /// The encoding is done at the network layer, and this endpoint exposes
    /// graph components for plain audio input/output. Ownership of this object implies
    /// ownership of potentially multiple graph components - you should only need to
    /// dispose of this to dispose of everything encompassed by it.
    /// </summary>
    public abstract class NetworkAudioEndpoint : IDisposable
    {
        private readonly WeakPointer<ISocket> _socket;
        private readonly ILogger _logger;
        private readonly NetworkDuplex _duplex;
        private readonly string _nodeCustomNameBase;

        // Incoming audio components
        private readonly WeakPointer<IAudioGraph> _inputGraph;
        private readonly AudioDecoder _decoder;
        private AudioExceptionCircuitBreaker _inputCircuitBreaker;

        // Outgoing audio components
        private readonly WeakPointer<IAudioGraph> _outputGraph;
        private readonly AudioEncoder _encoder;
        private AudioExceptionCircuitBreaker _outputCircuitBreaker;

        private int _disposed = 0;

        protected NetworkAudioEndpoint(
            WeakPointer<ISocket> socket,
            ILogger logger,
            NetworkDuplex duplex,
            WeakPointer<IAudioGraph> inputGraph,
            AudioDecoder inputDecoder,
            WeakPointer<IAudioGraph> outputGraph,
            AudioEncoder outputEncoder,
            string nodeCustomNameBase)
        {
            Disconnected = new AsyncEvent<EventArgs>();
            _socket = socket.AssertNonNull(nameof(socket));
            _logger = logger.AssertNonNull(nameof(logger));
            _duplex = duplex;
            _nodeCustomNameBase = nodeCustomNameBase ?? string.Empty;

            if (_duplex.HasFlag(NetworkDuplex.Read))
            {
                // Incoming audio enabled
                _inputGraph = inputGraph.AssertNonNull(nameof(inputGraph));
                _decoder = inputDecoder.AssertNonNull(nameof(inputDecoder));
                if (!_decoder.IsInitialized)
                {
                    throw new ArgumentException("Audio decoder must be initialized before creating a network audio endpoint");
                }

                _inputCircuitBreaker = new AudioExceptionCircuitBreaker(_inputGraph, _decoder.OutputFormat, _nodeCustomNameBase + "InputCB", _logger.Clone("NetAudioInErrors"));
                _inputCircuitBreaker.ExceptionRaisedEvent.Subscribe(OnNetworkDisconnected);
                _decoder.ConnectOutput(_inputCircuitBreaker);
                IncomingAudio = _inputCircuitBreaker;
            }

            if (_duplex.HasFlag(NetworkDuplex.Write))
            {
                // Outgoing audio enabled
                _outputGraph = outputGraph.AssertNonNull(nameof(outputGraph));
                _encoder = outputEncoder.AssertNonNull(nameof(outputEncoder));
                if (!_encoder.IsInitialized)
                {
                    throw new ArgumentException("Audio encoder must be initialized before creating a network audio endpoint");
                }

                _outputCircuitBreaker = new AudioExceptionCircuitBreaker(_outputGraph, _encoder.InputFormat, _nodeCustomNameBase + "OutputCB", _logger.Clone("NetAudioOutErrors"));
                _outputCircuitBreaker.ExceptionRaisedEvent.Subscribe(OnNetworkDisconnected);
                _outputCircuitBreaker.ConnectOutput(_encoder);
                OutgoingAudio = _outputCircuitBreaker;
            }

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~NetworkAudioEndpoint()
        {
            Dispose(false);
        }
#endif

        /// <summary>
        /// This event fires when the underlying network connection has been disconnected
        /// by the remote host and further audio input/output will be truncated. This event
        /// implies that the corresponding graph components should be disconnected
        /// as they will no longer function.
        /// </summary>
        public AsyncEvent<EventArgs> Disconnected { get; private set; }

        /// <summary>
        /// Gets the duplex status of this endpoint, indicating whether this endpoint
        /// will send audio, receive it, or both.
        /// </summary>
        public NetworkDuplex Duplex => _duplex;

        /// <summary>
        /// If the duplex indicates incoming audio, this is the audio graph component
        /// for which that input will come. Otherwise, it will be null.
        /// The input component will provide a guarantee that it will
        /// not throw exceptions outside of its own graph boundary, so you shouldn't need
        /// to wrap it with an exception handling component.
        /// </summary>
        public IAudioSampleSource IncomingAudio { get; protected set; }

        /// <summary>
        /// If the duplex indicates outgoing audio, this is the audio graph component
        /// for which audio output will be written. Otherwise, it will be null.
        /// The output component will provide a guarantee that it will
        /// not throw exceptions outside of its own graph boundary, so you shouldn't need
        /// to wrap it with an exception handling component. However, it will not
        /// provide async buffering for you automatically, so writes may be
        /// unpredictably slow.
        /// </summary>
        public IAudioSampleTarget OutgoingAudio { get; protected set; }

        public async Task CloseWrite(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_duplex.HasFlag(NetworkDuplex.Write))
            {
                await _encoder.Finish(cancelToken, realTime).ConfigureAwait(false);
                await _socket.Value.Disconnect(cancelToken, realTime, NetworkDuplex.Write, allowLinger: false).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
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
                try
                {
                    _socket.Value?.Disconnect(CancellationToken.None, DefaultRealTimeProvider.Singleton).Await();

                    if (_duplex.HasFlag(NetworkDuplex.Read))
                    {
                        _inputCircuitBreaker.DisconnectOutput();
                        _decoder?.Dispose();
                        _inputCircuitBreaker?.Dispose();
                    }

                    if (_duplex.HasFlag(NetworkDuplex.Write))
                    {
                        _outputCircuitBreaker.DisconnectInput();
                        _encoder?.Dispose();
                        _outputCircuitBreaker?.Dispose();
                    }
                }
                catch (Exception e)
                {
                    _logger.Log(e);
                }
            }
        }

        private async Task OnNetworkDisconnected(object sender, EventArgs args, IRealTimeProvider realTime)
        {
            await Disconnected.Fire(this, args, realTime);
        }
    }
}
