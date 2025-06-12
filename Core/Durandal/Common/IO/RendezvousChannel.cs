using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.IO
{
    /// <summary>
    /// Implements a CSP or Go-style unbuffered channel between two processes.
    /// </summary>
    public class RendezvousChannel<T> : IChannel<T>
    {
        private AutoResetEventAsync _producerWroteValue = new AutoResetEventAsync(false);
        private AutoResetEventAsync _producerFinished = new AutoResetEventAsync(true);
        private AutoResetEventAsync _consumerIsWaiting = new AutoResetEventAsync(false);
        private AutoResetEventAsync _consumerFinished = new AutoResetEventAsync(true);
        private T _value = default(T);
        
        public void Send(T data)
        {
            _consumerFinished.Wait();
            _value = data;
            _producerWroteValue.Set();
            _consumerIsWaiting.Wait();
            _producerFinished.Set();
        }

        public async ValueTask SendAsync(T data)
        {
            await _consumerFinished.WaitAsync().ConfigureAwait(false);
            _value = data;
            _producerWroteValue.Set();
            await _consumerIsWaiting.WaitAsync().ConfigureAwait(false);
            _producerFinished.Set();
        }

        /// <summary>
        /// Spinwaits until another process sends an object down this channel
        /// </summary>
        /// <returns></returns>
        public T Receive(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            _producerFinished.Wait();
            _consumerIsWaiting.Set();
            _producerWroteValue.Wait();
            T returnVal = _value;
            _value = default(T);
            _consumerFinished.Set();
            return returnVal;
        }

        public async ValueTask<T> ReceiveAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            await _producerFinished.WaitAsync().ConfigureAwait(false);
            _consumerIsWaiting.Set();
            await _producerWroteValue.WaitAsync().ConfigureAwait(false);
            T returnVal = _value;
            _value = default(T);
            _consumerFinished.Set();
            return returnVal;
        }

        public RetrieveResult<T> TryReceive(CancellationToken cancelToken, IRealTimeProvider realTime, TimeSpan timeout)
        {
            throw new NotImplementedException("Conditional receive is not yet implemented for rendezvous channel");
        }

        public ValueTask<RetrieveResult<T>> TryReceiveAsync(CancellationToken cancelToken, IRealTimeProvider realTime, TimeSpan timeout)
        {
            throw new NotImplementedException("Conditional receive is not yet implemented for rendezvous channel");
        }

        public void Clear()
        {
        }
    }
}
