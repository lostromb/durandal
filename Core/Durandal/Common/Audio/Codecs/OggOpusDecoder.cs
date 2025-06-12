using Durandal.Common.Audio.Codecs.Opus;
using Durandal.Common.Audio.Codecs.Opus.Enums;
using Durandal.Common.Audio.Codecs.Opus.Ogg;
using Durandal.Common.Audio.Codecs.Opus.Structs;
using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Codecs
{
    /// <summary>
    /// Audio codec which decodes Opus data inside of an OGG container.
    /// </summary>
    public class OggOpusDecoder : AudioDecoder
    {
        // used in real-time scenarios to prevent the codec from trying to decode a huge amount of data in a single read
        private readonly TimeSpan? _realTimeDecodingBudget;
        private readonly string _codecDescription;
        private readonly ILogger _logger;
        private bool _endOfStream;
        private PooledBuffer<float> _currentDecodedPacket = null;
        private int _currentDecodedPacketIndexPerChannel = 0;
        private int _currentDecodedPacketLengthPerChannel = 0;
        private OpusOggReadStream _reader;
        private int _disposed = 0;

        public override bool PlaybackFinished => _endOfStream;

        public override string CodecDescription => _codecDescription;

        /// <summary>
        /// Constructs a new <see cref="OggOpusDecoder"/> 
        /// </summary>
        /// <param name="graph">The audio graph that this component is part of</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        /// <param name="logger">A logger</param>
        /// <param name="realTimeDecodingBudget">An optional limit to the amount of real time a single read loop can take.
        /// Useful to prevent stutter caused by the decoder trying to do too much work at once.</param>
        /// <returns>A newly created <see cref="OggOpusDecoder"/></returns>
        public OggOpusDecoder(
            WeakPointer<IAudioGraph> graph,
            string nodeCustomName,
            ILogger logger,
            TimeSpan? realTimeDecodingBudget)
                : base(OggOpusCodecFactory.CODEC_NAME, graph, nameof(OggOpusDecoder), nodeCustomName)
        {
            _logger = logger.AssertNonNull(nameof(logger));
            _codecDescription = $"OggOpus audio codec (via {OpusCodecFactory.Provider.GetVersionString()})";
            _realTimeDecodingBudget = realTimeDecodingBudget;
        }

        public override async Task<AudioInitializationResult> Initialize(NonRealTimeStream inputStream, bool ownsStream, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (IsInitialized)
            {
                return AudioInitializationResult.Already_Initialized;
            }

            InputStream = inputStream.AssertNonNull(nameof(inputStream));
            OwnsStream = ownsStream;
            _reader = await OpusOggReadStream.Create(InputStream, OwnsStream).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(_reader.LastError))
            {
                _logger.Log(_reader.LastError, LogLevel.Err);
                return AudioInitializationResult.Failure_Unspecified;
            }

            OutputFormat = _reader.DecodedFormat;
            IsInitialized = true;
            return AudioInitializationResult.Success;
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
                    _reader?.Dispose();
                    _currentDecodedPacket?.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        protected override async ValueTask<int> ReadAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_endOfStream)
            {
                return -1;
            }

            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (bufferOffset < 0) throw new ArgumentOutOfRangeException(nameof(bufferOffset));
            if (numSamplesPerChannel <= 0) throw new ArgumentOutOfRangeException(nameof(numSamplesPerChannel));
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;

            int samplesPerChannelReturnedToCaller = 0;
            bool continueDecoding = true;
            long readStartTimeTicks = realTime.TimestampTicks;
            while (continueDecoding && samplesPerChannelReturnedToCaller < numSamplesPerChannel)
            {
                // Is there any data left in the sample buffer? Then use that
                int samplesPerChannelCanReadFromDecodedBuffer = 0;
                if (_currentDecodedPacket != null)
                {
                    samplesPerChannelCanReadFromDecodedBuffer = Math.Min(
                        _currentDecodedPacketLengthPerChannel - _currentDecodedPacketIndexPerChannel,
                        numSamplesPerChannel - samplesPerChannelReturnedToCaller);
                }

                if (samplesPerChannelCanReadFromDecodedBuffer > 0)
                {
                    int readOffset = _currentDecodedPacketIndexPerChannel * OutputFormat.NumChannels;
                    int writeOffset = (bufferOffset + (samplesPerChannelReturnedToCaller * OutputFormat.NumChannels));
                    int readSize = samplesPerChannelCanReadFromDecodedBuffer * OutputFormat.NumChannels;
                    ArrayExtensions.MemCopy(_currentDecodedPacket.Buffer, readOffset, buffer, writeOffset, readSize);
                    samplesPerChannelReturnedToCaller += samplesPerChannelCanReadFromDecodedBuffer;
                    _currentDecodedPacketIndexPerChannel += samplesPerChannelCanReadFromDecodedBuffer;
                    if (_currentDecodedPacketIndexPerChannel == _currentDecodedPacketLengthPerChannel)
                    {
                        _currentDecodedPacketIndexPerChannel = 0;
                        _currentDecodedPacketLengthPerChannel = 0;
                        _currentDecodedPacket.Dispose();
                        _currentDecodedPacket = null;
                    }
                }

                // Now try and decode a packet
                if (_currentDecodedPacket == null)
                {
                    _currentDecodedPacket = await _reader.DecodeNextPacket().ConfigureAwait(false);
                    if (_currentDecodedPacket == null)
                    {
                        _endOfStream = true;
                        return samplesPerChannelReturnedToCaller;
                    }
                    else
                    {
                        _currentDecodedPacketLengthPerChannel = _currentDecodedPacket.Length / OutputFormat.NumChannels;
                    }
                }

                // If this decoder has a real-time budget, see if we've hit it
                if (samplesPerChannelReturnedToCaller > 0 &&
                    _realTimeDecodingBudget.HasValue &&
                    (_realTimeDecodingBudget.Value == TimeSpan.Zero ||
                    (realTime.TimestampTicks - readStartTimeTicks) >= _realTimeDecodingBudget.Value.Ticks))
                {
                    continueDecoding = false;
                }
            }

            return samplesPerChannelReturnedToCaller;
        }
    }
}
