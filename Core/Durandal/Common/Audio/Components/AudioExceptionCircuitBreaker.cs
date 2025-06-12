using Durandal.Common.Audio.WebRtc;
using Durandal.Common.Collections;
using Durandal.Common.Events;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
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
    /// Audio graph filter which monitors any exceptions that happen on read/write and then "trips"
    /// if one happens, causing any future reads/writes to be a no-op.
    /// </summary>
    public sealed class AudioExceptionCircuitBreaker : AbstractAudioSampleFilter
    {
        private readonly ILogger _logger;
        private int _breakerTriggered = 0;

        public AudioExceptionCircuitBreaker(WeakPointer<IAudioGraph> graph, AudioSampleFormat format, string nodeCustomName, ILogger logger)
            : base(graph, nameof(AudioExceptionCircuitBreaker), nodeCustomName)
        {
            InputFormat = format.AssertNonNull(nameof(format));
            OutputFormat = format;
            _logger = logger.AssertNonNull(nameof(logger));
            ExceptionRaisedEvent = new AsyncEvent<EventArgs>();
        }

        public AsyncEvent<EventArgs> ExceptionRaisedEvent { get; private set; }

        public override bool PlaybackFinished => _playbackFinished || _breakerTriggered != 0;

        protected override async ValueTask<int> ReadAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (PlaybackFinished)
            {
                return -1;
            }

            if (_breakerTriggered == 0)
            {
                try
                {
                    int streamReturnVal = await Input.ReadAsync(buffer, offset, count, cancelToken, realTime).ConfigureAwait(false);
                    if (streamReturnVal < 0)
                    {
                        _playbackFinished = true;
                    }

                    return streamReturnVal;
                }
                catch (Exception e)
                {
                    if (AtomicOperations.ExecuteOnce(ref _breakerTriggered))
                    {
                        _logger.Log(e);
                        ExceptionRaisedEvent.FireInBackground(this, new EventArgs(), _logger, realTime);
                    }
                }
            }

            return -1;
        }

        protected override async ValueTask WriteAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_breakerTriggered == 0)
            {
                try
                {
                    await Output.WriteAsync(buffer, offset, count, cancelToken, realTime).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    if (AtomicOperations.ExecuteOnce(ref _breakerTriggered))
                    {
                        _logger.Log(e);
                        ExceptionRaisedEvent.FireInBackground(this, new EventArgs(), _logger, realTime);
                    }
                }
            }
        }
    }
}
