using Durandal.Common.Logger;
using Durandal.Common.Utils;
using Durandal.Common.Cache;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Audio;
using Durandal.Common.IO;
using System.Threading;
using Durandal.API;
using Durandal.Common.Instrumentation;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Dialog.Services
{
    /// <summary>
    /// Implements a <see cref="IStreamingAudioCache"/> using only local memory.
    /// This should be considered the default baseline implementation.
    /// </summary>
    public class InMemoryStreamingAudioCache : IStreamingAudioCache
    {
        private readonly InMemoryCache<EndpointInternal> _endpointsWithInstrumentation;
        private int _disposed = 0;
        
        public InMemoryStreamingAudioCache()
        {
            _endpointsWithInstrumentation = new InMemoryCache<EndpointInternal>();
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~InMemoryStreamingAudioCache()
        {
            Dispose(false);
        }
#endif

        public void Clear()
        {
            _endpointsWithInstrumentation.Clear();
        }

        public async Task<RetrieveResult<IAudioDataSource>> TryGetAudioReadStream(string key, ILogger queryLogger, CancellationToken cancelToken, IRealTimeProvider realTime, TimeSpan? maxSpinTime = null)
        {
            RetrieveResult<EndpointInternal> rr = await _endpointsWithInstrumentation.TryRetrieve(key, queryLogger, realTime, maxSpinTime).ConfigureAwait(false);
            if (rr.Success)
            {
                TimeSpan timeSpentInCache = TimeSpan.FromTicks(realTime.TimestampTicks - rr.Result.TimeWrittenToCache);
                queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Dialog_StreamingAudioTimeInCache, timeSpentInCache), LogLevel.Ins);
                return new RetrieveResult<IAudioDataSource>(rr.Result.CreateNewReader(), rr.LatencyMs);
            }
            else
            {
                return new RetrieveResult<IAudioDataSource>(null, rr.LatencyMs, false);
            }
        }

        public async Task<NonRealTimeStream> CreateAudioWriteStream(string key, string codec, string codecParams, ILogger queryLogger, IRealTimeProvider realTime)
        {
            using (PipeStream pipe = new PipeStream())
            {
                EndpointInternal cacheItem = new EndpointInternal(codec, codecParams, pipe.GetReadStream(), realTime);
                await _endpointsWithInstrumentation.Store(key, cacheItem, null, TimeSpan.FromSeconds(60), false, queryLogger, realTime).ConfigureAwait(false);
                return pipe.GetWriteStream();
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
                Clear();
                _endpointsWithInstrumentation?.Dispose();
            }
        }

        /// <summary>
        /// Represents a cached audio endpoint which we can potentially read from multiple times.
        /// </summary>
        private class EndpointInternal : IDisposable
        {
            private readonly string _codec;
            private readonly string _codecParams;
            private readonly MultiReadStream _multiReadStream;

            // This only exists to force the MultiReadStream to guarantee that it always buffers the entire stream
            // It should really have a better mechanism to enforce maximum buffer length though
            private readonly NonRealTimeStream _placeholderCursor;
            private int _disposed = 0;

            public EndpointInternal(string codec, string codecParams, NonRealTimeStream pipeStream, IRealTimeProvider realTime)
            {
                _codec = codec;
                _codecParams = codecParams;
                _multiReadStream = new MultiReadStream(pipeStream);
                _placeholderCursor = _multiReadStream.CreateCursor(0);
                TimeWrittenToCache = realTime.TimestampTicks;
                DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
            }

#if TRACK_IDISPOSABLE_LEAKS
            ~EndpointInternal()
            {
                Dispose(false);
            }
#endif

            public long TimeWrittenToCache { get; private set; }

            public IAudioDataSource CreateNewReader()
            {
                return new InMemoryAudioDataSource(_codec, _codecParams, _multiReadStream.CreateCursor(0));
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
                    _placeholderCursor?.Dispose();
                }
            }
        }

        /// <summary>
        /// Data source for reading a copy of the audio data from an in-memory cache
        /// </summary>
        private class InMemoryAudioDataSource : IAudioDataSource
        {
            private int _disposed = 0;

            public InMemoryAudioDataSource(string codec, string codecParams, NonRealTimeStream stream)
            {
                Codec = codec;
                CodecParams = codecParams;
                AudioDataReadStream = stream;
            }

#if TRACK_IDISPOSABLE_LEAKS
            ~InMemoryAudioDataSource()
            {
                Dispose(false);
            }
#endif

            public string Codec { get; private set; }

            public string CodecParams { get; private set; }

            public NonRealTimeStream AudioDataReadStream { get; private set; }

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
                    AudioDataReadStream?.Dispose();
                }
            }
        }
    }
}
