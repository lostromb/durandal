using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Durandal.Common.Tasks
{
    /// <summary>
    /// A very simple implementation of a reader-writer lock which doesn't require disposable system handles.
    /// It's basically just an array of Monitors. As such, it is not compatible with Async methods because it
    /// depends on thread identity. Do NOT await any methods while holding a lock!
    /// </summary>
    public class ReaderWriterLockMicro
    {
        private readonly object[] _mutexes; // the array of mutexes
        private readonly uint _numLocks; // number of mutexes
        private readonly uint _lockMask; // used as an optimization instead of the modulo operator
        private int _currentLockId; // monotonously incrementing unsigned int to enforce resource ordering and prevent deadlocks

        /// <summary>
        /// Creates a new micro reader-writer lock.
        /// </summary>
        /// <param name="maxReaders">The maximum number of readers to allow. This number MUST be a power of two.</param>
        public ReaderWriterLockMicro(int maxReaders = 4)
        {
            if (maxReaders < 1)
            {
                throw new ArgumentOutOfRangeException("Readers must be a positive integer");
            }

            if (maxReaders > 256)
            {
                throw new ArgumentOutOfRangeException("Too many readers, you mad lad");
            }

            int bitCount = 0;
            uint shiftedMaxReaders = (uint)maxReaders;
            _lockMask = 0;
            while (shiftedMaxReaders > 0)
            {
                if ((shiftedMaxReaders & 0x1) != 0)
                {
                    bitCount++;
                }

                shiftedMaxReaders = shiftedMaxReaders >> 1;
                _lockMask |= shiftedMaxReaders;
            }

            // Check that it's a power of two
            if (bitCount != 1)
            {
                throw new ArgumentException("The number of readers must be a power of two");
            }

            _currentLockId = -1;
            _numLocks = (uint)maxReaders;
            _mutexes = new object[_numLocks];
            for (int c = 0; c < _numLocks; c++)
            {
                _mutexes[c] = new object();
            }
        }

        /// <summary>
        /// Enters a single read lock, returning the lock handle.
        /// </summary>
        /// <returns>A handle to the lock.</returns>
        public uint EnterReadLock()
        {
            uint lockId = ((uint)Interlocked.Increment(ref _currentLockId)) & _lockMask;
            Monitor.Enter(_mutexes[lockId]);
            return lockId;
        }

        /// <summary>
        /// Exits a previously entered read lock.
        /// </summary>
        /// <param name="hRead">The handle that was acquired when the lock was entered.</param>
        public void ExitReadLock(uint hRead)
        {
            Monitor.Exit(_mutexes[hRead]);
        }

        /// <summary>
        /// Enters the write lock.
        /// </summary>
        public void EnterWriteLock()
        {
            for (uint c = 0; c < _numLocks; c++)
            {
                Monitor.Enter(_mutexes[c]);
            }
        }

        /// <summary>
        /// Exits the write lock.
        /// </summary>
        public void ExitWriteLock()
        {
            for (uint c = 0; c < _numLocks; c++)
            {
                Monitor.Exit(_mutexes[c]);
            }
        }
    }
}
