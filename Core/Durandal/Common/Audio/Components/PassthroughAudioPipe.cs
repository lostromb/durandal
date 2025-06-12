using Durandal.Common.IO;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Components
{
    /// <summary>
    /// Audio graph filter which does nothing, just passes through data.
    /// This also can double as a "motivator" where you can figuratively open
    /// a valve and let a specific number of samples to pass from input to output
    /// in graphs which otherwise don't have an active node like a microphone.
    /// </summary>
    public sealed class PassthroughAudioPipe : AbstractAudioSampleFilter
    {
        public PassthroughAudioPipe(WeakPointer<IAudioGraph> graph, AudioSampleFormat format, string nodeCustomName)
            : base(graph, nameof(PassthroughAudioPipe), nodeCustomName)
        {
            InputFormat = format.AssertNonNull(nameof(format));
            OutputFormat = format;
        }

        protected override ValueTask<int> ReadAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return Input.ReadAsync(buffer, offset, count, cancelToken, realTime);
        }

        protected override ValueTask WriteAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return Output.WriteAsync(buffer, offset, count, cancelToken, realTime);
        }

        /// <summary>
        /// Drives the audio graph which owns this component, reading from
        /// this component's input and writing it to output until the
        /// specified number of audio samples have been written
        /// </summary>
        /// <param name="amountToDrive">The length of audio to drive measured in time units</param>
        /// <param name="cancelToken">A cancel token for the operation.</param>
        /// <param name="realTime">A definition of real time.</param>
        /// <returns>The total number of samples per channel written (may be less than requested if the input ended prematurely)</returns>
        public Task<long> DriveGraph(TimeSpan amountToDrive, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return DriveGraph(AudioMath.ConvertTimeSpanToSamplesPerChannel(InputFormat.SampleRateHz, amountToDrive), cancelToken, realTime);
        }

        /// <summary>
        /// Drives the audio graph which owns this component, reading from
        /// this component's input and writing it to output until the
        /// specified number of audio samples have been written
        /// </summary>
        /// <param name="samplesPerChannelToDrive">The number of samples per channel of audio to drive</param>
        /// <param name="cancelToken">A cancel token for the operation.</param>
        /// <param name="realTime">A definition of real time.</param>
        /// <returns>The total number of samples per channel written (may be less than requested if the input ended prematurely)</returns>
        public async Task<long> DriveGraph(long samplesPerChannelToDrive, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            const int bufferSizeSamplesPerChannel = 65536;
            realTime.AssertNonNull(nameof(realTime));
            long totalSamplesToWrite = samplesPerChannelToDrive.AssertNonNegative(nameof(samplesPerChannelToDrive));
            long totalSamplesWritten = 0;

            if (samplesPerChannelToDrive == 0)
            {
                return 0;
            }

            using (PooledBuffer<float> pooledBuf = BufferPool<float>.Rent(bufferSizeSamplesPerChannel * OutputFormat.NumChannels))
            {
                while (!PlaybackFinished && totalSamplesWritten < totalSamplesToWrite)
                {
                    await _graph.Value.LockGraphAsync(cancelToken, realTime).ConfigureAwait(false);
                    _graph.Value.BeginInstrumentedScope(realTime, NodeFullName);
                    try
                    {
                        // have to check on each loop because someone may have messed with the graph while it was unlocked
                        if (Input == null)
                        {
                            throw new NullReferenceException("Attempted to drive audio input while input was null");
                        }
                        if (Output == null)
                        {
                            throw new NullReferenceException("Attempted to drive audio output while output was null");
                        }

                        int samplesPerChannelRead = await Input.ReadAsync(
                            pooledBuf.Buffer,
                            0,
                            (int)Math.Min(totalSamplesToWrite - totalSamplesWritten, (long)bufferSizeSamplesPerChannel),
                            cancelToken,
                            realTime).ConfigureAwait(false);
                        if (samplesPerChannelRead > 0)
                        {
                            await Output.WriteAsync(pooledBuf.Buffer, 0, samplesPerChannelRead, cancelToken, realTime).ConfigureAwait(false);
                            totalSamplesWritten += samplesPerChannelRead;
                        }
                        else if (samplesPerChannelRead < 0)
                        {
                            _playbackFinished = true;
                        }
                    }
                    finally
                    {
                        _graph.Value.EndInstrumentedScope(realTime, AudioMath.ConvertSamplesPerChannelToTimeSpan(OutputFormat.SampleRateHz, bufferSizeSamplesPerChannel));
                        _graph.Value.UnlockGraph();
                    }
                }

                if (Output != null)
                {
                    await Output.FlushAsync(cancelToken, realTime).ConfigureAwait(false);
                }
            }

            return totalSamplesWritten;
        }

        /// <summary>
        /// Drives the audio graph which owns this component, reading from
        /// this component's input and writing it to output until the
        /// input reports end of stream.
        /// </summary>
        /// <param name="cancelToken">A cancel token for the operation.</param>
        /// <param name="realTime">A definition of real time.</param>
        /// <param name="limitToRealTime">Whether to insert delays to restrict the operation to real time or not.</param>
        /// <returns>An async task</returns>
        public async Task DriveGraphFully(CancellationToken cancelToken, IRealTimeProvider realTime, bool limitToRealTime = false)
        {
            const int bufferSizeSamplesPerChannel = 65536;
            realTime.AssertNonNull(nameof(realTime));
            // 15 milliseconds is a generous estimate assuming low-precision Windows wait providers
            long minimumTicksToTriggerWait = TimeSpan.FromMilliseconds(15).Ticks;
            long operationStartedTicks = realTime.TimestampTicks;
            long totalSamplesWritten = 0;
            using (PooledBuffer<float> pooledBuf = BufferPool<float>.Rent(bufferSizeSamplesPerChannel * OutputFormat.NumChannels))
            {
                while (!PlaybackFinished)
                {
                    await _graph.Value.LockGraphAsync(cancelToken, realTime).ConfigureAwait(false);
                    _graph.Value.BeginInstrumentedScope(realTime, NodeFullName);
                    try
                    {
                        // have to check on each loop because someone may have messed with the graph while it was unlocked
                        if (Input == null)
                        {
                            throw new NullReferenceException("Attempted to drive audio input while input was null");
                        }
                        if (Output == null)
                        {
                            throw new NullReferenceException("Attempted to drive audio output while output was null");
                        }

                        int samplesPerChannelRead = await Input.ReadAsync(pooledBuf.Buffer, 0, bufferSizeSamplesPerChannel, cancelToken, realTime).ConfigureAwait(false);
                        if (samplesPerChannelRead > 0)
                        {
                            await Output.WriteAsync(pooledBuf.Buffer, 0, samplesPerChannelRead, cancelToken, realTime).ConfigureAwait(false);
                            totalSamplesWritten += samplesPerChannelRead;
                        }
                        else if (samplesPerChannelRead < 0)
                        {
                            _playbackFinished = true;
                        }
                    }
                    finally
                    {
                        _graph.Value.EndInstrumentedScope(realTime, AudioMath.ConvertSamplesPerChannelToTimeSpan(OutputFormat.SampleRateHz, bufferSizeSamplesPerChannel));
                        _graph.Value.UnlockGraph();
                    }

                    if (limitToRealTime && !PlaybackFinished)
                    {
                        // This paranoid-looking logic is here to account for the fact that the time spent in waiting
                        // is likely not the actual amount that we requested. To avoid buffer underruns down the line,
                        // make sure we adjust our budget by the actual amount of time that has passed,
                        // and carry any potential deficit forwards.
                        long totalTicksElapsedRealTime = realTime.TimestampTicks - operationStartedTicks;
                        long ticksOfAudioWritten = AudioMath.ConvertSamplesPerChannelToTicks(InputFormat.SampleRateHz, totalSamplesWritten);
                        long ticksWeCanWait = ticksOfAudioWritten - totalTicksElapsedRealTime;
                        if (ticksWeCanWait > minimumTicksToTriggerWait)
                        {
                            await realTime.WaitAsync(TimeSpan.FromTicks(ticksWeCanWait), cancelToken).ConfigureAwait(false);
                        }
                    }
                }

                if (Output != null)
                {
                    await Output.FlushAsync(cancelToken, realTime).ConfigureAwait(false);
                }
            }
        }
    }
}
