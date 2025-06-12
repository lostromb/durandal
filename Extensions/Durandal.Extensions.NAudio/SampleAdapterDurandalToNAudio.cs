using Durandal.Common.Audio;
using Durandal.Common.Collections;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Extensions.NAudio
{
    /// <summary>
    /// Converts a Durandal audio sample source to an NAudio float <see cref="ISampleProvider"/>
    /// </summary>
    public class SampleAdapterDurandalToNAudio : ISampleProvider
    {
        private readonly IAudioSampleTarget _speaker;
        private readonly WeakPointer<IAudioGraph> _graph;
        private readonly ILogger _logger;

        /// <summary>
        /// Constructs a new sample adapter
        /// </summary>
        /// <param name="speaker">The NAudio output</param>
        /// <param name="graph">The Durandal graph</param>
        /// <param name="logger">A logger</param>
        public SampleAdapterDurandalToNAudio(IAudioSampleTarget speaker, WeakPointer<IAudioGraph> graph, ILogger logger)
        {
            _speaker = speaker.AssertNonNull(nameof(speaker));
            _graph = graph.AssertNonNull(nameof(graph));
            _logger = logger.AssertNonNull(nameof(logger));
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(speaker.InputFormat.SampleRateHz, speaker.InputFormat.NumChannels);
        }

        /// <summary>
        /// The NAudio wave format to convert to (PCM 32-float)
        /// </summary>
        public WaveFormat WaveFormat { get; private set; }

        /// <summary>
        /// Reads audio samples.
        /// </summary>
        /// <param name="buffer">Buffer to write to</param>
        /// <param name="offset">Offset when writing to output buffer</param>
        /// <param name="count">The total number of samples ACROSS ALL CHANNELS.</param>
        /// <returns>The total number of samples written to the buffer</returns>
        public int Read(float[] buffer, int offset, int count)
        {
            buffer.AssertNonNull(nameof(buffer));
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            // Reading less than the requested budget will result in dropped samples, so loop until we have
            // enough to satisfy the entire request, or the input is finished
            int samplesPerChannelRequested = count / WaveFormat.Channels;
            int totalSamplesPerChannelRead = 0;
            while (totalSamplesPerChannelRead < samplesPerChannelRequested)
            {
                _graph.Value.LockGraph();
                _graph.Value.BeginInstrumentedScope(DefaultRealTimeProvider.Singleton, _speaker.NodeFullName);
                try
                {
                    int currentOffset = offset + (totalSamplesPerChannelRead * WaveFormat.Channels);
                    int thisReadSizePerChannel = samplesPerChannelRequested - totalSamplesPerChannelRead;

                    IAudioSampleSource actualInput = _speaker.Input;
                    if (actualInput == null || actualInput.PlaybackFinished)
                    {
                        ArrayExtensions.WriteZeroes(
                            buffer,
                            currentOffset,
                            thisReadSizePerChannel * WaveFormat.Channels);
                        return count;
                    }
                    else
                    {
                        int samplesPerChannelRead = actualInput.ReadAsync(
                            buffer,
                            currentOffset,
                            thisReadSizePerChannel,
                            CancellationToken.None,
                            DefaultRealTimeProvider.Singleton).Await();

                        if (samplesPerChannelRead <= 0)
                        {
                            // Pad with silence if nothing read from input
                            ArrayExtensions.WriteZeroes(
                                buffer,
                                currentOffset,
                                thisReadSizePerChannel * WaveFormat.Channels);
                            return count;
                        }
                        else
                        {
                            totalSamplesPerChannelRead += samplesPerChannelRead;
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.Log(e, LogLevel.Err);
                    ArrayExtensions.WriteZeroes(
                        buffer,
                        offset + (totalSamplesPerChannelRead * WaveFormat.Channels),
                        (samplesPerChannelRequested - totalSamplesPerChannelRead) * WaveFormat.Channels);
                    return count;
                }
                finally
                {
                    _graph.Value.EndInstrumentedScope(DefaultRealTimeProvider.Singleton, AudioMath.ConvertSamplesPerChannelToTimeSpan(WaveFormat.SampleRate, samplesPerChannelRequested));
                    _graph.Value.UnlockGraph();
                }
            }

            return totalSamplesPerChannelRead * WaveFormat.Channels;
        }
    }
}
