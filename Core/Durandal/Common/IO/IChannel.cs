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
    /// Defines a channel for safely passing objects between two threads or tasks. Implementations of this
    /// interface might include buffered channels or CSP-style rendevous channels.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IChannel<T> 
    {
        T Receive(CancellationToken cancelToken, IRealTimeProvider realTime);

        ValueTask<T> ReceiveAsync(CancellationToken cancelToken, IRealTimeProvider realTime);

        RetrieveResult<T> TryReceive(CancellationToken cancelToken, IRealTimeProvider realTime, TimeSpan timeout);

        ValueTask<RetrieveResult<T>> TryReceiveAsync(CancellationToken cancelToken, IRealTimeProvider realTime, TimeSpan timeout);

        void Send(T obj);

        ValueTask SendAsync(T obj);

        void Clear();
    }
}
