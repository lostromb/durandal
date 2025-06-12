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
    /// Encoder which encodes using square-root-delta algorithm
    /// </summary>
    public class SquareDeltaEncoder : AudioEncoder
    {
        //private static readonly byte
        private readonly int[] _curPerChannel;
        private readonly double _ceil = -1;
        private readonly double _invCeil = -1;
        private readonly float _ceilF = -1;
        private readonly float _invCeilF = -1;

        /// <summary>
        /// Constructs a new <see cref="SquareDeltaEncoder"/>.
        /// </summary>
        /// <param name="graph">The audio graph that this component is a part of.</param>
        /// <param name="format">The audio sample format of the input</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        /// <returns>A newly constructed object.</returns>
        public SquareDeltaEncoder(WeakPointer<IAudioGraph> graph, AudioSampleFormat format, string nodeCustomName)
            : base(graph, format, nameof(SquareDeltaEncoder), nodeCustomName)
        {
            _curPerChannel = new int[InputFormat.NumChannels];
            _ceil = GetCeiling(InputFormat.SampleRateHz);
            _invCeil = 1.0 / _ceil;
            _ceilF = (float)_ceil;
            _invCeilF = (float)_invCeil;
        }

        public override string Codec => SquareDeltaCodecFactory.CODEC_NAME;
        public override string CodecParams => CommonCodecParamHelper.CreateCodecParams(InputFormat);

        public override async Task<AudioInitializationResult> Initialize(NonRealTimeStream outputStream, bool ownsStream, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (IsInitialized)
            {
                return AudioInitializationResult.Already_Initialized;
            }

            OutputStream = outputStream.AssertNonNull(nameof(outputStream));
            OwnsStream = ownsStream;

            // Write a 4-byte sample rate header to the output stream
            byte[] header = new byte[4];
            BinaryHelpers.Int32ToByteArrayLittleEndian(InputFormat.SampleRateHz, header, 0);
            await OutputStream.WriteAsync(header, 0, header.Length, cancelToken, realTime).ConfigureAwait(false);

            IsInitialized = true;
            return AudioInitializationResult.Success;
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

                    int diff = next - _curPerChannel[channel];
                    int actualDelta;
                    buf[c] = EncodeLargeJump((short)diff, out actualDelta);
                    _curPerChannel[channel] += actualDelta;

                    channel = (channel + 1) % InputFormat.NumChannels;
                }

                await OutputStream.WriteAsync(buf, 0, bytesRequired, cancelToken, realTime).ConfigureAwait(false);
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
        /// Maps the byte code points along a curve that ranges from 0 to ceil
        /// </summary>
        /// <param name="difference"></param>
        /// <param name="actualDelta"></param>
        /// <returns></returns>
        private byte EncodeLargeJump(short difference, out int actualDelta)
        {
#if NET6_0_OR_GREATER
            byte code = (byte)MathF.Min(127, MathF.Sqrt(MathF.Abs((float)difference) * (127.0f * 127.0f) * _invCeilF));
            actualDelta = (int)MathF.Round(code * code * _ceilF * (1.0f / 127.0f / 127.0f));
#else
            byte code = (byte)Math.Min(127, Math.Sqrt(Math.Abs((double)difference) * (127.0 * 127.0) * _invCeil));
            actualDelta = (int)Math.Round(code * code * _ceil * (1.0 / 127.0 / 127.0));
#endif
            if (actualDelta > short.MaxValue)
            {
                actualDelta = short.MaxValue;
            }

            if (difference < 0)
            {
                code |= 0x80;
                actualDelta = 0 - actualDelta;
            }

            return code;
        }
    }
}
