using Durandal.Common.Collections;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Components
{
    /// <summary>
    /// Audio graph component which splits a single input into one or more outputs without altering or buffering the signal.
    /// </summary>
    public sealed class AudioSplitter : AbstractAudioSampleTarget
    {
        private readonly HashSet<AudioSplitterOutputStream> _outputs = new HashSet<AudioSplitterOutputStream>();
        private readonly List<Task> _parallelTasks = new List<Task>();

        /// <summary>
        /// Constructs a new audio splitter.
        /// </summary>
        /// <param name="graph">The graph that this component is a part of</param>
        /// <param name="inputFormat">The format of audio going through this component</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        public AudioSplitter(WeakPointer<IAudioGraph> graph, AudioSampleFormat inputFormat, string nodeCustomName) : base(graph, nameof(AudioSplitter), nodeCustomName)
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
        public void AddOutput(IAudioSampleTarget target)
        {
            AudioSplitterOutputStream newOutput;
            InputGraph.LockGraph();
            try
            {
                newOutput = new AudioSplitterOutputStream(new WeakPointer<IAudioGraph>(InputGraph), new WeakPointer<AudioSplitter>(this));
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
            if (_outputs.Count == 0)
            {
                return;
            }
            else if (_outputs.Count == 1)
            {
                // fast path for single output
                await _outputs.First().SplitterFlushOutputAsync(cancelToken, realTime).ConfigureAwait(false);
            }
            else
            {
                _parallelTasks.Clear();

                foreach (AudioSplitterOutputStream output in _outputs)
                {
                    _parallelTasks.Add(output.SplitterFlushOutputAsync(cancelToken, realTime));
                }

                foreach (Task t in _parallelTasks)
                {
                    await t.ConfigureAwait(false);
                }
            }
        }

        protected override async ValueTask WriteAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_outputs.Count == 0)
            {
                return;
            }
            else if (_outputs.Count == 1)
            {
                // fast path for single output
                await _outputs.First().SplitterWriteOutputAsync(buffer, offset, count, cancelToken, realTime).ConfigureAwait(false);
            }
            else
            {
                // fixme: need to fork the real time providers here?
                _parallelTasks.Clear();
                foreach (AudioSplitterOutputStream output in _outputs)
                {
                    _parallelTasks.Add(output.SplitterWriteOutputAsync(buffer, offset, count, cancelToken, realTime));
                }

                foreach (Task t in _parallelTasks)
                {
                    await t.ConfigureAwait(false);
                }
            }
        }

        private void RemoveOutput(AudioSplitterOutputStream output)
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
        private async ValueTask<int> DriveInput(AudioSplitterOutputStream pullingStream, float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
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
                // This function is using the output component's read buffer as a scratch space so we don't have to allocate our own
                amountActuallyRead = await Input.ReadAsync(buffer, bufferOffset, numSamplesPerChannel, cancelToken, realTime).ConfigureAwait(false);
            }

            // And then yoink that data out of the pulling stream's buffer and write it to all the other outputs as well
            if (amountActuallyRead > 0 && _outputs.Count > 1)
            {
                _parallelTasks.Clear();

                // fixme: do we need to split real time providers here?
                foreach (AudioSplitterOutputStream outStream in _outputs)
                {
                    if (outStream != pullingStream)
                    {
                        _parallelTasks.Add(outStream.SplitterWriteOutputAsync(buffer, bufferOffset, amountActuallyRead, cancelToken, realTime));
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
        /// Internal endpoint class for output from an <see cref="AudioSplitter"/>.
        /// </summary>
        private class AudioSplitterOutputStream : AbstractAudioSampleSource
        {
            private readonly WeakPointer<AudioSplitter> _parent;
            private readonly Guid _streamId;

            public AudioSplitterOutputStream(WeakPointer<IAudioGraph> graph, WeakPointer<AudioSplitter> parent) : base(graph, nameof(AudioSplitterOutputStream), nodeCustomName: null)
            {
                _parent = parent.AssertNonNull(nameof(parent));
                _streamId = Guid.NewGuid();
                OutputFormat = parent.Value.InputFormat;
            }

            public override bool PlaybackFinished
            {
                get
                {
                    return _parent.Value.Input == null ? false : _parent.Value.Input.PlaybackFinished;
                }
            }

            public async Task SplitterFlushOutputAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                if (Output != null)
                {
                    await Output.FlushAsync(cancelToken, realTime).ConfigureAwait(false);
                }
            }

            public async Task SplitterWriteOutputAsync(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                if (Output != null)
                {
                    await Output.WriteAsync(buffer, offset, count, cancelToken, realTime).ConfigureAwait(false);
                }
            }

            public override bool Equals(object obj)
            {
                if (obj == null || GetType() != obj.GetType())
                {
                    return false;
                }

                AudioSplitterOutputStream other = (AudioSplitterOutputStream)obj;
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
