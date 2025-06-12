using Durandal.Common.Audio.Components;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Codecs
{
    /// <summary>
    /// Audio codec which decodes sqrt
    /// </summary>
    public class SquareDeltaDecoder : AudioDecoder
    {
        private bool _endOfStream;
        private byte[] _sampleRateBuf = new byte[4];
        private int _sampleRateRead = 0;
        private double _ceil = -1;
        private float _ceilF = -1;
        private short[] _curPerChannel;

        public override bool PlaybackFinished => _endOfStream;

        /// <summary>
        /// Creates a new instance of <see cref="SquareDeltaDecoder"/> 
        /// </summary>
        /// <param name="graph">The audio graph that this component is part of</param>
        /// <param name="codecParams">Codec parameters from encoding</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        /// <returns>A newly created <see cref="SquareDeltaDecoder"/></returns>
        public SquareDeltaDecoder(WeakPointer<IAudioGraph> graph, string codecParams, string nodeCustomName)
            : base(SquareDeltaCodecFactory.CODEC_NAME, graph, nameof(SquareDeltaDecoder), nodeCustomName)
        {
            AudioSampleFormat parsedFormat;
            if (!string.IsNullOrEmpty(codecParams) && CommonCodecParamHelper.TryParseCodecParams(codecParams, out parsedFormat))
            {
                OutputFormat = parsedFormat;
            }
        }

        public override async Task<AudioInitializationResult> Initialize(NonRealTimeStream inputStream, bool ownsStream, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (IsInitialized)
            {
                return AudioInitializationResult.Already_Initialized;
            }

            InputStream = inputStream.AssertNonNull(nameof(inputStream));
            OwnsStream = ownsStream;
            IsInitialized = true;

            // Read 4 bytes from the stream. This corresponds to the sample rate for legacy reasons.
            while (_sampleRateRead < 4)
            {
                int readVal = await InputStream.ReadAsync(_sampleRateBuf, _sampleRateRead, 4 - _sampleRateRead, cancelToken, realTime).ConfigureAwait(false);
                if (readVal < 0)
                {
                    return AudioInitializationResult.Failure_StreamEnded;
                }

                _sampleRateRead += readVal;
            }

            int sampleRate = BinaryHelpers.ByteArrayToInt32LittleEndian(_sampleRateBuf, 0);

            if (OutputFormat == null)
            {
                // If we haven't got any output format from codec params, use the parsed sample rate here
                // This is, like, super super legacy code but whatever.
                OutputFormat = AudioSampleFormat.Mono(sampleRate);
            }

            _ceil = GetCeiling(OutputFormat.SampleRateHz);
            _ceilF = (float)_ceil;
            _curPerChannel = new short[OutputFormat.NumChannels];
            return AudioInitializationResult.Success;
        }

        public override string CodecDescription => "Arithmetic SRD codec";

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

            using (PooledBuffer<byte> pooledBuf = BufferPool<byte>.Rent(numSamplesPerChannel * OutputFormat.NumChannels))
            {
                byte[] intermediateBuffer = pooledBuf.Buffer;
                int bytesToRead = numSamplesPerChannel * OutputFormat.NumChannels;
                int byteAlignment = OutputFormat.NumChannels;
                int bytesActuallyRead = await InputStream.ReadAsync(intermediateBuffer, 0, bytesToRead, cancelToken, realTime).ConfigureAwait(false);

                if (bytesActuallyRead == 0)
                {
                    // Reached end of stream.
                    _endOfStream = true;
                    return -1;
                }

                // Force the stream to honor byte alignment, otherwise everything breaks
                while (bytesActuallyRead % byteAlignment != 0)
                {
                    int fillerBytes = await InputStream.ReadAsync(intermediateBuffer, bytesActuallyRead, bytesActuallyRead % byteAlignment, cancelToken, realTime).ConfigureAwait(false);
                    if (fillerBytes < 0)
                    {
                        // Reached end of stream partway through reading a sample.
                        // In this case, discard the last partial sample.
                        _endOfStream = true;
                        bytesActuallyRead -= bytesActuallyRead % byteAlignment;
                    }
                    else
                    {
                        bytesActuallyRead += fillerBytes;
                    }
                }

                int samplesActuallyRead = bytesActuallyRead;
                int samplesPerChannelActuallyRead = samplesActuallyRead / OutputFormat.NumChannels;

                int readCursor = 0;
                int writeCursor = bufferOffset;
                int channel = 0;
                while (readCursor < bytesActuallyRead)
                {
                    short diff = DecodeLargeJump(intermediateBuffer[readCursor++]);

                    // Should never hit these first two cases. But just be safe, clamp the output value
                    if (((int)_curPerChannel[channel] + (int)diff) > short.MaxValue)
                    {
                        _curPerChannel[channel] = short.MaxValue;
                    }
                    else if (((int)_curPerChannel[channel] + (int)diff) < short.MinValue)
                    {
                        _curPerChannel[channel] = short.MinValue;
                    }
                    else
                    {
                        _curPerChannel[channel] += diff;
                    }

                    buffer[writeCursor++] = (float)_curPerChannel[channel] / (float)(short.MaxValue);
                    channel = (channel + 1) % OutputFormat.NumChannels;
                }

                return samplesPerChannelActuallyRead;
            }
        }

        /// <summary>
        /// Determines the maximum delta range to use based on sample rate.
        /// Lower numbers mean greater precision but worse handling of large jumps
        /// </summary>
        /// <param name="sampleRate"></param>
        /// <returns></returns>
        private static double GetCeiling(int sampleRate)
        {
            if (sampleRate > 44100)
            {
                return 5000;
            }
            else if (sampleRate > 16000)
            {
                return 8000;
            }
            else
            {
                return 16000;
            }
        }

        /// <summary>
        /// Inverts the transformation performed by EncodeLargeJump. Converts the byte code into
        /// the actual magnitude of audio difference
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        private short DecodeLargeJump(byte code)
        {
#if NET6_0_OR_GREATER
            float c = (float)(code & 0x7F);
            int result = (int)MathF.Round(c * c * _ceilF * (1.0f / 127.0f / 127.0f));
#else
            double c = (double)(code & 0x7F);
            int result = (int)Math.Round(c * c * _ceil * (1.0 / 127.0 / 127.0));
#endif
            if ((code & 0x80) != 0)
            {
                result = 0 - result;
            }

            return (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, result));
        }
    }
}
