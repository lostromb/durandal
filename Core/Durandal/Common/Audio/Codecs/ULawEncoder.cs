using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Tasks;
using System.IO;
using Durandal.Common.Utils;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.Audio.Components;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Audio.Codecs
{
    /// <summary>
    /// Encoder which encodes 16-bit samples to 8-bit μ-law samples
    /// </summary>
    public class ULawEncoder : AudioEncoder
    {
        private const int BIAS = 0x84;
        private const int CLIP = 32635;
        private static readonly byte[] COMPRESS_TABLE = new byte[256]
        {
             0,0,1,1,2,2,2,2,3,3,3,3,3,3,3,3,
             4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,
             5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,
             5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,
             6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,
             6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,
             6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,
             6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,
             7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,
             7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,
             7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,
             7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,
             7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,
             7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,
             7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,
             7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7
        };

        /// <summary>
        /// Constructs a new <see cref="ULawEncoder"/>.
        /// </summary>
        /// <param name="graph">The audio graph that this component is a part of.</param>
        /// <param name="format">The audio sample format of the input</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        /// <returns>A newly constructed object.</returns>
        public ULawEncoder(WeakPointer<IAudioGraph> graph, AudioSampleFormat format, string nodeCustomName)
            : base(graph, format, nameof(ULawEncoder), nodeCustomName)
        {
        }

        public override string Codec => ULawCodecFactory.CODEC_NAME;
        public override string CodecParams => CommonCodecParamHelper.CreateCodecParams(InputFormat);

        public override Task<AudioInitializationResult> Initialize(NonRealTimeStream outputStream, bool ownsStream, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (IsInitialized)
            {
                return Task.FromResult(AudioInitializationResult.Already_Initialized);
            }

            OutputStream = outputStream.AssertNonNull(nameof(outputStream));
            OwnsStream = ownsStream;
            IsInitialized = true;
            return Task.FromResult(AudioInitializationResult.Success);
        }

        protected override async ValueTask WriteAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            // Rent a byte buffer for the conversion
            int bytesRequired = count * InputFormat.NumChannels;
            using (PooledBuffer<byte> pooledBuf = BufferPool<byte>.Rent(bytesRequired))
            {
                byte[] buf = pooledBuf.Buffer;
                int samplesToWrite = count * InputFormat.NumChannels;
                int channel = 0;
                for (int c = 0; c < samplesToWrite; c++)
                {
                    int next = (int)(buffer[offset + c] * (float)short.MaxValue);
                    if (next > short.MaxValue)
                    {
                        next = short.MaxValue;
                    }
                    else if (next < short.MinValue)
                    {
                        next = short.MinValue;
                    }

                    buf[c] = LinearToMuLawSample((short)next);
                    channel = (channel + 1) % InputFormat.NumChannels;
                }

                await OutputStream.WriteAsync(buf, 0, bytesRequired, cancelToken, realTime).ConfigureAwait(false);
            }
        }

        public static byte LinearToMuLawSample(short sample)
        {
            int sign = (sample >> 8) & 0x80;
            if (sign != 0)
                sample = (short)-sample;
            if (sample > CLIP)
                sample = CLIP;
            sample = (short)(sample + BIAS);
            int exponent = (int)COMPRESS_TABLE[(sample >> 7) & 0xFF];
            int mantissa = (sample >> (exponent + 3)) & 0x0F;
            int compressedByte = ~(sign | (exponent << 4) | mantissa);

            return (byte)compressedByte;
        }
    }
}
