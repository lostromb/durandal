using Durandal.API;
using Durandal.Common.Collections;
using Durandal.Common.Logger;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.ServiceMgmt
{
    public class ServiceResolver<T> : IServiceResolver<T>
        where T : class
    {
        private static readonly TimeSpan DISPOSAL_CHECK_INTERVAL = TimeSpan.FromSeconds(60);
        private readonly ILogger _errorLogger;
        private readonly FastConcurrentHashSet<GarbageHandle<T>> _obsoleteReferences;
        private readonly IRealTimeProvider _realTime;
        private T _currentReference;
        private int _disposed = 0;

        public ServiceResolver(ILogger errorLogger)
        {
            _errorLogger = errorLogger.AssertNonNull(nameof(errorLogger));
            _obsoleteReferences = new FastConcurrentHashSet<GarbageHandle<T>>();
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~ServiceResolver()
        {
            Dispose(false);
        }
#endif

        /// <inheritdoc />
        public T ResolveService()
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(ServiceResolver<T>));
            }

            CheckOldDisposableReferences(_realTime);
            return _currentReference;
        }

        public void SetServiceImplementation(T implementation, TimeSpan timeUntilOldOneDisposes)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(ServiceResolver<T>));
            }

            T oldImpl = Interlocked.Exchange(ref _currentReference, implementation);
            if (oldImpl != null)
            {
                // Enqueue the old object
                _obsoleteReferences.Add(new GarbageHandle<T>(oldImpl, _realTime, timeUntilOldOneDisposes));
            }
        }

        /// <inheritdoc/>
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
                if (_currentReference is IDisposable)
                {
                    ((IDisposable)_currentReference)?.Dispose();
                    _currentReference = null;
                }
            }
        }

        private void CheckOldDisposableReferences(IRealTimeProvider realTime)
        {
            DateTimeOffset currentTime = realTime.Time;
            var oldReferenceEnumerator = _obsoleteReferences.GetValueEnumerator();
            while (oldReferenceEnumerator.MoveNext())
            {
                GarbageHandle<T> oldReference = oldReferenceEnumerator.Current;
                if (oldReference._nextCheckTime > currentTime)
                {
                    continue;
                }

                // Are we waiting for disposal still?
                if (oldReference._strongRef != null)
                {
                    if (oldReference._dontDisposeBefore < currentTime)
                    {
                        // Dispose of it. Make sure we also clear the strong reference so the GC tracker can work properly.
                        if (oldReference._strongRef is IDisposable)
                        {
                            ((IDisposable)oldReference._strongRef).Dispose();
                        }

                        oldReference._strongRef = null;
                    }

                    // It's not ready for disposal yet. Ignore it for now.
                    continue;
                }

                T target;
                if (!oldReference._weakRef.TryGetTarget(out target) || target == null)
                {
                    _obsoleteReferences.Remove(oldReference);
                    continue;
                }

                _errorLogger.LogFormat(
                    LogLevel.Wrn,
                    DataPrivacyClassification.SystemMetadata,
                    "Someone is still holding a reference to {0} which should have been disposed. Please get a memory dump and !GCRoot to diagnose.",
                    target.GetType().FullName);
                oldReference._nextCheckTime = currentTime + DISPOSAL_CHECK_INTERVAL;
            }
        }

        private class GarbageHandle<E> where E : class
        {
            private readonly Guid _uuid;
            public readonly WeakReference<E> _weakRef;
            public DateTimeOffset _nextCheckTime;
            public readonly DateTimeOffset _dontDisposeBefore;
            public E _strongRef;

            public GarbageHandle(E toObserve, IRealTimeProvider realTime, TimeSpan disposeAt)
            {
                _weakRef = new WeakReference<E>(toObserve.AssertNonNull(nameof(toObserve)));
                _nextCheckTime = realTime.Time + DISPOSAL_CHECK_INTERVAL;
                _uuid = Guid.NewGuid();
                _strongRef = toObserve.AssertNonNull(nameof(toObserve));
                _dontDisposeBefore = realTime.Time + disposeAt;
            }

            public override int GetHashCode()
            {
                return _uuid.GetHashCode();
            }

            public override bool Equals(object obj)
            {
                if (obj is GarbageHandle<E>)
                {
                    return _uuid == (obj as GarbageHandle<E>)._uuid;
                }

                return false;
            }
        }
    }
}
