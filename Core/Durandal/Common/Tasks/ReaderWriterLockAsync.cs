using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Tasks
{
    /// <summary>
    /// A simple implementation of a reader/writer lock that is suitable for async environments.
    /// Most built-in mutexes use thread ID to determine who holds a lock. However, in an async context,
    /// the thread ID may change mid-function, which causes lots of problems.
    /// This class solves that by passing around a token that represents an async task's handle on a lock,
    /// which is independent of thread ID.
    /// </summary>
    public class ReaderWriterLockAsync : IDisposable
    {
        private const int WRITE_LOCK_TOKEN = 2;
        private const int READ_LOCK_TOKEN = 1;
        private readonly int _maxReaders;
        private readonly SemaphoreSlim _readPool;
        private readonly SemaphoreSlim _writePool;
        private readonly IRandom _random;
        private int _disposed = 0;

        /// <summary>
        /// Creates a new async-aware reader/writer lock which supports the given number of max readers.
        /// </summary>
        /// <param name="maxReaders">The maximum number of readers</param>
        public ReaderWriterLockAsync(int maxReaders = 16)
        {
            if (maxReaders < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxReaders));
            }

            _maxReaders = maxReaders;
            _readPool = new SemaphoreSlim(_maxReaders);
            _writePool = new SemaphoreSlim(1);
            _random = new FastRandom();
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~ReaderWriterLockAsync()
        {
            Dispose(false);
        }
#endif

        /// <summary>
        /// Acquires a read lock asynchronously.
        /// </summary>
        /// <returns>A token representing the read lock.</returns>
        public async Task<int> EnterReadLockAsync()
        {
            await _readPool.WaitAsync().ConfigureAwait(false);
            int token = READ_LOCK_TOKEN;
            return token;
        }

        /// <summary>
        /// Acquires a read lock asynchronously with the option to cancel.
        /// </summary>
        /// <param name="cancelToken">A cancellation token.</param>
        /// <returns>A token representing the read lock.</returns>
        public async Task<int> EnterReadLockAsync(CancellationToken cancelToken)
        {
            await _readPool.WaitAsync(cancelToken).ConfigureAwait(false);
            int token = READ_LOCK_TOKEN;
            return token;
        }

        /// <summary>
        /// Acquires a read lock synchronously.
        /// </summary>
        /// <returns>A token representing the read lock.</returns>
        public int EnterReadLock()
        {
            _readPool.Wait();
            int token = READ_LOCK_TOKEN;
            return token;
        }

        /// <summary>
        /// Releases a read lock using a token that was previously acquired.
        /// </summary>
        /// <param name="readToken">A read token that was received when acquiring the lock.</param>
        public void ExitReadLock(int readToken)
        {
            // Verify token
            if (readToken != READ_LOCK_TOKEN)
            {
                throw new InvalidOperationException("Cannot exit read lock when one is not held");
            }

            _readPool.Release();
        }

        /// <summary>
        /// Acquires a write lock asynchronously.
        /// </summary>
        /// <returns>A token representing the write lock.</returns>
        public async Task<int> EnterWriteLockAsync()
        {
            // Acquire the write handle
            await _writePool.WaitAsync().ConfigureAwait(false);

            // Then acquire all the read handles, allowing time for all current reads to finish
            for (int c = 0; c < _maxReaders; c++)
            {
                await _readPool.WaitAsync().ConfigureAwait(false);
            }
            
            return WRITE_LOCK_TOKEN;
        }

        /// <summary>
        /// Acquires a write lock asynchronously with the option to cancel.
        /// </summary>
        /// <param name="cancelToken">A cancellation token.</param>
        /// <returns>A token representing the write lock.</returns>
        public async Task<int> EnterWriteLockAsync(CancellationToken cancelToken)
        {
            int numReadLocksAcquired = 0;

            // Acquire the write handle
            await _writePool.WaitAsync(cancelToken).ConfigureAwait(false);
            try
            {
                // Then acquire all the read handles, allowing time for all current reads to finish
                while (numReadLocksAcquired < _maxReaders)
                {
                    await _readPool.WaitAsync(cancelToken).ConfigureAwait(false);
                    numReadLocksAcquired++;
                }
            }
            catch (OperationCanceledException)
            {
                // we have at least one lock but the user requested a cancellation - make sure we're not left in a bad state
                if (numReadLocksAcquired > 0)
                {
                    _readPool.Release(numReadLocksAcquired);
                }

                _writePool.Release();
            }

            return WRITE_LOCK_TOKEN;
        }

        /// <summary>
        /// Acquires a write lock synchronously
        /// </summary>
        /// <returns>A token representing the write lock.</returns>
        public int EnterWriteLock()
        {
            // Acquire the write handle
            _writePool.Wait();

            // Then acquire all the read handles, allowing time for all current reads to finish
            for (int c = 0; c < _maxReaders; c++)
            {
                _readPool.Wait();
            }

            return WRITE_LOCK_TOKEN;
        }

        /// <summary>
        /// Releases a write lock using a token that was previously acquired.
        /// </summary>
        /// <param name="writeToken">A write token that was received when acquiring the lock.</param>
        public void ExitWriteLock(int writeToken)
        {
            // Verify token
            if (writeToken != WRITE_LOCK_TOKEN)
            {
                throw new InvalidOperationException("Cannot exit write lock when it is not held");
            }

            // Release the reader pool
            _readPool.Release(_maxReaders);

            // Release the write lock
            _writePool.Release();
        }

        /// <summary>
        /// Given a read lock token, upgrade to a write lock token synchronously.
        /// </summary>
        /// <param name="readToken">A previously acquired read token</param>
        /// <returns>A write token</returns>
        public int UpgradeToWriteLock(int readToken)
        {
            // There's really nothing special going on here. This could definitely be optimized
            ExitReadLock(readToken);
            return EnterWriteLock();
        }

        /// <summary>
        /// Given a read lock token, upgrade to a write lock token asynchronously.
        /// </summary>
        /// <param name="readToken">A previously acquired read token</param>
        /// <returns>A write token</returns>
        public Task<int> UpgradeToWriteLockAsync(int readToken)
        {
            // There's really nothing special going on here
            ExitReadLock(readToken);
            return EnterWriteLockAsync();
        }

        /// <summary>
        /// Given a write lock token, downgrade to a read lock token synchronously.
        /// </summary>
        /// <param name="writeToken">A previously acquired write token</param>
        /// <returns>A read token</returns>
        public int DowngradeToReadLock(int writeToken)
        {
            // Verify token
            if (writeToken != WRITE_LOCK_TOKEN)
            {
                throw new InvalidOperationException("Cannot exit write lock when it is not held");
            }

            // Release all reader locks except for one
            _readPool.Release(_maxReaders - 1);

            // Release the write lock
            _writePool.Release();

            // Return a read lock
            return READ_LOCK_TOKEN;
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
                _readPool.Dispose();
                _writePool.Dispose();
            }
        }
    }
}
