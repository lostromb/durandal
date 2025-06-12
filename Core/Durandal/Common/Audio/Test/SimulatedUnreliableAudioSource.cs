using Durandal.Common.Audio;
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
    /// Audio filter used for testing which randomly reports 0 samples available on read.
    /// This is useful for exercising things like buffer underflows and empty reads in unexpected places.
    /// </summary>
    public class SimulatedUnreliableAudioSource : AbstractAudioSampleFilter
    {
        private readonly IRandom _rand;
        private float _chanceOfFailure;
        private float _chanceOfPartialRead;

        /// <summary>
        /// Creates an audio filter which will pass through the input on read only some of the time.
        /// </summary>
        /// <param name="graph">The graph that this will be a part of</param>
        /// <param name="format">The audio format</param>
        /// <param name="rand">A random provider</param>
        /// <param name="chanceOfFailure">The chance of any given read failing, expressed as a float between [0.0, 1.0]</param>
        /// <param name="chanceOfPartialRead">The chance of a failing read returning partial data, expressed as a float between [0.0, 1.0]</param>
        public SimulatedUnreliableAudioSource(WeakPointer<IAudioGraph> graph, AudioSampleFormat format, IRandom rand, float chanceOfFailure = 0.5f, float chanceOfPartialRead = 0.5f)
            : base(graph, nameof(SimulatedUnreliableAudioSource), nodeCustomName: null)
        {
            InputFormat = format.AssertNonNull(nameof(format));
            OutputFormat = format;
            _rand = rand.AssertNonNull(nameof(rand));
            if (chanceOfFailure < 0 || chanceOfFailure > 1)
            {
                throw new ArgumentOutOfRangeException("Chance of failure must be between 0.0 and 1.0");
            }

            if (chanceOfPartialRead < 0 || chanceOfPartialRead > 1)
            {
                throw new ArgumentOutOfRangeException("Chance of partial read must be between 0.0 and 1.0");
            }

            _chanceOfFailure = chanceOfFailure;
            _chanceOfPartialRead = chanceOfPartialRead;
        }

        public float ChanceOfFailure
        {
            get
            {
                return _chanceOfFailure;
            }
            set
            {
                _chanceOfFailure = value;
            }
        }

        protected override async ValueTask<int> ReadAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_rand.NextFloat() <= _chanceOfFailure)
            {
                // Decide whether to return zero or do a partial read
                if (count == 1 || _rand.NextFloat() >= _chanceOfPartialRead)
                {
                    // Report no data available
                    return 0;
                }
                else
                {
                    // Report only partial data available
                    return await Input.ReadAsync(buffer, offset, _rand.NextInt(1, count), cancelToken, realTime);
                }
            }

            return await Input.ReadAsync(buffer, offset, count, cancelToken, realTime);
        }

        protected override async ValueTask WriteAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            await Output.WriteAsync(buffer, offset, count, cancelToken, realTime);
        }
    }
}
