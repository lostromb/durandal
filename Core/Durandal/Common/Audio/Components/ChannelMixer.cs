using Durandal.Common.Collections;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Components
{
    /// <summary>
    /// Audio graph component which converts between channel mappings, for example splitting mono to stereo, muxing stereo to mono, swapping channels, etc.
    /// </summary>
    public sealed class ChannelMixer : AbstractAudioSampleFilter
    {
        private static readonly TimeSpan PROCESS_BUFFER_SIZE = TimeSpan.FromMilliseconds(100);
        private readonly float[] _processingBuffer;
        private readonly int _processingBufferSizeSamplesPerChannel;
        private readonly MapChannelImpl _currentAlgorithm;

        /// <summary>
        /// Algorithm implementation that does the channel mapping
        /// </summary>
        /// <param name="inBuffer">Input sample buffer</param>
        /// <param name="inBufferOffset">Read offset, array index</param>
        /// <param name="outBuffer">Output sample buffer</param>
        /// <param name="outBufferOffset">Write offet, array index</param>
        /// <param name="samplesPerChannelToProcess">The number of samples per channel to process</param>
        private delegate void MapChannelImpl(float[] inBuffer, int inBufferOffset, float[] outBuffer, int outBufferOffset, int samplesPerChannelToProcess);

        /// <summary>
        /// Creates a channel mixer component to adapt input and output channel mappings.
        /// </summary>
        /// <param name="graph">The graph that this component is a part of.</param>
        /// <param name="sampleRate">The sample rate of the pipe.</param>
        /// <param name="inputChannelLayout">The input channel layout.</param>
        /// <param name="outputChannelLayout">The output channel layout.</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        public ChannelMixer(WeakPointer<IAudioGraph> graph, int sampleRate, MultiChannelMapping inputChannelLayout, MultiChannelMapping outputChannelLayout, string nodeCustomName)
            : base(graph, nameof(ChannelMixer), nodeCustomName)
        {
            if (sampleRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            }

            InputFormat = new AudioSampleFormat(sampleRate, inputChannelLayout);
            OutputFormat = new AudioSampleFormat(sampleRate, outputChannelLayout);

            // Create a scratch buffer long enough to store 100ms of samples, in either channel layout
            // Note that this is just scratch space. It doesn't actually impact latency, only the size of consecutive Read/Write operations on the pipe
            _processingBufferSizeSamplesPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(InputFormat.SampleRateHz, PROCESS_BUFFER_SIZE);
            _processingBuffer = new float[_processingBufferSizeSamplesPerChannel * FastMath.Max(InputFormat.NumChannels, OutputFormat.NumChannels)];
            _currentAlgorithm = GetAlgorithm(inputChannelLayout, InputFormat.NumChannels, outputChannelLayout, OutputFormat.NumChannels);
        }

        protected override async ValueTask<int> ReadAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int totalSamplesPerChannelProcessed = 0;
            while (totalSamplesPerChannelProcessed < count)
            {
                int thisBatchSizePerChannel = await Input.ReadAsync(
                    _processingBuffer,
                    0,
                    FastMath.Min(count - totalSamplesPerChannelProcessed, _processingBufferSizeSamplesPerChannel),
                    cancelToken,
                    realTime).ConfigureAwait(false);

                if (thisBatchSizePerChannel < 0)
                {
                    // End of input. Return -1 end of stream if we haven't read any samples in this loop at all
                    return totalSamplesPerChannelProcessed == 0 ? -1 : totalSamplesPerChannelProcessed;
                }
                else if (thisBatchSizePerChannel == 0)
                {
                    // Input is exhausted so just return what we've got
                    return totalSamplesPerChannelProcessed;
                }
                else
                {
                    // Apply the mapping algorithm, copying from the intermediate buffer to the output buffer
                    _currentAlgorithm(_processingBuffer, 0, buffer, offset, thisBatchSizePerChannel);

                    totalSamplesPerChannelProcessed += thisBatchSizePerChannel;
                    offset += thisBatchSizePerChannel * OutputFormat.NumChannels;
                }
            }

            return totalSamplesPerChannelProcessed;
        }

        protected override async ValueTask WriteAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int totalSamplesPerChannelProcessed = 0;
            while (totalSamplesPerChannelProcessed < count)
            {
                int thisBatchSizePerChannel = FastMath.Min(count - totalSamplesPerChannelProcessed, _processingBufferSizeSamplesPerChannel);

                // Apply the mapping algorithm, copying from the write buffer to the intermediate buffer
                _currentAlgorithm(buffer, offset, _processingBuffer, 0, thisBatchSizePerChannel);

                // And pipe to output
                await Output.WriteAsync(_processingBuffer, 0, thisBatchSizePerChannel, cancelToken, realTime).ConfigureAwait(false);

                totalSamplesPerChannelProcessed += thisBatchSizePerChannel;
                offset += thisBatchSizePerChannel * InputFormat.NumChannels;
            }
        }

        /// <summary>
        /// No-op mapping for single-channel audio
        /// </summary>
        private static void MapChannel_Passthrough_1ch(float[] inBuffer, int inBufferOffset, float[] outBuffer, int outBufferOffset, int samplesPerChannelToProcess)
        {
            ArrayExtensions.MemCopy(inBuffer, inBufferOffset, outBuffer, outBufferOffset, samplesPerChannelToProcess);
        }

        /// <summary>
        /// No-op mapping for 2-channel audio
        /// </summary>
        private static void MapChannel_Passthrough_2ch(float[] inBuffer, int inBufferOffset, float[] outBuffer, int outBufferOffset, int samplesPerChannelToProcess)
        {
            ArrayExtensions.MemCopy(inBuffer, inBufferOffset, outBuffer, outBufferOffset, samplesPerChannelToProcess * 2);
        }

        /// <summary>
        /// No-op mapping for 3-channel audio
        /// </summary>
        private static void MapChannel_Passthrough_3ch(float[] inBuffer, int inBufferOffset, float[] outBuffer, int outBufferOffset, int samplesPerChannelToProcess)
        {
            ArrayExtensions.MemCopy(inBuffer, inBufferOffset, outBuffer, outBufferOffset, samplesPerChannelToProcess * 3);
        }

        /// <summary>
        /// No-op mapping for 4-channel audio
        /// </summary>
        private static void MapChannel_Passthrough_4ch(float[] inBuffer, int inBufferOffset, float[] outBuffer, int outBufferOffset, int samplesPerChannelToProcess)
        {
            ArrayExtensions.MemCopy(inBuffer, inBufferOffset, outBuffer, outBufferOffset, samplesPerChannelToProcess * 4);
        }

        /// <summary>
        /// No-op mapping for 5-channel audio
        /// </summary>
        private static void MapChannel_Passthrough_5ch(float[] inBuffer, int inBufferOffset, float[] outBuffer, int outBufferOffset, int samplesPerChannelToProcess)
        {
            ArrayExtensions.MemCopy(inBuffer, inBufferOffset, outBuffer, outBufferOffset, samplesPerChannelToProcess * 5);
        }

        /// <summary>
        /// No-op mapping for 6-channel audio
        /// </summary>
        private static void MapChannel_Passthrough_6ch(float[] inBuffer, int inBufferOffset, float[] outBuffer, int outBufferOffset, int samplesPerChannelToProcess)
        {
            ArrayExtensions.MemCopy(inBuffer, inBufferOffset, outBuffer, outBufferOffset, samplesPerChannelToProcess * 6);
        }

        /// <summary>
        /// No-op mapping for 7-channel audio
        /// </summary>
        private static void MapChannel_Passthrough_7ch(float[] inBuffer, int inBufferOffset, float[] outBuffer, int outBufferOffset, int samplesPerChannelToProcess)
        {
            ArrayExtensions.MemCopy(inBuffer, inBufferOffset, outBuffer, outBufferOffset, samplesPerChannelToProcess * 7);
        }

        /// <summary>
        /// No-op mapping for 8-channel audio
        /// </summary>
        private static void MapChannel_Passthrough_8ch(float[] inBuffer, int inBufferOffset, float[] outBuffer, int outBufferOffset, int samplesPerChannelToProcess)
        {
            ArrayExtensions.MemCopy(inBuffer, inBufferOffset, outBuffer, outBufferOffset, samplesPerChannelToProcess * 8);
        }

        /// <summary>
        /// Converts mono audio to stereo by duplicating the input channel
        /// </summary>
        private static void MapChannel_Mono_To_Stereo(float[] inBuffer, int inBufferOffset, float[] outBuffer, int outBufferOffset, int samplesPerChannelToProcess)
        {
            float input;
            int outputIdx = outBufferOffset;
            for (int inputIdx = 0; inputIdx < samplesPerChannelToProcess; inputIdx++)
            {
                input = inBuffer[inputIdx + inBufferOffset];
                outBuffer[outputIdx++] = input;
                outBuffer[outputIdx++] = input;
            }
        }

        /// <summary>
        /// Converts stereo audio to mono using the arithmetic mean of both channels
        /// </summary>
        private static void MapChannel_Stereo_To_Mono(float[] inBuffer, int inBufferOffset, float[] outBuffer, int outBufferOffset, int samplesPerChannelToProcess)
        {
            float input;
            int inputIdx = inBufferOffset;
            for (int outputIdx = 0; outputIdx < samplesPerChannelToProcess; outputIdx++)
            {
                input = inBuffer[inputIdx++];
                input += inBuffer[inputIdx++];
                outBuffer[outputIdx + outBufferOffset] = input / 2;
            }
        }

        /// <summary>
        /// Flips L and R stereo channels
        /// </summary>
        private static void MapChannel_SwapStereoChannels(float[] inBuffer, int inBufferOffset, float[] outBuffer, int outBufferOffset, int samplesPerChannelToProcess)
        {
            int sample2 = 0;
            int sample2Plus1 = 1;
            for (int sample = 0; sample < samplesPerChannelToProcess; sample++)
            {
                outBuffer[outBufferOffset + sample2] = inBuffer[inBufferOffset + sample2Plus1];
                outBuffer[outBufferOffset + sample2Plus1] = inBuffer[inBufferOffset + sample2];
                sample2 += 2;
                sample2Plus1 += 2;
            }
        }

        /// <summary>
        /// Converts 5.1 (rear or side, doesn't matter) to stereo using ffmpeg's mixing weights
        /// </summary>
        private static void MapChannel_5_1_To_Stereo(float[] inBuffer, int inBufferOffset, float[] outBuffer, int outBufferOffset, int samplesPerChannelToProcess)
        {
            // Don't bother trying to vectorize this code with weights and dot products, it doesn't run faster

            // === Original code ===
            // input channels come in this order:
            //float FL, FR, FC, LFE, SL, SR;
            //int inputIdx = inBufferOffset;
            //int outputIdx = outBufferOffset;
            //for (int sampleNum = 0; sampleNum < samplesPerChannelToProcess; sampleNum++)
            //{
            //    FL = inBuffer[inputIdx++];
            //    FR = inBuffer[inputIdx++];
            //    FC = inBuffer[inputIdx++];
            //    LFE = inBuffer[inputIdx++];
            //    SL = inBuffer[inputIdx++];
            //    SR = inBuffer[inputIdx++];
            //    outBuffer[outputIdx++] = (FL * 1.0f) + (FC * 0.5f) + (SL * 0.6f);
            //    outBuffer[outputIdx++] = (FR * 1.0f) + (FC * 0.5f) + (SR * 0.6f);
            //}

            // Minified code, runs 25% faster
            for (int sampleNum = 0; sampleNum < samplesPerChannelToProcess; sampleNum++)
            {
                float FC = inBuffer[inBufferOffset + 2];
                outBuffer[outBufferOffset] = (inBuffer[inBufferOffset] * 1.0f) + (FC * 0.5f) + (inBuffer[inBufferOffset + 4] * 0.6f);
                outBuffer[outBufferOffset + 1] = (inBuffer[inBufferOffset + 1] * 1.0f) + (FC * 0.5f) + (inBuffer[inBufferOffset + 5] * 0.6f);
                inBufferOffset += 6;
                outBufferOffset += 2;
            }
        }

        /// <summary>
        /// Converts 5.1 (rear or side, doesn't matter) to stereo using ffmpeg's mixing weights
        /// </summary>
        private static void MapChannel_5_1_Vorbis_To_Stereo(float[] inBuffer, int inBufferOffset, float[] outBuffer, int outBufferOffset, int samplesPerChannelToProcess)
        {
            // input channels come in this order:
            //float FL, FC, FR, SL, SR, LFE;
            for (int sampleNum = 0; sampleNum < samplesPerChannelToProcess; sampleNum++)
            {
                float FC = inBuffer[inBufferOffset + 1];
                outBuffer[outBufferOffset] = (inBuffer[inBufferOffset] * 1.0f) + (FC * 0.5f) + (inBuffer[inBufferOffset + 3] * 0.6f);
                outBuffer[outBufferOffset + 1] = (inBuffer[inBufferOffset + 2] * 1.0f) + (FC * 0.5f) + (inBuffer[inBufferOffset + 4] * 0.6f);
                inBufferOffset += 6;
                outBufferOffset += 2;
            }
        }

        /// <summary>
        /// Converts 5.1 (rear or side, doesn't matter) to mono using ffmpeg's mixing weights
        /// </summary>
        private static void MapChannel_5_1_To_Mono(float[] inBuffer, int inBufferOffset, float[] outBuffer, int outBufferOffset, int samplesPerChannelToProcess)
        {
            // === Original code ===
            // input channels come in this order:
            //float FL, FR, FC, LFE, SL, SR;
            //int inputIdx = inBufferOffset;
            //int outputIdx = outBufferOffset;
            //for (int sampleNum = 0; sampleNum < samplesPerChannelToProcess; sampleNum++)
            //{
            //    FL = inBuffer[inputIdx++];
            //    FR = inBuffer[inputIdx++];
            //    FC = inBuffer[inputIdx++];
            //    LFE = inBuffer[inputIdx++];
            //    SL = inBuffer[inputIdx++];
            //    SR = inBuffer[inputIdx++];
            //    outBuffer[outputIdx++] = (FL * 0.5f) + (FC * 0.5f) + (FR * 0.5f) + (SL * 0.3f) + (SR * 0.3f);
            //}

            // === Minified code ===
            for (int sampleNum = 0; sampleNum < samplesPerChannelToProcess; sampleNum++)
            {
                outBuffer[outBufferOffset++] = 
                    (inBuffer[inBufferOffset] * 0.5f) +
                    (inBuffer[inBufferOffset + 1] * 0.5f) +
                    (inBuffer[inBufferOffset + 2] * 0.5f) +
                    (inBuffer[inBufferOffset + 4] * 0.3f) +
                    (inBuffer[inBufferOffset + 5] * 0.3f);
                inBufferOffset += 6;
            }
        }

        /// <summary>
        /// Converts 5.1 (rear or side, doesn't matter) to mono using ffmpeg's mixing weights
        /// </summary>
        private static void MapChannel_5_1_Vorbis_To_Mono(float[] inBuffer, int inBufferOffset, float[] outBuffer, int outBufferOffset, int samplesPerChannelToProcess)
        {
            // input channels come in this order:
            //float FL, FC, FR, SL, SR, LFE;
            for (int sampleNum = 0; sampleNum < samplesPerChannelToProcess; sampleNum++)
            {
                outBuffer[outBufferOffset++] =
                    (inBuffer[inBufferOffset] * 0.5f) +
                    (inBuffer[inBufferOffset + 1] * 0.5f) +
                    (inBuffer[inBufferOffset + 2] * 0.5f) +
                    (inBuffer[inBufferOffset + 3] * 0.3f) +
                    (inBuffer[inBufferOffset + 4] * 0.3f);
                inBufferOffset += 6;
            }
        }

        /// <summary>
        /// Swizzles 5.1 (standard or AAC channel order) to 5.1 (Vorbis channel order)
        /// </summary>
        private static void MapChannel_5_1_Standard_to_Vorbis(float[] inBuffer, int inBufferOffset, float[] outBuffer, int outBufferOffset, int samplesPerChannelToProcess)
        {
            for (int sample = 0; sample < samplesPerChannelToProcess; sample++)
            {
                outBuffer[outBufferOffset + 0] = inBuffer[inBufferOffset + 0];
                outBuffer[outBufferOffset + 1] = inBuffer[inBufferOffset + 2];
                outBuffer[outBufferOffset + 2] = inBuffer[inBufferOffset + 1];
                outBuffer[outBufferOffset + 3] = inBuffer[inBufferOffset + 4];
                outBuffer[outBufferOffset + 4] = inBuffer[inBufferOffset + 5];
                outBuffer[outBufferOffset + 5] = inBuffer[inBufferOffset + 3];
                inBufferOffset += 6;
                outBufferOffset += 6;
            }
        }

        /// <summary>
        /// Swizzles 5.1 (Vorbis channel order) to 5.1 (standard or AAC channel order)
        /// </summary>
        private static void MapChannel_5_1_Vorbis_to_Standard(float[] inBuffer, int inBufferOffset, float[] outBuffer, int outBufferOffset, int samplesPerChannelToProcess)
        {
            for (int sample = 0; sample < samplesPerChannelToProcess; sample++)
            {
                outBuffer[outBufferOffset + 0] = inBuffer[inBufferOffset + 0];
                outBuffer[outBufferOffset + 1] = inBuffer[inBufferOffset + 2];
                outBuffer[outBufferOffset + 2] = inBuffer[inBufferOffset + 1];
                outBuffer[outBufferOffset + 3] = inBuffer[inBufferOffset + 5];
                outBuffer[outBufferOffset + 4] = inBuffer[inBufferOffset + 3];
                outBuffer[outBufferOffset + 5] = inBuffer[inBufferOffset + 4];
                inBufferOffset += 6;
                outBufferOffset += 6;
            }
        }

        private static MapChannelImpl GetAlgorithm(
            MultiChannelMapping inputLayout,
            int inputNumChannels,
            MultiChannelMapping outputLayout,
            int outputNumChannels)
        {
            if (inputLayout == outputLayout ||
                AudioSampleFormat.IsPackedChannelLayout(inputLayout) ||
                AudioSampleFormat.IsPackedChannelLayout(outputLayout))
            {
                switch (inputNumChannels)
                {
                    case 1:
                        return MapChannel_Passthrough_1ch;
                    case 2:
                        return MapChannel_Passthrough_2ch;
                    case 3:
                        return MapChannel_Passthrough_3ch;
                    case 4:
                        return MapChannel_Passthrough_4ch;
                    case 5:
                        return MapChannel_Passthrough_5ch;
                    case 6:
                        return MapChannel_Passthrough_6ch;
                    case 7:
                        return MapChannel_Passthrough_7ch;
                    case 8:
                        return MapChannel_Passthrough_8ch;
                }
            }

            if ((inputLayout == MultiChannelMapping.Stereo_L_R && outputLayout == MultiChannelMapping.Stereo_R_L) ||
                (inputLayout == MultiChannelMapping.Stereo_R_L && outputLayout == MultiChannelMapping.Stereo_L_R))
            {
                return MapChannel_SwapStereoChannels;
            }
            else if (inputLayout == MultiChannelMapping.Monaural && (outputLayout == MultiChannelMapping.Stereo_L_R || outputLayout == MultiChannelMapping.Stereo_R_L))
            {
                return MapChannel_Mono_To_Stereo;
            }
            else if ((inputLayout == MultiChannelMapping.Stereo_L_R || inputLayout == MultiChannelMapping.Stereo_R_L) && outputLayout == MultiChannelMapping.Monaural)
            {
                return MapChannel_Stereo_To_Mono;
            }
            else if ((inputLayout == MultiChannelMapping.Surround_5_1ch || inputLayout == MultiChannelMapping.Surround_5_1ch_side) && outputLayout == MultiChannelMapping.Stereo_L_R)
            {
                return MapChannel_5_1_To_Stereo;
            }
            else if ((inputLayout == MultiChannelMapping.Surround_5_1ch || inputLayout == MultiChannelMapping.Surround_5_1ch_side) && outputLayout == MultiChannelMapping.Monaural)
            {
                return MapChannel_5_1_To_Mono;
            }
            else if ((inputLayout == MultiChannelMapping.Surround_5_1ch_Vorbis_Layout || inputLayout == MultiChannelMapping.Surround_5_1ch_side_Vorbis_Layout) && outputLayout == MultiChannelMapping.Stereo_L_R)
            {
                return MapChannel_5_1_Vorbis_To_Stereo;
            }
            else if ((inputLayout == MultiChannelMapping.Surround_5_1ch_Vorbis_Layout || inputLayout == MultiChannelMapping.Surround_5_1ch_side_Vorbis_Layout) && outputLayout == MultiChannelMapping.Monaural)
            {
                return MapChannel_5_1_Vorbis_To_Mono;
            }
            else if ((inputLayout == MultiChannelMapping.Surround_5_1ch && outputLayout == MultiChannelMapping.Surround_5_1ch_side) ||
                (inputLayout == MultiChannelMapping.Surround_5_1ch_side && outputLayout == MultiChannelMapping.Surround_5_1ch))
            {
                return MapChannel_Passthrough_6ch;
            }
            else if ((inputLayout == MultiChannelMapping.Surround_5_1ch || inputLayout == MultiChannelMapping.Surround_5_1ch_side) &&
                (outputLayout == MultiChannelMapping.Surround_5_1ch_Vorbis_Layout || outputLayout == MultiChannelMapping.Surround_5_1ch_side_Vorbis_Layout))
            {
                return MapChannel_5_1_Standard_to_Vorbis;
            }
            else if ((inputLayout == MultiChannelMapping.Surround_5_1ch_Vorbis_Layout || inputLayout == MultiChannelMapping.Surround_5_1ch_side_Vorbis_Layout) &&
                (outputLayout == MultiChannelMapping.Surround_5_1ch || outputLayout == MultiChannelMapping.Surround_5_1ch_side))
            {
                return MapChannel_5_1_Vorbis_to_Standard;
            }
            else
            {
                throw new NotImplementedException("The given channel mapping from " + inputLayout.ToString() + " to " + outputLayout.ToString() + " is not implemented");
            }
        }
    }
}
