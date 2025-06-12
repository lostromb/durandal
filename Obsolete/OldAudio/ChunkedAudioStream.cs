using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Durandal.Common.Audio
{
    using Durandal.Common.Client;
    using Durandal.Common.Logger;
    using Durandal.Common.Time;
    using Durandal.Common.Utils;
    using System.ComponentModel;
    using System.Threading;

    // FIXME this entire class needs an overhaul. We should replace all streams + AudioChunk processing with a single
    // robust audio stream format with 32-bit floating point multichannel
    public class ChunkedAudioStream : IDisposable
    {
        // fixme the wait operations in this class should use a realtimeprovider
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1); // fixme semaphore is probably not the best design here
        private readonly Queue<AudioChunk> _buffer = new Queue<AudioChunk>();
        private readonly EventWaitHandle _writeSignal = new EventWaitHandle(false, EventResetMode.AutoReset);
        private bool _closed = false;
        private int _disposed = 0;

        public ChunkedAudioStream()
        {
            DataWritten = new AsyncEvent<AudioEventArgs>();
            Finished = new AsyncEvent<EventArgs>();
        }

        ~ChunkedAudioStream()
        {
            Dispose(false);
        }

        public bool EndOfStream
        {
            get
            {
                _lock.Wait();
                try
                {
                    return _closed && _buffer.Count == 0;
                }
                finally
                {
                    _lock.Release();
                }
            }
        }

        public bool Write(AudioChunk data, bool closeStream = false)
        {
            _lock.Wait();
            try
            {
                if (_closed)
                {
                    // Can't write to a closed stream
                    throw new ObjectDisposedException("Cannot write to a closed audio stream");
                }

                if (closeStream)
                {
                    _closed = true;
                }

                _buffer.Enqueue(data);
                _writeSignal.Set();
            }
            finally
            {
                _lock.Release();
            }

            OnDataWritten(data);
            if (closeStream)
            {
                OnFinished();
            }

            return true;
        }

        /// <summary>
        /// Attempts to read audio from the stream
        /// </summary>
        /// <param name="msToWait"></param>
        /// <returns></returns>
        public AudioChunk Read(int msToWait = 100)
        {
            if (!_lock.Wait(msToWait))
            {
                return null;
            }

            try
            {
                if (_closed && _buffer.Count == 0)
                {
                    // Stream is closed, return null
                    // And also set the write signal so any other waiting processes will stop blocking
                    _writeSignal.Set();
                    return null;
                }
            }
            finally
            {
                _lock.Release();
            }

            // Block on write if we need to do so (but unlock the mutex first so another thread can write)
            if (!_writeSignal.WaitOne(msToWait))
            {
                return null;
            }

            try
            {
                // Now actually read the data
                _lock.Wait();

                // Race condition check - make sure the stream has not been closed in between the last read + write
                if (_closed && _buffer.Count == 0)
                {
                    _writeSignal.Set();
                    return null;
                }

                AudioChunk returnVal = _buffer.Dequeue();
                if (_buffer.Count > 0)
                {
                    // Set the write flag which tells other readers that more data is available
                    _writeSignal.Set();
                }
                return returnVal;
            }
            finally
            {
                _lock.Release();
            }
        }

        public AsyncEvent<AudioEventArgs> DataWritten { get; private set; }

        public AsyncEvent<EventArgs> Finished { get; private set; }

        protected virtual void OnDataWritten(AudioChunk audio)
        {
            DataWritten.FireInBackground(this, new AudioEventArgs(audio), NullLogger.Singleton, DefaultRealTimeProvider.Singleton);
        }

        protected virtual void OnFinished()
        {
            Finished.FireInBackground(this, new EventArgs(), NullLogger.Singleton, DefaultRealTimeProvider.Singleton);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            {
                return;
            }

            if (!disposing) Durandal.Common.Utils.DebugMemoryLeaktracer.TraceDisposableItemFinalized(this.GetType());

            if (disposing)
            {
                _lock.Dispose();
                _writeSignal.Dispose();
            }
        }
    }
}


// Alternate implementation??
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;

//namespace Durandal.Common.Audio
//{
//    using Durandal.Common.Client;
//    using System.Collections.Concurrent;
//    using System.ComponentModel;
//    using System.Threading;
//    using Durandal.Common.Time;

//    public class ChunkedAudioStream
//    {
//        // fixme the wait operations in this class should use a realtimeprovider

//        private bool _closed;
//        private ConcurrentQueue<AudioChunk> _buffer;
//        private IRealTimeProvider _timeProvider;

//        public ChunkedAudioStream(IRealTimeProvider timeProvider = null)
//        {
//            _buffer = new ConcurrentQueue<AudioChunk>();
//            _closed = false;
//            _timeProvider = timeProvider ?? DefaultRealTimeProvider.Singleton;
//        }

//        public bool EndOfStream
//        {
//            get
//            {
//                return _closed && _buffer.Count == 0;
//            }
//        }

//        public bool Write(AudioChunk data, bool closeStream = false)
//        {
//            if (_closed)
//            {
//                // Can't write to a closed stream
//                throw new ObjectDisposedException("Cannot write to a closed audio stream");
//            }

//            if (closeStream)
//            {
//                _closed = true;
//            }

//            _buffer.Enqueue(data);

//            OnDataWritten(data);
//            if (closeStream)
//            {
//                OnFinished();
//            }

//            return true;
//        }

//        /// <summary>
//        /// Attempts to read audio from the stream. If no data is made available within the time specified by msToWait, this returns null.
//        /// If the stream is closed this also returns null.
//        /// </summary>
//        /// <param name="msToWait"></param>
//        /// <returns></returns>
//        public AudioChunk Read(int msToWait = 100)
//        {
//            // Now start waiting for input
//            int timeWaited = 0;
//            AudioChunk returnVal = null;
//            while (returnVal == null && timeWaited < msToWait)
//            {
//                // Race condition check - make sure the stream has not been closed in between the last read + write
//                if (EndOfStream)
//                {
//                    return null;
//                }

//                // Is there any data?
//                if (_buffer.TryDequeue(out returnVal))
//                {
//                    return returnVal;
//                }
//                else
//                {
//                    // Otherwise wait for a little bit
//                    _timeProvider.Wait(TimeSpan.FromMilliseconds(2), CancellationToken.None);
//                    timeWaited += 2;
//                }
//            }

//            // Timed out while waiting for incoming data.
//            return null;
//        }

//        public event EventHandler<AudioEventArgs> DataWritten;
//        public event EventHandler<EventArgs> Finished;

//        protected virtual void OnDataWritten(AudioChunk audio)
//        {
//            EventHandler<AudioEventArgs> handler = this.DataWritten;
//            if (handler != null)
//            {
//                handler(this, new AudioEventArgs(audio));
//            }
//        }

//        protected virtual void OnFinished()
//        {
//            EventHandler<EventArgs> handler = this.Finished;
//            if (handler != null)
//            {
//                handler(this, null);
//            }
//        }
//    }
//}
