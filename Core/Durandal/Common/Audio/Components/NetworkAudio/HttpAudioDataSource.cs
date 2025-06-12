using Durandal.Common.Net.Http;
using Durandal.Common.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Durandal.Common.Audio;
using Durandal.Common.Utils;
using Durandal.Common.Time;
using Durandal.Common.Tasks;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Dialog
{
    /// <summary>
    /// Stores the result of fetching a streaming audio response from an HTTP server response.
    /// </summary>
    public class HttpAudioDataSource : IAudioDataSource
    {
        private readonly HttpResponse _httpResponse;
        private int _disposed = 0;

        public NonRealTimeStream AudioDataReadStream { get; private set; }
        public string Codec { get; private set; }
        public string CodecParams { get; private set; }

        public HttpAudioDataSource(HttpResponse response, string codec, string codecParams)
        {
            _httpResponse = response;
            AudioDataReadStream = response.ReadContentAsStream();
            Codec = codec;
            CodecParams = codecParams;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~HttpAudioDataSource()
        {
            Dispose(false);
        }
#endif

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
                if (_httpResponse != null)
                {
                    // If all goes well, we have read the entire response already so this is a no-op.
                    // But in an error case this could cause blocking
                    _httpResponse.FinishAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).Await();
                    _httpResponse.Dispose();
                }
            }
        }
    }
}
