using Durandal.Common.Net.Http;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Newtonsoft.Json.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net
{
    public class NetworkResponseInstrumented<T> : IDisposable
    {
        private int _disposed = 0;

        public NetworkResponseInstrumented(T response, int requestSize = 0, int responseSize = 0, double? sendLatency = null, double? remoteLatency = null, double? recvLatency = null)
        {
            Response = response;
            SendLatency = sendLatency;
            RemoteLatency = remoteLatency;
            RecieveLatency = recvLatency;
            RequestSize = requestSize;
            ResponseSize = responseSize;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~NetworkResponseInstrumented()
        {
            Dispose(false);
        }
#endif

        public T Response
        {
            get;
            private set;
        }

        public bool Success
        {
            get
            {
                return Response != null;
            }
        }

        public double? SendLatency
        {
            get;
            private set;
        }

        public double? RemoteLatency
        {
            get;
            private set;
        }

        public double? RecieveLatency
        {
            get;
            private set;
        }

        public int RequestSize
        {
            get;
            private set;
        }

        public int ResponseSize
        {
            get;
            private set;
        }

        public double NetworkLatency
        {
            get
            {
                return SendLatency.GetValueOrDefault(0) + RecieveLatency.GetValueOrDefault(0);
            }
        }

        public double EndToEndLatency
        {
            get
            {
                return SendLatency.GetValueOrDefault(0) + RecieveLatency.GetValueOrDefault(0) + RemoteLatency.GetValueOrDefault(0);
            }
        }

        public NetworkResponseInstrumented<E> Convert<E>(E newResponse)
        {
            return new NetworkResponseInstrumented<E>(newResponse, RequestSize, ResponseSize, SendLatency, RemoteLatency, RecieveLatency);
        }

        /// <summary>
        /// Transfers ownership of the internal Response object to the caller and then disposes of this object.
        /// Used when you just want to get the response and don't want any of the other instrumentation data.
        /// </summary>
        /// <returns></returns>
        public T UnboxAndDispose()
        {
            T returnVal = Response;
            Response = default(T);
            Dispose();
            return returnVal;
        }

        public Task FinishAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (Response != null && Response is HttpResponse)
            {
                return (Response as HttpResponse).FinishAsync(cancelToken, realTime);
            }

            return DurandalTaskExtensions.NoOpTask;
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
                IDisposable disposableResponse = Response as IDisposable;
                disposableResponse?.Dispose();
            }
        }
    }
}
