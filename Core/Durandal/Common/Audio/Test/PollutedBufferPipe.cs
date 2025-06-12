using Durandal.Common.Audio;
using Durandal.Common.Collections;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Test
{
    /// <summary>
    /// Used to simulate the same write target buffer getting reused multiple times without clearing in between
    /// </summary>
    public class PollutedBufferPipe : AbstractAudioSampleFilter
    {
        private const int SCRATCH_SIZE_SAMPLES_PER_CHANNEL = 128; // use small read/write sizes to test busy loops
        private const int SCRATCH_OFFSET = 100; // use a constant offset in all our operations to test that offsets are calculated properly

        private readonly IRandom _rand = new FastRandom();
        private readonly float[] _scratch;

        public PollutedBufferPipe(WeakPointer<IAudioGraph> graph, AudioSampleFormat format)
            : base(graph, nameof(PollutedBufferPipe), nodeCustomName: null)
        {
            InputFormat = format.AssertNonNull(nameof(format));
            OutputFormat = format;
            _scratch = new float[SCRATCH_OFFSET + (SCRATCH_SIZE_SAMPLES_PER_CHANNEL * format.NumChannels)];
        }

        private void PolluteBuffer()
        {
            for (int c = 0; c < _scratch.Length; c++)
            {
                _scratch[c] = _rand.NextFloat();
            }
        }

        protected override async ValueTask<int> ReadAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int samplesPerChannelReadFromInputTotal = 0;
            while (samplesPerChannelReadFromInputTotal < count)
            {
                PolluteBuffer();
                int samplesPerChannelCanReadFromInput = Math.Min(count - samplesPerChannelReadFromInputTotal, SCRATCH_SIZE_SAMPLES_PER_CHANNEL);
                int samplesPerChannelActuallyReadFromInput = await Input.ReadAsync(
                    _scratch,
                    SCRATCH_OFFSET,
                    samplesPerChannelCanReadFromInput,
                    cancelToken,
                    realTime).ConfigureAwait(false);

                if (samplesPerChannelActuallyReadFromInput == 0)
                {
                    break;
                }
                else if (samplesPerChannelActuallyReadFromInput < 0)
                {
                    if (samplesPerChannelReadFromInputTotal == 0)
                    {
                        return -1;
                    }
                    else
                    {
                        return samplesPerChannelReadFromInputTotal;
                    }
                }
                else
                {
                    ArrayExtensions.MemCopy(
                        _scratch,
                        SCRATCH_OFFSET,
                        buffer,
                        (offset + (samplesPerChannelReadFromInputTotal * InputFormat.NumChannels)),
                        samplesPerChannelActuallyReadFromInput * InputFormat.NumChannels);
                    samplesPerChannelReadFromInputTotal += samplesPerChannelActuallyReadFromInput;
                }
            }

            return samplesPerChannelReadFromInputTotal;
        }

        protected override async ValueTask WriteAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int samplesPerChannelWritten = 0;
            while (samplesPerChannelWritten < count)
            {
                PolluteBuffer();
                int samplesPerChannelCanReadFromInput = Math.Min(count - samplesPerChannelWritten, SCRATCH_SIZE_SAMPLES_PER_CHANNEL);
                ArrayExtensions.MemCopy(
                    buffer,
                    (offset + (samplesPerChannelWritten * InputFormat.NumChannels)),
                    _scratch,
                    SCRATCH_OFFSET,
                    samplesPerChannelCanReadFromInput * InputFormat.NumChannels);

                await Output.WriteAsync(_scratch, SCRATCH_OFFSET, samplesPerChannelCanReadFromInput, cancelToken, realTime);
                samplesPerChannelWritten += samplesPerChannelCanReadFromInput;
            }
        }
    }
}
