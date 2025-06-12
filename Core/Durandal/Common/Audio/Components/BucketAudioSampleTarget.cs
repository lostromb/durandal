using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Tasks;
using Durandal.Common.IO;
using Durandal.Common.Utils;
using Durandal.Common.MathExt;
using Durandal.Common.Collections;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Audio.Components
{
    /// <summary>
    /// Audio sample target which dumps input audio samples into a "bucket" that can be pulled out later.
    /// </summary>
    public sealed class BucketAudioSampleTarget : AbstractAudioSampleTarget
    {
        private readonly Queue<BufferSegment> _buffer = new Queue<BufferSegment>();
        private int _samplesPerChannelInBuffer = 0;
        private int _disposed = 0;

        public BucketAudioSampleTarget(WeakPointer<IAudioGraph> graph, AudioSampleFormat format, string nodeCustomName)
            : base(graph, nameof(BucketAudioSampleTarget), nodeCustomName)
        {
            InputFormat = format;
        }

        /// <summary>
        /// Gets the number of samples per channel of audio data currently stored in this buffer
        /// </summary>
        public int SamplesPerChannelInBucket => _samplesPerChannelInBuffer;

        /// <summary>
        /// Retrieves all captured audio as a single audio sample.
        /// </summary>
        /// <returns>A copy of this bucket's data as a contiguous sample.</returns>
        public AudioSample GetAllAudio()
        {
            InputGraph.LockGraph();
            try
            {
                float[] returnData = new float[_samplesPerChannelInBuffer * InputFormat.NumChannels];
                int cursor = 0;
                foreach (BufferSegment buf in _buffer)
                {
                    ArrayExtensions.MemCopy(buf.PooledBuffer.Buffer, 0, returnData, cursor, buf.Count);
                    cursor += buf.Count;
                }

                return new AudioSample(new ArraySegment<float>(returnData), InputFormat);
            }
            finally
            {
                InputGraph.UnlockGraph();
            }
        }

        /// <summary>
        /// Removes all stored data from this bucket.
        /// </summary>
        public void ClearBucket()
        {
            InputGraph.LockGraph();
            try
            {
                foreach (BufferSegment buf in _buffer)
                {
                    buf.PooledBuffer.Dispose();
                }

                _buffer.Clear();
                _samplesPerChannelInBuffer = 0;
            }
            finally
            {
                InputGraph.UnlockGraph();
            }
        }

        /// <summary>
        /// Reads as much data as possible from the input until the input is exhausted.
        /// </summary>
        /// <param name="cancelToken"></param>
        /// <param name="realTime"></param>
        /// <returns></returns>
        public async Task ReadFully(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            while (!Input.PlaybackFinished)
            {
                await ReadSamplesFromInput(BufferPool<float>.DEFAULT_BUFFER_SIZE, cancelToken, realTime).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Reads as much data as possible from the input until the input is exhausted or the specified amount of audio is read.
        /// </summary>
        /// <param name="cancelToken"></param>
        /// <param name="realTime"></param>
        /// <param name="maxAmountToRead">The maximum length of audio to read</param>
        /// <returns></returns>
        public async Task ReadFully(CancellationToken cancelToken, IRealTimeProvider realTime, TimeSpan maxAmountToRead)
        {
            int maxSamplesPerChannelToRead = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(InputFormat.NumChannels, maxAmountToRead);
            int samplesRead = 0;
            while (!Input.PlaybackFinished && samplesRead < maxSamplesPerChannelToRead)
            {
                int thisRead = await ReadSamplesFromInput(FastMath.Min(BufferPool<float>.DEFAULT_BUFFER_SIZE, maxSamplesPerChannelToRead - samplesRead), cancelToken, realTime).ConfigureAwait(false);
                if (thisRead > 0)
                {
                    samplesRead += thisRead;
                }
            }
        }

        /// <summary>
        /// Reads the specified number of samples per channel from the input and puts them into the bucket.
        /// </summary>
        /// <param name="samplesPerChannelToRead">The maximum number of samples to read from input</param>
        /// <param name="cancelToken">Cancel token for the read</param>
        /// <param name="realTime">Real time definition</param>
        /// <returns>The number of samples per channel actually read. If end of stream, return -1</returns>
        public async Task<int> ReadSamplesFromInput(int samplesPerChannelToRead, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (samplesPerChannelToRead <= 0)
            {
                throw new ArgumentOutOfRangeException("Samples requested must be a positive integer");
            }

            int samplesPerChannelActuallyRead = 0;
            while (samplesPerChannelActuallyRead < samplesPerChannelToRead)
            {
                int thisReadMaxSizeSamplesPerChannel = FastMath.Min(BufferPool<float>.DEFAULT_BUFFER_SIZE / InputFormat.NumChannels, samplesPerChannelToRead - samplesPerChannelActuallyRead);
                PooledBuffer<float> pooledBuffer = BufferPool<float>.Rent(thisReadMaxSizeSamplesPerChannel * InputFormat.NumChannels);
                await InputGraph.LockGraphAsync(cancelToken, realTime).ConfigureAwait(false);
                try
                {
                    InputGraph.BeginInstrumentedScope(realTime, NodeFullName);
                    if (Input == null)
                    {
                        return samplesPerChannelActuallyRead;
                    }

                    int thisReadActualSizeSamplesPerChannel = await Input.ReadAsync(
                        pooledBuffer.Buffer,
                        0,
                        thisReadMaxSizeSamplesPerChannel,
                        cancelToken,
                        realTime).ConfigureAwait(false);

                    if (thisReadActualSizeSamplesPerChannel < 0)
                    {
                        return samplesPerChannelActuallyRead == 0 ? -1 : samplesPerChannelActuallyRead;
                    }
                    else if (thisReadActualSizeSamplesPerChannel == 0)
                    {
                        return samplesPerChannelActuallyRead;
                    }
                    else if (thisReadActualSizeSamplesPerChannel > 0)
                    {
                        _buffer.Enqueue(new BufferSegment(pooledBuffer, thisReadActualSizeSamplesPerChannel * InputFormat.NumChannels));
                        _samplesPerChannelInBuffer += thisReadActualSizeSamplesPerChannel;
                        samplesPerChannelActuallyRead += thisReadActualSizeSamplesPerChannel;
                        pooledBuffer = null;
                    }
                }
                finally
                {
                    InputGraph.EndInstrumentedScope(realTime, AudioMath.ConvertSamplesPerChannelToTimeSpan(InputFormat.SampleRateHz, samplesPerChannelToRead));
                    InputGraph.UnlockGraph();
                    pooledBuffer?.Dispose();
                }
            }

            return samplesPerChannelActuallyRead;
        }

        protected override void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            try
            {
                if (disposing)
                {
                    ClearBucket();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        protected override ValueTask WriteAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int samplesPerChannelCopied = 0;
            while (samplesPerChannelCopied < count)
            {
                int thisWriteSizeSamplesPerChannel = FastMath.Min(BufferPool<float>.DEFAULT_BUFFER_SIZE / InputFormat.NumChannels, count - samplesPerChannelCopied);
                PooledBuffer<float> pooledBuffer = BufferPool<float>.Rent(thisWriteSizeSamplesPerChannel * InputFormat.NumChannels);
                try
                {
                    ArrayExtensions.MemCopy(
                        buffer,
                        (offset + (samplesPerChannelCopied * InputFormat.NumChannels)),
                        pooledBuffer.Buffer,
                        0,
                        thisWriteSizeSamplesPerChannel * InputFormat.NumChannels);
                    _buffer.Enqueue(new BufferSegment(pooledBuffer, thisWriteSizeSamplesPerChannel * InputFormat.NumChannels));
                    _samplesPerChannelInBuffer += thisWriteSizeSamplesPerChannel;
                    samplesPerChannelCopied += thisWriteSizeSamplesPerChannel;
                    pooledBuffer = null;
                }
                finally
                {
                    pooledBuffer?.Dispose();
                }
            }

            return new ValueTask();
        }

        /// <summary>
        /// Value struct for entries in the buffer. Uses pooled float arrays to try and save some memory
        /// </summary>
        private struct BufferSegment
        {
            public BufferSegment(PooledBuffer<float> buf, int count)
            {
                PooledBuffer = buf;
                Count = count;
            }

            public PooledBuffer<float> PooledBuffer;
            public int Count;
        }
    }
}
