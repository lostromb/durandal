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
    /// Encoder which writes out 16-bit audio samples to .wav-formatted stream
    /// </summary>
    public class RawPcmEncoder : AudioEncoder
    {
        private readonly RiffWaveFormat _sampleFormat;
        private readonly int _bytesPerSample;
        private readonly string _codecName;

        /// <summary>
        /// Constructs a new <see cref="RawPcmEncoder"/> asynchronously.
        /// </summary>
        /// <param name="graph">The audio graph that this component is a part of.</param>
        /// <param name="format">The audio sample format of the input</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        /// <param name="sampleFormat">The actual format to encode samples in</param>
        /// <returns>A newly constructed object.</returns>
        public RawPcmEncoder(
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat format,
            string nodeCustomName,
            RiffWaveFormat sampleFormat = RiffWaveFormat.PCM_S16LE)
            : base(graph, format, nameof(RawPcmEncoder), nodeCustomName)
        {
            switch (sampleFormat)
            {
                case RiffWaveFormat.PCM_S16LE:
                    _bytesPerSample = sizeof(short);
                    _codecName = RawPcmCodecFactory.CODEC_NAME_PCM_S16LE;
                    break;
                case RiffWaveFormat.PCM_S24LE:
                    _bytesPerSample = 3;
                    _codecName = RawPcmCodecFactory.CODEC_NAME_PCM_S24LE;
                    break;
                case RiffWaveFormat.PCM_S32LE:
                    _bytesPerSample = sizeof(int);
                    _codecName = RawPcmCodecFactory.CODEC_NAME_PCM_S32LE;
                    break;
                case RiffWaveFormat.PCM_F32LE:
                    _bytesPerSample = sizeof(float);
                    _codecName = RawPcmCodecFactory.CODEC_NAME_PCM_F32LE;
                    break;
                default:
                    throw new ArgumentException($"PCM encoder format {sampleFormat} is not supported");
            }

            _sampleFormat = sampleFormat;
        }

        public override string Codec => _codecName;
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
            int samplesToConvert = count * InputFormat.NumChannels;
            int bytesRequired = samplesToConvert * _bytesPerSample;

            using (PooledBuffer<byte> pooledBuf = BufferPool<byte>.Rent(bytesRequired))
            {
                switch (_sampleFormat)
                {
                    case RiffWaveFormat.PCM_S16LE:
                        AudioMath.ConvertSamples_FloatTo2BytesIntLittleEndian(
                            buffer, offset, pooledBuf.Buffer, 0, samplesToConvert, clamp: true);
                        break;
                    case RiffWaveFormat.PCM_S24LE:
                        AudioMath.ConvertSamples_FloatTo3BytesIntLittleEndian(
                            buffer, offset, pooledBuf.Buffer, 0, samplesToConvert, clamp: true);
                        break;
                    case RiffWaveFormat.PCM_S32LE:
                        AudioMath.ConvertSamples_FloatTo4BytesIntLittleEndian(
                            buffer, offset, pooledBuf.Buffer, 0, samplesToConvert, clamp: true);
                        break;
                    case RiffWaveFormat.PCM_F32LE:
                        AudioMath.ConvertSamples_FloatTo4BytesFloatLittleEndian(
                            buffer, offset, pooledBuf.Buffer, 0, samplesToConvert);
                        break;
                }

                await OutputStream.WriteAsync(pooledBuf.Buffer, 0, bytesRequired, cancelToken, realTime).ConfigureAwait(false);
            }
        }
    }
}
