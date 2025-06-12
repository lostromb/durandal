using Durandal.Common.Audio.Components;
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
    /// Audio codec which reads from a raw stream of 16-bit samples.
    /// </summary>
    public class RawPcmDecoder : AudioDecoder
    {
        private readonly RiffWaveFormat _sampleFormat;
        private readonly int _singleFrameSize;
        private bool _endOfStream;

        public override bool PlaybackFinished => _endOfStream;

        public override string CodecDescription => "Uncompressed PCM";

        /// <summary>
        /// Constructs a new <see cref="RawPcmDecoder"/> 
        /// </summary>
        /// <param name="graph">The audio graph that this component is part of</param>
        /// <param name="outputFormat">The format to interpret the audio stream as</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        /// <param name="sampleFormat">The data format of the actual samples being decoded (16-bit int, 24-bit int, float, etc.)</param>
        /// <returns>A newly created <see cref="RawPcmDecoder"/></returns>
        public RawPcmDecoder(
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat outputFormat,
            string nodeCustomName,
            RiffWaveFormat sampleFormat = RiffWaveFormat.PCM_S16LE)
            : base(RawPcmCodecFactory.CODEC_NAME_PCM_S16LE, graph, nameof(RawPcmDecoder), nodeCustomName)
        {
            OutputFormat = outputFormat;
            _sampleFormat = sampleFormat;
            switch (_sampleFormat)
            {
                case RiffWaveFormat.PCM_S16LE:
                    _singleFrameSize = sizeof(short) * OutputFormat.NumChannels;
                    Codec = RawPcmCodecFactory.CODEC_NAME_PCM_S16LE;
                    break;
                case RiffWaveFormat.PCM_S24LE:
                    _singleFrameSize = 3 * OutputFormat.NumChannels;
                    Codec = RawPcmCodecFactory.CODEC_NAME_PCM_S24LE;
                    break;
                case RiffWaveFormat.PCM_S32LE:
                    _singleFrameSize = sizeof(int) * OutputFormat.NumChannels;
                    Codec = RawPcmCodecFactory.CODEC_NAME_PCM_S32LE;
                    break;
                case RiffWaveFormat.PCM_F32LE:
                    _singleFrameSize = sizeof(float) * OutputFormat.NumChannels;
                    Codec = RawPcmCodecFactory.CODEC_NAME_PCM_F32LE;
                    break;
                default:
                    throw new ArgumentException($"Raw PCM decoder does not support sample format {sampleFormat}", nameof(sampleFormat));
            }
        }

        /// <summary>
        /// Constructs a new <see cref="RawPcmDecoder"/> 
        /// </summary>
        /// <param name="graph">The audio graph that this component is part of</param>
        /// <param name="codecParams">The codec parameters used during the encode</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        /// <param name="actualCodecName">The actual PCM subcodec to use, if for example you wanted to encode as 24-bit or 32-bit float. Example <see cref="RawPcmCodecFactory.CODEC_NAME_PCM_S24LE"/>.</param>
        /// <returns>A newly created <see cref="RawPcmDecoder"/></returns>
        public RawPcmDecoder(
            WeakPointer<IAudioGraph> graph,
            string codecParams,
            string nodeCustomName,
            string actualCodecName = "pcm")
            : base(actualCodecName, graph, nameof(RawPcmDecoder), nodeCustomName)
        {
            AudioSampleFormat sampleFormat;

            if (CommonCodecParamHelper.TryParseCodecParams(codecParams, out sampleFormat))
            {
                OutputFormat = sampleFormat;

                // Determine the sample format based on the actual PCM codec name in use
                if (string.Equals(actualCodecName, RawPcmCodecFactory.CODEC_NAME_PCM_S16LE, StringComparison.OrdinalIgnoreCase))
                {
                    _sampleFormat = RiffWaveFormat.PCM_S16LE;
                    _singleFrameSize = sizeof(short) * OutputFormat.NumChannels;
                }
                else if (string.Equals(actualCodecName, RawPcmCodecFactory.CODEC_NAME_PCM_S24LE, StringComparison.OrdinalIgnoreCase))
                {
                    _sampleFormat = RiffWaveFormat.PCM_S24LE;
                    _singleFrameSize = 3 * OutputFormat.NumChannels;
                }
                else if (string.Equals(actualCodecName, RawPcmCodecFactory.CODEC_NAME_PCM_S32LE, StringComparison.OrdinalIgnoreCase))
                {
                    _sampleFormat = RiffWaveFormat.PCM_S32LE;
                    _singleFrameSize = sizeof(int) * OutputFormat.NumChannels;
                }
                else if (string.Equals(actualCodecName, RawPcmCodecFactory.CODEC_NAME_PCM_F32LE, StringComparison.OrdinalIgnoreCase))
                {
                    _sampleFormat = RiffWaveFormat.PCM_F32LE;
                    _singleFrameSize = sizeof(float) * OutputFormat.NumChannels;
                }
                else
                {
                    throw new ArgumentException($"Unknown PCM subcodec name {actualCodecName}");
                }
            }
            else
            {
                throw new ArgumentException("Codec params were invalid. String was \"" + codecParams + "\"");
            }
        }

        public override Task<AudioInitializationResult> Initialize(NonRealTimeStream inputStream, bool ownsStream, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (IsInitialized)
            {
                return Task.FromResult(AudioInitializationResult.Already_Initialized);
            }

            InputStream = inputStream.AssertNonNull(nameof(inputStream));
            OwnsStream = ownsStream;
            IsInitialized = true;
            return Task.FromResult(AudioInitializationResult.Success);
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

            using (PooledBuffer<byte> pooledBuf = BufferPool<byte>.Rent(numSamplesPerChannel * _singleFrameSize))
            {
                byte[] intermediateBuffer = pooledBuf.Buffer;
                int bytesToRead = numSamplesPerChannel * _singleFrameSize;
                int bytesActuallyRead = await InputStream.ReadAsync(intermediateBuffer, 0, bytesToRead, cancelToken, realTime).ConfigureAwait(false);

                if (bytesActuallyRead <= 0)
                {
                    // Reached end of stream.
                    _endOfStream = true;
                    return -1;
                }

                // Force the stream to honor byte alignment, otherwise everything breaks
                while (bytesActuallyRead % _singleFrameSize != 0)
                {
                    int fillerBytes = await InputStream.ReadAsync(intermediateBuffer, bytesActuallyRead, bytesActuallyRead % _singleFrameSize, cancelToken, realTime).ConfigureAwait(false);
                    if (fillerBytes <= 0)
                    {
                        // Reached end of stream partway through reading a sample.
                        // In this case, discard the last partial sample.
                        _endOfStream = true;
                        bytesActuallyRead -= bytesActuallyRead % _singleFrameSize;
                    }
                    else
                    {
                        bytesActuallyRead += fillerBytes;
                    }
                }

                int samplesPerChannelActuallyRead = bytesActuallyRead / _singleFrameSize;
                int samplesActuallyRead = samplesPerChannelActuallyRead * OutputFormat.NumChannels;

                switch (_sampleFormat)
                {
                    case RiffWaveFormat.PCM_S16LE:
                        // Convert from raw bytes (actually signed 16-bit samples) to float (32-bit) samples
                        AudioMath.ConvertSamples_2BytesIntLittleEndianToFloat(intermediateBuffer, 0, buffer, bufferOffset, samplesActuallyRead);
                        break;
                    case RiffWaveFormat.PCM_S24LE:
                        // Convert from raw bytes (actually signed 24-bit samples) to float (32-bit) samples
                        AudioMath.ConvertSamples_3BytesIntLittleEndianToFloat(intermediateBuffer, 0, buffer, bufferOffset, samplesActuallyRead);
                        break;
                    case RiffWaveFormat.PCM_S32LE:
                        // Convert from raw bytes (actually signed 24-bit samples) to float (32-bit) samples
                        AudioMath.ConvertSamples_4BytesIntLittleEndianToFloat(intermediateBuffer, 0, buffer, bufferOffset, samplesActuallyRead);
                        break;
                    case RiffWaveFormat.PCM_F32LE:
                        // Convert from raw bytes (actually float32 little-endian) to float (platform native) samples
                        AudioMath.ConvertSamples_4BytesFloatLittleEndianToFloat(intermediateBuffer, 0, buffer, bufferOffset, samplesActuallyRead);
                        break;
                    default:
                        throw new Exception($"Cannot decode PCM sample format {_sampleFormat}, this shouldn't happen");
                }

                return samplesPerChannelActuallyRead;
            }
        }
    }
}
