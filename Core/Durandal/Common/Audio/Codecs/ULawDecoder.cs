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
    /// Audio codec which decodes raw μ-law data
    /// </summary>
    public class ULawDecoder : AudioDecoder
    {
        private static readonly short[] DECOMPRESS_TABLE = new short[256]
        {
             -32124,-31100,-30076,-29052,-28028,-27004,-25980,-24956,
             -23932,-22908,-21884,-20860,-19836,-18812,-17788,-16764,
             -15996,-15484,-14972,-14460,-13948,-13436,-12924,-12412,
             -11900,-11388,-10876,-10364, -9852, -9340, -8828, -8316,
              -7932, -7676, -7420, -7164, -6908, -6652, -6396, -6140,
              -5884, -5628, -5372, -5116, -4860, -4604, -4348, -4092,
              -3900, -3772, -3644, -3516, -3388, -3260, -3132, -3004,
              -2876, -2748, -2620, -2492, -2364, -2236, -2108, -1980,
              -1884, -1820, -1756, -1692, -1628, -1564, -1500, -1436,
              -1372, -1308, -1244, -1180, -1116, -1052,  -988,  -924,
               -876,  -844,  -812,  -780,  -748,  -716,  -684,  -652,
               -620,  -588,  -556,  -524,  -492,  -460,  -428,  -396,
               -372,  -356,  -340,  -324,  -308,  -292,  -276,  -260,
               -244,  -228,  -212,  -196,  -180,  -164,  -148,  -132,
               -120,  -112,  -104,   -96,   -88,   -80,   -72,   -64,
                -56,   -48,   -40,   -32,   -24,   -16,    -8,     -1,
              32124, 31100, 30076, 29052, 28028, 27004, 25980, 24956,
              23932, 22908, 21884, 20860, 19836, 18812, 17788, 16764,
              15996, 15484, 14972, 14460, 13948, 13436, 12924, 12412,
              11900, 11388, 10876, 10364,  9852,  9340,  8828,  8316,
               7932,  7676,  7420,  7164,  6908,  6652,  6396,  6140,
               5884,  5628,  5372,  5116,  4860,  4604,  4348,  4092,
               3900,  3772,  3644,  3516,  3388,  3260,  3132,  3004,
               2876,  2748,  2620,  2492,  2364,  2236,  2108,  1980,
               1884,  1820,  1756,  1692,  1628,  1564,  1500,  1436,
               1372,  1308,  1244,  1180,  1116,  1052,   988,   924,
                876,   844,   812,   780,   748,   716,   684,   652,
                620,   588,   556,   524,   492,   460,   428,   396,
                372,   356,   340,   324,   308,   292,   276,   260,
                244,   228,   212,   196,   180,   164,   148,   132,
                120,   112,   104,    96,    88,    80,    72,    64,
                 56,    48,    40,    32,    24,    16,     8,     0
        };

        private bool _endOfStream;

        public override bool PlaybackFinished => _endOfStream;

        /// <summary>
        /// Creates a new instance of <see cref="ULawDecoder"/> 
        /// </summary>
        /// <param name="graph">The audio graph that this component is part of</param>
        /// <param name="codecParams">Codec parameters from encoding</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        /// <returns>A newly created <see cref="ULawDecoder"/></returns>
        public ULawDecoder(WeakPointer<IAudioGraph> graph, string codecParams, string nodeCustomName)
            : base(ULawCodecFactory.CODEC_NAME, graph, nameof(ULawDecoder), nodeCustomName)
        {
            AudioSampleFormat parsedFormat;
            if (!string.IsNullOrEmpty(codecParams) && CommonCodecParamHelper.TryParseCodecParams(codecParams, out parsedFormat))
            {
                OutputFormat = parsedFormat;
            }
        }

        /// <summary>
        /// Creates a new instance of <see cref="ULawDecoder"/> 
        /// </summary>
        /// <param name="graph">The audio graph that this component is part of</param>
        /// <param name="outputFormat">The known output format</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        /// <returns>A newly created <see cref="ULawDecoder"/></returns>
        public ULawDecoder(WeakPointer<IAudioGraph> graph, AudioSampleFormat outputFormat, string nodeCustomName)
            : base(ULawCodecFactory.CODEC_NAME, graph, nameof(ULawDecoder), nodeCustomName)
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

        public override string CodecDescription => "μlaw codec";

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
