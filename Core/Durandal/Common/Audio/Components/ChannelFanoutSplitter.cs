using Durandal.Common.Collections;
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
    /// Audio graph component which splits a single multichannel input into one or more outputs containing a swizzled selection of
    /// the original input's channels. The typical use case for this is to split, say, a 5.1 surround signal into 6 separate mono signals.
    /// But you could also do arbitrary swizzling, or split front and rear into separate stereo tracks, or a whole bunch of other things.
    /// </summary>
    public sealed class ChannelFanoutSplitter : AbstractAudioSampleTarget
    {
        private readonly HashSet<FanoutSplitterOutputStream> _outputs = new HashSet<FanoutSplitterOutputStream>();
        private readonly List<Task> _parallelTasks = new List<Task>();

        /// <summary>
        /// Constructs a new audio splitter.
        /// </summary>
        /// <param name="graph">The graph that this component is a part of</param>
        /// <param name="inputFormat">The format of audio going through this component</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        public ChannelFanoutSplitter(WeakPointer<IAudioGraph> graph, AudioSampleFormat inputFormat, string nodeCustomName) : base(graph, nameof(AudioSplitter), nodeCustomName)
        {
            InputFormat = inputFormat.AssertNonNull(nameof(inputFormat));
        }

        /// <summary>
        /// Connects a component to the output of the splitter.
        /// You can add as many outputs as you like. Please disconnect the output when you are done using it.
        /// <para>
        /// Important!!!! Make sure that no single audio graph component has multiple routes to the same splitter
        /// (in other words, that the same thing is not connected multiple times). It will cause very strange
        /// bugs as the splitter tries to write to all of its outputs in parallel.
        /// </para>
        /// </summary>
        /// <param name="target">The component to connect the output to.</param>
        /// <param name="channelSwizzle">A byte array representing the channel swizzle to apply. Each index in the byte array represents the output channel,
        /// and the value at that index represents the input channel to map to that output.
        /// You can specify -1 in any entry to leave a channel unmapped.</param>
        public void AddOutput(IAudioSampleTarget target, params sbyte[] channelSwizzle)
        {
            target.AssertNonNull(nameof(target));
            channelSwizzle.AssertNonNull(nameof(channelSwizzle));

            if (target.InputFormat.ChannelMapping == MultiChannelMapping.Unknown)
            {
                throw new ArgumentException("Cannot output an unknown channel layout (did you mean to use a packed format?)");
            }

            if (InputFormat.SampleRateHz != target.InputFormat.SampleRateHz)
            {
                throw new ArgumentException($"Cannot add output to fanout splitter: Sample rate mismatch (input {InputFormat.SampleRateHz} output {target.InputFormat.SampleRateHz})");
            }

            if (channelSwizzle.Length != target.InputFormat.NumChannels)
            {
                throw new ArgumentOutOfRangeException($"The given channel swizzle pattern does not match the actual number of channels for the requested output format {target.InputFormat}. Expected {target.InputFormat.NumChannels} got {channelSwizzle.Length}");
            }

            for (int outChan = 0; outChan < channelSwizzle.Length; outChan++)
            {
                sbyte inChan = channelSwizzle[outChan];
                if (inChan >= InputFormat.NumChannels)
                {
                    throw new ArgumentOutOfRangeException($"Invalid channel swizzle: not enough input channels to map input {inChan} -> output {outChan}");
                }
            }

            FanoutSplitterOutputStream newOutput;
            InputGraph.LockGraph();
            try
            {
                newOutput = new FanoutSplitterOutputStream(new WeakPointer<IAudioGraph>(InputGraph), new WeakPointer<ChannelFanoutSplitter>(this), target.InputFormat, channelSwizzle);
                _outputs.Add(newOutput);
            }
            finally
            {
                InputGraph.UnlockGraph();
            }

            target.ConnectInput(newOutput);
        }

        protected override async ValueTask FlushAsyncInternal(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            _parallelTasks.Clear();

            foreach (FanoutSplitterOutputStream output in _outputs)
            {
                _parallelTasks.Add(output.FlushOutputAsync(cancelToken, realTime));
            }

            foreach (Task t in _parallelTasks)
            {
                await t.ConfigureAwait(false);
            }
        }

        protected override async ValueTask WriteAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            _parallelTasks.Clear();
            foreach (FanoutSplitterOutputStream output in _outputs)
            {
                _parallelTasks.Add(output.WriteOutputAsync(buffer, offset, count, cancelToken, realTime));
            }

            foreach (Task t in _parallelTasks)
            {
                await t.ConfigureAwait(false);
            }
        }

        private void RemoveOutput(FanoutSplitterOutputStream output)
        {
            // Graph is locked during OnOutputDisconnected() so we don't have to lock again
            //_graph.LockGraph();
            //try
            //{
            _outputs.Remove(output);
            //}
            //finally
            //{
            //    _graph.UnlockGraph();
            //}
        }

        /// <summary>
        /// Called internally when one splitter output is pulling audio through.
        /// In this case, we have to advance the input by that amount and push it to all outputs simultaneously
        /// </summary>
        /// <param name="pullingStream"></param>
        /// <param name="buffer"></param>
        /// <param name="bufferOffset"></param>
        /// <param name="numSamplesPerChannel"></param>
        /// <param name="cancelToken"></param>
        /// <param name="realTime"></param>
        /// <returns></returns>
        private async ValueTask<int> DriveInput(FanoutSplitterOutputStream pullingStream, float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int amountActuallyRead;
            if (Input == null)
            {
                // Input is disconnected
                amountActuallyRead = 0;
            }
            else
            {
                // Read from source and copy to the pulling stream
                amountActuallyRead = await Input.ReadAsync(buffer, bufferOffset, numSamplesPerChannel, cancelToken, realTime).ConfigureAwait(false);
            }

            // And then yoink that data out of the pulling stream's buffer and write it to all the other outputs as well
            if (amountActuallyRead > 0)
            {
                _parallelTasks.Clear();

                // fixme: do we need to split real time providers here?
                foreach (FanoutSplitterOutputStream outStream in _outputs)
                {
                    if (outStream != pullingStream)
                    {
                        _parallelTasks.Add(outStream.WriteOutputAsync(buffer, bufferOffset, amountActuallyRead, cancelToken, realTime));
                    }
                }

                foreach (Task t in _parallelTasks)
                {
                    await t.ConfigureAwait(false);
                }

                _parallelTasks.Clear();
            }

            return amountActuallyRead;
        }

        /// <summary>
        /// Internal endpoint class for output from an <see cref="ChannelFanoutSplitter"/>.
        /// </summary>
        private class FanoutSplitterOutputStream : AbstractAudioSampleSource
        {
            private readonly WeakPointer<ChannelFanoutSplitter> _parent;
            private readonly sbyte[] _channelSwizzle;
            private readonly Guid _streamId;

            public FanoutSplitterOutputStream(WeakPointer<IAudioGraph> graph, WeakPointer<ChannelFanoutSplitter> parent, AudioSampleFormat outputFormat, sbyte[] channelSwizzle)
                : base(graph, nameof(FanoutSplitterOutputStream), nodeCustomName: null)
            {
                _parent = parent.AssertNonNull(nameof(parent));
                _streamId = Guid.NewGuid();
                _channelSwizzle = channelSwizzle.AssertNonNull(nameof(channelSwizzle));
                OutputFormat = outputFormat;
            }

            public override bool PlaybackFinished
            {
                get
                {
                    return _parent.Value.Input == null ? false : _parent.Value.Input.PlaybackFinished;
                }
            }

            public async Task FlushOutputAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                if (Output != null)
                {
                    await Output.FlushAsync(cancelToken, realTime).ConfigureAwait(false);
                }
            }

            public async Task WriteOutputAsync(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                if (Output != null)
                {
                    using (PooledBuffer<float> scratchBuf = BufferPool<float>.Rent())
                    {
                        int samplesPerChannelProcessed = 0;
                        int samplesPerChannelCanFitInScratch = scratchBuf.Buffer.Length / OutputFormat.NumChannels;

                        while (samplesPerChannelProcessed < count)
                        {
                            int samplesPerChannelToProcessThisTime = FastMath.Min(count - samplesPerChannelProcessed, samplesPerChannelCanFitInScratch);
                            // Apply swizzle one channel at a time (don't iterate each swizzle per-frame, it's less efficient)
                            for (int outputChannelIdx = 0; outputChannelIdx < _channelSwizzle.Length; outputChannelIdx++)
                            {
                                int inputChannelIdx = _channelSwizzle[outputChannelIdx];
                                int outIdx = outputChannelIdx;
                                if (inputChannelIdx < 0)
                                {
                                    // negative input channel means fill with zeroes
                                    for (int sample = 0; sample < samplesPerChannelToProcessThisTime; sample++)
                                    {
                                        scratchBuf.Buffer[outIdx] = 0;
                                        outIdx += OutputFormat.NumChannels;
                                    }
                                }
                                else
                                {
                                    int inIdx = offset + (samplesPerChannelProcessed * _parent.Value.InputFormat.NumChannels) + inputChannelIdx;
                                    for (int sample = 0; sample < samplesPerChannelToProcessThisTime; sample++)
                                    {
                                        scratchBuf.Buffer[outIdx] = buffer[inIdx];
                                        outIdx += OutputFormat.NumChannels;
                                        inIdx += _parent.Value.InputFormat.NumChannels;
                                    }
                                }
                            }

                            await Output.WriteAsync(scratchBuf.Buffer, 0, samplesPerChannelToProcessThisTime, cancelToken, realTime).ConfigureAwait(false);
                            samplesPerChannelProcessed += samplesPerChannelToProcessThisTime;
                        }
                    }
                }
            }

            public override bool Equals(object obj)
            {
                if (obj == null || GetType() != obj.GetType())
                {
                    return false;
                }

                FanoutSplitterOutputStream other = (FanoutSplitterOutputStream)obj;
                return _streamId.Equals(other._streamId);
            }

            public override int GetHashCode()
            {
                return _streamId.GetHashCode();
            }

            protected override ValueTask<int> ReadAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                return _parent.Value.DriveInput(this, buffer, bufferOffset, numSamplesPerChannel, cancelToken, realTime);
            }

            /// <summary>
            /// When the output is disconnected, dispose of this object.
            /// This is to prevent memory leaks from the splitter having lots of disconnected outputs.
            /// Calling ConnectOutput() to just switch the output target shouldn't affect this.
            /// </summary>
            protected override void OnOutputDisconnected()
            {
                _parent.Value.RemoveOutput(this);
                Dispose();
            }
        }
    }
}
