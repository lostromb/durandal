using Durandal.Common.MathExt;
using Durandal.Common.Security.Login;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Tasks
{
    /// <summary>
    /// Implements an async (threadID-agnostic) mutex based internally on <see cref="SemaphoreSlim"/>, which is also aware
    /// of non-realtime semantics so that locks held during unit tests advance the clock as expected.
    /// </summary>
    public class AsyncLockSlim : IDisposable
    {
        // The number of times we spinwait on a lock before sleeping
        private const int NUM_SPIN_ITERATIONS = 100;

        private static readonly TimeSpan BACKOFF_TIME = TimeSpan.FromMilliseconds(1);

        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 2); // Go up to 2 maximum so we can detect callers who release the lock without holding it and throw an exception
        //private string _whoOwnsLock = string.Empty;

        private int _disposed = 0;

        public AsyncLockSlim()
        {
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~AsyncLockSlim()
        {
            Dispose(false);
        }
#endif

        public void GetLock()
        {
            GetLock(CancellationToken.None, DefaultRealTimeProvider.Singleton);
        }

        public void GetLock(CancellationToken cancelToken)
        {
            GetLock(cancelToken, DefaultRealTimeProvider.Singleton);
        }

        public void GetLock(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (realTime == null)
            {
                throw new ArgumentNullException(nameof(realTime));
            }

            if (!realTime.IsForDebug)
            {
                _lock.Wait(cancelToken);
            }
            else
            {   
                // Non-realtime path for unit testing
                while (!cancelToken.IsCancellationRequested)
                {
                    for (int spin = 0; spin < NUM_SPIN_ITERATIONS; spin++)
                    {
                        if (TryGetLock())
                        {
                            //_whoOwnsLock = GetCallStack();
                            return;
                        }
                    }

                    realTime.Wait(BACKOFF_TIME, cancelToken);
                }

                cancelToken.ThrowIfCancellationRequested();
            }
        }

        public bool TryGetLock()
        {
            bool gotLock = _lock.Wait(0);
            if (gotLock)
            {
                //_whoOwnsLock = GetCallStack();
            }

            return gotLock;
        }

        public ValueTask GetLockAsync()
        {
            return GetLockAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton);
        }

        public ValueTask GetLockAsync(CancellationToken cancelToken)
        {
            return GetLockAsync(cancelToken, DefaultRealTimeProvider.Singleton);
        }

        public async ValueTask GetLockAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (realTime == null)
            {
                throw new ArgumentNullException(nameof(realTime));
            }

            if (!realTime.IsForDebug)
            {
                await _lock.WaitAsync(cancelToken);
            }
            else
            {
                // consume virtual time on the nonrealtime path
                while (!cancelToken.IsCancellationRequested)
                {
                    for (int spin = 0; spin < NUM_SPIN_ITERATIONS; spin++)
                    {
                        if (TryGetLock())
                        {
                            //_whoOwnsLock = GetCallStack();
                            return;
                        }
                    }

                    await realTime.WaitAsync(BACKOFF_TIME, cancelToken).ConfigureAwait(false);
                }

                cancelToken.ThrowIfCancellationRequested();
            }
        }

        public void Release()
        {
            if (_lock.Release() != 0)
            {
                throw new InvalidOperationException("Cannot release an unlocked " + nameof(AsyncLockSlim));
            }

            //_whoOwnsLock = string.Empty;
        }

        //        private string GetCallStack()
        //        {
        //#if NETFRAMEWORK
        //            StackTrace trace = new StackTrace();
        //            return trace.ToString();
        //#else
        //            try
        //            {
        //                throw new DivideByZeroException();
        //            }
        //            catch (Exception e)
        //            {
        //                return e.StackTrace;
        //            }
        //#endif
        //        }

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
                _lock.Dispose();
            }
        }
    }
}
