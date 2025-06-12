using Durandal.Common.Client.Actions;
using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Net.Http2.Session;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Newtonsoft.Json.Bson;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net.Http2
{
    internal class Http2PriorityQueue : IDisposable
    {
        // This code is copied from BufferedChannel, except that there are multiple internal queues (for varying priorities)
        // instead of just a single FIFO queue.

        /// <summary>
        /// The maximum amount of time an outgoing data stream can be empty and untouched before we
        /// cull it from the queue to avoid memory leaks (assuming that stream was cancelled or otherwise cut off).
        /// </summary>
        private static readonly TimeSpan MAX_STREAM_IDLE_TIME = TimeSpan.FromMinutes(2);

        private static readonly IRandom _rand = new FastRandom();
        private const int NUM_PRIORITIES = 6;
        private readonly TimeSpan _debugWaitGranularity = TimeSpan.FromMilliseconds(10);
        private readonly SemaphoreSlim _semaphore;
        private readonly ConcurrentQueue<ISessionCommand>[] _queues;
        private readonly FastConcurrentDictionary<int, DataUploadQueue> _dataStreams;
        private int _approximateCount;
        private int _disposed = 0;

        public Http2PriorityQueue()
        {
            _queues = new ConcurrentQueue<ISessionCommand>[NUM_PRIORITIES];
            for (int c = 0; c < NUM_PRIORITIES; c++)
            {
                if (c != (int)HttpPriority.Data)
                {
                    _queues[c] = new ConcurrentQueue<ISessionCommand>();
                }
            }

            _semaphore = new SemaphoreSlim(0, 1000);
            _dataStreams = new FastConcurrentDictionary<int, DataUploadQueue>();
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~Http2PriorityQueue()
        {
            Dispose(false);
        }
#endif

        public void Enqueue(ISessionCommand toSend, HttpPriority priority)
        {
            if (priority == HttpPriority.Data)
            {
                throw new InvalidOperationException("Use EnqueueData to send data frames");
            }

            _queues[(int)priority].Enqueue(toSend);
            Interlocked.Increment(ref _approximateCount);
            SignalWrite();
        }

        /// <summary>
        /// Enqueues a data frame specifically
        /// </summary>
        /// <param name="toSend">The frame to send</param>
        /// <param name="realTime">Real time, used for timestamping the command</param>
        public void EnqueueData(SendDataFrameCommand toSend, IRealTimeProvider realTime)
        {
            DataUploadQueue streamQueue;
            _dataStreams.TryGetValueOrSet(toSend.ToSend.StreamId, out streamQueue, () => new DataUploadQueue());
            streamQueue.LastWriteTimeMs = realTime.Time;
            streamQueue.Queue.Enqueue(toSend);
            Interlocked.Increment(ref _approximateCount);
            SignalWrite();
        }

        /// <summary>
        /// Enqueues a trailers frame associated with a particular data stream.
        /// </summary>
        /// <param name="toSend">The frame to send</param>
        /// <param name="realTime">Real time, used for timestamping the command</param>
        public void EnqueueTrailers(SendResponseTrailerBlockCommand toSend, IRealTimeProvider realTime)
        {
            DataUploadQueue streamQueue;
            _dataStreams.TryGetValueOrSet(toSend.StreamId, out streamQueue, () => new DataUploadQueue());
            streamQueue.LastWriteTimeMs = realTime.Time;
            streamQueue.Queue.Enqueue(toSend);
            Interlocked.Increment(ref _approximateCount);
            SignalWrite();
        }

        public async ValueTask<ISessionCommand> Dequeue(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            ISessionCommand returnVal = null;
            bool got = false;

            // Quick tentative check if we are being disposed
            if ( _disposed != 0)
            {
                TryMultiDequeue(out returnVal, realTime);
                return returnVal;
            }

            if (realTime.IsForDebug)
            {
                // Slow path
                while (!got)
                {
                    got = TryMultiDequeue(out returnVal, realTime);
                    if (got && _approximateCount > 0)
                    {
                        // technically the semaphore never gets released in this path because it's based on spinwait.
                        // That might be considered a performance bug but one which should only affect unit tests
                        SignalWrite();
                    }

                    if (_disposed != 0)
                    {
                        TryMultiDequeue(out returnVal, realTime);
                        return returnVal;
                    }

                    if (!got)
                    {
                        await realTime.WaitAsync(_debugWaitGranularity, cancelToken).ConfigureAwait(false);
                    }
                }
            }
            else
            {
                // Fast path
                while (!got)
                {
                    got = TryMultiDequeue(out returnVal, realTime);

                    if (got)
                    {
                        if (_approximateCount > 0)
                        {
                            SignalWrite();
                        }
                    }
                    else
                    {
                        await _semaphore.WaitAsync(cancelToken).ConfigureAwait(false);
                        if (_disposed != 0) // End of stream
                        {
                            _semaphore.Release();
                            TryMultiDequeue(out returnVal, realTime);
                            return returnVal;
                        }
                    }
                }
            }

            return returnVal;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                // Set the write signal which will tell readers to pick up on the EOF
                _semaphore.Release();
                _semaphore.Dispose();
            }
        }

        /// <summary>
        /// Queue from the array of queues in descending priority order
        /// </summary>
        /// <param name="command"></param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns></returns>
        private bool TryMultiDequeue(out ISessionCommand command,  IRealTimeProvider realTime)
        {
            for (int priority = 0; priority < NUM_PRIORITIES; priority++)
            {
                if (priority != (int)HttpPriority.Data)
                {
                    // Regular non-data priority queues
                    if (_queues[priority].TryDequeue(out command))
                    {
                        Interlocked.Decrement(ref _approximateCount);
                        return true;
                    }
                }
                else
                {
                    // This is the data layer. Pick a random data queue and see if there are any which have values to send

                    // We have a loop set up here because of this unconventional enumeration. We are
                    // iterating through and also pruning the queue dictionary at the same time.
                    // We want to make sure that, if there is actually valid data to dequeue, we don't skip
                    // over it by accident and wait when we don't need to.
                    bool modifiedCollection = true;
                    while (modifiedCollection)
                    {
                        modifiedCollection = false;
                        IEnumerator<KeyValuePair<int, DataUploadQueue>> dataStreamEnumerator = _dataStreams.GetRandomEnumerator(_rand);
                        while (dataStreamEnumerator.MoveNext())
                        {
                            // Always try and dequeue if the stream is non-empty
                            if (!dataStreamEnumerator.Current.Value.Queue.IsEmpty)
                            {
                                ISessionCommand nextCommand;
                                if (dataStreamEnumerator.Current.Value.Queue.TryDequeue(out nextCommand))
                                {
                                    bool isEndOfStream;
                                    if (nextCommand is SendDataFrameCommand)
                                    {
                                        isEndOfStream = (nextCommand as SendDataFrameCommand).ToSend.EndStream;
                                    }
                                    else if (nextCommand is SendResponseTrailerBlockCommand)
                                    {
                                        isEndOfStream = true;
                                    }
                                    else
                                    {
                                        throw new InvalidCastException("Unexpected data frame type in HTTP2 data stream: " + nextCommand.GetType().Name);
                                    }

                                    if (isEndOfStream)
                                    {
                                        // Reached end of stream, so clean up the queue
                                        _dataStreams.Remove(dataStreamEnumerator.Current.Key);
                                        //modifiedCollection = true;
                                    }

                                    command = nextCommand;
                                    Interlocked.Decrement(ref _approximateCount);
                                    return true;
                                }
                            }
                            else if (dataStreamEnumerator.Current.Value.LastWriteTimeMs + MAX_STREAM_IDLE_TIME < realTime.Time)
                            {
                                // Stream is empty. If it has expired, cull it
                                _dataStreams.Remove(dataStreamEnumerator.Current.Key);
                                modifiedCollection = true;
                            }
                        }
                    }
                }
            }

            command = null;
            return false;
        }

        private void SignalWrite()
        {
            if (_semaphore.CurrentCount < 3)
            {
                _semaphore.Release();
            }
        }

        private sealed class DataUploadQueue
        {
            public DataUploadQueue()
            {
                Queue = new ConcurrentQueue<ISessionCommand>();
            }

            public readonly ConcurrentQueue<ISessionCommand> Queue;
            public DateTimeOffset LastWriteTimeMs;
        }
    }
}
