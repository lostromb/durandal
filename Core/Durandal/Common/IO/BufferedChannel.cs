using Durandal.Common.Collections;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.IO
{
    /// <summary>
    /// A thread-safe queue for sending objects between two processes
    /// </summary>
    public class BufferedChannel<T> : IChannel<T>, IDisposable
    {
        private readonly int _waitGranularityMs;
        private readonly TimeSpan _waitGranularity;
        private readonly SemaphoreSlim _semaphore;
        private readonly ConcurrentQueue<T> _data;
        private int _disposed = 0;

        public BufferedChannel(int waitGranularityMs = 100)
        {
            _waitGranularityMs = Math.Max(1, waitGranularityMs);
            _waitGranularity = TimeSpan.FromMilliseconds(_waitGranularityMs);
            _data = new ConcurrentQueue<T>();
            _semaphore = new SemaphoreSlim(0, 1000);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~BufferedChannel()
        {
            Dispose(false);
        }
#endif

        private void SignalWrite()
        {
            try
            {
                if (_semaphore.CurrentCount < 3)
                {
                    _semaphore.Release();
                }
            }
            catch (ObjectDisposedException) { } // can happen if the channel closes while we're still waiting to read from it.
        }

        public ValueTask SendAsync(T data)
        {
            _data.Enqueue(data);
            SignalWrite();
            return new ValueTask();
        }

        public void Send(T data)
        {
            _data.Enqueue(data);
            SignalWrite();
        }

        public T Receive(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            T returnVal = default(T);
            bool got = false;

            if (_disposed != 0)
            {
                _data.TryDequeue(out returnVal);
                return returnVal;
            }

            if (realTime.IsForDebug)
            {
                // Slow path - use busy loop on wait provider
                while (!got)
                {
                    got = _data.TryDequeue(out returnVal);
                    if (_data.ApproximateCount > 0)
                    {
                        SignalWrite();
                    }

                    if (_disposed != 0) // End of stream
                    {
                        _data.TryDequeue(out returnVal);
                        return returnVal;
                    }

                    if (!got)
                    {
                        realTime.Wait(_waitGranularity, cancelToken);
                    }
                }
            }
            else
            {
                // Fast path - use async wait event
                while (!got)
                {
                    got = _data.TryDequeue(out returnVal);
                    if (_data.ApproximateCount > 0)
                    {
                        SignalWrite();
                    }

                    if (!got)
                    {
                        _semaphore.Wait(cancelToken);
                        if (_disposed != 0) // End of stream
                        {
                            SignalWrite(); // This method won't propagate an ObjectDisposedException if the semaphore is disposed
                            _data.TryDequeue(out returnVal);
                            return returnVal;
                        }
                    }
                }
            }

            return returnVal;
        }

        public async ValueTask<T> ReceiveAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            T returnVal = default(T);
            bool got = false;

            if (_disposed != 0)
            {
                _data.TryDequeue(out returnVal);
                return returnVal;
            }

            if (realTime.IsForDebug)
            {
                // Slow path
                while (!got)
                {
                    got = _data.TryDequeue(out returnVal);
                    if (_data.ApproximateCount > 0)
                    {
                        SignalWrite();
                    }

                    if (_disposed != 0)
                    {
                        _data.TryDequeue(out returnVal);
                        return returnVal;
                    }

                    if (!got)
                    {
                        await realTime.WaitAsync(_waitGranularity, cancelToken).ConfigureAwait(false);
                    }
                }
            }
            else
            {
                // Fast path
                while (!got)
                {
                    got = _data.TryDequeue(out returnVal);
                    if (_data.ApproximateCount > 0)
                    {
                        SignalWrite();
                    }

                    if (!got)
                    {
                        await _semaphore.WaitAsync(cancelToken).ConfigureAwait(false);
                        if (_disposed != 0) // End of stream
                        {
                            SignalWrite(); // This method won't propagate an ObjectDisposedException if the semaphore is disposed
                            _data.TryDequeue(out returnVal);
                            return returnVal;
                        }
                    }
                }
            }

            return returnVal;
        }

        public async ValueTask<T> ReceiveAsyncSlowPath(CancellationToken cancelToken, IRealTimeProvider realTime, TimeSpan spinwaitTime)
        {
            T returnVal = default(T);
            bool got = false;

            if (_disposed != 0)
            {
                _data.TryDequeue(out returnVal);
                return returnVal;
            }

            while (!got)
            {
                got = _data.TryDequeue(out returnVal);
                if (_data.ApproximateCount > 0)
                {
                    SignalWrite();
                }

                if (_disposed != 0) // End of stream
                {
                    _data.TryDequeue(out returnVal);
                    return returnVal;
                }

                if (!got)
                {
                    await realTime.WaitAsync(spinwaitTime, cancelToken).ConfigureAwait(false);
                }
            }

            return returnVal;
        }

        public RetrieveResult<T> TryReceive(CancellationToken cancelToken, IRealTimeProvider realTime, TimeSpan timeout)
        {
            if (_disposed != 0)
            {
                T returnVal;
                if (_data.TryDequeue(out returnVal))
                {
                    return new RetrieveResult<T>(returnVal, 0);
                }
                else
                {
                    return new RetrieveResult<T>();
                }
            }

            if (timeout == TimeSpan.Zero)
            {
                // Tentative read path - only block briefly enough to get the lock
                T returnVal;
                bool got = _data.TryDequeue(out returnVal);
                if (_data.ApproximateCount > 0)
                {
                    SignalWrite();
                }

                if (got)
                {
                    return new RetrieveResult<T>(returnVal, 0);
                }
                else
                {
                    return new RetrieveResult<T>();
                }
            }
            else if (!realTime.IsForDebug)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                try
                {
                    bool got = false;
                    T returnVal = default(T);
                    while (!got)
                    {
                        got = _data.TryDequeue(out returnVal);
                        if (_data.ApproximateCount > 0)
                        {
                            SignalWrite();
                        }

                        if (!got)
                        {
                            if (_disposed != 0) // End of stream
                            {
                                if (_data.TryDequeue(out returnVal))
                                {
                                    return new RetrieveResult<T>(returnVal, 0);
                                }
                                else
                                {
                                    return new RetrieveResult<T>();
                                }
                            }

                            TimeSpan timeRemaining = timeout - stopwatch.Elapsed;
                            if (timeRemaining <= TimeSpan.Zero)
                            {
                                // Out of time
                                return new RetrieveResult<T>(default(T), stopwatch.ElapsedMillisecondsPrecise(), false);
                            }

                            // Use the built-in timeout of the semaphore which prevents us from having to create a linked token source here
                            _semaphore.Wait(timeRemaining, cancelToken);
                        }
                    }

                    return new RetrieveResult<T>(returnVal, stopwatch.ElapsedMillisecondsPrecise());
                }
                catch (OperationCanceledException)
                {
                    return new RetrieveResult<T>(default(T), stopwatch.ElapsedMillisecondsPrecise(), false);
                }
            }
            else
            {
                // Slow path for non-realtime wait providers
                using (CancellationTokenSource timeoutCancelizer = new NonRealTimeCancellationTokenSource(realTime, timeout))
                {
                    using (CancellationTokenSource combinedCancelizer = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, timeoutCancelizer.Token))
                    {
                        try
                        {
                            T returnVal = Receive(combinedCancelizer.Token, realTime);
                            return new RetrieveResult<T>(returnVal, 0);
                        }
                        catch (OperationCanceledException)
                        {
                            return new RetrieveResult<T>(default(T), timeout.TotalMilliseconds, false);
                        }
                    }
                }
            }
        }

        public async ValueTask<RetrieveResult<T>> TryReceiveAsync(CancellationToken cancelToken, IRealTimeProvider realTime, TimeSpan timeout)
        {
            if (_disposed != 0)
            {
                T returnVal;
                if (_data.TryDequeue(out returnVal))
                {
                    return new RetrieveResult<T>(returnVal, 0);
                }
                else
                {
                    return new RetrieveResult<T>();
                }
            }

            if (timeout == TimeSpan.Zero)
            {
                // Tentative read path - only block briefly enough to get the lock
                T returnVal;
                bool got = _data.TryDequeue(out returnVal);
                if (_data.ApproximateCount > 0)
                {
                    SignalWrite();
                }

                if (got)
                {
                    return new RetrieveResult<T>(returnVal, 0);
                }
                else
                {
                    return new RetrieveResult<T>();
                }
            }
            else if (!realTime.IsForDebug)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                try
                {
                    bool got = false;
                    T returnVal = default(T);
                    while (!got)
                    {
                        got = _data.TryDequeue(out returnVal);
                        if (_data.ApproximateCount > 0)
                        {
                            SignalWrite();
                        }

                        if (!got)
                        {
                            if (_disposed != 0) // End of stream
                            {
                                if (_data.TryDequeue(out returnVal))
                                {
                                    return new RetrieveResult<T>(returnVal, 0);
                                }
                                else
                                {
                                    return new RetrieveResult<T>();
                                }
                            }

                            TimeSpan timeRemaining = timeout - stopwatch.Elapsed;
                            if (timeRemaining <= TimeSpan.Zero)
                            {
                                // Out of time
                                return new RetrieveResult<T>(default(T), stopwatch.ElapsedMillisecondsPrecise(), false);
                            }

                            // Use the built-in timeout of the semaphore which prevents us from having to create a linked token source here
                            await _semaphore.WaitAsync(timeRemaining, cancelToken).ConfigureAwait(false);
                        }
                    }

                    return new RetrieveResult<T>(returnVal, stopwatch.ElapsedMillisecondsPrecise());
                }
                catch (OperationCanceledException)
                {
                    return new RetrieveResult<T>(default(T), stopwatch.ElapsedMillisecondsPrecise(), false);
                }
            }
            else
            {
                // Slow path for non-realtime wait providers
                using (CancellationTokenSource timeoutCancelizer = new NonRealTimeCancellationTokenSource(realTime, timeout))
                {
                    using (CancellationTokenSource combinedCancelizer = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, timeoutCancelizer.Token))
                    {
                        try
                        {
                            Stopwatch timer = Stopwatch.StartNew();

                            T returnVal = default(T);
                            bool got = false;
                            if (realTime.IsForDebug)
                            {
                                double msWaited = 0;
                                double maxMsToWait = timeout.TotalMilliseconds;
                                while (!got && msWaited < maxMsToWait)
                                {
                                    got = _data.TryDequeue(out returnVal);
                                    if (_data.ApproximateCount > 0)
                                    {
                                        SignalWrite();
                                    }

                                    if (!got)
                                    {
                                        double msCanWait = Math.Min(_waitGranularityMs, maxMsToWait - msWaited);
                                        if (msCanWait > 0)
                                        {
                                            await realTime.WaitAsync(TimeSpan.FromMilliseconds(msCanWait), cancelToken).ConfigureAwait(false);
                                            msWaited += msCanWait;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                returnVal = await ReceiveAsync(combinedCancelizer.Token, realTime).ConfigureAwait(false);
                            }

                            timer.Stop();
                            return new RetrieveResult<T>(returnVal, timer.ElapsedMillisecondsPrecise());
                        }
                        catch (OperationCanceledException)
                        {
                            return new RetrieveResult<T>(default(T), timeout.TotalMilliseconds, false);
                        }
                    }
                }
            }
        }

        public void Clear()
        {
            _data.Clear();
        }

        public int ApproxItemsInBuffer
        {
            get
            {
                return _data.ApproximateCount;
            }
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
                _semaphore.Release(500);
                _semaphore.Dispose();

                // TODO do we dispose of IDisposable items left inside the buffer?
            }
        }
    }
}
