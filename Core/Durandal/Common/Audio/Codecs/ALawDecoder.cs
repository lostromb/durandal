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
    /// Audio codec which decodes raw A-law data
    /// </summary>
    public class ALawDecoder : AudioDecoder
    {
        private static readonly short[] DECOMPRESS_TABLE = new short[256]
        {
             -5504, -5248, -6016, -5760, -4480, -4224, -4992, -4736,
             -7552, -7296, -8064, -7808, -6528, -6272, -7040, -6784,
             -2752, -2624, -3008, -2880, -2240, -2112, -2496, -2368,
             -3776, -3648, -4032, -3904, -3264, -3136, -3520, -3392,
             -22016,-20992,-24064,-23040,-17920,-16896,-19968,-18944,
             -30208,-29184,-32256,-31232,-26112,-25088,-28160,-27136,
             -11008,-10496,-12032,-11520,-8960, -8448, -9984, -9472,
             -15104,-14592,-16128,-15616,-13056,-12544,-14080,-13568,
             -344,  -328,  -376,  -360,  -280,  -264,  -312,  -296,
             -472,  -456,  -504,  -488,  -408,  -392,  -440,  -424,
             -88,   -72,   -120,  -104,  -24,   -8,    -56,   -40,
             -216,  -200,  -248,  -232,  -152,  -136,  -184,  -168,
             -1376, -1312, -1504, -1440, -1120, -1056, -1248, -1184,
             -1888, -1824, -2016, -1952, -1632, -1568, -1760, -1696,
             -688,  -656,  -752,  -720,  -560,  -528,  -624,  -592,
             -944,  -912,  -1008, -976,  -816,  -784,  -880,  -848,
              5504,  5248,  6016,  5760,  4480,  4224,  4992,  4736,
              7552,  7296,  8064,  7808,  6528,  6272,  7040,  6784,
              2752,  2624,  3008,  2880,  2240,  2112,  2496,  2368,
              3776,  3648,  4032,  3904,  3264,  3136,  3520,  3392,
              22016, 20992, 24064, 23040, 17920, 16896, 19968, 18944,
              30208, 29184, 32256, 31232, 26112, 25088, 28160, 27136,
              11008, 10496, 12032, 11520, 8960,  8448,  9984,  9472,
              15104, 14592, 16128, 15616, 13056, 12544, 14080, 13568,
              344,   328,   376,   360,   280,   264,   312,   296,
              472,   456,   504,   488,   408,   392,   440,   424,
              88,    72,   120,   104,    24,     8,    56,    40,
              216,   200,   248,   232,   152,   136,   184,   168,
              1376,  1312,  1504,  1440,  1120,  1056,  1248,  1184,
              1888,  1824,  2016,  1952,  1632,  1568,  1760,  1696,
              688,   656,   752,   720,   560,   528,   624,   592,
              944,   912,  1008,   976,   816,   784,   880,   848
        };

        private bool _endOfStream;

        public override bool PlaybackFinished => _endOfStream;

        /// <summary>
        /// Creates a new instance of <see cref="ALawDecoder"/> 
        /// </summary>
        /// <param name="graph">The audio graph that this component is part of</param>
        /// <param name="codecParams">Parameters that came out of the encoding</param>
        /// <param name="nodeCustomName">A custom name for this audio node, for debugging (can be null).</param>
        /// <returns>A newly created <see cref="ALawDecoder"/></returns>
        public ALawDecoder(WeakPointer<IAudioGraph> graph, string codecParams, string nodeCustomName)
            : base(ALawCodecFactory.CODEC_NAME, graph, nameof(ALawDecoder), nodeCustomName)
        {
            AudioSampleFormat parsedFormat;
            if (!string.IsNullOrEmpty(codecParams) && CommonCodecParamHelper.TryParseCodecParams(codecParams, out parsedFormat))
            {
                OutputFormat = parsedFormat;
            }
        }

        /// <summary>
        /// Creates a new instance of <see cref="ALawDecoder"/> 
        /// </summary>
        /// <param name="graph">The audio graph that this component is part of</param>
        /// <param name="outputFormat">The known output format of the audio to decode to</param>
        /// <param name="nodeCustomName">A custom name for this audio node, for debugging (can be null).</param>
        /// <returns>A newly created <see cref="ALawDecoder"/></returns>
        public ALawDecoder(WeakPointer<IAudioGraph> graph, AudioSampleFormat outputFormat, string nodeCustomName)
            : base(ALawCodecFactory.CODEC_NAME, graph, nameof(ALawDecoder), nodeCustomName)
        {   
            OutputFormat = outputFormat.AssertNonNull(nameof(outputFormat));
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

        public override string CodecDescription => "A-law codec";

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
                while (readCursor < bytesActuallyRead)
                {
                    buffer[writeCursor++] = (float)DECOMPRESS_TABLE[intermediateBuffer[readCursor++]] / (float)(short.MaxValue);
                }

                return samplesPerChannelActuallyRead;
            }
        }
    }
}
