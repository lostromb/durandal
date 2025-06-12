using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.LU
{
    public interface ILUClient
    {
        Task<IDictionary<string, string>> GetStatus(ILogger queryLogger = null, CancellationToken cancelToken = default(CancellationToken), IRealTimeProvider realTime = null);

        Task<NetworkResponseInstrumented<LUResponse>> MakeQueryRequest(LURequest request, ILogger queryLogger = null, CancellationToken cancelToken = default(CancellationToken), IRealTimeProvider realTime = null);
    }
}
